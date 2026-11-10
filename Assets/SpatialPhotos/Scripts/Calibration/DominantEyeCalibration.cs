using System.Collections;
using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Finds the dominant eye with a sighting test: a dot floats a few meters away, and the user pinches a frame
    /// around it with both eyes open. People center the frame for their dominant eye, so the eye whose ray through
    /// the frame's center passes closest to the dot is dominant. While it runs, <see cref="ImageBoundaryBox"/> hands
    /// it the frame instead of taking a photo. Starts by itself until an eye is saved; run it again with <see cref="Begin"/>.
    /// </summary>
    public class DominantEyeCalibration : MonoBehaviour
    {
        [SerializeField] private float targetDistance = 3f;
        [SerializeField] private float targetDiameter = 0.05f;
        [SerializeField] private float labelHeight = 0.05f;    // Cap height of the instructions, readable at Target Distance
        [Tooltip("How far, in degrees, the frame's center may be off the dot as seen from the dominant eye")]
        [SerializeField] private float maxAngle = 3f;
        [Tooltip("How much closer, in degrees, the frame's center must be to the dot for one eye than for the other")]
        [SerializeField] private float minDifference = 1.5f;
        [SerializeField] private int trials = 3;
        [SerializeField] private float resultDuration = 2.5f;  // How long the result stays up before the dot goes away

        public bool IsActive { get; private set; }

        private Transform root;
        private TextMesh label;
        private int leftVotes;
        private int rightVotes;

        private IEnumerator Start()
        {
            CreateVisuals();
            if (DominantEye.HasValue)
            {
                yield break;
            }

            // Give tracking a moment, so the dot appears in front of the head rather than the origin
            yield return new WaitForSeconds(1f);
            Begin();
        }

        public void Begin()
        {
            Transform head = XRRig.Head;
            if (head == null)
            {
                XRRig.Warn(this, "the head");
                return;
            }

            StopAllCoroutines();
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;
            root.SetPositionAndRotation(head.position + forward * targetDistance, Quaternion.LookRotation(forward));
            root.gameObject.SetActive(true);

            leftVotes = rightVotes = 0;
            IsActive = true;
            label.text = "Keep both eyes open\nand pinch a frame around the dot";
        }

        /// <summary>Takes one sighting: the center of the frame the user pinched.</summary>
        public void Submit(Vector3 frameCenter)
        {
            if (!IsActive)
            {
                return;
            }

            Vector3 target = root.position;
            float leftAngle = SightingAngle(Eye.Left, frameCenter, target);
            float rightAngle = SightingAngle(Eye.Right, frameCenter, target);
            Debug.Log($"DominantEyeCalibration: dot is {leftAngle:F1}° off for the left eye, {rightAngle:F1}° for the right.");

            if (Mathf.Min(leftAngle, rightAngle) > maxAngle || Mathf.Abs(leftAngle - rightAngle) < minDifference)
            {
                label.text = "Center the dot in the frame\nand try again";
                return;
            }

            if (leftAngle < rightAngle) leftVotes++;
            else rightVotes++;

            int done = leftVotes + rightVotes;
            if (done < trials)
            {
                label.text = $"Got it ({done} of {trials})\nOnce more";
                return;
            }

            Eye eye = leftVotes > rightVotes ? Eye.Left : Eye.Right;
            IsActive = false;
            DominantEye.Set(eye);
            label.text = $"Your {eye.ToString().ToLowerInvariant()} eye is dominant";
            StartCoroutine(HideAfter(resultDuration));
        }

        private static float SightingAngle(Eye eye, Vector3 frameCenter, Vector3 target)
        {
            Vector3 position = XRRig.Eye(eye).position;
            return Vector3.Angle(target - position, frameCenter - position);
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            root.gameObject.SetActive(false);
        }

        private void CreateVisuals()
        {
            root = new GameObject("DominantEyeTarget").transform;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(root, false);
            dot.transform.localScale = Vector3.one * targetDiameter;
            var dotRenderer = dot.GetComponent<MeshRenderer>();
            dotRenderer.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = FrameOutline.StrokeColor };
            dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Labels read from their -Z side, and root's +Z points away from the viewer
            label = PokeButton.CreateLabel(root, "", labelHeight);
            label.transform.localPosition = new Vector3(0f, targetDiameter + labelHeight * 3f, 0f);

            CaptureLayers.Apply(root.gameObject, CaptureLayers.CaptureIgnore);
            root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }
    }
}
