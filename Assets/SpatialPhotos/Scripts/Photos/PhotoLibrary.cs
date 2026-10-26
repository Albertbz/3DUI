using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace SpatialPhotos
{
    public class PhotoEntry
    {
        public string PngPath;
        public float WidthM;
        public float HeightM;
        public DateTime Taken;
        public Texture2D Thumbnail;
    }

    public class PhotoLibrary : MonoBehaviour
    {
        [Serializable]
        private class PhotoMetadata
        {
            public float widthM;
            public float heightM;
        }

        public static PhotoLibrary Instance { get; private set; }

        [SerializeField] private int thumbnailSize = 256;
        [SerializeField] private float fallbackHeight = 0.3f; // Used when a PNG has no metadata file

        // Newest first
        private readonly List<PhotoEntry> entries = new List<PhotoEntry>();
        public IReadOnlyList<PhotoEntry> Entries => entries;

        public event Action<PhotoEntry> OnPhotoAdded;
        public event Action OnLibraryLoaded;

        private string PhotoDirectory => Path.Combine(Application.persistentDataPath, "Photos");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple PhotoLibrary instances found. Destroying duplicate.");
                Destroy(this);
                return;
            }
            Instance = this;
            Directory.CreateDirectory(PhotoDirectory);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Start()
        {
            var files = new List<FileInfo>(new DirectoryInfo(PhotoDirectory).GetFiles("*.png"));
            files.Sort((a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));

            foreach (var file in files)
            {
                var entry = new PhotoEntry { PngPath = file.FullName, Taken = file.CreationTime };

                // Decode one image per frame to avoid hitching on startup
                Texture2D full = LoadTextureFromDisk(entry.PngPath);
                if (full == null)
                {
                    continue;
                }

                if (!TryReadMetadata(entry))
                {
                    entry.HeightM = fallbackHeight;
                    entry.WidthM = fallbackHeight * full.width / full.height;
                }

                entry.Thumbnail = CreateThumbnail(full);
                Destroy(full);
                entries.Add(entry);
                yield return null;
            }

            Debug.Log($"PhotoLibrary loaded {entries.Count} photos from {PhotoDirectory}");
            OnLibraryLoaded?.Invoke();
        }

        /// <summary>
        /// Saves the photo to disk (PNG + metadata) and adds it to the front of the library.
        /// The render texture is copied, so the caller may release it after this call.
        /// </summary>
        public PhotoEntry Add(RenderTexture photo, float widthM, float heightM)
        {
            var entry = new PhotoEntry
            {
                PngPath = Path.Combine(PhotoDirectory, $"photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png"),
                WidthM = widthM,
                HeightM = heightM,
                Taken = DateTime.Now,
            };

            entry.Thumbnail = CreateThumbnail(photo);
            entries.Insert(0, entry);
            OnPhotoAdded?.Invoke(entry);

            int width = photo.width;
            int height = photo.height;
            string metadataJson = JsonUtility.ToJson(new PhotoMetadata { widthM = widthM, heightM = heightM });

            AsyncGPUReadback.Request(photo, 0, TextureFormat.RGBA32, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("PhotoLibrary: GPU readback failed, photo not saved.");
                    return;
                }

                NativeArray<byte> png = ImageConversion.EncodeNativeArrayToPNG(
                    request.GetData<byte>(), GraphicsFormat.R8G8B8A8_SRGB, (uint)width, (uint)height);
                byte[] pngBytes = png.ToArray();
                png.Dispose();

                WriteFilesAsync(entry.PngPath, pngBytes, metadataJson);
            });

            return entry;
        }

        /// <summary>Loads the full resolution image for an entry. Caller owns the returned texture.</summary>
        public Texture2D LoadFullTexture(PhotoEntry entry)
        {
            return LoadTextureFromDisk(entry.PngPath);
        }

        private static async void WriteFilesAsync(string pngPath, byte[] pngBytes, string metadataJson)
        {
            try
            {
                await Task.Run(() =>
                {
                    File.WriteAllBytes(pngPath, pngBytes);
                    File.WriteAllText(Path.ChangeExtension(pngPath, ".json"), metadataJson);
                });
                Debug.Log($"Photo saved to {pngPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"PhotoLibrary: failed to save {pngPath}: {e}");
            }
        }

        private static bool TryReadMetadata(PhotoEntry entry)
        {
            string jsonPath = Path.ChangeExtension(entry.PngPath, ".json");
            if (!File.Exists(jsonPath))
            {
                return false;
            }

            try
            {
                var metadata = JsonUtility.FromJson<PhotoMetadata>(File.ReadAllText(jsonPath));
                if (metadata.widthM <= 0f || metadata.heightM <= 0f)
                {
                    return false;
                }
                entry.WidthM = metadata.widthM;
                entry.HeightM = metadata.heightM;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PhotoLibrary: could not read {jsonPath}: {e.Message}");
                return false;
            }
        }

        private static Texture2D LoadTextureFromDisk(string path)
        {
            if (!File.Exists(path))
            {
                // The PNG may still be in the middle of being written right after a capture
                Debug.LogWarning($"PhotoLibrary: {path} does not exist (yet).");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), true))
            {
                Debug.LogWarning($"PhotoLibrary: could not decode {path}");
                Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private Texture2D CreateThumbnail(Texture source)
        {
            float scale = Mathf.Min(1f, (float)thumbnailSize / Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, temp);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temp;
            var thumbnail = new Texture2D(width, height, TextureFormat.RGBA32, false);
            thumbnail.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            thumbnail.Apply(false, true); // Upload and drop the CPU copy
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);

            thumbnail.wrapMode = TextureWrapMode.Clamp;
            return thumbnail;
        }
    }
}
