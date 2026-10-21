using UnityEngine;

public class ImageBoundaryBox : MonoBehaviour
{
    [SerializeField] private BoundaryFramePrefab boundaryFrame;
    [SerializeField] private Camera head;

    private void Awake()
    {
        EnableBoundaryFrame(false);
    }

    private void OnEnable()
    {
        HandPinchManager.OnProximityPinchTriggered += HandleProximityPinch;
        HandPinchManager.OnProximityPinchReleased += HandleProximityPinchRelease;
        HandPinchManager.OnProximityPinchUpdated += HandleProximityPinchUpdate;
    }

    private void OnDisable()
    {
        HandPinchManager.OnProximityPinchTriggered -= HandleProximityPinch;
        HandPinchManager.OnProximityPinchReleased -= HandleProximityPinchRelease;
        HandPinchManager.OnProximityPinchUpdated -= HandleProximityPinchUpdate;
    }

    private void HandleProximityPinch(Vector3 leftHandPosition, Vector3 rightHandPosition)
    {
        EnableBoundaryFrame(true);
        UpdateBoundaryFrameFacingCamera(leftHandPosition, rightHandPosition);
    }

    private void HandleProximityPinchRelease(Vector3 leftHandPosition, Vector3 rightHandPosition)
    {
        EnableBoundaryFrame(false);
        // TODO: Handle taking screenshot using the framed dimensions
    }

    private void HandleProximityPinchUpdate(Vector3 leftHandPosition, Vector3 rightHandPosition)
    {
        UpdateBoundaryFrameFacingCamera(leftHandPosition, rightHandPosition);
    }

    private void EnableBoundaryFrame(bool enable)
    {
        if (boundaryFrame != null)
        {
            boundaryFrame.gameObject.SetActive(enable);
        }
    }

    private void UpdateBoundaryFrameFacingCamera(Vector3 cornerA, Vector3 cornerB)
    {
        Transform frameTransform = boundaryFrame.transform;

        // 1. Center is always the midpoint between pinch points
        Vector3 center = (cornerA + cornerB) * 0.5f;
        frameTransform.position = center;

        // 2. Align rotation to face the headset
        Vector3 forward = (center - head.transform.position).normalized;
        Quaternion frameRotation = Quaternion.LookRotation(forward, head.transform.up);
        frameTransform.rotation = frameRotation;

        // 3. Project corner displacement into the frame's local coordinate frame
        Vector3 diagonalWorld = cornerB - cornerA;
        Vector3 diagonalLocal = Quaternion.Inverse(frameRotation) * diagonalWorld;

        float width = Mathf.Abs(diagonalLocal.x);
        float height = Mathf.Abs(diagonalLocal.y);

        // 4. Update the prefab's internal edge positions/scales
        boundaryFrame.SetDimensions(width, height);
    }
}