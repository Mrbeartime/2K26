using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMove)), CanEditMultipleObjects]
public class PlayerMoveEditor : Editor
{
    private static readonly string[] Names = { "Character", "Rogue", "ArcherMan", "SwordMan" };
    private static readonly Type[] Types = { typeof(Character), typeof(Rogue), typeof(ArcherMan), typeof(SwordMan) };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Character Class", EditorStyles.boldLabel);
        int selected = GetClassIndex((PlayerMove)target);
        bool mixed = false;
        foreach (UnityEngine.Object item in targets)
            if (GetClassIndex((PlayerMove)item) != selected) mixed = true;

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup("Profession", selected, Names);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (changed && next >= 0)
                foreach (UnityEngine.Object item in targets) SetClass((PlayerMove)item, Types[next]);
        }
        if (selected < 0)
            EditorGUILayout.HelpBox("Select a profession to add its Character component.", MessageType.Info);
        EditorGUILayout.HelpBox("Set Movement Range and combat values on the profession component below. Change professions outside Play Mode.", MessageType.None);
    }

    private static int GetClassIndex(PlayerMove player)
    {
        Character character = player.GetComponent<Character>();
        return character == null ? -1 : Array.IndexOf(Types, character.GetType());
    }

    private static void SetClass(PlayerMove player, Type type)
    {
        Character[] existing = player.GetComponents<Character>();
        if (existing.Length == 1 && existing[0].GetType() == type) return;
        // Preserve shared settings (CanKill, movement range) when switching.
        string settings = existing.Length > 0 ? JsonUtility.ToJson(existing[0]) : null;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Change Character Profession");
        foreach (Character character in existing) Undo.DestroyObjectImmediate(character);
        Component replacement = Undo.AddComponent(player.gameObject, type);
        if (settings != null) JsonUtility.FromJsonOverwrite(settings, replacement);
        EditorUtility.SetDirty(replacement);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
        Undo.CollapseUndoOperations(group);
    }
}
