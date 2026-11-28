using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectD.EditorTools.LevelDesign
{
    internal sealed class TraceBrushIcons
    {
        readonly Dictionary<Enum, Texture2D> images = new();
        public TraceBrushIcons()
        {
            // Read the live HUD assignments so a portrait or timeline art change follows the painter.
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("69cd515c7bc15274ba913033a0d5eae4"));
            var manager = hud != null ? hud.GetComponentInChildren<InGameUIManager>(true) : null;
            if (manager != null)
            {
                var data = new SerializedObject(manager);
                var entries = data.FindProperty("CharacterList");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var actor = (InGameUIManager.CharacterType)entry.FindPropertyRelative("characterType").intValue switch
                    {
                        InGameUIManager.CharacterType.Sword => Actor.Warrior,
                        InGameUIManager.CharacterType.Archer => Actor.Archer,
                        InGameUIManager.CharacterType.Rogue => Actor.Rogue,
                        _ => Actor.None
                    };
                    if (actor != Actor.None) images[actor] = (entry.FindPropertyRelative("portrait").objectReferenceValue as Sprite)?.texture;
                }
                images[Prop.ArrowTrap] = (data.FindProperty("arrowTrapIcon").objectReferenceValue as Sprite)?.texture;
                images[Prop.SpikeTrap] = images[Prop.LinkedSpikes] = (data.FindProperty("spikeTrapIcon").objectReferenceValue as Sprite)?.texture;
            }
            Load(Actor.SwordSkeleton, "Icon_skellySword");
            Load(Actor.BowSkeleton, "Icon_skellyBow");
            foreach (var kind in new[] { Prop.Goal, Prop.Lever, Prop.Plate, Prop.BowTarget }) Load(kind, kind.ToString());
        }

        void Load(Enum kind, string file) => images[kind] = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/_Project/Core/Editor/TraceIcons/" + file + ".png");

        public void Draw(Rect rect, Enum brush, Facing facing)
        {
            if (Event.current.type != EventType.Repaint) return;
            var matrix = GUI.matrix;
            try
            {
                GUIUtility.RotateAroundPivot(90f * (int)facing, rect.center);
                if (images.TryGetValue(brush, out var texture) && texture != null)
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                else GUI.Label(rect, brush.ToString(), EditorStyles.centeredGreyMiniLabel);
            }
            finally { GUI.matrix = matrix; }
        }

        public T Palette<T>(T selected, Facing facing) where T : struct, Enum
        {
            int i = 0;
            foreach (T brush in Enum.GetValues(typeof(T)))
            {
                if (Convert.ToInt32(brush) == 0) continue;
                if (i % 4 == 0) EditorGUILayout.BeginHorizontal();
                var r = GUILayoutUtility.GetRect(42, 39, GUILayout.Width(46));
                string name = ObjectNames.NicifyVariableName(brush.ToString());
                if (GUI.Toggle(r, brush.Equals(selected), new GUIContent("", name), GUI.skin.button)) selected = brush;
                Draw(new Rect(r.center.x - 16, r.center.y - 16, 32, 32), brush,
                    brush is Prop p && (TraceLevelPlan.RandomFacing(p) || p == Prop.Goal) ? Facing.North : facing);
                if (++i % 4 == 0) EditorGUILayout.EndHorizontal();
            }
            if (i % 4 != 0) EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(selected.ToString()), EditorStyles.miniBoldLabel);
            return selected;
        }
    }
}
