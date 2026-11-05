using Oculus.Interaction;
using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Two-hand grab on a floating photo: the photo follows both hands, turns with the line between them and
    /// resizes with their distance, so moving the hands apart makes it bigger. It resizes the photo itself
    /// rather than scaling the transform, so the outline keeps its stroke width.
    /// <see cref="PhotoObject"/> sets it up as its Grabbable's two-grab transformer.
    /// </summary>
    public class PhotoResizeTransformer : MonoBehaviour, ITransformer
    {
        public float MinHeight { get; set; } = 0.05f;
        public float MaxHeight { get; set; } = 1.5f;

        private IGrabbable grabbable;
        private PhotoObject photo;
        private float startDistance;
        private Vector3 startAxis;
        private Vector2 startSize;
        private Vector3 startOffset;      // Photo position relative to the midpoint between the hands
        private Quaternion startRotation;

        public void Initialize(IGrabbable owner)
        {
            grabbable = owner;
            photo = GetComponent<PhotoObject>();
        }

        public void BeginTransform()
        {
            Vector3 a = grabbable.GrabPoints[0].position;
            Vector3 b = grabbable.GrabPoints[1].position;
            Transform target = grabbable.Transform;

            startAxis = b - a;
            startDistance = Mathf.Max(startAxis.magnitude, 0.01f);
            startSize = photo.Size;
            startOffset = target.position - (a + b) * 0.5f;
            startRotation = target.rotation;
        }

        public void UpdateTransform()
        {
            Vector3 a = grabbable.GrabPoints[0].position;
            Vector3 b = grabbable.GrabPoints[1].position;
            Transform target = grabbable.Transform;

            float ratio = Mathf.Clamp((b - a).magnitude / startDistance, MinHeight / startSize.y, MaxHeight / startSize.y);
            Quaternion turn = Quaternion.FromToRotation(startAxis, b - a);

            // Scaling the offset keeps both hands on the same spots of the photo
            target.SetPositionAndRotation((a + b) * 0.5f + turn * (startOffset * ratio), turn * startRotation);
            photo.Resize(startSize * ratio);
        }

        public void EndTransform()
        {
        }
    }
}
