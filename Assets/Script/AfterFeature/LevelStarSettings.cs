using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectD.AfterFeature
{
    [CreateAssetMenu(fileName = "LevelStarSettings", menuName = "After Feature/Level Star Settings")]
    public sealed class LevelStarSettings : ScriptableObject
    {
        [Serializable]
        public sealed class SceneRule
        {
#if UNITY_EDITOR
            [SerializeField] private UnityEditor.SceneAsset scene;
#endif
            [SerializeField, HideInInspector] private string scenePath;
            [Tooltip("Guaranteed stars on victory, before checking the 2/3-star thresholds.")]
            [Range(1, 3)] public int minimumStarsOnVictory = 1;
            [Header("2 stars: BOTH minimums must be met")]
            [Min(0)] public int twoStarMinimumTurns = 1;
            [Min(0)] public int twoStarMinimumCharacters = 2;
            [Header("3 stars: BOTH minimums must be met")]
            [Min(0)] public int threeStarMinimumTurns = 3;
            [Min(0)] public int threeStarMinimumCharacters = 3;
            public string ScenePath => scenePath;

            public int Calculate(int turnsRemaining, int charactersRemaining)
            {
                int turns = Mathf.Max(0, turnsRemaining);
                int characters = Mathf.Max(0, charactersRemaining);
                if (turns >= threeStarMinimumTurns && characters >= threeStarMinimumCharacters) return 3;
                int minimum = Mathf.Clamp(minimumStarsOnVictory, 1, 3);
                if (turns >= twoStarMinimumTurns && characters >= twoStarMinimumCharacters) return Mathf.Max(minimum, 2);
                return minimum; // Called only after confirmed victory.
            }

#if UNITY_EDITOR
            public void Validate()
            {
                scenePath = scene != null ? UnityEditor.AssetDatabase.GetAssetPath(scene) : string.Empty;
                minimumStarsOnVictory = Mathf.Clamp(minimumStarsOnVictory, 1, 3);
                twoStarMinimumTurns = Mathf.Max(0, twoStarMinimumTurns);
                twoStarMinimumCharacters = Mathf.Max(0, twoStarMinimumCharacters);
                threeStarMinimumTurns = Mathf.Max(twoStarMinimumTurns, threeStarMinimumTurns);
                threeStarMinimumCharacters = Mathf.Max(twoStarMinimumCharacters, threeStarMinimumCharacters);
            }
#endif
        }

        [Tooltip("Choose each scene and its thresholds. Unlisted scenes do not award stars.")]
        [SerializeField] private List<SceneRule> sceneRules = new();

        public bool TryCalculate(string scenePath, int turnsRemaining, int charactersRemaining, out int stars)
        {
            foreach (var rule in sceneRules)
                if (rule != null && !string.IsNullOrEmpty(scenePath) && rule.ScenePath == scenePath)
                {
                    stars = rule.Calculate(turnsRemaining, charactersRemaining);
                    return true;
                }
            stars = 0;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var paths = new HashSet<string>();
            foreach (var rule in sceneRules)
            {
                if (rule == null) continue;
                rule.Validate();
                if (!string.IsNullOrEmpty(rule.ScenePath) && !paths.Add(rule.ScenePath))
                    Debug.LogWarning("Duplicate star rule: " + rule.ScenePath + ". The first rule is used.", this);
            }
        }
#endif
    }
}
