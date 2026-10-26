using System.Collections;
using Oculus.Interaction;
using UnityEngine;

public class PhotoObject : MonoBehaviour
{
    [SerializeField] private Renderer photoRenderer;      // Unit quad, scaled to the photo size
    [SerializeField] private BoxCollider grabCollider;
    [SerializeField] private Rigidbody body;              // Kinematic while floating
    [SerializeField] private PointableElement grabbable;  // Grabbable on this object

    [Header("Outline")]
    [SerializeField] private float outlineGap = 0.004f;       // Space between the photo and its rim
    [SerializeField] private float outlineThickness = 0.0025f;

    [Header("Appear")]
    [SerializeField] private float flashDuration = 0.4f;
    [SerializeField] private float growDuration = 0.25f;

    [Header("Throw to dismiss")]
    [SerializeField] private float throwDeleteSpeed = 1.5f; // m/s
    [SerializeField] private float dismissDuration = 1.5f;
    [SerializeField] private float destroyBelowY = -2f;

    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private static int heldCount;
    public static bool AnyHeld => heldCount > 0;

    public PhotoEntry Entry { get; private set; }
    public bool IsHeld { get; private set; }

    private Material material;
    private Texture ownedTexture;
    private Vector2 size;
    private bool dismissing;
    private FrameOutline outline;
    private int hoverCount;

    // Recent positions while held, used to estimate release velocity
    private const int VelocitySamples = 5;
    private readonly Vector3[] samplePositions = new Vector3[VelocitySamples];
    private readonly float[] sampleTimes = new float[VelocitySamples];
    private int sampleIndex;
    private int sampleCount;

    private void Awake()
    {
        material = photoRenderer.material; // Instance, so each photo has its own texture and flash
        body.isKinematic = true;
        body.useGravity = false;

        // Same outline as the capture frame and gallery, so the photo reads as an object in the room:
        // a solid rim at rest, marching ants while hovered or held
        outline = FrameOutline.Create(transform, "Outline");
        outline.transform.localPosition = new Vector3(0f, 0f, 0.001f); // Just behind the photo
        outline.Thickness = outlineThickness;
        outline.CornerRadius = 0.006f;
        outline.Padding = 0.015f;
        outline.DashLength = 0.015f;
        outline.Dashed = false;
    }

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
        hoverCount = 0;
        SetHeld(false);
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
        ReleaseOwnedTexture();
    }

    /// <summary>
    /// Shows the given texture at the entry's physical size. When <paramref name="ownsTexture"/> is true
    /// the photo releases the texture when destroyed.
    /// </summary>
    public void Init(PhotoEntry entry, Texture texture, bool flash, bool ownsTexture = true)
    {
        Entry = entry;
        ownedTexture = ownsTexture ? texture : null;
        material.SetTexture(MainTexId, texture);
        material.SetFloat(AlphaId, 1f);
        material.SetFloat(FlashAmountId, flash ? 1f : 0f);
        SetSize(new Vector2(entry.WidthM, entry.HeightM));

        if (flash)
        {
            outline.Activity = 1f;
            StartCoroutine(FlashRoutine());
        }
    }

    /// <summary>Animates the photo from <paramref name="startSize"/> up to its full size.</summary>
    public void GrowFrom(Vector2 startSize)
    {
        StartCoroutine(GrowRoutine(startSize, new Vector2(Entry.WidthM, Entry.HeightM)));
    }

    private void SetSize(Vector2 newSize)
    {
        size = newSize;
        photoRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        grabCollider.size = new Vector3(size.x, size.y, 0.01f);
        outline.Size = size + Vector2.one * (outlineGap * 2f);
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (dismissing)
        {
            return;
        }

        switch (evt.Type)
        {
            case PointerEventType.Hover:
                hoverCount++;
                break;
            case PointerEventType.Unhover:
                hoverCount = Mathf.Max(0, hoverCount - 1);
                break;
            case PointerEventType.Select:
                if (!IsHeld)
                {
                    SetHeld(true);
                    sampleCount = 0;
                }
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                if (IsHeld && grabbable.SelectingPointsCount == 0)
                {
                    SetHeld(false);
                    HandleRelease();
                }
                break;
        }
    }

    /// <summary>Marks the photo as held by something other than its own Grabbable (e.g. a gallery thumbnail).</summary>
    public void BeginExternalHold()
    {
        SetHeld(true);
        sampleCount = 0;
    }

    /// <summary>Ends an external hold and applies the same release/throw logic as a normal grab.</summary>
    public void EndExternalHold()
    {
        if (IsHeld && !dismissing)
        {
            SetHeld(false);
            HandleRelease();
        }
    }

    private void SetHeld(bool held)
    {
        if (held == IsHeld)
        {
            return;
        }
        IsHeld = held;
        heldCount += held ? 1 : -1;
    }

    private void Update()
    {
        outline.Dashed = !dismissing && (IsHeld || hoverCount > 0);
        if (IsHeld)
        {
            outline.Activity = 1f;
            samplePositions[sampleIndex] = transform.position;
            sampleTimes[sampleIndex] = Time.time;
            sampleIndex = (sampleIndex + 1) % VelocitySamples;
            sampleCount = Mathf.Min(sampleCount + 1, VelocitySamples);
        }

        if (transform.position.y < destroyBelowY)
        {
            Destroy(gameObject);
        }
    }

    private Vector3 EstimateVelocity()
    {
        if (sampleCount < 2)
        {
            return Vector3.zero;
        }

        int newest = (sampleIndex - 1 + VelocitySamples) % VelocitySamples;
        int oldest = (sampleIndex - sampleCount + VelocitySamples) % VelocitySamples;
        float dt = sampleTimes[newest] - sampleTimes[oldest];
        return dt > 0f ? (samplePositions[newest] - samplePositions[oldest]) / dt : Vector3.zero;
    }

    private void HandleRelease()
    {
        Vector3 velocity = EstimateVelocity();
        if (velocity.magnitude > throwDeleteSpeed)
        {
            StartCoroutine(DismissRoutine(velocity));
        }
        else
        {
            // Stay floating where it was let go
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private IEnumerator DismissRoutine(Vector3 velocity)
    {
        dismissing = true;
        if (grabbable != null)
        {
            grabbable.enabled = false;
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = velocity;

        // Wait a moment, then shrink and fade out
        float fadeDuration = dismissDuration * 0.5f;
        yield return new WaitForSeconds(dismissDuration - fadeDuration);

        Vector3 startScale = transform.localScale;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            float k = t / fadeDuration;
            transform.localScale = startScale * (1f - k);
            material.SetFloat(AlphaId, 1f - k);
            outline.Appear = 1f - k;
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator FlashRoutine()
    {
        for (float t = 0f; t < flashDuration; t += Time.deltaTime)
        {
            material.SetFloat(FlashAmountId, 1f - t / flashDuration);
            yield return null;
        }
        material.SetFloat(FlashAmountId, 0f);
    }

    private IEnumerator GrowRoutine(Vector2 from, Vector2 to)
    {
        for (float t = 0f; t < growDuration; t += Time.deltaTime)
        {
            SetSize(Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / growDuration)));
            yield return null;
        }
        SetSize(to);
    }

    private void ReleaseOwnedTexture()
    {
        if (ownedTexture is RenderTexture rt)
        {
            rt.Release();
            Destroy(rt);
        }
        else if (ownedTexture != null)
        {
            Destroy(ownedTexture);
        }
        ownedTexture = null;
    }
}
