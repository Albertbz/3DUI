// Gallery interaction test (needs the gallery visible: pose.cs face, and >= Visible Slots + 2 photos):
// layout, thumbnail short pull / long pull, swipe toward the viewer's left, and the two arrows.
// Runs as a coroutine; read the result with wait_log.sh.
var gallery = UnityEngine.Object.FindFirstObjectByType<WristGallery>();
var lib = UnityEngine.Object.FindFirstObjectByType<PhotoLibrary>();
var visual = gallery.transform.Find("Visual");
var strip = visual.Find("Strip");
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var log = new System.Text.StringBuilder();
var d = System.AppDomain.CurrentDomain;
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);
if (!visual.gameObject.activeSelf) { log.AppendLine("gallery is not visible; run pose.cs face first"); d.SetData("xrsim.done", true); return "gallery hidden"; }

System.Func<int, GalleryThumbnail> slotThumb = i => strip.GetChild(i).GetComponentInChildren<GalleryThumbnail>(true);
System.Func<int> firstShown = () => { var e = slotThumb(0).Entry; for (int i = 0; i < lib.Entries.Count; i++) if (lib.Entries[i] == e) return i; return -1; };
System.Func<int> photoCount = () => UnityEngine.Object.FindObjectsByType<PhotoObject>(UnityEngine.FindObjectsSortMode.None).Length;
float Side(UnityEngine.Vector3 p) => UnityEngine.Vector3.Dot(p - visual.position, head.right);

var gso = new UnityEditor.SerializedObject(gallery);
float spacing = gso.FindProperty("slotSpacing").floatValue;
float widest = 0f;
for (int i = 0; i < strip.childCount; i++) { var th = slotThumb(i); if (th.gameObject.activeSelf) widest = UnityEngine.Mathf.Max(widest, th.GetComponentInChildren<UnityEngine.Renderer>().transform.localScale.x); }
log.AppendLine($"layout: widest thumbnail {widest:F3} m < slot spacing {spacing:F3} m: {widest < spacing}");
log.AppendLine($"layout: newest photo on viewer's left={Side(strip.GetChild(0).position) < 0}, LeftArrow on left={Side(visual.Find("LeftArrow").position) < 0}, RightArrow on right={Side(visual.Find("RightArrow").position) > 0} (expect True True True)");

System.Collections.IEnumerator Drag(Oculus.Interaction.PointableElement target, int id, UnityEngine.Vector3 from, UnityEngine.Vector3 to, int frames, System.Action beforeRelease)
{
    var pose = new UnityEngine.Pose(from, target.transform.rotation);
    target.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, Oculus.Interaction.PointerEventType.Hover, pose));
    target.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, Oculus.Interaction.PointerEventType.Select, pose));
    for (int i = 1; i <= frames; i++)
    {
        pose.position = UnityEngine.Vector3.Lerp(from, to, i / (float)frames);
        target.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, Oculus.Interaction.PointerEventType.Move, pose));
        yield return new UnityEngine.WaitForSeconds(0.03f);
    }
    beforeRelease?.Invoke();
    target.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, Oculus.Interaction.PointerEventType.Unselect, pose));
    target.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, Oculus.Interaction.PointerEventType.Unhover, pose));
}

void Poke(Oculus.Interaction.PokeInteractable target, int id, UnityEngine.Pose pose, Oculus.Interaction.PointerEventType type) =>
    target.PublishPointerEvent(new Oculus.Interaction.PointerEvent(id, type, pose));

