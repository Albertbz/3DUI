using UnityEngine;

public class BoundaryFramePrefab : MonoBehaviour
{
    public enum FrameStyle { Viewfinder = 0, Glow = 1, MarchingAnts = 2, Glass = 3 }

    [Header("Look")]
    [SerializeField] private FrameStyle style = FrameStyle.Viewfinder;
    [SerializeField] private Color color = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField] private Color tooSmallColor = new Color(1f, 0.45f, 0.35f, 1f);
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

    // Extra quad area around the border so the glow is not clipped
    private const float Padding = 0.03f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int InvalidColorId = Shader.PropertyToID("_InvalidColor");
    private static readonly int StyleId = Shader.PropertyToID("_Style");
    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int PaddingId = Shader.PropertyToID("_Padding");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
    private static readonly int ValidId = Shader.PropertyToID("_Valid");
    private static readonly int ActivityId = Shader.PropertyToID("_Activity");
    private static readonly int AppearId = Shader.PropertyToID("_Appear");

    private Transform quad;
    private Material material;

    private float width;
    private float height;
    private float previousDiagonal = -1f;
    private float activity;
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

        if (frameMaterial == null)
        {
            frameMaterial = Resources.Load<Material>("CaptureFrame");
        }
        material = new Material(frameMaterial);

        var quadObject = new GameObject("FrameQuad");
        quadObject.layer = gameObject.layer; // keeps it out of captures
        quad = quadObject.transform;
        quad.SetParent(transform, false);
        quadObject.AddComponent<MeshFilter>().sharedMesh = CreateQuadMesh();
        var meshRenderer = quadObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        if (resizeAudio == null && !TryGetComponent(out resizeAudio))
        {
            resizeAudio = gameObject.AddComponent<FrameResizeAudio>();
        }
    }

    private void OnEnable()
    {
        appear = 0f;
        activity = 0f;
        previousDiagonal = -1f;
    }

    private void OnDestroy()
    {
        Destroy(material);
    }

    private void Update()
    {
        appear = appearDuration > 0f ? Mathf.MoveTowards(appear, 1f, Time.deltaTime / appearDuration) : 1f;
        valid = Mathf.MoveTowards(valid, capturable ? 1f : 0f, Time.deltaTime * 8f);
        // Let the activity glow ease out once the hands stop moving
        activity = Mathf.MoveTowards(activity, 0f, Time.deltaTime * 2f);

        material.SetColor(ColorId, color);
        material.SetColor(InvalidColorId, tooSmallColor);
        material.SetFloat(StyleId, (float)style);
        material.SetVector(SizeId, new Vector4(width, height, 0f, 0f));
        material.SetFloat(PaddingId, Padding);
        material.SetFloat(ThicknessId, lineThickness);
        material.SetFloat(CornerRadiusId, cornerRadius);
        material.SetFloat(ValidId, valid);
        material.SetFloat(ActivityId, activity);
        material.SetFloat(AppearId, Mathf.SmoothStep(0f, 1f, appear));
    }

    public void SetDimensions(float width, float height)
    {
        this.width = width;
        this.height = height;
        quad.localScale = new Vector3(width + Padding * 2f, height + Padding * 2f, 1f);

        float diagonal = Mathf.Sqrt(width * width + height * height);
        float sizeChangeSpeed = 0f;
        if (previousDiagonal >= 0f && Time.deltaTime > 0f)
        {
            sizeChangeSpeed = (diagonal - previousDiagonal) / Time.deltaTime;
        }
        previousDiagonal = diagonal;

        activity = Mathf.Max(activity, Mathf.Clamp01(Mathf.Abs(sizeChangeSpeed) / fullActivitySpeed));

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

    private static Mesh CreateQuadMesh()
    {
        var mesh = new Mesh { name = "CaptureFrameQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
