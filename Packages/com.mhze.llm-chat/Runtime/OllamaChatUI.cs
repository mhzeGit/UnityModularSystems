using UnityEngine;

public class OllamaChatUI : MonoBehaviour
{
    [SerializeField] private OllamaChat chatController;

    private string _prompt = "Why is the sky blue?";
    private string _response;
    private string _status = "Ready";
    private string _focusedControl;
    private bool _submitRequested;

    private const string PromptControl = "OllamaPromptField";

    private void Awake()
    {
        if (chatController == null)
            chatController = FindObjectOfType<OllamaChat>();
    }

    private void OnEnable()
    {
        chatController.OnResponseReceived += HandleResponse;
        chatController.OnError += HandleError;
        chatController.OnLoadingChanged += HandleLoadingChanged;
    }

    private void OnDisable()
    {
        chatController.OnResponseReceived -= HandleResponse;
        chatController.OnError -= HandleError;
        chatController.OnLoadingChanged -= HandleLoadingChanged;
    }

    private void HandleResponse(string response)
    {
        _response = response;
        _status = "Response received";
    }

    private void HandleError(string error)
    {
        _response = $"Error: {error}";
        _status = "Error";
    }

    private void HandleLoadingChanged(bool loading)
    {
        _status = loading ? "Sending request..." : _status;
    }

    private void OnGUI()
    {
        _focusedControl = GUI.GetNameOfFocusedControl();

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
            && _focusedControl == PromptControl && !chatController.IsLoading)
        {
            _submitRequested = true;
            Event.current.Use();
        }

        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

        GUILayout.Label($"Status: {_status}");

        if (chatController.IsLoading)
        {
            GUILayout.Label("Waiting for response...");
        }
        else if (!string.IsNullOrEmpty(_response))
        {
            GUILayout.Space(10);
            GUILayout.Label("Response:");
            GUILayout.TextArea(_response, GUILayout.ExpandHeight(true));
        }

        GUILayout.FlexibleSpace();

        GUI.SetNextControlName(PromptControl);
        _prompt = GUILayout.TextField(_prompt, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Send") || _submitRequested)
        {
            _submitRequested = false;
            if (!string.IsNullOrWhiteSpace(_prompt))
            {
                _response = "";
                _status = "Sending...";
                chatController.SendPrompt(_prompt);
            }
        }

        GUILayout.EndArea();
    }
}
