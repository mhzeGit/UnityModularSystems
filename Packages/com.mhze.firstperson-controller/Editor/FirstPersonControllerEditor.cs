using UnityEditor;
using UnityEngine;
using System.IO;

namespace MHZE.FirstPersonController.Editor
{
    [CustomEditor(typeof(FirstPersonController))]
    public class FirstPersonControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty physicsMode;
        private SerializedProperty characterController;
        private SerializedProperty playerRigidbody;
        private SerializedProperty playerCapsule;
        private SerializedProperty playerCamera;
        private SerializedProperty cameraPivot;
        private SerializedProperty settings;
        private SerializedProperty moveAction;
        private SerializedProperty lookAction;
        private SerializedProperty jumpAction;
        private SerializedProperty crouchAction;
        private SerializedProperty sprintAction;

        // --- GameObject menu entries -----------------------------

        private const float EyeHeight = 1.65f;

        [MenuItem("GameObject/MHZE/First Person Controller/Character Controller", false, 10)]
        private static void CreateCharacterFPC() => CreateFPC(FPCPhysicsMode.CharacterController);

        [MenuItem("GameObject/MHZE/First Person Controller/Rigidbody (Physics)", false, 11)]
        private static void CreatePhysicsFPC() => CreateFPC(FPCPhysicsMode.Rigidbody);

        private static void CreateFPC(FPCPhysicsMode mode)
        {
            string name = mode == FPCPhysicsMode.CharacterController
                ? "FPC_Character" : "FPC_Physics";

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create FPC");

            var fpc = go.AddComponent<FirstPersonController>();
            var so = new SerializedObject(fpc);

            so.FindProperty("physicsMode").enumValueIndex = (int)mode;
            so.ApplyModifiedProperties();

            var editor = (FirstPersonControllerEditor)CreateEditor(fpc);
            editor.MigrateComponents();
            DestroyImmediate(editor);

            // --- Settings: find or create -------------------------
            var settingsAsset = FindOrCreateSettings();
            if (settingsAsset != null)
            {
                so.Update();
                so.FindProperty("settings").objectReferenceValue = settingsAsset;
                so.ApplyModifiedProperties();
            }

            // --- Camera hierarchy at eye height ------------------
            GameObject pivot = new GameObject("CameraPivot");
            Undo.RegisterCreatedObjectUndo(pivot, "Create FPC Camera");
            pivot.transform.SetParent(go.transform);
            pivot.transform.localPosition = new Vector3(0f, EyeHeight, 0f);

            GameObject camObj = new GameObject("PlayerCamera");
            Undo.RegisterCreatedObjectUndo(camObj, "Create FPC Camera");
            camObj.transform.SetParent(pivot.transform);
            camObj.transform.localPosition = Vector3.zero;

            var cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 80f;
            cam.tag = "MainCamera";
            camObj.AddComponent<AudioListener>();

            so.Update();
            so.FindProperty("playerCamera").objectReferenceValue = cam;
            so.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            so.ApplyModifiedProperties();

            Selection.activeGameObject = go;
        }

        // --- Settings asset resolver ------------------------------

        /// <summary>Find the first FPCSettings in the project, or create one if none exist.</summary>
        internal static FPCSettings FindOrCreateSettings()
        {
            // Search all assets of type FPCSettings
            string[] guids = AssetDatabase.FindAssets("t:FPCSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<FPCSettings>(path);
            }

            // None found — create one in Assets/Settings/
            string folder = "Assets/Settings";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/FPCSettings.asset");

            var instance = ScriptableObject.CreateInstance<FPCSettings>();
            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FPC] Created new settings asset at {assetPath}", instance);
            return instance;
        }

        // --- MigrateComponents -----------------------------------

        internal void MigrateComponents()
        {
            FirstPersonController fpc = (FirstPersonController)target;
            GameObject go = fpc.gameObject;
            bool isChar = physicsMode.enumValueIndex == 0;

            if (isChar)
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) DestroyImmediate(rb);
                var cap = go.GetComponent<CapsuleCollider>();
                if (cap != null) DestroyImmediate(cap);

                var cc = go.GetComponent<CharacterController>();
                if (cc == null) cc = go.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.radius = 0.3f;
                cc.stepOffset = 0.2f;
                cc.slopeLimit = 45f;

                characterController.objectReferenceValue = cc;
                playerRigidbody.objectReferenceValue = null;
                playerCapsule.objectReferenceValue = null;
            }
            else
            {
                var cc = go.GetComponent<CharacterController>();
                if (cc != null) DestroyImmediate(cc);

                var rb = go.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = go.AddComponent<Rigidbody>();
                    rb.mass = 75f;
                    rb.useGravity = false;
                    rb.freezeRotation = true;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                var cap = go.GetComponent<CapsuleCollider>();
                if (cap == null)
                {
                    cap = go.AddComponent<CapsuleCollider>();
                    cap.height = 1.8f;
                    cap.center = new Vector3(0f, 0.9f, 0f);
                    cap.radius = 0.3f;
                }

                characterController.objectReferenceValue = null;
                playerRigidbody.objectReferenceValue = rb;
                playerCapsule.objectReferenceValue = cap;
            }

            serializedObject.ApplyModifiedProperties();

            var pivot = fpc.transform.Find("CameraPivot");
            if (pivot != null)
            {
                Undo.RecordObject(pivot, "Adjust CameraPivot height");
                pivot.localPosition = new Vector3(0f, EyeHeight, 0f);
            }
        }

        // --- Inspector -------------------------------------------

        private void OnEnable()
        {
            physicsMode         = serializedObject.FindProperty("physicsMode");
            characterController = serializedObject.FindProperty("characterController");
            playerRigidbody     = serializedObject.FindProperty("playerRigidbody");
            playerCapsule       = serializedObject.FindProperty("playerCapsule");
            playerCamera        = serializedObject.FindProperty("playerCamera");
            cameraPivot         = serializedObject.FindProperty("cameraPivot");
            settings            = serializedObject.FindProperty("settings");
            moveAction          = serializedObject.FindProperty("moveAction");
            lookAction          = serializedObject.FindProperty("lookAction");
            jumpAction          = serializedObject.FindProperty("jumpAction");
            crouchAction        = serializedObject.FindProperty("crouchAction");
            sprintAction        = serializedObject.FindProperty("sprintAction");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            string modeLabel = physicsMode.enumValueIndex == 0
                ? "Character Controller" : "Rigidbody (Physics)";

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Physics Mode");
                EditorGUILayout.LabelField(modeLabel, EditorStyles.boldLabel);
            }
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);

            if (physicsMode.enumValueIndex == 0)
                EditorGUILayout.PropertyField(characterController);
            else
            {
                EditorGUILayout.PropertyField(playerRigidbody);
                EditorGUILayout.PropertyField(playerCapsule);
            }

            EditorGUILayout.PropertyField(playerCamera);
            EditorGUILayout.PropertyField(cameraPivot);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(settings);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Input Actions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(moveAction);
            EditorGUILayout.PropertyField(lookAction);
            EditorGUILayout.PropertyField(jumpAction);
            EditorGUILayout.PropertyField(crouchAction);
            EditorGUILayout.PropertyField(sprintAction);

            serializedObject.ApplyModifiedProperties();
        }
    }
}