// Central dialog controller. Plays a DialogSequence on a Canvas DialogView, reveals text with an optional typewriter effect, advances on input or after each line's duration, and raises started/finished/line-changed events. Access the active instance through DialogManager.Instance, or call Play from any script with a sequence reference.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MHZE.DialogSystem
{
[DisallowMultipleComponent]
public class DialogManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private DialogView view;

    [Header("Input")]
    [Tooltip("Optional advance action. When empty, Space, Enter and left mouse button advance the dialog.")]
    [SerializeField] private InputActionReference advanceAction;

    [Header("Behaviour")]
    [Tooltip("Keep the manager and its canvas alive when a new scene loads.")]
    [SerializeField] private bool keepAcrossScenes;

    [Tooltip("Characters revealed per second. 0 shows every line instantly.")]
    [Min(0f)]
    [SerializeField] private float charactersPerSecond = 45f;

    [Tooltip("Advance a line automatically once its duration has elapsed.")]
    [SerializeField] private bool autoAdvance = true;

    [Tooltip("Optional audio source used to play a line's voice-over clip.")]
    [SerializeField] private AudioSource voiceSource;

    private readonly List<DialogLine> runtimeLines = new();

    private DialogSequence currentSequence;
    private DialogSequence runtimeSequence;
    private string speakerOverride;
    private int currentIndex = -1;
    private string currentSpeaker;
    private string fullText = string.Empty;
    private float revealedCharacters;
    private float lineTimer;
    private bool lineRevealed;

    /// <summary>The active manager, or null when none exists in the scene.</summary>
    public static DialogManager Instance { get; private set; }

    public event Action<DialogSequence> DialogStarted;

    /// <summary>Raised when a conversation ends, whether it completed or was stopped.</summary>
    public event Action<DialogSequence> DialogFinished;

    /// <summary>Raised for every line with its speaker name and full (not typed-out) text.</summary>
    public event Action<string, string> LineChanged;

    public bool IsPlaying { get; private set; }

    public DialogSequence CurrentSequence => currentSequence;

    public DialogView View => view;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DialogManager] More than one DialogManager exists; destroying the duplicate on '" + name + "'.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        IsPlaying = false;
        currentIndex = -1;
        currentSequence = null;

        if (keepAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (view != null)
        {
            view.Hide();
            view.SetContinueVisible(false);
        }
    }

    private void OnDisable()
    {
        IsPlaying = false;
        currentIndex = -1;
        currentSequence = null;

        if (voiceSource != null)
        {
            voiceSource.Stop();
        }

        DestroyRuntimeSequence();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Starts the sequence from its first line. Replaces any conversation already playing.</summary>
    public void Play(DialogSequence sequence, string speaker = null)
    {
        if (sequence == null || sequence.LineCount == 0)
        {
            Stop();
            return;
        }

        if (runtimeSequence != null && runtimeSequence != sequence)
        {
            DestroyRuntimeSequence();
        }

        speakerOverride = string.IsNullOrWhiteSpace(speaker) ? null : speaker;
        currentSequence = sequence;
        currentIndex = -1;
        IsPlaying = true;

        if (view != null)
        {
            view.Show();
        }

        DialogStarted?.Invoke(sequence);
        NextLine();
    }

    /// <summary>Convenience overload for quick runtime dialog: Play("Girlfriend", "Hello!", "Welcome!").</summary>
    public void Play(string speaker, params string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            Stop();
            return;
        }

        DialogSequence sequence = ScriptableObject.CreateInstance<DialogSequence>();
        sequence.name = "RuntimeDialog";

        runtimeLines.Clear();
        for (int i = 0; i < lines.Length; i++)
        {
            runtimeLines.Add(new DialogLine(speaker, lines[i]));
        }

        sequence.SetLines(runtimeLines);
        runtimeSequence = sequence;
        Play(sequence, speaker);
    }

    /// <summary>Reveals the rest of the current line, or moves to the next line when it is already revealed.</summary>
    public void Advance()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (!lineRevealed)
        {
            RevealAll();
            return;
        }

        NextLine();
    }

    /// <summary>Ends the conversation immediately and hides the dialog UI.</summary>
    public void Stop()
    {
        if (!IsPlaying && currentSequence == null)
        {
            return;
        }

        DialogSequence stopped = currentSequence;
        End();
        if (stopped != null)
        {
            DialogFinished?.Invoke(stopped);
        }

        DestroyRuntimeSequence();
    }

    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (WasAdvancePressed())
        {
            Advance();
            return;
        }

        if (!lineRevealed)
        {
            Reveal();
            return;
        }

        DialogLine line = currentSequence != null ? currentSequence.GetLine(currentIndex) : null;
        if (autoAdvance && line != null && line.Duration > 0f)
        {
            lineTimer -= Time.unscaledDeltaTime;
            if (lineTimer <= 0f)
            {
                NextLine();
            }
        }
    }

    private void NextLine()
    {
        while (true)
        {
            currentIndex++;
            if (currentSequence == null || currentIndex >= currentSequence.LineCount)
            {
                Finish();
                return;
            }

            DialogLine line = currentSequence.GetLine(currentIndex);
            if (line != null && !string.IsNullOrWhiteSpace(line.Text))
            {
                Show(line);
                return;
            }
        }
    }

    private void Show(DialogLine line)
    {
        currentSpeaker = !string.IsNullOrWhiteSpace(speakerOverride)
            ? speakerOverride
            : (!string.IsNullOrWhiteSpace(line.Speaker) ? line.Speaker : currentSequence.DefaultSpeaker);

        fullText = line.Text;
        revealedCharacters = 0f;
        lineRevealed = charactersPerSecond <= 0f || fullText.Length == 0;
        lineTimer = line.Duration;

        if (view != null)
        {
            view.SetSpeaker(currentSpeaker);
            view.SetText(lineRevealed ? fullText : string.Empty);
            view.SetContinueVisible(lineRevealed);
        }

        if (voiceSource != null)
        {
            voiceSource.Stop();
            if (line.VoiceOver != null)
            {
                voiceSource.clip = line.VoiceOver;
                voiceSource.Play();
            }
        }

        LineChanged?.Invoke(currentSpeaker, fullText);
    }

    private void Reveal()
    {
        revealedCharacters += Mathf.Max(charactersPerSecond, 0f) * Time.unscaledDeltaTime;
        int count = Mathf.Clamp(Mathf.FloorToInt(revealedCharacters), 0, fullText.Length);
        if (count >= fullText.Length)
        {
            RevealAll();
            return;
        }

        if (view != null)
        {
            view.SetText(fullText.Substring(0, count));
        }
    }

    private void RevealAll()
    {
        lineRevealed = true;
        if (view != null)
        {
            view.SetText(fullText);
            view.SetContinueVisible(true);
        }
    }

    private void Finish()
    {
        DialogSequence finished = currentSequence;
        End();
        if (finished != null)
        {
            DialogFinished?.Invoke(finished);
        }

        DestroyRuntimeSequence();
    }

    private void End()
    {
        IsPlaying = false;
        currentIndex = -1;
        currentSequence = null;

        if (voiceSource != null)
        {
            voiceSource.Stop();
        }

        if (view != null)
        {
            view.SetContinueVisible(false);
            view.Hide();
        }
    }

    private void DestroyRuntimeSequence()
    {
        if (runtimeSequence == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeSequence);
        }
        else
        {
            DestroyImmediate(runtimeSequence);
        }

        runtimeSequence = null;
    }

    private bool WasAdvancePressed()
    {
        if (advanceAction != null && advanceAction.action != null)
        {
            return advanceAction.action.WasPressedThisFrame();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }
}
}
