// How the hold ring looks and moves: its size, colours, and one editable curve plus a duration for each of its three motions (appear, complete burst, cancel). Assign it on the ring, or put an asset named InputPromptHoldRingProfile in any Resources folder; without one the built-in defaults below are used.

using UnityEngine;

namespace MHZE.InputPromptSystem
{
/// <summary>
/// Look and motion of the <see cref="InputPromptHoldRing"/>. Each motion is one curve over normalized time
/// (x: 0 to 1 across its duration) with a stated y meaning, so the feel is tuned by dragging keys in the
/// inspector, never by code. The fill itself is not animated: it always shows the true share of the hold.
/// The profile is read when a hold starts, so edits apply from the next hold (also in Play Mode).
/// </summary>
[CreateAssetMenu(fileName = DefaultResourceName, menuName = "Input Prompts/Hold Ring Profile")]
public class InputPromptHoldRingProfile : ScriptableObject
{
    public const string DefaultResourceName = "InputPromptHoldRingProfile";

    [Header("Look")]
    [Tooltip("Outer diameter of the ring in canvas units (1920x1080 reference).")]
    [Min(4f)] public float diameter = 58f;

    [Tooltip("Width of the ring stroke in canvas units.")]
    [Min(1f)] public float thickness = 5f;

    [Tooltip("Width of the dark outline around the stroke, so it reads on bright walls. 0: none.")]
    [Min(0f)] public float outlineWidth = 1.5f;

    [Tooltip("Offset of the ring from the centre of the screen, in canvas units.")]
    public Vector2 offset = Vector2.zero;

    [Tooltip("Rounded ends on the filled part of the stroke.")]
    public bool roundCaps = true;

    public Color outlineColor = new(0f, 0f, 0f, 0.4f);
    public Color trackColor = new(1f, 1f, 1f, 0.2f);

    [Tooltip("Colour of the filled part by hold progress (left: just started, right: done).")]
    public Gradient fillColor = DefaultFillGradient();

    [Header("Appear (when the hold starts)")]
    [Min(0f)] public float appearDuration = 0.16f;

    [Tooltip("y: 0 = invisible at Appear Start Scale, 1 = full size and opaque. Above 1 overshoots (pops bigger).")]
    public AnimationCurve appearCurve = DefaultAppearCurve();

    [Min(0f)] public float appearStartScale = 0.55f;

    [Header("Complete (the hold finished)")]
    [Min(0f)] public float completeDuration = 0.32f;

    [Tooltip("y: 0 = full ring at rest, 1 = grown to Complete Scale and faded out.")]
    public AnimationCurve completeCurve = DefaultCompleteCurve();

    [Min(0f)] public float completeScale = 1.45f;

    [Header("Cancel (the key was let go early)")]
    [Min(0f)] public float cancelDuration = 0.2f;

    [Tooltip("y: 0 = as it was when let go, 1 = drained, shrunk to Cancel Scale and faded out.")]
    public AnimationCurve cancelCurve = DefaultCancelCurve();

    [Min(0f)] public float cancelScale = 0.8f;

    private static InputPromptHoldRingProfile fallback;

    /// <summary>The asset named <see cref="DefaultResourceName"/> in a Resources folder, or the built-in defaults.</summary>
    public static InputPromptHoldRingProfile LoadDefault()
    {
        var loaded = Resources.Load<InputPromptHoldRingProfile>(DefaultResourceName);
        if (loaded != null)
        {
            return loaded;
        }

        if (fallback == null)
        {
            fallback = CreateInstance<InputPromptHoldRingProfile>();
            fallback.hideFlags = HideFlags.HideAndDontSave;
        }

        return fallback;
    }

    /// <summary>Quick rise with a small overshoot that settles back to 1.</summary>
    public static AnimationCurve DefaultAppearCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.2f),
            new Keyframe(0.62f, 1.08f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));
    }

    /// <summary>Ease-out burst: fast at first, slowing as it fades.</summary>
    public static AnimationCurve DefaultCompleteCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.2f),
            new Keyframe(1f, 1f, 0f, 0f));
    }

    /// <summary>Smoothstep drain.</summary>
    public static AnimationCurve DefaultCancelCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));
    }

    /// <summary>Soft white that warms to the crosshair's gold as the hold completes.</summary>
    public static Gradient DefaultFillGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.93f, 0.72f), 0.55f),
                new GradientColorKey(new Color(1f, 0.82f, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }
}
}
