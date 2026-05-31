using System;
using System.Reflection;
using UnityEngine;

namespace MHZE.EventSystem
{
    [Serializable]
    public class ParameterEntry
    {
        [SerializeField] internal string _parameterName;
        [SerializeField] internal string _parameterTypeName;

        [SerializeField] internal ArgumentSource _source = ArgumentSource.Constant;

        [SerializeField] internal Component _sourceComponent;
        [SerializeField] internal string _sourceMemberName;
        [SerializeField] internal string _eventVariableName;
        [SerializeField] internal int _eventArgIndex;

        [SerializeField] internal int _intValue;
        [SerializeField] internal float _floatValue;
        [SerializeField] internal double _doubleValue;
        [SerializeField] internal long _longValue;
        [SerializeField] internal bool _boolValue;
        [SerializeField] internal string _stringValue;
        [SerializeField] internal Vector2 _vector2Value;
        [SerializeField] internal Vector3 _vector3Value;
        [SerializeField] internal Vector4 _vector4Value;
        [SerializeField] internal Color _colorValue;
        [SerializeField] internal Rect _rectValue;
        [SerializeField] internal Bounds _boundsValue;
        [SerializeField] internal AnimationCurve _curveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] internal Gradient _gradientValue = new Gradient();
        [SerializeField] internal LayerMask _layerMaskValue;
        [SerializeField] internal UnityEngine.Object _objectValue;

        [NonSerialized] private FieldInfo _cachedField;
        [NonSerialized] private PropertyInfo _cachedProperty;
        [NonSerialized] private Component _cachedComponent;
        [NonSerialized] private bool _didCache;

        public string ParameterName => _parameterName;
        public string ParameterTypeName => _parameterTypeName;
        public ArgumentSource Source { get => _source; internal set => _source = value; }
        public Component SourceComponent { get => _sourceComponent; internal set => _sourceComponent = value; }
        public string SourceMemberName { get => _sourceMemberName; internal set => _sourceMemberName = value; }
        public string EventVariableName { get => _eventVariableName; internal set => _eventVariableName = value; }
        public int EventArgIndex { get => _eventArgIndex; internal set => _eventArgIndex = value; }

        public int IntValue { get => _intValue; set => _intValue = value; }
        public float FloatValue { get => _floatValue; set => _floatValue = value; }
        public double DoubleValue { get => _doubleValue; set => _doubleValue = value; }
        public long LongValue { get => _longValue; set => _longValue = value; }
        public bool BoolValue { get => _boolValue; set => _boolValue = value; }
        public string StringValue { get => _stringValue; set => _stringValue = value; }
        public Vector2 Vector2Value { get => _vector2Value; set => _vector2Value = value; }
        public Vector3 Vector3Value { get => _vector3Value; set => _vector3Value = value; }
        public Vector4 Vector4Value { get => _vector4Value; set => _vector4Value = value; }
        public Color ColorValue { get => _colorValue; set => _colorValue = value; }
        public Rect RectValue { get => _rectValue; set => _rectValue = value; }
        public Bounds BoundsValue { get => _boundsValue; set => _boundsValue = value; }
        public AnimationCurve CurveValue { get => _curveValue; set => _curveValue = value; }
        public Gradient GradientValue { get => _gradientValue; set => _gradientValue = value; }
        public LayerMask LayerMaskValue { get => _layerMaskValue; set => _layerMaskValue = value; }
        public UnityEngine.Object ObjectValue { get => _objectValue; set => _objectValue = value; }

        internal void InvalidateCache()
        {
            _cachedField = null;
            _cachedProperty = null;
            _cachedComponent = null;
            _didCache = false;
        }

        public object Resolve(Type expectedType)
        {
            if (_source == ArgumentSource.Script)
                return ResolveScriptValue(expectedType);

            return GetConstantValue(expectedType);
        }

        private void CacheMemberInfo()
        {
            if (_didCache || _sourceComponent == null)
                return;

            _didCache = true;
            _cachedComponent = _sourceComponent;
            var type = _sourceComponent.GetType();

            _cachedField = type.GetField(_sourceMemberName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (_cachedField == null)
            {
                _cachedProperty = type.GetProperty(_sourceMemberName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            }
        }

        private object ResolveScriptValue(Type expectedType)
        {
            if (_sourceComponent == null)
                return GetDefaultValue(expectedType);

            if (!_didCache || _cachedComponent != _sourceComponent)
            {
                _didCache = false;
                CacheMemberInfo();
            }

            if (_cachedField != null)
                return _cachedField.GetValue(_sourceComponent);

            if (_cachedProperty != null)
                return _cachedProperty.GetValue(_sourceComponent);

            return GetDefaultValue(expectedType);
        }

        private object GetConstantValue(Type expectedType)
        {
            if (expectedType == null)
                return null;

            if (expectedType == typeof(int) || expectedType == typeof(short) || expectedType == typeof(byte) || expectedType == typeof(sbyte))
                return _intValue;
            if (expectedType == typeof(uint) || expectedType == typeof(ushort))
                return (uint)_intValue;
            if (expectedType == typeof(float))
                return _floatValue;
            if (expectedType == typeof(double))
                return _doubleValue;
            if (expectedType == typeof(long))
                return _longValue;
            if (expectedType == typeof(ulong))
                return (ulong)_longValue;
            if (expectedType == typeof(bool))
                return _boolValue;
            if (expectedType == typeof(string))
                return _stringValue ?? string.Empty;
            if (expectedType == typeof(char))
                return _stringValue != null && _stringValue.Length > 0 ? _stringValue[0] : '\0';
            if (expectedType == typeof(Vector2))
                return _vector2Value;
            if (expectedType == typeof(Vector3))
                return _vector3Value;
            if (expectedType == typeof(Vector4))
                return _vector4Value;
            if (expectedType == typeof(Quaternion))
                return Quaternion.Euler(_vector3Value);
            if (expectedType == typeof(Color))
                return _colorValue;
            if (expectedType == typeof(Rect))
                return _rectValue;
            if (expectedType == typeof(Bounds))
                return _boundsValue;
            if (expectedType == typeof(AnimationCurve))
                return _curveValue;
            if (expectedType == typeof(Gradient))
                return _gradientValue;
            if (expectedType == typeof(LayerMask))
                return _layerMaskValue;

            if (expectedType == typeof(GameObject))
            {
                if (_objectValue is Component comp)
                    return comp.gameObject;
                return _objectValue;
            }

            if (typeof(Component).IsAssignableFrom(expectedType))
                return _objectValue as Component;

            if (typeof(UnityEngine.Object).IsAssignableFrom(expectedType) || expectedType == typeof(UnityEngine.Object))
                return _objectValue;

            if (expectedType.IsEnum)
                return Enum.ToObject(expectedType, _intValue);

            return _stringValue;
        }

        private static object GetDefaultValue(Type type)
        {
            if (type == null)
                return null;

            if (type.IsValueType)
                return Activator.CreateInstance(type);

            return null;
        }
    }
}
