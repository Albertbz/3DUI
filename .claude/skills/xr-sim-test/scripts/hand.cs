// hand.cs <left|right> <freeze|live|pinch|release|at> [right up forward]
// 'at' places the hand's pointer pose relative to the head: metres to the right, up and forward.
// freeze stops OVRHand reading tracking each frame, so its pinch state and pointer pose can be scripted.
// HandPinchManager and WristGallery then run their real logic on that state. 'live' resumes tracking.
var anchor = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/" + ("__ARG1__" == "left" ? "Left" : "Right") + "HandAnchor");
var hand = anchor.GetComponentInChildren<OVRHand>(true);
var type = typeof(OVRHand);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var stateField = type.GetField("_handState", flags);
var state = (OVRPlugin.HandState)stateField.GetValue(hand);
switch ("__ARG2__")
{
    case "freeze":
        hand.enabled = false;
        type.GetProperty("IsTracked").SetValue(hand, true);
        type.GetProperty("IsDataValid").SetValue(hand, true);
        state.Pinches = 0;
        break;
    case "live": hand.enabled = true; break;
    case "pinch": state.Pinches = OVRPlugin.HandFingerPinch.Index; break;
    case "release": state.Pinches = 0; break;
    case "at":
        var head = UnityEngine.GameObject.Find("[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor").transform;
        hand.PointerPose.position = head.position + head.right * float.Parse("__ARG3__") + head.up * float.Parse("__ARG4__") + head.forward * float.Parse("__ARG5__");
        break;
}
stateField.SetValue(hand, state);
return $"{hand.name}: scripted={!hand.enabled} pinching={hand.GetFingerIsPinching(OVRHand.HandFinger.Index)} pointer={hand.PointerPose.position:F2}";
