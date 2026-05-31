using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MHZE.EventSystem
{
    [Serializable]
    public class Listener
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Component _target;
        [SerializeField] internal GameObject _gameObject;
        [SerializeField] private string _methodName;
        [SerializeField] private string _methodDisplayName;
        [SerializeField] private string _customLabel;
        [SerializeField] private ParameterEntry[] _parameters = Array.Empty<ParameterEntry>();

        [NonSerialized] private MethodInfo _cachedMethod;
        [NonSerialized] private ParameterInfo[] _cachedParamInfo;
        [NonSerialized] private object[] _cachedArgs;
        [NonSerialized] private bool _initialized;

        [NonSerialized] private Action _cachedAction;

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
            _cachedAction = null;
            _initialized = false;

            for (int i = 0; i < _parameters.Length; i++)
                _parameters[i].InvalidateCache();
        }

        internal void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            _cachedAction = null;
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

            if (_cachedMethod == null)
                return;

            _cachedParamInfo = _cachedMethod.GetParameters();

            if (_cachedParamInfo.Length == 0)
            {
                _cachedArgs = null;
                _cachedAction = (Action)Delegate.CreateDelegate(typeof(Action), _target, _cachedMethod, false);
            }
            else
            {
                _cachedArgs = new object[_cachedParamInfo.Length];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke()
        {
            if (!_enabled)
                return;

            if (!_initialized)
                Initialize();

            Action action = _cachedAction;
            if (action != null)
            {
                action();
                return;
            }

            if (_cachedMethod == null)
                return;

            int paramCount = _cachedParamInfo.Length;
            for (int i = 0; i < paramCount; i++)
            {
                if (i < _parameters.Length && _parameters[i] != null)
                    _cachedArgs[i] = _parameters[i].Resolve(_cachedParamInfo[i].ParameterType);
                else
                    _cachedArgs[i] = GetDefaultValue(_cachedParamInfo[i].ParameterType);
            }

            _cachedMethod.Invoke(_target, _cachedArgs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(object eventArg)
        {
            Invoke(new object[] { eventArg });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(object[] eventArgs)
        {
            if (!_enabled)
                return;

            if (!_initialized)
                Initialize();

            Action action = _cachedAction;
            if (action != null)
            {
                action();
                return;
            }

            if (_cachedMethod == null)
                return;

            int paramCount = _cachedParamInfo.Length;
            for (int i = 0; i < paramCount; i++)
            {
                if (i < _parameters.Length && _parameters[i] != null)
                {
                    if (_parameters[i].Source == ArgumentSource.Event)
                        _cachedArgs[i] = ResolveEventArg(eventArgs, _parameters[i]._eventArgIndex, _parameters[i]._eventVariableName, _cachedParamInfo[i].ParameterType);
                    else
                        _cachedArgs[i] = _parameters[i].Resolve(_cachedParamInfo[i].ParameterType);
                }
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

        private static object ResolveEventArg(object[] eventArgs, int argIndex, string variableName, Type expectedType)
        {
            if (eventArgs == null || argIndex < 0 || argIndex >= eventArgs.Length)
                return GetDefaultValue(expectedType);

            object eventArg = eventArgs[argIndex];

            if (string.IsNullOrEmpty(variableName))
                return eventArg;

            if (eventArg == null)
                return GetDefaultValue(expectedType);

            var argType = eventArg.GetType();

            var field = argType.GetField(variableName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(eventArg);

            var prop = argType.GetProperty(variableName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0)
                return prop.GetValue(eventArg);

            return eventArg;
        }
    }
}
