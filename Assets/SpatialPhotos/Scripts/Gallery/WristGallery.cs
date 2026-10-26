using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Strip of photo thumbnails on the left wrist that appears when the palm faces the head.
    /// Drop the WristGallery prefab anywhere in the scene: at startup it attaches itself to the
    /// OVRCameraRig's left hand anchor and finds the hand, head and photo library.
    /// </summary>
    public class WristGallery : MonoBehaviour
    {
        [Header("References (found at startup when empty)")]
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private Transform wrist;             // LeftHandAnchor; the gallery attaches itself to it
        [SerializeField] private Transform head;              // CenterEyeAnchor
        [SerializeField] private Transform visualRoot;        // Scaled in/out when shown/hidden
        [SerializeField] private Transform stripRoot;         // Parent of the thumbnail slots
        [SerializeField] private PhotoLibrary library;

        [Header("Prefabs")]
        [SerializeField] private GalleryThumbnail thumbnailPrefab;
        [SerializeField] private PhotoObject photoPrefab;

        [Header("Scrolling")]
        [SerializeField] private PokeInteractable swipeSurface; // Optional: poke and drag along the strip
        [SerializeField] private PokeInteractable leftArrow;    // Optional
        [SerializeField] private PokeInteractable rightArrow;   // Optional

        [Header("Layout")]
        [SerializeField] private int visibleSlots = 5;
        [SerializeField] private float slotSpacing = 0.065f;
        [SerializeField] private float thumbnailGap = 0.01f;  // Minimum space between neighbouring thumbnails

        [Header("Look (shares the capture frame's marching-ants outline)")]
        [SerializeField] private float panelMargin = 0.012f;
        [SerializeField] private float buttonSize = 0.025f;

        [Header("Palm-up detection")]
        [Tooltip("Palm normal in the wrist transform's local space. Tune this in play mode if the gallery shows on the wrong side.")]
        [SerializeField] private Vector3 palmNormalLocal = Vector3.down;
        [SerializeField] private float showThreshold = 0.7f;
        [SerializeField] private float hideThreshold = 0.5f;
        [SerializeField] private float scaleSpeed = 1f / 0.15f;

        private readonly List<GalleryThumbnail> thumbnails = new List<GalleryThumbnail>();
        private int firstIndex;
        private bool shown;
        private float visibility;
        private float swipeStartX;
        private bool swiping;
        private FrameOutline leftArrowOutline;
        private FrameOutline rightArrowOutline;

        private void Awake()
        {
            if (library == null) library = FindFirstObjectByType<PhotoLibrary>();

            for (int i = 0; i < visibleSlots; i++)
            {
                var slot = new GameObject($"Slot {i}").transform;
                slot.SetParent(stripRoot, false);
                slot.localPosition = new Vector3((i - (visibleSlots - 1) * 0.5f) * slotSpacing, 0f, 0f);

                GalleryThumbnail thumbnail = Instantiate(thumbnailPrefab, slot, false);
                thumbnail.Init(library, photoPrefab, slotSpacing - thumbnailGap);
                thumbnails.Add(thumbnail);
            }

            CreatePanel();
            leftArrowOutline = StyleArrow(leftArrow);
            rightArrowOutline = StyleArrow(rightArrow);

            CaptureLayers.Apply(gameObject, CaptureLayers.CaptureIgnore);

            visualRoot.localScale = Vector3.zero;
            visualRoot.gameObject.SetActive(false);
            Refresh();
        }

        private void Start()
        {
            if (wrist == null) wrist = XRRig.HandAnchor(left: true);
            if (head == null) head = XRRig.Head;
            if (leftHand == null) leftHand = XRRig.Hand(left: true);
            if (wrist == null || head == null)
            {
                XRRig.Warn(this, "the left hand anchor and head");
                return;
            }

            // Follow the wrist; the gallery's own offset from the palm lives on its Visual child
            if (transform.parent != wrist)
            {
                transform.SetParent(wrist, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        private void OnEnable()
        {
            library.OnPhotoAdded += HandlePhotoAdded;
            library.OnLibraryLoaded += Refresh;
            if (swipeSurface != null) swipeSurface.WhenPointerEventRaised += HandleSwipe;
            if (leftArrow != null) leftArrow.WhenPointerEventRaised += HandleLeftArrow;
            if (rightArrow != null) rightArrow.WhenPointerEventRaised += HandleRightArrow;
        }

        private void OnDisable()
        {
            library.OnPhotoAdded -= HandlePhotoAdded;
            library.OnLibraryLoaded -= Refresh;
            if (swipeSurface != null) swipeSurface.WhenPointerEventRaised -= HandleSwipe;
            if (leftArrow != null) leftArrow.WhenPointerEventRaised -= HandleLeftArrow;
            if (rightArrow != null) rightArrow.WhenPointerEventRaised -= HandleRightArrow;
        }

        private void Update()
        {
            UpdateShown();

            float target = shown ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, target, scaleSpeed * Time.deltaTime);
            visualRoot.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, visibility);
            visualRoot.gameObject.SetActive(visibility > 0f);
        }

        private void UpdateShown()
        {
            // Never hide while a thumbnail is being pulled, it would vanish from the hand
            if (GalleryThumbnail.AnyHeld)
            {
                return;
            }

            if (wrist == null || head == null || leftHand != null && !leftHand.IsTracked || HandPinchManager.IsProximityPinchActive)
            {
                shown = false;
                return;
            }

            Vector3 palmNormal = wrist.TransformDirection(palmNormalLocal).normalized;
            Vector3 toHead = (head.position - wrist.position).normalized;
            float facing = Vector3.Dot(palmNormal, toHead);

            if (!shown && facing > showThreshold)
            {
                shown = true;
            }
            else if (shown && facing < hideThreshold)
            {
                shown = false;
            }
        }

        private void HandlePhotoAdded(PhotoEntry entry)
        {
            firstIndex = 0;
            Refresh();
        }

        public void Scroll(int delta)
        {
            if (GalleryThumbnail.AnyHeld)
            {
                return;
            }

            int maxFirst = Mathf.Max(0, library.Entries.Count - visibleSlots);
            int newFirst = Mathf.Clamp(firstIndex + delta, 0, maxFirst);
            if (newFirst != firstIndex)
            {
                firstIndex = newFirst;
                Refresh();
            }
        }

        private void Refresh()
        {
            IReadOnlyList<PhotoEntry> entries = library.Entries;
            for (int i = 0; i < thumbnails.Count; i++)
            {
                int index = firstIndex + i;
                thumbnails[i].Bind(index < entries.Count ? entries[index] : null);
            }
        }

        private void HandleSwipe(PointerEvent evt)
        {
            float x = stripRoot.InverseTransformPoint(evt.Pose.position).x;
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    swiping = true;
                    swipeStartX = x;
                    break;
                case PointerEventType.Move when swiping:
                    // Dragging the strip to the left reveals older photos
                    float delta = swipeStartX - x;
                    if (Mathf.Abs(delta) >= slotSpacing)
                    {
                        int steps = (int)(delta / slotSpacing);
                        Scroll(steps);
                        swipeStartX -= steps * slotSpacing;
                    }
                    break;
                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    swiping = false;
                    break;
            }
        }

        private void HandleLeftArrow(PointerEvent evt)
        {
            if (evt.Type != PointerEventType.Select) return;
            Scroll(-1);
            if (leftArrowOutline != null) leftArrowOutline.Activity = 1f;
        }

        private void HandleRightArrow(PointerEvent evt)
        {
            if (evt.Type != PointerEventType.Select) return;
            Scroll(1);
            if (rightArrowOutline != null) rightArrowOutline.Activity = 1f;
        }

        // Backplate behind the strip and arrows with the capture frame's marching-ants border
        private void CreatePanel()
        {
            float halfThumbWidth = (slotSpacing - thumbnailGap) * 0.5f;
            float halfStrip = (visibleSlots - 1) * 0.5f * slotSpacing + halfThumbWidth;
            float minX = stripRoot.localPosition.x - halfStrip;
            float maxX = stripRoot.localPosition.x + halfStrip;
            foreach (PokeInteractable arrow in new[] { leftArrow, rightArrow })
            {
                if (arrow == null) continue;
                float x = visualRoot.InverseTransformPoint(arrow.transform.position).x;
                minX = Mathf.Min(minX, x - buttonSize * 0.5f);
                maxX = Mathf.Max(maxX, x + buttonSize * 0.5f);
            }
            float contentHeight = Mathf.Max(thumbnails.Count > 0 ? thumbnails[0].ThumbnailHeight : 0f, buttonSize);

            FrameOutline panel = FrameOutline.Create(visualRoot, "Panel");
            panel.transform.localPosition = new Vector3((minX + maxX) * 0.5f, stripRoot.localPosition.y, 0.006f); // Behind the content
            panel.Size = new Vector2(maxX - minX + panelMargin * 2f, contentHeight + panelMargin * 2f);
            panel.Thickness = 0.002f;
            panel.CornerRadius = 0.01f;
            panel.Padding = 0.01f;
            panel.DashLength = 0.012f;
            panel.FillStrength = 2f;
        }

        // Rounded outline behind the arrow icon, matching the thumbnails
        private FrameOutline StyleArrow(PokeInteractable arrow)
        {
            if (arrow == null) return null;

            FrameOutline outline = FrameOutline.Create(arrow.transform, "Outline");
            outline.transform.localPosition = new Vector3(0f, 0f, -0.003f); // Between the icon and the panel
            outline.Size = Vector2.one * buttonSize;
            outline.Thickness = 0.0015f;
            outline.CornerRadius = 0.006f;
            outline.Padding = 0.008f;
            outline.FillStrength = 2f;
            outline.Dashed = false;
            return outline;
        }
    }
}
