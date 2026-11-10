# Spatial Photos

Pinch with both hands and pull them apart to draw a frame. Release to take a photo of what's inside, both the real room (passthrough camera) and virtual content, as your dominant eye sees it through the frame. Photos float where they were taken; grab them, move them, or throw them away. Grab one with both hands and move your hands apart or together to resize it. Let go of a photo near a wall, table or other flat surface and it lies flat on it; while you hold it there, a dashed outline shows where it will land. Open your left palm facing up and a gallery of saved photos floats beside it, to the right of your hand and turned toward you, so the hand doesn't cover it; pinch a thumbnail and pull it away from the strip to take the photo out, or move the pinch along the strip to scroll (and then away to take out the photo under your fingers). Poke the × in a thumbnail's corner to delete it: a popup above the gallery asks you to confirm or cancel.

On first launch a dot appears a few meters away: keep both eyes open and pinch a frame around it three times. People center a frame for their dominant eye, so this finds which eye that is. It is saved, and the **Eye** button under the gallery runs the test again.

Everything lives in this folder. To use it in another project, copy the `SpatialPhotos` folder into `Assets/` and follow the setup below.

## Requirements

- Unity 6 with the built-in render pipeline (the shaders are written for it)
- Meta XR packages: **Core SDK** (`OVRCameraRig`, `OVRHand`), **Interaction SDK** (grab and poke) and **MR Utility Kit** (`PassthroughCameraAccess`). `com.meta.xr.sdk.all` covers all of them.
- A Quest 3 or 3S for the passthrough camera. Without it, photos contain virtual content only.

## Setup in a new scene

1. **Add the capture layers.** Run **Tools → Spatial Photos → Add Capture Layers**. This adds `Photo` and `CaptureIgnore` to Tags & Layers. Objects are moved onto them by name at runtime, so the layer numbers don't matter.
2. **Meta building blocks** (Meta → Tools → Building Blocks):
   - **Camera Rig**
   - **Hand Tracking**: provides the `OVRHand` on each hand anchor
   - **Passthrough**: on the center eye camera, set Clear Flags to Solid Color with a black background at alpha 0
   - **Passthrough Camera Access**: about 1280×960. Camera Position is switched at runtime to the dominant eye's side
   - **Hand Grab** and **Poke** interactions, or `OVRInteractionComprehensive` with the controller parts removed
   - **MR Utility Kit** (optional): an `MRUK` object lets photos snap to the scanned room's walls, tables and floor. Untick its Scene Settings → Load Scene On Startup: `SurfaceSnap` loads the room itself without sending people without a scan to Space Setup. Without MRUK or a scan, photos snap to surfaces found by environment depth instead
3. **Put the hand visuals on `CaptureIgnore`** so your hands don't show up in photos. Usually this means the hand tracking objects and the interaction rig's hand visuals.
4. **Drag in the prefabs** from `Prefabs/`, anywhere in the hierarchy:
   - `PhotoCaptureSystem`: the pinch frame, capture and photo library
   - `WristGallery` (optional): floats above the left hand anchor at runtime
5. **Android permissions:** add `horizonos.permission.HEADSET_CAMERA` and `com.oculus.permission.USE_SCENE` (environment depth) to `AndroidManifest.xml`. `PhotoCapture` requests them at startup.

That's all the wiring needed. Scene references left empty in the Inspector are found at startup: hands, head, wrist, passthrough camera and photo library. They are looked up through the scene's `OVRCameraRig`, and a warning is logged for anything missing. Assign a field yourself to override the lookup.

## Folder layout

