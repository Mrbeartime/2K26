using System.Collections.Generic;
using System.Linq;
using ProjectD.Menus;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(SceneCatalog))]
public class SceneCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Add levels here. Keep each ID permanent. Sync Build Settings after assigning a new scene.", MessageType.Info);
        DrawDefaultInspector();
        if (GUILayout.Button("Sync Scenes To Build Settings")) Sync((SceneCatalog)target);
    }

    public static void Sync(SceneCatalog catalog)
    {
        // Keep other preloaded assets; this catalog becomes the active scene configuration.
        var preloaded = PlayerSettings.GetPreloadedAssets().Where(asset => !(asset is SceneCatalog)).ToList();
        preloaded.Add(catalog);
        PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
        catalog.RefreshPaths();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string path in catalog.ScenePaths().Distinct())
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError("SceneCatalog: scene does not exist: " + path, catalog);
                continue;
            }
            var existing = scenes.Find(scene => scene.path == path);
            if (existing == null) scenes.Add(new EditorBuildSettingsScene(path, true));
            else existing.enabled = true;
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("SceneCatalog: Build Settings updated.", catalog);
    }
}

// Report missing scenes before shipping a build instead of leaving a broken load button.
public class SceneCatalogBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        var catalogs = PlayerSettings.GetPreloadedAssets().OfType<SceneCatalog>().ToArray();
        if (catalogs.Length != 1)
            throw new BuildFailedException("Select the SceneCatalog asset and click Sync Scenes To Build Settings. Exactly one catalog must be preloaded.");
        var catalog = catalogs[0];
        catalog.RefreshPaths();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        if (new[] { catalog.startScene.Path, catalog.settingsScene.Path, catalog.levelSelectScene.Path,
            catalog.guideScene.Path, catalog.loadingScene.Path }.Any(string.IsNullOrEmpty))
            throw new BuildFailedException("SceneCatalog: assign Start, Settings, Level Select, Guide and Loading scenes.");
        var ids = new HashSet<int>();
        foreach (var level in catalog.Levels)
            if (level == null || level.id < 1 || !ids.Add(level.id) || string.IsNullOrEmpty(level.scene.Path))
                throw new BuildFailedException("SceneCatalog has an empty scene or duplicate/invalid level ID.");
        if (catalog.FindLevel(catalog.firstLevelId) == null)
            throw new BuildFailedException("SceneCatalog: First Level ID does not exist.");
        foreach (string path in catalog.ScenePaths().Distinct())
            if (!EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path))
                throw new BuildFailedException("Scene missing from Build Settings: " + path + ". Select SceneCatalog and click Sync Scenes To Build Settings.");
    }
}
