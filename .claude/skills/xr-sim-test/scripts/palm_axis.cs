// Measures which LeftHandAnchor axis points out of the palm, from the simulated hand's joints
// (Interaction SDK hand), and compares it with WristGallery's Palm Normal Local.
var lAnchor = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/LeftHandAnchor").transform;
var g = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.WristGallery>();
var configured = new UnityEditor.SerializedObject(g).FindProperty("palmNormalLocal").vector3Value;
foreach (var h in UnityEngine.Object.FindObjectsByType<Oculus.Interaction.Input.Hand>(UnityEngine.FindObjectsSortMode.None))
{
    if (h.Handedness != Oculus.Interaction.Input.Handedness.Left || !h.IsTrackedDataValid) continue;
    if (!h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandWristRoot, out var w)) continue;
    h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandIndex1, out var i1);
    h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandPinky1, out var p1);
    h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandMiddleTip, out var mt);
    // For a left hand, cross(index - wrist, pinky - wrist) points out of the palm
    var palm = UnityEngine.Vector3.Cross(i1.position - w.position, p1.position - w.position).normalized;
    var palmLocal = lAnchor.InverseTransformDirection(palm);
    var fingersLocal = lAnchor.InverseTransformDirection((mt.position - w.position).normalized);
    return $"measured palm normal (anchor-local)={palmLocal:F2} fingers={fingersLocal:F2}; configured Palm Normal Local={configured}; agreement={UnityEngine.Vector3.Dot(palmLocal, configured.normalized):F2} (expect > 0.9)";
}
return "no tracked left Interaction SDK hand; check 'metavr xrsim input source --hand left --source hand'";
