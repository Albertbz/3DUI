using System.Collections.Generic;
using UnityEngine;

namespace SpatialPhotos
{
    public class ImageBoundaryBox : MonoBehaviour
    {
        [Tooltip("Empty fields are found in the scene at startup")]
        [SerializeField] private CaptureFrameVisual boundaryFrame;
        [SerializeField] private Camera head;
        [SerializeField] private PhotoCapture photoCapture;
        [SerializeField] private DominantEyeCalibration calibration; // Takes the frame instead of a photo while it runs; created when empty
        [SerializeField] private float minCaptureSize = 0.05f; // 5 cm, ignore accidental tiny frames
        [Tooltip("How long before the pinch release the photo is taken, so it shows the closed pinch rather than opening fingers")]
        [SerializeField] private float releaseLookback = 0.15f;

        /// <summary>The frame as it was at one moment while pinching.</summary>
        private struct FrameState
        {
            public Vector3 Corner0, Corner1, Corner2, Corner3; // Bottom-left, bottom-right, top-right, top-left
            public Pose Pose;
            public Vector3 Eye;
            public float Width;
            public float Height;
            public float Time;

            public Vector3[] Corners => new[] { Corner0, Corner1, Corner2, Corner3 };
        }

        // Recent frames, oldest first. A release is only detected once the fingers have opened and the hands are
        // often already moving, so the photo uses the frame from Release Lookback before it
        private readonly List<FrameState> history = new List<FrameState>();
        private FrameState captured; // The state the last release used

        private void Awake()
        {
            if (boundaryFrame == null) boundaryFrame = FindFirstObjectByType<CaptureFrameVisual>(FindObjectsInactive.Include);
            if (photoCapture == null) photoCapture = FindFirstObjectByType<PhotoCapture>();
            if (calibration == null) calibration = FindFirstObjectByType<DominantEyeCalibration>();
            if (calibration == null) calibration = gameObject.AddComponent<DominantEyeCalibration>();
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
            history.Clear();
            if (photoCapture != null)
            {
                photoCapture.BeginRecording();
            }
            UpdateBoundaryFrameFacingCamera(leftHandPosition, rightHandPosition);
        }

        private void HandleProximityPinchRelease(Vector3 leftHandPosition, Vector3 rightHandPosition)
        {
            // Hide the frame first so it can never end up in the photo
            EnableBoundaryFrame(false);
            if (history.Count == 0)
            {
                EndRecording();
                return;
            }
            captured = StateBefore(Time.time - releaseLookback);

            if (calibration.IsActive)
            {
                calibration.Submit(captured.Pose.position);
            }
            else if (captured.Width < minCaptureSize || captured.Height < minCaptureSize)
            {
                Debug.Log($"Frame too small to capture ({captured.Width:F3} x {captured.Height:F3} m).");
            }
            else if (photoCapture != null)
            {
                photoCapture.Capture(captured.Corners, captured.Pose, captured.Width, captured.Height, captured.Eye, captured.Time);
            }

            EndRecording();
        }

        private void EndRecording()
        {
            if (photoCapture != null)
            {
                photoCapture.EndRecording();
            }
        }

        /// <summary>The newest frame from at or before <paramref name="time"/>, or the oldest one after a very short pinch.</summary>
        private FrameState StateBefore(float time)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Time <= time)
                {
                    return history[i];
                }
            }
            return history[0];
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

            // 2. Face the eye that sights through the frame: the dominant one, or the head's center while calibrating
            Transform eye = calibration.IsActive ? head.transform : XRRig.DominantEyeTransform;
            Vector3 forward = (center - eye.position).normalized;
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
            history.Add(new FrameState
            {
                Corner0 = center - right - up,
                Corner1 = center + right - up,
                Corner2 = center + right + up,
                Corner3 = center - right + up,
                Pose = new Pose(center, frameRotation),
                Eye = eye.position,
                Width = width,
                Height = height,
                Time = Time.time,
            });
            float keepAfter = Time.time - releaseLookback - 0.2f;
            while (history.Count > 1 && history[1].Time <= keepAfter)
            {
                history.RemoveAt(0);
            }
        }
    }
}
