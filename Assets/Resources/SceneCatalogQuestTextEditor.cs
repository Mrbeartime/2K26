#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectD.Menus
{
    internal sealed class SceneCatalogQuestTextEditor
    {
        private static readonly Dictionary<string, GUIContent> FieldLabels = new()
        {
            ["title"] = new("Title"),
            ["description"] = new("Description"),
            ["depth"] = new("Depth", "A value from 0 to 10."),
            ["characters"] = new("Characters", "Character aliases in display order, separated by commas."),
            ["paper"] = new("Paper", "Use default or the Sprite name from QuestPaper's Paper Styles."),
            ["unlockMain"] = new("Required Main Levels", "Comma-separated level IDs."),
            ["unlockSecret"] = new("Required Secret Levels", "Comma-separated secret IDs."),
            ["unlockCount"] = new("Completed Level Count", "Leave blank to disable this condition."),
            ["unlockMinDepth"] = new("Minimum Depth", "Leave blank to disable this condition."),
            ["unlockTotalDepth"] = new("Total Depth", "Leave blank to disable this condition.")
        };

        private string path;
        private string selectedQuestId;
        private string importedText;
        private string originalSection;
        private Dictionary<string, string> draft;
        private bool dirty;
        private string status;
        private MessageType statusType;

        public void Draw(SceneCatalog catalog)
        {
            string assetPath = AssetDatabase.GetAssetPath(catalog.questData);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorGUILayout.HelpBox("Assign a quest text file under Assets to edit it here.", MessageType.Info);
                return;
            }

            try
            {
                DrawQuest(catalog, assetPath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is FormatException)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            }
        }

        private void DrawQuest(SceneCatalog catalog, string assetPath)
        {
            string source = catalog.questData.text;
            if (path != assetPath || (!dirty && importedText != source))
            {
                bool changedPath = path != assetPath;
                path = assetPath;
                importedText = source;
                draft = null;
                dirty = false;
                if (changedPath) status = null;
                catalog.InvalidateQuestData();
            }

            var entries = catalog.Levels.Where(level => level != null)
                .Select(level => (Key: $"level:{level.id}", Label: $"{level.id} · {level.Title}"))
                .Concat(catalog.SecretLevels.Where(level => level != null)
                    .Select(level => (Key: $"secret:{level.id}", Label: $"Secret · {level.title}")))
                .ToArray();
            if (entries.Length == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Edit Quest Data", EditorStyles.boldLabel);
            int index = Mathf.Max(0, Array.FindIndex(entries, entry => entry.Key == selectedQuestId));
            using (new EditorGUI.DisabledScope(dirty))
                index = EditorGUILayout.Popup("Quest", index, entries.Select(entry => entry.Label).ToArray());
            string selectedKey = entries[index].Key;
            if (draft == null || selectedKey != selectedQuestId) LoadDraft(catalog, selectedKey, source);

            EditorGUI.BeginChangeCheck();
            foreach (string field in SceneCatalogQuestData.FieldNames)
            {
                string value = draft[field];
                var label = FieldLabels[field];
                if (field == "description")
                {
                    EditorGUILayout.LabelField(label);
                    draft[field] = EditorGUILayout.TextArea(value, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 3f));
                }
                else
                {
                    draft[field] = EditorGUILayout.TextField(label, value);
                }
            }
            dirty |= EditorGUI.EndChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!dirty && originalSection != null))
                {
                    if (GUILayout.Button("Save to TXT"))
                    {
                        if (SaveQuest(catalog, assetPath, selectedQuestId, originalSection, draft, out string error))
                        {
                            ImportAndLoad(catalog, assetPath);
                            SetStatus("Quest saved to the text file.", MessageType.Info);
                        }
                        else
                        {
                            SetStatus(error, MessageType.Warning);
                        }
                    }
                }
                if (GUILayout.Button("Reload from TXT"))
                {
                    ImportAndLoad(catalog, assetPath);
                    SetStatus("Quest reloaded from the text file.", MessageType.Info);
                }
            }
            if (status != null) EditorGUILayout.HelpBox(status, statusType);
        }

        private void ImportAndLoad(SceneCatalog catalog, string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            catalog.InvalidateQuestData();
            importedText = catalog.questData.text;
            LoadDraft(catalog, selectedQuestId, importedText);
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
        }

        private void LoadDraft(SceneCatalog catalog, string selectedKey, string source)
        {
            selectedQuestId = selectedKey;
            originalSection = ReadSection(source, selectedKey);
            if (selectedKey.StartsWith("level:", StringComparison.Ordinal))
            {
                var level = catalog.FindLevel(int.Parse(selectedKey.Substring("level:".Length)));
                draft = CreateDraft(catalog, level.Title, level.description, level.depth, level.characterAvailable, level.paperSprite, level.unlockConditions);
            }
            else
            {
                var level = catalog.FindSecretLevel(selectedKey.Substring("secret:".Length));
                draft = CreateDraft(catalog, level.title, level.description, level.depth, level.characterAvailable, level.paperSprite, level.unlockConditions);
            }
            if (originalSection != null)
            {
                foreach (string line in originalSection.Split('\n'))
                {
                    string field = SceneCatalogQuestData.ReadField(SceneCatalogQuestData.TrimLine(line), out string value);
                    if (field != null) draft[field] = field.Equals("description", StringComparison.OrdinalIgnoreCase)
                        ? SceneCatalogQuestData.DecodeDescription(value)
                        : value;
                }
            }
            dirty = false;
            status = null;
        }

        private static Dictionary<string, string> CreateDraft(SceneCatalog catalog, string title, string description, int depth,
            Sprite[] icons, Sprite paper, LevelUnlockConditions unlock) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = title ?? string.Empty,
            ["description"] = description ?? string.Empty,
            ["depth"] = depth.ToString(),
            ["characters"] = string.Join(", ", (icons ?? Array.Empty<Sprite>()).Where(icon => icon != null).Select(catalog.QuestCharacterName)),
            ["paper"] = paper != null ? paper.name : "default",
            ["unlockMain"] = unlock?.requireCompletedLevelList == true ? string.Join(", ", unlock.requiredMainLevelIds ?? Array.Empty<int>()) : "",
            ["unlockSecret"] = unlock?.requireCompletedLevelList == true ? string.Join(", ", unlock.requiredSecretLevelIds ?? Array.Empty<string>()) : "",
            ["unlockCount"] = unlock?.requireCompletedLevelCount == true ? unlock.completedLevelCount.ToString() : "",
            ["unlockMinDepth"] = unlock?.requireMinimumDepth == true ? unlock.minimumDepth.ToString() : "",
            ["unlockTotalDepth"] = unlock?.requireTotalDepth == true ? unlock.totalDepth.ToString() : ""
        };

        internal static string ReadSection(string text, string id)
        {
            var range = FindSection(text, id);
            return range.Start < 0 ? null : text.Substring(range.Start, range.Length);
        }

        private static (int Start, int Length) FindSection(string text, string id)
        {
            int start = -1;
            int end = text.Length;
            for (int lineStart = 0; lineStart < text.Length;)
            {
                int lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = text.Length;
                string line = SceneCatalogQuestData.TrimLine(text.Substring(lineStart, lineEnd - lineStart));
                string header = SceneCatalogQuestData.ReadHeader(line);
                if (header != null)
                {
                    if (start >= 0 && end == text.Length) end = lineStart;
                    if (header == id)
                    {
                        if (start >= 0) throw new FormatException($"Duplicate [{id}] sections. Fix the text file and reload it.");
                        start = lineStart;
                    }
                }
                lineStart = lineEnd + 1;
            }
            return (start, start < 0 ? 0 : end - start);
        }

        internal static bool SaveQuest(SceneCatalog catalog, string assetPath, string id, string baseline,
            IReadOnlyDictionary<string, string> values, out string error)
        {
            string latest = File.ReadAllText(assetPath, Encoding.UTF8);
            var range = FindSection(latest, id);
            string existing = range.Start < 0 ? null : latest.Substring(range.Start, range.Length);
            if (NormalizeLineEndings(existing)?.TrimEnd() != NormalizeLineEndings(baseline)?.TrimEnd())
            {
                error = "This quest changed in the text file. Reload from TXT before saving it here.";
                return false;
            }

            string eol = latest.Contains("\r\n") ? "\r\n" : "\n";
            string section = WriteFields(existing ?? $"[{id}]{eol}", values, eol);
            if (!SceneCatalogQuestData.IsValidSection(catalog, id, section))
            {
                error = "Fix the invalid quest fields before saving. See the Console for the field details.";
                return false;
            }

            string updated = range.Start >= 0
                ? latest.Substring(0, range.Start) + section + latest.Substring(range.Start + range.Length)
                : latest.TrimEnd('\r', '\n') + eol + eol + section;
            if (!string.Equals(updated, latest, StringComparison.Ordinal))
            {
                string temporary = assetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, updated, new UTF8Encoding(false));
                    if (File.ReadAllText(assetPath, Encoding.UTF8) != latest)
                    {
                        error = "The text file changed while saving. Reload from TXT and try again.";
                        return false;
                    }
                    File.Replace(temporary, assetPath, null);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
            error = null;
            return true;
        }

        private static string WriteFields(string section, IReadOnlyDictionary<string, string> values, string eol)
        {
            var lines = NormalizeLineEndings(section).Split('\n').ToList();
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Count; i++)
            {
                string field = SceneCatalogQuestData.ReadField(SceneCatalogQuestData.TrimLine(lines[i]), out string oldValue);
                if (field == null || !values.TryGetValue(field, out string value)) continue;
                written.Add(field);
                value = EncodeValue(value);
                if (value != oldValue) lines[i] = field + "=" + value;
            }

            int insertAt = lines.Count;
            while (insertAt > 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1])) insertAt--;
            foreach (string field in SceneCatalogQuestData.FieldNames)
            {
                if (written.Contains(field) || !values.TryGetValue(field, out string value)) continue;
                value = EncodeValue(value);
                if (value.Length > 0 || !field.StartsWith("unlock", StringComparison.Ordinal))
                    lines.Insert(insertAt++, field + "=" + value);
            }
            if (lines[lines.Count - 1].Length > 0) lines.Add(string.Empty);
            return string.Join(eol, lines);
        }

        private static string EncodeValue(string value) => NormalizeLineEndings(value.Trim()).Replace("\r", "\n").Replace("\n", "\\n");

        private static string NormalizeLineEndings(string text) => text?.Replace("\r\n", "\n");
    }
}
#endif
