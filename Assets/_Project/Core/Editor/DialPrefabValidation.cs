using System;
using UnityEditor;
using UnityEngine;

public static class DialPrefabValidation
{
    [MenuItem("Tools/UI/Validate In-Game HUD")]
    public static void Validate()
    {
        const string hudGuid = "69cd515c7bc15274ba913033a0d5eae4";
        string path = AssetDatabase.GUIDToAssetPath(hudGuid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var manager = prefab == null ? null : prefab.GetComponentInChildren<InGameUIManager>(true);
        if (manager == null)
            throw new InvalidOperationException("NewUISet is missing its HUD manager: " + path);

        var serialized = new SerializedObject(manager);
        foreach (string field in new[] { "ActionUI", "PauseUI", "turnBarPrefab", "hazardEventPrefab", "characterIconPrefab" })
        {
            if (serialized.FindProperty(field)?.objectReferenceValue == null)
                throw new InvalidOperationException("NewUISet has an unassigned HUD reference: " + field);
        }
        Debug.Log("NewUISet HUD references verified.", prefab);
    }
}
