using System;
using System.Collections.Generic;
using MHZE.DialogSystem;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>Explicit per-line cues. Unmapped lines and dialog termination release only this component's expression.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Npc))]
    [AddComponentMenu("Modular NPC/Dialog Expressions")]
    public sealed class NpcDialogExpressions : MonoBehaviour
    {
        [Serializable]
        public sealed class Cue
        {
            public DialogSequence sequence;
            [Min(0)] public int line;
            public string expression = "Happy";
            [Range(0,1)] public float intensity = 0.5f;
            [Min(0.1f)] public float duration = 2f;
        }
        [SerializeField] private List<Cue> cues = new List<Cue>();
        public IList<Cue> Cues => cues;
        private DialogManager _manager;
        private Npc _npc;
        private DialogSequence _sequence;
        private int _shown;
        private void OnEnable() { _npc = GetComponent<Npc>(); Connect(); }
        private void Update() { Connect(); }
        private void Connect()
        {
            if (_manager == DialogManager.Instance) return;
            Disconnect(); _manager = DialogManager.Instance;
            if (_manager == null) return;
            _manager.DialogStarted += Started; _manager.LineChanged += Line; _manager.DialogFinished += Finished;
        }
        private void OnDisable() { Disconnect(); }
        private void Disconnect()
        {
            if (_manager != null) { _manager.DialogStarted -= Started; _manager.LineChanged -= Line; _manager.DialogFinished -= Finished; }
            Release(true); _manager = null; _sequence = null;
        }
        private void Started(DialogSequence sequence) { Release(false); _sequence = sequence; _shown = 0; }
        private void Finished(DialogSequence sequence) { if (_sequence == sequence) { _sequence = null; Release(false); } }
        private void Release(bool immediate)
        {
            if (_npc != null && _npc.Features.TryGet(out NpcExpressions face)) face.Clear(immediate ? 0 : 0.35f, this);
        }
        private void Line(string speaker, string text)
        {
            if (_sequence == null || _npc == null || !_npc.Features.TryGetOperational(out NpcExpressions face)) return;
            int seen = 0, index = -1;
            for (int i = 0; i < _sequence.LineCount; i++) {
                var line = _sequence.GetLine(i);
                if (line == null || string.IsNullOrWhiteSpace(line.Text)) continue;
                if (seen++ == _shown) { index = i; break; }
            }
            _shown++;
            var cue = cues.Find(c => c != null && c.sequence == _sequence && c.line == index);
            if (cue == null) { Release(false); return; }
            face.Show(cue.expression, cue.intensity, Mathf.Max(0.1f, cue.duration), 0.3f, this);
        }
    }
}
