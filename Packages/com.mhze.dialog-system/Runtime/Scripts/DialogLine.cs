// A single spoken line of dialog: who says it, what they say, how long it stays on screen before auto-advancing, and an optional voice-over clip.

using System;
using UnityEngine;

namespace MHZE.DialogSystem
{
[Serializable]
public class DialogLine
{
    [SerializeField] private string speaker = string.Empty;

    [TextArea(2, 5)]
    [SerializeField] private string text = string.Empty;

    [Tooltip("Seconds this line stays on screen before auto-advancing. 0 or less waits for the player to advance.")]
    [Min(0f)]
    [SerializeField] private float duration;

    [Tooltip("Optional voice-over played while the line is shown.")]
    [SerializeField] private AudioClip voiceOver;

    public DialogLine()
    {
    }

    public DialogLine(string speaker, string text, float duration = 0f)
    {
        this.speaker = speaker;
        this.text = text;
        this.duration = duration;
    }

    public string Speaker => speaker;

    public string Text => text;

    public float Duration => duration;

    public AudioClip VoiceOver => voiceOver;
}
}
