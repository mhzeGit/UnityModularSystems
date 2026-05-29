// Holds a list of InputPromptDefinition assets in one place and builds a quick-lookup dictionary so the manager can find a prompt by its key string. Automatically rebuilds the dictionary when the asset is loaded or edited in the inspector. Case-insensitive key matching.

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InputPromptCollection", menuName = "Input Prompts/Input Prompt Collection")]
public class InputPromptCollection : ScriptableObject
{
    [SerializeField] private List<InputPromptDefinition> prompts = new();

    private readonly Dictionary<string, InputPromptDefinition> lookup = new();

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public bool TryGetPrompt(string key, out InputPromptDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            definition = null;
            return false;
        }

        if (lookup.Count == 0)
        {
            RebuildLookup();
        }

        return lookup.TryGetValue(key.Trim().ToLowerInvariant(), out definition);
    }

    private void RebuildLookup()
    {
        lookup.Clear();

        for (var i = 0; i < prompts.Count; i++)
        {
            var prompt = prompts[i];
            if (prompt == null || string.IsNullOrWhiteSpace(prompt.Key))
            {
                continue;
            }

            var normalized = prompt.Key.Trim().ToLowerInvariant();
            lookup[normalized] = prompt;
        }
    }
}