using UnityEngine;

namespace SpatialPhotos
{
    public class ImageBoundaryBox : MonoBehaviour
    {
        [Tooltip("Empty fields are found in the scene at startup")]
        [SerializeField] private CaptureFrameVisual boundaryFrame;
        [SerializeField] private Camera head;
        [SerializeField] private PhotoCapture photoCapture;
        [SerializeField] private float minCaptureSize = 0.05f; // 5 cm, ignore accidental tiny frames

        // Last frame computed while pinching, used for the capture on release
        private readonly Vector3[] lastCorners = new Vector3[4];
        private Pose lastPose;
        private float lastWidth;
        private float lastHeight;

        private void Awake()
        {
            if (boundaryFrame == null) boundaryFrame = FindFirstObjectByType<CaptureFrameVisual>(FindObjectsInactive.Include);
            if (photoCapture == null) photoCapture = FindFirstObjectByType<PhotoCapture>();
            EnableBoundaryFrame(false);
        }

        private void Start()
        {
            if (head == null) head = XRRig.HeadCamera;
            if (head == null) XRRig.Warn(this, "the head camera");
        }

        private void OnEnable()
        {
            HandPinchManager.OnProximityPinchTriggered += HandleProximityPinch;
            HandPinchManager.OnProximityPinchReleased += HandleProximityPinchRelease;
            HandPinchManager.OnProximityPinchUpdated += HandleProximityPinchUpdate;
        }

        private void OnDisable()
        {
            HandPinchManager.OnProximityPinchTriggered -= HandleProximityPinch;
            HandPinchManager.OnProximityPinchReleased -= HandleProximityPinchRelease;
            HandPinchManager.OnProximityPinchUpdated -= HandleProximityPinchUpdate;
        }

        private void HandleProximityPinch(Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            EnableBoundaryFrame(true);
            UpdateBoundaryFrameFacingCamera(leftHandPosition, rightHandPosition);
        }

        private void HandleProximityPinchRelease(Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            // Hide the frame first so it can never end up in the photo
            EnableBoundaryFrame(false);

            if (photoCapture == null)
            {
                return;
            }

            if (lastWidth < minCaptureSize || lastHeight < minCaptureSize)
            {
                Debug.Log($"Frame too small to capture ({lastWidth:F3} x {lastHeight:F3} m).");
                return;
            }

            photoCapture.Capture(lastCorners, lastPose, lastWidth, lastHeight);
        }

        private void HandleProximityPinchUpdate(Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            UpdateBoundaryFrameFacingCamera(leftHandPosition, rightHandPosition);
        }

        private void EnableBoundaryFrame(bool enable)
        {
            if (boundaryFrame != null)
            {
                boundaryFrame.gameObject.SetActive(enable);
            }
        }

        private void UpdateBoundaryFrameFacingCamera(Vector3 cornerA, Vector3 cornerB)
        {
            Transform frameTransform = boundaryFrame.transform;

            // 1. Center is always the midpoint between pinch points
            Vector3 center = (cornerA + cornerB) * 0.5f;
            frameTransform.position = center;

            // 2. Align rotation to face the headset
            Vector3 forward = (center - head.transform.position).normalized;
            Quaternion frameRotation = Quaternion.LookRotation(forward, head.transform.up);
            frameTransform.rotation = frameRotation;

            // 3. Project corner displacement into the frame's local coordinate frame
            Vector3 diagonalWorld = cornerB - cornerA;
            Vector3 diagonalLocal = Quaternion.Inverse(frameRotation) * diagonalWorld;

            float width = Mathf.Abs(diagonalLocal.x);
            float height = Mathf.Abs(diagonalLocal.y);

            // 4. Update the prefab's internal edge positions/scales
            boundaryFrame.SetDimensions(width, height);
            boundaryFrame.SetCapturable(width >= minCaptureSize && height >= minCaptureSize);

            // 5. Remember the frame so it can be captured on release
            Vector3 right = frameRotation * Vector3.right * (width * 0.5f);
            Vector3 up = frameRotation * Vector3.up * (height * 0.5f);
            lastCorners[0] = center - right - up;
            lastCorners[1] = center + right - up;
            lastCorners[2] = center + right + up;
            lastCorners[3] = center - right + up;
            lastPose = new Pose(center, frameRotation);
            lastWidth = width;
            lastHeight = height;
        }
    }
}
