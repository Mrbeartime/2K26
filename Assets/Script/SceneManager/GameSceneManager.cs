using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectD.Menus
{
    /// <summary>Public navigation API. SceneCatalog owns paths, IDs and level order.</summary>
    public static class GameSceneManager
    {
        private static SceneCatalog catalog;
        private static bool reportedMissingCatalog;
        private static AsyncOperation pendingNavigation;
        public static SceneCatalog Catalog
        {
            get
            {
                if (catalog == null)
                {
                    catalog = FindPreloadedCatalog();
                    if (catalog == null && !reportedMissingCatalog)
                    {
                        reportedMissingCatalog = true;
                        Debug.LogError("SceneCatalog is not configured. Select the catalog asset and click Sync Scenes To Build Settings. Keep exactly one catalog in Preloaded Assets.");
                    }
                }
                return catalog;
            }
        }

        // Preloaded Assets reference the asset by GUID, so its folder can change safely.
        private static SceneCatalog FindPreloadedCatalog()
        {
#if UNITY_EDITOR
            var assets = UnityEditor.PlayerSettings.GetPreloadedAssets();
#else
            var assets = Resources.FindObjectsOfTypeAll<SceneCatalog>();
#endif
            SceneCatalog found = null;
            foreach (var asset in assets)
                if (asset is SceneCatalog candidate)
                {
                    if (found != null) return null;
                    found = candidate;
                }
            return found;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeCatalog() => Catalog?.InvalidateQuestData();
        public static string StartScene => Catalog != null ? Catalog.startScene.Path : string.Empty;
        public static string SettingsScene => Catalog != null ? Catalog.settingsScene.Path : string.Empty;
        public static string SelectScene => Catalog != null ? Catalog.levelSelectScene.Path : string.Empty;
        public static string GuideScene => Catalog != null ? Catalog.guideScene.Path : string.Empty;
        public static string LoadingScene => Catalog != null ? Catalog.loadingScene.Path : string.Empty;
        public static int LevelCount => Catalog != null ? Catalog.Levels.Count : 0;
        public static bool IsInGameSettingsOpen => InGameSettingsSession.IsOpen;
        public static bool IsChangingScene => SceneFade.IsBusy || LoadingScreen.IsLoading || (pendingNavigation != null && !pendingNavigation.isDone);
        public static int ActiveLevel => Catalog != null ? Catalog.FindLevelByScene(SceneManager.GetActiveScene().path)?.id ?? 0 : 0;
        public static string LevelPath(int id) => Catalog != null ? Catalog.FindLevel(id)?.scene.Path ?? string.Empty : string.Empty;
        public static bool IsUnlocked(int id) => LevelProgressStore.IsUnlocked(id);
        public static bool IsCompleted(int id) => LevelProgressStore.IsCompleted(id);
        public static bool IsSecretUnlocked(string id) => LevelProgressStore.IsSecretUnlocked(id);
        public static void UnlockSecret(string id) => LevelProgressStore.UnlockSecret(id);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { catalog = null; pendingNavigation = null; reportedMissingCatalog = false; }

        public static int UnlockedLevel
        {
            get
            {
                if (Catalog == null) return 0;
                for (int i = Catalog.Levels.Count - 1; i >= 0; i--)
                {
                    var level = Catalog.Levels[i];
                    if (level != null && IsUnlocked(level.id)) return level.id;
                }
                return 0;
            }
        }

        public static void OpenInGameSettings() => InGameSettingsSession.Open();
        public static void CloseInGameSettings() => InGameSettingsSession.Close();
        public static void CompleteActiveLevel() => CompleteLevelForScene(SceneManager.GetActiveScene().path);
        public static bool CompleteLevelForScene(string path)
        {
            if (Catalog == null) return false;
            var level = Catalog.FindLevelByScene(path);
            if (level != null) return LevelProgressStore.CompleteLevel(level.id);
            var secret = Catalog.FindSecretLevelByScene(path);
            return secret != null && LevelProgressStore.CompleteSecret(secret.id);
        }

        public static void OpenStart() => Load(StartScene);
        public static void OpenLevelSelect() => Load(SelectScene);
        public static void OpenGuide() => Load(GuideScene);
        public static void OpenSecretLevel(string path) => OpenSecret(path, false);
        public static void ReloadCurrentScene() => Load(SceneManager.GetActiveScene().path);
        public static void OpenLevel(int id) { if (IsUnlocked(id)) Load(LevelPath(id)); }
        // Only the LevelSelect Play button uses the Loading scene.
        public static bool OpenLevelFromSelect(int id) => IsUnlocked(id) && Load(LevelPath(id), true);
        public static bool OpenSecretFromSelect(string path) => OpenSecret(path, true);

        private static bool OpenSecret(string path, bool showLoading)
        {
            var secret = Catalog?.FindSecretLevelByScene(path);
            return secret != null && IsSecretUnlocked(secret.id) && Load(path, showLoading);
        }

        public static bool Load(string path, bool showLoading = false)
        {
            if (Catalog == null) return false;
            if (IsChangingScene) return false;
            if (string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError("Cannot load scene. Check SceneCatalog and Build Settings: " + path);
                return false;
            }
            // Every scene change fades to black (0.5 s), loads, then fades back in (0.5 s).
            return SceneFade.Run(() => showLoading ? LoadingScreen.TryLoad(path) : LoadNow(path));
        }

        private static bool LoadNow(string path)
        {
            InGameSettingsSession.LeaveForSceneChange();
            Time.timeScale = 1f;
            try
            {
                pendingNavigation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
                return pendingNavigation != null;
            }
            catch (Exception error) { pendingNavigation = null; Debug.LogException(error); return false; }
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
