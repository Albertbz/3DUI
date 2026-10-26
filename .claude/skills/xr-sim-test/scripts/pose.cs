// pose.cs <face|tray|off> [tilt degrees]
// face: left palm toward the eyes, fingers up, ~35 cm away. tray: palm up below the head, fingers forward.
// tilt rotates the palm away from the head around the finger axis (0 = facing straight at it).
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
string pose = "__ARG1__";
float tilt = float.TryParse("__ARG2__", out var t) ? t : 0f;
var d = System.AppDomain.CurrentDomain;
if (pose == "off" || pose == "") { d.SetData("lh.pos", null); d.SetData("lh.rot", null); return "override off"; }
UnityEngine.Vector3 pos, fingers;
if (pose == "face") { pos = head.position + head.forward * 0.35f - head.right * 0.08f - head.up * 0.12f; fingers = UnityEngine.Vector3.up; }
else { pos = head.position + head.forward * 0.38f - head.right * 0.12f - head.up * 0.4f; fingers = head.forward; }
var toHead = (head.position - pos).normalized;
// Hand anchor convention (measured from the simulated hand's joints): -Y = palm normal, +Z = fingers
var rot = UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.ProjectOnPlane(fingers, toHead), -toHead);
rot = UnityEngine.Quaternion.AngleAxis(tilt, rot * UnityEngine.Vector3.forward) * rot;
d.SetData("lh.pos", pos); d.SetData("lh.rot", rot);
return $"pose {pose} tilt {tilt}: palm·toHead={UnityEngine.Vector3.Dot(rot * UnityEngine.Vector3.down, toHead):F2}";
