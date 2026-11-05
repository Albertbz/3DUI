// delete_photo.cs [popup|cancel|confirm] [thumbnail index]
// Deleting from the wrist gallery (needs the gallery visible: pose.cs palm).
//   popup   pokes the thumbnail's corner cross: the confirmation pops up above it, the thumbnail turns red
//   cancel  pokes Cancel: the popup closes and nothing is deleted
//   confirm pokes Confirm: the photo leaves the library and its PNG + JSON leave the disk
// Pokes are sent as Interaction SDK pointer events on the buttons' PokeInteractables.
var g = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.WristGallery>();
var lib = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoLibrary>();
var v = g.transform.Find("Visual");
var strip = v.Find("Strip");
var popup = v.Find("DeleteConfirmation");
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
int index = "__ARG2__" == "" ? 0 : int.Parse("__ARG2__");
var thumb = strip.GetChild(index).GetComponentInChildren<SpatialPhotos.GalleryThumbnail>(true);
var s = new System.Text.StringBuilder();

void Poke(UnityEngine.Transform button)
{
    var poke = button.GetComponent<Oculus.Interaction.PokeInteractable>();
    var pose = new UnityEngine.Pose(button.position, button.rotation);
    foreach (var t in new[] { Oculus.Interaction.PointerEventType.Hover, Oculus.Interaction.PointerEventType.Select, Oculus.Interaction.PointerEventType.Unselect, Oculus.Interaction.PointerEventType.Unhover })
        poke.PublishPointerEvent(new Oculus.Interaction.PointerEvent(950, t, pose));
}
float Side(UnityEngine.Vector3 p) => UnityEngine.Vector3.Dot(p - v.position, head.right);
float Up(UnityEngine.Vector3 p) => UnityEngine.Vector3.Dot(p - v.position, head.up);

int before = lib.Entries.Count;
var entry = thumb.Entry;
string png = entry?.PngPath;
switch ("__ARG1__")
{
    case "popup":
        var cross = thumb.transform.Find("DeleteButton");
        var photo = thumb.GetComponentInChildren<UnityEngine.Renderer>().transform;
        s.AppendLine($"cross: active={cross.gameObject.activeInHierarchy} topRight={Side(cross.position) > Side(photo.position) && Up(cross.position) > Up(photo.position)} inFront={UnityEngine.Vector3.Dot(cross.position - photo.position, -v.forward) > 0.002f} (expect True True True)");
        Poke(cross);
        s.AppendLine($"after poke: popup={popup.gameObject.activeSelf} aboveGallery={Up(popup.position) > Up(strip.position) + 0.04f} overPhoto={UnityEngine.Mathf.Abs(Side(popup.position) - Side(thumb.transform.position)):F3}m off marked={thumb.MarkedForDeletion} library={lib.Entries.Count} (expect True True small True {before})");
        foreach (var label in popup.GetComponentsInChildren<UnityEngine.TextMesh>()) s.AppendLine($"  label '{label.text}' on {label.transform.parent.name}");
        break;
    case "cancel":
        Poke(popup.Find("CancelButton"));
        s.AppendLine($"after cancel: popup={popup.gameObject.activeSelf} marked={thumb.MarkedForDeletion} library={lib.Entries.Count} (expect False False {before})");
        break;
    case "confirm":
        Poke(popup.Find("ConfirmButton"));
        bool inLibrary = false;
        foreach (var e in lib.Entries) inLibrary |= e == entry;
        s.AppendLine($"after confirm: popup={popup.gameObject.activeSelf} library={before}->{lib.Entries.Count} entryInLibrary={inLibrary} slotNowShowsAnother={thumb.Entry != entry} anyMarked={System.Linq.Enumerable.Any(strip.GetComponentsInChildren<SpatialPhotos.GalleryThumbnail>(), t => t.MarkedForDeletion)} (expect False -1 False True False)");
        s.AppendLine($"deleted file: {png}");
        break;
}
return s.ToString();
