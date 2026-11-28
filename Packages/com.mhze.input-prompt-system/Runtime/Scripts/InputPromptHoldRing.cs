// A ring around the centre of the screen that fills while the player holds a button, showing how much of the hold is left. It pops in when the hold starts, bursts outward when the hold completes and drains away when the key is let go early. Builds its own visuals (outline, track, fill, rounded caps) from generated textures, so it needs no art. Look and motion come from an InputPromptHoldRingProfile.

using UnityEngine;
using UnityEngine.UI;

namespace MHZE.InputPromptSystem
{
/// <summary>
/// Hold-to-interact progress ring. Call <see cref="Begin"/> when a hold starts; the fill then advances on its own
/// with game time (or follow the true value with <see cref="SetProgress"/>). End with <see cref="Complete"/> or
/// <see cref="Cancel"/>. <see cref="InputPromptManager"/> exposes it as ShowHoldProgress and friends and creates
/// one at the centre of the prompt canvas when none is assigned.
/// </summary>
[AddComponentMenu("Input Prompts/Input Prompt Hold Ring")]
[RequireComponent(typeof(RectTransform))]
public class InputPromptHoldRing : MonoBehaviour
{
    private const float TexelsPerUnit = 3f;
    private const float CapTextureRadius = 15f;

    private enum Phase
    {
        Hidden,
        Filling,
        Completing,
        Cancelling
    }

    [Tooltip("Look and motion. Empty: the InputPromptHoldRingProfile asset in a Resources folder, or the built-in defaults.")]
    [SerializeField] private InputPromptHoldRingProfile profile;

    private InputPromptHoldRingProfile active;
    private Phase phase = Phase.Hidden;
    private float duration;
    private float progress;
    private bool driven;
    private float phaseTime;
    private float endFromScale = 1f;
    private float endFromAlpha = 1f;
    private float endFromProgress;

    private RectTransform rect;
    private RectTransform body;
    private CanvasGroup group;
    private Image outline;
    private Image track;
    private Image fill;
    private Image startCap;
    private Image headCap;
    private Image startCapOutline;
    private Image headCapOutline;

    private Texture2D ringTexture;
    private Texture2D outlineTexture;
    private Sprite ringSprite;
    private Sprite outlineSprite;
    private float builtDiameter = -1f;
    private float builtThickness = -1f;
    private float builtOutline = -1f;

    private static Texture2D capTexture;
    private static Sprite capSprite;

    /// <summary>True while the ring is visible (filling or playing its end motion).</summary>
    public bool IsShowing => phase != Phase.Hidden;

    /// <summary>True while a hold is in progress.</summary>
    public bool IsFilling => phase == Phase.Filling;

    /// <summary>Shown share of the hold, 0 to 1.</summary>
    public float Progress => progress;

    public InputPromptHoldRingProfile Profile
    {
        get => profile;
        set => profile = value;
    }

    private void Awake()
    {
        EnsureBuilt();
        HideImmediate();
    }

    private void OnDisable()
    {
        if (phase != Phase.Hidden)
        {
            HideImmediate();
        }
    }

    private void OnDestroy()
    {
        DestroyGenerated(ref ringSprite);
        DestroyGenerated(ref outlineSprite);
        DestroyGenerated(ref ringTexture);
        DestroyGenerated(ref outlineTexture);
    }

    /// <summary>Starts a hold of <paramref name="holdSeconds"/>: the ring pops in empty and fills over that time.</summary>
    public void Begin(float holdSeconds)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        active = profile != null ? profile : InputPromptHoldRingProfile.LoadDefault();
        EnsureBuilt();
        ApplyLook();

        duration = Mathf.Max(0f, holdSeconds);
        progress = duration <= 0f ? 1f : 0f;
        driven = false;
        phase = Phase.Filling;
        phaseTime = 0f;

        body.gameObject.SetActive(true);
        transform.SetAsLastSibling();
        UpdateVisuals();
    }

    /// <summary>Sets the shown progress (0 to 1) directly. After the first call the ring stops advancing on its own.</summary>
    public void SetProgress(float normalized)
    {
        if (phase != Phase.Filling)
        {
            return;
        }

        driven = true;
        progress = Mathf.Clamp01(normalized);
    }

    /// <summary>The hold finished: the ring fills up and bursts outward.</summary>
    public void Complete()
    {
        if (phase == Phase.Hidden || phase == Phase.Completing)
        {
            return;
        }

        CaptureEndStart();
        progress = 1f;
        phase = Phase.Completing;
        phaseTime = 0f;
        UpdateVisuals();
    }

