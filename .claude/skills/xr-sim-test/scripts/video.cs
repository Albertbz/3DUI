// Renders a gallery sequence frame by frame from the head's view into __OUT__/video/:
// palm turns toward the face (gallery scales in), pages right twice and left once, palm turns away.
// Needs hook.cs installed. Rendering + PNG encoding is slow (~1-2 min for ~170 frames); wait with wait_log.sh.
var lib = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoLibrary>();
var gallery = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.WristGallery>();
var visual = gallery.transform.Find("Visual");
var headT = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var dir = "__OUT__/video/";
if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
System.IO.Directory.CreateDirectory(dir);
var d = System.AppDomain.CurrentDomain;
var log = new System.Text.StringBuilder();
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);
foreach (var p in UnityEngine.Object.FindObjectsByType<SpatialPhotos.PhotoObject>(UnityEngine.FindObjectsSortMode.None)) UnityEngine.Object.Destroy(p.gameObject);

var go = new UnityEngine.GameObject("TempVideoCam") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
var cam = go.AddComponent<UnityEngine.Camera>();
cam.enabled = false; cam.stereoTargetEye = UnityEngine.StereoTargetEyeMask.None;
cam.fieldOfView = 80f; cam.aspect = 1.4f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 50f;
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.22f, 0.24f, 0.28f);
var rt = new UnityEngine.RenderTexture(840, 600, 24);
var tex = new UnityEngine.Texture2D(840, 600, UnityEngine.TextureFormat.RGB24, false);
int frame = 0;

var pos = headT.position + headT.forward * 0.42f + headT.right * 0.04f - headT.up * 0.12f;
var toHead = (headT.position - pos).normalized;
var baseRot = UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.ProjectOnPlane(UnityEngine.Vector3.up, toHead), -toHead);
void SetTilt(float tilt) { d.SetData("lh.pos", pos); d.SetData("lh.rot", UnityEngine.Quaternion.AngleAxis(tilt, baseRot * UnityEngine.Vector3.forward) * baseRot); }

void Grab()
{
    go.transform.SetPositionAndRotation(headT.position, headT.rotation);
    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
    UnityEngine.RenderTexture.active = rt;
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 840, 600), 0, 0); tex.Apply();
    UnityEngine.RenderTexture.active = null;
    System.IO.File.WriteAllBytes(dir + $"f{frame++:D4}.png", tex.EncodeToPNG());
}

void Press(string name)
{
    var a = visual.Find(name)?.GetComponent<Oculus.Interaction.PokeInteractable>();
    if (a == null) return;
    var p = new UnityEngine.Pose(a.transform.position, a.transform.rotation);
    foreach (var t in new[] { Oculus.Interaction.PointerEventType.Hover, Oculus.Interaction.PointerEventType.Select, Oculus.Interaction.PointerEventType.Unselect, Oculus.Interaction.PointerEventType.Unhover })
        a.PublishPointerEvent(new Oculus.Interaction.PointerEvent(601, t, p));
}

System.Collections.IEnumerator Run()
{
    float dt = 1f / 30f;
    for (float t = 0; t <= 1.2f; t += dt) { SetTilt(UnityEngine.Mathf.Lerp(110f, 0f, t / 1.2f)); yield return new UnityEngine.WaitForEndOfFrame(); Grab(); yield return new UnityEngine.WaitForSeconds(dt); }
    foreach (var step in new[] { "", "RightArrow", "RightArrow", "LeftArrow", "" })
    {
        if (step != "") Press(step);
        for (int i = 0; i < 18; i++) { yield return new UnityEngine.WaitForEndOfFrame(); Grab(); yield return new UnityEngine.WaitForSeconds(dt); }
    }
    for (float t = 0; t <= 1.0f; t += dt) { SetTilt(UnityEngine.Mathf.Lerp(0f, 110f, t / 1.0f)); yield return new UnityEngine.WaitForEndOfFrame(); Grab(); yield return new UnityEngine.WaitForSeconds(dt); }
    for (int i = 0; i < 10; i++) { yield return new UnityEngine.WaitForEndOfFrame(); Grab(); yield return new UnityEngine.WaitForSeconds(dt); }
    UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
    log.AppendLine($"{frame} frames in {dir}");
    d.SetData("xrsim.done", true);
}
lib.StartCoroutine(Run());
return "video render started";
