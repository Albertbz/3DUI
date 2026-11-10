# Testing the photo feature without a headset

The full procedure, with expected values for every check, is in
`.claude/skills/xr-sim-test/SKILL.md`. This page is the short version.

Play mode runs in the **Meta XR Simulator** on the Mac. Hand poses, pinches, grabs and pokes are sent from code through the Unity CLI, so the app's own scripts do the rest.

## Before you start

- Open this project in the Unity Editor (6000.3.26f1). `unity status` should show `ready`.
- Fix any compile errors first. With errors the Editor can't connect to the bridge.
- The Meta XR Simulator must be installed at `/Applications/MetaXRSimulator.app`.
- The `metavr` CLI must be on `PATH`, or installed by the meta-vr plugin.
- Standalone XR loader: OpenXR.

## Run the tests

From `.claude/skills/xr-sim-test/scripts`:

```bash
export XRSIM_OUT=<somewhere writable>   # renders and logs go here
./photos.sh baseline                     # remember existing photos
./connect.sh                             # start simulator, enter Play mode
./ev.sh hook.cs && ./ev.sh palm_axis.cs && ./ev.sh seed.cs 6
```

Then run the checks in order:

1. **Gallery visibility** (palm down hidden, palm to face shown, hysteresis, tracking loss)
2. **Pinch capture** (`capture.sh`, then `crop_check.cs` to compare camera and photo)
3. **Frame look and resize sound** (`frame_check.sh`)
4. **Gallery look** (`gallery_look.cs`, hover, grab, arrow press)
5. **Floating photo outline** (`photo_look.cs`)
6. **Grabbing** (`grab.cs`, then `wait_log.sh`)
7. **Gallery interactions** (`gallery_interact.cs`, then `wait_log.sh`)
8. Optional: `video.sh` for an MP4 of the gallery

Each result line ends with `(expect …)` so you can compare it with the value shown.

## Clean up

```bash
./ev.sh pose.cs off
~/.unity/bin/unity command editor.playmode stop --no-banner
./photos.sh cleanup
```

## What the tests cannot show

- Real finger-pose detection and pinch quality
- Whether a real fingertip hits the small poke targets
- Alignment with the real cameras
- How things look over real passthrough, and how the sounds actually sound
- Performance on the Quest 3

These need a headset run (checklist item 13 in `PHOTO_FEATURE_CHECKLIST.md`).
