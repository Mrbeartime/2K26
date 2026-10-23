using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore;

public static class GuideFontSetup
{
    private const string FontRoot = "Assets/Fonts/";
    [MenuItem("Tools/Guide/Setup and validate Thai fonts")]
    public static void Setup()
    {
        FontEngine.InitializeFontEngine();
        var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot + "NotoSansThai-Regular SDF.asset");
        regular.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        var regularData = new SerializedObject(regular);
        regularData.FindProperty("m_SourceFontFile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "Noto_Sans_Thai/static/NotoSansThai-Regular.ttf");
        regularData.ApplyModifiedPropertiesWithoutUndo();
        regular.isMultiAtlasTexturesEnabled = true;
        var boldPath = FontRoot + "NotoSansThai-Bold SDF.asset";
        var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(boldPath);
        if (bold == null)
        {
            bold = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(FontRoot + "Noto_Sans_Thai/static/NotoSansThai-Bold.ttf"), 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
            bold.name = "NotoSansThai-Bold SDF";
            AssetDatabase.CreateAsset(bold, boldPath);
            AssetDatabase.AddObjectToAsset(bold.material, bold);
            foreach (var texture in bold.atlasTextures) AssetDatabase.AddObjectToAsset(texture, bold);
        }
        regular.fontWeightTable[4].regularTypeface = regular;
        regular.fontWeightTable[7].regularTypeface = bold;
        bold.fontWeightTable[4].regularTypeface = regular;
        bold.fontWeightTable[7].regularTypeface = bold;
        string source = Resources.Load<TextAsset>("GuideText").text;
        string characters = new string(Regex.Replace(source, "<[^>]+>", "").Where(c => !char.IsControl(c)).Concat(Enumerable.Range(32, 95).Select(c => (char)c)).Distinct().ToArray());
        foreach (var font in new[] { regular, bold })
        {
            string pending = new string(characters.Where(c => !font.HasCharacter(c)).ToArray());
            if (pending.Length > 0 && !font.TryAddCharacters(pending, out string missing, true))
                throw new Exception(font.name + " missing characters: " + missing);
            foreach (var texture in font.atlasTextures)
                if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bom/GuideScene.unity", OpenSceneMode.Single);
        var texts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)).ToArray();
        foreach (var text in texts)
        {
            text.font = regular;
            text.fontSharedMaterial = regular.material;
            text.richText = true;
            text.fontFeatures = new System.Collections.Generic.List<OTL_FeatureTag> { OTL_FeatureTag.kern, OTL_FeatureTag.mark, OTL_FeatureTag.mkmk };
            EditorUtility.SetDirty(text);
        }
        var manager = UnityEngine.Object.FindAnyObjectByType<GuideUIManager>();
        var serialized = new SerializedObject(manager);
        var body = (TMP_Text)serialized.FindProperty("bodyText").objectReferenceValue;
        var title = (TMP_Text)serialized.FindProperty("titleText").objectReferenceValue;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 3;
        body.paragraphSpacing = 2;
        body.margin = new Vector4(8, 6, 8, 6);
        body.enableAutoSizing = true;
        body.fontSizeMin = 14;
        body.fontSizeMax = 20;
        GuideTextDatabase.Reload();
        body.text = GuideTextDatabase.Get("goal01");
        title.text = serialized.FindProperty("GoalEntries").GetArrayElementAtIndex(0).FindPropertyRelative("title").stringValue;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var report = new StringBuilder("Guide font validation\n");
        report.AppendLine("Scene TMP components: " + texts.Length);
        foreach (Match match in Regex.Matches(source, @"(?m)^\[([^\]\r\n]+)\]"))
        {
            string key = match.Groups[1].Value;
            string content = GuideTextDatabase.Get(key);
            foreach (string style in new[] { "original", "bold", "underline", "bold underline" })
            {
                string plain = Regex.Replace(content, "<[^>]+>", "");
                body.text = style == "original" ? content : style == "bold" ? "<b>" + plain + "</b>" : style == "underline" ? "<u>" + content + "</u>" : "<b><u>" + plain + "</u></b>";
                body.ForceMeshUpdate(true, true);
                if (body.textInfo.characterCount == 0) throw new Exception("Empty mesh: " + key);
                if (style.Contains("bold") && !body.textInfo.characterInfo.Take(body.textInfo.characterCount).Any(c => c.isVisible && c.fontAsset == bold)) throw new Exception("Bold font not selected: " + key);
                if (body.isTextOverflowing) throw new Exception("Text overflows: " + key + " / " + style);
                report.AppendLine(key + " / " + style + ": characters=" + body.textInfo.characterCount + ", vertices=" + body.textInfo.meshInfo.Sum(m => m.vertexCount) + ", size=" + body.fontSize + ", overflow=" + body.isTextOverflowing);
            }
        }
        body.text = GuideTextDatabase.Get("goal01");
        body.ForceMeshUpdate(true, true);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GuideFontValidation.txt", report.ToString());
        AssetDatabase.SaveAssets();
        Debug.Log("GUIDE_FONT_VALIDATION_SUCCESS\n" + report);
    }
}
