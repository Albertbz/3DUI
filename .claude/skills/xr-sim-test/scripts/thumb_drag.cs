// thumb_drag.cs: a pinch on a gallery thumbnail decides by direction (needs pose.cs palm and >= Visible Slots + 2
// photos). Sideways scrolls the strip under the finger; toward the viewer pulls the photo out, growing and
// brightening on the way; a small jitter does neither. Also prints the poke hover zones that used to block pinching.
// Pinches are sent as pointer events on the thumbnail's Grabbable. Runs as a coroutine; read with wait_log.sh.
var gallery = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.WristGallery>();
var lib = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoLibrary>();
var visual = gallery.transform.Find("Visual");
var strip = visual.Find("Strip");
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var log = new System.Text.StringBuilder();
var d = System.AppDomain.CurrentDomain;
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);
if (!visual.gameObject.activeSelf) { log.AppendLine("gallery is not visible; run pose.cs palm first"); d.SetData("xrsim.done", true); return "gallery hidden"; }

var spacing = new UnityEditor.SerializedObject(gallery).FindProperty("slotSpacing").floatValue;
var swipe = visual.Find("SwipeSurface").GetComponent<Oculus.Interaction.PokeInteractable>();
var cross = strip.GetComponentInChildren<SpatialPhotos.PokeButton>(true).Interactable;
log.AppendLine($"poke hover zones: swipe surface {swipe.EnterHoverNormal * 100f:F0} cm, × button {cross.EnterHoverNormal * 100f:F0} cm (expect 2 and 2, was 15)");

System.Func<int, SpatialPhotos.GalleryThumbnail> slotThumb = i => strip.GetChild(i).GetComponentInChildren<SpatialPhotos.GalleryThumbnail>(true);
System.Func<int> firstShown = () => { var e = slotThumb(0).Entry; for (int i = 0; i < lib.Entries.Count; i++) if (lib.Entries[i] == e) return i; return -1; };
System.Func<float> stripPos = () => firstShown() - strip.GetChild(0).localPosition.x / spacing - (strip.childCount - 2) * 0.5f;
System.Func<int> photoCount = () => UnityEngine.Object.FindObjectsByType<SpatialPhotos.PhotoObject>(UnityEngine.FindObjectsSortMode.None).Length;

System.Collections.IEnumerator Pinch(SpatialPhotos.GalleryThumbnail thumb, int id, UnityEngine.Vector3 move, int frames, System.Action<float> during)
{
    var grab = thumb.GetComponent<Oculus.Interaction.Grabbable>();
    var from = thumb.transform.position;
    var pose = new UnityEngine.Pose(from, thumb.transform.rotation);
    void Send(Oculus.Interaction.PointerEventType t) => grab.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, t, pose));
    Send(Oculus.Interaction.PointerEventType.Hover);
    Send(Oculus.Interaction.PointerEventType.Select);
    for (int i = 1; i <= frames; i++)
    {
        pose.position = from + move * (i / (float)frames);
        Send(Oculus.Interaction.PointerEventType.Move);
        yield return new UnityEngine.WaitForSeconds(0.03f);
        during?.Invoke(i / (float)frames);
    }
    Send(Oculus.Interaction.PointerEventType.Unselect);
    Send(Oculus.Interaction.PointerEventType.Unhover);
}

