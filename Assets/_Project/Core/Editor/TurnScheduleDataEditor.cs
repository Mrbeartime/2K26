using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TurnScheduleData))]
public sealed class TurnScheduleDataEditor : Editor
{
    private void OnEnable() => EditorApplication.delayCall += Refresh;
    private void OnDisable() => EditorApplication.delayCall -= Refresh;

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Refresh From Scene") || changed) Refresh();
        }
    }

    private void Refresh()
    {
        if (Application.isPlaying || target == null) return;
        var data = (TurnScheduleData)target;
        if (!data.gameObject.scene.IsValid() || !data.gameObject.scene.isLoaded || EditorUtility.IsPersistent(data)) return;
        string before = EditorJsonUtility.ToJson(data);
        data.RefreshFromScene();
        if (before != EditorJsonUtility.ToJson(data))
        {
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(data.gameObject.scene);
        }
        serializedObject.Update();
        Repaint();
    }
}
