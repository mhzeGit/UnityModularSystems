using System;
using System.Collections.Generic;
using MHZE.DialogSystem;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// Moves the NPC's mouth for its dialog lines that have no voice clip. <see cref="NpcTalking"/> plays the
    /// talking animation while voice audio plays; a line without audio (text only) would leave the speaker
    /// silent-faced. This component follows <see cref="DialogManager"/>: when a line's speaker is one of
    /// <see cref="SpeakerNames"/> and the line has no voice clip, it holds the talking state for about as long
    /// as the text takes to say (<see cref="CharactersPerSecond"/>, at least <see cref="MinimumSeconds"/>,
    /// never longer than the line's own duration when it has one), and lets go when the line changes or the
    /// dialog ends. Lines with a voice clip are left to the audio.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Npc))]
    [AddComponentMenu("Modular NPC/Dialog Talking")]
    public sealed class NpcDialogTalking : MonoBehaviour
    {
        [SerializeField, Tooltip("Speaker names (as written in the dialog lines) that are this character. Case is ignored.")]
        private List<string> speakerNames = new List<string>();

        [SerializeField, Min(1f), Tooltip("Talking speed used to time a silent line's mouth movement, in characters per second.")]
        private float charactersPerSecond = 14f;

        [SerializeField, Min(0.1f), Tooltip("Shortest time the mouth moves for a silent line, in seconds.")]
        private float minimumSeconds = 0.8f;

        private DialogManager _manager;
        private Npc _npc;
        private float _until;
        private bool _holding;

        /// <summary>Speaker names that are this character.</summary>
        public IList<string> SpeakerNames => speakerNames;

        public float CharactersPerSecond
        {
            get => charactersPerSecond;
            set => charactersPerSecond = Mathf.Max(1f, value);
        }

        public float MinimumSeconds
        {
            get => minimumSeconds;
            set => minimumSeconds = Mathf.Max(0.1f, value);
        }

        private void OnEnable()
        {
            _npc = GetComponent<Npc>();
            Connect();
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void Update()
        {
            Connect();
            if (_holding && Time.unscaledTime >= _until)
            {
                Release();
            }
        }

        private void Connect()
        {
            if (_manager == DialogManager.Instance)
            {
                return;
            }

            Disconnect();
            _manager = DialogManager.Instance;
            if (_manager == null)
            {
                return;
            }

            _manager.LineChanged += OnLine;
            _manager.DialogFinished += OnFinished;
        }

        private void Disconnect()
        {
            if (_manager != null)
            {
                _manager.LineChanged -= OnLine;
                _manager.DialogFinished -= OnFinished;
            }

            Release();
            _manager = null;
        }

        private void OnFinished(DialogSequence sequence)
        {
            Release();
        }

        private void OnLine(string speaker, string text)
        {
            Release();
            if (!IsMe(speaker) || _manager == null)
            {
                return;
            }

            DialogSequence sequence = _manager.CurrentSequence;
            int index = _manager.CurrentLineIndex;
            DialogLine line = sequence != null && index >= 0 ? sequence.GetLine(index) : null;
            if (line != null && line.VoiceOver != null)
            {
                return;     // the voice audio drives the talking state
            }

            float seconds = Mathf.Max(minimumSeconds, (text ?? string.Empty).Length / Mathf.Max(1f, charactersPerSecond));
            if (line != null && line.Duration > 0f)
            {
                seconds = Mathf.Min(seconds, line.Duration);
            }

            if (_npc == null || !_npc.Features.TryGetOperational(out NpcTalking talking))
            {
                return;
            }

            talking.Hold(this, true);
            _holding = true;
            _until = Time.unscaledTime + seconds;
        }

        private void Release()
        {
            if (!_holding)
            {
                return;
            }

            _holding = false;
            if (_npc != null && _npc.Features.TryGet(out NpcTalking talking))
            {
                talking.Hold(this, false);
            }
        }

        private bool IsMe(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
            {
                return false;
            }

            string trimmed = speaker.Trim();
            for (int i = 0; i < speakerNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(speakerNames[i]) &&
                    string.Equals(speakerNames[i].Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
