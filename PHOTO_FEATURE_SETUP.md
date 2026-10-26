# Photo capture + wrist gallery: editor setup

All scripts are in `Assets/Scripts/` and shaders are in `Assets/Shaders/`. The steps below create the scene objects and prefabs and connect them to the scripts. The `Photo` (6) and `CaptureIgnore` (7) layers have already been added to Tags & Layers.

## 1. Passthrough
1. Select `[BuildingBlock] Camera Rig` → **OVRManager**.
   - Under *Quest Features → General*, set **Passthrough Support** to *Required*.
   - Under *Insight Passthrough*, tick **Enable Passthrough**.
2. Add the **Passthrough** building block (Meta → Tools → Building Blocks). Alternatively, add `OVRPassthroughLayer` to the Camera Rig with Placement set to *Underlay*.
3. On `CenterEyeAnchor`'s Camera, set **Clear Flags** to *Solid Color* and **Background** to black with **alpha 0**.

## 2. Passthrough Camera Access
1. Add the **Passthrough Camera Access** building block. This adds a `PassthroughCameraAccess` component.
2. Set **Camera Position** to *Left* and **Requested Resolution** to 1280×960.
3. The `horizonos.permission.HEADSET_CAMERA` permission is already in `AndroidManifest.xml`, and `PhotoCapture` requests it at startup.

## 3. Hand interactions (grab + poke)
Add the **Hand Grab** and **Poke** building blocks. An alternative is to drag in `OVRInteractionComprehensive` and remove the controller parts. Either way, each hand ends up with HandGrab and Poke interactors.

## 4. Things that must not appear in photos
Set these objects (and their children) to the **CaptureIgnore** layer:
- the `BoundaryFrame` object
- the hand visuals, e.g. `OVRHandPrefab` / synthetic hand meshes

`WristGallery` sets its own hierarchy to this layer automatically.

## 5. Photo prefab → `Assets/Prefabs/Photo.prefab`
Build the prefab with this structure, everything on the **Photo** layer:

```
Photo (root)
  PhotoObject
  Rigidbody (Is Kinematic ✔, Use Gravity ✘)
  BoxCollider
  Grabbable (Kinematic While Selected ✘, Throw When Unselected ✘)
  HandGrabInteractable (Pointable Element = Grabbable, Rigidbody = Rigidbody)
  ├─ Visual: Quad (remove its MeshCollider). Material uses shader 3DUI/PhotoUnlit
  └─ Border (optional): a copy of the BoundaryFrame object with BoundaryFramePrefab
```

Then wire up the `PhotoObject` fields:

| Field | Value |
|---|---|
| Photo Renderer | Visual |
| Grab Collider | the BoxCollider |
| Body | the Rigidbody |
| Grabbable | the Grabbable |
| Border | Border (optional) |

## 6. Thumbnail prefab → `Assets/Prefabs/GalleryThumbnail.prefab`
This has the same structure and Rigidbody/Grabbable settings as the Photo prefab, with only these differences:
- Use `GalleryThumbnail` instead of `PhotoObject`.
- Leave out the Border.
- Put it on the **CaptureIgnore** layer.

Wire Thumbnail Renderer, Grab Collider and Grabbable in the same way as for the Photo prefab. Set **Thumbnail Height** to 0.045.

## 7. Scene objects
1. **PhotoSystem** (an empty object at the root) with these components:
   - **PhotoLibrary**
   - **AudioSource**: Audio Resource = a shutter sound clip (import a short .wav/.ogg, e.g. `Assets/Audio/Shutter.wav`), Play On Awake ✘, Loop ✘, Spatial Blend 0 (2D)
   - **PhotoCapture**, wired as follows:

| PhotoCapture field | Value |
|---|---|
| Passthrough Camera | the PassthroughCameraAccess component |
| Head Camera | CenterEyeAnchor |
| Capture Camera | a child Camera named "CaptureCamera". Disable the component, Target Eye = None |
| Photo Prefab | Photo |
| Library | PhotoLibrary |
| Shutter Sound | the AudioSource |
| Alpha Over Shader | `Hidden/3DUI/AlphaOver` |
| Capture Culling Mask | Everything (the Photo and CaptureIgnore layers are removed automatically) |

2. On the object that has **ImageBoundaryBox**, set **Photo Capture** to PhotoSystem.
3. **WristGallery**: see section 8.

## 8. WristGallery

The gallery floats a few centimetres **above the palm** (not the back of the wrist). It only appears when the palm faces your head, so on the back of the wrist it would be hidden behind your hand.

### Hierarchy
Build this under `[BuildingBlock] Camera Rig/TrackingSpace/LeftHandAnchor`:

```
LeftHandAnchor
└─ WristGallery        WristGallery component. Local pos 0,0,0 · rot 0,0,0 · scale 1
   └─ Visual           ← Visual Root.  Local pos (0, -0.07, 0) · rot (-90, 180, 0) · scale 1
      ├─ Strip         ← Strip Root.   Empty. Local pos/rot 0 · scale 1
      ├─ SwipeSurface  Poke surface behind the thumbnails (see below)
      ├─ LeftArrow     Poke button (see below)
      └─ RightArrow    Poke button (see below)
```

