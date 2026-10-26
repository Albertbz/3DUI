# Spatial Photos

Pinch with both hands and pull them apart to draw a frame. Release to take a photo of what's inside, both the real room (passthrough camera) and virtual content. Photos float where they were taken; grab them, move them, or throw them away. A gallery on your left wrist shows saved photos when you turn your palm toward you, and you can pull a photo out of it.

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
   - `WristGallery` (optional): attaches itself to the left hand anchor at startup
5. **Android permission:** add `horizonos.permission.HEADSET_CAMERA` to `AndroidManifest.xml`. `PhotoCapture` requests it at startup.

That's all the wiring needed. Scene references left empty in the Inspector are found at startup: hands, head, wrist, passthrough camera and photo library. They are looked up through the scene's `OVRCameraRig`, and a warning is logged for anything missing. Assign a field yourself to override the lookup.

## Folder layout

| Folder | Contents |
|---|---|
| `Prefabs/` | `PhotoCaptureSystem`, `WristGallery`, and `Photo`/`GalleryThumbnail`, which the scripts spawn |
| `Scripts/Input` | `HandPinchManager`: two-hand proximity pinch events |
| `Scripts/Frame` | `ImageBoundaryBox` (pinch → frame → capture), `CaptureFrameVisual`, `FrameOutline` (the shared outline look), `FrameResizeAudio` |
| `Scripts/Capture` | `PhotoCapture`: passthrough image + virtual layer → cropped photo |
| `Scripts/Photos` | `PhotoLibrary` (save and load PNGs), `PhotoObject` (floating photo) |
| `Scripts/Gallery` | `WristGallery`, `GalleryThumbnail` |
| `Scripts/Common` | `XRRig` (finds the rig's head and hands), `CaptureLayers` |
| `Editor/` | the Add Capture Layers menu |
| `Shaders/`, `Materials/`, `Meshes/`, `Audio/`, `Resources/` | assets used by the above; `Resources/CaptureFrame.mat` is loaded by `FrameOutline` |

All scripts are in the `SpatialPhotos` namespace and the `SpatialPhotos` assembly.

## Tuning

- **Frame look:** `PhotoCaptureSystem/CaptureFrame` → `CaptureFrameVisual`. Style (Viewfinder, Glow, Marching Ants, Glass), color, line thickness.
- **Pinch:** `PinchInput` → `HandPinchManager`. Proximity Threshold is how close the two pinches must start. `ImageBoundaryBox` → Min Capture Size.
- **Resize sound:** add a `FrameResizeAudio` to `CaptureFrame` to change pitch range, tick spacing and volumes, or to assign your own clips.
- **Gallery:** `WristGallery` → Visible Slots, Slot Spacing, show/hide thresholds.
  - **Palm Normal Local:** the default (0, −1, 0) matches Meta's hand anchors; change it if the gallery shows on the wrong side of the hand.
  - **Visual:** the child `Visual` holds the gallery's offset from the palm. Scale `WristGallery`, not `Visual`, because `Visual`'s scale is animated.

Photos are saved as PNG + JSON in `Application.persistentDataPath/Photos`.
