using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MHZE.EventSystem
{
    [Serializable]
    public class EventBinding
    {
        [SerializeField] private List<Listener> _listeners = new List<Listener>();

        public int ListenerCount => _listeners.Count;

        public IReadOnlyList<Listener> Listeners => _listeners;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke()
        {
            List<Listener> listeners = _listeners;
            int count = listeners.Count;
            for (int i = 0; i < count; i++)
                listeners[i].Invoke();
        }

        public Listener AddListener()
        {
            var listener = new Listener();
            _listeners.Add(listener);
            return listener;
        }

        public void RemoveListener(Listener listener)
        {
            _listeners.Remove(listener);
        }

        public void RemoveListenerAt(int index)
        {
            if (index >= 0 && index < _listeners.Count)
                _listeners.RemoveAt(index);
        }

        public void MoveListener(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _listeners.Count) return;
            if (toIndex < 0 || toIndex >= _listeners.Count) return;
            if (fromIndex == toIndex) return;
            var listener = _listeners[fromIndex];
            _listeners.RemoveAt(fromIndex);
            _listeners.Insert(toIndex, listener);
        }

        public void Clear()
        {
            _listeners.Clear();
        }
    }

    [Serializable]
    public class EventBinding<T> : EventBinding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(T arg)
        {
            var listeners = Listeners;
            int count = listeners.Count;
            for (int i = 0; i < count; i++)
                listeners[i].Invoke(arg);
        }
    }
}
