using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProjectD.Menus
{
    [Serializable]
    internal sealed class QuestPaperPlacement
    {
        public const float MaxRotation = 5f;
        public static int ColumnsFor(int capacity) => capacity % 2 == 0 ? capacity + 1 : capacity;

        public int level;
        public int page; // Kept to read placements from saves created when the board had pages.
        public int slot;
        public float x;
        public float y;
        public float rotation;
        public bool popInPlayed; // the paper's pop-in animation has already played once
    }

    /// <summary>JSON is the source of truth. PlayerPrefs is read only for first-time migration.</summary>
    public static class LevelProgressStore
    {
        private const int CenteredQuestPaperLayout = 1;

        [Serializable]
        public sealed class LevelState
        {
            public int level;
            public bool completed;
            public bool unlocked;
        }

        [Serializable]
        private sealed class QuestDepthDescription
        {
            public string questId;
            public string text;
        }

        [Serializable]
        private sealed class SaveData
        {
            public int version;
            public List<LevelState> levels;
            public List<string> unlockedSecrets;
            public List<string> completedSecrets;
            public int questPaperLayoutVersion;
            public List<QuestPaperPlacement> questPaperPlacements;
            public List<QuestDepthDescription> questDepthDescriptions;
        }

        private static readonly System.Random paperRandom = new System.Random();
        private static SaveData data;
        private static string loadedFileStamp;
        private static bool questPaperDataDirty;
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
            questPaperDataDirty = false;
            Revision = 0;
        }

        /// <summary>Call after editing JSON externally; ordinary queries use an in-memory cache.</summary>
        public static void Reload()
        {
            // Keep pending quest paper changes when a write failed and the save on disk is unchanged.
            if (!questPaperDataDirty || loadedFileStamp != GetFileStamp())
            {
                questPaperDataDirty = false;
                data = null;
                EnsureLoaded();
            }
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
            return ValidLevel(level) && FindState(level).completed;
        }

        public static bool IsUnlocked(int level)
        {
            ReloadIfFileChanged();
            EnsureLoaded();
            var catalog = GameSceneManager.Catalog;
            var definition = catalog?.FindLevel(level);
            return definition != null && (FindState(level)?.completed == true ||
                MeetsUnlockConditions(catalog, data, definition.unlockConditions));
        }

        public static bool IsSecretUnlocked(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            ReloadIfFileChanged();
            EnsureLoaded();
            if (data.unlockedSecrets.Contains(id) || data.completedSecrets.Contains(id)) return true;

            var catalog = GameSceneManager.Catalog;
            var definition = catalog?.FindSecretLevel(id);
            return definition != null && MeetsUnlockConditions(catalog, data, definition.unlockConditions);
        }

        public static bool IsSecretCompleted(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(id) && data.completedSecrets.Contains(id);
        }

        private static bool MeetsUnlockConditions(SceneCatalog catalog, SaveData save, LevelUnlockConditions conditions)
        {
            if (conditions == null) return true;

            if (conditions.requireCompletedLevelList)
            {
                foreach (int id in conditions.requiredMainLevelIds ?? Array.Empty<int>())
                    if (!save.levels.Exists(state => state.level == id && state.completed)) return false;
                foreach (string id in conditions.requiredSecretLevelIds ?? Array.Empty<string>())
                    if (string.IsNullOrWhiteSpace(id) || !save.completedSecrets.Contains(id)) return false;
            }

            if (!conditions.requireCompletedLevelCount && !conditions.requireMinimumDepth && !conditions.requireTotalDepth)
                return true;

            int completedCount = 0;
            int highestDepth = -1;
            long totalDepth = 0;

            void IncludeCompletedLevel(int depth)
            {
                depth = Mathf.Clamp(depth, 0, 10);
                completedCount++;
                highestDepth = Mathf.Max(highestDepth, depth);
                totalDepth += depth;
            }

            foreach (var state in save.levels)
            {
                if (!state.completed) continue;
                var level = catalog.FindLevel(state.level);
                if (level != null) IncludeCompletedLevel(level.depth);
            }

            foreach (var level in catalog.SecretLevels)
                if (level != null && save.completedSecrets.Contains(level.id)) IncludeCompletedLevel(level.depth);

            return (!conditions.requireCompletedLevelCount || completedCount >= conditions.completedLevelCount) &&
                (!conditions.requireMinimumDepth || highestDepth >= conditions.minimumDepth) &&
                (!conditions.requireTotalDepth || totalDepth >= conditions.totalDepth);
        }

        internal static IReadOnlyDictionary<int, QuestPaperPlacement> GetOrCreateQuestPaperPlacements(
            IReadOnlyList<LevelDefinition> boardLevels, int capacity)
        {
            ReloadIfFileChanged();
            EnsureLoaded();

            if (data.questPaperLayoutVersion < CenteredQuestPaperLayout)
            {
                // Allocate the old layout again once so existing papers can start near the center.
                data.questPaperPlacements.Clear();
                data.questPaperLayoutVersion = CenteredQuestPaperLayout;
                questPaperDataDirty = true;
            }

            int columns = QuestPaperPlacement.ColumnsFor(capacity);
            var byLevel = new Dictionary<int, QuestPaperPlacement>();
            foreach (var placement in data.questPaperPlacements)
                if (!byLevel.TryAdd(placement.level, placement)) questPaperDataDirty = true;

            // Reserve valid slots first, then relocate papers from old pages and add new ones.
            var occupied = new HashSet<int>();
            var needsSlot = new List<QuestPaperPlacement>();
            foreach (var level in boardLevels)
            {
                if (!byLevel.TryGetValue(level.id, out var placement))
                {
                    placement = new QuestPaperPlacement
                    {
                        level = level.id,
                        slot = -1,
                        x = (float)paperRandom.NextDouble(),
                        y = (float)paperRandom.NextDouble(),
                        rotation = (float)(2d * paperRandom.NextDouble() - 1d) * QuestPaperPlacement.MaxRotation
                    };
                    byLevel.Add(level.id, placement);
                }
                if (placement.page != 0 || placement.slot < 0 || placement.slot >= columns || !occupied.Add(placement.slot))
                    needsSlot.Add(placement);
            }

            if (needsSlot.Count > 0)
            {
                var freeSlots = GetCenterOutSlotOrder(columns);
                freeSlots.RemoveAll(occupied.Contains);
                for (int i = 0; i < needsSlot.Count; i++)
                {
                    needsSlot[i].page = 0;
                    needsSlot[i].slot = freeSlots[i];
                }
                questPaperDataDirty = true;
            }

            if (questPaperDataDirty)
            {
                data.questPaperPlacements = new List<QuestPaperPlacement>(byLevel.Values);
                data.questPaperPlacements.Sort((left, right) => left.level.CompareTo(right.level));
                Save();
            }
            return byLevel;
        }

        internal static string GetOrCreateQuestDepthDescription(string questId, Func<string> createDescription)
        {
            ReloadIfFileChanged();
            EnsureLoaded();

            var entry = data.questDepthDescriptions.Find(item => item.questId == questId);
            if (entry == null)
            {
                string text = createDescription();
                if (string.IsNullOrWhiteSpace(text)) return null;

                entry = new QuestDepthDescription { questId = questId, text = text };
                data.questDepthDescriptions.Add(entry);
                questPaperDataDirty = true;
            }

            if (questPaperDataDirty) Save();
            return entry.text;
        }

        /// <summary>Remembers that these board papers played their pop-in, so it only happens the first time they are created.</summary>
        internal static void MarkQuestPaperPopIn(IEnumerable<int> levels)
        {
            EnsureLoaded();
            bool changed = false;
            foreach (int level in levels)
            {
                var placement = data.questPaperPlacements.Find(item => item.level == level);
                if (placement == null || placement.popInPlayed) continue;
                placement.popInPlayed = true;
                changed = true;
            }
            if (changed) Save();
        }

        private static List<int> GetCenterOutSlotOrder(int columns)
        {
            int middle = columns / 2;
            var order = new List<int>(columns) { middle };
            for (int distance = 1; distance <= middle; distance++)
            {
                int side = paperRandom.Next(2) == 0 ? -1 : 1;
                order.Add(middle + distance * side);
                order.Add(middle - distance * side);
            }
            return order;
        }

        public static bool CompleteLevel(int level)
        {
            if (!ValidLevel(level)) return false;
            ReloadIfFileChanged();
            EnsureLoaded();
            FindState(level).completed = true;
            return Save();
        }

        public static bool UnlockSecret(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            ReloadIfFileChanged();
            EnsureLoaded();
            if (!data.unlockedSecrets.Contains(id)) data.unlockedSecrets.Add(id);
            return Save();
        }

        public static bool CompleteSecret(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            ReloadIfFileChanged();
            EnsureLoaded();
            if (!data.completedSecrets.Contains(id)) data.completedSecrets.Add(id);
            if (!data.unlockedSecrets.Contains(id)) data.unlockedSecrets.Add(id);
            return Save();
        }

        private static bool ValidLevel(int level) => GameSceneManager.Catalog != null && GameSceneManager.Catalog.FindLevel(level) != null;
        private static LevelState FindState(int id)
        {
            var found = data.levels.Find(state => state.level == id);
            if (found != null || !ValidLevel(id)) return found;
            found = new LevelState { level = id };
            data.levels.Add(found);
            return found;
        }

        private static SaveData NewData() => new SaveData
        {
            version = 1,
            levels = new List<LevelState>(),
            unlockedSecrets = new List<string>(),
            completedSecrets = new List<string>(),
            questPaperLayoutVersion = CenteredQuestPaperLayout,
            questPaperPlacements = new List<QuestPaperPlacement>(),
            questDepthDescriptions = new List<QuestDepthDescription>()
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
            foreach (var definition in GameSceneManager.Catalog.Levels)
            {
                int level = definition.id;
                data.levels.Add(new LevelState
                {
                    level = level,
                    completed = PlayerPrefs.GetInt($"Progress.Level{level}.Complete", 0) == 1,
                    unlocked = level <= oldUnlocked
                });
            }
            const string secret = "SecretLevel1";
            if (PlayerPrefs.GetInt("Progress.Secret." + secret + ".Unlocked", 0) == 1)
                data.unlockedSecrets.Add(secret);
            if (PlayerPrefs.GetInt("Progress.Secret." + secret + ".Complete", 0) == 1)
            {
                data.completedSecrets.Add(secret);
                if (!data.unlockedSecrets.Contains(secret)) data.unlockedSecrets.Add(secret);
            }
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
            // Merge by permanent ID, never by array index. Keep entries absent from the
            // current catalog so temporarily removing a level cannot erase its save.
            var states = new Dictionary<int, LevelState>();
            foreach (var entry in save.levels)
            {
                if (entry == null || entry.level < 1) continue;
                if (!states.TryGetValue(entry.level, out var state))
                    states.Add(entry.level, state = new LevelState { level = entry.level });
                state.completed |= entry.completed;
                state.unlocked |= entry.unlocked;
            }
            var catalog = GameSceneManager.Catalog;
            if (catalog != null)
            {
                foreach (var level in catalog.Levels)
                    if (level != null && !states.ContainsKey(level.id)) states.Add(level.id, new LevelState { level = level.id });
            }
            save.levels = new List<LevelState>(states.Values);
            save.levels.Sort((left, right) => left.level.CompareTo(right.level));
            if (save.unlockedSecrets == null) save.unlockedSecrets = new List<string>();
            if (save.completedSecrets == null) save.completedSecrets = new List<string>();
            if (catalog != null)
                foreach (var level in catalog.Levels)
                    if (level != null)
                    {
                        var state = states[level.id];
                        state.unlocked = state.completed || MeetsUnlockConditions(catalog, save, level.unlockConditions);
                    }

            if (save.questPaperPlacements == null) save.questPaperPlacements = new List<QuestPaperPlacement>();
            save.questPaperPlacements.RemoveAll(placement => !IsValidPlacement(placement) ||
                (states.TryGetValue(placement.level, out var state) && state.completed));

            save.questDepthDescriptions ??= new List<QuestDepthDescription>();
            var describedQuests = new HashSet<string>(StringComparer.Ordinal);
            save.questDepthDescriptions.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.questId) ||
                string.IsNullOrWhiteSpace(entry.text) || !describedQuests.Add(entry.questId));
        }

        private static bool IsValidPlacement(QuestPaperPlacement placement)
        {
            return placement != null && placement.level > 0 && placement.page >= 0 && placement.slot >= 0 &&
                placement.x >= 0f && placement.x <= 1f && placement.y >= 0f && placement.y <= 1f &&
                Mathf.Abs(placement.rotation) <= QuestPaperPlacement.MaxRotation;
        }

        private static bool Save()
        {
            Normalize(data);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));
                string temporary = SaveFilePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
                // Keep the preceding save as a backup and avoid partially-written primary JSON.
                if (File.Exists(SaveFilePath)) File.Replace(temporary, SaveFilePath, SaveFilePath + ".bak");
                else File.Move(temporary, SaveFilePath);
                loadedFileStamp = GetFileStamp();
                questPaperDataDirty = false;
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
