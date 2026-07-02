using UnityEditor;
using UnityEngine;
using MHZE.AirParticlePhysics;

namespace MHZE.AirParticlePhysics.Editor
{
    [InitializeOnLoad]
    internal static class AirParticleViewportDebug
    {
        static AirParticleViewportDebug()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView view)
        {
            var emitters = Object.FindObjectsByType<AirParticleEmitter>(FindObjectsSortMode.None);
            foreach (var emitter in emitters)
            {
                if (!emitter.debugDraw) continue;

                Vector3 origin = emitter.transform.position;
                Vector3 direction = emitter.VelocityDirection;

                if (direction.sqrMagnitude < 0.001f) continue;

                float length = HandleUtility.GetHandleSize(origin) * 1.5f;
                Vector3 end = origin + direction * length;

                Handles.color = Color.green;
                Handles.DrawLine(origin, end);

                Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.001f)
                    right = Vector3.Cross(direction, Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(right, direction).normalized;

                float arrowSize = length * 0.2f;
                float arrowAngle = 30f * Mathf.Deg2Rad;

                Vector3 arrowLeft = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                      + right * arrowSize * Mathf.Sin(arrowAngle);
                Vector3 arrowRight = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                       - right * arrowSize * Mathf.Sin(arrowAngle);
                Vector3 arrowUp = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                    - up * arrowSize * Mathf.Sin(arrowAngle);
                Vector3 arrowDown = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                      + up * arrowSize * Mathf.Sin(arrowAngle);

                Handles.DrawLine(end, arrowLeft);
                Handles.DrawLine(end, arrowRight);
                Handles.DrawLine(end, arrowUp);
                Handles.DrawLine(end, arrowDown);
            }
        }
    }
}
