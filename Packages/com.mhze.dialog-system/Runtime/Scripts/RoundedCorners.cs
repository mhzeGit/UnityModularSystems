// Attach to any GameObject with a UI Image to give it procedurally generated rounded corners. Works in the editor as well as at runtime: change the radius in the inspector and the sprite regenerates immediately. Reusable for any 2D UI sprite, not just dialog boxes.

using UnityEngine;
using UnityEngine.UI;

namespace MHZE.DialogSystem
{
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("UI/Rounded Corners (Procedural)")]
public class RoundedCorners : MonoBehaviour
{
    [Tooltip("Corner radius in UI pixels. 0 disables the effect and restores the original sprite.")]
    [Min(0f)]
    [SerializeField] private float radius = 24f;

    [Tooltip("Restore the Image's original sprite and type when this component is disabled or removed.")]
    [SerializeField] private bool restoreOnDisable = true;

    private Image target;
    private Sprite originalSprite;
    private Image.Type originalType;
    private bool originalFillCenter;
    private bool hasOriginal;

    public float Radius
    {
        get => radius;
        set
        {
            radius = Mathf.Max(0f, value);
            Apply();
        }
    }

    private Image Target
    {
        get
        {
            if (target == null)
            {
                target = GetComponent<Image>();
            }

            return target;
        }
    }

    private void OnEnable()
    {
        CaptureOriginal();
        Apply();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            Apply();
        }
    }

    private void OnDisable()
    {
        if (restoreOnDisable)
        {
            Restore();
        }
    }

    /// <summary>(Re)generates and assigns the rounded sprite.</summary>
    public void Apply()
    {
        Image image = Target;
        if (image == null)
        {
            return;
        }

        CaptureOriginal();

        int roundedRadius = Mathf.RoundToInt(radius);
        if (roundedRadius <= 0)
        {
            Restore();
            return;
        }

        image.sprite = ProceduralRoundedSprite.Get(roundedRadius);
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
    }

    /// <summary>Puts the Image back to the sprite and type it had before this component ran.</summary>
    public void Restore()
    {
        Image image = Target;
        if (image == null || !hasOriginal)
        {
            return;
        }

        image.sprite = originalSprite;
        image.type = originalType;
        image.fillCenter = originalFillCenter;
    }

    private void CaptureOriginal()
    {
        if (hasOriginal)
        {
            return;
        }

        Image image = Target;
        if (image == null)
        {
            return;
        }

        originalSprite = image.sprite;
        originalType = image.type;
        originalFillCenter = image.fillCenter;
        hasOriginal = true;
    }
}
}
