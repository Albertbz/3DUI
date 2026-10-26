#!/bin/zsh
# connect.sh : starts the Meta XR Simulator, points the open Unity Editor at it (no sudo needed),
# enters Play mode and waits until the simulator reports a connected runtime.
DIR="${0:A:h}"
U=~/.unity/bin/unity
M=$("$DIR/metavr.sh") || exit 1
JSON=/Applications/MetaXRSimulator.app/Contents/Resources/MetaXRSimulator/meta_openxr_simulator.json
[ -f "$JSON" ] || { echo "Meta XR Simulator not found at /Applications/MetaXRSimulator.app"; exit 1; }

"$U" status --no-banner | grep -q ready || { echo "No ready Unity Editor. Open the project and check 'unity status'."; exit 1; }
"$M" xrsim app ensure-running >/dev/null 2>&1 || { echo "Could not start the simulator frontend"; exit 1; }

# The OpenXR loader reads XR_RUNTIME_JSON when Play mode creates the XR instance.
# Setting it inside the Editor process avoids 'metavr xrsim runtime activate', which needs sudo.
"$U" command script eval --code "System.Environment.SetEnvironmentVariable(\"XR_RUNTIME_JSON\", \"$JSON\"); return \"ok\";" --no-banner >/dev/null 2>&1

if ! "$U" command script eval --code 'return UnityEngine.Application.isPlaying;' --no-banner 2>/dev/null | grep -q '"result":true'; then
  "$U" command console clear --no-banner >/dev/null 2>&1
  "$U" command editor.playmode start --no-banner >/dev/null 2>&1
fi
for i in $(seq 1 30); do
  "$U" command script eval --code 'return UnityEngine.Application.isPlaying && UnityEngine.Time.frameCount > 60;' --no-banner 2>/dev/null | grep -q '"result":true' && break
  sleep 3
done
for i in $(seq 1 10); do
  "$M" xrsim runtime list --format json --timeout 3 2>/dev/null | grep -q endpoint_uri && { echo "connected"; exit 0; }
  sleep 2
done
echo "Play mode is running but no simulator runtime connected. Check that OpenXR is the Standalone XR loader, and see 'metavr xrsim runtime logs'."
exit 1
