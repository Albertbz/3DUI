// hold.cs <grab|release> : holds (or lets go of) the first photo in the scene through its Grabbable,
// the way a HandGrabInteractable would. Used to check that a held photo blocks the capture pinch.
var ph = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoObject>();
if (ph == null) return "no photo in scene";
var g = ph.GetComponent<Oculus.Interaction.Grabbable>();
var pose = new UnityEngine.Pose(ph.transform.position, ph.transform.rotation);
var types = "__ARG1__" == "grab"
    ? new[] { Oculus.Interaction.PointerEventType.Hover, Oculus.Interaction.PointerEventType.Select }
    : new[] { Oculus.Interaction.PointerEventType.Unselect, Oculus.Interaction.PointerEventType.Unhover };
foreach (var t in types) g.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(501, t, pose));
return $"__ARG1__: AnyHeld={SpatialPhotos.PhotoObject.AnyHeld}";
