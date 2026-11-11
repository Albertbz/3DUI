using System;
using System.Collections.Generic;
using Meta.XR;
using UnityEngine;

namespace SpatialPhotos
{
    public class PhotoCapture : MonoBehaviour
    {
        // Empty scene references (passthrough camera, head camera, library, shutter sound) are found at startup
        [Header("Sources")]
        [Tooltip("One of the two passthrough cameras; the other side is created at startup when the scene has only one")]
        [SerializeField] private PassthroughCameraAccess passthroughCamera;
        [SerializeField] private Camera headCamera;      // Fallback when the passthrough camera is unavailable
        [SerializeField] private Camera captureCamera;   // Disabled camera used to render the virtual layer; created when empty

        [Header("Output")]
        [SerializeField] private PhotoObject photoPrefab;
        [SerializeField] private PhotoLibrary library;   // Optional: without one, photos are not saved
        [SerializeField] private AudioSource shutterSound;
        [SerializeField] private Shader alphaOverShader;
        [SerializeField] private Shader warpShader;

        [Header("Virtual layer")]
        [SerializeField] private LayerMask captureCullingMask = ~0;
        [SerializeField] private Vector2Int fallbackResolution = new Vector2Int(1280, 960); // Head camera's field of view without passthrough
        [SerializeField] private int maxPhotoSize = 2048;
        [SerializeField] private float nearClip = 0.05f;
        [SerializeField] private float farClip = 100f;

        [Header("Real-world layer")]
        [Tooltip("Assumed distance to the real world behind the frame when the environment depth raycast has no answer")]
        [SerializeField] private float fallbackDepth = 2f;
        [SerializeField] private EnvironmentRaycastManager raycastManager; // Created when empty and supported
        [Tooltip("Camera images kept while the frame is up, so the photo can come from just before the pinch release. About 6.5 MB each at 1280x1280, for each of the two cameras")]
        [SerializeField] private int maxSnapshots = 10;
        [SerializeField] private float snapshotSeconds = 0.4f;
        [Tooltip("How far past the camera images' edges the frame may reach, as a share of the image (a little edge stretch)")]
        [SerializeField] private float coverageAllowance = 0.02f;

        private static readonly int OverlayTexId = Shader.PropertyToID("_OverlayTex");
        private static readonly int HomographyId = Shader.PropertyToID("_Homography");
        private static readonly int SecondTexId = Shader.PropertyToID("_SecondTex");
        private static readonly int SecondHomographyId = Shader.PropertyToID("_SecondHomography");
        private static readonly int UseSecondId = Shader.PropertyToID("_UseSecond");

        private Material alphaOverMaterial;
        private Material warpMaterial;

        /// <summary>A copy of one passthrough image with the camera pose it was taken from.</summary>
        private struct Snapshot
        {
            public RenderTexture Image;
            public Pose CameraPose;
            public float Time;
        }

        /// <summary>One passthrough camera with its recent images.</summary>
        private class Feed
        {
            public PassthroughCameraAccess Camera;
            public readonly List<Snapshot> Snapshots = new List<Snapshot>(); // Oldest first

            public bool IsPlaying => Camera != null && Camera.enabled && Camera.IsPlaying;
        }

        // Both cameras: the frame often reaches past the edge of the one on the dominant eye's side, and the other
        // one fills in what it misses
        private readonly Feed leftFeed = new Feed();
        private readonly Feed rightFeed = new Feed();
        private bool recording;

        private Feed DominantFeed => DominantEye.Current == Eye.Left ? leftFeed : rightFeed;
        private Feed OtherFeed => DominantEye.Current == Eye.Left ? rightFeed : leftFeed;

