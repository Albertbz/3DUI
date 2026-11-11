// snap.cs <room|spawn|wall|table|far|tilted|release|state> [distance]
// Tests snapping photos to surfaces. room: what MRUK loaded. spawn: takes one photo in front of the head.
// wall/table/far/tilted: holds the newest photo (BeginExternalHold) near the nearest wall, above a table
// (or the floor), 40 cm from everything, or 8 cm from a wall but turned 80° away from it.
// release: EndExternalHold. state: preview, target and the photo's pose relative to the surface.
var mruk = Meta.XR.MRUtilityKit.MRUK.Instance;
var room = mruk != null && mruk.IsInitialized ? mruk.GetCurrentRoom() : null;
var head = SpatialPhotos.XRRig.Head;
var photos = UnityEngine.Object.FindObjectsByType<SpatialPhotos.PhotoObject>(UnityEngine.FindObjectsSortMode.InstanceID);
var ph = photos.Length > 0 ? photos[photos.Length - 1] : null;
string mode = "__ARG1__";
float dist = float.TryParse("__ARG2__", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.08f;
string F(UnityEngine.Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

Meta.XR.MRUtilityKit.MRUKAnchor Nearest(System.Func<Meta.XR.MRUtilityKit.MRUKAnchor, bool> pick)
{
    Meta.XR.MRUtilityKit.MRUKAnchor best = null; float bd = float.MaxValue;
    foreach (var a in room.Anchors)
    {
        if (!pick(a)) continue;
        float ad = (a.transform.position - head.position).sqrMagnitude;
        if (ad < bd) { bd = ad; best = a; }
    }
    return best;
}

if (mode == "room")
{
    if (mruk == null) return "no MRUK in scene";
    if (room == null) return $"MRUK initialized={mruk.IsInitialized}, no room";
    var labels = new System.Collections.Generic.List<string>();
    foreach (var a in room.Anchors) labels.Add(a.Label.ToString());
    return $"room anchors={room.Anchors.Count}: {string.Join(",", labels)}";
}

if (mode == "spawn")
{
    var capture = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoCapture>();
    float w = 0.2f, h = 0.15f;
    var center = head.position + head.forward * 0.5f;
    var rot = UnityEngine.Quaternion.LookRotation(head.forward, UnityEngine.Vector3.up);
    var r = rot * UnityEngine.Vector3.right * (w / 2); var u = rot * UnityEngine.Vector3.up * (h / 2);
    capture.Capture(new[] { center - r - u, center + r - u, center + r + u, center - r + u }, new UnityEngine.Pose(center, rot), w, h, head.position, UnityEngine.Time.time);
    return "spawned";
}

if (ph == null) return "no photo in scene";
var preview = UnityEngine.GameObject.Find("SnapPreview");

if (mode == "release") { ph.EndExternalHold(); return "released"; }

if (mode == "state")
{
    string s = $"held={ph.IsHeld} preview={(preview != null && preview.activeSelf)} pos={F(ph.transform.position)} fwd={F(ph.transform.forward)} up={F(ph.transform.up)}";
    if (room != null)
    {
        float sd = room.TryGetClosestSurfacePosition(ph.transform.position, out var sp, out var sa, out var sn);
        if (UnityEngine.Vector3.Dot(sn, ph.transform.position - sp) < 0) sn = -sn;
        s += $" | surface={sa?.Label} dist={sd * 100f:F1}cm normal={F(sn)} fwd·-n={UnityEngine.Vector3.Dot(ph.transform.forward, -sn):F3} up·worldUp={UnityEngine.Vector3.Dot(ph.transform.up, UnityEngine.Vector3.up):F3}";
    }
    if (preview != null && preview.activeSelf)
        s += $" | previewPos={F(preview.transform.position)} previewFwd={F(preview.transform.forward)}";
    return s;
}

if (room == null) return "no room loaded";
ph.BeginExternalHold();
UnityEngine.Vector3 point, normal;
if (mode == "wall" || mode == "tilted" || mode == "far")
{
    var wall = Nearest(a => a.Label.HasFlag(Meta.XR.MRUtilityKit.MRUKAnchor.SceneLabels.WALL_FACE));
    if (wall == null) return "no wall";
    // A point on the wall at eye height, toward the head
    point = wall.transform.position + UnityEngine.Vector3.up * (head.position.y - wall.transform.position.y);
    normal = wall.transform.forward;
    if (UnityEngine.Vector3.Dot(normal, head.position - point) < 0) normal = -normal;
    float off = mode == "far" ? 0.4f : dist;
    // Facing the wall but turned 20° (or 80° for tilted) and a little rolled, as a hand would hold it
    var face = UnityEngine.Quaternion.LookRotation(-normal, UnityEngine.Vector3.up);
    var rot = face * UnityEngine.Quaternion.Euler(0, mode == "tilted" ? 80f : 20f, 8f);
    ph.transform.SetPositionAndRotation(point + normal * off, rot);
    return $"{mode}: wall {wall.name} at {F(point)} normal {F(normal)}";
}
if (mode == "table")
{
    var table = Nearest(a => a.VolumeBounds.HasValue && (a.Label.HasFlag(Meta.XR.MRUtilityKit.MRUKAnchor.SceneLabels.TABLE) || a.Label.HasFlag(Meta.XR.MRUtilityKit.MRUKAnchor.SceneLabels.STORAGE) || a.Label.HasFlag(Meta.XR.MRUtilityKit.MRUKAnchor.SceneLabels.BED)));
    UnityEngine.Vector3 top;
    string what;
    if (table != null)
    {
        var b = table.VolumeBounds.Value;
        // The closest point to a spot high above the box's centre is the middle of its top
        table.GetClosestSurfacePosition(table.transform.TransformPoint(b.center) + UnityEngine.Vector3.up * 2f, out top, out _);
        what = $"{table.Label} {table.name}";
    }
    else
    {
        var floor = room.FloorAnchor;
        top = new UnityEngine.Vector3(head.position.x, floor.transform.position.y, head.position.z) + UnityEngine.Vector3.ProjectOnPlane(head.forward, UnityEngine.Vector3.up).normalized * 0.5f;
        what = "floor";
    }
    // Picture side up, tilted 25° from flat and turned 30° about up
    var rot = UnityEngine.Quaternion.Euler(0, 30f, 0) * UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.down, UnityEngine.Vector3.forward) * UnityEngine.Quaternion.Euler(25f, 0, 0);
    ph.transform.SetPositionAndRotation(top + UnityEngine.Vector3.up * dist, rot);
    return $"table: {what} top at {F(top)}, held up={F(ph.transform.up)}";
}
return "unknown mode " + mode;
