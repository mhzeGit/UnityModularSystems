using System;
using System.Collections.Generic;
using UnityEngine;

namespace MHZE.EventSystem
{
    [Serializable]
    public class EventBinding
    {
        [SerializeField] private List<Listener> _listeners = new List<Listener>();

        public int ListenerCount => _listeners.Count;

        public IReadOnlyList<Listener> Listeners => _listeners;

        public void Invoke()
        {
            int count = _listeners.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    _listeners[i]?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
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

        public void Clear()
        {
            _listeners.Clear();
        }
    }
}
