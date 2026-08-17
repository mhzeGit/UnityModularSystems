// Reads a persistent GlobalVariable asset. In the editor, the inspector detects all global variables created by GlobalVariableWriter and shows the current default value of the selected one. At runtime, the value can be read or set with GetValue / SetValue, and every change fires OnValueChanged.

using System;
using UnityEngine;

namespace MHZE.GlobalVariables
{
    public class GlobalVariableReader : MonoBehaviour
    {
        [SerializeField] private GlobalVariable globalVariable;

        public event Action<object> OnValueChanged;

        public GlobalVariable GlobalVariable => globalVariable;

        void OnEnable()
        {
            if (globalVariable != null)
                globalVariable.OnValueChanged += OnGlobalValueChanged;
        }

        void OnDisable()
        {
            if (globalVariable != null)
                globalVariable.OnValueChanged -= OnGlobalValueChanged;
        }

        void OnGlobalValueChanged(object newValue)
        {
            OnValueChanged?.Invoke(newValue);
        }

        public object GetValue()
        {
            return globalVariable != null ? globalVariable.GetValue() : null;
        }

        public T GetValue<T>()
        {
            return TryGetValue<T>(out T result) ? result : default;
        }

        public bool TryGetValue<T>(out T result)
        {
            result = default;

            if (globalVariable == null) return false;

            return globalVariable.TryGetValue<T>(out result);
        }

        public void SetValue(object newValue)
        {
            if (globalVariable == null)
            {
                Debug.LogWarning($"GlobalVariableReader on '{gameObject.name}' has no GlobalVariable asset assigned. Assign it in the Inspector.", this);
                return;
            }

            globalVariable.SetValue(newValue);
        }

        public void SetValue<T>(T newValue)
        {
            SetValue((object)newValue);
        }
    }
}
