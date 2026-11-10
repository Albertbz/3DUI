using System;
using Meta.XR;
using UnityEngine;

namespace SpatialPhotos
{
    public class PhotoCapture : MonoBehaviour
    {
        // Empty scene references (passthrough camera, head camera, library, shutter sound) are found at startup
        [Header("Sources")]
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

        private static readonly int OverlayTexId = Shader.PropertyToID("_OverlayTex");
        private static readonly int HomographyId = Shader.PropertyToID("_Homography");

        private Material alphaOverMaterial;
        private Material warpMaterial;

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

        private void OnEnable()
        {
            DominantEye.Changed += MatchCameraToDominantEye;
        }

        private void OnDisable()
        {
            DominantEye.Changed -= MatchCameraToDominantEye;
        }

        private void Start()
        {
            if (headCamera == null) headCamera = XRRig.HeadCamera;
            if (passthroughCamera == null)
            {
                Debug.LogWarning("PhotoCapture: no PassthroughCameraAccess in the scene, photos will contain virtual content only.", this);
            }
            MatchCameraToDominantEye();

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
        }

        private void OnDestroy()
        {
            Destroy(alphaOverMaterial);
            Destroy(warpMaterial);
        }

        /// <summary>
        /// Captures what <paramref name="eyePosition"/> sees through the given world-space frame corners and spawns a
        /// photo at <paramref name="framePose"/>. The frame must face the eye, as <see cref="ImageBoundaryBox"/> places it.
        /// Corners go bottom-left, bottom-right, top-right, top-left as seen from the eye.
        /// </summary>
        public void Capture(Vector3[] frameCornersWorld, Pose framePose, float width, float height, Vector3 eyePosition)
        {
            float frameDistance = Vector3.Distance(eyePosition, framePose.position);
            float verticalFov = 2f * Mathf.Atan(height * 0.5f / frameDistance) * Mathf.Rad2Deg;

            Texture baseImage;
            Matrix4x4 warp;
            int photoHeight;

            if (passthroughCamera != null && passthroughCamera.enabled && passthroughCamera.IsPlaying)
            {
                // The real world seen through the frame, taken as a plane at the scene's depth: each corner's ray from
                // the eye hits that plane, and those hits are found in the passthrough image
                float depth = SceneDepth(eyePosition, framePose.position, frameDistance);
                Pose cameraPose = passthroughCamera.GetCameraPose();
                Vector2Int resolution = passthroughCamera.CurrentResolution;
                var quad = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    Vector3 onScene = eyePosition + (frameCornersWorld[i] - eyePosition) * (depth / frameDistance);
                    quad[i] = passthroughCamera.WorldToViewportPoint(onScene, cameraPose);
                }

                if (!OverlapsViewport(quad))
                {
                    Debug.LogWarning("PhotoCapture: frame is outside the camera view, nothing captured.");
                    return;
                }

                baseImage = passthroughCamera.GetTexture();
                warp = SquareToQuad(quad);
                // As many pixels as the frame covers in the camera image
                float leftEdge = Vector2.Scale(quad[3] - quad[0], resolution).magnitude;
                float rightEdge = Vector2.Scale(quad[2] - quad[1], resolution).magnitude;
                photoHeight = Mathf.RoundToInt(Mathf.Max(leftEdge, rightEdge));
                Debug.Log($"PhotoCapture: {DominantEye.Current} eye, scene depth {depth:F2} m, frame at {frameDistance:F2} m.");
            }
            else
            {
                Debug.LogWarning("PhotoCapture: passthrough camera is not playing, capturing virtual content only.");
                baseImage = Texture2D.blackTexture;
                warp = Matrix4x4.identity;
                photoHeight = Mathf.RoundToInt(fallbackResolution.y * verticalFov / headCamera.fieldOfView);
            }

            photoHeight = Mathf.Clamp(photoHeight, 1, maxPhotoSize);
            int photoWidth = Mathf.Clamp(Mathf.RoundToInt(photoHeight * width / height), 1, maxPhotoSize);

            RenderTexture virtualLayer = RenderTexture.GetTemporary(photoWidth, photoHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture realLayer = RenderTexture.GetTemporary(photoWidth, photoHeight, 0, RenderTextureFormat.ARGB32);

            // 1. Render the virtual scene from the eye through the frame onto a transparent background. The frame
            //    faces the eye, so its edges are a centered frustum
            captureCamera.transform.SetPositionAndRotation(eyePosition, framePose.rotation);
            captureCamera.ResetProjectionMatrix();
            captureCamera.fieldOfView = verticalFov;
            captureCamera.aspect = width / height;
            captureCamera.nearClipPlane = nearClip;
            captureCamera.farClipPlane = farClip;
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

        // Passthrough comes from the camera on the dominant eye's side, the closest to that eye's view.
        // PassthroughCameraAccess picks its camera when enabled, so restart it to switch
        private void MatchCameraToDominantEye()
        {
            if (passthroughCamera == null)
            {
                return;
            }

            var side = DominantEye.Current == Eye.Left
                ? PassthroughCameraAccess.CameraPositionType.Left
                : PassthroughCameraAccess.CameraPositionType.Right;
            if (passthroughCamera.CameraPosition == side)
            {
                return;
            }

            bool wasEnabled = passthroughCamera.enabled;
            passthroughCamera.enabled = false;
            passthroughCamera.CameraPosition = side;
            passthroughCamera.enabled = wasEnabled;
        }
    }
}
