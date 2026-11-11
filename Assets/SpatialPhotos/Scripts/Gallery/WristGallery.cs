using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Strip of photo thumbnails that floats above the left hand when the palm opens upward, lying on the palm
    /// like a tray and tilting with it, with both ends equally far from the eyes. Drop the WristGallery prefab
    /// anywhere in the scene: at startup it finds the OVRCameraRig's left hand anchor, hand, head and the photo
    /// library.
    /// </summary>
    public class WristGallery : MonoBehaviour
    {
        [Header("References (found at startup when empty)")]
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private Transform wrist;             // LeftHandAnchor; the gallery floats above it
        [SerializeField] private Transform head;              // CenterEyeAnchor
        [SerializeField] private Transform visualRoot;        // Scaled in/out when shown/hidden
        [SerializeField] private Transform stripRoot;         // Parent of the thumbnail slots
        [SerializeField] private PhotoLibrary library;
        [SerializeField] private DominantEyeCalibration calibration; // Run again by the Eye button

        [Header("Prefabs")]
        [SerializeField] private GalleryThumbnail thumbnailPrefab;
        [SerializeField] private PhotoObject photoPrefab;

        [Header("Scrolling")]
        [SerializeField] private PokeInteractable swipeSurface; // Optional: poke and drag along the strip
        [Tooltip("How close a fingertip must come to the strip before poking it. Each hand hovers one interactor at a time and poke goes first, so a large zone would block pinching a thumbnail.")]
        [SerializeField] private float swipeHoverDistance = 0.02f;
        [Tooltip("Pinch strength at which a hand near the strip stops poking it, so the pinch can grab a thumbnail instead")]
        [SerializeField] private float pinchTakeoverStrength = 0.3f;
        [SerializeField] private float pinchTakeoverReach = 0.1f;   // Index tip distance from the strip
        [SerializeField] private PokeInteractable leftArrow;    // Optional
        [SerializeField] private PokeInteractable rightArrow;   // Optional
        [SerializeField] private float scrollSmoothing = 12f;   // How quickly the strip settles after a swipe or arrow press

        [Header("Layout")]
        [SerializeField] private int visibleSlots = 5;
        [SerializeField] private float slotSpacing = 0.065f;
        [SerializeField] private float thumbnailGap = 0.01f;  // Minimum space between neighbouring thumbnails

        [Header("Look (shares the capture frame's marching-ants outline)")]
        [SerializeField] private float panelMargin = 0.012f;
        [SerializeField] private float buttonSize = 0.025f;

        [Header("Delete confirmation (pops up above the photo)")]
        [SerializeField] private Vector2 confirmButtonSize = new Vector2(0.07f, 0.022f);
        [SerializeField] private float confirmGap = 0.008f;      // Between the gallery panel and the popup
        [SerializeField] private float labelHeight = 0.006f;

        [Header("Dominant eye button (below the gallery, at its right end)")]
        [SerializeField] private Vector2 eyeButtonSize = new Vector2(0.045f, 0.018f);
        [SerializeField] private float eyeButtonGap = 0.008f;    // Between the gallery panel and the button

        [Header("Placement above the palm")]
        [Tooltip("Interaction SDK hand whose palm joint the gallery floats above; the rig's left hand when empty")]
        [SerializeField, Interface(typeof(IHand))] private Object palmHand;
        [Tooltip("Palm centre in the wrist transform's local space, used only while there is no hand joint data")]
        [SerializeField] private Vector3 palmCenterLocal = new Vector3(0f, 0f, 0.06f);
        [SerializeField] private float heightAbovePalm = 0.12f;     // To the middle of the thumbnail strip
        [Tooltip("Space between the palm centre and the gallery's left edge, toward the viewer's right, so the hand doesn't cover what you're selecting")]
        [SerializeField] private float gapRightOfPalm = 0.04f;
        [SerializeField] private float followSmoothing = 25f;      // Higher follows the hand more tightly

        [Header("Open-palm detection")]
        [Tooltip("Palm normal in the wrist transform's local space. Tune this in play mode if the gallery shows on the wrong side.")]
        [SerializeField] private Vector3 palmNormalLocal = Vector3.down;
        [Tooltip("Direction the fingers point in the wrist transform's local space; used only when the eyes are straight above the palm")]
        [SerializeField] private Vector3 fingersLocal = Vector3.forward;
        [Tooltip("How far the palm must face up (dot with world up) to show the gallery, and below which it hides again")]
        [SerializeField] private float showThreshold = 0.7f;
        [SerializeField] private float hideThreshold = 0.5f;
        [SerializeField] private float scaleSpeed = 1f / 0.15f;

        private IHand palm;
        private IHand leftData;
        private IHand rightData;
        private bool pokesEnabled = true;
        private readonly List<GalleryThumbnail> thumbnails = new List<GalleryThumbnail>();
        private int firstIndex;
        private float scrollPosition; // In slots: 1.5 means halfway between the 2nd and 3rd photo at the left edge
        private float scrollTarget;
        private bool shown;
        private float visibility;
        private float swipeStartX;
        private float swipeStartScroll;
        private bool swiping;
        private FrameOutline leftArrowOutline;
        private FrameOutline rightArrowOutline;
        private float panelMinX;
        private float panelMaxX;
        private float panelTop;
        private float panelBottom;
        private TextMesh eyeLabel;
        private Transform confirmPopup;
        private float confirmPopupWidth;
        private PhotoEntry pendingDelete;

        private void Awake()
        {
            if (library == null) library = FindFirstObjectByType<PhotoLibrary>();

            // One slot more than is visible, so a photo can slide in while another slides out
            for (int i = 0; i <= visibleSlots; i++)
            {
                var slot = new GameObject($"Slot {i}").transform;
                slot.SetParent(stripRoot, false);

                GalleryThumbnail thumbnail = Instantiate(thumbnailPrefab, slot, false);
                thumbnail.Init(library, photoPrefab, slotSpacing - thumbnailGap);
                thumbnail.DeleteRequested += RequestDelete;
                // Pinching a thumbnail and moving sideways scrolls like a swipe
                thumbnail.ScrollDragStarted += BeginDrag;
                thumbnail.ScrollDragMoved += delta => Drag(-delta);
                thumbnail.ScrollDragEnded += EndDrag;
                thumbnails.Add(thumbnail);
            }

            CreatePanel();
            CreateConfirmPopup();
            CreateEyeButton();
            leftArrowOutline = StyleArrow(leftArrow);
            rightArrowOutline = StyleArrow(rightArrow);

            if (swipeSurface != null)
            {
                swipeSurface.EnterHoverNormal = swipeHoverDistance;
                swipeSurface.ExitHoverNormal = swipeHoverDistance * 2f;
            }

            CaptureLayers.Apply(gameObject, CaptureLayers.CaptureIgnore);

            visualRoot.localScale = Vector3.zero;
            visualRoot.gameObject.SetActive(false);
            Refresh();
            LayoutSlots();
        }

        private void Start()
        {
            if (wrist == null) wrist = XRRig.HandAnchor(left: true);
            if (head == null) head = XRRig.Head;
            if (leftHand == null) leftHand = XRRig.Hand(left: true);
            if (calibration == null) calibration = FindFirstObjectByType<DominantEyeCalibration>();
            if (wrist == null || head == null)
            {
                XRRig.Warn(this, "the left hand anchor and head");
                return;
            }

            // Placed in world space every frame, relative to the hand
            transform.SetParent(null, true);
        }

        private void LateUpdate()
        {
            if (wrist == null || head == null)
            {
                return;
            }

            // Lying on the hand like a tray: visualRoot's -Z (the side people look at) faces out of the palm. Within
            // the palm's plane it is turned so its top points away from the eyes, keeping both ends equally far
            // away however the fingers point; turning the head alone doesn't move it
            Vector3 palmCenter = PalmCenter();
            Vector3 palmNormal = wrist.TransformDirection(palmNormalLocal).normalized;
            Quaternion rotation = wrist.rotation * Quaternion.LookRotation(-palmNormalLocal, fingersLocal);
            // Where the panel's middle lands depends on the turn, so settle it in a few passes
            for (int i = 0; i < 3; i++)
            {
                Vector3 panelMiddle = TrayTarget(palmCenter, rotation) + rotation * Vector3.right * ((panelMinX + panelMaxX) * 0.5f);
                Vector3 away = Vector3.ProjectOnPlane(panelMiddle - head.position, palmNormal);
                if (away.sqrMagnitude < 1e-6f) break;
                rotation = Quaternion.LookRotation(-palmNormal, away);
            }
            Vector3 target = TrayTarget(palmCenter, rotation);

            // Snap into place when appearing, then follow smoothly to hide tracking jitter
            if (visibility <= 0f)
            {
                transform.SetPositionAndRotation(target, rotation);
                return;
            }
            float t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, target, t), Quaternion.Slerp(transform.rotation, rotation, t));
        }

        // Above the palm centre along the palm normal; the panel's left edge sits Gap Right Of Palm from the palm
        // centre, toward the hand's thumb side
        private Vector3 TrayTarget(Vector3 palmCenter, Quaternion rotation)
        {
            return palmCenter + rotation * new Vector3(gapRightOfPalm - panelMinX, 0f, -heightAbovePalm);
        }

        // Each hand hovers one interactor at a time and poke goes first, so a finger touching the strip would keep
        // the pinch from grabbing a thumbnail. Once a hand near the strip starts to pinch, switch the strip's pokes
        // off (cancelling a swipe in progress) until the pinch and any grab end
        private void UpdatePinchTakeover()
        {
            leftData ??= FindHand(Handedness.Left);
            rightData ??= FindHand(Handedness.Right);
            bool pinchNear = PinchingNear(leftData) || PinchingNear(rightData);
            SetPokesEnabled(!pinchNear && !GalleryThumbnail.AnyHeld);
        }

        private bool PinchingNear(IHand hand)
        {
            return hand != null
                && hand.GetFingerPinchStrength(HandFinger.Index) >= pinchTakeoverStrength
                && hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip)
                && Vector3.Distance(tip.position, stripRoot.position) < pinchTakeoverReach + (visibleSlots * 0.5f) * slotSpacing
                && Mathf.Abs(stripRoot.InverseTransformPoint(tip.position).y) < pinchTakeoverReach;
        }

        private void SetPokesEnabled(bool enabled)
        {
            if (enabled == pokesEnabled) return;
            pokesEnabled = enabled;
            if (swipeSurface != null) swipeSurface.enabled = enabled;
            foreach (GalleryThumbnail thumbnail in thumbnails)
            {
                thumbnail.SetPokeEnabled(enabled);
            }
        }

        // The tracked palm joint, so the gallery sits over the middle of the hand whatever the anchor's origin is
        private Vector3 PalmCenter()
        {
            palm ??= palmHand as IHand ?? FindHand(Handedness.Left);
            if (palm != null && palm.GetJointPose(HandJointId.HandPalm, out Pose palmPose))
            {
                return palmPose.position;
            }
            return wrist.TransformPoint(palmCenterLocal);
        }

        // A tracked hand, not a synthetic one: those are pinned to whatever the hand is grabbing
        private static IHand FindHand(Handedness handedness)
        {
            foreach (Hand hand in FindObjectsByType<Hand>(FindObjectsSortMode.None))
            {
                if (!(hand is SyntheticHand) && hand.IsTrackedDataValid && hand.Handedness == handedness)
                {
                    return hand;
                }
            }
            return null;
        }

        private void OnEnable()
        {
            library.OnPhotoAdded += HandlePhotoAdded;
            library.OnLibraryLoaded += Refresh;
            library.OnPhotoRemoved += HandlePhotoRemoved;
            if (swipeSurface != null) swipeSurface.WhenPointerEventRaised += HandleSwipe;
            if (leftArrow != null) leftArrow.WhenPointerEventRaised += HandleLeftArrow;
            if (rightArrow != null) rightArrow.WhenPointerEventRaised += HandleRightArrow;
            DominantEye.Changed += UpdateEyeLabel;
            UpdateEyeLabel();
        }

        private void OnDisable()
        {
            library.OnPhotoAdded -= HandlePhotoAdded;
            library.OnLibraryLoaded -= Refresh;
            library.OnPhotoRemoved -= HandlePhotoRemoved;
            if (swipeSurface != null) swipeSurface.WhenPointerEventRaised -= HandleSwipe;
            if (leftArrow != null) leftArrow.WhenPointerEventRaised -= HandleLeftArrow;
            if (rightArrow != null) rightArrow.WhenPointerEventRaised -= HandleRightArrow;
            DominantEye.Changed -= UpdateEyeLabel;
        }

        private void Update()
        {
            UpdateShown();
            UpdatePinchTakeover();
            if (!shown && pendingDelete != null)
            {
                CancelDelete();
            }

            float target = shown ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, target, scaleSpeed * Time.deltaTime);
            visualRoot.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, visibility);
            visualRoot.gameObject.SetActive(visibility > 0f);

            if (!swiping && !GalleryThumbnail.AnyHeld)
            {
                scrollPosition = Mathf.Lerp(scrollPosition, scrollTarget, 1f - Mathf.Exp(-scrollSmoothing * Time.deltaTime));
                if (Mathf.Abs(scrollPosition - scrollTarget) < 0.001f)
                {
                    scrollPosition = scrollTarget;
                }
            }
            ApplyScroll();
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

            // An open palm facing up, as if holding the gallery
            Vector3 palmNormal = wrist.TransformDirection(palmNormalLocal).normalized;
            float facing = Vector3.Dot(palmNormal, Vector3.up);

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
            scrollPosition = scrollTarget = 0f;
            ApplyScroll(force: true);
        }

        private void HandlePhotoRemoved(PhotoEntry entry)
        {
            if (entry == pendingDelete)
            {
                pendingDelete = null;
            }
            scrollTarget = Mathf.Clamp(scrollTarget, 0f, MaxScroll);
            scrollPosition = Mathf.Clamp(scrollPosition, 0f, MaxScroll);
            ApplyScroll(force: true);
        }

        private float MaxScroll => Mathf.Max(0, library.Entries.Count - visibleSlots);

        public void Scroll(int delta)
        {
            if (GalleryThumbnail.AnyHeld)
            {
                return;
            }

            scrollTarget = Mathf.Clamp(Mathf.Round(scrollTarget) + delta, 0f, MaxScroll);
        }

        // Rebinds the slots when the strip has moved past a whole photo, then slides them by the remainder
        private void ApplyScroll(bool force = false)
        {
            int first = Mathf.FloorToInt(scrollPosition);
            if (force || first != firstIndex)
            {
                firstIndex = first;
                Refresh();
            }
            LayoutSlots();
            if (pendingDelete != null)
            {
                UpdateConfirmPopup();
            }
        }

        private void LayoutSlots()
        {
            float offset = scrollPosition - firstIndex;
            float edge = (visibleSlots - 1) * 0.5f * slotSpacing;
            for (int i = 0; i < thumbnails.Count; i++)
            {
                float x = (i - offset) * slotSpacing - edge;
                thumbnails[i].transform.parent.localPosition = new Vector3(x, 0f, 0f);

                // Fade out over half a slot past the outermost positions, before reaching the arrows
                float overshoot = Mathf.Max(0f, Mathf.Abs(x) - edge);
                thumbnails[i].SetFade(1f - Mathf.SmoothStep(0f, 1f, overshoot / (slotSpacing * 0.5f)));
            }
        }

        private void Refresh()
        {
            IReadOnlyList<PhotoEntry> entries = library.Entries;
            for (int i = 0; i < thumbnails.Count; i++)
            {
                int index = firstIndex + i;
                thumbnails[i].Bind(index < entries.Count ? entries[index] : null);
                thumbnails[i].MarkedForDeletion = pendingDelete != null && thumbnails[i].Entry == pendingDelete;
            }
            UpdateConfirmPopup();
        }

        private void RequestDelete(GalleryThumbnail thumbnail)
        {
            if (thumbnail.Entry == null || GalleryThumbnail.AnyHeld)
            {
                return;
            }
            pendingDelete = thumbnail.Entry;
            Refresh();
        }

        public void ConfirmDelete()
        {
            PhotoEntry entry = pendingDelete;
            pendingDelete = null;
            if (entry != null)
            {
                library.Delete(entry); // Refreshes through OnPhotoRemoved
            }
            Refresh();
        }

        public void CancelDelete()
        {
            pendingDelete = null;
            Refresh();
        }

        // Shows the popup above the photo waiting for confirmation, or hides it; a photo scrolled out of view is
        // no longer pending, since it's unclear which photo the popup would be about
        private void UpdateConfirmPopup()
        {
            GalleryThumbnail target = pendingDelete == null ? null : thumbnails.Find(t => t.Entry == pendingDelete);
            if (target == null || target.Fade < 0.5f)
            {
                pendingDelete = null;
                if (target != null) target.MarkedForDeletion = false;
                confirmPopup.gameObject.SetActive(false);
                return;
            }

            // Centered over the photo, but kept within the gallery's width
            float x = visualRoot.InverseTransformPoint(target.transform.parent.position).x;
            float halfWidth = confirmPopupWidth * 0.5f;
            x = panelMaxX - panelMinX > confirmPopupWidth
                ? Mathf.Clamp(x, panelMinX + halfWidth, panelMaxX - halfWidth)
                : (panelMinX + panelMaxX) * 0.5f;
            Vector3 position = confirmPopup.localPosition;
            position.x = x;
            confirmPopup.localPosition = position;
            confirmPopup.gameObject.SetActive(true);
        }

        private void HandleSwipe(PointerEvent evt)
        {
            float x = stripRoot.InverseTransformPoint(evt.Pose.position).x;
            switch (evt.Type)
            {
                case PointerEventType.Select when !GalleryThumbnail.AnyHeld && !swiping:
                    swipeStartX = x;
                    BeginDrag();
                    break;
                case PointerEventType.Move when swiping:
                    Drag(swipeStartX - x);
                    break;
                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    EndDrag();
                    break;
            }
        }

        // Shared by poke swipes and sideways pinches on a thumbnail
        private void BeginDrag()
        {
            swiping = true;
            swipeStartScroll = scrollPosition;
        }

        /// <param name="leftward">How far the finger has moved to the left since the drag began, in meters.</param>
        private void Drag(float leftward)
        {
            if (!swiping) return;
            // The strip follows the finger; dragging it to the left reveals older photos
            scrollPosition = Mathf.Clamp(swipeStartScroll + leftward / slotSpacing, 0f, MaxScroll);
            scrollTarget = scrollPosition;
        }

        private void EndDrag()
        {
            if (!swiping) return;
            swiping = false;
            scrollTarget = Mathf.Round(scrollPosition); // Settle on the nearest photo
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

            panelMinX = minX - panelMargin;
            panelMaxX = maxX + panelMargin;
            panelTop = stripRoot.localPosition.y + contentHeight * 0.5f + panelMargin;
            panelBottom = stripRoot.localPosition.y - contentHeight * 0.5f - panelMargin;

            FrameOutline panel = FrameOutline.Create(visualRoot, "Panel");
            panel.transform.localPosition = new Vector3((minX + maxX) * 0.5f, stripRoot.localPosition.y, 0.006f); // Behind the content
            panel.Size = new Vector2(maxX - minX + panelMargin * 2f, contentHeight + panelMargin * 2f);
            panel.Thickness = 0.002f;
            panel.CornerRadius = 0.01f;
            panel.Padding = 0.01f;
            panel.DashLength = 0.012f;
            panel.FillStrength = 2f;
        }

        // "Delete photo?" with Confirm / Cancel, on its own marching-ants panel above the gallery
        private void CreateConfirmPopup()
        {
            const float spacing = 0.008f;
            float titleHeight = labelHeight * 2.5f;
            confirmPopupWidth = confirmButtonSize.x * 2f + spacing + panelMargin * 2f;
            float height = titleHeight + confirmButtonSize.y + panelMargin * 2f;

            confirmPopup = new GameObject("DeleteConfirmation").transform;
            confirmPopup.gameObject.layer = gameObject.layer;
            confirmPopup.SetParent(visualRoot, false);
            confirmPopup.localPosition = new Vector3(0f, panelTop + confirmGap + height * 0.5f, 0f);

            FrameOutline panel = FrameOutline.Create(confirmPopup, "Panel");
            panel.transform.localPosition = new Vector3(0f, 0f, 0.006f); // Behind the buttons, like the gallery panel
            panel.Size = new Vector2(confirmPopupWidth, height);
            panel.Thickness = 0.002f;
            panel.CornerRadius = 0.01f;
            panel.Padding = 0.01f;
            panel.DashLength = 0.012f;
            panel.FillStrength = 2f;
            panel.Valid = 0f; // Warning tint: this can't be undone

            TextMesh title = PokeButton.CreateLabel(confirmPopup, "Delete photo?", labelHeight);
            title.transform.localPosition = new Vector3(0f, height * 0.5f - panelMargin - titleHeight * 0.5f, -0.001f);

            float buttonY = -height * 0.5f + panelMargin + confirmButtonSize.y * 0.5f;
            float buttonX = (confirmButtonSize.x + spacing) * 0.5f;

            PokeButton confirm = PokeButton.Create(confirmPopup, "ConfirmButton", confirmButtonSize, 0.006f);
            confirm.transform.localPosition = new Vector3(-buttonX, buttonY, 0f);
            confirm.Outline.Valid = 0f;
            confirm.AddLabel("Confirm", labelHeight).color = FrameOutline.WarningColor;
            confirm.Pressed += ConfirmDelete;

            PokeButton cancel = PokeButton.Create(confirmPopup, "CancelButton", confirmButtonSize, 0.006f);
            cancel.transform.localPosition = new Vector3(buttonX, buttonY, 0f);
            cancel.AddLabel("Cancel", labelHeight);
            cancel.Pressed += CancelDelete;

            confirmPopup.gameObject.SetActive(false);
        }

        // Shows which eye photos are taken from; poking it runs the dominant eye calibration again
        private void CreateEyeButton()
        {
            PokeButton button = PokeButton.Create(visualRoot, "DominantEyeButton", eyeButtonSize, 0.006f);
            button.transform.localPosition = new Vector3(
                panelMaxX - eyeButtonSize.x * 0.5f, panelBottom - eyeButtonGap - eyeButtonSize.y * 0.5f, 0f);
            eyeLabel = button.AddLabel("", labelHeight);
            button.Pressed += () =>
            {
                if (calibration != null) calibration.Begin();
            };
        }

        private void UpdateEyeLabel()
        {
            if (eyeLabel == null) return;
            eyeLabel.text = !DominantEye.HasValue ? "Eye: ?" : DominantEye.Current == Eye.Left ? "Eye: L" : "Eye: R";
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
