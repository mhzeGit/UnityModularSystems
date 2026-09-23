// Starts a dialog when a target (usually the player) comes within range, optionally gated on another actor (usually the NPC) standing near a fixed spot. Finds targets by tag when none are assigned, supports one-shot or repeat-with-cooldown behaviour, and can stop the conversation when the target walks away. Draws the detection radii in the Scene view when selected.

using UnityEngine;

namespace MHZE.DialogSystem
{
[DisallowMultipleComponent]
public class DialogProximityTrigger : MonoBehaviour
{
    [Header("Dialog")]
    [SerializeField] private DialogSequence sequence;

    [Tooltip("Overrides the speaker name of every line. Leave empty to use each line's own speaker.")]
    [SerializeField] private string speakerOverride = string.Empty;

    [Header("Target")]
    [Tooltip("Explicit target (the player). When empty, the tag below is searched for.")]
    [SerializeField] private Transform target;

    [SerializeField] private string targetTag = "Player";

    [Tooltip("Radius in metres, measured on the horizontal plane. When a required actor is set this is measured from that actor, so it is how close the player must get to the actor.")]
    [Min(0.1f)]
    [SerializeField] private float radius = 3f;

    [Tooltip("Require the target to be in front of the origin.")]
    [SerializeField] private bool requireFacing;

    [Range(1f, 180f)]
    [SerializeField] private float facingAngle = 60f;

    [Header("Actor Gate")]
    [Tooltip("Optional actor (typically the NPC) that must also be near this trigger. Use it to gate a dialog on a character standing at a specific spot: the dialog only starts while the actor is inside the actor radius.")]
    [SerializeField] private Transform requiredActor;

    [Tooltip("Tag used to find the required actor when no explicit reference is assigned. Leave empty to skip.")]
    [SerializeField] private string requiredActorTag = string.Empty;

    [Tooltip("How close the required actor must be to this trigger, in metres, on the horizontal plane.")]
    [Min(0.1f)]
    [SerializeField] private float actorRadius = 3f;

    [Header("Gate")]
    [Tooltip("Optional component implementing IDialogGate. The dialog is not armed until that gate allows it while the required actor is present, so it can wait for the actor to be in a particular state.")]
    [SerializeField] private MonoBehaviour gate;

    [Header("Repeat")]
    [Tooltip("Only ever play the sequence once.")]
    [SerializeField] private bool onlyOnce = true;

    [Tooltip("Seconds before the sequence can play again when onlyOnce is disabled.")]
    [Min(0f)]
    [SerializeField] private float retriggerDelay = 10f;

    [Tooltip("Stop the conversation when the target leaves the radius.")]
    [SerializeField] private bool stopWhenTargetLeaves;

    private bool hasPlayed;
    private float nextAllowedTime;
    private bool targetInside;
    private bool pendingPlay;
    private bool actorArmed;
    private IDialogGate resolvedGate;

    public DialogSequence Sequence
    {
        get => sequence;
        set => sequence = value;
    }

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    public float Radius
    {
        get => radius;
        set => radius = value;
    }

    public Transform RequiredActor
    {
        get => requiredActor;
        set => requiredActor = value;
    }

    public float ActorRadius
    {
        get => actorRadius;
        set => actorRadius = value;
    }

    public MonoBehaviour Gate
    {
        get => gate;
        set
        {
            gate = value;
            resolvedGate = value as IDialogGate;
        }
    }

    private void Awake()
    {
        resolvedGate = gate as IDialogGate;
        if (gate != null && resolvedGate == null)
        {
            Debug.LogWarning("[DialogProximityTrigger] The assigned Gate does not implement IDialogGate and will be ignored.", this);
        }
    }

    private void OnDisable()
    {
        targetInside = false;
        pendingPlay = false;
        actorArmed = false;
    }

    private void Update()
    {
        if (sequence == null || !TryGetTarget(out Transform current))
        {
            return;
        }

        bool inside = IsInside(current);
        if (inside && !targetInside)
        {
            OnTargetEntered();
        }
        else if (!inside && targetInside)
        {
            OnTargetLeft();
        }

        targetInside = inside;

        if (pendingPlay && !IsDialogPlaying())
        {
            pendingPlay = false;
            if (inside)
            {
                StartDialog();
            }
        }
    }

