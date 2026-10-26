// Capture frame look and resize sound, checked over several frames while a proximity pinch is open
// (open one first: hand.cs freeze/at/pinch for both hands, as frame_check.sh does).
// Moves the frozen hands' pointer poses smoothly: too small -> grow -> hold still -> shrink,
// and samples the frame material (_Valid, _Activity) and FrameResizeAudio's hum and ticks.
// Runs as a coroutine; read the result with wait_log.sh.
var runner = UnityEngine.Object.FindFirstObjectByType<PhotoLibrary>();
var log = new System.Text.StringBuilder();
var d = System.AppDomain.CurrentDomain;
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);
var frame = UnityEngine.Object.FindFirstObjectByType<BoundaryFramePrefab>(UnityEngine.FindObjectsInactive.Include);
if (!HandPinchManager.IsProximityPinchActive || !frame.gameObject.activeSelf)
{
    log.AppendLine("no proximity pinch open; run frame_check.sh"); d.SetData("xrsim.done", true); return "no pinch";
}
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var mat = frame.transform.Find("FrameQuad").GetComponent<FrameOutline>().Material;
var audio = frame.GetComponent<FrameResizeAudio>();
var hum = (UnityEngine.AudioSource)typeof(FrameResizeAudio).GetField("humSource", flags).GetValue(audio);
var tick = (UnityEngine.AudioSource)typeof(FrameResizeAudio).GetField("tickSource", flags).GetValue(audio);
var lastTickField = typeof(FrameResizeAudio).GetField("lastTickTime", flags);
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
OVRHand Hand(string side) => UnityEngine.GameObject.Find($"[BuildingBlock] Camera Rig/TrackingSpace/{side}HandAnchor").GetComponentInChildren<OVRHand>(true);
var left = Hand("Left"); var right = Hand("Right");

// Half the hand separation is 'a'; the frame is about 2a wide and 1.2a tall
void Place(float a)
{
    var c = head.position + head.up * -0.15f + head.forward * 0.5f;
    left.PointerPose.position = c - head.right * a - head.up * (0.6f * a);
    right.PointerPose.position = c + head.right * a + head.up * (0.6f * a);
}

var quad = frame.transform.Find("FrameQuad");
log.AppendLine($"setup: quad layer={UnityEngine.LayerMask.LayerToName(quad.gameObject.layer)} shader={mat.shader.name} legacy edges hidden={frame.transform.Find("TopEdge") == null || !frame.transform.Find("TopEdge").gameObject.activeSelf} (expect CaptureIgnore 3DUI/CaptureFrame True)");

System.Collections.IEnumerator Phase(float from, float to, float seconds, System.Collections.Generic.List<string> ticks, float[] stats)
{
    // stats: [startPitch, endPitch, peakVolume, peakActivity]
    float lastTick = (float)lastTickField.GetValue(audio);
    stats[2] = 0; stats[3] = 0;
    float t0 = UnityEngine.Time.time;
    bool first = true;
    while (UnityEngine.Time.time - t0 < seconds)
    {
        float k = UnityEngine.Mathf.SmoothStep(0, 1, (UnityEngine.Time.time - t0) / seconds);
        Place(UnityEngine.Mathf.Lerp(from, to, k));
        yield return null;
        if (first && UnityEngine.Time.time - t0 > 0.15f) { stats[0] = hum.pitch; first = false; }
        stats[2] = UnityEngine.Mathf.Max(stats[2], hum.volume);
        stats[3] = UnityEngine.Mathf.Max(stats[3], mat.GetFloat("_Activity"));
        float lt = (float)lastTickField.GetValue(audio);
        if (lt != lastTick) { lastTick = lt; ticks.Add(tick.pitch.ToString("F2")); }
    }
    Place(to);
    stats[1] = hum.pitch;
}

System.Collections.IEnumerator Run()
{
    var stats = new float[4];
    var ticks = new System.Collections.Generic.List<string>();

    Place(0.01f);
    for (int i = 0; i < 20; i++) yield return null;
    yield return new UnityEngine.WaitForSeconds(0.4f);
    log.AppendLine($"too small (~2x1 cm): _Valid={mat.GetFloat("_Valid"):F2} (expect ~0 = red tint)");

    ticks.Clear();
    yield return Phase(0.04f, 0.25f, 1.5f, ticks, stats);
    var growTicks = new System.Collections.Generic.List<string>(ticks);
    log.AppendLine($"grow 0.09->0.58 m diagonal: _Valid={mat.GetFloat("_Valid"):F2} humPitch {stats[0]:F2}->{stats[1]:F2} peakHumVol={stats[2]:F2} peakActivity={stats[3]:F2} ticks={growTicks.Count} pitches=[{string.Join(" ", growTicks)}]");
    log.AppendLine("  (expect _Valid 1, pitch rising, volume > 0.1, activity > 0.5, ~15-20 ticks with rising pitch)");

    ticks.Clear();
    yield return Phase(0.25f, 0.25f, 1.0f, ticks, stats);
    log.AppendLine($"hold still 1 s: humVol={hum.volume:F3} activity={mat.GetFloat("_Activity"):F2} ticks={ticks.Count} humPlaying={hum.isPlaying} (expect ~0, ~0, 0, True)");

    ticks.Clear();
    yield return Phase(0.25f, 0.04f, 1.5f, ticks, stats);
    var shrinkTicks = new System.Collections.Generic.List<string>(ticks);
    log.AppendLine($"shrink 0.58->0.09 m diagonal: humPitch {stats[0]:F2}->{stats[1]:F2} peakHumVol={stats[2]:F2} ticks={shrinkTicks.Count} pitches=[{string.Join(" ", shrinkTicks)}]");
    log.AppendLine("  (expect pitch falling, a similar tick count, each tick ~0.8x the grow tick at the same size)");

    d.SetData("xrsim.done", true);
}
runner.StartCoroutine(Run());
return "frame check started";