System.Collections.IEnumerator Run()
{
    var thumb = slotThumb(1);
    var grab = thumb.GetComponent<Oculus.Interaction.Grabbable>();
    var slotPos = thumb.transform.parent.position;
    var toHead = (head.position - slotPos).normalized;
    int before = photoCount();

    yield return Drag(grab, 201, slotPos, slotPos + toHead * 0.05f, 10, () =>
        log.AppendLine($"short pull: held={GalleryThumbnail.AnyHeld} galleryVisible={visual.gameObject.activeSelf} spawned={photoCount() - before} (expect True True 0)"));
    yield return new UnityEngine.WaitForSeconds(0.6f);
    log.AppendLine($"short pull released: back in slot={thumb.transform.localPosition.magnitude < 0.002f} (expect True)");

    yield return Drag(grab, 202, slotPos, slotPos + toHead * 0.2f + head.right * 0.1f, 25, () =>
    {
        foreach (var ph in UnityEngine.Object.FindObjectsByType<PhotoObject>(UnityEngine.FindObjectsSortMode.None))
        {
            var tex = ph.GetComponentInChildren<UnityEngine.Renderer>().material.mainTexture;
            log.AppendLine($"long pull: photo spawned held={ph.IsHeld} followsThumb={UnityEngine.Vector3.Distance(ph.transform.position, thumb.transform.position) < 0.01f} texture={tex.width}x{tex.height} thumbHidden={!thumb.GetComponentInChildren<UnityEngine.Renderer>().enabled} galleryVisible={visual.gameObject.activeSelf} (expect True True full-res True True)");
        }
    });
    yield return new UnityEngine.WaitForSeconds(0.6f);
    var after = UnityEngine.Object.FindObjectsByType<PhotoObject>(UnityEngine.FindObjectsSortMode.None);
    log.AppendLine($"long pull released: photos={after.Length - before} floating={(after.Length > 0 && !after[0].IsHeld && after[0].GetComponent<UnityEngine.Rigidbody>().isKinematic)} thumbBackInSlot={thumb.transform.localPosition.magnitude < 0.002f} thumbVisible={thumb.GetComponentInChildren<UnityEngine.Renderer>().enabled} (expect 1 True True True)");
    foreach (var ph in after) UnityEngine.Object.Destroy(ph.gameObject);

    // Swipe 16 cm toward the viewer's left: should page to older photos (2 steps at 6.5-7.5 cm spacing)
    var swipe = visual.Find("SwipeSurface").GetComponent<Oculus.Interaction.PokeInteractable>();
    var a = strip.position + head.right * 0.08f;
    var b = strip.position - head.right * 0.08f;
    var pose = new UnityEngine.Pose(a, swipe.transform.rotation);
    int was = firstShown();
    Poke(swipe, 401, pose, Oculus.Interaction.PointerEventType.Hover);
    Poke(swipe, 401, pose, Oculus.Interaction.PointerEventType.Select);
    for (int i = 1; i <= 10; i++) { pose.position = UnityEngine.Vector3.Lerp(a, b, i / 10f); Poke(swipe, 401, pose, Oculus.Interaction.PointerEventType.Move); yield return null; }
    Poke(swipe, 401, pose, Oculus.Interaction.PointerEventType.Unselect);
    Poke(swipe, 401, pose, Oculus.Interaction.PointerEventType.Unhover);
    log.AppendLine($"swipe left: first shown {was} -> {firstShown()} (expect it to increase = older)");

    foreach (var (name, expect) in new[] { ("LeftArrow", -1), ("RightArrow", 1) })
    {
        var arrow = visual.Find(name)?.GetComponent<Oculus.Interaction.PokeInteractable>();
        if (arrow == null) { log.AppendLine($"{name}: missing"); continue; }
        var p = new UnityEngine.Pose(arrow.transform.position, arrow.transform.rotation);
        int w = firstShown();
        foreach (var t in new[] { Oculus.Interaction.PointerEventType.Hover, Oculus.Interaction.PointerEventType.Select, Oculus.Interaction.PointerEventType.Unselect, Oculus.Interaction.PointerEventType.Unhover })
            Poke(arrow, 402, p, t);
        yield return null;
        log.AppendLine($"{name}: first shown {w} -> {firstShown()} (expect {(expect < 0 ? "-1, newer" : "+1, older")} unless at the end)");
    }
    d.SetData("xrsim.done", true);
}
lib.StartCoroutine(Run());
return "gallery interaction test started";
