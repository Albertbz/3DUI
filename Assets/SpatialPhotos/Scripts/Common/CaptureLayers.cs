using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// The two layers that keep things out of photos. Layers are stored by index in prefabs, which differs
    /// between projects, so objects are moved onto them by name at runtime.
    /// Add them with Tools → Spatial Photos → Add Capture Layers.
    /// </summary>
    public static class CaptureLayers
    {
        public const string Photo = "Photo";                 // Floating photos
        public const string CaptureIgnore = "CaptureIgnore"; // Frame, gallery and hand visuals

        public static readonly string[] All = { Photo, CaptureIgnore };

        /// <summary>Moves <paramref name="root"/> and its children onto the named layer; warns once if it is missing.</summary>
        public static void Apply(GameObject root, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                WarnMissing(layerName);
                return;
            }
            SetRecursively(root.transform, layer);
        }

        private static readonly System.Collections.Generic.HashSet<string> warned = new System.Collections.Generic.HashSet<string>();

        public static void WarnMissing(string layerName)
        {
            if (!warned.Add(layerName))
            {
                return;
            }
            Debug.LogWarning($"Spatial Photos: layer '{layerName}' is missing, so these objects can appear in photos. " +
                             "Add it with Tools → Spatial Photos → Add Capture Layers.");
        }

        private static void SetRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetRecursively(child, layer);
            }
        }
    }
}
