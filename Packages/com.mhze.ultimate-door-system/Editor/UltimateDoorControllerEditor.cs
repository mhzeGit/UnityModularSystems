using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(UltimateDoorController))]
    public class UltimateDoorControllerEditor : UnityEditor.Editor
    {
        private VisualElement _rotationSection;
        private VisualElement _slidingSection;
        private VisualElement _triggerGroup;
        private VisualElement _passwordDetails;
        private VisualElement _proceduralSection;
        private VisualElement _animationSection;
        private VisualElement _animatorSection;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;

            var so = serializedObject;

            AddSectionHeader(root, "Animation");
            var animTypeField = new PropertyField(so.FindProperty("_animationType"));
            root.Add(animTypeField);

            _proceduralSection = new VisualElement();
            var modeField = new PropertyField(so.FindProperty("_mode"));
            _proceduralSection.Add(modeField);
            _rotationSection = new VisualElement();
            _rotationSection.Add(new PropertyField(so.FindProperty("_rotationAxis")));
            _rotationSection.Add(new PropertyField(so.FindProperty("_openAngle")));
            _proceduralSection.Add(_rotationSection);
            _slidingSection = new VisualElement();
            _slidingSection.Add(new PropertyField(so.FindProperty("_slideAxis")));
            _slidingSection.Add(new PropertyField(so.FindProperty("_slideDistance")));
            _proceduralSection.Add(_slidingSection);
            _proceduralSection.Add(new PropertyField(so.FindProperty("_animationDuration")));
            _proceduralSection.Add(new PropertyField(so.FindProperty("_movementCurve")));
            _proceduralSection.Add(new PropertyField(so.FindProperty("_allowIntruption"), "Allow Interruption"));
            root.Add(_proceduralSection);

            _animationSection = new VisualElement();
            _animationSection.Add(new PropertyField(so.FindProperty("_animationComponent")));
            _animationSection.Add(new PropertyField(so.FindProperty("_openClip")));
            _animationSection.Add(new PropertyField(so.FindProperty("_closeClip")));
            _animationSection.Add(new PropertyField(so.FindProperty("_unlockClip")));
            _animationSection.Add(new PropertyField(so.FindProperty("_lockClip")));
            _animationSection.Add(new PropertyField(so.FindProperty("_attemptOpenLockedClip")));
            root.Add(_animationSection);

            _animatorSection = new VisualElement();
            _animatorSection.Add(new PropertyField(so.FindProperty("_animatorComponent")));
            _animatorSection.Add(new PropertyField(so.FindProperty("_openTrigger")));
            _animatorSection.Add(new PropertyField(so.FindProperty("_closeTrigger")));
            _animatorSection.Add(new PropertyField(so.FindProperty("_openBool")));
            _animatorSection.Add(new PropertyField(so.FindProperty("_unlockTrigger")));
            _animatorSection.Add(new PropertyField(so.FindProperty("_lockTrigger")));
            _animatorSection.Add(new PropertyField(so.FindProperty("_attemptOpenLockedTrigger")));
            root.Add(_animatorSection);

            AddSectionHeader(root, "Automatic Door");
            var autoOpenField = new PropertyField(so.FindProperty("_autoOpen"), "Auto Open");
            root.Add(autoOpenField);
            var autoCloseField = new PropertyField(so.FindProperty("_autoClose"), "Auto Close");
            root.Add(autoCloseField);
            _triggerGroup = new VisualElement();
            _triggerGroup.Add(new PropertyField(so.FindProperty("_triggerLayer")));
            root.Add(_triggerGroup);

            AddSectionHeader(root, "Lock");
            root.Add(new PropertyField(so.FindProperty("_lockState")));
            var hasPasswordField = new PropertyField(so.FindProperty("_hasPassword"));
            root.Add(hasPasswordField);
            _passwordDetails = new VisualElement();
            BuildPasswordFields(_passwordDetails, so);
            root.Add(_passwordDetails);

            AddSectionHeader(root, "Events");
            root.Add(new PropertyField(so.FindProperty("OnOpened")));
            root.Add(new PropertyField(so.FindProperty("OnClosed")));
            root.Add(new PropertyField(so.FindProperty("OnLocked")));
            root.Add(new PropertyField(so.FindProperty("OnUnlocked")));
            root.Add(new PropertyField(so.FindProperty("OnLockFailed")));
            root.Add(new PropertyField(so.FindProperty("OnAttemptOpenLocked")));

            root.Bind(so);

            UpdateSectionVisibility(
                (UltimateDoorController.DoorMode)so.FindProperty("_mode").enumValueIndex,
                (UltimateDoorController.AnimationType)so.FindProperty("_animationType").enumValueIndex
            );
            RefreshTriggerVisibility();
            UpdatePasswordVisibility(so.FindProperty("_hasPassword").boolValue);

            modeField.RegisterValueChangeCallback(evt =>
            {
                UpdateSectionVisibility(
                    (UltimateDoorController.DoorMode)evt.changedProperty.enumValueIndex,
                    (UltimateDoorController.AnimationType)so.FindProperty("_animationType").enumValueIndex
                );
            });

            animTypeField.RegisterValueChangeCallback(evt =>
            {
                UpdateSectionVisibility(
                    (UltimateDoorController.DoorMode)so.FindProperty("_mode").enumValueIndex,
                    (UltimateDoorController.AnimationType)evt.changedProperty.enumValueIndex
                );
            });

            autoOpenField.RegisterValueChangeCallback(evt => RefreshTriggerVisibility());
            autoCloseField.RegisterValueChangeCallback(evt => RefreshTriggerVisibility());

            hasPasswordField.RegisterValueChangeCallback(evt =>
            {
                UpdatePasswordVisibility(evt.changedProperty.boolValue);
            });

            return root;
        }

        private void UpdateSectionVisibility(
            UltimateDoorController.DoorMode mode,
            UltimateDoorController.AnimationType animType)
        {
            bool isProcedural = animType == UltimateDoorController.AnimationType.Procedural;
            _proceduralSection.style.display = isProcedural ? DisplayStyle.Flex : DisplayStyle.None;
            _animationSection.style.display = animType == UltimateDoorController.AnimationType.Animation
                ? DisplayStyle.Flex : DisplayStyle.None;
            _animatorSection.style.display = animType == UltimateDoorController.AnimationType.Animator
                ? DisplayStyle.Flex : DisplayStyle.None;

            if (isProcedural)
            {
                _rotationSection.style.display = mode == UltimateDoorController.DoorMode.Rotating
                    ? DisplayStyle.Flex : DisplayStyle.None;
                _slidingSection.style.display = mode == UltimateDoorController.DoorMode.Sliding
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshTriggerVisibility()
        {
            serializedObject.Update();
            bool show = serializedObject.FindProperty("_autoOpen").boolValue
                     || serializedObject.FindProperty("_autoClose").boolValue;
            _triggerGroup.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdatePasswordVisibility(bool hasPassword)
        {
            _passwordDetails.style.display = hasPassword ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildPasswordFields(VisualElement parent, SerializedObject so)
        {
            var hashProp = so.FindProperty("_passwordHash");

            var passwordField = new TextField("Password");
            passwordField.isPasswordField = true;
            passwordField.tooltip = "Set a new lock password.";
            parent.Add(passwordField);

            var instructions = new Label("Press Enter or focus away to hash and store the password.");
            instructions.style.fontSize = 10;
            instructions.style.color = new Color(0.6f, 0.6f, 0.6f);
            instructions.style.marginLeft = 16;
            instructions.style.marginBottom = 3;
            instructions.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(instructions);

            var hashField = new TextField("Password Hash");
            hashField.isReadOnly = true;
            hashField.tooltip = "Stored SHA256 hash (read-only).";
            parent.Add(hashField);

            System.Action refreshHashDisplay = () =>
            {
                so.Update();
                hashField.value = hashProp.stringValue;
            };

            passwordField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    ApplyPassword(so, hashProp, passwordField.value);
                    refreshHashDisplay();
                    passwordField.value = string.Empty;
                }
            });

            passwordField.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (!string.IsNullOrEmpty(passwordField.value))
                {
                    ApplyPassword(so, hashProp, passwordField.value);
                    refreshHashDisplay();
                    passwordField.value = string.Empty;
                }
            });

            so.Update();
            hashField.value = hashProp.stringValue;
        }

        private static void ApplyPassword(SerializedObject so, SerializedProperty hashProp, string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                hashProp.stringValue = Convert.ToBase64String(bytes);
            }

            so.ApplyModifiedProperties();
        }

        private static void AddSectionHeader(VisualElement root, string label)
        {
            root.Add(new VisualElement { style = { height = 6 } });

            var line = new VisualElement();
            line.style.height = 1;
            line.style.marginLeft = 4;
            line.style.marginRight = 4;
            line.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f, 0.3f);
            root.Add(line);

            var header = new Label(label);
            header.style.fontSize = 12;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 3;
            header.style.marginBottom = 3;
            root.Add(header);
        }
    }
