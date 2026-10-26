// Grab test on the first photo in the scene, driven through its Grabbable over several frames:
// a slow 15 cm drag and release (should stay floating), then a ~3 m/s throw (should fall, fade, be destroyed).
// Runs as a coroutine; read the result with wait_log.sh.
var photo = UnityEngine.Object.FindFirstObjectByType<PhotoObject>();
var runner = UnityEngine.Object.FindFirstObjectByType<PhotoLibrary>();
var log = new System.Text.StringBuilder();
var d = System.AppDomain.CurrentDomain;
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);
if (photo == null) { log.AppendLine("no photo in scene; run capture.sh first"); d.SetData("xrsim.done", true); return "no photo"; }
var grabbable = photo.GetComponent<Oculus.Interaction.Grabbable>();
var body = photo.GetComponent<UnityEngine.Rigidbody>();

// Interactors always send Hover before Select (and Unhover after Unselect). Without the Hover the
// Grabbable ignores the Move events and the object never moves.
void Send(int id, Oculus.Interaction.PointerEventType type, UnityEngine.Pose pose) =>
    grabbable.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, type, pose));

System.Collections.IEnumerator Run()
{
    var t = photo.transform;
    var start = t.position;
    var pose = new UnityEngine.Pose(start, t.rotation);
    Send(101, Oculus.Interaction.PointerEventType.Hover, pose);
    Send(101, Oculus.Interaction.PointerEventType.Select, pose);
    yield return null;
    log.AppendLine($"slow: after select IsHeld={photo.IsHeld} AnyHeld={PhotoObject.AnyHeld} (expect True True)");
    for (int i = 1; i <= 30; i++)
    {
        pose.position = start + new UnityEngine.Vector3(0.005f * i, -0.002f * i, -0.002f * i);
        Send(101, Oculus.Interaction.PointerEventType.Move, pose);
        yield return new UnityEngine.WaitForSeconds(0.033f);
    }
    log.AppendLine($"slow: photo moved {UnityEngine.Vector3.Distance(start, t.position):F3} m (expect ~0.17)");
    Send(101, Oculus.Interaction.PointerEventType.Unselect, pose);
    Send(101, Oculus.Interaction.PointerEventType.Unhover, pose);
    yield return new UnityEngine.WaitForSeconds(1f);
    log.AppendLine($"slow: 1 s after release held={photo.IsHeld} kinematic={body.isKinematic} gravity={body.useGravity} drift={UnityEngine.Vector3.Distance(pose.position, t.position):F3} m (expect False True False ~0)");

    pose = new UnityEngine.Pose(t.position, t.rotation);
    Send(102, Oculus.Interaction.PointerEventType.Hover, pose);
    Send(102, Oculus.Interaction.PointerEventType.Select, pose);
    yield return null;
    for (int i = 0; i < 8; i++)
    {
        pose.position += new UnityEngine.Vector3(3f, 0.5f, 0f) * UnityEngine.Time.deltaTime;
        Send(102, Oculus.Interaction.PointerEventType.Move, pose);
        yield return null;
    }
    Send(102, Oculus.Interaction.PointerEventType.Unselect, pose);
    Send(102, Oculus.Interaction.PointerEventType.Unhover, pose);
    yield return null;
    log.AppendLine($"throw: kinematic={body.isKinematic} gravity={body.useGravity} speed={body.linearVelocity.magnitude:F2} m/s (expect False True >1.5)");
    int libBefore = runner.Entries.Count;
    yield return new UnityEngine.WaitForSeconds(2f);
    log.AppendLine($"throw: 2 s later destroyed={photo == null} library unchanged={runner.Entries.Count == libBefore} (expect True True)");
    d.SetData("xrsim.done", true);
}
runner.StartCoroutine(Run());
return "grab test started";
