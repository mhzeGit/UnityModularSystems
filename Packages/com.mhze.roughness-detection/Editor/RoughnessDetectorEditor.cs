using UnityEngine;
using UnityEditor;

namespace MHZE.RoughnessDetection.Editor
{
    [CustomEditor(typeof(RoughnessDetector))]
    [CanEditMultipleObjects]
    public class RoughnessDetectorEditor : UnityEditor.Editor
    {
        private SerializedProperty m_MaxDistance;
        private SerializedProperty m_LayerMask;
        private SerializedProperty m_UpdateInterval;
        private SerializedProperty m_RoughnessOutputShader;
        private SerializedProperty m_ShowRay;
        private SerializedProperty m_RayColor;
        private SerializedProperty m_ShowHitPoint;
        private SerializedProperty m_HitPointColor;
        private SerializedProperty m_ShowRoughnessLabel;
        private SerializedProperty m_HitPointRadius;
        private SerializedProperty m_ShowGUI;
        private SerializedProperty m_GuiFontSize;

        private void OnEnable()
        {
            m_MaxDistance = serializedObject.FindProperty("maxDistance");
            m_LayerMask = serializedObject.FindProperty("layerMask");
            m_UpdateInterval = serializedObject.FindProperty("updateInterval");
            m_RoughnessOutputShader = serializedObject.FindProperty("roughnessOutputShader");
            m_ShowRay = serializedObject.FindProperty("showRay");
            m_RayColor = serializedObject.FindProperty("rayColor");
            m_ShowHitPoint = serializedObject.FindProperty("showHitPoint");
            m_HitPointColor = serializedObject.FindProperty("hitPointColor");
            m_ShowRoughnessLabel = serializedObject.FindProperty("showRoughnessLabel");
            m_HitPointRadius = serializedObject.FindProperty("hitPointRadius");
            m_ShowGUI = serializedObject.FindProperty("showGUI");
            m_GuiFontSize = serializedObject.FindProperty("guiFontSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var detector = (RoughnessDetector)target;

            DrawRoughnessPreview(detector);

            EditorGUILayout.Space(4);

            DrawDetectionSection();
            DrawGpuCaptureSection();
            DrawVisualizationSection();
            DrawGuiSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRoughnessPreview(RoughnessDetector detector)
        {
            var roughness = detector.CurrentRoughness;
            var hasHit = detector.HasHit;

            var bgRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Roughness Output", EditorStyles.boldLabel);

                if (hasHit && roughness >= 0f)
                {
                    var t = Mathf.Clamp01(roughness);
                    var color = Color.Lerp(new Color(0.2f, 0.6f, 1f), new Color(1f, 0.3f, 0.1f), t);

                    var progressRect = EditorGUILayout.BeginVertical();
                    EditorGUI.DrawRect(
                        EditorGUILayout.GetControlRect(false, 20),
                        new Color(0.15f, 0.15f, 0.15f)
                    );

                    var fillRect = EditorGUILayout.GetControlRect(false, 20);
                    fillRect.x += 2;
                    fillRect.width = (fillRect.width - 4) * t;
                    fillRect.y -= 20;
                    EditorGUI.DrawRect(fillRect, color);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space(2);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Smooth", GUILayout.Width(50));
                    EditorGUILayout.LabelField($"{roughness:F3}", new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 16,
                        normal = { textColor = color },
                        alignment = TextAnchor.MiddleCenter
                    });
                    EditorGUILayout.LabelField("Rough", GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button("Capture Now", GUILayout.Height(28)))
                    {
                        foreach (var targetObj in targets)
                        {
                            ((RoughnessDetector)targetObj).SendMessage("PerformDetection", SendMessageOptions.DontRequireReceiver);
                        }
                        SceneView.RepaintAll();
                        Repaint();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No hit detected. Position this GameObject so the forward ray intersects a surface.", MessageType.Info);
                }

                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawDetectionSection()
        {
            EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_MaxDistance);
            EditorGUILayout.PropertyField(m_LayerMask);
            EditorGUILayout.PropertyField(m_UpdateInterval);
        }

        private void DrawGpuCaptureSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("GPU Capture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_RoughnessOutputShader);

            if (m_RoughnessOutputShader.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign the RoughnessOutput shader for GPU-based pixel-accurate roughness capture. " +
                    "Without it, the detector falls back to CPU material sampling (less accurate).",
                    MessageType.Warning
                );
            }
        }

        private void DrawVisualizationSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ShowRay);
            EditorGUILayout.PropertyField(m_RayColor);
            EditorGUILayout.PropertyField(m_ShowHitPoint);
            EditorGUILayout.PropertyField(m_HitPointColor);
            EditorGUILayout.PropertyField(m_ShowRoughnessLabel);
            EditorGUILayout.PropertyField(m_HitPointRadius);
        }

        private void DrawGuiSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("GUI Overlay", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ShowGUI);
            EditorGUILayout.PropertyField(m_GuiFontSize);
        }

        private void OnSceneGUI()
        {
            var detector = (RoughnessDetector)target;

            if (!detector.HasHit || detector.CurrentRoughness < 0f)
                return;

            var roughness = detector.CurrentRoughness;
            var t = Mathf.Clamp01(roughness);
            var color = Color.Lerp(new Color(0.2f, 0.6f, 1f), new Color(1f, 0.3f, 0.1f), t);

            var hit = detector.LastHit;

            Handles.color = color;
            Handles.DrawSolidDisc(hit.point, hit.normal, 0.06f);

            var style = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            };

            var labelPos = hit.point + hit.normal * 0.25f;
            Handles.Label(labelPos, $"Roughness: {roughness:F3}", style);

            Handles.color = new Color(color.r, color.g, color.b, 0.4f);
            Handles.DrawLine(hit.point, hit.point + hit.normal * 0.25f);
        }
    }
}
