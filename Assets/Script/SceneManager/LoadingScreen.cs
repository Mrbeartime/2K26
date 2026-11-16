using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    /// <summary>
    /// Lives in Loading.unity. Shows the scene-authored UI, loads the queued scene
    /// asynchronously, and keeps the screen above it until activation is complete.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private static LoadingScreen instance;
        private static string destination;
        public static bool IsLoading { get; private set; }
        public float Progress { get; private set; }

        [Header("UI (objects in the Loading scene/prefab)")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image rogue;
        [SerializeField] private Sprite[] rogueWalkFrames;
        [SerializeField, Min(1f)] private float rogueWalkFrameRate = 24f;
        [SerializeField] private TMP_Text progressLabel;
        [Header("Timing")]
        [SerializeField, Min(0f)] private float minShowTime = 1f;
        [SerializeField, Min(0f)] private float fadeTime = 0.25f;
        [SerializeField, Min(0.01f)] private float fillSpeed = 1.5f;
        private AsyncOperation pendingLoad;
        private float walkStartedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() { instance = null; destination = null; IsLoading = false; }

        public static bool TryLoad(string path)
        {
            if (IsLoading) return false;
            string loadingPath = GameSceneManager.LoadingScene;
            if (string.IsNullOrEmpty(loadingPath) || path == loadingPath ||
                !Application.CanStreamedLevelBeLoaded(loadingPath) ||
                string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError("Loading scene or destination is missing from SceneCatalog / Build Settings: " + path);
                return false;
            }
            destination = path;
            IsLoading = true;
            InGameSettingsSession.LeaveForSceneChange();
            Time.timeScale = 1f;
            try
            {
                SceneManager.LoadSceneAsync(loadingPath, LoadSceneMode.Single);
                return true;
            }
            catch (Exception error)
            {
                destination = null;
                IsLoading = false;
                Debug.LogException(error);
                return false;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            // The root prefab survives unloading Loading.unity while the target activates.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            if (group == null) group = GetComponentInChildren<CanvasGroup>(true);
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32700;
            }
            if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true; }
            walkStartedAt = Time.unscaledTime;
            SetProgress(0f);
        }

        private IEnumerator Start()
        {
            if (instance != this) yield break;
            // Opening Loading directly in the editor previews a transition to Start.
            string path = string.IsNullOrEmpty(destination) ? GameSceneManager.StartScene : destination;
            IsLoading = true;
            if (group == null)
            {
                Debug.LogError("LoadingScreen: assign a CanvasGroup in the Loading scene.", this);
                Finish();
                yield break;
            }
            yield return null; // Render the loading screen before starting expensive work.
            float started = Time.unscaledTime;
            pendingLoad = BeginDestinationLoad(path);
            if (pendingLoad == null) { Finish(); yield break; }
            pendingLoad.allowSceneActivation = false;
            try
            {
                float shown = 0f;
                while (pendingLoad.progress < 0.9f || shown < 1f)
                {
                    float sceneProgress = Mathf.Clamp01(pendingLoad.progress / 0.9f);
                    float timeProgress = minShowTime > 0f
                        ? Mathf.Clamp01((Time.unscaledTime - started) / minShowTime)
                        : 1f;
                    shown = Mathf.MoveTowards(shown, Mathf.Min(sceneProgress, timeProgress),
                        Mathf.Max(0.01f, fillSpeed) * Time.unscaledDeltaTime);
                    SetProgress(shown);
                    yield return null;
                }
                SetProgress(1f);
                pendingLoad.allowSceneActivation = true;
                while (!pendingLoad.isDone) yield return null;
                yield return null;
                float duration = Mathf.Max(0f, fadeTime);
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    group.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                    yield return null;
                }
            }
            finally
            {
                if (pendingLoad != null) pendingLoad.allowSceneActivation = true;
                Finish();
            }
        }

        private AsyncOperation BeginDestinationLoad(string path)
        {
            try { return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single); }
            catch (Exception error) { Debug.LogException(error); return null; }
        }
        private void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            if (rogue != null)
            {
                RectTransform rect = rogue.rectTransform;
                rect.anchorMin = new Vector2(Progress, rect.anchorMin.y);
                rect.anchorMax = new Vector2(Progress, rect.anchorMax.y);

                if (Progress < 1f && rogueWalkFrames != null && rogueWalkFrames.Length > 0)
                {
                    int frame = Mathf.FloorToInt((Time.unscaledTime - walkStartedAt) * rogueWalkFrameRate);
                    rogue.sprite = rogueWalkFrames[frame % rogueWalkFrames.Length];
                }
            }
            if (progressLabel != null) progressLabel.text = Mathf.RoundToInt(Progress * 100f) + "%";
        }
        private void Finish()
        {
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            if (instance == this) { instance = null; destination = null; IsLoading = false; }
            Destroy(gameObject);
        }
        private void OnDestroy()
        {
            if (pendingLoad != null && !pendingLoad.isDone) pendingLoad.allowSceneActivation = true;
            if (instance == this) { instance = null; destination = null; IsLoading = false; }
        }
    }
}
