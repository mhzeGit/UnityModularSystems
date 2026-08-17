// Writes a BindableValue variable from any script into a persistent GlobalVariable asset. In the editor, assigning a source script and variable automatically creates/updates the asset and syncs the current value into it. At runtime it subscribes to the variable's change event and mirrors every change into the asset — no Update polling.

using System;
using System.Reflection;
using MHZE.TextWrite;
using UnityEngine;

namespace MHZE.GlobalVariables
{
    public class GlobalVariableWriter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour sourceComponent;
        [SerializeField] private string variableName = string.Empty;
        [SerializeField] private GlobalVariable globalVariable;

        BindableValue boundValue;

        public GlobalVariable GlobalVariable => globalVariable;

        void OnEnable()
        {
            SubscribeAndMirror();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public void SyncNow()
        {
            if (!ResolveBinding()) return;

            if (globalVariable == null)
            {
                Debug.LogWarning($"GlobalVariableWriter on '{gameObject.name}' has no GlobalVariable asset assigned. Assign it in the Inspector.", this);
                return;
            }

            WriteCurrentValue();
        }

        void SubscribeAndMirror()
        {
            if (globalVariable == null)
            {
                Debug.LogWarning($"GlobalVariableWriter on '{gameObject.name}' has no GlobalVariable asset assigned. Assign it in the Inspector.", this);
                return;
            }

            if (!ResolveBinding()) return;

            boundValue.Changed += OnBoundValueChanged;
            WriteCurrentValue();
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
            WriteCurrentValue();
        }

        void WriteCurrentValue()
        {
            if (boundValue == null || globalVariable == null) return;

            globalVariable.SetValue(boundValue.GetValue());
        }

        bool ResolveBinding()
        {
            if (sourceComponent == null)
            {
                Debug.LogWarning($"GlobalVariableWriter on '{gameObject.name}' has no source component assigned. Assign it in the Inspector.", this);
                return false;
            }

            if (string.IsNullOrEmpty(variableName))
            {
                Debug.LogWarning($"GlobalVariableWriter on '{gameObject.name}' has no variable selected. Pick a BindableValue member in the Inspector.", this);
                return false;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            Type type = sourceComponent.GetType();

            FieldInfo field = type.GetField(variableName, flags);
            if (field != null)
            {
                if (!typeof(BindableValue).IsAssignableFrom(field.FieldType))
                {
                    Debug.LogWarning($"GlobalVariableWriter on '{gameObject.name}': member '{variableName}' on '{sourceComponent.name}' is not a BindableValue. Use a BindableValue<T> field.", this);
                    return false;
                }

                boundValue = field.GetValue(sourceComponent) as BindableValue;
            }
            else
            {
                PropertyInfo property = type.GetProperty(variableName, flags);
                if (property == null || !typeof(BindableValue).IsAssignableFrom(property.PropertyType) || !property.CanRead)
                {
                    Debug.LogWarning($"GlobalVariableWriter on '{gameObject.name}': member '{variableName}' not found or not a readable BindableValue on '{sourceComponent.name}'. Re-select it in the Inspector.", this);
                    return false;
                }

                boundValue = property.GetValue(sourceComponent) as BindableValue;
            }

            if (boundValue == null)
            {
                Debug.LogWarning($"GlobalVariableWriter on '{gameObject.name}': BindableValue '{variableName}' on '{sourceComponent.name}' is null. Initialize it in the source script, e.g. 'public readonly BindableValue<int> Gold = new BindableValue<int>(0);'.", this);
                return false;
            }

            return true;
        }
    }
}
