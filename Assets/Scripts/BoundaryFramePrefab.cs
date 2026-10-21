using UnityEngine;

public class BoundaryFramePrefab : MonoBehaviour
{
    [SerializeField] private Transform topEdge;
    [SerializeField] private Transform bottomEdge;
    [SerializeField] private Transform leftEdge;
    [SerializeField] private Transform rightEdge;

    [SerializeField] private float edgeThickness = 0.015f; // 1.5 cm

    public void SetDimensions(float width, float height)
    {
        float clampedThickness = Mathf.Min(edgeThickness, Mathf.Min(width, height) * 0.5f);

        // Top & Bottom bars span full width
        topEdge.localPosition = new Vector3(0f, (height - clampedThickness) * 0.5f, 0f);
        topEdge.localScale = new Vector3(width, clampedThickness, 1f);

        bottomEdge.localPosition = new Vector3(0f, -(height - clampedThickness) * 0.5f, 0f);
        bottomEdge.localScale = new Vector3(width, clampedThickness, 1f);

        // Left & Right bars fit between top and bottom bars to prevent corner overlap
        float sideHeight = Mathf.Max(0f, height - (clampedThickness * 2f));

        leftEdge.localPosition = new Vector3(-(width - clampedThickness) * 0.5f, 0f, 0f);
        leftEdge.localScale = new Vector3(clampedThickness, sideHeight, 1f);

        rightEdge.localPosition = new Vector3((width - clampedThickness) * 0.5f, 0f, 0f);
        rightEdge.localScale = new Vector3(clampedThickness, sideHeight, 1f);
    }
}