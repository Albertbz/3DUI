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

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static int heldCount;
        public static bool AnyHeld => heldCount > 0;

        public PhotoEntry Entry { get; private set; }
        public float ThumbnailHeight => thumbnailHeight;

        private PhotoObject photoPrefab;
        private PhotoLibrary library;
        private Material material;
        private Vector2 size;
        private bool isHeld;
        private PhotoObject pulledPhoto;
        private float maxWidth = float.PositiveInfinity;
        private FrameOutline outline;
        private int hoverCount;

        private void Awake()
        {
            material = thumbnailRenderer.material;

            // Same outline as the capture frame: a solid rim at rest, marching ants while hovered or held
            outline = FrameOutline.Create(transform, "Outline");
            outline.transform.localPosition = new Vector3(0f, 0f, 0.001f); // Just behind the photo
            outline.Thickness = outlineThickness;
            outline.CornerRadius = 0.004f;
            outline.Padding = 0.008f;
            outline.DashLength = 0.01f;
            outline.FillStrength = 2f;
            outline.Dashed = false;
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
            material.SetTexture(MainTexId, entry.Thumbnail);
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
