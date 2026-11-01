using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    public class LoadingScreen : MonoBehaviour
    {
        private static LoadingScreen instance;
        public static bool IsLoading { get; private set; }

        [Header("UI")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image filler;

        [Header("Timing")]
        [Tooltip("Show the screen at least this long, so it does not just flash on fast loads")]
        [SerializeField] private float minShowTime = 1f;
        [SerializeField] private float fadeTime = 0.25f;
        [Tooltip("How fast the bar catches up with the real progress (full bar per second)")]
        [SerializeField] private float fillSpeed = 1.5f;

        private void Awake()
        {
            if (group == null) group = GetComponentInChildren<CanvasGroup>(true);
            if (group == null) return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        public static bool TryLoad(string scenePath)
        {
            if (IsLoading) return true;

            if (instance == null)
            {
                LoadingScreen prefab = Resources.Load<LoadingScreen>("LoadingScreen");
                if (prefab == null) return false;
                instance = Instantiate(prefab);
                DontDestroyOnLoad(instance.gameObject);
            }

            instance.StartCoroutine(instance.LoadRoutine(scenePath));
            return true;
        }

        private IEnumerator LoadRoutine(string scenePath)
        {
            IsLoading = true;
            SetProgress(0f);

            group.blocksRaycasts = true;
            yield return Fade(0f, 1f);

            float startTime = Time.unscaledTime;
            AsyncOperation op = SceneManager.LoadSceneAsync(scenePath);
            op.allowSceneActivation = false;

            float shown = 0f;
            while (op.progress < 0.9f || shown < 1f || Time.unscaledTime - startTime < minShowTime)
            {
                float target = Mathf.Clamp01(op.progress / 0.9f);
                shown = Mathf.MoveTowards(shown, target, fillSpeed * Time.unscaledDeltaTime);
                SetProgress(shown);
                yield return null;
            }

            SetProgress(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;

            yield return Fade(1f, 0f);
            group.blocksRaycasts = false;
            IsLoading = false;
        }

        private void SetProgress(float value)
        {
            if (filler != null) filler.fillAmount = value;
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / fadeTime);
                yield return null;
            }
            group.alpha = to;
        }
    }
}