using System;
using System.Collections;
using System.Collections.Generic;
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

        [Serializable]
        private sealed class CharacterWalk
        {
            public Sprite portrait;
            public Sprite[] frames;
            public float verticalOffset;
        }

        [Header("UI (objects in the Loading scene/prefab)")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image runnerTemplate;
        [SerializeField] private CharacterWalk[] characterWalks = Array.Empty<CharacterWalk>();
        [SerializeField, Min(1f)] private float walkFrameRate = 24f;
        [SerializeField, Min(0f)] private float runnerSpacing = 70f;
        [SerializeField] private TMP_Text progressLabel;
        [Header("Timing")]
        [SerializeField, Min(0f)] private float minShowTime = 1f;
        [SerializeField, Min(0f)] private float fadeTime = 0.25f;
        [SerializeField, Min(0.01f)] private float fillSpeed = 1.5f;
        private readonly List<(Image Image, Sprite[] Frames)> runners = new();
        private RectTransform runnerTrack;
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
            CreateRunners(destination);
            walkStartedAt = Time.unscaledTime;
            SetProgress(0f);
        }

        private void CreateRunners(string path)
        {
            if (runnerTemplate == null) return;
            runnerTemplate.gameObject.SetActive(false);
            runnerTrack = (RectTransform)runnerTemplate.rectTransform.parent;

            // Use the same portraits as the selected Quest Paper, in the same order.
            var catalog = GameSceneManager.Catalog;
            var level = catalog?.FindLevelByScene(path);
            var secret = catalog?.FindSecretLevelByScene(path);
            if (level == null && secret == null)
            {
                if (characterWalks.Length > 0) AddRunner(characterWalks[0]);
                return;
            }

            var portraits = level != null ? level.characterAvailable : secret.characterAvailable;
            if (portraits == null) return;
            foreach (var portrait in portraits)
            {
                if (portrait == null) continue;
                foreach (var walk in characterWalks)
                {
                    if (walk?.portrait != portrait) continue;
                    AddRunner(walk);
                    break;
                }
            }
        }

        private void AddRunner(CharacterWalk walk)
        {
            if (walk?.portrait == null || walk.frames == null || walk.frames.Length == 0 || walk.frames[0] == null) return;
            var image = Instantiate(runnerTemplate, runnerTrack, false);
            image.name = walk.portrait.name;
            image.sprite = walk.frames[0];
            image.rectTransform.anchoredPosition += Vector2.up * walk.verticalOffset;
            image.gameObject.SetActive(true);
            runners.Add((image, walk.frames));
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
            int followerCount = runners.Count - 1;
            float spacing = followerCount > 0 ? Mathf.Min(runnerSpacing, runnerTrack.rect.width / followerCount) : 0f;
            int frame = Mathf.FloorToInt((Time.unscaledTime - walkStartedAt) * walkFrameRate);
            for (int i = 0; i < runners.Count; i++)
            {
                var (image, frames) = runners[i];
                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(Progress, rect.anchorMin.y);
                rect.anchorMax = new Vector2(Progress, rect.anchorMax.y);

                // Keep the whole group on the track, from the last runner at the start to the leader at the finish.
                float offset = spacing * (followerCount * (1f - Progress) - i);
                rect.anchoredPosition = new Vector2(offset, rect.anchoredPosition.y);
                if (Progress < 1f) image.sprite = frames[frame % frames.Length];
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
