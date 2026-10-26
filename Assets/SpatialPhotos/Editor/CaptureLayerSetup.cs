using UnityEditor;
using UnityEngine;

namespace SpatialPhotos.Editor
{
    /// <summary>Adds the Photo and CaptureIgnore layers to the project, so a new project needs no manual Tags & Layers setup.</summary>
    public static class CaptureLayerSetup
    {
        [MenuItem("Tools/Spatial Photos/Add Capture Layers")]
        public static void AddLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            foreach (string layerName in CaptureLayers.All)
            {
                if (LayerMask.NameToLayer(layerName) >= 0)
                {
                    continue;
                }

                // 0-2, 4 and 5 are Unity's built-in layers; every other slot is a user layer
                bool added = false;
                for (int i = 3; i < layers.arraySize && !added; i++)
                {
                    if (i == 4 || i == 5)
                    {
                        continue;
                    }
                    SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = layerName;
                        added = true;
                        Debug.Log($"Spatial Photos: added layer '{layerName}' at index {i}.");
                    }
                }
                if (!added)
                {
                    Debug.LogError($"Spatial Photos: no free layer slot for '{layerName}'.");
                }
            }
            tagManager.ApplyModifiedProperties();
        }
    }
}
