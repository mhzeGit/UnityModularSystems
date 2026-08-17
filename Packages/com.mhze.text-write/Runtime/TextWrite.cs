// Smart UI text binder. Add it to a GameObject with a Text or TMP component (it auto-detects and takes it), assign any script that has BindableValue fields, pick a variable in the inspector, and the text updates automatically each time the value changes through an event subscription — no Update polling.

using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MHZE.TextWrite
{
    public class TextWrite : MonoBehaviour
    {
        [SerializeField] private Text text;
        [SerializeField] private TMP_Text tmpText;

        [SerializeField] private MonoBehaviour sourceComponent;
        [SerializeField] private string variableName = string.Empty;
        [SerializeField] private string format = "{0}";

        FieldInfo boundField;
        PropertyInfo boundProperty;
        BindableValue boundValue;

        void Reset()
        {
            DetectTextComponent();
        }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public void DetectTextComponent()
        {
            if (text == null)
                text = GetComponent<Text>();

            if (tmpText == null)
                tmpText = GetComponent<TMP_Text>();
        }

        public void RefreshText()
        {
            if (boundValue == null) return;

            string output = SafeFormat(format, boundValue.GetValue(), this);

            if (text != null)
                text.text = output;
            else if (tmpText != null)
                tmpText.text = output;
        }

        void Subscribe()
        {
            if (text == null && tmpText == null)
            {
                DetectTextComponent();
                if (text == null && tmpText == null)
                {
                    Debug.LogWarning($"TextWrite on '{gameObject.name}' found no Text or TMP component. Add one to the same GameObject.", this);
                    return;
                }
            }

            if (sourceComponent == null)
            {
                Debug.LogWarning($"TextWrite on '{gameObject.name}' has no source component assigned. Assign it in the Inspector.", this);
                return;
            }

            if (string.IsNullOrEmpty(variableName))
            {
                Debug.LogWarning($"TextWrite on '{gameObject.name}' has no variable selected. Pick a BindableValue member in the Inspector.", this);
                return;
            }

            if (!ResolveMember()) return;

            object valueObject = GetMemberValue();
            if (valueObject == null)
            {
                Debug.LogWarning($"TextWrite on '{gameObject.name}': BindableValue '{variableName}' on '{sourceComponent.name}' is null. Initialize it in the source script, e.g. 'public readonly BindableValue<int> Gold = new BindableValue<int>(0);'.", this);
                return;
            }

            boundValue = (BindableValue)valueObject;
            boundValue.Changed += OnBoundValueChanged;
            RefreshText();
        }

        void Unsubscribe()
        {
            if (boundValue != null)
            {
                boundValue.Changed -= OnBoundValueChanged;
                boundValue = null;
            }
        }

        void OnBoundValueChanged()
        {
            RefreshText();
        }

        bool ResolveMember()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            Type type = sourceComponent.GetType();

            boundField = type.GetField(variableName, flags);
            if (boundField != null)
            {
                if (typeof(BindableValue).IsAssignableFrom(boundField.FieldType)) return true;

                Debug.LogWarning($"TextWrite on '{gameObject.name}': member '{variableName}' on '{sourceComponent.name}' is not a BindableValue, so it cannot be subscribed to. Use a BindableValue<T> field.", this);
                return false;
            }

            boundProperty = type.GetProperty(variableName, flags);
            if (boundProperty != null)
            {
                if (!typeof(BindableValue).IsAssignableFrom(boundProperty.PropertyType))
                {
                    Debug.LogWarning($"TextWrite on '{gameObject.name}': member '{variableName}' on '{sourceComponent.name}' is not a BindableValue, so it cannot be subscribed to. Use a BindableValue<T> property.", this);
                    return false;
                }

                if (!boundProperty.CanRead)
                {
                    Debug.LogWarning($"TextWrite on '{gameObject.name}': property '{variableName}' on '{sourceComponent.name}' is not readable.", this);
                    return false;
                }

                return true;
            }

            Debug.LogWarning($"TextWrite on '{gameObject.name}': member '{variableName}' not found on '{sourceComponent.name}'. Re-select the variable in the Inspector.", this);
            return false;
        }

        object GetMemberValue()
        {
            if (boundField != null)
                return boundField.GetValue(sourceComponent);

            if (boundProperty != null)
                return boundProperty.GetValue(sourceComponent);

            return null;
        }

        static string SafeFormat(string formatString, object value, TextWrite context)
        {
            if (string.IsNullOrEmpty(formatString))
                formatString = "{0}";

            try
            {
                return string.Format(formatString, value);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"TextWrite on '{context.gameObject.name}': invalid format string '{formatString}'. Use {{0}} as the placeholder. Showing the raw value instead.", context);
                return value?.ToString() ?? string.Empty;
            }
        }
    }
}
