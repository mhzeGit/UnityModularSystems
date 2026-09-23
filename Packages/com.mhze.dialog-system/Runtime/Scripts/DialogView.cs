// The Canvas UI for a conversation: a speech bubble whose size follows the text, with the speaker and the spoken line shown together as {Name}:"Text". Assign the child references in the inspector, or let Tools > Dialog System > Setup Dialog Canvas build and wire them.

using UnityEngine;
using UnityEngine.UI;

namespace MHZE.DialogSystem
{
public class DialogView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text speakerText;
    [SerializeField] private Text dialogText;
    [SerializeField] private GameObject continueIndicator;

    [Header("Format")]
    [Tooltip("Show the speaker and the line as one piece of text: {Name}:\"Text\". When off, the legacy separate speaker label is used.")]
    [SerializeField] private bool combineSpeakerIntoText = true;

    [Tooltip("Tint applied to the speaker name inside the combined text.")]
    [SerializeField] private Color speakerColor = new Color(1f, 0.92f, 0.6f);

    [Header("Layout")]
    [Tooltip("Resize the bubble to fit the text instead of using a fixed panel size.")]
    [SerializeField] private bool dynamicSize = true;

    [Tooltip("Center the text inside the bubble.")]
    [SerializeField] private bool centerText = true;

    [Tooltip("Smallest bubble width, in reference pixels.")]
    [Min(0f)]
    [SerializeField] private float minWidth = 300f;

    [Tooltip("Largest bubble width, in reference pixels. Longer lines wrap to a new line.")]
    [Min(0f)]
    [SerializeField] private float maxWidth = 860f;

    [Tooltip("Inner space between the text and the bubble edge, in reference pixels.")]
    [SerializeField] private Vector2 padding = new Vector2(28f, 20f);

    [Tooltip("Ease into each new size instead of snapping, so the bubble follows the typewriter reveal smoothly.")]
    [SerializeField] private bool smoothResize = true;

    [Tooltip("How quickly the bubble catches up to its target size. Higher is snappier.")]
    [Min(0f)]
    [SerializeField] private float resizeSpeed = 16f;

    [Header("Appearance")]
    [Tooltip("Tint of the spoken line. The speaker name keeps its own Speaker Color.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Background colour of the bubble.")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.78f);

    private string currentSpeaker = string.Empty;
    private string currentText = string.Empty;
    private Vector2 resizeTarget;
    private bool snapNextResize = true;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    public GameObject PanelRoot => panelRoot;

    public Text SpeakerText => speakerText;

    public Text DialogText => dialogText;

    public GameObject ContinueIndicator => continueIndicator;

    private void Awake()
    {
        Hide();
        SetContinueVisible(false);
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        snapNextResize = true;
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void SetSpeaker(string speaker)
    {
        currentSpeaker = speaker ?? string.Empty;

        if (combineSpeakerIntoText)
        {
            if (speakerText != null)
            {
                speakerText.gameObject.SetActive(false);
            }
        }
        else if (speakerText != null)
        {
            speakerText.text = currentSpeaker;
            speakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(currentSpeaker));
        }

        RefreshBody();
    }

    /// <summary>
    /// Sets the visible line and resizes the bubble to fit it. With a typewriter reveal this is called
    /// once per revealed chunk, so the bubble grows along with the text.
    /// </summary>
    public void SetText(string text)
    {
        currentText = text ?? string.Empty;
        RefreshBody();
    }

