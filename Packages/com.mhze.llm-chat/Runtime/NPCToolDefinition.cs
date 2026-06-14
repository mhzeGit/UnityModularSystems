using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Ollama/NPC Tool", fileName = "NewNPCTool")]
public class NPCToolDefinition : ScriptableObject
{
    [SerializeField] private string toolName;
    [TextArea(2, 5)]
    [SerializeField] private string description;
    [TextArea(3, 10)]
    [SerializeField] private string parametersJson = "{\"type\":\"object\",\"properties\":{},\"required\":[]}";

    public string ToolName => toolName;
    public string Description => description;
    public string ParametersJson => parametersJson;

    public UnityEvent<string> onExecute;
}
