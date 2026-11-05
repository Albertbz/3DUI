// resize_hands.cs: are the visual hands free to follow the real hands during a two-hand grab?
// Force-selects the first photo in the scene with the real HandGrabInteractors (one hand, then both, then
// releases) and reports each hand's grab target alignment, whether its HandGrabStateVisual has locked the
// wrist, and how far the visual (synthetic) wrist is from the tracked wrist.
// The simulator's tracked hands can't be moved from code, so the photo is turned 25° inside the two-hand grab
// instead (through the resize transformer's start rotation): a visual hand locked onto the photo swings away
// from the real hand, a free one stays on it. A control run repeats this with the old AlignOnGrab behaviour.
// Runs as a coroutine; read the result with wait_log.sh. Needs a photo in the scene (resize.cs leaves one).
var photo = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoObject>();
var runner = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoLibrary>();
var log = new System.Text.StringBuilder();
var d = System.AppDomain.CurrentDomain;
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);
if (photo == null) { log.AppendLine("no photo in scene; run resize.cs first"); d.SetData("xrsim.done", true); return "no photo"; }

var interactable = photo.GetComponentInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>();
var interactors = UnityEngine.Object.FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabInteractor>(UnityEngine.FindObjectsSortMode.None);
var visuals = UnityEngine.Object.FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabStateVisual>(UnityEngine.FindObjectsSortMode.None);
var synths = UnityEngine.Object.FindObjectsByType<Oculus.Interaction.Input.SyntheticHand>(UnityEngine.FindObjectsSortMode.None);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var wristFree = typeof(Oculus.Interaction.HandGrab.HandGrabStateVisual).GetField("_isWristFree", flags);
var visualHand = typeof(Oculus.Interaction.HandGrab.HandGrabStateVisual).GetField("_syntheticHand", flags);
var startRotation = typeof(SpatialPhotos.PhotoResizeTransformer).GetField("startRotation", flags);
var resize = photo.GetComponent<SpatialPhotos.PhotoResizeTransformer>();
Oculus.Interaction.HandGrab.HandGrabInteractor Find(Oculus.Interaction.Input.Handedness h) => System.Linq.Enumerable.FirstOrDefault(interactors, i => i.isActiveAndEnabled && i.Hand.Handedness == h);
var left = Find(Oculus.Interaction.Input.Handedness.Left);
var right = Find(Oculus.Interaction.Input.Handedness.Right);

string Report(Oculus.Interaction.HandGrab.HandGrabInteractor i)
{
    var s = $"{i.Hand.Handedness}: target={i.HandGrabTarget.HandAlignment}";
    i.Hand.GetRootPose(out var real);
    float worst = 0f; bool free = true;
    foreach (var v in visuals)
    {
        var sh = visualHand.GetValue(v) as Oculus.Interaction.Input.SyntheticHand;
        if (!v.isActiveAndEnabled || sh == null || sh.Handedness != i.Hand.Handedness) continue;
        free &= (bool)wristFree.GetValue(v);
        if (sh.GetRootPose(out var vis)) worst = UnityEngine.Mathf.Max(worst, UnityEngine.Vector3.Distance(vis.position, real.position));
    }
    return s + $" wristFree={free} visualWristFromRealHand={worst * 1000f:F0}mm";
}

System.Collections.IEnumerator Frames(int n) { for (int k = 0; k < n; k++) yield return null; }

System.Collections.IEnumerator Pass(string name, bool oldBehaviour)
{
    left.ForceSelect(interactable, true);
    yield return Frames(5);
    if (name == "fix") log.AppendLine($"one hand: {Report(left)} (expect AlignOnGrab: locks onto the photo as before)");
    if (oldBehaviour) interactable.HandAlignment = Oculus.Interaction.HandGrab.HandAlignType.AlignOnGrab;
    right.ForceSelect(interactable, true);
    yield return Frames(5);
    log.AppendLine($"{name}, two hands: {Report(left)} | {Report(right)}");

    // Turn the photo 25° in the grab while the real hands stay still
    var r = (UnityEngine.Quaternion)startRotation.GetValue(resize);
    startRotation.SetValue(resize, UnityEngine.Quaternion.AngleAxis(25f, photo.transform.up) * r);
    yield return Frames(5);
    log.AppendLine($"{name}, photo turned 25°: {Report(left)} | {Report(right)}");

    right.ForceRelease();
    yield return Frames(5);
    if (name == "fix") log.AppendLine($"right let go: {Report(left)} (expect still free: no snap mid-grab)");
    left.ForceRelease();
    yield return Frames(5);
}

System.Collections.IEnumerator Run()
{
    log.AppendLine($"interactors: left={left != null} right={right != null} grab visuals={visuals.Length}");
    if (left == null || right == null) { d.SetData("xrsim.done", true); yield break; }
    yield return Pass("fix", false);
    log.AppendLine("  (expect both target=None wristFree=True, visual wrist stays on the real hand when the photo turns)");
    yield return Pass("control (old AlignOnGrab)", true);
    log.AppendLine("  (control: wrists locked, visual wrists swing away with the photo — the reported bug)");
    log.AppendLine($"released: held={photo.IsHeld} photo alignment={interactable.HandAlignment} (expect False AlignOnGrab)");
    d.SetData("xrsim.done", true);
}
runner.StartCoroutine(Run());
return "resize hands test started";
