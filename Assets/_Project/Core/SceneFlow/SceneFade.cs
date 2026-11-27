using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    /// <summary>
    /// Black fade for every scene change made through GameSceneManager: fades out, starts the load,
    /// then fades back in once the new scene is up. It creates itself on first use; nothing to set up in scenes.
    /// </summary>
    public class SceneFade : MonoBehaviour
    {
        public const float Duration = 0.5f;
        private const float LoadTimeout = 30f;   // never leave the screen black if a load fails silently

        private static SceneFade instance;
        public static bool IsBusy { get; private set; }

        private CanvasGroup group;
        private bool sceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() { instance = null; IsBusy = false; }

        /// <summary>Fades to black, then calls <paramref name="start"/> (which begins the load). False if already fading.</summary>
        public static bool Run(Func<bool> start)
        {
            if (IsBusy) return false;
            if (instance == null) instance = Create();
            IsBusy = true;
            instance.StartCoroutine(instance.Routine(start));
            return true;
        }

        private static SceneFade Create()
        {
            var root = new GameObject("SceneFade", typeof(RectTransform));
            DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;   // above the Loading screen (32700)
            root.AddComponent<GraphicRaycaster>();

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)black.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            black.GetComponent<Image>().color = Color.black;

            var fade = root.AddComponent<SceneFade>();
            fade.group = group;
            return fade;
        }

        private IEnumerator Routine(Func<bool> start)
        {
            group.blocksRaycasts = true;   // no clicks while the screen is changing
            yield return Fade(0f, 1f);

            sceneLoaded = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            bool started = start();
            for (float waited = 0f; started && !sceneLoaded && waited < LoadTimeout; waited += Time.unscaledDeltaTime)
                yield return null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (started) yield return null;   // let the new scene draw one frame before revealing it

            yield return Fade(1f, 0f);
            group.blocksRaycasts = false;
            IsBusy = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneLoaded = true;

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / Duration);
                yield return null;
            }
            group.alpha = to;
        }

        private void OnDestroy()
        {
            if (instance == this) { instance = null; IsBusy = false; }
        }
    }
}
