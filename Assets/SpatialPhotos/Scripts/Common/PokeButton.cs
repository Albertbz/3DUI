using System;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Flat poke button built entirely from code: an Interaction SDK PokeInteractable on a clipped plane, drawn
    /// with the shared <see cref="FrameOutline"/> (solid rim at rest, marching ants while hovered, a glow when
    /// pressed). Its face points along -Z, like the gallery's arrows. Create one with <see cref="Create"/>.
    /// </summary>
    public class PokeButton : MonoBehaviour
    {
        public event Action Pressed;

        public FrameOutline Outline { get; private set; }
        public PokeInteractable Interactable { get; private set; }

        private static Font font;
        private static Material iconMaterial;
        private static Material backdropMaterial;

        private int hoverCount;

        /// <param name="hoverDistance">How close a fingertip must come before the button reacts, in meters.</param>
        public static PokeButton Create(Transform parent, string name, Vector2 size, float cornerRadius, float hoverDistance = 0.02f)
        {
            var buttonObject = new GameObject(name);
            buttonObject.layer = parent.gameObject.layer;
            buttonObject.transform.SetParent(parent, false);

            var plane = buttonObject.AddComponent<PlaneSurface>();
            var clipper = buttonObject.AddComponent<BoundsClipper>();
            clipper.Size = new Vector3(size.x, size.y, 1f);
            var clippedPlane = buttonObject.AddComponent<ClippedPlaneSurface>();
            clippedPlane.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { clipper });

            var button = buttonObject.AddComponent<PokeButton>();
            button.Interactable = buttonObject.AddComponent<PokeInteractable>();
            button.Interactable.InjectAllPokeInteractable(clippedPlane);
            // Each hand hovers one interactor at a time and poke goes first: keep the zone small so these buttons
            // don't block pinching what's around them
            button.Interactable.EnterHoverNormal = hoverDistance;
            button.Interactable.ExitHoverNormal = hoverDistance * 2f;
            button.Interactable.WhenPointerEventRaised += button.HandlePointerEvent;

            button.Outline = FrameOutline.Create(buttonObject.transform, "Outline");
            button.Outline.transform.localPosition = new Vector3(0f, 0f, 0.001f); // Just behind the label or icon
            button.Outline.Size = size;
            button.Outline.Thickness = 0.0015f;
            button.Outline.CornerRadius = cornerRadius;
            button.Outline.Padding = 0.008f;
            button.Outline.DashLength = 0.01f;
            button.Outline.FillStrength = 2f;
            button.Outline.Dashed = false;
            return button;
        }

        /// <summary>Adds a centered text label, <paramref name="height"/> being the cap height in meters.</summary>
        public TextMesh AddLabel(string text, float height)
        {
            return CreateLabel(transform, text, height);
        }

        /// <summary>Adds an X drawn with two crossed strokes.</summary>
        public void AddCross(float size, float strokeWidth)
        {
            AddMesh("Cross", CreateCrossMesh(size, strokeWidth), IconMaterial, -0.001f);
        }

        /// <summary>Adds a dark disc behind the icon, so it stays readable over bright photos.</summary>
        public void AddBackdrop(float diameter)
        {
            AddMesh("Backdrop", CreateDiscMesh(diameter * 0.5f, 32), BackdropMaterial, -0.0005f);
        }

        private void AddMesh(string meshName, Mesh mesh, Material material, float z)
        {
            var meshObject = new GameObject(meshName);
            meshObject.layer = gameObject.layer;
            meshObject.transform.SetParent(transform, false);
            meshObject.transform.localPosition = new Vector3(0f, 0f, z);
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = material;
        }

        /// <summary>World-space text in the outline's stroke color, readable from the -Z side.</summary>
        public static TextMesh CreateLabel(Transform parent, string text, float height)
        {
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            var labelObject = new GameObject("Label");
            labelObject.layer = parent.gameObject.layer;
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.001f);

            const int fontSize = 64; // Rasterised large and scaled down so the text stays sharp
            var label = labelObject.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = fontSize;
            label.characterSize = height * 10f / fontSize / 0.7f; // A cap is about 70% of the em size
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = FrameOutline.StrokeColor;
            label.text = text;

            var meshRenderer = labelObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = font.material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return label;
        }

        private static Material IconMaterial
        {
            get
            {
                if (iconMaterial == null)
                {
                    iconMaterial = new Material(Shader.Find("Unlit/Color")) { color = FrameOutline.StrokeColor };
                }
                return iconMaterial;
            }
        }

        private static Material BackdropMaterial
        {
            get
            {
                if (backdropMaterial == null)
                {
                    backdropMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.1f, 0.14f, 0.18f, 1f) };
                }
                return backdropMaterial;
            }
        }

        private void OnDisable()
        {
            hoverCount = 0;
            Outline.Dashed = false;
        }

        private void OnDestroy()
        {
            if (Interactable != null)
            {
                Interactable.WhenPointerEventRaised -= HandlePointerEvent;
            }
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    hoverCount++;
                    break;
                case PointerEventType.Unhover:
                    hoverCount = Mathf.Max(0, hoverCount - 1);
                    break;
                case PointerEventType.Select:
                    Outline.Activity = 1f;
                    Pressed?.Invoke();
                    break;
            }
            Outline.Dashed = hoverCount > 0;
        }

        private static Mesh CreateCrossMesh(float size, float strokeWidth)
        {
            var vertices = new Vector3[8];
            var triangles = new int[12];
            float half = size * 0.5f;
            float halfWidth = strokeWidth * 0.5f;
            for (int stroke = 0; stroke < 2; stroke++)
            {
                Quaternion rotation = Quaternion.Euler(0f, 0f, stroke == 0 ? 45f : -45f);
                int v = stroke * 4;
                vertices[v + 0] = rotation * new Vector3(-half, -halfWidth, 0f);
                vertices[v + 1] = rotation * new Vector3(half, -halfWidth, 0f);
                vertices[v + 2] = rotation * new Vector3(half, halfWidth, 0f);
                vertices[v + 3] = rotation * new Vector3(-half, halfWidth, 0f);

                // Wound to face -Z, toward the viewer
                int t = stroke * 6;
                triangles[t + 0] = v + 0; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 0; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "CrossIcon", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDiscMesh(float radius, int segments)
        {
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

                // Counter-clockwise angles, wound to face -Z
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "Disc", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
