using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Rounded outline drawn by the CaptureFrame shader on a single quad. Shared by the capture frame and the
    /// wrist gallery so they speak the same design language: thin light-blue strokes, a faint fill and a soft
    /// glow, with marching-ants dashes for whatever is actively being selected.
    /// Create one with <see cref="Create"/>; it scales its own transform to fit <see cref="Size"/>.
    /// </summary>
    public class FrameOutline : MonoBehaviour
    {
        public enum FrameStyle { Viewfinder = 0, Glow = 1, MarchingAnts = 2, Glass = 3 }

        public static readonly Color AccentColor = new Color(0.55f, 0.9f, 1f, 1f);
        public static readonly Color WarningColor = new Color(1f, 0.45f, 0.35f, 1f);
        // Icons drawn on top of outlines: the accent pushed toward white, like the shader's strokes
        public static readonly Color StrokeColor = Color.Lerp(AccentColor, Color.white, 0.35f);

        public FrameStyle Style { get; set; } = FrameStyle.MarchingAnts;
        public Color Color { get; set; } = AccentColor;
        public Color InvalidColor { get; set; } = WarningColor;
        public float Thickness { get; set; } = 0.004f;
        public float CornerRadius { get; set; } = 0.01f;
        public float Padding { get; set; } = 0.03f;     // Extra quad area around the outline so the glow is not clipped
        public bool Dashed { get; set; } = true;        // Marching ants only; false draws a solid line
        public float DashLength { get; set; } = 0.02f;
        public float FillStrength { get; set; } = 1f;
        public float Valid { get; set; } = 1f;          // 0 tints the outline with InvalidColor
        public float Appear { get; set; } = 1f;
        public float Activity { get; set; }             // Brightens the outline; eases back to 0 by itself
        public float ActivityDecay { get; set; } = 2f;  // Per second
        public Vector2 Size { get; set; }

        public Material Material { get; private set; }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int InvalidColorId = Shader.PropertyToID("_InvalidColor");
        private static readonly int StyleId = Shader.PropertyToID("_Style");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int PaddingId = Shader.PropertyToID("_Padding");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
        private static readonly int DashedId = Shader.PropertyToID("_Dashed");
        private static readonly int DashLengthId = Shader.PropertyToID("_DashLength");
        private static readonly int FillStrengthId = Shader.PropertyToID("_FillStrength");
        private static readonly int ValidId = Shader.PropertyToID("_Valid");
        private static readonly int ActivityId = Shader.PropertyToID("_Activity");
        private static readonly int AppearId = Shader.PropertyToID("_Appear");

        private static Mesh quadMesh;

        /// <param name="template">Material to copy; Resources/CaptureFrame when null.</param>
        public static FrameOutline Create(Transform parent, string name, Material template = null)
        {
            var outlineObject = new GameObject(name);
            outlineObject.layer = parent.gameObject.layer;
            outlineObject.transform.SetParent(parent, false);

            if (quadMesh == null)
            {
                quadMesh = CreateQuadMesh();
            }
            outlineObject.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var meshRenderer = outlineObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            var outline = outlineObject.AddComponent<FrameOutline>();
            outline.Material = new Material(template != null ? template : Resources.Load<Material>("CaptureFrame"));
            meshRenderer.sharedMaterial = outline.Material;
            return outline;
        }

        private void OnDestroy()
        {
            Destroy(Material);
        }

        private void LateUpdate()
        {
            Activity = Mathf.MoveTowards(Activity, 0f, Time.deltaTime * ActivityDecay);
            transform.localScale = new Vector3(Size.x + Padding * 2f, Size.y + Padding * 2f, 1f);

            Material.SetColor(ColorId, Color);
            Material.SetColor(InvalidColorId, InvalidColor);
            Material.SetFloat(StyleId, (float)Style);
            Material.SetVector(SizeId, new Vector4(Size.x, Size.y, 0f, 0f));
            Material.SetFloat(PaddingId, Padding);
            Material.SetFloat(ThicknessId, Thickness);
            Material.SetFloat(CornerRadiusId, CornerRadius);
            Material.SetFloat(DashedId, Dashed ? 1f : 0f);
            Material.SetFloat(DashLengthId, DashLength);
            Material.SetFloat(FillStrengthId, FillStrength);
            Material.SetFloat(ValidId, Valid);
            Material.SetFloat(ActivityId, Activity);
            Material.SetFloat(AppearId, Appear);
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "FrameOutlineQuad" };
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
}
