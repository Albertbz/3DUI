// state.cs [snapshot-name]
// Prints gallery and capture state. With a name, also saves a render of the head's view (virtual
// content only; the simulator's passthrough room is not in it) to __OUT__/shots/<name>.png.
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var lA = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/LeftHandAnchor").transform;
var g = UnityEngine.Object.FindFirstObjectByType<WristGallery>();
var v = g.transform.Find("Visual");
var gso = new UnityEditor.SerializedObject(g);
var palmLocal = gso.FindProperty("palmNormalLocal").vector3Value;
var dot = UnityEngine.Vector3.Dot(lA.TransformDirection(palmLocal).normalized, (head.position - lA.position).normalized);
var s = new System.Text.StringBuilder();
s.Append($"gallery: palm·toHead={dot:F2} visible={v.gameObject.activeSelf} scale={v.localScale.x:F2}");
if (v.gameObject.activeSelf)
{
    var toView = (head.position - v.position).normalized;
    var l = v.Find("LeftArrow"); var r = v.Find("RightArrow");
    s.Append($" facesViewer={UnityEngine.Vector3.Dot(-v.forward, toView):F2} upright={UnityEngine.Vector3.Dot(v.up, head.up):F2} notMirrored={UnityEngine.Vector3.Dot(v.right, head.right):F2} dist={UnityEngine.Vector3.Distance(v.position, head.position):F2}m");
    if (l != null && r != null) s.Append($" width={UnityEngine.Vector3.Angle(l.position - head.position, r.position - head.position):F0}°");
}
var frame = UnityEngine.Object.FindFirstObjectByType<BoundaryFramePrefab>(UnityEngine.FindObjectsInactive.Include);
var lib = UnityEngine.Object.FindFirstObjectByType<PhotoLibrary>();
var photos = UnityEngine.Object.FindObjectsByType<PhotoObject>(UnityEngine.FindObjectsSortMode.None);
s.Append($"\ncapture: pinchActive={HandPinchManager.IsProximityPinchActive} frameActive={frame.gameObject.activeSelf} library={lib.Entries.Count} photosInScene={photos.Length} photoHeld={PhotoObject.AnyHeld} thumbHeld={GalleryThumbnail.AnyHeld}");
foreach (var p in photos) s.Append($"\n  photo at {p.transform.position:F2} {p.Entry.WidthM:F2}x{p.Entry.HeightM:F2} m layer={UnityEngine.LayerMask.LayerToName(p.gameObject.layer)}");

string name = "__ARG1__";
if (name != "")
{
    var go = new UnityEngine.GameObject("TempSnapCam") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
    var cam = go.AddComponent<UnityEngine.Camera>();
    cam.enabled = false; cam.stereoTargetEye = UnityEngine.StereoTargetEyeMask.None;
    cam.fieldOfView = 70f; cam.aspect = 1.3f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 50f;
    cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.22f, 0.24f, 0.28f);
    go.transform.SetPositionAndRotation(head.position, head.rotation);
    var rt = new UnityEngine.RenderTexture(910, 700, 24);
    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
    UnityEngine.RenderTexture.active = rt;
    var tex = new UnityEngine.Texture2D(910, 700, UnityEngine.TextureFormat.RGB24, false);
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 910, 700), 0, 0); tex.Apply();
    UnityEngine.RenderTexture.active = null;
    System.IO.Directory.CreateDirectory("__OUT__/shots");
    var path = $"__OUT__/shots/{name}.png";
    System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
    s.Append($"\nsnapshot: {path}");
}
return s.ToString();
