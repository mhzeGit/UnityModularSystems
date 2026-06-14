using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class NPCAgent : MonoBehaviour
{
    [SerializeField] private string serverUrl = "http://localhost:11434";
    [SerializeField] private string model = "llama3.2";
    [SerializeField] private float requestTimeout = 30f;
    [SerializeField] private AgentRules agentRules;
    [SerializeField] private List<NPCToolDefinition> tools;

    public bool IsBusy { get; private set; }

    public event Action<string> OnDialog;
    public event Action<string> OnAgentError;

    private List<Dictionary<string, object>> _messages;
    private bool _hasBuiltinRunDialog;

    private void Awake()
    {
        _messages = new List<Dictionary<string, object>>();
        _hasBuiltinRunDialog = tools == null || tools.Count == 0
            || !tools.Exists(t => t.ToolName == "rundialog");
    }

    public void SendPlayerMessage(string playerText)
    {
        if (IsBusy) return;
        StartCoroutine(SendMessageCoroutine(playerText));
    }

    private IEnumerator SendMessageCoroutine(string playerText)
    {
        IsBusy = true;

        InitializeHistory();

        _messages.Add(new Dictionary<string, object>
        {
            ["role"] = "user",
            ["content"] = playerText
        });

        var toolDefs = BuildToolDefinitions();

        for (int i = 0; i < 10; i++)
        {
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = new List<Dictionary<string, object>>(_messages),
                ["stream"] = false
            };

            if (toolDefs.Count > 0)
                requestBody["tools"] = toolDefs;

            string json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            string responseJson;
            using (UnityWebRequest req = new UnityWebRequest($"{serverUrl}/api/chat", "POST"))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.RoundToInt(requestTimeout);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    OnAgentError?.Invoke(req.error);
                    IsBusy = false;
                    yield break;
                }

                responseJson = req.downloadHandler.text;
            }

            var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);
            var message = response["message"] as JObject;
            var assistantMsg = JsonConvert.DeserializeObject<Dictionary<string, object>>(message.ToString());
            _messages.Add(assistantMsg);

            var toolCalls = message["tool_calls"] as JArray;

            if (toolCalls == null || toolCalls.Count == 0)
            {
                string content = message["content"]?.ToString();
                if (!string.IsNullOrEmpty(content))
                    OnDialog?.Invoke(content);

                IsBusy = false;
                yield break;
            }

            foreach (var tc in toolCalls)
            {
                var func = tc["function"] as JObject;
                string name = func["name"]?.ToString();
                string args = func["arguments"]?.ToString();

                var toolDef = tools != null ? tools.Find(t => t.ToolName == name) : null;
                if (toolDef != null)
                {
                    toolDef.onExecute?.Invoke(args);
                }
                else if (_hasBuiltinRunDialog && name == "rundialog")
                {
                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(args);
                    if (parsed.TryGetValue("text", out var dialogText))
                        OnDialog?.Invoke(dialogText);
                }

                _messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "tool",
                    ["content"] = $"Executed {name} successfully.",
                    ["name"] = name
                });
            }
        }

        OnAgentError?.Invoke("Agent reached maximum response iterations.");
        IsBusy = false;
    }

    private void InitializeHistory()
    {
        if (_messages.Count != 0) return;

        string systemPrompt = agentRules != null
            ? agentRules.BuildFullSystemPrompt()
            : "You are a helpful NPC in a game world. You have access to functions you can call to interact.";

        _messages.Add(new Dictionary<string, object>
        {
            ["role"] = "system",
            ["content"] = systemPrompt
        });
    }

    private List<Dictionary<string, object>> BuildToolDefinitions()
    {
        var defs = new List<Dictionary<string, object>>();

        if (_hasBuiltinRunDialog)
        {
            defs.Add(new Dictionary<string, object>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object>
                {
                    ["name"] = "rundialog",
                    ["description"] = "Display dialog text to the player. Call this when your character speaks.",
                    ["parameters"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["text"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The dialog text the character says"
                            }
                        },
                        ["required"] = new List<string> { "text" }
                    }
                }
            });
        }

        if (tools != null)
        {
            foreach (var tool in tools)
            {
                if (string.IsNullOrEmpty(tool.ToolName)) continue;

                var parameters = string.IsNullOrEmpty(tool.ParametersJson)
                    ? new Dictionary<string, object>()
                    : JsonConvert.DeserializeObject<Dictionary<string, object>>(tool.ParametersJson);

                defs.Add(new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = tool.ToolName,
                        ["description"] = tool.Description,
                        ["parameters"] = parameters
                    }
                });
            }
        }

        return defs;
    }

    public void ClearHistory()
    {
        _messages.Clear();
    }
}
