// Installs a hook that overrides the left hand anchor pose right after OVRCameraRig updates it.
// pose.cs sets the pose through AppDomain data "lh.pos" / "lh.rot"; null means real tracking.
var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
var old = System.AppDomain.CurrentDomain.GetData("lh.hook") as System.Action<OVRCameraRig>;
if (old != null) rig.UpdatedAnchors -= old;
System.Action<OVRCameraRig> hook = r =>
{
    var pos = System.AppDomain.CurrentDomain.GetData("lh.pos");
    var rot = System.AppDomain.CurrentDomain.GetData("lh.rot");
    if (pos != null) r.leftHandAnchor.position = (UnityEngine.Vector3)pos;
    if (rot != null) r.leftHandAnchor.rotation = (UnityEngine.Quaternion)rot;
};
rig.UpdatedAnchors += hook;
System.AppDomain.CurrentDomain.SetData("lh.hook", hook);
return $"hook installed on {rig.name}";
