# Spatial Photos

Pinch with both hands and pull them apart to draw a frame. Release to take a photo of what's inside, both the real room (passthrough camera) and virtual content. Photos float where they were taken; grab them, move them, or throw them away. Grab one with both hands and move your hands apart or together to resize it. Open your left palm facing up and a gallery of saved photos floats above your hand, turned toward you; pinch a thumbnail and pull it away from the strip to take the photo out, or move the pinch along the strip to scroll (and then away to take out the photo under your fingers). Poke the × in a thumbnail's corner to delete it: a popup above the gallery asks you to confirm or cancel.

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
   - **Passthrough Camera Access**: Camera Position *Left*, about 1280×960
   - **Hand Grab** and **Poke** interactions, or `OVRInteractionComprehensive` with the controller parts removed
3. **Put the hand visuals on `CaptureIgnore`** so your hands don't show up in photos. Usually this means the hand tracking objects and the interaction rig's hand visuals.
4. **Drag in the prefabs** from `Prefabs/`, anywhere in the hierarchy:
   - `PhotoCaptureSystem`: the pinch frame, capture and photo library
   - `WristGallery` (optional): floats above the left hand anchor at runtime
5. **Android permission:** add `horizonos.permission.HEADSET_CAMERA` to `AndroidManifest.xml`. `PhotoCapture` requests it at startup.

That's all the wiring needed. Scene references left empty in the Inspector are found at startup: hands, head, wrist, passthrough camera and photo library. They are looked up through the scene's `OVRCameraRig`, and a warning is logged for anything missing. Assign a field yourself to override the lookup.

## Folder layout

| Folder | Contents |
|---|---|
| `Prefabs/` | `PhotoCaptureSystem`, `WristGallery`, and `Photo`/`GalleryThumbnail`, which the scripts spawn |
| `Scripts/Input` | `HandPinchManager`: two-hand proximity pinch events |
| `Scripts/Frame` | `ImageBoundaryBox` (pinch → frame → capture), `CaptureFrameVisual`, `FrameOutline` (the shared outline look), `FrameResizeAudio` |
| `Scripts/Capture` | `PhotoCapture`: passthrough image + virtual layer → cropped photo |
| `Scripts/Photos` | `PhotoLibrary` (save and load PNGs), `PhotoObject` (floating photo), `PhotoResizeTransformer` (two-hand move and resize) |
| `Scripts/Gallery` | `WristGallery`, `GalleryThumbnail` |
| `Scripts/Common` | `XRRig` (finds the rig's head and hands), `CaptureLayers`, `PokeButton` (poke button built from code) |
| `Editor/` | the Add Capture Layers menu |
| `Shaders/`, `Materials/`, `Meshes/`, `Audio/`, `Resources/` | assets used by the above; `Resources/CaptureFrame.mat` is loaded by `FrameOutline` |

All scripts are in the `SpatialPhotos` namespace and the `SpatialPhotos` assembly.

## Tuning

- **Frame look:** `PhotoCaptureSystem/CaptureFrame` → `CaptureFrameVisual`. Style (Viewfinder, Glow, Marching Ants, Glass), color, line thickness.
- **Pinch:** `PinchInput` → `HandPinchManager`. Proximity Threshold is how close the two pinches must start. `ImageBoundaryBox` → Min Capture Size.
- **Resize sound:** add a `FrameResizeAudio` to `CaptureFrame` to change pitch range, tick spacing and volumes, or to assign your own clips.
- **Photo resize:** `Photo` prefab → `PhotoObject` → Min Height and Max Height limit how small or large a two-hand grab can make a photo.
- **Gallery:** `WristGallery` → Visible Slots, Slot Spacing, show/hide thresholds, Scroll Smoothing (how fast the strip settles after a swipe or arrow press).
  - **Pinch vs. poke:** each hand hovers one Interaction SDK interactor at a time and poke goes first, so the strip's swipe surface and the small buttons only react within 2 cm (`WristGallery` → Swipe Hover Distance; `PokeButton.Create`'s hover distance). Further out, the pinch can grab a thumbnail. And as soon as a hand near the strip starts to pinch (Pinch Takeover Strength 0.3, within Pinch Takeover Reach), the gallery switches the swipe surface and × buttons off, cancelling a swipe in progress, so the pinch can grab even while the finger is touching the strip. They come back once the pinch and any grab end.
  - **Pinch drag:** `GalleryThumbnail` → Decide Distance (1.5 cm): after that, a pinch moving mostly along the strip scrolls it and one moving away from it (toward you or up) pulls the photo, which comes out at Pull Out Distance (10 cm) and grows by Pull Growth on the way. A scrolling pinch that moves Scroll To Pull Distance (4 cm) away from the strip stops scrolling and pulls the photo under the fingers.
  - **Deleting:** `WristGallery` → Confirm Button Size, Confirm Gap and Label Height for the popup; `GalleryThumbnail` → Delete Button Size and Inset for the corner ×. Deleting removes the PNG and JSON; photos already floating in the room stay until thrown away.
  - **Placement:** the gallery floats above the tracked palm joint of the left Interaction SDK hand (found automatically, or set Palm Hand), Height Above Palm (12 cm by default) to the middle of the strip, with Follow Smoothing. Palm Center Local is only a fallback while there is no hand joint data. The gallery turns to face your eyes and stays upright however the hand is turned.
  - **Showing:** the gallery appears when the palm faces up (palm normal · world up above Show Threshold 0.7) and hides below Hide Threshold 0.5.
  - **Palm Normal Local:** the default (0, −1, 0) matches Meta's hand anchors; change it if the gallery shows with the palm facing down.
  - **Visual:** scale `WristGallery`, not its child `Visual`, because `Visual`'s scale is animated.

Photos are saved as PNG + JSON in `Application.persistentDataPath/Photos`.
