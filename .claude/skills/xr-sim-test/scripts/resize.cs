// Two-hand resize test: grabs the first photo in the scene (takes one in front of the head if there is none)
// at its left and right edges with two pointers, moves them apart, then together, then lets go one hand at a
// time. Grabs are sent as Interaction SDK pointer events on the photo's Grabbable.
// Runs as a coroutine; read the result with wait_log.sh.
var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
var runner = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoLibrary>();
var photo = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoObject>();
if (photo == null)
{
    var capture = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoCapture>();
    float w = 0.2f, h = 0.15f;
    var center = head.position + head.forward * 0.5f;
    var rot = UnityEngine.Quaternion.LookRotation(head.forward, head.up);
    var r = rot * UnityEngine.Vector3.right * (w / 2); var u = rot * UnityEngine.Vector3.up * (h / 2);
    capture.Capture(new[] { center - r - u, center + r - u, center + r + u, center - r + u }, new UnityEngine.Pose(center, rot), w, h);
}
var log = new System.Text.StringBuilder();
var d = System.AppDomain.CurrentDomain;
d.SetData("xrsim.log", log); d.SetData("xrsim.done", null);

System.Collections.IEnumerator Run()
{
    yield return new UnityEngine.WaitForSeconds(0.5f); // Let a new photo finish growing in
    photo = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoObject>();
    var grabbable = photo.GetComponent<Oculus.Interaction.Grabbable>();
    var outline = photo.GetComponentInChildren<SpatialPhotos.FrameOutline>();
    var t = photo.transform;
    void Send(int id, Oculus.Interaction.PointerEventType type, UnityEngine.Vector3 p) =>
        grabbable.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(id, type, new UnityEngine.Pose(p, t.rotation)));
    // Where a world point sits on the photo, in fractions of its size (0,0 = centre, 0.5 = right edge)
    UnityEngine.Vector2 OnPhoto(UnityEngine.Vector3 p) { var l = t.InverseTransformPoint(p); return new UnityEngine.Vector2(l.x / photo.Size.x, l.y / photo.Size.y); }

    var size0 = photo.Size;
    float thickness = outline.Thickness;
    var left = t.position - t.right * size0.x * 0.4f;
    var right = t.position + t.right * size0.x * 0.4f;
    var leftSpot = OnPhoto(left); var rightSpot = OnPhoto(right);

    Send(1, Oculus.Interaction.PointerEventType.Hover, left);
    Send(1, Oculus.Interaction.PointerEventType.Select, left);
    yield return null;
    Send(2, Oculus.Interaction.PointerEventType.Hover, right);
    Send(2, Oculus.Interaction.PointerEventType.Select, right);
    yield return null;
    log.AppendLine($"two hands: held={photo.IsHeld} grabPoints={grabbable.SelectingPointsCount} captureFrame={SpatialPhotos.HandPinchManager.IsProximityPinchActive} (expect True 2 False)");

    System.Collections.IEnumerator MoveTo(UnityEngine.Vector3 l, UnityEngine.Vector3 r, int frames)
    {
        var l0 = left; var r0 = right;
        for (int i = 1; i <= frames; i++)
        {
            left = UnityEngine.Vector3.Lerp(l0, l, i / (float)frames); right = UnityEngine.Vector3.Lerp(r0, r, i / (float)frames);
            Send(1, Oculus.Interaction.PointerEventType.Move, left);
            Send(2, Oculus.Interaction.PointerEventType.Move, right);
            yield return new UnityEngine.WaitForSeconds(0.03f);
        }
    }

    // Apart to 1.5x the distance, symmetric about the centre
    var mid = (left + right) * 0.5f; var half = (right - left) * 0.5f;
    yield return MoveTo(mid - half * 1.5f, mid + half * 1.5f, 15);
    log.AppendLine($"apart 1.5x: size {size0.x:F3}x{size0.y:F3} -> {photo.Size.x:F3}x{photo.Size.y:F3} ratio={photo.Size.y / size0.y:F2} aspectKept={UnityEngine.Mathf.Abs(photo.Size.x / photo.Size.y - size0.x / size0.y) < 0.001f} (expect 1.50 True)");
    log.AppendLine($"  hands stay on the same spots: left {leftSpot:F2}->{OnPhoto(left):F2} right {rightSpot:F2}->{OnPhoto(right):F2}");
    log.AppendLine($"  transform scale={t.localScale.x:F2} outline thickness {thickness:F4}->{outline.Thickness:F4} (expect 1.00, unchanged)");

    // Together to 0.6x of the original distance, while turning the pair 30 degrees about the view axis
    var turn = UnityEngine.Quaternion.AngleAxis(30f, head.forward);
    yield return MoveTo(mid - turn * half * 0.6f, mid + turn * half * 0.6f, 15);
    log.AppendLine($"together 0.6x + 30° turn: ratio={photo.Size.y / size0.y:F2} roll={UnityEngine.Vector3.SignedAngle(head.right, t.right, head.forward):F0}° (expect 0.60, ~30°)");

    // Very close: clamped at the minimum height
    yield return MoveTo(mid - turn * half * 0.01f, mid + turn * half * 0.01f, 10);
    log.AppendLine($"hands almost touching: height={photo.Size.y:F3} m (expect the 0.050 minimum)");
    yield return MoveTo(mid - turn * half * 0.6f, mid + turn * half * 0.6f, 10);

    // Let go of one hand: the other keeps moving it, without changing its size
    var sizeAtOne = photo.Size;
    Send(2, Oculus.Interaction.PointerEventType.Unselect, right);
    Send(2, Oculus.Interaction.PointerEventType.Unhover, right);
    var p0 = t.position;
    for (int i = 1; i <= 10; i++) { left += head.up * 0.005f; Send(1, Oculus.Interaction.PointerEventType.Move, left); yield return new UnityEngine.WaitForSeconds(0.03f); }
    log.AppendLine($"one hand left: held={photo.IsHeld} moved={UnityEngine.Vector3.Distance(p0, t.position):F3} m sizeKept={photo.Size == sizeAtOne} (expect True 0.050 True)");
    Send(1, Oculus.Interaction.PointerEventType.Unselect, left);
    Send(1, Oculus.Interaction.PointerEventType.Unhover, left);
    yield return new UnityEngine.WaitForSeconds(0.5f);
    log.AppendLine($"released: held={photo.IsHeld} floating={photo.GetComponent<UnityEngine.Rigidbody>().isKinematic} size={photo.Size.x:F3}x{photo.Size.y:F3} (expect False True, the resized size)");
    d.SetData("xrsim.done", true);
}
runner.StartCoroutine(Run());
return "resize test started";