    /// <summary>Starts the dialog now, ignoring range and repeat rules.</summary>
    public void PlayNow()
    {
        pendingPlay = false;
        StartDialog();
    }

    private void OnTargetEntered()
    {
        if (hasPlayed && onlyOnce)
        {
            return;
        }

        if (Time.time < nextAllowedTime)
        {
            return;
        }

        if (requireFacing && target != null && !IsFacing(target))
        {
            return;
        }

        if (IsDialogPlaying())
        {
            pendingPlay = true;
            return;
        }

        StartDialog();
    }

    private void OnTargetLeft()
    {
        pendingPlay = false;

        if (!stopWhenTargetLeaves)
        {
            return;
        }

        DialogManager manager = DialogManager.Instance;
        if (manager != null && manager.IsPlaying && manager.CurrentSequence == sequence)
        {
            manager.Stop();
        }
    }

    private void StartDialog()
    {
        if (sequence == null)
        {
            return;
        }

        DialogManager manager = DialogManager.Instance;
        if (manager == null)
        {
            WarnMissingManager();
            return;
        }

        manager.Play(sequence, speakerOverride);
        hasPlayed = true;
        nextAllowedTime = Time.time + retriggerDelay;
    }

    private static bool IsDialogPlaying()
    {
        DialogManager manager = DialogManager.Instance;
        return manager != null && manager.IsPlaying;
    }

    private bool TryGetTarget(out Transform current)
    {
        if (target == null && !string.IsNullOrWhiteSpace(targetTag))
        {
            GameObject found = GameObject.FindGameObjectWithTag(targetTag);
            if (found != null)
            {
                target = found.transform;
            }
        }

        current = target;
        return current != null;
    }

    private Transform TryGetActor()
    {
        if (requiredActor == null && !string.IsNullOrWhiteSpace(requiredActorTag))
        {
            GameObject found = GameObject.FindGameObjectWithTag(requiredActorTag);
            if (found != null)
            {
                requiredActor = found.transform;
            }
        }

        return requiredActor;
    }

    private bool IsInside(Transform current)
    {
        Transform actor = TryGetActor();

        bool actorHere = actor == null || WithinRadius(actor.position, transform.position, actorRadius);
        if (!actorHere)
        {
            // The actor is not at this spot: the spot has to be earned again before it can fire.
            actorArmed = false;
            return false;
        }

        if (IsGateOpen())
        {
            actorArmed = true;
        }

        if (!actorArmed)
        {
            return false;
        }

        Vector3 origin = actor != null ? actor.position : transform.position;
        return WithinRadius(current.position, origin, radius);
    }

    private bool IsGateOpen()
    {
        if (resolvedGate == null)
        {
            resolvedGate = gate as IDialogGate;
        }

        return resolvedGate == null || resolvedGate.CanStartDialog;
    }

    private bool IsFacing(Transform current)
    {
        Transform actor = requiredActor;
        Vector3 origin = actor != null ? actor.position : transform.position;
        Vector3 toTarget = current.position - origin;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        return Vector3.Angle(transform.forward, toTarget) <= facingAngle;
    }

    private static bool WithinRadius(Vector3 point, Vector3 origin, float r)
    {
        Vector3 delta = point - origin;
        delta.y = 0f;
        return delta.sqrMagnitude <= r * r;
    }

    private void WarnMissingManager()
    {
        Debug.LogWarning("[DialogProximityTrigger] No DialogManager found. Add one via Tools > Dialog System > Setup Dialog Canvas.", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (requiredActor != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, actorRadius);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(requiredActor.position, radius);
        }
        else
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        if (!requireFacing)
        {
            return;
        }

        Vector3 origin = requiredActor != null ? requiredActor.position : transform.position;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Vector3 left = Quaternion.Euler(0f, -facingAngle, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, facingAngle, 0f) * transform.forward;
        Gizmos.DrawRay(origin, left * radius);
        Gizmos.DrawRay(origin, right * radius);
    }
}
}
