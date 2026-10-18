using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MHZE.DialogSystem.Editor
{
    /// <summary>Shows live and completed dialog lines and saves edits back to their sequence assets.</summary>
    public sealed class DialogWorkbenchWindow : EditorWindow
    {
        private sealed class Entry
        {
            public DialogSequence Sequence;
            public int LineIndex;
            public int Conversation;
            public string SequenceName;
            public string Speaker;
            public string Text;
            public double StartedAt;
            public bool Finished;
        }

        private const int HistoryLimit = 500;
        private readonly List<Entry> _history = new List<Entry>();
        private DialogManager _manager;
        private Entry _current;
        private Entry _selected;
        private Vector2 _playingScroll;
        private Vector2 _playedScroll;
        private string _editSpeaker;
        private string _editText;
        private string _notice;
        private bool _noticeError;
        private int _conversation;
        private double _nextRepaint;

        [MenuItem("Tools/BlahBlah Family/Dialog/Live Dialog Workbench", false, 11)]
        public static void Open()
        {
            var window = GetWindow<DialogWorkbenchWindow>("Dialog Workbench");
            window.minSize = new Vector2(760, 540);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(760, 540);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Unbind;
            Bind(DialogManager.Instance);
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Unbind;
            Unbind();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (_current != null) _current.Finished = true;
                _current = null;
                Unbind();
            }
            if (state == PlayModeStateChange.EnteredPlayMode) Bind(DialogManager.Instance);
            Repaint();
        }

        private void Tick()
        {
            if (_manager != DialogManager.Instance) Bind(DialogManager.Instance);
            if (EditorApplication.timeSinceStartup < _nextRepaint) return;
            _nextRepaint = EditorApplication.timeSinceStartup + .1;
            Repaint();
        }

        private void Bind(DialogManager manager)
        {
            Unbind();
            _manager = manager;
            if (_manager == null) return;
            _manager.DialogStarted += OnDialogStarted;
            _manager.LineChanged += OnLineChanged;
            _manager.DialogFinished += OnDialogFinished;
            if (_manager.IsPlaying && _manager.CurrentSequence != null && _manager.CurrentLineIndex >= 0)
            {
                _conversation++;
                AddCurrentLine(_manager.CurrentSequence, _manager.CurrentLineIndex, _manager.CurrentSequence.GetLine(_manager.CurrentLineIndex)?.Speaker ?? "", _manager.CurrentSequence.GetLine(_manager.CurrentLineIndex)?.Text ?? "");
            }
        }

        private void Unbind()
        {
            if (_manager != null)
            {
                _manager.DialogStarted -= OnDialogStarted;
                _manager.LineChanged -= OnLineChanged;
                _manager.DialogFinished -= OnDialogFinished;
            }
            _manager = null;
        }

        private void OnDialogStarted(DialogSequence sequence)
        {
            _conversation++;
            if (_current != null) _current.Finished = true;
            _current = null;
        }

        private void OnLineChanged(string speaker, string text)
        {
            if (_manager == null || _manager.CurrentSequence == null) return;
            int index = _manager.CurrentLineIndex;
            if (_current != null && _current.Sequence == _manager.CurrentSequence && _current.LineIndex == index)
            {
                _current.Speaker = speaker;
                _current.Text = text;
                return;
            }

            if (_current != null) _current.Finished = true;
            AddCurrentLine(_manager.CurrentSequence, index, speaker, text);
        }

        private void AddCurrentLine(DialogSequence sequence, int index, string speaker, string text)
        {
            _current = new Entry
            {
                Sequence = sequence,
                LineIndex = index,
                Conversation = _conversation,
                SequenceName = sequence.name,
                Speaker = speaker,
                Text = text,
                StartedAt = EditorApplication.timeSinceStartup
            };
            _history.Add(_current);
            while (_history.Count > HistoryLimit) _history.RemoveAt(0);
        }

        private void OnDialogFinished(DialogSequence sequence)
        {
            if (_current != null && _current.Sequence == sequence)
            {
                _current.Finished = true;
                _current = null;
            }
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Clear History", EditorStyles.toolbarButton, GUILayout.Width(85)))
                {
                    _history.Clear(); _current = null; _selected = null;
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(EditorApplication.isPlaying ? "Live capture" : "Enter Fast Play to capture dialog", EditorStyles.miniLabel);
            }

            bool active = _manager != null && _manager.IsPlaying;
            EditorGUILayout.LabelField($"{(active ? "Dialog playing" : "No dialog playing")}   •   {_history.Count:N0} lines captured", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawEntryList("Playing", _history.Where(e => !e.Finished).ToList(), ref _playingScroll, true);
                GUILayout.Space(4);
                DrawEntryList("Played", _history.Where(e => e.Finished).Reverse().ToList(), ref _playedScroll, false);
            }
            DrawEditor();
            if (!string.IsNullOrEmpty(_notice)) EditorGUILayout.HelpBox(_notice, _noticeError ? MessageType.Error : MessageType.Info);
        }

        private void DrawEntryList(string title, List<Entry> rows, ref Vector2 scroll, bool playing)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true), GUILayout.MinWidth(200)))
            {
                EditorGUILayout.LabelField(title + "  (" + rows.Count + ")", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
                foreach (Entry entry in rows)
                {
                    var style = entry == _selected ? EditorStyles.helpBox : EditorStyles.label;
                    string elapsed = playing ? $"{Math.Max(0, EditorApplication.timeSinceStartup - entry.StartedAt):F1}s" : "played";
                    if (GUILayout.Button($"{entry.SequenceName}  ·  {entry.Speaker}\n{Trim(entry.Text, 92)}\n{elapsed}  ·  line {entry.LineIndex + 1}", style, GUILayout.Height(58), GUILayout.ExpandWidth(true)))
                    {
                        Select(entry);
                    }
                }
                EditorGUILayout.EndScrollView();
                if (rows.Count == 0) EditorGUILayout.LabelField(playing ? "No active dialog." : "No completed dialog yet.", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void Select(Entry entry)
        {
            _selected = entry;
            _editSpeaker = entry.Speaker;
            _editText = entry.Text;
            _notice = null;
        }

        private void DrawEditor()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Dialog content", EditorStyles.boldLabel);
                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("Select a playing or played line to edit its authored content.", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField($"{_selected.SequenceName}  ·  line {_selected.LineIndex + 1}", EditorStyles.miniLabel);
                bool persistent = _selected.Sequence != null && EditorUtility.IsPersistent(_selected.Sequence);
                if (persistent)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Ping Sequence", GUILayout.Width(110))) EditorGUIUtility.PingObject(_selected.Sequence);
                        GUILayout.Label(AssetDatabase.GetAssetPath(_selected.Sequence), EditorStyles.miniLabel);
                    }
                }
                _editSpeaker = EditorGUILayout.TextField("Speaker", _editSpeaker ?? "");
                _editText = EditorGUILayout.TextArea(_editText ?? "", GUILayout.MinHeight(70));
                bool current = _selected == _current;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!persistent || _selected.LineIndex < 0 || _selected.LineIndex >= _selected.Sequence.LineCount))
                    {
                        if (GUILayout.Button(current ? "Save & Update Live Dialog" : "Save to Dialog Asset")) SaveEdit();
                    }
                    if (current) GUILayout.Label("This line is on screen now.", EditorStyles.miniLabel);
                }
                if (!persistent)
                    EditorGUILayout.HelpBox("This is a runtime-only dialog and has no persistent sequence asset to save to.", MessageType.Warning);
            }
        }

        private void SaveEdit()
        {
            try
            {
                if (_selected?.Sequence == null || !EditorUtility.IsPersistent(_selected.Sequence))
                    throw new InvalidOperationException("The selected dialog is not backed by a saved sequence asset.");
                DialogLine line = _selected.Sequence.GetLine(_selected.LineIndex);
                if (line == null) throw new InvalidOperationException("The selected line no longer exists in its sequence.");

                Undo.RecordObject(_selected.Sequence, "Edit Dialog Line");
                line.SetContent(_editSpeaker, _editText);
                EditorUtility.SetDirty(_selected.Sequence);
                AssetDatabase.SaveAssets();
                _selected.Speaker = _editSpeaker;
                _selected.Text = _editText;
                if (_selected == _current && _manager != null)
                    _manager.RefreshCurrentLineFromSequence();
                _notice = "Dialog saved to " + AssetDatabase.GetAssetPath(_selected.Sequence) + (_selected == _current ? " and updated in the running game." : ".");
                _noticeError = false;
            }
            catch (Exception ex)
            {
                _notice = ex.GetBaseException().Message;
                _noticeError = true;
            }
        }

        private static string Trim(string text, int length)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string oneLine = text.Replace('\n', ' ').Replace('\r', ' ');
            return oneLine.Length <= length ? oneLine : oneLine.Substring(0, length - 1) + "…";
        }
    }
}