System.Collections.IEnumerator Run()
{
    // 1. Sideways: 8 cm toward the viewer's left = ~1.2 slots toward older photos, no photo comes out
    var thumb = slotThumb(1);
    float start = stripPos(); int before = photoCount();
    var samples = new System.Collections.Generic.List<string>();
    float drift = 0f;
    yield return Pinch(thumb, 301, -strip.right * 0.08f + (head.position - thumb.transform.position).normalized * 0.01f, 10, k =>
    {
        samples.Add((stripPos() - start).ToString("F2"));
        if (k > 0.5f) drift = UnityEngine.Mathf.Max(drift, thumb.transform.localPosition.magnitude);
    });
    log.AppendLine($"sideways 8 cm: strip moved {string.Join(" ", samples)} (expect 0 at first, then steady steps to ~1.2)");
    log.AppendLine($"  photos spawned={photoCount() - before} thumbStaysInSlotWhileScrolling={drift < 0.002f} (expect 0 True)");
    yield return new UnityEngine.WaitForSeconds(0.8f);
    log.AppendLine($"  released: settled at {stripPos():F2} (expect {UnityEngine.Mathf.Round(start + 0.08f / spacing):F0}.00)");

    // 2. Jitter: 1 cm wobble does nothing
    start = stripPos();
    yield return Pinch(thumb, 302, strip.right * 0.006f + (head.position - thumb.transform.position).normalized * 0.006f, 6, null);
    yield return new UnityEngine.WaitForSeconds(0.6f);
    log.AppendLine($"jitter 1 cm: strip moved {stripPos() - start:F2} photos spawned={photoCount() - before} (expect 0.00 0)");

    // 3. Toward the viewer with some sideways wobble: grows, brightens, comes out at 10 cm; the strip stays put
    thumb = slotThumb(1);
    start = stripPos();
    var renderer = thumb.GetComponentInChildren<UnityEngine.Renderer>().transform;
    var outline = thumb.GetComponentInChildren<SpatialPhotos.FrameOutline>(true);
    var baseScale = renderer.localScale.y;
    var feedback = new System.Collections.Generic.List<string>();
    var toward = (head.position - thumb.transform.position).normalized;
    yield return Pinch(thumb, 303, toward * 0.13f + strip.right * 0.03f, 13, k =>
    {
        if (photoCount() == before) feedback.Add($"{renderer.localScale.y / baseScale:F2}/{outline.Activity:F2}");
    });
    log.AppendLine($"pull 13 cm toward you (+3 cm sideways): size/glow before coming out {string.Join(" ", feedback)} (expect rising toward 1.15/1.00)");
    log.AppendLine($"  photos spawned={photoCount() - before} strip moved={stripPos() - start:F2} (expect 1 0.00)");
    yield return new UnityEngine.WaitForSeconds(0.5f);
    log.AppendLine($"  released: thumb back to size={UnityEngine.Mathf.Abs(renderer.localScale.y / baseScale - 1f) < 0.01f} (expect True)");
    foreach (var ph in UnityEngine.Object.FindObjectsByType<SpatialPhotos.PhotoObject>(UnityEngine.FindObjectsSortMode.None)) UnityEngine.Object.Destroy(ph.gameObject);
    yield return new UnityEngine.WaitForSeconds(0.5f);

    // 4. Changing your mind: 6 cm to the left (scrolls), then 12 cm toward you: stops scrolling and pulls the photo out
    thumb = slotThumb(1);
    before = photoCount(); start = stripPos();
    float scrolled = 0f, pokesWhileHeld = -1f;
    yield return Pinch(thumb, 304, strip.right * 0.06f, 8, k => { scrolled = stripPos() - start; pokesWhileHeld = swipe.enabled ? 1f : 0f; });
    // Pinch() released; repeat as one continuous gesture instead
    yield return new UnityEngine.WaitForSeconds(0.8f);
    start = stripPos();
    var grab = thumb.GetComponent<Oculus.Interaction.Grabbable>();
    var p0 = thumb.transform.position;
    var pose = new UnityEngine.Pose(p0, thumb.transform.rotation);
    void Send(Oculus.Interaction.PointerEventType t) => grab.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(305, t, pose));
    Send(Oculus.Interaction.PointerEventType.Hover); Send(Oculus.Interaction.PointerEventType.Select);
    for (int i = 1; i <= 8; i++) { pose.position = p0 - strip.right * (0.06f * i / 8f); Send(Oculus.Interaction.PointerEventType.Move); yield return new UnityEngine.WaitForSeconds(0.03f); }
    float midScroll = stripPos() - start;
    var p1 = pose.position;
    for (int i = 1; i <= 12; i++) { pose.position = p1 + toward * (0.12f * i / 12f); Send(Oculus.Interaction.PointerEventType.Move); yield return new UnityEngine.WaitForSeconds(0.03f); }
    int spawned = photoCount() - before;
    Send(Oculus.Interaction.PointerEventType.Unselect); Send(Oculus.Interaction.PointerEventType.Unhover);
    yield return new UnityEngine.WaitForSeconds(0.8f);
    log.AppendLine($"sideways 6 cm then toward you 12 cm: scrolled {midScroll:F2} slots, then photos spawned={spawned}, settled at {stripPos():F2} (expect ~0.9, 1, a whole number)");
    log.AppendLine($"  pokes off while a thumbnail is held: swipe surface enabled={pokesWhileHeld == 1f} (expect False); after release enabled={swipe.enabled} (expect True)");
    foreach (var ph in UnityEngine.Object.FindObjectsByType<SpatialPhotos.PhotoObject>(UnityEngine.FindObjectsSortMode.None)) UnityEngine.Object.Destroy(ph.gameObject);
    yield return new UnityEngine.WaitForSeconds(0.5f);

    // 5. Straight up out of the tray (not toward you) also pulls
    thumb = slotThumb(1);
    before = photoCount(); start = stripPos();
    yield return Pinch(thumb, 306, UnityEngine.Vector3.up * 0.12f, 12, null);
    log.AppendLine($"pull 12 cm straight up: photos spawned={photoCount() - before} strip moved={stripPos() - start:F2} (expect 1 0.00)");
    foreach (var ph in UnityEngine.Object.FindObjectsByType<SpatialPhotos.PhotoObject>(UnityEngine.FindObjectsSortMode.None)) UnityEngine.Object.Destroy(ph.gameObject);
    d.SetData("xrsim.done", true);
}
lib.StartCoroutine(Run());
return "thumbnail drag test started";
