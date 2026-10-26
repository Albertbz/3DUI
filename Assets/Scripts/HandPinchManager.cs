using System;
using UnityEngine;

public class HandPinchManager : MonoBehaviour
{
    public enum HandType { Left, Right }

    public class HandState
    {
        public HandType Type;
        public OVRHand Hand;
        public bool IsPinching;
    }

    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRHand rightHand;

    private HandState leftState;
    private HandState rightState;
    private HandState[] handStates;

    // Tracks the hand that initiated the first pinch
    private HandState primaryPinchingHand;

    // Create event for proximity pinch detection
    public static event Action<Vector3, Vector3> OnProximityPinchTriggered;
    public static event Action<Vector3, Vector3> OnProximityPinchReleased;
    public static event Action<Vector3, Vector3> OnProximityPinchUpdated;

    private bool performingProximityPinch = false;

    // Lets other systems (e.g. the wrist gallery) react while the frame is being drawn
    public static bool IsProximityPinchActive { get; private set; }

    private void Awake()
    {
        leftState = new HandState { Type = HandType.Left, Hand = leftHand };
        rightState = new HandState { Type = HandType.Right, Hand = rightHand };

        // Cache the array to avoid allocations in Update
        handStates = new[] { leftState, rightState };
    }

    private void Update()
    {
        for (int i = 0; i < handStates.Length; i++)
        {
            var state = handStates[i];
            bool currentlyPinching = state.Hand != null &&
                                     state.Hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

            // Edge detection: pinch started this frame
            if (currentlyPinching && !state.IsPinching)
            {
                state.IsPinching = true;
                OnPinchStarted(state);
            }
            // Edge detection: pinch released this frame
            else if (!currentlyPinching && state.IsPinching)
            {
                state.IsPinching = false;
                OnPinchReleased(state);
            }
        }

        if (performingProximityPinch && leftHand != null && rightHand != null)
        {
            // Update the proximity pinch event with the current position of each PointerPose
            OnProximityPinchUpdated?.Invoke(leftHand.PointerPose.position, rightHand.PointerPose.position);
        }
    }

    private void OnPinchStarted(HandState hand)
    {
        if (primaryPinchingHand == null)
        {
            // First hand to pinch becomes the main hand
            primaryPinchingHand = hand;
            Debug.Log($"Primary pinch initiated by: {hand.Type}");
        }
        else
        {
            // The other hand started pinching while the primary hand was already pinching
            Debug.Log($"Secondary pinch detected: {hand.Type} started while {primaryPinchingHand.Type} is active.");
            OnSecondaryPinchStarted(primaryPinchingHand, hand);
        }
    }

    private void OnPinchReleased(HandState hand)
    {
        if (hand == primaryPinchingHand)
        {
            // Find if the secondary hand is still pinching
            HandState otherHand = (hand == leftState) ? rightState : leftState;

            if (otherHand.IsPinching)
            {
                // Promote secondary hand to primary, or clear depending on requirements
                primaryPinchingHand = otherHand;
                Debug.Log($"Primary hand released. Handed over primary to: {primaryPinchingHand.Type}");
            }
            else
            {
                primaryPinchingHand = null;
                Debug.Log("All pinches released.");
            }
        }

        if (performingProximityPinch)
        {
            performingProximityPinch = false;
            IsProximityPinchActive = false;
            Vector3 leftHandPosition = leftHand.PointerPose.position;
            Vector3 rightHandPosition = rightHand.PointerPose.position;
            OnProximityPinchReleased?.Invoke(leftHandPosition, rightHandPosition);
            Debug.Log("Proximity pinch released.");
        }
    }

    private void OnSecondaryPinchStarted(HandState primary, HandState secondary)
    {
        // A pinch that is grabbing a photo or thumbnail should not open the capture frame
        if (PhotoObject.AnyHeld || GalleryThumbnail.AnyHeld)
        {
            return;
        }

        if (!performingProximityPinch)
        {
            // Check distance between the two pinches
            Vector3 primaryPosition = primary.Hand.PointerPose.position;
            Vector3 secondaryPosition = secondary.Hand.PointerPose.position;
            float distance = Vector3.Distance(primaryPosition, secondaryPosition);
            Debug.Log($"Distance between pinches: {distance}");

            // If the distance is less than a threshold, consider it a "proximity pinch"
            // and trigger an event that can be handled by other scripts
            if (distance < 0.2f) // Example threshold
            {
                Debug.Log("Proximity pinch detected!");
                // Trigger an event or call a method to handle the proximity pinch
                Vector3 leftHandPosition = leftHand.PointerPose.position;
                Vector3 rightHandPosition = rightHand.PointerPose.position;

                performingProximityPinch = true;
                IsProximityPinchActive = true;
                OnProximityPinchTriggered?.Invoke(leftHandPosition, rightHandPosition);
            }

        }
    }
}