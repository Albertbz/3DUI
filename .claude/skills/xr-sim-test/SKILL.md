---
name: xr-sim-test
description: Test this project's photo capture and wrist gallery without a headset, by running Play mode in the Meta XR Simulator and scripting hand poses, pinches, grabs and pokes from the Unity CLI. Use when asked to test, verify or check the wrist gallery, photo capture, pinch frame, grabbing or poking in the simulator, or after changing WristGallery, GalleryThumbnail, PhotoObject, PhotoCapture, PhotoLibrary, HandPinchManager or ImageBoundaryBox.
---

# Testing the photo feature in the Meta XR Simulator

This tests the photo capture and wrist gallery end to end on a Mac with no headset:
- Unity Play mode runs against the **Meta XR Simulator**, so hand and head tracking and a synthetic passthrough camera are live.
- Hand poses, pinches, grabs and pokes are **scripted from inside Unity** through the `unity` CLI. The simulator's CLI can't pose hands or pinch, and its keyboard and mouse controls need a person.
- Everything after the injected input is the app's real code. That includes `HandPinchManager`, `WristGallery`, the Interaction SDK `Grabbable` and `PokeInteractable`, `PhotoCapture` and `PhotoLibrary`.

All scripts are in `scripts/` next to this file; the paths below are relative to that folder. Output (renders, video, logs) goes to `$XRSIM_OUT`, which defaults to `${TMPDIR:-/tmp}/xrsim-test`. Point it at your scratchpad when you have one.

## Prerequisites

- **Unity Editor open on this project, with the bridge connected.** `unity status` shows `ready`, and `com.unity.pipeline` is in `Packages/manifest.json`. If it isn't connected, use the `unity-cli` skill.
- **No compile errors.** With compile errors the Editor is in Safe Mode and the bridge can't connect.
- **Meta XR Simulator** installed at `/Applications/MetaXRSimulator.app`.
- **`metavr` CLI.** `scripts/metavr.sh` finds it on `PATH` or where the meta-vr plugin installs it.
- **Standalone XR loader is OpenXR.** It is in this project.
- `ffmpeg` is optional; it's only needed for the MP4.

## Workflow

Run the steps in order and compare each result line with the expected values in its output, which are written as `(expect …)`.

```bash
cd .claude/skills/xr-sim-test/scripts
export XRSIM_OUT=<scratchpad>/xrsim        # optional
./photos.sh baseline                        # remember existing photos so cleanup only removes test ones
./connect.sh                                # simulator up, Editor pointed at it, Play mode, runtime connected
./ev.sh hook.cs                             # left-hand pose override (needed for pose.cs and video.cs)
./ev.sh palm_axis.cs                        # measured palm normal must agree with Palm Normal Local (> 0.9)
./ev.sh seed.cs 6                           # 6 photos so the gallery can scroll
```

**1. Gallery visibility.** Run these one at a time with ~1.5 s between them; the gallery animates.

```bash
./ev.sh pose.cs off;  sleep 1.5; ./ev.sh state.cs default        # real resting pose, palm down: hidden
./ev.sh pose.cs face; sleep 1.5; ./ev.sh state.cs palm_to_face   # visible, facesViewer≈1, upright>0.9, notMirrored>0.9
./ev.sh pose.cs tray; sleep 1.5; ./ev.sh state.cs                # palm up below the head: visible
for t in 53 66 53 40; do ./ev.sh pose.cs face $t; sleep 1; ./ev.sh state.cs | head -1; done
#   palm·toHead 0.60 -> still visible, 0.41 -> hidden, 0.60 -> still hidden, 0.77 -> visible (hysteresis 0.7 / 0.5)
M=$(./metavr.sh)
$M xrsim input source --hand left --source none; sleep 1.5; ./ev.sh state.cs | head -1   # real tracking loss: hidden
$M xrsim input source --hand left --source hand; sleep 2;   ./ev.sh state.cs | head -1   # back: visible
```

Look at `shots/palm_to_face.png`. The thumbnails should be upright and evenly spaced, with ← on the left and → on the right. The arrow-to-arrow `width` angle should stay below ~50°.

**2. Pinch capture.** Then compare the camera image with the saved photo.

```bash
./capture.sh                 # frame opens and hides the gallery, release captures, holding a photo blocks the pinch
./ev.sh crop_check.cs        # camera image with the frame's outline + path of the newest saved photo
```

Read `shots/camera_with_crop.png` and the newest photo; both should show the same region. `ffmpeg … hstack` gives one image to look at side by side.

