using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Net.Http;

namespace MHZE.LLMChat.Editor
{
    [CustomEditor(typeof(OllamaChat))]
    public class OllamaChatEditor : UnityEditor.Editor
    {
        private SerializedProperty _host;
        private SerializedProperty _model;
        private string[] _models = Array.Empty<string>();
        private int _selectedIndex;
        private bool _isLoading;
        private string _error;

        private void OnEnable()
        {
            _host = serializedObject.FindProperty("_host");
            _model = serializedObject.FindProperty("_model");
            TryRestoreSelection();
            FetchModels();
        }

        private void TryRestoreSelection()
        {
            var current = _model.stringValue;
            _selectedIndex = Array.IndexOf(_models, current);
            if (_selectedIndex < 0 && _models.Length > 0)
                _selectedIndex = 0;
        }

        private async void FetchModels()
        {
            _isLoading = true;
            _error = null;

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var host = _host.stringValue.TrimEnd('/');
                var json = await client.GetStringAsync($"{host}/api/tags");
                var parsed = JsonUtility.FromJson<OllamaModelsResponse>(json);
                var names = parsed?.models?.Select(m => m.name).ToArray();

                if (names != null && names.Length > 0)
                {
                    _models = names;
                    var current = _model.stringValue;
                    var idx = Array.IndexOf(_models, current);
                    _selectedIndex = idx >= 0 ? idx : 0;
                    _model.stringValue = _models[_selectedIndex];
                    serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    _error = "No models found";
                }
            }
            catch (Exception e)
            {
                _error = e.Message;
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var prevHost = _host.stringValue;
            EditorGUILayout.PropertyField(_host);
            if (_host.stringValue != prevHost)
                FetchModels();

            DrawModelField();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawModelField()
        {
            var rect = EditorGUILayout.GetControlRect();
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            var fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y,
                rect.width - EditorGUIUtility.labelWidth - 56f, rect.height);
            var btnRect = new Rect(fieldRect.xMax + 4f, rect.y, 52f, rect.height);

            EditorGUI.LabelField(labelRect, "Model");

            if (_isLoading)
            {
                EditorGUI.LabelField(fieldRect, "Loading models...");
            }
            else if (_error != null)
            {
                var color = GUI.color;
                GUI.color = Color.yellow;
                EditorGUI.LabelField(fieldRect, _error, EditorStyles.miniLabel);
                GUI.color = color;
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var selected = EditorGUI.Popup(fieldRect, _selectedIndex, _models);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedIndex = selected;
                    _model.stringValue = _models[_selectedIndex];
                }
            }

            if (GUI.Button(btnRect, "Fetch"))
                FetchModels();
        }

        [Serializable]
        private class OllamaModelsResponse
        {
            public OllamaModelEntry[] models;
        }

        [Serializable]
        private class OllamaModelEntry
        {
            public string name;
        }
    }
}
