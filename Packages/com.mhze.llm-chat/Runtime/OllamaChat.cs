using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

public class OllamaChat : MonoBehaviour
{
    [SerializeField] private string serverUrl = "http://localhost:11434";
    [SerializeField] private string model = "llama3.2";
    [SerializeField] private float requestTimeout = 30f;
    [SerializeField] private AgentRules agentRules;

    public bool IsLoading { get; private set; }
    public string LastResponse { get; private set; }
    public string LastError { get; private set; }

    public event Action<string> OnResponseReceived;
    public event Action<string> OnError;
    public event Action<bool> OnLoadingChanged;

    public void SendPrompt(string prompt)
    {
        if (IsLoading) return;
        StartCoroutine(SendRequestCoroutine(prompt));
    }

    private IEnumerator SendRequestCoroutine(string prompt)
    {
        IsLoading = true;
        LastResponse = "";
        LastError = "";
        OnLoadingChanged?.Invoke(true);

        string systemPrompt = agentRules != null ? agentRules.BuildFullSystemPrompt() : "";

        string json = JsonUtility.ToJson(new OllamaGenerateRequest
        {
            model = model,
            prompt = prompt,
            system = systemPrompt,
            stream = false
        });

        using UnityWebRequest req = new UnityWebRequest($"{serverUrl}/api/generate", "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = Mathf.RoundToInt(requestTimeout);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            OllamaGenerateResponse data = JsonUtility.FromJson<OllamaGenerateResponse>(req.downloadHandler.text);
            LastResponse = data.response;
            OnResponseReceived?.Invoke(LastResponse);
        }
        else
        {
            LastError = req.error;
            OnError?.Invoke(LastError);
        }

        IsLoading = false;
        OnLoadingChanged?.Invoke(false);
    }

    [Serializable]
    private class OllamaGenerateRequest
    {
        public string model;
        public string prompt;
        public string system;
        public bool stream;
    }

    [Serializable]
    private class OllamaGenerateResponse
    {
        public string model;
        public string created_at;
        public string response;
        public bool done;
    }
}