**2b. Frame look and resize sound.**

```bash
./frame_check.sh             # renders every frame style + too-small state, then a grow/hold/shrink sweep
```

View `shots/frame_Viewfinder.png`, `frame_Glow.png`, `frame_MarchingAnts.png`, `frame_Glass.png` and `frame_too_small.png` (red tint). The sweep samples the shader's `_Valid`/`_Activity` and `FrameResizeAudio`'s hum pitch, volume and ticks. It can't tell you how the sound actually sounds. A frame hitch can merge ticks, since only one tick plays per frame. `frame_style.cs <Style>` switches the style during Play mode.

**3. Grabbing a photo.** This needs a photo in the scene; `capture.sh` leaves one.

```bash
./ev.sh grab.cs && ./wait_log.sh      # slow drag floats where released; fast throw falls, fades, is destroyed
```

**4. Gallery interactions.**

```bash
./ev.sh pose.cs face; sleep 1.5
./ev.sh gallery_interact.cs && ./wait_log.sh   # layout, short/long thumbnail pull, swipe, arrows
```

**5. Video (optional).**

```bash
./video.sh      # ~1-2 min; writes gallery.mp4 and gallery_strip.png (5 key frames; view the PNG)
```

**6. Clean up.** Always do this.

```bash
./ev.sh pose.cs off
~/.unity/bin/unity command editor.playmode stop --no-banner
./photos.sh cleanup
```

The Editor keeps using the simulator for Play mode until Unity restarts.

## Reporting

Give the user a table of every check with ✅/❌. Name the scripted parts honestly: hand poses and pinches were set from code, and grabs and pokes were sent as Interaction SDK pointer events. Send the key renders: the palm_to_face shot, frame_drawn, the crop comparison and the video strip.

Always list what this can't cover:
- real finger-pose detection (pinch and grab quality)
- whether a real fingertip can hit the small poke targets
- alignment with the real cameras
- performance on the device

## Fixing failures

- **Change values in edit mode, not during Play mode.** Play-mode changes are lost. Stop Play mode first, then change the scene or prefab through the bridge with `Undo.RecordObject` + `EditorSceneManager.SaveScene`, or `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset` for prefabs.
- **After a code change**, run `unity command script recompile`, wait until `recompile_status` is `completed`, check `console status` for `compilationFailed: false`, then run `connect.sh` again and repeat the failed steps.
- **Keep the setup guide in sync.** Update `PHOTO_FEATURE_SETUP.md` when gallery layout values change.

## Gotchas (all hit in practice)

- **`metavr xrsim runtime activate` needs sudo** and fails non-interactively. `connect.sh` avoids it by setting `XR_RUNTIME_JSON` inside the Editor process.
- **`metavr xrsim record` isn't supported on macOS** ("does not support native MP4 recording"). Use `state.cs <name>` and `video.sh`, which render the head view from a temporary camera. Those renders contain virtual content only. The synthetic room is only in the passthrough camera image, which `crop_check.cs` saves.
- **Send `Hover` before `Select` (and `Unhover` after `Unselect`)** when driving a `Grabbable` or `PokeInteractable` by hand. Without the hover the Grabbable ignores `Move` and nothing moves, which looks like a bug in the app.
- **`script eval` runs as a method body.** There are no `using` lines, so fully qualify types (`UnityEngine.Vector3`, `System.Linq.Enumerable.Select(...)`); local functions and iterators work. There's no waiting inside one eval: anything spread over frames must be a coroutine started on an existing MonoBehaviour, such as `PhotoLibrary`. Pass results back through `AppDomain.CurrentDomain.SetData`.
- **`--format json` output can hold two JSON documents.** Parse it with `raw_decode`, as `ev.sh` does.
- **Rendering every frame with `ReadPixels` makes Play mode run at a few fps.** That's expected; wait it out instead of assuming a hang (`Time.frameCount` keeps rising).
- **Freezing an `OVRHand`** (`hand.cs … freeze`) stops its tracking updates until `hand.cs … live`. `capture.sh` turns them back on at the end.
- **The hand anchor's −Y is the palm normal and +Z points along the fingers**, measured from the simulator's hand joints. `pose.cs` relies on this. If `palm_axis.cs` disagrees, the gallery's Palm Normal Local is wrong.
- **macOS simulator support is partial.** If no hands are tracked, check `metavr xrsim runtime status` (it should show `L=hand R=hand`) and set `metavr xrsim input source --hand left --source hand`.