    /// <summary>The hold was let go or interrupted: the ring drains and shrinks away.</summary>
    public void Cancel()
    {
        if (phase != Phase.Filling)
        {
            return;
        }

        CaptureEndStart();
        endFromProgress = progress;
        phase = Phase.Cancelling;
        phaseTime = 0f;
        UpdateVisuals();
    }

    /// <summary>Hides the ring at once, without its end motion.</summary>
    public void HideImmediate()
    {
        phase = Phase.Hidden;
        progress = 0f;
        if (body != null)
        {
            body.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (phase == Phase.Hidden)
        {
            return;
        }

        // The fill follows game time like the hold timer it shows; the pop and fades use real time so they
        // finish even when the completed interaction pauses the game.
        if (phase == Phase.Filling && !driven && duration > 0f)
        {
            progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
        }

        phaseTime += Time.unscaledDeltaTime;
        UpdateVisuals();
    }

    private void CaptureEndStart()
    {
        EvaluateFilling(out endFromScale, out endFromAlpha);
        endFromProgress = progress;
    }

    private void EvaluateFilling(out float scale, out float alpha)
    {
        var p = Active;
        var t = p.appearDuration <= 0f ? 1f : Mathf.Clamp01(phaseTime / p.appearDuration);
        var y = p.appearCurve != null ? p.appearCurve.Evaluate(t) : t;
        scale = Mathf.LerpUnclamped(p.appearStartScale, 1f, y);
        alpha = Mathf.Clamp01(y);
    }

    private void UpdateVisuals()
    {
        if (body == null)
        {
            return;
        }

        var p = Active;
        float scale;
        float alpha;
        var shown = progress;

        switch (phase)
        {
            case Phase.Filling:
                EvaluateFilling(out scale, out alpha);
                break;

            case Phase.Completing:
            {
                var t = p.completeDuration <= 0f ? 1f : Mathf.Clamp01(phaseTime / p.completeDuration);
                var y = p.completeCurve != null ? p.completeCurve.Evaluate(t) : t;
                scale = Mathf.LerpUnclamped(endFromScale, endFromScale * p.completeScale, y);
                alpha = endFromAlpha * (1f - Mathf.Clamp01(y));
                shown = 1f;
                if (t >= 1f)
                {
                    HideImmediate();
                    return;
                }

                break;
            }

            case Phase.Cancelling:
            {
                var t = p.cancelDuration <= 0f ? 1f : Mathf.Clamp01(phaseTime / p.cancelDuration);
                var y = p.cancelCurve != null ? p.cancelCurve.Evaluate(t) : t;
                scale = Mathf.LerpUnclamped(endFromScale, endFromScale * p.cancelScale, y);
                alpha = endFromAlpha * (1f - Mathf.Clamp01(y));
                shown = endFromProgress * (1f - Mathf.Clamp01(y));
                if (t >= 1f)
                {
                    HideImmediate();
                    return;
                }

                break;
            }

            default:
                return;
        }

        body.localScale = new Vector3(scale, scale, 1f);
        group.alpha = alpha;

        var color = p.fillColor != null ? p.fillColor.Evaluate(phase == Phase.Cancelling ? endFromProgress : shown) : Color.white;
        fill.color = color;
        fill.fillAmount = shown;

        var caps = p.roundCaps && shown > 0.001f;
        startCap.enabled = caps;
        headCap.enabled = caps;
        startCapOutline.enabled = caps && p.outlineWidth > 0f;
        headCapOutline.enabled = caps && p.outlineWidth > 0f;
        if (!caps)
        {
            return;
        }

        startCap.color = color;
        headCap.color = color;

        var radius = (p.diameter - p.thickness) * 0.5f;
        var angle = shown * Mathf.PI * 2f;
        var head = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
        var start = new Vector2(0f, radius);
        startCap.rectTransform.anchoredPosition = start;
        startCapOutline.rectTransform.anchoredPosition = start;
        headCap.rectTransform.anchoredPosition = head;
        headCapOutline.rectTransform.anchoredPosition = head;
    }

    private InputPromptHoldRingProfile Active
    {
        get
        {
            if (active == null)
            {
                active = profile != null ? profile : InputPromptHoldRingProfile.LoadDefault();
            }

            return active;
        }
    }

    private void EnsureBuilt()
    {
        if (body != null)
        {
            return;
        }

        rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;

        var bodyObject = new GameObject("Ring", typeof(RectTransform), typeof(CanvasGroup));
        body = (RectTransform)bodyObject.transform;
        body.SetParent(rect, false);
        group = bodyObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        outline = CreateImage("Outline", body);
        track = CreateImage("Track", body);
        startCapOutline = CreateImage("StartCapOutline", body);
        headCapOutline = CreateImage("HeadCapOutline", body);
        fill = CreateImage("Fill", body);
        startCap = CreateImage("StartCap", body);
        headCap = CreateImage("HeadCap", body);

        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.fillAmount = 0f;
    }

    private void ApplyLook()
    {
        var p = Active;
        if (!Mathf.Approximately(builtDiameter, p.diameter)
            || !Mathf.Approximately(builtThickness, p.thickness)
            || !Mathf.Approximately(builtOutline, p.outlineWidth))
        {
            RebuildTextures(p);
        }

        rect.anchoredPosition = p.offset;

        var size = new Vector2(p.diameter, p.diameter);
        track.rectTransform.sizeDelta = size;
        fill.rectTransform.sizeDelta = size;
        track.sprite = ringSprite;
        fill.sprite = ringSprite;
        track.color = p.trackColor;

        var outlineSize = p.diameter + p.outlineWidth * 2f;
        outline.rectTransform.sizeDelta = new Vector2(outlineSize, outlineSize);
        outline.sprite = outlineSprite;
        outline.color = p.outlineColor;
        outline.enabled = p.outlineWidth > 0f;

        var cap = CapSprite;
        var capSize = new Vector2(p.thickness, p.thickness);
        var capOutlineSize = new Vector2(p.thickness + p.outlineWidth * 2f, p.thickness + p.outlineWidth * 2f);
        foreach (var image in new[] { startCap, headCap })
        {
            image.sprite = cap;
            image.rectTransform.sizeDelta = capSize;
        }

        foreach (var image in new[] { startCapOutline, headCapOutline })
        {
            image.sprite = cap;
            image.rectTransform.sizeDelta = capOutlineSize;
            image.color = p.outlineColor;
        }
    }

    private void RebuildTextures(InputPromptHoldRingProfile p)
    {
        DestroyGenerated(ref ringSprite);
        DestroyGenerated(ref outlineSprite);
        DestroyGenerated(ref ringTexture);
        DestroyGenerated(ref outlineTexture);

        var outer = p.diameter * 0.5f;
        var inner = Mathf.Max(0f, outer - p.thickness);
        ringTexture = CreateRingTexture(outer, inner, outer);
        ringSprite = CreateSprite(ringTexture);

        var outlineOuter = outer + p.outlineWidth;
        var outlineInner = Mathf.Max(0f, inner - p.outlineWidth);
        outlineTexture = CreateRingTexture(outlineOuter, outlineInner, outlineOuter);
        outlineSprite = CreateSprite(outlineTexture);

        builtDiameter = p.diameter;
        builtThickness = p.thickness;
        builtOutline = p.outlineWidth;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchoredPosition = Vector2.zero;
        return image;
    }

    /// <summary>An anti-aliased white ring; radii in canvas units, the texture covers a square of half-size <paramref name="half"/>.</summary>
    private static Texture2D CreateRingTexture(float outerRadius, float innerRadius, float half)
    {
        var size = Mathf.Clamp(Mathf.CeilToInt(half * 2f * TexelsPerUnit), 32, 1024);
        var texelsPerUnit = size / (half * 2f);
        var outer = outerRadius * texelsPerUnit;
        var inner = innerRadius * texelsPerUnit;
        var centre = size * 0.5f;

        var texture = NewTexture(size);
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - centre;
                var dy = y + 0.5f - centre;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Sprite CapSprite
    {
        get
        {
            if (capSprite != null)
            {
                return capSprite;
            }

            capTexture = CreateRingTexture(CapTextureRadius, 0f, CapTextureRadius + 1f);
            capTexture.hideFlags = HideFlags.HideAndDontSave;
            capSprite = CreateSprite(capTexture);
            capSprite.hideFlags = HideFlags.HideAndDontSave;
            return capSprite;
        }
    }

    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "InputPromptHoldRing",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
    }

    private static Sprite CreateSprite(Texture2D texture)
    {
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        return sprite;
    }

    private static void DestroyGenerated<T>(ref T asset) where T : Object
    {
        if (asset == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(asset);
        }
        else
        {
            DestroyImmediate(asset);
        }

        asset = null;
    }
}
}
