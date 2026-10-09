using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectD.Menus
{
    // Added only to the five campaign levels; also works when Play starts directly in a level.
    public class LevelResultNavigation : MonoBehaviour
    {
        private TurnGameManager manager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameSceneManager.ActiveLevel > 0 && FindAnyObjectByType<LevelResultNavigation>() == null)
                new GameObject("Level Result Navigation").AddComponent<LevelResultNavigation>();
        }

        private void Start() => manager = FindAnyObjectByType<TurnGameManager>();

        private void OnGUI()
        {
            if (manager == null || !manager.IsGameComplete) return;
            int level = GameSceneManager.ActiveLevel;
            float width = Mathf.Min(520, Screen.width - 24);
            var area = new Rect((Screen.width - width) / 2, Screen.height - 100, width, 90);
            GUILayout.BeginArea(area);
            if (level == GameSceneManager.LevelCount && GameSceneManager.IsCompleted(level))
                GUILayout.Label("All 5 levels completed!");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Main Menu", GUILayout.Height(40))) GameSceneManager.OpenStart();
            if (GUILayout.Button("Select Level", GUILayout.Height(40))) GameSceneManager.OpenLevelSelect();
            if (level < GameSceneManager.LevelCount && GameSceneManager.IsUnlocked(level + 1) &&
                GUILayout.Button("Next Level", GUILayout.Height(40))) GameSceneManager.OpenLevel(level + 1);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
