using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProjectD.Menus
{
    /// <summary>JSON is the source of truth. PlayerPrefs is read only for first-time migration.</summary>
    public static class LevelProgressStore
    {
        [Serializable]
        public sealed class LevelState
        {
            public int level;
            public bool completed;
            public bool unlocked;
        }

        [Serializable]
        private sealed class SaveData
        {
            public int version;
            public List<LevelState> levels;
            public List<string> unlockedSecrets;
            public List<string> completedSecrets;
        }

        private static SaveData data;
        private static string loadedFileStamp;
        public static int Revision { get; private set; }
        public static string SaveFilePath
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Application.dataPath, "Script", "SceneManager", "level-progress.json");
#else
                // Installed games may not have permission to write into their application assets.
                return Path.Combine(Application.persistentDataPath, "level-progress.json");
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            data = null;
            loadedFileStamp = null;
            Revision = 0;
        }

        /// <summary>Call after editing JSON externally; ordinary queries use an in-memory cache.</summary>
        public static void Reload()
        {
            data = null;
            EnsureLoaded();
            Revision++;
        }

        public static bool ReloadIfFileChanged()
        {
            string stamp = GetFileStamp();
            if (stamp == null || (data != null && stamp == loadedFileStamp)) return false;
            Reload();
            return true;
        }

        private static string GetFileStamp()
        {
            try
            {
                var file = new FileInfo(SaveFilePath);
                return file.Exists ? file.LastWriteTimeUtc.Ticks + ":" + file.Length : "missing";
            }
            catch (Exception ex) when (IsStorageError(ex)) { return null; }
        }

        public static bool IsCompleted(int level)
        {
            ReloadIfFileChanged();
            EnsureLoaded();
            return ValidLevel(level) && data.levels[level - 1].completed;
        }

        public static bool IsUnlocked(int level)
        {
            ReloadIfFileChanged();
            EnsureLoaded();
            // Never trust a leftover unlocked:true flag for levels 2-5.
            return ValidLevel(level) && (level == 1 || data.levels[0].completed);
        }

        public static bool IsSecretUnlocked(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(id) && data.unlockedSecrets.Contains(id);
        }

        public static bool IsSecretCompleted(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(id) && data.completedSecrets.Contains(id);
        }

        public static bool CompleteLevel(int level)
        {
            if (!ValidLevel(level)) return false;
            ReloadIfFileChanged();
            EnsureLoaded();
            data.levels[level - 1].completed = true;
            Normalize(data);
            return Save();
        }

        public static bool UnlockSecret(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            EnsureLoaded();
            if (!data.unlockedSecrets.Contains(id)) data.unlockedSecrets.Add(id);
            return Save();
        }

        public static bool CompleteSecret(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            EnsureLoaded();
            if (!data.completedSecrets.Contains(id)) data.completedSecrets.Add(id);
            if (!data.unlockedSecrets.Contains(id)) data.unlockedSecrets.Add(id);
            return Save();
        }

        private static bool ValidLevel(int level) => level >= 1 && level <= GameSceneManager.LevelCount;

        private static SaveData NewData() => new SaveData
        {
            version = 1,
            levels = new List<LevelState>(),
            unlockedSecrets = new List<string>(),
            completedSecrets = new List<string>()
        };

        private static void EnsureLoaded()
        {
            if (data != null) return;
            LoadData();
            loadedFileStamp = GetFileStamp();
        }

        private static void LoadData()
        {
            if (data != null) return;
            if (File.Exists(SaveFilePath))
            {
                if (TryRead(SaveFilePath, out data)) return;
                // Preserve the damaged file before any future save replaces it.
                try
                {
                    File.Copy(SaveFilePath, SaveFilePath + ".corrupt-" + Guid.NewGuid().ToString("N"));
                }
                catch (Exception ex) when (IsStorageError(ex))
                {
                    Debug.LogWarning("Could not preserve damaged progress JSON: " + ex.Message);
                }
                if (TryRead(SaveFilePath + ".bak", out data)) return;
                // Existing but invalid JSON must not resurrect old PlayerPrefs progress.
                data = NewData();
                Normalize(data);
                return;
            }
            if (TryRead(SaveFilePath + ".bak", out data)) return;

#if UNITY_EDITOR
            // Import the old editor save once. Never overwrite an existing project JSON.
            string previousSave = Path.Combine(Application.persistentDataPath, "level-progress.json");
            if (TryRead(previousSave, out data))
            {
                Save();
                return;
            }
#endif

            data = NewData();
            int oldUnlocked = Mathf.Clamp(PlayerPrefs.GetInt("Progress.UnlockedLevel", 1), 1, GameSceneManager.LevelCount);
            for (int level = 1; level <= GameSceneManager.LevelCount; level++)
                data.levels.Add(new LevelState
                {
                    level = level,
                    completed = PlayerPrefs.GetInt($"Progress.Level{level}.Complete", 0) == 1,
                    unlocked = level <= oldUnlocked
                });
            const string secret = "SecretLevel1";
            if (PlayerPrefs.GetInt("Progress.Secret." + secret + ".Unlocked", 0) == 1)
                data.unlockedSecrets.Add(secret);
            if (PlayerPrefs.GetInt("Progress.Secret." + secret + ".Complete", 0) == 1)
            {
                data.completedSecrets.Add(secret);
                if (!data.unlockedSecrets.Contains(secret)) data.unlockedSecrets.Add(secret);
            }
            Normalize(data);
            Save();
        }

        private static bool TryRead(string path, out SaveData loaded)
        {
            loaded = null;
            if (!File.Exists(path)) return false;
            try
            {
                var candidate = JsonUtility.FromJson<SaveData>(File.ReadAllText(path, Encoding.UTF8));
                if (candidate == null || candidate.version != 1 || candidate.levels == null)
                    throw new ArgumentException("Missing levels or unsupported save version.");
                Normalize(candidate);
                loaded = candidate;
                return true;
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                Debug.LogWarning("Could not read level progress: " + path + " | " + ex.Message);
                return false;
            }
        }

        private static void Normalize(SaveData save)
        {
            var ordered = new List<LevelState>();
            for (int level = 1; level <= GameSceneManager.LevelCount; level++)
            {
                var state = new LevelState { level = level };
                foreach (var entry in save.levels)
                {
                    if (entry == null || entry.level != level) continue;
                    state.completed |= entry.completed;
                }
                ordered.Add(state);
            }
            // Level 1 is the only gate, including when loading an older JSON save.
            bool passedFirstLevel = ordered[0].completed;
            foreach (var state in ordered) state.unlocked = state.level == 1 || passedFirstLevel;
            save.levels = ordered;
            if (save.unlockedSecrets == null) save.unlockedSecrets = new List<string>();
            if (save.completedSecrets == null) save.completedSecrets = new List<string>();
        }

        private static bool Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));
                string temporary = SaveFilePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
                // Keep the preceding save as a backup and avoid partially-written primary JSON.
                if (File.Exists(SaveFilePath)) File.Replace(temporary, SaveFilePath, SaveFilePath + ".bak");
                else File.Move(temporary, SaveFilePath);
                loadedFileStamp = GetFileStamp();
                Revision++;
                Debug.Log("[LevelProgress] Saved: " + SaveFilePath);
                return true;
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                Debug.LogWarning("Level progress is in memory but could not be saved: " + ex.Message);
                return false;
            }
        }

        private static bool IsStorageError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException ||
            ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException;
    }
}
