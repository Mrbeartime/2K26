using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectD.Menus
{
    public static class GameSceneManager
    {
        public const string StartScene = "Assets/Scenes/Mon/SceneGame/Start.unity";
        public const string SelectScene = "Assets/Scenes/Mon/SceneGame/LevelSelect.unity";
        public const int LevelCount = 5;
        public static int UnlockedLevel => Mathf.Clamp(PlayerPrefs.GetInt("Progress.UnlockedLevel", 1), 1, LevelCount);
        public static string LevelPath(int level) => $"Assets/Scenes/TestLevel/Level{level}.unity";
        public static bool IsUnlocked(int level) => level >= 1 && level <= UnlockedLevel;
        public static bool IsCompleted(int level) => PlayerPrefs.GetInt($"Progress.Level{level}.Complete", 0) == 1;

        public static int ActiveLevel
        {
            get
            {
                string path = SceneManager.GetActiveScene().path;
                for (int i = 1; i <= LevelCount; i++)
                    if (path == LevelPath(i)) return i;
                return 0;
            }
        }

        // Called only by the game's confirmed victory path, never by opening a menu.
        public static void CompleteActiveLevel()
        {
            int level = ActiveLevel;
            if (level == 0) return;
            PlayerPrefs.SetInt($"Progress.Level{level}.Complete", 1);
            PlayerPrefs.SetInt("Progress.UnlockedLevel", Mathf.Max(UnlockedLevel, Mathf.Min(LevelCount, level + 1)));
            PlayerPrefs.Save();
        }

        public static void OpenStart() => Load(StartScene);
        public static void OpenLevelSelect() => Load(SelectScene);
        public static void OpenLevel(int level)
        {
            if (IsUnlocked(level)) Load(LevelPath(level));
        }

        private static void Load(string path)
        {
            if (!Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError("Scene is missing from Build Settings: " + path);
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(path);
        }

        public static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
