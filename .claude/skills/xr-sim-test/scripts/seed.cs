// seed.cs [count]
// Takes <count> photos (default 6) through PhotoCapture.Capture with frames of varying aspect in front of
// the head, so the gallery has content. Uses the simulator's synthetic passthrough camera when it plays.
// The spawned photo objects are removed; the photos stay in the library (on disk).
int count = int.TryParse("__ARG1__", out var c) ? c : 6;
var capture = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoCapture>();
var lib = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoLibrary>();
var pca = UnityEngine.Object.FindFirstObjectByType<Meta.XR.PassthroughCameraAccess>();
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
for (int i = 0; i < count; i++)
{
    float w = 0.16f + 0.04f * (i % 3), h = 0.12f;
    var center = head.position + UnityEngine.Quaternion.Euler(-5f + 4f * i, -15f + 6f * i, 0) * head.forward * 0.6f;
    var rot = UnityEngine.Quaternion.LookRotation((center - head.position).normalized, head.up);
    var r = rot * UnityEngine.Vector3.right * (w / 2); var u = rot * UnityEngine.Vector3.up * (h / 2);
    capture.Capture(new[] { center - r - u, center + r - u, center + r + u, center - r + u }, new UnityEngine.Pose(center, rot), w, h);
}
foreach (var p in UnityEngine.Object.FindObjectsByType<SpatialPhotos.PhotoObject>(UnityEngine.FindObjectsSortMode.None)) UnityEngine.Object.Destroy(p.gameObject);
return $"library={lib.Entries.Count}; passthrough camera playing={pca != null && pca.IsPlaying}";