        private void Awake()
        {
            if (captureCamera == null)
            {
                var cameraObject = new GameObject("CaptureCamera");
                cameraObject.transform.SetParent(transform, false);
                captureCamera = cameraObject.AddComponent<Camera>();
                captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
            }
            captureCamera.enabled = false;

            if (library == null) library = FindFirstObjectByType<PhotoLibrary>();
            if (shutterSound == null) TryGetComponent(out shutterSound);
            if (passthroughCamera == null) passthroughCamera = FindFirstObjectByType<PassthroughCameraAccess>();
            if (alphaOverShader == null) alphaOverShader = Shader.Find("Hidden/3DUI/AlphaOver");
            alphaOverMaterial = new Material(alphaOverShader);
            if (warpShader == null) warpShader = Shader.Find("Hidden/3DUI/QuadWarp");
            warpMaterial = new Material(warpShader);

            // Photos, the boundary frame, hands and the gallery should never appear in a capture
            foreach (string layerName in CaptureLayers.All)
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0)
                {
                    captureCullingMask &= ~(1 << layer);
                }
                else
                {
                    CaptureLayers.WarnMissing(layerName);
                }
            }
        }

        private void OnDisable()
        {
            EndRecording();
        }

        private void Start()
        {
            if (headCamera == null) headCamera = XRRig.HeadCamera;
            if (passthroughCamera == null)
            {
                Debug.LogWarning("PhotoCapture: no PassthroughCameraAccess in the scene, photos will contain virtual content only.", this);
            }
            else
            {
                SetUpFeeds();
            }

            if (raycastManager == null) raycastManager = FindFirstObjectByType<EnvironmentRaycastManager>();
            if (raycastManager == null && EnvironmentRaycastManager.IsSupported)
            {
                raycastManager = gameObject.AddComponent<EnvironmentRaycastManager>();
            }
            if (raycastManager == null)
            {
                Debug.Log($"PhotoCapture: no environment depth, the real world is assumed {fallbackDepth} m behind the frame.", this);
            }

    #if UNITY_ANDROID && !UNITY_EDITOR
            // PassthroughCameraAccess and EnvironmentRaycastManager wait for these permissions but do not request them
            string[] permissions = { "horizonos.permission.HEADSET_CAMERA", OVRPermissionsRequester.ScenePermission };
            var missing = Array.FindAll(permissions, p => !UnityEngine.Android.Permission.HasUserAuthorizedPermission(p));
            if (missing.Length > 0)
            {
                UnityEngine.Android.Permission.RequestUserPermissions(missing);
            }
    #endif
            // Room scan for snapping photos to surfaces; retried by the photos once the permission is granted
            SurfaceSnap.LoadScene();
        }

        /// <summary>
        /// Starts keeping recent camera images, so <see cref="Capture"/> can use one from before the moment it is
        /// called. <see cref="ImageBoundaryBox"/> records while the frame is up.
        /// </summary>
        public void BeginRecording()
        {
            recording = true;
        }

        public void EndRecording()
        {
            recording = false;
            foreach (Feed feed in new[] { leftFeed, rightFeed })
            {
                foreach (Snapshot snapshot in feed.Snapshots)
                {
                    RenderTexture.ReleaseTemporary(snapshot.Image);
                }
                feed.Snapshots.Clear();
            }
        }

        private void LateUpdate()
        {
            if (!recording)
            {
                return;
            }
            Record(leftFeed);
            Record(rightFeed);
        }

        private void Record(Feed feed)
        {
            if (!feed.IsPlaying || !feed.Camera.IsUpdatedThisFrame)
            {
                return;
            }

            Texture source = feed.Camera.GetTexture();
            RenderTexture image = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, image);
            List<Snapshot> snapshots = feed.Snapshots;
            snapshots.Add(new Snapshot { Image = image, CameraPose = feed.Camera.GetCameraPose(), Time = Time.time });

            while (snapshots.Count > 1 && (snapshots.Count > maxSnapshots || snapshots[0].Time < Time.time - snapshotSeconds))
            {
                RenderTexture.ReleaseTemporary(snapshots[0].Image);
                snapshots.RemoveAt(0);
            }
        }

        private void OnDestroy()
        {
            EndRecording();
            Destroy(alphaOverMaterial);
            Destroy(warpMaterial);
        }

        /// <summary>
        /// Captures what <paramref name="eyePosition"/> sees through the given world-space frame corners and spawns a
        /// photo at <paramref name="framePose"/>. The frame may be turned away from the eye: <see cref="ImageBoundaryBox"/>
        /// faces it toward the head's center while the photo comes from the dominant eye. Corners go bottom-left, bottom-right, top-right, top-left as seen from the eye. The real world comes from
        /// the camera image recorded at or before <paramref name="atTime"/> (a <see cref="Time.time"/>), or the live
        /// image when nothing was recorded.
        /// </summary>
        public void Capture(Vector3[] frameCornersWorld, Pose framePose, float width, float height, Vector3 eyePosition, float atTime)
        {
            float frameDistance = Vector3.Distance(eyePosition, framePose.position);
            float verticalFov = 2f * Mathf.Atan(height * 0.5f / frameDistance) * Mathf.Rad2Deg;

            Texture baseImage;
            Matrix4x4 warp;
            int photoHeight;

            // The dominant eye's side gives the image; the other camera, when playing, fills in past its edges
            Feed primary = DominantFeed.IsPlaying ? DominantFeed : OtherFeed;
            Feed secondary = primary == DominantFeed && OtherFeed.IsPlaying ? OtherFeed : null;
            warpMaterial.SetFloat(UseSecondId, 0f);

            if (primary.IsPlaying)
            {
                // The real world seen through the frame, taken as a plane at the scene's depth: each corner's ray from
                // the eye hits that plane, and those hits are found in the passthrough image
                float depth = SceneDepth(eyePosition, framePose.position, frameDistance);
                var sceneCorners = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    sceneCorners[i] = eyePosition + (frameCornersWorld[i] - eyePosition) * (depth / frameDistance);
                }

                Vector2[] quad = FrameInImage(primary, sceneCorners, atTime, out baseImage, out string imageAge);
                bool inPrimary = OverlapsViewport(quad);
                bool inSecondary = false;
                if (secondary != null)
                {
                    Vector2[] secondQuad = FrameInImage(secondary, sceneCorners, atTime, out Texture secondImage, out _);
                    inSecondary = OverlapsViewport(secondQuad);
                    if (inSecondary)
                    {
                        warpMaterial.SetTexture(SecondTexId, secondImage);
                        warpMaterial.SetMatrix(SecondHomographyId, SquareToQuad(secondQuad));
                        warpMaterial.SetFloat(UseSecondId, 1f);
                    }
                }

                if (!inPrimary && !inSecondary)
                {
                    Debug.LogWarning("PhotoCapture: frame is outside the camera view, nothing captured.");
                    return;
                }

                Vector2Int resolution = primary.Camera.CurrentResolution;
                warp = SquareToQuad(quad);
                // As many pixels as the frame covers in the camera image
                float leftEdge = Vector2.Scale(quad[3] - quad[0], resolution).magnitude;
                float rightEdge = Vector2.Scale(quad[2] - quad[1], resolution).magnitude;
                photoHeight = Mathf.RoundToInt(Mathf.Max(leftEdge, rightEdge));
                Debug.Log($"PhotoCapture: {DominantEye.Current} eye, {primary.Camera.CameraPosition} camera" +
                          (inSecondary ? $" filled in by {secondary.Camera.CameraPosition}" : "") +
                          $", scene depth {depth:F2} m, frame at {frameDistance:F2} m, {imageAge}.");
            }
            else
            {
                Debug.LogWarning("PhotoCapture: passthrough camera is not playing, capturing virtual content only.");
                baseImage = Texture2D.blackTexture;
                warp = Matrix4x4.identity;
                photoHeight = Mathf.RoundToInt(fallbackResolution.y * verticalFov / headCamera.fieldOfView);
            }

            // Keep the frame's aspect when the longer side hits the size limit
            float scale = Mathf.Min(1f, maxPhotoSize / (float)photoHeight, maxPhotoSize / (photoHeight * width / height));
            int photoWidth = Mathf.Clamp(Mathf.RoundToInt(photoHeight * scale * width / height), 1, maxPhotoSize);
            photoHeight = Mathf.Clamp(Mathf.RoundToInt(photoHeight * scale), 1, maxPhotoSize);

            RenderTexture virtualLayer = RenderTexture.GetTemporary(photoWidth, photoHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture realLayer = RenderTexture.GetTemporary(photoWidth, photoHeight, 0, RenderTextureFormat.ARGB32);

            // 1. Render the virtual scene from the eye through the frame onto a transparent background. The camera
            //    looks along the frame's normal, so the frame is an off-center window on its image plane
            captureCamera.transform.SetPositionAndRotation(eyePosition, framePose.rotation);
            captureCamera.nearClipPlane = nearClip;
            captureCamera.farClipPlane = farClip;
            captureCamera.projectionMatrix = FrustumThrough(frameCornersWorld[0], frameCornersWorld[2], eyePosition, framePose.rotation);
            captureCamera.cullingMask = captureCullingMask;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.clear;
            captureCamera.targetTexture = virtualLayer;
            captureCamera.Render();
            captureCamera.targetTexture = null;

            // 2. Warp the framed part of the real-world image into a rectangle
            warpMaterial.SetMatrix(HomographyId, warp);
            Graphics.Blit(baseImage, realLayer, warpMaterial);

            // 3. Composite the virtual layer over it
            var photo = new RenderTexture(photoWidth, photoHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "Photo",
                wrapMode = TextureWrapMode.Clamp,
            };
            photo.Create();
            alphaOverMaterial.SetTexture(OverlayTexId, virtualLayer);
            Graphics.Blit(realLayer, photo, alphaOverMaterial);

            RenderTexture.ReleaseTemporary(virtualLayer);
            RenderTexture.ReleaseTemporary(realLayer);

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

        /// <summary>
        /// Where <paramref name="sceneCorners"/> appear in the feed's image from <paramref name="atTime"/> (the live
        /// image when nothing was recorded), in viewport coordinates.
        /// </summary>
        private static Vector2[] FrameInImage(Feed feed, Vector3[] sceneCorners, float atTime, out Texture image, out string imageAge)
        {
            image = feed.Camera.GetTexture();
            Pose cameraPose = feed.Camera.GetCameraPose();
            imageAge = "live image";
            if (TryGetSnapshot(feed.Snapshots, atTime, out Snapshot snapshot))
            {
                image = snapshot.Image;
                cameraPose = snapshot.CameraPose;
                imageAge = $"image from {Time.time - snapshot.Time:F2} s ago";
            }

            var quad = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                quad[i] = feed.Camera.WorldToViewportPoint(sceneCorners[i], cameraPose);
            }
            return quad;
        }

        /// <summary>The newest recorded image from at or before <paramref name="time"/>, or the oldest one.</summary>
        private static bool TryGetSnapshot(List<Snapshot> snapshots, float time, out Snapshot snapshot)
        {
            snapshot = default;
            if (snapshots.Count == 0)
            {
                return false;
            }
            snapshot = snapshots[0];
            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                if (snapshots[i].Time <= time)
                {
                    snapshot = snapshots[i];
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// Distance from the eye to the real world behind the frame's center, from the environment depth raycast,
        /// or <see cref="fallbackDepth"/> without one. Never nearer than the frame itself.
        /// </summary>
        private float SceneDepth(Vector3 eyePosition, Vector3 frameCenter, float frameDistance)
        {
            float depth = fallbackDepth;
            if (raycastManager != null && raycastManager.isActiveAndEnabled)
            {
                var ray = new Ray(eyePosition, frameCenter - eyePosition);
                if (raycastManager.Raycast(ray, out EnvironmentRaycastHit hit, farClip))
                {
                    depth = Vector3.Distance(eyePosition, hit.point);
                }
            }
            return Mathf.Max(depth, frameDistance);
        }

        /// <summary>
        /// Projection from <paramref name="eyePosition"/>, looking along <paramref name="rotation"/> (the frame's), whose
        /// edges pass through the frame's bottom-left and top-right corners.
        /// </summary>
        private Matrix4x4 FrustumThrough(Vector3 bottomLeft, Vector3 topRight, Vector3 eyePosition, Quaternion rotation)
        {
            Quaternion toLocal = Quaternion.Inverse(rotation);
            Vector3 min = toLocal * (bottomLeft - eyePosition);
            Vector3 max = toLocal * (topRight - eyePosition);
            float near = nearClip;
            return Matrix4x4.Frustum(min.x / min.z * near, max.x / max.z * near, min.y / min.z * near, max.y / max.z * near,
                near, farClip);
        }

        /// <summary>
        /// What the passthrough cameras can see right now, as seen from one eye through a frame, so the frame can be kept
        /// inside it. Make one with <see cref="BeginCoverage"/>.
        /// </summary>
        public class Coverage
        {
            internal readonly List<(PassthroughCameraAccess Camera, Pose Pose)> Cameras = new List<(PassthroughCameraAccess, Pose)>();
            internal Vector3 Eye;
            internal float DepthScale;
            internal float Allowance;

            /// <summary>True when the real world behind <paramref name="framePoint"/> is in at least one camera image.</summary>
            public bool Sees(Vector3 framePoint)
            {
                Vector3 onScene = Eye + (framePoint - Eye) * DepthScale;
                foreach ((PassthroughCameraAccess camera, Pose pose) in Cameras)
                {
                    if (Vector3.Dot(onScene - pose.position, pose.forward) <= 0f)
                    {
                        continue;
                    }
                    Vector2 point = camera.WorldToViewportPoint(onScene, pose);
                    if (point.x >= -Allowance && point.x <= 1f + Allowance && point.y >= -Allowance && point.y <= 1f + Allowance)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private readonly Coverage coverage = new Coverage();

        /// <summary>
        /// Sets up a check of what the cameras see from <paramref name="eyePosition"/> through a frame centered at
        /// <paramref name="frameCenter"/>, at the same scene depth as <see cref="Capture"/>. Returns null when no camera is
        /// playing, so there is nothing to limit the frame to.
        /// </summary>
        public Coverage BeginCoverage(Vector3 eyePosition, Vector3 frameCenter)
        {
            coverage.Cameras.Clear();
            foreach (Feed feed in new[] { leftFeed, rightFeed })
            {
                if (feed.IsPlaying)
                {
                    coverage.Cameras.Add((feed.Camera, feed.Camera.GetCameraPose()));
                }
            }
            if (coverage.Cameras.Count == 0)
            {
                return null;
            }

            float frameDistance = Vector3.Distance(eyePosition, frameCenter);
            coverage.Eye = eyePosition;
            coverage.DepthScale = SceneDepth(eyePosition, frameCenter, frameDistance) / frameDistance;
            coverage.Allowance = coverageAllowance;
            return coverage;
        }

        private static bool OverlapsViewport(Vector2[] quad)
        {
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector2 point in quad)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return min.x < 1f && min.y < 1f && max.x > 0f && max.y > 0f;
        }

        /// <summary>
        /// Projective map from the unit square onto <paramref name="quad"/> (corners in the same order as the square's
        /// (0,0), (1,0), (1,1), (0,1)), as rows of a 3x3 matrix for the QuadWarp shader.
        /// </summary>
        private static Matrix4x4 SquareToQuad(Vector2[] quad)
        {
            Vector2 p0 = quad[0], p1 = quad[1], p2 = quad[2], p3 = quad[3];
            Vector2 sum = p0 - p1 + p2 - p3;
            Vector2 d1 = p1 - p2;
            Vector2 d2 = p3 - p2;
            float denominator = d1.x * d2.y - d2.x * d1.y;
            float g = 0f, h = 0f;
            if (Mathf.Abs(denominator) > 1e-9f)
            {
                g = (sum.x * d2.y - d2.x * sum.y) / denominator;
                h = (d1.x * sum.y - sum.x * d1.y) / denominator;
            }

            var matrix = Matrix4x4.zero;
            matrix.SetRow(0, new Vector4(p1.x - p0.x + g * p1.x, p3.x - p0.x + h * p3.x, p0.x, 0f));
            matrix.SetRow(1, new Vector4(p1.y - p0.y + g * p1.y, p3.y - p0.y + h * p3.y, p0.y, 0f));
            matrix.SetRow(2, new Vector4(g, h, 1f, 0f));
            return matrix;
        }

        // Finds or creates a camera for each side. Both run all the time, so a change of dominant eye needs no restart
        private void SetUpFeeds()
        {
            var cameras = new List<PassthroughCameraAccess> { passthroughCamera };
            cameras.AddRange(FindObjectsByType<PassthroughCameraAccess>(FindObjectsSortMode.None));
            foreach (PassthroughCameraAccess camera in cameras)
            {
                Feed feed = camera.CameraPosition == PassthroughCameraAccess.CameraPositionType.Left ? leftFeed : rightFeed;
                if (feed.Camera == null) feed.Camera = camera;
            }

            Feed missing = leftFeed.Camera == null ? leftFeed : rightFeed.Camera == null ? rightFeed : null;
            if (missing == null)
            {
                return;
            }

            // The camera picks its side when enabled, so set it up on an inactive object first
            PassthroughCameraAccess existing = (missing == leftFeed ? rightFeed : leftFeed).Camera;
            var cameraObject = new GameObject("PassthroughCamera" + (missing == leftFeed ? "Left" : "Right"));
            cameraObject.SetActive(false);
            cameraObject.transform.SetParent(existing.transform.parent, false);
            missing.Camera = cameraObject.AddComponent<PassthroughCameraAccess>();
            missing.Camera.enabled = false; // MaxFramerate can only be set while disabled
            missing.Camera.CameraPosition = missing == leftFeed
                ? PassthroughCameraAccess.CameraPositionType.Left
                : PassthroughCameraAccess.CameraPositionType.Right;
            missing.Camera.RequestedResolution = existing.RequestedResolution;
            missing.Camera.MaxFramerate = existing.MaxFramerate;
            cameraObject.SetActive(true);
            missing.Camera.enabled = existing.enabled;
        }
    }
}
