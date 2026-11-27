using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectD.Menus
{
    internal sealed class SceneCatalogQuestData
    {
        internal static readonly string[] FieldNames =
        {
            "title", "description", "depth", "characters", "paper",
            "unlockMain", "unlockSecret", "unlockCount", "unlockMinDepth", "unlockTotalDepth"
        };

        private readonly SceneCatalog catalog;
        private readonly HashSet<int> mainIds;
        private readonly HashSet<string> secretIds;
        private readonly Dictionary<string, Section> sections = new(StringComparer.Ordinal);

        public List<LevelDefinition> Levels { get; }
        public List<SecretLevelDefinition> SecretLevels { get; }

        public SceneCatalogQuestData(SceneCatalog catalog, List<LevelDefinition> levels, List<SecretLevelDefinition> secrets)
            : this(catalog, catalog.questData.text, levels, secrets)
        {
            Levels = levels.ConvertAll(ResolveLevel);
            SecretLevels = secrets.ConvertAll(ResolveSecret);
            ApplyTextOverlay();
            foreach (string id in sections.Keys)
                Warn($"[{id}] does not match a registered level.");
        }

        private SceneCatalogQuestData(SceneCatalog catalog, string text, IReadOnlyList<LevelDefinition> levels,
            IReadOnlyList<SecretLevelDefinition> secrets)
        {
            this.catalog = catalog;
            mainIds = levels.Where(level => level != null).Select(level => level.id).ToHashSet();
            secretIds = secrets.Where(level => level != null).Select(level => level.id).ToHashSet(StringComparer.Ordinal);
            ReadSections(text);
        }

        private LevelDefinition ResolveLevel(LevelDefinition level)
        {
            if (level == null || !TryReadQuest($"level:{level.id}", out var entry)) return level;
            var result = level.Copy();
            (result.title, result.description, result.depth, result.characterAvailable, result.paperSprite, result.unlockConditions) = entry;
            return result;
        }

        private SecretLevelDefinition ResolveSecret(SecretLevelDefinition level)
        {
            if (level == null || !TryReadQuest($"secret:{level.id}", out var entry)) return level;
            var result = level.Copy();
            (result.title, result.description, result.depth, result.characterAvailable, result.paperSprite, result.unlockConditions) = entry;
            return result;
        }

        // Optional language overlay (Resources/QuestCatalog_en.txt or QuestCatalog_th.txt). It has the same [level:1] /
        // [secret:ID] sections, but only title and description are read; everything else comes from QuestCatalog.txt.
        private void ApplyTextOverlay()
        {
            var file = GameLanguage.LoadOverlay(catalog.questData);
            if (file == null) return;

            var overlay = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            Dictionary<string, string> current = null;
            foreach (string rawLine in file.text.Split('\n'))
            {
                string line = TrimLine(rawLine);
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

                string id = ReadHeader(line);
                if (id != null)
                {
                    overlay[id] = current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                string field = ReadField(line, out string value);
                if (current != null && field != null &&
                    (field.Equals("title", StringComparison.OrdinalIgnoreCase) || field.Equals("description", StringComparison.OrdinalIgnoreCase)))
                    current[field] = value;
            }

            for (int i = 0; i < Levels.Count; i++)
            {
                if (Levels[i] == null || !overlay.TryGetValue($"level:{Levels[i].id}", out var fields)) continue;
                var copy = Levels[i].Copy();
                if (fields.TryGetValue("title", out string title) && title.Length > 0) copy.title = title;
                if (fields.TryGetValue("description", out string description)) copy.description = DecodeDescription(description);
                Levels[i] = copy;
            }

            for (int i = 0; i < SecretLevels.Count; i++)
            {
                if (SecretLevels[i] == null || !overlay.TryGetValue($"secret:{SecretLevels[i].id}", out var fields)) continue;
                var copy = SecretLevels[i].Copy();
                if (fields.TryGetValue("title", out string title) && title.Length > 0) copy.title = title;
                if (fields.TryGetValue("description", out string description)) copy.description = DecodeDescription(description);
                SecretLevels[i] = copy;
            }
        }

        private void ReadSections(string text)
        {
            Section section = null;
            int lineNumber = 0;
            foreach (string rawLine in text.Split('\n'))
            {
                lineNumber++;
                string line = TrimLine(rawLine);
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

                string id = ReadHeader(line);
                if (id != null)
                {
                    section = new Section();
                    if (!sections.TryAdd(id, section))
                    {
                        sections[id].Invalid = true;
                        section.Invalid = true;
                        Warn($"Line {lineNumber}: duplicate section [{id}].");
                    }
                    continue;
                }

                string field = ReadField(line, out string fieldValue);
                if (section == null || field == null || !section.Fields.TryAdd(field, fieldValue))
                {
                    if (section != null) section.Invalid = true;
                    Warn($"Line {lineNumber}: expected a unique quest field and value.");
                }
            }
        }

        private bool TryReadQuest(string id, out (string Title, string Description, int Depth, Sprite[] Characters,
            Sprite Paper, LevelUnlockConditions Unlock) entry)
        {
            entry = default;
            if (!sections.Remove(id, out var section) || section.Invalid) return false;
            var values = section.Fields;
            if (!values.TryGetValue("title", out string title) ||
                !values.TryGetValue("description", out string description) ||
                !values.TryGetValue("depth", out string depthText) ||
                !int.TryParse(depthText, out int depth) || depth < 0 || depth > 10 ||
                !values.TryGetValue("characters", out string charactersText) ||
                !values.TryGetValue("paper", out string paperName))
            {
                Warn($"[{id}] needs title, description, depth (0–10), characters and paper.");
                return false;
            }

            string[] names = SplitList(charactersText);
            var characters = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                characters[i] = catalog.FindQuestCharacter(names[i]);
                if (characters[i] != null) continue;
                Warn($"[{id}] unknown character '{names[i]}'.");
                return false;
            }

            Sprite paper = null;
            if (paperName.Length > 0 && !paperName.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                paper = catalog.questPaperPrefab?.FindPaperSprite(paperName);
                if (paper == null)
                {
                    Warn($"[{id}] unknown paper sprite '{paperName}'.");
                    return false;
                }
            }

            if (!TryReadUnlock(values, out var unlock))
            {
                Warn($"[{id}] has an unknown prerequisite or an invalid unlock threshold.");
                return false;
            }

            entry = (title, DecodeDescription(description), depth, characters, paper, unlock);
            return true;
        }

        private bool TryReadUnlock(Dictionary<string, string> values, out LevelUnlockConditions unlock)
        {
            unlock = null;
            string[] requiredMain = SplitList(values.GetValueOrDefault("unlockMain"));
            string[] requiredSecrets = SplitList(values.GetValueOrDefault("unlockSecret"));
            var main = new int[requiredMain.Length];
            for (int i = 0; i < main.Length; i++)
                if (!int.TryParse(requiredMain[i], out main[i]) || !mainIds.Contains(main[i])) return false;

            if (requiredSecrets.Any(id => !secretIds.Contains(id)) ||
                !TryReadThreshold(values, "unlockCount", int.MaxValue, out var count) ||
                !TryReadThreshold(values, "unlockMinDepth", 10, out var minDepth) ||
                !TryReadThreshold(values, "unlockTotalDepth", int.MaxValue, out var totalDepth))
                return false;

            unlock = new LevelUnlockConditions
            {
                requireCompletedLevelList = main.Length > 0 || requiredSecrets.Length > 0,
                requiredMainLevelIds = main,
                requiredSecretLevelIds = requiredSecrets,
                requireCompletedLevelCount = count.HasValue,
                completedLevelCount = count.GetValueOrDefault(),
                requireMinimumDepth = minDepth.HasValue,
                minimumDepth = minDepth.GetValueOrDefault(),
                requireTotalDepth = totalDepth.HasValue,
                totalDepth = totalDepth.GetValueOrDefault()
            };
            return true;
        }

        private static bool TryReadThreshold(Dictionary<string, string> values, string key, int maximum, out int? threshold)
        {
            threshold = null;
            if (!values.TryGetValue(key, out string text) || text.Length == 0) return true;
            if (!int.TryParse(text, out int value) || value < 0 || value > maximum) return false;
            threshold = value;
            return true;
        }

        private static string[] SplitList(string text) => string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(',').Select(value => value.Trim()).ToArray();

        internal static string TrimLine(string line) => line.Trim().TrimStart('\uFEFF').TrimStart();

        internal static string DecodeDescription(string value) => value.Replace("\\n", "\n");

        internal static string ReadHeader(string line) => line.StartsWith("[") && line.EndsWith("]")
            ? line.Substring(1, line.Length - 2).Trim()
            : null;

        internal static string ReadField(string line, out string value)
        {
            value = null;
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0) return null;
            string name = line.Substring(0, equalsIndex).Trim();
            if (!FieldNames.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
            value = line.Substring(equalsIndex + 1).Trim();
            return name;
        }

        private void Warn(string message) => Debug.LogWarning($"{catalog.questData.name}: {message}", catalog);

        private sealed class Section
        {
            public readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase);
            public bool Invalid;
        }

#if UNITY_EDITOR
        internal static bool IsValidSection(SceneCatalog catalog, string sectionId, string sectionText)
        {
            var validator = new SceneCatalogQuestData(catalog, sectionText, catalog.Levels, catalog.SecretLevels);
            return validator.TryReadQuest(sectionId, out _);
        }
#endif
    }
}
