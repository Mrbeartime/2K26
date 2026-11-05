using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DialPrefabValidation
{
    [MenuItem("Tools/UI/Validate Dial Prefabs")]
    public static void Validate()
    {
        foreach (string name in new[] { "DialTick", "DialEventIcon" })
        {
            string path = "Assets/Resources/UI/Dial/" + name + ".prefab";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<Image>() == null)
                throw new Exception("Invalid dial prefab: " + path);
            if (Resources.Load<GameObject>("UI/Dial/" + name) == null)
                throw new Exception("Resource load failed: " + name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            try
            {
                if (instance.GetComponent<RectTransform>() == null)
                    throw new Exception("Missing RectTransform: " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        var host = new GameObject("Dial validation");
        try
        {
            var manager = host.AddComponent<InGameUIManager>();
            var serialized = new SerializedObject(manager);
            foreach (string field in new[] { "dialTickPrefab", "dialEventIconPrefab" })
                if (serialized.FindProperty(field) == null)
                    throw new Exception("Missing Inspector field: " + field);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        Debug.Log("DIAL_VALIDATION_PASSED: Both prefabs imported, loaded and instantiated; InGameUIManager Inspector fields verified.");
    }
}
