// smooth_scroll.cs: the strip follows the finger while dragging the swipe surface (needs pose.cs palm and
// >= Visible Slots + 2 photos). Drags part of a slot and samples the slots every frame, then checks that the
// strip settles on the nearest photo after release. Runs as a coroutine; read the result with wait_log.sh.
var gallery = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.WristGallery>();
var visual = gallery.transform.Find("Visual");
var strip = visual.Find("Strip");
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var swipe = visual.Find("SwipeSurface").GetComponent<Oculus.Interaction.PokeInteractable>();
var lib = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoLibrary>();
var log = new System.Text.StringBuilder();
var d = System.AppDomain.CurrentDomain;
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);
var spacing = new UnityEditor.SerializedObject(gallery).FindProperty("slotSpacing").floatValue;

System.Func<int> firstShown = () => { var e = strip.GetChild(0).GetComponentInChildren<SpatialPhotos.GalleryThumbnail>(true).Entry; for (int i = 0; i < lib.Entries.Count; i++) if (lib.Entries[i] == e) return i; return -1; };
// Where the newest-shown photo sits, in slots: 0 = leftmost position, grows as the strip moves left
System.Func<float> stripPos = () => firstShown() - strip.GetChild(0).localPosition.x / spacing - (strip.childCount - 2) * 0.5f;
void Poke(UnityEngine.Pose pose, Oculus.Interaction.PointerEventType t) => swipe.PublishPointerEvent(new Oculus.Interaction.PointerEvent(451, t, pose));

System.Collections.IEnumerator Drag(float slots, int frames)
{
    var a = strip.position + head.right * 0.03f;
    var b = a - head.right * (slots * spacing);
    var pose = new UnityEngine.Pose(a, swipe.transform.rotation);
    Poke(pose, Oculus.Interaction.PointerEventType.Hover);
    Poke(pose, Oculus.Interaction.PointerEventType.Select);
    float start = stripPos();
    var samples = new System.Collections.Generic.List<string>();
    for (int i = 1; i <= frames; i++)
    {
        pose.position = UnityEngine.Vector3.Lerp(a, b, i / (float)frames);
        Poke(pose, Oculus.Interaction.PointerEventType.Move);
        yield return null;
        samples.Add((stripPos() - start).ToString("F2"));
    }
    log.AppendLine($"drag {slots} slots: strip moved {string.Join(" ", samples)} (expect steady steps up to {slots})");
    Poke(pose, Oculus.Interaction.PointerEventType.Unselect);
    Poke(pose, Oculus.Interaction.PointerEventType.Unhover);
    yield return new UnityEngine.WaitForSeconds(0.8f);
    log.AppendLine($"  released: settled at {stripPos():F2} (expect {UnityEngine.Mathf.Round(start + slots):F0}.00)");
}

System.Collections.IEnumerator Run()
{
    yield return Drag(0.4f, 8);   // Less than half a slot: springs back
    yield return Drag(0.7f, 8);   // More than half: moves on by one
    var fades = new System.Text.StringBuilder();
    foreach (var t in strip.GetComponentsInChildren<SpatialPhotos.GalleryThumbnail>()) fades.Append($"{t.Fade:F2} ");
    log.AppendLine($"  fades at rest: {fades}(expect 1.00 for the visible ones, 0.00 for the spare)");
    yield return Drag(-0.7f, 8);
    d.SetData("xrsim.done", true);
}
lib.StartCoroutine(Run());
return "smooth scroll test started";
