// Event-driven value wrapper. Add BindableValue<T> fields to any script, then change them with SetValue (or the Value property) — every change fires events that TextWrite subscribes to, so no Update polling is needed.

using System;
using System.Collections.Generic;

namespace MHZE.TextWrite
{
    public abstract class BindableValue
    {
        public event Action Changed;

        public abstract object GetValue();
        public abstract string GetStringValue();

        protected void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }

    public class BindableValue<T> : BindableValue
    {
        T value;

        public T Value
        {
            get => value;
            set => SetValue(value);
        }

        public event Action<T> OnValueChanged;

        public BindableValue() : this(default) { }

        public BindableValue(T initialValue)
        {
            value = initialValue;
        }

        public void SetValue(T newValue)
        {
            if (EqualityComparer<T>.Default.Equals(value, newValue)) return;

            value = newValue;
            OnValueChanged?.Invoke(value);
            RaiseChanged();
        }

        public override object GetValue() => value;
        public override string GetStringValue() => value?.ToString() ?? string.Empty;
    }
}
