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
        [Tooltip("Keep the frame inside what the passthrough cameras see, so photos have no stretched edges")]
        [SerializeField] private bool limitToCameraView = true;
        [Tooltip("Points checked along each frame edge against the camera images")]
        [SerializeField] private int coverageSamplesPerEdge = 5;

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

            // 2. Face the head's center, which turns steadily with the head. The photo is still what the eye that
            //    sights through the frame sees: the dominant one, or the head's center while calibrating
            Transform eye = calibration.IsActive ? head.transform : XRRig.DominantEyeTransform;
            Vector3 forward = (center - head.transform.position).normalized;
            Quaternion frameRotation = Quaternion.LookRotation(forward, head.transform.up);
            frameTransform.rotation = frameRotation;

            // 3. Project corner displacement into the frame's local coordinate frame
            Vector3 diagonalWorld = cornerB - cornerA;
            Vector3 diagonalLocal = Quaternion.Inverse(frameRotation) * diagonalWorld;

            float width = Mathf.Abs(diagonalLocal.x);
            float height = Mathf.Abs(diagonalLocal.y);
            if (limitToCameraView && !calibration.IsActive && photoCapture != null)
            {
                FitToCameraView(center, frameRotation, eye.position, ref width, ref height);
            }

            // 4. Update the prefab's internal edge positions/scales. A frame squeezed to nothing by the camera view still
            //    shows, at the smallest size and tinted as too small
            boundaryFrame.SetDimensions(Mathf.Max(width, minCaptureSize), Mathf.Max(height, minCaptureSize));
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

        /// <summary>
        /// Shrinks the frame, kept centered between the hands, until the cameras see everything behind it: each side
        /// first stops where it leaves their view through the frame's center, then both shrink together if a corner
        /// is still outside.
        /// </summary>
        private void FitToCameraView(Vector3 center, Quaternion rotation, Vector3 eyePosition, ref float width, ref float height)
        {
            PhotoCapture.Coverage coverage = photoCapture.BeginCoverage(eyePosition, center);
            if (coverage == null || Sees(coverage, center, rotation, width, height))
            {
                return;
            }

            const float thin = 0.001f;
            float fullWidth = width, fullHeight = height;
            float fittedWidth = LargestFit(1f, s => Sees(coverage, center, rotation, fullWidth * s, thin)) * fullWidth;
            float fittedHeight = LargestFit(1f, s => Sees(coverage, center, rotation, thin, fullHeight * s)) * fullHeight;
            float scale = LargestFit(1f, s => Sees(coverage, center, rotation, fittedWidth * s, fittedHeight * s));
            width = fittedWidth * scale;
            height = fittedHeight * scale;
        }

        /// <summary>The largest scale in 0..<paramref name="max"/> for which <paramref name="fits"/> holds, by bisection.</summary>
        private static float LargestFit(float max, System.Func<float, bool> fits)
        {
            if (fits(max))
            {
                return max;
            }
            float low = 0f, high = max;
            for (int i = 0; i < 10; i++)
            {
                float middle = (low + high) * 0.5f;
                if (fits(middle)) low = middle;
                else high = middle;
            }
            return low;
        }

        private bool Sees(PhotoCapture.Coverage coverage, Vector3 center, Quaternion rotation, float width, float height)
        {
            Vector3 right = rotation * Vector3.right * (width * 0.5f);
            Vector3 up = rotation * Vector3.up * (height * 0.5f);
            Vector3 corner0 = center - right - up, corner1 = center + right - up, corner2 = center + right + up, corner3 = center - right + up;
            int steps = Mathf.Max(1, coverageSamplesPerEdge - 1);
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                if (!coverage.Sees(Vector3.Lerp(corner0, corner1, t)) || !coverage.Sees(Vector3.Lerp(corner1, corner2, t)) ||
                    !coverage.Sees(Vector3.Lerp(corner2, corner3, t)) || !coverage.Sees(Vector3.Lerp(corner3, corner0, t)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
