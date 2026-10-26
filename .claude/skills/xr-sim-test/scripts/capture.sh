#!/bin/zsh
# capture.sh : scripted two-hand pinch capture, then the "held photo blocks the pinch" check.
# Freezes both OVRHands (scripted state), pinches left then right 10 cm apart (proximity pinch),
# pulls the hands apart to draw a frame, releases right to capture. Saves frame/after renders.
# Hand positions are relative to the head (right, up, forward in metres).
DIR="${0:A:h}"
ev() { "$DIR/ev.sh" "$DIR/$@"; }

ev hand.cs left freeze >/dev/null; ev hand.cs right freeze >/dev/null
ev hand.cs left at -0.05 -0.15 0.45 >/dev/null; ev hand.cs right at 0.05 -0.15 0.45 >/dev/null
ev hand.cs left pinch >/dev/null; sleep 0.5
ev hand.cs right pinch >/dev/null; sleep 0.8
echo "== both pinching 10 cm apart (expect pinchActive=True frameActive=True, gallery hidden)"; ev state.cs
ev hand.cs left at -0.14 -0.25 0.55 >/dev/null; ev hand.cs right at 0.14 -0.05 0.55 >/dev/null; sleep 1.2
echo "== hands pulled apart"; ev state.cs frame_drawn
ev hand.cs right release >/dev/null; sleep 1.0
echo "== right released (expect library +1, one photo on the Photo layer at the frame, frame hidden)"; ev state.cs after_capture
ev hand.cs left release >/dev/null; sleep 0.5

echo "== hold the photo, then pinch both hands (expect pinchActive=False frameActive=False)"
ev hold.cs grab
ev hand.cs right pinch >/dev/null; sleep 0.4; ev hand.cs left pinch >/dev/null; sleep 0.6
ev state.cs | sed -n 2p
ev hand.cs left release >/dev/null; ev hand.cs right release >/dev/null; sleep 0.4
ev hold.cs release
ev hand.cs left live >/dev/null; ev hand.cs right live >/dev/null
echo "hands back on live tracking"
