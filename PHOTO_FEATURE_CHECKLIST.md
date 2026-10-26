# Photo capture + wrist gallery: implementation checklist

The full plan is in `~/.claude/plans/okay-so-i-would-clever-perlis.md`. Work through these items in order. Tick an item only once its **Goal** has been verifiably met.

- [x] **1. Confirm SDK APIs**
  - **Goal:** confirm the exact names of the APIs the scripts rely on, read from the package sources in `Library/PackageCache`.
  - **Result:** confirmed in the package sources:
    - `Oculus.Interaction.Grabbable` exposes `WhenPointerEventRaised` with `PointerEventType.Select`/`Unselect`.
    - `Meta.XR.PassthroughCameraAccess` provides `IsPlaying`, `GetTexture()`, `GetCameraPose()`, `WorldToViewportPoint()` and `Intrinsics`. Its texture lags one frame.
    - The project uses the built-in render pipeline (no URP in the manifest).

- [x] **2. Shaders** (`Assets/Shaders/AlphaOver.shader`, `Assets/Shaders/PhotoUnlit.shader`)
  - **Goal:** both files exist. AlphaOver composites `_OverlayTex` over `_MainTex` using the overlay's alpha. PhotoUnlit draws `_MainTex` double-sided with `_FlashAmount` (0–1, lerps to white) and `_Alpha`. Neither shows errors in the Unity console.

- [x] **3. `PhotoLibrary.cs`**
  - **Goal:**
    - `Add(RenderTexture, w, h)` writes `photo_<timestamp>.png` and a matching `.json` (`widthM`, `heightM`) to `persistentDataPath/Photos` without blocking the main thread.
    - On startup it loads existing PNGs, newest first, with a fallback size when the JSON is missing.
    - It creates a thumbnail for each entry and raises `OnPhotoAdded` (for new captures) and `OnLibraryLoaded`.
    - `LoadFullTexture(entry)` returns the full-resolution texture.

- [x] **4. `PhotoObject.cs`**
  - **Goal:**
    - `Init(entry, texture)` sizes the quad to `widthM × heightM` and shows the texture with the flash/fade-in.
    - Grabbing sets `IsHeld`, and the static `AnyHeld` is true while any photo is held.
    - Releasing faster than `throwDeleteSpeed` makes the photo fall, fade and be destroyed. A slower release leaves it floating, kinematic, where it was let go.
    - Destroying a photo never touches the disk or the library.

- [x] **5. `PhotoCapture.cs`**
  - **Goal:**
    - `Capture(corners, pose, w, h)` renders the virtual scene from the passthrough camera's pose using its intrinsics. The culling mask excludes the `Photo` and `CaptureIgnore` layers.
    - It composites that over the passthrough texture and crops to the bounding box of the projected corners.
    - It plays the shutter sound, spawns a `PhotoObject` at the frame pose, and calls `PhotoLibrary.Add`.
    - If the passthrough camera isn't playing, it falls back to a virtual-only capture from the head camera and logs a warning.

- [x] **6. `GalleryThumbnail.cs`**
  - **Goal:**
    - Grabbing a thumbnail and pulling it more than 10 cm from its slot spawns a full-size `PhotoObject`. The photo grows to full size and follows the grab until release, and then the thumbnail returns to its slot.
    - Releasing before 10 cm springs the thumbnail back into its slot.

- [x] **7. `WristGallery.cs`**
  - **Goal:**
    - The gallery is attached to the left wrist and shows when the palm faces the head (dot product > 0.7) and hides below 0.5. It also hides during a proximity pinch or when tracking is lost, and animates its scale in and out.
    - It shows a pooled strip of 5 visible thumbnails from `PhotoLibrary`, scrolled with poke arrows and by swiping with the right index finger.
    - A new capture's thumbnail appears at the front.

- [x] **8. Hook up `ImageBoundaryBox.cs`**
  - **Goal:** it stores the last corners, pose and size. On release it hides the frame and calls `PhotoCapture.Capture`, but skips the capture if either side is under 5 cm.

- [x] **9. Update `HandPinchManager.cs`**
  - **Goal:**
    - No proximity pinch starts while `PhotoObject.AnyHeld` or `GalleryThumbnail.AnyHeld` is true.
    - The per-frame update is null-safe for the hand references.
    - It exposes `IsProximityPinchActive` for the gallery.

- [x] **10. Android manifest**
  - **Goal:** `Assets/Plugins/Android/AndroidManifest.xml` declares `horizonos.permission.HEADSET_CAMERA`, and the app requests it at runtime (the PCA component or our code does this).

- [x] **11. Compile check**
  - **Goal:** the project compiles with zero errors, verified with a Unity batchmode compile or the editor console.

- [x] **12. Scene/editor setup guide** (`PHOTO_FEATURE_SETUP.md`)
  - **Goal:** a step-by-step guide that someone can follow in the editor without guessing. It covers:
    - enabling passthrough and the passthrough layer, with a transparent camera clear
    - the Passthrough Camera Access block
    - the Hand Grab / interaction rig
    - the `Photo` and `CaptureIgnore` layers
    - the Photo and Thumbnail prefabs
    - the capture camera, the gallery object, and every serialized field to wire up

- [ ] **13. On-device verification (Quest 3)**
  - **Goal:** every item in the plan's Verification section passes on the headset. The user runs this; I report what is still untested.
