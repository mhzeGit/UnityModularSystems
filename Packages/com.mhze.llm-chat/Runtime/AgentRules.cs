using UnityEngine;

[CreateAssetMenu(menuName = "Ollama/Agent Rules", fileName = "NewAgentRules")]
public class AgentRules : ScriptableObject
{
    [TextArea(5, 20)]
    [SerializeField] private string systemPrompt = "You are a helpful AI assistant.";
    [TextArea(3, 10)]
    [SerializeField] private string rules = "";
    [TextArea(3, 10)]
    [SerializeField] private string responseFormat = "";

    public string BuildFullSystemPrompt()
    {
        string result = systemPrompt;
        if (!string.IsNullOrWhiteSpace(rules))
            result += $"\n\nRules:\n{rules}";
        if (!string.IsNullOrWhiteSpace(responseFormat))
            result += $"\n\nResponse Format:\n{responseFormat}";
        return result;
    }
}
