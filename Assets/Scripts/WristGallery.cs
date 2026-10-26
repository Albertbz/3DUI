using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Strip of photo thumbnails on the left wrist that appears when the palm faces the head.
/// Place this object as a child of the left wrist/hand anchor.
/// </summary>
public class WristGallery : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private Transform wrist;             // Usually LeftHandAnchor
    [SerializeField] private Transform head;              // CenterEyeAnchor
    [SerializeField] private Transform visualRoot;        // Scaled in/out when shown/hidden
    [SerializeField] private Transform stripRoot;         // Parent of the thumbnail slots
    [SerializeField] private PhotoLibrary library;
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

    private void Awake()
    {
        int captureIgnoreLayer = LayerMask.NameToLayer("CaptureIgnore");

        for (int i = 0; i < visibleSlots; i++)
        {
            var slot = new GameObject($"Slot {i}").transform;
            slot.SetParent(stripRoot, false);
            slot.localPosition = new Vector3((i - (visibleSlots - 1) * 0.5f) * slotSpacing, 0f, 0f);

            GalleryThumbnail thumbnail = Instantiate(thumbnailPrefab, slot, false);
            thumbnail.Init(library, photoPrefab, slotSpacing - thumbnailGap);
            thumbnails.Add(thumbnail);
        }

        if (captureIgnoreLayer >= 0)
        {
            SetLayerRecursively(transform, captureIgnoreLayer);
        }

        visualRoot.localScale = Vector3.zero;
        visualRoot.gameObject.SetActive(false);
        Refresh();
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

        if (leftHand != null && !leftHand.IsTracked || HandPinchManager.IsProximityPinchActive)
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
        if (evt.Type == PointerEventType.Select) Scroll(-1);
    }

    private void HandleRightArrow(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select) Scroll(1);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }
}
