using System;
using Meta.XR;
using UnityEngine;

public class PhotoCapture : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private PassthroughCameraAccess passthroughCamera;
    [SerializeField] private Camera headCamera;      // Fallback when the passthrough camera is unavailable
    [SerializeField] private Camera captureCamera;   // Disabled camera used to render the virtual layer

    [Header("Output")]
    [SerializeField] private PhotoObject photoPrefab;
    [SerializeField] private PhotoLibrary library;
    [SerializeField] private AudioSource shutterSound;
    [SerializeField] private Shader alphaOverShader;

    [Header("Virtual layer")]
    [SerializeField] private LayerMask captureCullingMask = ~0;
    [SerializeField] private Vector2Int fallbackResolution = new Vector2Int(1280, 960);
    [SerializeField] private float nearClip = 0.05f;
    [SerializeField] private float farClip = 100f;

    private static readonly int OverlayTexId = Shader.PropertyToID("_OverlayTex");

    private Material alphaOverMaterial;

    private void Awake()
    {
        captureCamera.enabled = false;
        alphaOverMaterial = new Material(alphaOverShader);

        // Photos, the boundary frame, hands and the gallery should never appear in a capture
        foreach (string layerName in new[] { "Photo", "CaptureIgnore" })
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                captureCullingMask &= ~(1 << layer);
            }
            else
            {
                Debug.LogWarning($"PhotoCapture: layer '{layerName}' is missing. Add it in Tags & Layers.");
            }
        }
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // PassthroughCameraAccess waits for this permission but does not request it
        const string cameraPermission = "horizonos.permission.HEADSET_CAMERA";
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cameraPermission))
        {
            UnityEngine.Android.Permission.RequestUserPermission(cameraPermission);
        }
#endif
    }

    private void OnDestroy()
    {
        Destroy(alphaOverMaterial);
    }

    /// <summary>
    /// Captures the area inside the given world-space frame corners and spawns a photo at <paramref name="framePose"/>.
    /// </summary>
    public void Capture(Vector3[] frameCornersWorld, Pose framePose, float width, float height)
    {
        Texture baseImage;
        Vector2Int resolution;
        Func<Vector3, Vector2> worldToViewport;

        if (passthroughCamera != null && passthroughCamera.enabled && passthroughCamera.IsPlaying)
        {
            Pose cameraPose = passthroughCamera.GetCameraPose();
            baseImage = passthroughCamera.GetTexture();
            resolution = passthroughCamera.CurrentResolution;
            worldToViewport = p => passthroughCamera.WorldToViewportPoint(p, cameraPose);

            captureCamera.transform.SetPositionAndRotation(cameraPose.position, cameraPose.rotation);
            captureCamera.projectionMatrix = ProjectionFromIntrinsics(passthroughCamera.Intrinsics, resolution);
        }
        else
        {
            Debug.LogWarning("PhotoCapture: passthrough camera is not playing, capturing virtual content only.");
            baseImage = Texture2D.blackTexture;
            resolution = fallbackResolution;
            worldToViewport = p => captureCamera.WorldToViewportPoint(p);

            captureCamera.transform.SetPositionAndRotation(headCamera.transform.position, headCamera.transform.rotation);
            captureCamera.ResetProjectionMatrix();
            captureCamera.fieldOfView = headCamera.fieldOfView;
            captureCamera.aspect = (float)resolution.x / resolution.y;
            captureCamera.nearClipPlane = nearClip;
            captureCamera.farClipPlane = farClip;
        }

        if (!TryGetCropRect(frameCornersWorld, worldToViewport, out Rect crop))
        {
            Debug.LogWarning("PhotoCapture: frame is outside the camera view, nothing captured.");
            return;
        }

        RenderTexture virtualLayer = RenderTexture.GetTemporary(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32);
        RenderTexture composite = RenderTexture.GetTemporary(resolution.x, resolution.y, 0, RenderTextureFormat.ARGB32);

        // 1. Render the virtual scene from the capture camera onto a transparent background
        captureCamera.cullingMask = captureCullingMask;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = Color.clear;
        captureCamera.targetTexture = virtualLayer;
        captureCamera.Render();
        captureCamera.targetTexture = null;

        // 2. Composite it over the real-world image
        alphaOverMaterial.SetTexture(OverlayTexId, virtualLayer);
        Graphics.Blit(baseImage, composite, alphaOverMaterial);

        // 3. Crop to the framed area
        int photoWidth = Mathf.Max(1, Mathf.RoundToInt(crop.width * resolution.x));
        int photoHeight = Mathf.Max(1, Mathf.RoundToInt(crop.height * resolution.y));
        var photo = new RenderTexture(photoWidth, photoHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "Photo",
            wrapMode = TextureWrapMode.Clamp,
        };
        photo.Create();
        Graphics.Blit(composite, photo, crop.size, crop.position);

        RenderTexture.ReleaseTemporary(virtualLayer);
        RenderTexture.ReleaseTemporary(composite);

        // 4. Spawn the photo where the frame was and store it
        if (shutterSound != null)
        {
            shutterSound.Play();
        }

        PhotoEntry entry = library != null
            ? library.Add(photo, width, height)
            : new PhotoEntry { WidthM = width, HeightM = height, Taken = DateTime.Now };

        PhotoObject photoObject = Instantiate(photoPrefab, framePose.position, framePose.rotation);
        photoObject.Init(entry, photo, flash: true);
    }

    private static bool TryGetCropRect(Vector3[] corners, Func<Vector3, Vector2> worldToViewport, out Rect crop)
    {
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        foreach (Vector3 corner in corners)
        {
            Vector2 viewport = worldToViewport(corner);
            min = Vector2.Min(min, viewport);
            max = Vector2.Max(max, viewport);
        }

        min = Vector2.Max(min, Vector2.zero);
        max = Vector2.Min(max, Vector2.one);
        crop = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return crop.width > 0f && crop.height > 0f;
    }

    /// <summary>
    /// Builds an off-axis projection matching the passthrough camera image, so virtual content
    /// lines up with the real world. Mirrors the sensor crop math in PassthroughCameraAccess.
    /// </summary>
    private Matrix4x4 ProjectionFromIntrinsics(PassthroughCameraAccess.CameraIntrinsics intrinsics, Vector2Int resolution)
    {
        Vector2 sensor = intrinsics.SensorResolution;
        Vector2 scale = (Vector2)resolution / sensor;
        scale /= Mathf.Max(scale.x, scale.y);
        Rect cropRegion = new Rect(
            sensor.x * (1f - scale.x) * 0.5f,
            sensor.y * (1f - scale.y) * 0.5f,
            sensor.x * scale.x,
            sensor.y * scale.y);

        Vector2 focal = intrinsics.FocalLength;
        Vector2 principal = intrinsics.PrincipalPoint;

        var projection = Matrix4x4.zero;
        projection[0, 0] = 2f * focal.x / cropRegion.width;
        projection[0, 2] = 1f - 2f * (principal.x - cropRegion.x) / cropRegion.width;
        projection[1, 1] = 2f * focal.y / cropRegion.height;
        projection[1, 2] = 1f - 2f * (principal.y - cropRegion.y) / cropRegion.height;
        projection[2, 2] = -(farClip + nearClip) / (farClip - nearClip);
        projection[2, 3] = -2f * farClip * nearClip / (farClip - nearClip);
        projection[3, 2] = -1f;
        return projection;
    }
}
