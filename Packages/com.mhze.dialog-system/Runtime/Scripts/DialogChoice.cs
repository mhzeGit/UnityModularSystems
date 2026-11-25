// One answer the player can give to a question asked with DialogManager.Ask: its label and whether it can be picked now (a disabled answer is shown greyed out and struck through).

using System;

namespace MHZE.DialogSystem
{
[Serializable]
public struct DialogChoice
{
    /// <summary>What the player can say, as shown on the answer's button.</summary>
    public string Label;

    /// <summary>False shows the answer greyed out and struck through; it cannot be picked.</summary>
    public bool Enabled;

    public DialogChoice(string label, bool enabled = true)
    {
        Label = label;
        Enabled = enabled;
    }
}
}