| Folder | Contents |
|---|---|
| `Prefabs/` | `PhotoCaptureSystem`, `WristGallery`, and `Photo`/`GalleryThumbnail`, which the scripts spawn |
| `Scripts/Input` | `HandPinchManager`: two-hand proximity pinch events |
| `Scripts/Frame` | `ImageBoundaryBox` (pinch → frame → capture), `CaptureFrameVisual`, `FrameOutline` (the shared outline look), `FrameResizeAudio` |
| `Scripts/Capture` | `PhotoCapture`: passthrough image reprojected to the dominant eye + virtual layer → photo |
| `Scripts/Calibration` | `DominantEyeCalibration`: the sighting test that finds the dominant eye |
| `Scripts/Photos` | `PhotoLibrary` (save and load PNGs), `PhotoObject` (floating photo), `PhotoResizeTransformer` (two-hand move and resize), `SurfaceSnap` (finds a wall or table near a photo) |
| `Scripts/Gallery` | `WristGallery`, `GalleryThumbnail` |
| `Scripts/Common` | `XRRig` (finds the rig's head, eyes and hands), `DominantEye` (the saved eye), `CaptureLayers`, `PokeButton` (poke button built from code) |
| `Editor/` | the Add Capture Layers menu |
| `Shaders/`, `Materials/`, `Meshes/`, `Audio/`, `Resources/` | assets used by the above; `Resources/CaptureFrame.mat` is loaded by `FrameOutline` |

All scripts are in the `SpatialPhotos` namespace and the `SpatialPhotos` assembly.

## Tuning

- **Frame look:** `PhotoCaptureSystem/CaptureFrame` → `CaptureFrameVisual`. Style (Viewfinder, Glow, Marching Ants, Glass), color, line thickness.
- **Dominant eye:** the frame faces the dominant eye, and the photo is what that eye sees through it. The real world is taken as a plane at the depth found by MRUK's environment raycast through the frame's center (`PhotoCapture` adds an `EnvironmentRaycastManager` when the device supports it), or `PhotoCapture` → Fallback Depth (2 m) without one. Things much nearer or farther than that plane shift slightly. Calibration: `DominantEyeCalibration`, added to `PhotoCaptureSystem` at runtime when missing. It has Target Distance, Max Angle (3°, how far off the dot the frame may be) and Trials (3).
- **Pinch:** `PinchInput` → `HandPinchManager`. Proximity Threshold is how close the two pinches must start. `ImageBoundaryBox` → Min Capture Size.
- **Release timing:** a pinch release is only detected once the fingers have opened, often with the hands already moving, so the photo uses the frame and camera image from `ImageBoundaryBox` → Release Lookback (0.15 s) before the release. That keeps the opening hands out of the picture. While the frame is up, `PhotoCapture` keeps up to Max Snapshots (10) camera images from the last Snapshot Seconds (0.4 s), about 5 MB each at 1280×960, and frees them on release.
- **Resize sound:** add a `FrameResizeAudio` to `CaptureFrame` to change pitch range, tick spacing and volumes, or to assign your own clips.
- **Photo resize:** `Photo` prefab → `PhotoObject` → Min Height and Max Height limit how small or large a two-hand grab can make a photo.
- **Snap to surfaces:** `Photo` prefab → `PhotoObject` → Snap Distance (12 cm from the photo's centre to the surface), Max Snap Angle (60°, how far the photo may be turned away from lying flat on it, so one held sideways next to a wall stays put), Snap Gap (4 mm in front of the surface) and Snap Duration. Photos on a wall hang straight; on a table or floor they keep the turn they were held at. `SurfaceSnap` uses the room scan when MRUK has loaded one, otherwise an environment depth raycast behind and below the photo.
- **Gallery:** `WristGallery` → Visible Slots, Slot Spacing, show/hide thresholds, Scroll Smoothing (how fast the strip settles after a swipe or arrow press).
  - **Pinch vs. poke:** each hand hovers one Interaction SDK interactor at a time and poke goes first, so the strip's swipe surface and the small buttons only react within 2 cm (`WristGallery` → Swipe Hover Distance; `PokeButton.Create`'s hover distance). Further out, the pinch can grab a thumbnail. And as soon as a hand near the strip starts to pinch (Pinch Takeover Strength 0.3, within Pinch Takeover Reach), the gallery switches the swipe surface and × buttons off, cancelling a swipe in progress, so the pinch can grab even while the finger is touching the strip. They come back once the pinch and any grab end.
  - **Pinch drag:** `GalleryThumbnail` → Decide Distance (1.5 cm): after that, a pinch moving mostly along the strip scrolls it and one moving away from it (toward you or up) pulls the photo, which comes out at Pull Out Distance (10 cm) and grows by Pull Growth on the way. A scrolling pinch that moves Scroll To Pull Distance (4 cm) away from the strip stops scrolling and pulls the photo under the fingers.
  - **Deleting:** `WristGallery` → Confirm Button Size, Confirm Gap and Label Height for the popup; `GalleryThumbnail` → Delete Button Size and Inset for the corner ×. Deleting removes the PNG and JSON; photos already floating in the room stay until thrown away.
  - **Placement:** the gallery floats above the tracked palm joint of the left Interaction SDK hand (found automatically, or set Palm Hand), Height Above Palm (12 cm by default) to the middle of the strip, Gap Right Of Palm (4 cm from the palm centre to the panel's left edge, toward the viewer's right), with Follow Smoothing. Palm Center Local is only a fallback while there is no hand joint data. The gallery turns to face your eyes and stays upright however the hand is turned.
  - **Showing:** the gallery appears when the palm faces up (palm normal · world up above Show Threshold 0.7) and hides below Hide Threshold 0.5.
  - **Palm Normal Local:** the default (0, −1, 0) matches Meta's hand anchors; change it if the gallery shows with the palm facing down.
  - **Visual:** scale `WristGallery`, not its child `Visual`, because `Visual`'s scale is animated.

Photos are saved as PNG + JSON in `Application.persistentDataPath/Photos`.
