using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjectD.Menus
{
    [Serializable]
    public class SceneReference
    {
#if UNITY_EDITOR
        [SerializeField] private UnityEditor.SceneAsset scene;
#endif
        [SerializeField, HideInInspector] private string scenePath;
        public string Path
        {
            get
            {
#if UNITY_EDITOR
                // Scene assets keep their GUID when renamed or moved in Unity.
                if (scene != null) return UnityEditor.AssetDatabase.GetAssetPath(scene);
#endif
                return scenePath ?? string.Empty;
            }
        }
#if UNITY_EDITOR
        public void SyncPath() => scenePath = scene != null ? UnityEditor.AssetDatabase.GetAssetPath(scene) : string.Empty;
#endif
    }

    [Serializable]
    public class LevelUnlockConditions
    {
        [Tooltip("Require completion of every main and secret level ID listed below.")]
        public bool requireCompletedLevelList;
        public int[] requiredMainLevelIds = Array.Empty<int>();
        public string[] requiredSecretLevelIds = Array.Empty<string>();

        [Tooltip("Require at least this many distinct completed levels, including secret levels.")]
        public bool requireCompletedLevelCount;
        [Min(0)] public int completedLevelCount;

        [Tooltip("Require at least one completed level at or above this Depth.")]
        public bool requireMinimumDepth;
        [Range(0, 10)] public int minimumDepth;

        [Tooltip("Require the sum of Depth from all distinct completed levels to reach this value.")]
        public bool requireTotalDepth;
        [Min(0)] public int totalDepth;

        internal static LevelUnlockConditions AfterMainLevel(int id) => new LevelUnlockConditions
        {
            requireCompletedLevelList = true,
            requiredMainLevelIds = new[] { id }
        };
    }

    [Serializable]
    public class LevelDefinition
    {
        [Tooltip("Permanent save ID. Never change or reuse an ID after releasing a level.")]
        [Min(1)] public int id = 1;
        public string title;
        [TextArea] public string description = "Reach the exit before your turns run out.";
        [Tooltip("Icons for the characters available in this level, in display order.")]
        public Sprite[] characterAvailable = Array.Empty<Sprite>();
        [Tooltip("Approximate difficulty shown in Level Select.")]
        [Range(0, 10)] public int depth;
        [Tooltip("Optional paper style from the QuestPaper prefab. Empty uses its default style.")]
        public Sprite paperSprite;
        public SceneReference scene = new SceneReference();
        [Tooltip("All enabled conditions must be met to unlock the level.")]
        public LevelUnlockConditions unlockConditions = new LevelUnlockConditions();
        public string Title => string.IsNullOrWhiteSpace(title) ? "Level " + id : title;
        internal LevelDefinition Copy() => (LevelDefinition)MemberwiseClone();
    }

    [Serializable]
    public class SecretLevelDefinition
    {
        public string id = "SecretLevel1";
        public string title = "Secret Level";
        [TextArea] public string description;
        [Tooltip("Icons for the characters available in this level, in display order.")]
        public Sprite[] characterAvailable = Array.Empty<Sprite>();
        [Tooltip("Approximate difficulty shown in Level Select.")]
        [Range(0, 10)] public int depth;
        public Sprite paperSprite;
        public SceneReference scene = new SceneReference();
        [Tooltip("All enabled conditions must be met to unlock the level.")]
        public LevelUnlockConditions unlockConditions = new LevelUnlockConditions();
        [SerializeField, HideInInspector] private int requiredCompletedLevel = 5;

        internal SecretLevelDefinition Copy() => (SecretLevelDefinition)MemberwiseClone();

        internal void MigrateUnlockConditions()
        {
            if (requiredCompletedLevel > 0)
                unlockConditions = LevelUnlockConditions.AfterMainLevel(requiredCompletedLevel);
        }
    }

    /// <summary>Single source for scene paths, level IDs, order, and menu descriptions.</summary>
    [CreateAssetMenu(menuName = "Project D/Scene Catalog", fileName = "SceneCatalog")]
    public class SceneCatalog : ScriptableObject, ISerializationCallbackReceiver
    {
        public SceneReference startScene = new SceneReference();
        public SceneReference settingsScene = new SceneReference();
        public SceneReference levelSelectScene = new SceneReference();
        public SceneReference guideScene = new SceneReference();
        public SceneReference loadingScene = new SceneReference();
        [Header("Level Select Quest Papers")]
        [Tooltip("Shared paper prefab. Every level uses this automatically; no per-level button setup is required.")]
        public QuestPaperDecoration questPaperPrefab;
        [Tooltip("Readable font for level titles. Description ink still uses the QuestPaper's own font.")]
        public TMP_FontAsset questTitleFont;
        public Material questCharacterIconMaterial;
        [Tooltip("One option per line, in the form depth|description.")]
        public TextAsset questDepthDescriptions;
        [Header("Quest Data")]
        [Tooltip("Quest data keyed by permanent level IDs. The level entries below register scenes and provide fallback data.")]
        public TextAsset questData;
        [SerializeField] private QuestCharacter[] questCharacters = Array.Empty<QuestCharacter>();
        [HideInInspector] public int firstLevelId = 1;
        [SerializeField, HideInInspector] private bool requireFirstLevelCompletion = true;
        [SerializeField, HideInInspector] private bool unlockConditionsMigrated;
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();
        [SerializeField] private List<SecretLevelDefinition> secretLevels = new List<SecretLevelDefinition>();
        [NonSerialized] private SceneCatalogQuestData resolvedQuestData;
        [NonSerialized] private GameLanguage.Id resolvedLanguage;
        [NonSerialized] private int revision;
        private List<LevelDefinition> ActiveLevels => QuestData?.Levels ?? levels;
        private List<SecretLevelDefinition> ActiveSecretLevels => QuestData?.SecretLevels ?? secretLevels;
        private SceneCatalogQuestData QuestData
        {
            get
            {
                if (questData == null) return null;
                SyncQuestLanguage();
                if (resolvedQuestData == null)
                {
                    resolvedLanguage = GameLanguage.Current;
                    resolvedQuestData = new SceneCatalogQuestData(this, levels, secretLevels);
                }
                return resolvedQuestData;
            }
        }

        // Quest titles and descriptions can differ per language, so the resolved data is rebuilt after a language change.
        private void SyncQuestLanguage()
        {
            if (resolvedQuestData != null && resolvedLanguage != GameLanguage.Current) InvalidateQuestData();
        }

        public IReadOnlyList<LevelDefinition> Levels => ActiveLevels;
        public IReadOnlyList<SecretLevelDefinition> SecretLevels => ActiveSecretLevels;
        public int Revision
        {
            get
            {
                SyncQuestLanguage();
                return revision;
            }
        }
        public LevelDefinition FindLevel(int id) => ActiveLevels.Find(level => level != null && level.id == id);
        public LevelDefinition FindLevelByScene(string path) => string.IsNullOrEmpty(path) ? null :
            ActiveLevels.Find(level => level != null && level.scene.Path == path);
        public SecretLevelDefinition FindSecretLevel(string id) => string.IsNullOrWhiteSpace(id) ? null :
            ActiveSecretLevels.Find(level => level != null && level.id == id);
        public SecretLevelDefinition FindSecretLevelByScene(string path) => string.IsNullOrEmpty(path) ? null :
            ActiveSecretLevels.Find(level => level != null && level.scene.Path == path);
        public LevelDefinition NextLevel(int id)
        {
            var entries = ActiveLevels;
            int index = entries.FindIndex(level => level != null && level.id == id);
            return index >= 0 && index + 1 < entries.Count ? entries[index + 1] : null;
        }

        internal Sprite FindQuestCharacter(string name)
        {
            foreach (var character in questCharacters ?? Array.Empty<QuestCharacter>())
                if (character != null && string.Equals(name, character.name, StringComparison.OrdinalIgnoreCase))
                    return character.icon;
            return null;
        }

#if UNITY_EDITOR
        internal string QuestCharacterName(Sprite icon)
        {
            foreach (var character in questCharacters ?? Array.Empty<QuestCharacter>())
                if (character != null && character.icon == icon)
                    return character.name;
            return icon != null ? icon.name : string.Empty;
        }
#endif

        internal void InvalidateQuestData()
        {
            resolvedQuestData = null;
            revision++;
        }

        [Serializable]
        private sealed class QuestCharacter
        {
            public string name;
            public Sprite icon;
        }
        public IEnumerable<string> ScenePaths()
        {
            foreach (var reference in new[] { startScene, settingsScene, levelSelectScene, guideScene, loadingScene })
                if (reference != null && !string.IsNullOrEmpty(reference.Path)) yield return reference.Path;
            foreach (var level in levels)
                if (level != null && !string.IsNullOrEmpty(level.scene.Path)) yield return level.scene.Path;
            foreach (var level in secretLevels)
                if (level != null && !string.IsNullOrEmpty(level.scene.Path)) yield return level.scene.Path;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize() => MigrateUnlockConditions();

        private void MigrateUnlockConditions()
        {
            if (unlockConditionsMigrated) return;
            if (requireFirstLevelCompletion)
                foreach (var level in levels)
                    if (level != null && level.id != firstLevelId)
                        level.unlockConditions = LevelUnlockConditions.AfterMainLevel(firstLevelId);
            foreach (var level in secretLevels) level?.MigrateUnlockConditions();
            unlockConditionsMigrated = true;
        }

#if UNITY_EDITOR
        public void RefreshPaths()
        {
            foreach (var reference in new[] { startScene, settingsScene, levelSelectScene, guideScene, loadingScene }) reference?.SyncPath();
            foreach (var level in levels) level?.scene?.SyncPath();
            foreach (var level in secretLevels) level?.scene?.SyncPath();
        }

        private void OnValidate()
        {
            MigrateUnlockConditions();
            InvalidateQuestData();
            RefreshPaths();
            var ids = new HashSet<int>();
            var paths = new HashSet<string>();
            foreach (var level in levels)
            {
                if (level == null) continue;
                if (level.id < 1 || !ids.Add(level.id)) Debug.LogError("SceneCatalog: invalid or duplicate level ID " + level.id, this);
                if (!string.IsNullOrEmpty(level.scene.Path) && !paths.Add(level.scene.Path)) Debug.LogError("SceneCatalog: duplicate level scene " + level.scene.Path, this);
            }
            var secretIds = new HashSet<string>();
            foreach (var level in secretLevels)
            {
                if (level == null) continue;
                if (string.IsNullOrWhiteSpace(level.id) || !secretIds.Add(level.id)) Debug.LogError("SceneCatalog: invalid or duplicate secret ID " + level.id, this);
            }
        }
#endif
    }
}
