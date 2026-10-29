using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MHZE.BasicMenus
{
    /// <summary>Persistent, art-free async scene transition with unscaled fades and a configurable loading screen.</summary>
    public sealed class SceneTransition : UIScreen
    {
        [Serializable]
        public struct Slide
        {
            public string sceneName;
            public string title;
            public string caption;
            public string imageResource;
        }

        public CanvasGroup overlay;
        public CanvasGroup curtain;
        public GameObject page;
        public RawImage photograph;
        public TMP_Text destination;
        public TMP_Text caption;
        public TMP_Text status;
        public Image progressBar;
        public Slide[] slides;
        [Min(0.05f)] public float fadeSeconds = 0.4f;
        [Min(0f)] public float minimumDisplaySeconds = 1f;

        private static SceneTransition instance;
        private Texture2D picture;
        private float previousTimeScale;
        private ThreadPriority previousPriority;
        public static bool IsLoading => instance != null && instance.IsOpen;
        public string Phase { get; private set; } = "Idle";
        public override int BackPriority => 10000;
        public override bool OnBack() => IsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        protected override void Awake()
        {
            startHidden = false;
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            overlay.alpha = 1;
            curtain.alpha = 1;
            page.SetActive(false);
        }

        private IEnumerator Start()
        {
            if (instance != this) yield break;
            IsOpen = true;
            yield return null;
            yield return Fade(0);
            overlay.alpha = 0;
            overlay.blocksRaycasts = false;
            IsOpen = false;
        }

        /// <summary>Replace the loaded scenes. Returns false for a duplicate request or an unavailable scene.</summary>
        public static bool LoadScene(string sceneName)
        {
            if (IsLoading || string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
                return false;
            if (instance == null)
            {
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                return true;
            }
            instance.StartCoroutine(instance.Transition(sceneName, instance.Load(sceneName), true));
            return true;
        }

        /// <summary>Cover an additive load and its caller's setup; the persistent screen owns completion even if the caller stops.</summary>
        public static IEnumerator Cover(string sceneName, IEnumerator operation)
        {
            while (IsLoading) yield return null;
            if (instance == null) { yield return operation; yield break; }
            instance.StartCoroutine(instance.Transition(sceneName, operation, false));
            while (IsLoading) yield return null;
        }

        private IEnumerator Load(string sceneName)
        {
            // Single unloads the old scenes and runs Unity's unused-asset cleanup once.
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (!load.isDone)
            {
                progressBar.fillAmount = Mathf.Clamp01(load.progress / 0.9f) * 0.9f;
                yield return null;
            }
        }

        private IEnumerator Transition(string sceneName, IEnumerator operation, bool replaceScene)
        {
            IsOpen = true;
            overlay.alpha = 1;
            overlay.blocksRaycasts = true;
            previousTimeScale = Time.timeScale;
            previousPriority = Application.backgroundLoadingPriority;
            Time.timeScale = 0;
            Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
            try
            {
                Phase = "FadeOut";
                yield return Fade(1);
                yield return ShowSlide(sceneName);
                page.SetActive(true);
                progressBar.fillAmount = 0;
                status.text = "Loading";
                yield return Fade(0);
                float shownAt = Time.realtimeSinceStartup;
                Phase = "Loading";
                Exception failure = null;
                yield return Drive(operation, error => failure = error);
                if (failure != null)
                {
                    Debug.LogException(failure);
                    status.text = "Unable to load. Returning…";
                    yield return new WaitForSecondsRealtime(2);
                }
                else
                {
                    status.text = "Ready";
                    progressBar.fillAmount = 1;
                    while (Time.realtimeSinceStartup - shownAt < minimumDisplaySeconds) yield return null;
                }
                Phase = "Reveal";
                yield return Fade(1);
                page.SetActive(false);
                Time.timeScale = replaceScene && failure == null ? 1 : previousTimeScale;
                // Let Start, layout and the destination camera settle behind black.
                yield return null;
                yield return null;
                yield return Fade(0);
            }
            finally
            {
                Time.timeScale = replaceScene ? 1 : previousTimeScale;
                Application.backgroundLoadingPriority = previousPriority;
                photograph.texture = null;
                if (picture != null) Resources.UnloadAsset(picture);
                picture = null;
                page.SetActive(false);
                overlay.alpha = 0;
                overlay.blocksRaycasts = false;
                IsOpen = false;
                Phase = "Idle";
            }
        }

        private IEnumerator ShowSlide(string sceneName)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(sceneName);
            var matches = new List<Slide>();
            if (slides != null)
                foreach (Slide slide in slides)
                    if (string.Equals(slide.sceneName, name, StringComparison.OrdinalIgnoreCase)) matches.Add(slide);
            destination.text = "Loading…";
            caption.text = "";
            if (matches.Count == 0) yield break;
            Slide selected = matches[UnityEngine.Random.Range(0, matches.Count)];
            destination.text = selected.title;
            caption.text = selected.caption;
            ResourceRequest request = Resources.LoadAsync<Texture2D>(selected.imageResource);
            yield return request;
            picture = request.asset as Texture2D;
            photograph.texture = picture;
        }

        private IEnumerator Fade(float target)
        {
            float from = curtain.alpha;
            for (float elapsed = 0; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                curtain.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, elapsed / fadeSeconds));
                yield return null;
            }
            curtain.alpha = target;
        }

        private static IEnumerator Drive(IEnumerator operation, Action<Exception> failed)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(operation);
            try
            {
                while (stack.Count > 0)
                {
                    object next = null;
                    bool moved = false;
                    Exception error = null;
                    try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                    catch (Exception exception) { error = exception; }
                    if (error != null) { failed(error); yield break; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (next is IEnumerator inner) stack.Push(inner);
                    else yield return next;
                }
            }
            finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
        }

        protected override void OnDestroy()
        {
            if (instance == this)
            {
                StopAllCoroutines();
                instance = null;
            }
            base.OnDestroy();
        }
    }
}
