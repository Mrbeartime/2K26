using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Tools > TMP > Create Thai+English Font Assets
// Builds Regular + Bold TMP font assets for the 3 font packages in Assets/Fonts.
// Each asset holds Thai + English (Basic Latin) in ONE static atlas (no "Atlas 1" sub mesh).
// Regular and Bold are linked through the Font Weights table, so <b> uses the Bold file.
public static class CreateThaiEnglishTMPFonts
{
    const string SourceDir = "Assets/Fonts/";
    const string AtlasDir = "Assets/_Project/UI/Fonts/";

    // family name, ttf Regular, ttf Bold
    static readonly string[][] Families =
    {
        new[] { "NotoSansThai",     SourceDir + "Noto_Sans_Thai/static/NotoSansThai-Regular.ttf",       SourceDir + "Noto_Sans_Thai/static/NotoSansThai-Bold.ttf" },
        new[] { "PlaypenSansThai",  SourceDir + "Playpen_Sans_Thai/static/PlaypenSansThai-Regular.ttf", SourceDir + "Playpen_Sans_Thai/static/PlaypenSansThai-Bold.ttf" },
        new[] { "RedactedScript",   SourceDir + "Redacted_Script/RedactedScript-Regular.ttf",           SourceDir + "Redacted_Script/RedactedScript-Bold.ttf" },
    };

    static string Chars()
    {
        var sb = new StringBuilder();
        for (int c = 0x20; c <= 0x7E; c++) sb.Append((char)c);          // English
        for (int c = 0x0E01; c <= 0x0E3A; c++) sb.Append((char)c);      // Thai letters, vowels, tone marks
        for (int c = 0x0E3F; c <= 0x0E5B; c++) sb.Append((char)c);      // Thai baht sign .. digits
        sb.Append(' ').Append('​').Append('…').Append('◌'); // nbsp, zero-width space, ellipsis, dotted circle
        return sb.ToString();
    }

    static TMP_FontAsset Build(string ttf, string name, StringBuilder log)
    {
        string path = AtlasDir + name + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing) { log.AppendLine("kept existing: " + path); return existing; }

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (!font) { log.AppendLine("MISSING font file: " + ttf); return null; }

        var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        fa.name = name + " SDF";
        fa.TryAddCharacters(Chars(), out string missing);
        if (!string.IsNullOrEmpty(missing))
            log.AppendLine($"{name}: {missing.Length} chars not in this font (use fallback): U+{(missing.Length > 0 ? ((int)missing[0]).ToString("X4") : "")}...");
        fa.atlasPopulationMode = AtlasPopulationMode.Static;

        AssetDatabase.CreateAsset(fa, path);
        fa.material.name = name + " SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        for (int i = 0; i < fa.atlasTextures.Length; i++)
        {
            fa.atlasTextures[i].name = name + " SDF Atlas" + (i > 0 ? " " + i : "");
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
        }
        EditorUtility.SetDirty(fa);
        log.AppendLine("created: " + path);
        return fa;
    }

    [MenuItem("Tools/TMP/Create Thai+English Font Assets")]
    static void Run()
    {
        var log = new StringBuilder();
        foreach (var f in Families)
        {
            var reg = Build(f[1], f[0] + "-Regular", log);
            var bold = Build(f[2], f[0] + "-Bold", log);
            if (!reg || !bold) continue;

            // One family: <b> and FontStyle.Bold on the Regular asset resolve to the Bold asset.
            reg.fontWeightTable[4].regularTypeface = reg;
            reg.fontWeightTable[7].regularTypeface = bold;
            bold.fontWeightTable[4].regularTypeface = reg;
            bold.fontWeightTable[7].regularTypeface = bold;

            // Redacted Script has no Thai: send Thai to Noto Thai.
            if (f[0] == "RedactedScript")
            {
                var thaiReg = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AtlasDir + "NotoSansThai-Regular SDF.asset");
                var thaiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AtlasDir + "NotoSansThai-Bold SDF.asset");
                if (thaiReg) reg.fallbackFontAssetTable = new List<TMP_FontAsset> { thaiReg };
                if (thaiBold) bold.fallbackFontAssetTable = new List<TMP_FontAsset> { thaiBold };
            }
            EditorUtility.SetDirty(reg);
            EditorUtility.SetDirty(bold);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TMP Thai+English fonts:\n" + log);
    }
}
