using System;
using System.Collections.Generic;
using MHZE.DialogSystem;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Bridges the dialog system and the NPC's point feature (<see cref="INpcPointAt"/>,
    /// <see cref="NpcPointAtTarget"/>). While a mapped dialog line is on screen the NPC extends an arm
    /// toward the scene location the line talks about, and lowers it when the line range ends or the
    /// conversation finishes.
    ///
    /// Put it on the NPC, then add one entry per location: which sequence, which transform to point at
    /// and, optionally, the line range it applies to. The point feature picks the arm by itself from
    /// where the target is relative to the NPC's facing; an entry can force one arm. The component
    /// knows nothing about arms or bones - only which dialog line points where.
    ///
    /// Lives in the optional <c>ModularNPC.Dialog</c> assembly, which compiles only when the Dialog
    /// System package (<c>com.mhze.dialog-system</c>) is installed.
    /// </summary>
    [AddComponentMenu("Modular NPC/Dialog Point At")]
    [DisallowMultipleComponent]
    public sealed class NpcDialogPointAt : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The dialog this entry belongs to.")]
            public DialogSequence sequence;

            [Tooltip("The scene transform to point at: a door, a room marker, a landmark.")]
            public Transform target;

            [Tooltip("World-space offset added to the target, for example a little above a door's pivot so the arm aims at the door and not the floor.")]
            public Vector3 aimOffset = new Vector3(0f, 0.8f, 0f);

            [Tooltip("First line (0-based) the NPC points on.")]
            [Min(0)] public int firstLine;

            [Tooltip("Last line (0-based) the NPC points on. -1 keeps pointing until the dialog ends.")]
            public int lastLine = -1;

            [Tooltip("None lets the NPC choose the best arm for the target; Left or Right forces one.")]
            public NpcPointArm arm = NpcPointArm.None;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Tooltip("Logs a warning when the NPC has no operational point feature (NpcPointAtTarget).")]
        [SerializeField] private bool warnWhenMissing = true;

        private Npc _npc;
        private INpcPointAt _point;
        private DialogManager _manager;
        private DialogSequence _sequence;
        private int _shownLines;
        private bool _warned;

        public IList<Entry> Entries => entries;

        private void Awake()
        {
            _npc = GetComponent<Npc>();
            if (_npc == null)
            {
                _npc = GetComponentInParent<Npc>();
            }
        }

        private void Update()
        {
            // The dialog manager may not exist yet when this component enables, and a scene reload
            // can replace it, so follow whichever instance is current.
            DialogManager current = DialogManager.Instance;
            if (current == _manager)
            {
                return;
            }

            Unsubscribe();
            _manager = current;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _manager = null;
            _sequence = null;
            StopPointing();
        }

        private void Subscribe()
        {
            if (_manager == null)
            {
                return;
            }

            _manager.DialogStarted += OnDialogStarted;
            _manager.LineChanged += OnLineChanged;
            _manager.DialogFinished += OnDialogFinished;
        }

        private void Unsubscribe()
        {
            if (_manager == null)
            {
                return;
            }

            _manager.DialogStarted -= OnDialogStarted;
            _manager.LineChanged -= OnLineChanged;
            _manager.DialogFinished -= OnDialogFinished;
        }

        private void OnDialogStarted(DialogSequence sequence)
        {
            _sequence = sequence;
            _shownLines = 0;

            // A new conversation starts clean; a line event right after this raises the first point.
            if (!HasEntryFor(sequence))
            {
                StopPointing();
            }
        }

        private void OnLineChanged(string speaker, string text)
        {
            if (_sequence == null)
            {
                return;
            }

            // The manager skips blank lines and reports only the lines it shows, so the nth event is
            // the nth non-blank line of the sequence.
            int lineIndex = ResolveLineIndex(_sequence, _shownLines);
            _shownLines++;

            Entry entry = FindEntry(_sequence, lineIndex);
            if (entry == null)
            {
                StopPointing();
                return;
            }

            if (TryGetPoint(out INpcPointAt point))
            {
                point.PointAt(entry.target, entry.aimOffset, entry.arm);
            }
        }

        private void OnDialogFinished(DialogSequence sequence)
        {
            if (_sequence == sequence)
            {
                _sequence = null;
            }

            StopPointing();
        }

        private Entry FindEntry(DialogSequence sequence, int lineIndex)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.sequence != sequence || entry.target == null)
                {
                    continue;
                }

                bool afterStart = lineIndex >= entry.firstLine;
                bool beforeEnd = entry.lastLine < 0 || lineIndex <= entry.lastLine;
                if (afterStart && beforeEnd)
                {
                    return entry;
                }
            }

            return null;
        }

        private bool HasEntryFor(DialogSequence sequence)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].sequence == sequence)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveLineIndex(DialogSequence sequence, int shownCount)
        {
            int seen = 0;
            for (int i = 0; i < sequence.LineCount; i++)
            {
                DialogLine line = sequence.GetLine(i);
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                if (seen == shownCount)
                {
                    return i;
                }

                seen++;
            }

            return shownCount;
        }

        private void StopPointing()
        {
            if (_point != null)
            {
                _point.StopPointing();
            }
        }

        private bool TryGetPoint(out INpcPointAt point)
        {
            point = _point;
            if (point != null)
            {
                return true;
            }

            if (_npc != null && _npc.Features.TryGetOperational(out point))
            {
                _point = point;
                return true;
            }

            if (warnWhenMissing && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[NpcDialogPointAt] " + name + " has no operational point feature (NpcPointAtTarget).", this);
            }

            return false;
        }
    }
}
