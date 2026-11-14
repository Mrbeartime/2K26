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
    public class LevelDefinition
    {
        [Tooltip("Permanent save ID. Never change or reuse an ID after releasing a level.")]
        [Min(1)] public int id = 1;
        public string title;
        [TextArea] public string description = "Reach the exit before your turns run out.";
        [Tooltip("Optional paper style from the QuestPaper prefab. Empty uses its default style.")]
        public Sprite paperSprite;
        public SceneReference scene = new SceneReference();
        public string Title => string.IsNullOrWhiteSpace(title) ? "Level " + id : title;
    }

    [Serializable]
    public class SecretLevelDefinition
    {
        public string id = "SecretLevel1";
        public string title = "Secret Level";
        [TextArea] public string description;
        public Sprite paperSprite;
        public SceneReference scene = new SceneReference();
        [Min(0)] public int requiredCompletedLevel = 5;
    }

    /// <summary>Single source for scene paths, level IDs, order, and menu descriptions.</summary>
    [CreateAssetMenu(menuName = "Project D/Scene Catalog", fileName = "SceneCatalog")]
    public class SceneCatalog : ScriptableObject
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
        [Tooltip("The first level is always playable. Completing it unlocks all main levels.")]
        [Min(1)] public int firstLevelId = 1;
        public bool requireFirstLevelCompletion = true;
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();
        [SerializeField] private List<SecretLevelDefinition> secretLevels = new List<SecretLevelDefinition>();
        public IReadOnlyList<LevelDefinition> Levels => levels;
        public IReadOnlyList<SecretLevelDefinition> SecretLevels => secretLevels;
        public int Revision { get; private set; }
        public LevelDefinition FindLevel(int id) => levels.Find(level => level != null && level.id == id);
        public LevelDefinition FindLevelByScene(string path) => string.IsNullOrEmpty(path) ? null :
            levels.Find(level => level != null && level.scene.Path == path);
        public LevelDefinition NextLevel(int id)
        {
            int index = levels.FindIndex(level => level != null && level.id == id);
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
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
#if UNITY_EDITOR
        public void RefreshPaths()
        {
            foreach (var reference in new[] { startScene, settingsScene, levelSelectScene, guideScene, loadingScene }) reference?.SyncPath();
            foreach (var level in levels) level?.scene?.SyncPath();
            foreach (var level in secretLevels) level?.scene?.SyncPath();
        }

        private void OnValidate()
        {
            Revision++;
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
            if (FindLevel(firstLevelId) == null && levels.Count > 0) Debug.LogError("SceneCatalog: First Level ID must match a level in the list.", this);
        }
#endif
    }
}
