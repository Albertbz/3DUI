// gallery_look.cs <name> [hover|grab|clear] [thumbnail index]
// Puts a gallery thumbnail into a hover/grab state through its Grabbable (no movement), then saves a
// close-up render of the wrist gallery to __OUT__/shots/<name>.png and prints the outline states.
// 'clear' sends Unselect/Unhover for whatever an earlier call left on that thumbnail.
var g = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.WristGallery>();
var v = g.transform.Find("Visual");
var thumbs = v.GetComponentsInChildren<SpatialPhotos.GalleryThumbnail>();
int index = "__ARG3__" == "" ? 0 : int.Parse("__ARG3__");
var s = new System.Text.StringBuilder();
if ("__ARG2__" != "" && index < thumbs.Length)
{
    var grab = thumbs[index].GetComponent<Oculus.Interaction.Grabbable>();
    var pose = new UnityEngine.Pose(thumbs[index].transform.position, thumbs[index].transform.rotation);
    void Send(Oculus.Interaction.PointerEventType t) => grab.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(901, t, pose));
    switch ("__ARG2__")
    {
        case "hover": Send(Oculus.Interaction.PointerEventType.Hover); break;
        case "grab": Send(Oculus.Interaction.PointerEventType.Select); break;
        case "clear": Send(Oculus.Interaction.PointerEventType.Unselect); Send(Oculus.Interaction.PointerEventType.Unhover); break;
    }
}
foreach (var o in v.GetComponentsInChildren<SpatialPhotos.FrameOutline>())
    s.AppendLine($"{o.transform.parent.name}/{o.name}: size={o.Size.x:F3}x{o.Size.y:F3} dashed={o.Dashed} activity={o.Activity:F2} layer={UnityEngine.LayerMask.LayerToName(o.gameObject.layer)}");
foreach (var b in v.GetComponentsInChildren<UnityEngine.Transform>()) if (b.name == "Button") s.AppendLine($"{b.parent.name}/Button renderer enabled={b.GetComponent<UnityEngine.Renderer>().enabled}");

var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var go = new UnityEngine.GameObject("TempSnapCam") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
var cam = go.AddComponent<UnityEngine.Camera>();
cam.enabled = false; cam.stereoTargetEye = UnityEngine.StereoTargetEyeMask.None;
cam.fieldOfView = 38f; cam.aspect = 1.6f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 50f;
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.22f, 0.24f, 0.28f);
go.transform.SetPositionAndRotation(head.position, UnityEngine.Quaternion.LookRotation(v.position - head.position, head.up));
var rt = new UnityEngine.RenderTexture(1120, 700, 24) { antiAliasing = 4 };
cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1120, 700, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 1120, 700), 0, 0); tex.Apply();
UnityEngine.RenderTexture.active = null;
System.IO.Directory.CreateDirectory("__OUT__/shots");
System.IO.File.WriteAllBytes("__OUT__/shots/__ARG1__.png", tex.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
s.Append("snapshot: __OUT__/shots/__ARG1__.png");
return s.ToString();
