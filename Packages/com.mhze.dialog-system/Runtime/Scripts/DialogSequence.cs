// A ScriptableObject holding an ordered list of dialog lines the DialogManager can play. Create one from Assets > Create > MHZE > Dialog System > Dialog Sequence.

using System.Collections.Generic;
using UnityEngine;

namespace MHZE.DialogSystem
{
[CreateAssetMenu(menuName = "MHZE/Dialog System/Dialog Sequence", fileName = "DialogSequence")]
public class DialogSequence : ScriptableObject
{
    [Tooltip("Speaker name used by lines that leave their own speaker empty.")]
    [SerializeField] private string defaultSpeaker = "NPC";

    [SerializeField] private List<DialogLine> lines = new();

    public string DefaultSpeaker => defaultSpeaker;

    public int LineCount => lines != null ? lines.Count : 0;

    public IReadOnlyList<DialogLine> Lines => lines;

    public DialogLine GetLine(int index)
    {
        if (lines == null || index < 0 || index >= lines.Count)
        {
            return null;
        }

        return lines[index];
    }

    /// <summary>Replaces the line list. Intended for runtime-built sequences; edit the asset for authored dialog.</summary>
    public void SetLines(IEnumerable<DialogLine> newLines)
    {
        lines = newLines != null ? new List<DialogLine>(newLines) : new List<DialogLine>();
    }
}
}
