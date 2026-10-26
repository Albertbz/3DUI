using UnityEngine;

public class BoundaryFramePrefab : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private FrameOutline.FrameStyle style = FrameOutline.FrameStyle.Viewfinder;
    [SerializeField] private Color color = FrameOutline.AccentColor;
    [SerializeField] private Color tooSmallColor = FrameOutline.WarningColor;
    [SerializeField] private float lineThickness = 0.004f; // 4 mm
    [SerializeField] private float cornerRadius = 0.01f;
    [SerializeField] private float appearDuration = 0.15f;

    [Tooltip("Uses Resources/CaptureFrame when left empty")]
    [SerializeField] private Material frameMaterial;

    [Header("Resize feedback")]
    [Tooltip("Resize speed (m/s of diagonal change) that counts as full activity")]
    [SerializeField] private float fullActivitySpeed = 0.6f;
    [SerializeField] private FrameResizeAudio resizeAudio;

    // Old bar edges from the first version of the frame; hidden in favor of the shader quad
    [Header("Legacy bar edges")]
    [SerializeField] private Transform topEdge;
    [SerializeField] private Transform bottomEdge;
    [SerializeField] private Transform leftEdge;
    [SerializeField] private Transform rightEdge;

    private FrameOutline outline;
    private float previousDiagonal = -1f;
    private float valid = 1f;
    private bool capturable = true;
    private float appear;

    private void Awake()
    {
        foreach (Transform edge in new[] { topEdge, bottomEdge, leftEdge, rightEdge })
        {
            if (edge != null)
            {
                edge.gameObject.SetActive(false);
            }
        }

        // Inherits this object's layer, which keeps it out of captures
        outline = FrameOutline.Create(transform, "FrameQuad", frameMaterial);

        if (resizeAudio == null && !TryGetComponent(out resizeAudio))
        {
            resizeAudio = gameObject.AddComponent<FrameResizeAudio>();
        }
    }

    private void OnEnable()
    {
        appear = 0f;
        previousDiagonal = -1f;
        outline.Activity = 0f;
    }

    private void Update()
    {
        appear = appearDuration > 0f ? Mathf.MoveTowards(appear, 1f, Time.deltaTime / appearDuration) : 1f;
        valid = Mathf.MoveTowards(valid, capturable ? 1f : 0f, Time.deltaTime * 8f);

        outline.Style = style;
        outline.Color = color;
        outline.InvalidColor = tooSmallColor;
        outline.Thickness = lineThickness;
        outline.CornerRadius = cornerRadius;
        outline.Valid = valid;
        outline.Appear = Mathf.SmoothStep(0f, 1f, appear);
    }

    public void SetDimensions(float width, float height)
    {
        outline.Size = new Vector2(width, height);

        float diagonal = Mathf.Sqrt(width * width + height * height);
        float sizeChangeSpeed = 0f;
        if (previousDiagonal >= 0f && Time.deltaTime > 0f)
        {
            sizeChangeSpeed = (diagonal - previousDiagonal) / Time.deltaTime;
        }
        previousDiagonal = diagonal;

        outline.Activity = Mathf.Max(outline.Activity, Mathf.Clamp01(Mathf.Abs(sizeChangeSpeed) / fullActivitySpeed));

        if (resizeAudio != null)
        {
            resizeAudio.UpdateSize(diagonal, sizeChangeSpeed);
        }
    }

    // Tints the frame when it is too small to take a photo
    public void SetCapturable(bool isCapturable)
    {
        capturable = isCapturable;
    }
}
