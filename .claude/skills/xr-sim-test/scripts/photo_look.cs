// photo_look.cs <name> [hover|grab|clear]
// Puts the first floating photo into a hover/grab state through its Grabbable (no movement), then saves a
// close-up render of it from the head to __OUT__/shots/<name>.png and prints its outline state.
// Render in a separate call after the event: the outline switches in the next Update.
var photo = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoObject>();
if (photo == null) return "no photo in scene; run capture.sh first";
if ("__ARG2__" != "")
{
    var grab = photo.GetComponent<Oculus.Interaction.Grabbable>();
    var pose = new UnityEngine.Pose(photo.transform.position, photo.transform.rotation);
    void Send(Oculus.Interaction.PointerEventType t) => grab.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(903, t, pose));
    switch ("__ARG2__")
    {
        case "hover": Send(Oculus.Interaction.PointerEventType.Hover); break;
        case "grab": Send(Oculus.Interaction.PointerEventType.Select); break;
        case "clear": Send(Oculus.Interaction.PointerEventType.Unselect); Send(Oculus.Interaction.PointerEventType.Unhover); break;
    }
}
var o = photo.transform.Find("Outline").GetComponent<SpatialPhotos.FrameOutline>();
var s = $"photo {photo.Entry.WidthM:F2}x{photo.Entry.HeightM:F2} m held={photo.IsHeld} | outline size={o.Size.x:F3}x{o.Size.y:F3} dashed={o.Dashed} activity={o.Activity:F2} appear={o.Appear:F2} layer={UnityEngine.LayerMask.LayerToName(o.gameObject.layer)}";

var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var go = new UnityEngine.GameObject("TempSnapCam") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
var cam = go.AddComponent<UnityEngine.Camera>();
cam.enabled = false; cam.stereoTargetEye = UnityEngine.StereoTargetEyeMask.None;
cam.fieldOfView = 45f; cam.aspect = 1.6f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 50f;
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.22f, 0.24f, 0.28f);
go.transform.SetPositionAndRotation(head.position, UnityEngine.Quaternion.LookRotation(photo.transform.position - head.position, head.up));
var rt = new UnityEngine.RenderTexture(1120, 700, 24) { antiAliasing = 4 };
cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1120, 700, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 1120, 700), 0, 0); tex.Apply();
UnityEngine.RenderTexture.active = null;
System.IO.Directory.CreateDirectory("__OUT__/shots");
System.IO.File.WriteAllBytes("__OUT__/shots/__ARG1__.png", tex.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
return s + "\nsnapshot: __OUT__/shots/__ARG1__.png";
