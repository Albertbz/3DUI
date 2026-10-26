// frame_style.cs <Viewfinder|Glow|MarchingAnts|Glass> : switches the capture frame's style in Play mode
var frame = UnityEngine.Object.FindFirstObjectByType<SpatialPhotos.CaptureFrameVisual>(UnityEngine.FindObjectsInactive.Include);
var field = typeof(SpatialPhotos.CaptureFrameVisual).GetField("style", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
field.SetValue(frame, System.Enum.Parse(typeof(SpatialPhotos.FrameOutline.FrameStyle), "__ARG1__"));
return $"style={field.GetValue(frame)}";
