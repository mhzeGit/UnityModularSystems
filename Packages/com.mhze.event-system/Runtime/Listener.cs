using System;
using System.Reflection;
using UnityEngine;

namespace MHZE.EventSystem
{
    [Serializable]
    public class Listener
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Component _target;
        [SerializeField] private string _methodName;
        [SerializeField] private string _methodDisplayName;
        [SerializeField] private ParameterEntry[] _parameters = Array.Empty<ParameterEntry>();

        [NonSerialized] private MethodInfo _cachedMethod;
        [NonSerialized] private ParameterInfo[] _cachedParamInfo;
        [NonSerialized] private object[] _cachedArgs;
        [NonSerialized] private bool _initialized;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public Component Target
        {
            get => _target;
            internal set
            {
                _target = value;
                InvalidateCache();
            }
        }

        public string MethodName
        {
            get => _methodName;
            internal set
            {
                _methodName = value;
                InvalidateCache();
            }
        }

        public string MethodDisplayName
        {
            get => _methodDisplayName;
            internal set => _methodDisplayName = value;
        }

        public ParameterEntry[] Parameters => _parameters;

        internal void SetParameters(ParameterEntry[] parameters)
        {
            _parameters = parameters;
            InvalidateCache();
        }

        internal void InvalidateCache()
        {
            _cachedMethod = null;
            _cachedParamInfo = null;
            _cachedArgs = null;
            _initialized = false;

            for (int i = 0; i < _parameters.Length; i++)
                _parameters[i].InvalidateCache();
        }

        internal void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            _cachedArgs = null;

            if (_target == null || string.IsNullOrEmpty(_methodName))
            {
                _cachedMethod = null;
                _cachedParamInfo = null;
                return;
            }

            var type = _target.GetType();
            _cachedMethod = type.GetMethod(_methodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (_cachedMethod != null)
                _cachedParamInfo = _cachedMethod.GetParameters();
        }

        public void Invoke()
        {
            if (!_enabled)
                return;

            if (!_initialized)
                Initialize();

            if (_cachedMethod == null)
                return;

            int paramCount = _cachedParamInfo?.Length ?? 0;

            if (_cachedArgs == null || _cachedArgs.Length != paramCount)
                _cachedArgs = new object[paramCount];

            for (int i = 0; i < paramCount; i++)
            {
                if (i < _parameters.Length && _parameters[i] != null)
                    _cachedArgs[i] = _parameters[i].Resolve(_cachedParamInfo[i].ParameterType);
                else
                    _cachedArgs[i] = GetDefaultValue(_cachedParamInfo[i].ParameterType);
            }

            _cachedMethod.Invoke(_target, _cachedArgs);
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
