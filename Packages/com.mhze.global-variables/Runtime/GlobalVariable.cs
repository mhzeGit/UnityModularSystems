// Persistent global variable asset. Saved as a ScriptableObject in the project (Assets/GlobalVariables), created and kept in sync by GlobalVariableWriter, and read or set by GlobalVariableReader. Values are stored as invariant-culture strings and converted back to the recorded type on read.

using System;
using System.Globalization;
using UnityEngine;

namespace MHZE.GlobalVariables
{
    public class GlobalVariable : ScriptableObject
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string valueTypeName = string.Empty;
        [SerializeField] private string value = string.Empty;

        public event Action<object> OnValueChanged;

        public string DisplayName => displayName;
        public string ValueTypeName => valueTypeName;
        public string ValueString => value;

        public void SetDisplayName(string name)
        {
            displayName = name ?? string.Empty;
        }

        public void SetValueType(Type type)
        {
            valueTypeName = type != null ? type.AssemblyQualifiedName : string.Empty;
        }

        public Type GetValueType()
        {
            return string.IsNullOrEmpty(valueTypeName) ? null : Type.GetType(valueTypeName);
        }

        public object GetValue()
        {
            Type type = GetValueType();
            if (type == null || string.IsNullOrEmpty(value)) return value;

            if (type == typeof(string)) return value;

            try
            {
                if (type.IsEnum) return Enum.Parse(type, value, true);
                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"GlobalVariable '{displayName}': could not convert stored value '{value}' to type '{type.Name}'. ({exception.Message})");
                return value;
            }
        }

        public bool TryGetValue<T>(out T result)
        {
            result = default;

            object raw = GetValue();
            if (raw == null) return false;

            if (raw is T typed)
            {
                result = typed;
                return true;
            }

            try
            {
                result = (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SetValue(object newValue)
        {
            string serialized = SerializeValue(newValue);
            if (serialized == value) return;

            value = serialized;
            OnValueChanged?.Invoke(newValue);
        }

        public static string SerializeValue(object newValue)
        {
            if (newValue == null) return string.Empty;

            if (newValue is Enum) return newValue.ToString();

            return Convert.ToString(newValue, CultureInfo.InvariantCulture);
        }
    }
}
