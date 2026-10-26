#!/bin/zsh
# frame_check.sh : the capture frame's look and resize sound.
# Opens a proximity pinch with scripted hands, renders every frame style plus the too-small state,
# runs frame_check.cs (grow / hold / shrink, sampling the shader and FrameResizeAudio), then releases.
# The sweep ends at about 8x5 cm, under the minimum capture size, so the release takes no photo.
DIR="${0:A:h}"
ev() { "$DIR/ev.sh" "$DIR/$@"; }

ev hand.cs left freeze >/dev/null; ev hand.cs right freeze >/dev/null
ev hand.cs left at -0.05 -0.15 0.45 >/dev/null; ev hand.cs right at 0.05 -0.15 0.45 >/dev/null
ev hand.cs left pinch >/dev/null; sleep 0.5
ev hand.cs right pinch >/dev/null; sleep 0.8
echo "== pinch open (expect pinchActive=True frameActive=True)"; ev state.cs | sed -n 2p

ev hand.cs left at -0.14 -0.25 0.55 >/dev/null; ev hand.cs right at 0.14 -0.05 0.55 >/dev/null
for s in Viewfinder Glow MarchingAnts Glass; do
  ev frame_style.cs $s >/dev/null; sleep 0.6
  echo "== style $s"; ev state.cs frame_$s | tail -1
done
ev frame_style.cs Viewfinder >/dev/null

ev hand.cs left at -0.01 -0.15 0.5 >/dev/null; ev hand.cs right at 0.01 -0.14 0.5 >/dev/null; sleep 0.8
echo "== too small (expect a red tint)"; ev state.cs frame_too_small | tail -1

echo "== resize sweep"; ev frame_check.cs >/dev/null; "$DIR/wait_log.sh" 60

ev hand.cs right release >/dev/null; sleep 0.8
echo "== released under the minimum size (expect frameActive=False, library unchanged, hum stopped)"
ev state.cs | sed -n 2p
~/.unity/bin/unity command script eval --no-banner --code 'var f = UnityEngine.Object.FindFirstObjectByType<BoundaryFramePrefab>(UnityEngine.FindObjectsInactive.Include); var a = f.GetComponents<UnityEngine.AudioSource>(); return "audio sources playing=" + System.Linq.Enumerable.Count(a, s => s.isPlaying);' 2>&1 | grep -o "audio sources playing=[0-9]*" | head -1
ev hand.cs left release >/dev/null; sleep 0.3
ev hand.cs left live >/dev/null; ev hand.cs right live >/dev/null
echo "hands back on live tracking"