    public void SetContinueVisible(bool visible)
    {
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(visible);
        }
    }

    private void RefreshBody()
    {
        if (dialogText != null)
        {
            dialogText.text = BuildDisplay(currentText);
        }

        ApplyAppearance();
        ApplyLayout();
    }

    private void ApplyAppearance()
    {
        if (dialogText != null)
        {
            dialogText.color = textColor;
        }

        Image background = panelRoot != null ? panelRoot.GetComponent<Image>() : null;
        if (background == null)
        {
            return;
        }

        background.color = backgroundColor;
    }

    private void ApplyLayout()
    {
        if (!dynamicSize)
        {
            return;
        }

        RectTransform panelRect = panelRoot != null ? panelRoot.transform as RectTransform : null;
        Text body = dialogText != null ? dialogText : speakerText;
        if (panelRect == null || body == null)
        {
            return;
        }

        float padX = Mathf.Max(0f, padding.x);
        float padY = Mathf.Max(0f, padding.y);

        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        if (centerText)
        {
            body.alignment = TextAnchor.MiddleCenter;
        }

        RectTransform bodyRect = body.rectTransform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(padX, padY);
        bodyRect.offsetMax = new Vector2(-padX, -padY);

        float maxBody = Mathf.Max(1f, maxWidth - padX * 2f);
        float minBody = Mathf.Clamp(minWidth - padX * 2f, 1f, maxBody);

        string display = BuildDisplay(currentText);
        float unwrappedWidth = MeasureWidth(body, display);
        float bodyWidth = Mathf.Clamp(unwrappedWidth, minBody, maxBody);
        float bodyHeight = MeasureHeight(body, display, bodyWidth);

        resizeTarget = new Vector2(
            bodyWidth + padX * 2f,
            Mathf.Max(bodyHeight, body.fontSize) + padY * 2f);

        if (!smoothResize || !Application.isPlaying || snapNextResize)
        {
            panelRect.sizeDelta = resizeTarget;
            snapNextResize = false;
        }
        else
        {
            // Grow instantly so the bubble is never narrower (or shorter) than the text it already
            // shows. Animating growth would re-wrap the line mid-transition and clip it; shrinking is
            // eased by Update, which is always safe.
            Vector2 current = panelRect.sizeDelta;
            panelRect.sizeDelta = new Vector2(
                Mathf.Max(current.x, resizeTarget.x),
                Mathf.Max(current.y, resizeTarget.y));
        }
    }

    private void Update()
    {
        if (!dynamicSize || !smoothResize || resizeSpeed <= 0f)
        {
            return;
        }

        if (panelRoot == null || !panelRoot.activeSelf)
        {
            return;
        }

        RectTransform panelRect = panelRoot.transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        Vector2 current = panelRect.sizeDelta;
        if (current.x <= resizeTarget.x && current.y <= resizeTarget.y)
        {
            return;
        }

        float blend = 1f - Mathf.Exp(-resizeSpeed * Time.unscaledDeltaTime);
        Vector2 next = Vector2.Lerp(current, resizeTarget, blend);
        next.x = Mathf.Max(next.x, resizeTarget.x);
        next.y = Mathf.Max(next.y, resizeTarget.y);
        panelRect.sizeDelta = next;
    }

    private string BuildDisplay(string visibleText)
    {
        string text = visibleText ?? string.Empty;

        if (!combineSpeakerIntoText)
        {
            return text;
        }

        if (string.IsNullOrWhiteSpace(currentSpeaker))
        {
            return "\"" + text + "\"";
        }

        string hex = ColorUtility.ToHtmlStringRGB(speakerColor);
        return "<b><color=#" + hex + ">" + currentSpeaker + "</color></b>:\"" + text + "\"";
    }

    private static float MeasureWidth(Text text, string content)
    {
        TextGenerationSettings settings = text.GetGenerationSettings(Vector2.zero);
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        float pixelsPerUnit = text.pixelsPerUnit == 0f ? 1f : text.pixelsPerUnit;
        return text.cachedTextGeneratorForLayout.GetPreferredWidth(content, settings) / pixelsPerUnit;
    }

    private static float MeasureHeight(Text text, string content, float width)
    {
        TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        float pixelsPerUnit = text.pixelsPerUnit == 0f ? 1f : text.pixelsPerUnit;
        return text.cachedTextGeneratorForLayout.GetPreferredHeight(content, settings) / pixelsPerUnit;
    }
}
}
