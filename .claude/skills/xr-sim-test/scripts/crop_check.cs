// Saves the current passthrough camera image with the last capture frame's projected outline in red
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
var corners = (UnityEngine.Vector3[])typeof(SpatialPhotos.ImageBoundaryBox).GetField("lastCorners", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(box);
var camPose = pca.GetCameraPose();
UnityEngine.Vector2 min = UnityEngine.Vector2.positiveInfinity, max = UnityEngine.Vector2.negativeInfinity;
foreach (var c in corners) { var v = pca.WorldToViewportPoint(c, camPose); min = UnityEngine.Vector2.Min(min, v); max = UnityEngine.Vector2.Max(max, v); }
int x0 = (int)(min.x * img.width), x1 = (int)(max.x * img.width), y0 = (int)(min.y * img.height), y1 = (int)(max.y * img.height);
for (int t = -2; t <= 2; t++)
{
    for (int x = x0; x <= x1; x++) { img.SetPixel(x, y0 + t, UnityEngine.Color.red); img.SetPixel(x, y1 + t, UnityEngine.Color.red); }
    for (int y = y0; y <= y1; y++) { img.SetPixel(x0 + t, y, UnityEngine.Color.red); img.SetPixel(x1 + t, y, UnityEngine.Color.red); }
}
img.Apply();
System.IO.Directory.CreateDirectory("__OUT__/shots");
System.IO.File.WriteAllBytes("__OUT__/shots/camera_with_crop.png", img.EncodeToPNG());
var dir = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Photos");
var newest = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OrderByDescending(System.IO.Directory.GetFiles(dir, "*.png"), f => f));
return $"__OUT__/shots/camera_with_crop.png | newest photo: {newest} | crop viewport {min:F2}..{max:F2}";
