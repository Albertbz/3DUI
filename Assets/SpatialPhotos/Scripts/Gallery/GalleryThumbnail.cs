using System;
using Oculus.Interaction;
using UnityEngine;

namespace SpatialPhotos
{
    public class GalleryThumbnail : MonoBehaviour
    {
        [SerializeField] private Renderer thumbnailRenderer;  // Unit quad, scaled to the thumbnail size
        [SerializeField] private BoxCollider grabCollider;
        [SerializeField] private PointableElement grabbable;  // Grabbable on this object

        [SerializeField] private float thumbnailHeight = 0.05f;
        [SerializeField] private float pullOutDistance = 0.1f;
        [SerializeField] private float returnSpeed = 15f;

        [Header("Outline")]
        [SerializeField] private float outlineGap = 0.003f;       // Space between the photo and its rim
        [SerializeField] private float outlineThickness = 0.0015f;

        [Header("Delete button (top-right corner)")]
        [SerializeField] private float deleteButtonSize = 0.014f;
        [SerializeField] private float deleteButtonInset = 0.002f; // How far the button's center sits inside the corner

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        private static int heldCount;
        public static bool AnyHeld => heldCount > 0;

        public PhotoEntry Entry { get; private set; }
        public float ThumbnailHeight => thumbnailHeight;
        public float Fade => fade;

        /// <summary>Raised when the corner cross is poked; the gallery asks for confirmation.</summary>
        public event Action<GalleryThumbnail> DeleteRequested;

        /// <summary>Tints the outline with the warning color while a deletion waits for confirmation.</summary>
        public bool MarkedForDeletion
        {
            get => outline.Valid < 1f;
            set => outline.Valid = value ? 0f : 1f;
        }

        private PhotoObject photoPrefab;
        private PhotoLibrary library;
        private Material material;
        private Vector2 size;
        private bool isHeld;
        private PhotoObject pulledPhoto;
        private float maxWidth = float.PositiveInfinity;
        private FrameOutline outline;
        private int hoverCount;
        private PokeButton deleteButton;
        private float fade = 1f;

        private void Awake()
        {
            material = thumbnailRenderer.material;
            // After the gallery panel behind it, so a fading thumbnail blends over the panel instead of hiding it
            material.renderQueue = 3001;

            // Same outline as the capture frame: a solid rim at rest, marching ants while hovered or held
            outline = FrameOutline.Create(transform, "Outline");
            outline.transform.localPosition = new Vector3(0f, 0f, 0.001f); // Just behind the photo
            outline.Thickness = outlineThickness;
            outline.CornerRadius = 0.004f;
            outline.Padding = 0.008f;
            outline.DashLength = 0.01f;
            outline.FillStrength = 2f;
            outline.Dashed = false;

            deleteButton = PokeButton.Create(transform, "DeleteButton", Vector2.one * deleteButtonSize, deleteButtonSize * 0.5f);
            deleteButton.Outline.Padding = 0.006f;
            deleteButton.AddBackdrop(deleteButtonSize - 0.001f);
            deleteButton.AddCross(deleteButtonSize * 0.5f, 0.0012f);
            deleteButton.Pressed += () => DeleteRequested?.Invoke(this);
            deleteButton.Outline.Material.renderQueue = 3002; // In front of the photo
        }

        private void OnEnable()
        {
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }

        private void OnDisable()
        {
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
            hoverCount = 0;
            Release();
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        /// <param name="maxThumbnailWidth">Widest the thumbnail may be, so wide photos don't overlap the next slot.</param>
        public void Init(PhotoLibrary photoLibrary, PhotoObject photoObjectPrefab, float maxThumbnailWidth)
        {
            library = photoLibrary;
            photoPrefab = photoObjectPrefab;
            maxWidth = maxThumbnailWidth;
        }

        /// <summary>Shows <paramref name="entry"/> in this thumbnail, or hides it when null.</summary>
        public void Bind(PhotoEntry entry)
        {
            Entry = entry;
            gameObject.SetActive(entry != null);
            if (entry == null)
            {
                return;
            }

            float aspect = entry.HeightM > 0f ? entry.WidthM / entry.HeightM : 1f;
            size = new Vector2(thumbnailHeight * aspect, thumbnailHeight);
            if (size.x > maxWidth)
            {
                size *= maxWidth / size.x;
            }
            thumbnailRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            grabCollider.size = new Vector3(size.x, size.y, 0.01f);
            outline.Size = size + Vector2.one * (outlineGap * 2f);
            // In front of the photo and the gallery's swipe surface, so a fingertip reaches it first
            deleteButton.transform.localPosition = new Vector3(
                size.x * 0.5f - deleteButtonInset, size.y * 0.5f - deleteButtonInset, -0.004f);
            material.SetTexture(MainTexId, entry.Thumbnail);
        }

        /// <summary>
        /// Fades the thumbnail while it scrolls past the gallery's edge. Only a fully shown thumbnail has a
        /// delete button, and a mostly faded one can't be grabbed.
        /// </summary>
        public void SetFade(float value)
        {
            fade = value;
            material.SetFloat(AlphaId, fade);
            thumbnailRenderer.enabled = fade > 0f && pulledPhoto == null;
            outline.Appear = fade;
            grabCollider.enabled = fade > 0.5f;
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
                    if (!isHeld)
                    {
                        isHeld = true;
                        heldCount++;
                    }
                    break;
                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (grabbable.SelectingPointsCount == 0)
                    {
                        Release();
                    }
                    break;
            }
        }

        private void Update()
        {
            outline.Dashed = isHeld || hoverCount > 0;
            bool showDelete = !isHeld && fade > 0.99f;
            if (deleteButton.gameObject.activeSelf != showDelete)
            {
                deleteButton.gameObject.SetActive(showDelete);
            }
            if (isHeld)
            {
                outline.Activity = 1f;

                if (pulledPhoto == null && transform.localPosition.magnitude > pullOutDistance)
                {
                    PullOut();
                }

                if (pulledPhoto != null)
                {
                    pulledPhoto.transform.SetPositionAndRotation(transform.position, transform.rotation);
                }
            }
            else
            {
                // Spring back into the slot (the parent)
                float t = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
                transform.localPosition = Vector3.Lerp(transform.localPosition, Vector3.zero, t);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, t);
            }
        }

        private void PullOut()
        {
            Texture2D fullTexture = library != null ? library.LoadFullTexture(Entry) : null;
            bool ownsTexture = fullTexture != null;
            Texture texture = ownsTexture ? fullTexture : Entry.Thumbnail; // PNG may still be saving

            pulledPhoto = Instantiate(photoPrefab, transform.position, transform.rotation);
            pulledPhoto.Init(Entry, texture, flash: false, ownsTexture: ownsTexture);
            pulledPhoto.GrowFrom(size);
            pulledPhoto.BeginExternalHold();

            thumbnailRenderer.enabled = false;
            outline.gameObject.SetActive(false);
        }

        private void Release()
        {
            if (!isHeld)
            {
                return;
            }
            isHeld = false;
            heldCount--;

            if (pulledPhoto != null)
            {
                pulledPhoto.EndExternalHold();
                pulledPhoto = null;

                // Pulling a photo out does not remove it from the gallery: jump straight back to the slot
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            thumbnailRenderer.enabled = true;
            outline.gameObject.SetActive(true);
        }
    }
}
