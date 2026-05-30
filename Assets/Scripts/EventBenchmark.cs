using System;
using System.Diagnostics;
using System.Reflection;
using MHZE.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
public class EventBenchmark : MonoBehaviour
{
    [Header("Test Settings")]
    [Tooltip("How many times the active event fires per Update()")]
    [SerializeField] private int invocationsPerFrame = 50000;

    [Tooltip("How many empty listeners are subscribed to each event")]
    [SerializeField] private int listenerCount = 5;

    // 1) Unity serialised event
    public UnityEvent unityEvent = new UnityEvent();

    // 2) C# Action delegate
    public Action csharpAction;

    // 3) C# event keyword (slightly safer encapsulation, same perf as Action)
    public event Action csharpEvent;

    // 4) MHZE EventBinding (reflection-based dispatch)
    public EventBinding mhzeEvent = new EventBinding();

    private enum Mode { UnityEvent = 1, CSharpAction = 2, CSharpEvent = 3, MhzeEvent = 4 }
    private Mode _activeMode = Mode.UnityEvent;

    private readonly Stopwatch _sw = new Stopwatch();

    private float _fpsAccum;
    private int   _fpsFrames;
    private float _fpsDisplay;
    private const float FPS_INTERVAL = 0.5f;
    private float _fpsTimer;

    private long _lastTicks;
    private long _lastMs;

    private void Start()
    {
        RegisterListeners();
        RunBenchmark();
        UnityEngine.Debug.Log(
            "[EventBenchmark] Press 1 = UnityEvent | 2 = C# Action | 3 = C# event | 4 = MHZE EventBinding");
    }

    private void RegisterListeners()
    {
        for (int i = 0; i < listenerCount; i++)
        {
            unityEvent.AddListener(Dummy);
            csharpAction  += Dummy;
            csharpEvent   += Dummy;
        }

        for (int i = 0; i < listenerCount; i++)
        {
            Listener listener = mhzeEvent.AddListener();
            SetListenerTargetMethod(listener, this, nameof(Dummy));
        }
    }

    private static void SetListenerTargetMethod(Listener listener, Component target, string methodName)
    {
        typeof(Listener).GetProperty("Target", BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(listener, target);
        typeof(Listener).GetProperty("MethodName", BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(listener, methodName);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < listenerCount; i++)
            unityEvent.RemoveListener(Dummy);

        csharpAction = null;
        mhzeEvent.Clear();
    }

    private void Update()
    {
        ReadInput();
        RunBenchmark();
        UpdateFPS();
        UpdateOverlay();
    }

    private void ReadInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchMode(Mode.UnityEvent);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchMode(Mode.CSharpAction);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SwitchMode(Mode.CSharpEvent);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SwitchMode(Mode.MhzeEvent);
    }

    private void SwitchMode(Mode m)
    {
        _activeMode = m;
        UnityEngine.Debug.Log($"[EventBenchmark] Switched to: {m}");
    }

    private void RunBenchmark()
    {
        _sw.Restart();

        switch (_activeMode)
        {
            case Mode.UnityEvent:
                for (int i = 0; i < invocationsPerFrame; i++)
                    unityEvent.Invoke();
                break;

            case Mode.CSharpAction:
                for (int i = 0; i < invocationsPerFrame; i++)
                    csharpAction?.Invoke();
                break;

            case Mode.CSharpEvent:
                for (int i = 0; i < invocationsPerFrame; i++)
                    csharpEvent?.Invoke();
                break;

            case Mode.MhzeEvent:
                for (int i = 0; i < invocationsPerFrame; i++)
                    mhzeEvent.Invoke();
                break;
        }

        _sw.Stop();
        _lastTicks = _sw.ElapsedTicks;
        _lastMs    = _sw.ElapsedMilliseconds;
    }

    private void UpdateFPS()
    {
        _fpsAccum  += Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0f;
        _fpsFrames++;
        _fpsTimer  += Time.unscaledDeltaTime;

        if (_fpsTimer >= FPS_INTERVAL)
        {
            _fpsDisplay = _fpsAccum / _fpsFrames;
            _fpsAccum   = 0f;
            _fpsFrames  = 0;
            _fpsTimer   = 0f;
        }
    }

    private void UpdateOverlay()
    {
        //if (Time.frameCount % 60 == 0)
            //UnityEngine.Debug.Log("[EventBenchmark] " + BuildOverlayString().Replace('\n', ' '));
    }

    private string BuildOverlayString()
    {
        string label = _activeMode switch
        {
            Mode.UnityEvent    => "1 - UnityEvent",
            Mode.CSharpAction  => "2 - C# Action",
            Mode.CSharpEvent   => "3 - C# event",
            Mode.MhzeEvent     => "4 - MHZE EventBinding",
            _                  => "?"
        };

        return $"Active:    {label}\n" +
               $"FPS:       {_fpsDisplay:F1}\n" +
               $"Cost:      {_lastMs} ms  ({_lastTicks} ticks)\n" +
               $"Calls/frame: {invocationsPerFrame:N0}\n" +
               $"Listeners:   {listenerCount}\n\n" +
               "[1] UnityEvent  [2] C# Action  [3] C# event  [4] MHZE EventBinding";
    }

    public void Dummy() { }

    private void OnGUI()
    {
        GUI.color = Color.black;
        GUI.Label(new Rect(11, 11, 500, 200), BuildOverlayString());
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 500, 200), BuildOverlayString());
    }
}
