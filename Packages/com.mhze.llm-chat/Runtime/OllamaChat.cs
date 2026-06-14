using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MHZE.LLMChat
{
    public class OllamaChat : MonoBehaviour
    {
        [SerializeField] private string _host = "http://localhost:11434";
        [SerializeField] private string _model = "llama3.2";

        public string Host { get => _host; set => _host = value; }
        public string Model { get => _model; set => _model = value; }

        private readonly List<ChatSession> _sessions = new List<ChatSession>();
        private readonly Queue<Action> _actions = new Queue<Action>();
        private readonly object _lock = new object();
        private int _nextId;

        private void Update()
        {
            lock (_lock)
            {
                while (_actions.Count > 0)
                    _actions.Dequeue()();
            }
        }

        private void OnDestroy()
        {
            CancelAll();
        }

        public ChatSession Send(string prompt)
        {
            var session = new ChatSession(_nextId++, _model, _host, prompt, EnqueueAction, RemoveSession);
            _sessions.Add(session);
            StartCoroutine(session.Run());
            return session;
        }

        public void CancelAll()
        {
            StopAllCoroutines();
            for (int i = _sessions.Count - 1; i >= 0; i--)
                _sessions[i].Cancel();
            _sessions.Clear();
        }

        private void EnqueueAction(Action action)
        {
            lock (_lock) { _actions.Enqueue(action); }
        }

        private void RemoveSession(ChatSession session)
        {
            _sessions.Remove(session);
        }
    }

    public class ChatSession
    {
        public int SessionId { get; }

        public event Action<string> OnResponseChunk;
        public event Action<string> OnResponseComplete;
        public event Action<string> OnError;

        public string FullResponse => _responseBuilder?.ToString();

        internal StringBuilder _responseBuilder;
        internal UnityWebRequest _request;

        private readonly string _model;
        private readonly string _host;
        private readonly string _prompt;
        private readonly Action<Action> _dispatch;
        private readonly Action<ChatSession> _onDone;
        private bool _completed;

        internal ChatSession(int id, string model, string host, string prompt,
            Action<Action> dispatch, Action<ChatSession> onDone)
        {
            SessionId = id;
            _model = model;
            _host = host;
            _prompt = prompt;
            _dispatch = dispatch;
            _onDone = onDone;
        }

        internal IEnumerator Run()
        {
            var payload = JsonUtility.ToJson(new OllamaRequest
            {
                model = _model,
                stream = true,
                messages = new OllamaMessage[]
                {
                    new OllamaMessage { role = "user", content = _prompt }
                }
            });
            var body = Encoding.UTF8.GetBytes(payload);

            _responseBuilder = new StringBuilder();
            _request = new UnityWebRequest($"{_host}/api/chat", "POST");
            _request.uploadHandler = new UploadHandlerRaw(body);
            _request.uploadHandler.contentType = "application/json";
            _request.downloadHandler = new OllamaStreamHandler(OnDataReceived);
            _request.SendWebRequest();

            while (!_request.isDone)
                yield return null;

            if (_request.result != UnityWebRequest.Result.Success && !_completed)
                DispatchError(_request.error);
        }

        public void Cancel()
        {
            _completed = true;
            if (_request != null)
            {
                _request.Abort();
                _request.Dispose();
                _request = null;
            }
        }

        private void OnDataReceived(string line)
        {
            if (_completed || string.IsNullOrEmpty(line)) return;

            try
            {
                var chunk = JsonUtility.FromJson<OllamaChunk>(line);
                if (chunk?.message == null) return;

                _responseBuilder.Append(chunk.message.content);
                var text = chunk.message.content;
                _dispatch(() => OnResponseChunk?.Invoke(text));

                if (chunk.done)
                {
                    _completed = true;
                    var full = _responseBuilder.ToString();
                    _dispatch(() =>
                    {
                        OnResponseComplete?.Invoke(full);
                        _onDone(this);
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OllamaChat] Parse error: {line}\n{e}");
            }
        }

        private void DispatchError(string error)
        {
            _completed = true;
            _dispatch(() =>
            {
                OnError?.Invoke(error);
                _onDone(this);
            });
        }

        [Serializable] private class OllamaRequest
        {
            public string model;
            public bool stream;
            public OllamaMessage[] messages;
        }

        [Serializable] private class OllamaMessage
        {
            public string role;
            public string content;
        }

        [Serializable] private class OllamaChunk
        {
            public OllamaMessage message;
            public bool done;
        }
    }

    internal class OllamaStreamHandler : DownloadHandlerScript
    {
        private readonly Action<string> _onLine;
        private byte[] _remainder;

        public OllamaStreamHandler(Action<string> onLine) : base(new byte[4096])
        {
            _onLine = onLine;
            _remainder = Array.Empty<byte>();
        }

        protected override bool ReceiveData(byte[] data, int length)
        {
            if (data == null || length == 0) return false;

            var combined = new byte[_remainder.Length + length];
            Buffer.BlockCopy(_remainder, 0, combined, 0, _remainder.Length);
            Buffer.BlockCopy(data, 0, combined, _remainder.Length, length);

            var text = Encoding.UTF8.GetString(combined, 0, combined.Length);
            var lines = text.Split('\n');

            for (int i = 0; i < lines.Length - 1; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length > 0) _onLine(trimmed);
            }

            _remainder = Encoding.UTF8.GetBytes(lines[^1]);
            return true;
        }

        protected override void CompleteContent()
        {
            if (_remainder != null && _remainder.Length > 0)
            {
                var trimmed = Encoding.UTF8.GetString(_remainder).Trim();
                if (trimmed.Length > 0) _onLine(trimmed);
            }
            _remainder = null;
        }
    }
}
