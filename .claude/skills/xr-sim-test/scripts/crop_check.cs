// Saves the current passthrough camera image (the photo itself used one from just before the release; the scene
// is static in the simulator, so they match) with the last capture frame's outline in red, reprojected the way
// PhotoCapture does it: rays from the dominant eye through the corners, hitting a plane at the scene depth
// (__OUT__/shots/camera_with_crop.png). Compare it with the newest saved photo: they should show the same region.
var pca = UnityEngine.Object.FindFirstObjectByType<Meta.XR.PassthroughCameraAccess>();
if (pca == null || !pca.IsPlaying) return "passthrough camera is not playing";
var src = pca.GetTexture();
var rt = UnityEngine.RenderTexture.GetTemporary(src.width, src.height, 0);
UnityEngine.Graphics.Blit(src, rt);
UnityEngine.RenderTexture.active = rt;
var img = new UnityEngine.Texture2D(src.width, src.height, UnityEngine.TextureFormat.RGB24, false);
img.ReadPixels(new UnityEngine.Rect(0, 0, src.width, src.height), 0, 0);
UnityEngine.RenderTexture.active = null; UnityEngine.RenderTexture.ReleaseTemporary(rt);
var box = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.ImageBoundaryBox>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var publicFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
// The frame state the last release captured (from Release Lookback before it)
var state = typeof(SpatialPhotos.ImageBoundaryBox).GetField("captured", flags).GetValue(box);
var corners = (UnityEngine.Vector3[])state.GetType().GetProperty("Corners", publicFlags).GetValue(state);
var eye = (UnityEngine.Vector3)state.GetType().GetField("Eye", publicFlags).GetValue(state);
var pose = (UnityEngine.Pose)state.GetType().GetField("Pose", publicFlags).GetValue(state);
var capture = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.PhotoCapture>();
float frameDistance = UnityEngine.Vector3.Distance(eye, pose.position);
float depth = (float)typeof(SpatialPhotos.PhotoCapture).GetMethod("SceneDepth", flags).Invoke(capture, new object[] { eye, pose.position, frameDistance });
var camPose = pca.GetCameraPose();
var quad = new UnityEngine.Vector2[4];
for (int i = 0; i < 4; i++)
{
    var v = pca.WorldToViewportPoint(eye + (corners[i] - eye) * (depth / frameDistance), camPose);
    quad[i] = new UnityEngine.Vector2(v.x * img.width, v.y * img.height);
}
for (int i = 0; i < 4; i++)
{
    var a = quad[i]; var b = quad[(i + 1) % 4];
    int steps = (int)UnityEngine.Vector2.Distance(a, b) + 1;
    for (int k = 0; k <= steps; k++)
    {
        var p = UnityEngine.Vector2.Lerp(a, b, k / (float)steps);
        for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) img.SetPixel((int)p.x + dx, (int)p.y + dy, UnityEngine.Color.red);
    }
}
img.Apply();
System.IO.Directory.CreateDirectory("__OUT__/shots");
System.IO.File.WriteAllBytes("__OUT__/shots/camera_with_crop.png", img.EncodeToPNG());
var dir = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Photos");
var newest = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OrderByDescending(System.IO.Directory.GetFiles(dir, "*.png"), f => f));
return $"__OUT__/shots/camera_with_crop.png | newest photo: {newest} | eye {SpatialPhotos.DominantEye.Current}, depth {depth:F2} m, frame {frameDistance:F2} m, quad {string.Join(" ", quad)}";