Rules the code depends on:
- **The `WristGallery` component must be on the outer object, not on Visual.** The script deactivates Visual while it's hidden; on Visual it would switch itself off and never come back.
- **Visual's scale must be 1.** The script animates Visual's scale between 0 and 1 and overwrites any value you set. To make the whole gallery bigger or smaller, scale `WristGallery` instead.
- **Everything visible or pokeable goes under Visual**, so it all hides together.
- **Strip stays empty.** At runtime the script creates `Visible Slots` children (`Slot 0…2`) along Strip's local X axis, centred on Strip, `Slot Spacing` apart. A thumbnail is instantiated in each slot.
- Layers are set to `CaptureIgnore` automatically for the whole hierarchy.

### Position and orientation
The values above assume the palm normal is the wrist's −Y axis (the `Palm Normal Local` default):
- Visual sits 7 cm out from the palm, along that normal.
- The rotation makes the thumbnails face back toward your eyes. Unity's Quad is seen correctly from its −Z side.
- The 180° around Y keeps the thumbnails upright, with their top toward your fingertips (the anchor's +Z), and LeftArrow on your left. With (-90, 0, 0) the whole strip appears upside down.

These were verified in the Meta XR Simulator: −Y is the palm normal (measured from the simulated hand's joints), and the strip reads upright and unmirrored with the palm toward the face. If it looks wrong on the headset, check it in play mode over Link:
1. Hold your left palm up in front of your face.
2. Select `LeftHandAnchor` in the Scene view with Local gizmos on, and see which axis points out of the palm.
3. If that isn't −Y, set **Palm Normal Local** to that axis (e.g. (0, 1, 0) or (±1, 0, 0)). Move Visual's position to about 0.07 × that axis, and rotate it so its blue (+Z) arrow points *into* the palm.
4. If the gallery appears when your palm faces *away* from you, flip the sign of Palm Normal Local.
5. If the thumbnails look mirrored, rotate Visual 180° around its local Y; if they look upside down, rotate it 180° around its local Z. The photo shader is double-sided, so a backwards strip is still visible, just mirrored.
6. Copy the final values out of play mode before stopping, because changes made in play mode are lost.

### SwipeSurface
This is an empty child of Visual at local pos (0, 0, 0.005), slightly behind the thumbnails.
1. Right-click it → **Interaction SDK → Add Poke Interaction** (the Quick Action). This adds a `PokeInteractable` plus a plane surface.
2. Size the surface's bounds to cover the strip: about **0.215 × 0.06 m**. That's `Visible Slots × Slot Spacing` plus a margin.
3. Make sure the surface faces the same way as the thumbnails, with its normal pointing toward the viewer.

Swiping left or right with your right index finger scrolls one photo per `Slot Spacing` of movement. Dragging left shows older photos.

### LeftArrow / RightArrow
1. Create each one as a child of Visual. Put them at local x = **∓0.128**, just outside the strip; y and z are 0.
2. Give each a small visual, e.g. a 0.025 m cube or arrow sprite. Remove its collider; poking uses the surface, not the collider.
3. On each, run **Interaction SDK → Add Poke Interaction**, with a surface about 0.03 × 0.03 m.

The arrows are optional; leaving the fields empty is fine.

### WristGallery fields

| Field | Value |
|---|---|
| Left Hand | `[BuildingBlock] Hand Tracking left` (its OVRHand) |
| Wrist | LeftHandAnchor |
| Head | CenterEyeAnchor |
| Visual Root | Visual |
| Strip Root | Strip |
| Library | PhotoSystem (PhotoLibrary) |
| Thumbnail Prefab | GalleryThumbnail prefab |
| Photo Prefab | Photo prefab |
| Swipe Surface | SwipeSurface's PokeInteractable |
| Left Arrow / Right Arrow | each arrow's PokeInteractable |
| Visible Slots | 3. With the arrows the gallery is about 28 cm wide, ~40° of view at arm's length. Five slots made it ~49 cm, too wide to see at once |
| Slot Spacing | 0.065 |
| Thumbnail Gap | 0.01. Wide photos are shrunk to at most `Slot Spacing − Thumbnail Gap` wide, so they never overlap the next slot |
| Palm Normal Local | (0, −1, 0), or whatever you found above |
| Show / Hide Threshold | 0.7 / 0.5. Raise both if it pops up too easily |

### Requirements from earlier steps
- **Poking (swipe/arrows)** needs a `PokeInteractor` on the **right** hand; grabbing thumbnails needs a `HandGrabInteractor` (both from step 3).
- The gallery hides when the left hand isn't tracked or while you are pinching a capture frame. It stays visible while you hold a thumbnail.

## 9. Build
In Build Settings, choose Android → Build And Run on the Quest 3. Accept the camera permission prompt on first launch.
