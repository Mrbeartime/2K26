using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > TMP > Set All Fonts To NotoSansThai-Regular
// Sets every TMP_Text in Assets/ scenes and prefabs to NotoSansThai-Regular SDF
// and writes the list of what it touched to <project>/TMP_FontReport.txt.
public static class SetTMPFontToNotoThai
{
    const string FontPath = "Assets/_Project/UI/Fonts/NotoSansThai-Regular SDF.asset";

    static bool Skip(string p) => p.StartsWith("Assets/_Unused/", System.StringComparison.Ordinal) || p.Contains("/TextMesh Pro/") || p.Contains("/Plugins/");

    static string PathOf(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static int Apply(GameObject[] roots, TMP_FontAsset font, string where, StringBuilder log, out int total)
    {
        int changed = 0; total = 0;
        foreach (var r in roots)
            foreach (var t in r.GetComponentsInChildren<TMP_Text>(true))
            {
                total++;
                string from = t.font ? t.font.name : "none";
                if (t.font != font) { t.font = font; EditorUtility.SetDirty(t); changed++; }
                log.AppendLine($"  {where} :: {PathOf(t.transform)} ({t.GetType().Name}, was {from})");
            }
        return changed;
    }

    [MenuItem("Tools/TMP/Set All Fonts To NotoSansThai-Regular")]
    static void Run()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (!font) { Debug.LogError("Font asset not found: " + FontPath); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string prevScene = SceneManager.GetActiveScene().path;
        var log = new StringBuilder();
        int files = 0, changedTotal = 0;

        log.AppendLine("== PREFABS ==");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Skip(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            int ch = Apply(new[] { root }, font, path, new StringBuilder(), out int total);
            if (total > 0)
            {
                files++;
                Apply(new[] { root }, font, path, log, out _);
                if (ch > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                changedTotal += ch;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        log.AppendLine("== SCENES ==");
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Skip(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int ch = Apply(scene.GetRootGameObjects(), font, path, log, out int total);
            if (total > 0) files++;
            if (ch > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); changedTotal += ch; }
        }

        if (!string.IsNullOrEmpty(prevScene)) EditorSceneManager.OpenScene(prevScene);
        AssetDatabase.SaveAssets();

        var report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TMP_FontReport.txt");
        File.WriteAllText(report, log.ToString());
        Debug.Log($"TMP font set: {changedTotal} components changed across {files} files. List: {report}");
    }
}
