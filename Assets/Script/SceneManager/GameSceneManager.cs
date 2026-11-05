using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectD.Menus
{
    public static class GameSceneManager
    {
        public const string StartScene = "Assets/Scenes/Mon/SceneGame/Start.unity";
        public const string SelectScene = "Assets/Scenes/Mon/SceneGame/LevelSelect.unity";
        public const string SecretScene = "Assets/Scenes/Mon/SceneGame/SecretLevel1.unity";
        public const int LevelCount = 5;
        public static int UnlockedLevel
        {
            get
            {
                for (int level = LevelCount; level >= 1; level--)
                    if (IsUnlocked(level)) return level;
                return 1;
            }
        }
        public static string LevelPath(int level) => $"Assets/Scenes/TestLevel/Level{level}.unity";
        public static bool IsUnlocked(int level) => LevelProgressStore.IsUnlocked(level);
        public static bool IsCompleted(int level) => LevelProgressStore.IsCompleted(level);
        public static bool IsSecretUnlocked(string id) => LevelProgressStore.IsSecretUnlocked(id);
        public static void UnlockSecret(string id)
        {
            LevelProgressStore.UnlockSecret(id);
        }
        public static void OpenSecretLevel(string path) => Load(path);

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
            CompleteLevelForScene(SceneManager.GetActiveScene().path);
        }

        // Use the goal object's own scene, including when scenes are loaded additively.
        public static bool CompleteLevelForScene(string scenePath)
        {
            for (int level = 1; level <= LevelCount; level++)
                if (scenePath == LevelPath(level))
                {
                    return LevelProgressStore.CompleteLevel(level);
                }
            if (scenePath == SecretScene) return LevelProgressStore.CompleteSecret("SecretLevel1");
            return false;
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
