using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Finds the head and hands of the scene's Meta camera rig, so the prefabs work without wiring
    /// scene references by hand. Components only use it for fields left empty in the Inspector.
    /// The rig creates its anchors in Awake, so call these from Start or later.
    /// </summary>
    public static class XRRig
    {
        private static OVRCameraRig rig;

        public static OVRCameraRig Rig
        {
            get
            {
                if (rig == null)
                {
                    rig = Object.FindFirstObjectByType<OVRCameraRig>();
                }
                return rig;
            }
        }

        public static Camera HeadCamera
        {
            get
            {
                if (Rig != null && Rig.centerEyeAnchor != null && Rig.centerEyeAnchor.TryGetComponent(out Camera camera))
                {
                    return camera;
                }
                return Camera.main;
            }
        }

        public static Transform Head
        {
            get
            {
                Camera camera = HeadCamera;
                return camera != null ? camera.transform : null;
            }
        }

        public static Transform HandAnchor(bool left)
        {
            if (Rig == null)
            {
                return null;
            }
            return left ? Rig.leftHandAnchor : Rig.rightHandAnchor;
        }

        /// <summary>The OVRHand under the rig's hand anchor (hand tracking building block or OVRHandPrefab).</summary>
        public static OVRHand Hand(bool left)
        {
            Transform anchor = HandAnchor(left);
            return anchor != null ? anchor.GetComponentInChildren<OVRHand>(true) : null;
        }

        public static void Warn(Object context, string what)
        {
            Debug.LogWarning($"{context.GetType().Name}: could not find {what}. Add an OVRCameraRig to the scene or assign it in the Inspector.", context);
        }
    }
}
