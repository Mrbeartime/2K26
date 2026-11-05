using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using ProjectD.Menus;

namespace ProjectD.AfterFeature
{
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TurnGameManager))]
    public sealed class LevelStarRating : MonoBehaviour
    {
        public enum CharacterSource { PlayersInThisScene, AssignedCharacters }
        [SerializeField] private LevelStarSettings settings;
        [SerializeField] private TurnGameManager turnManager;
        [Header("Surviving characters")]
        [SerializeField] private CharacterSource characterSource;
        [SerializeField] private Character[] assignedCharacters = new Character[0];
        [SerializeField] private bool includeInactiveCharacters;
        [Header("Saving / diagnostics")]
        [SerializeField] private bool saveBestToJson = true;
        [SerializeField] private bool logResults = true;
        [Header("Last victory result (runtime)")]
        [SerializeField] private bool hasResult;
        [SerializeField, Range(0, 3)] private int starsEarned;
        [SerializeField] private int turnsRemaining;
        [SerializeField] private int charactersRemaining;
        [SerializeField] private string resultScenePath;
        [SerializeField] private bool savedToJson;
        [Header("Optional: connect your own victory UI in Hierarchy")]
        [SerializeField] private UnityEvent<int> onStarsAwarded = new();

        public bool HasResult => hasResult;
        public int StarsEarned => starsEarned;
        public int TurnsRemaining => turnsRemaining;
        public int CharactersRemaining => charactersRemaining;
        public bool SavedToJson => savedToJson;
        public event Action<int> StarsAwarded;

        private void OnEnable()
        {
            if (turnManager == null) turnManager = GetComponent<TurnGameManager>();
            if (turnManager != null) turnManager.GameEnded += OnGameEnded;
            if (turnManager != null && turnManager.IsGameComplete && turnManager.PlayerWon)
                OnGameEnded(true, string.Empty);
        }

        private void OnDisable()
        {
            if (turnManager != null) turnManager.GameEnded -= OnGameEnded;
        }

        private void OnGameEnded(bool won, string reason)
        {
            if (!won || hasResult || turnManager == null) return;
            string path = gameObject.scene.path;
            int survivors = CountSurvivingCharacters();
            int turns = Mathf.Max(0, turnManager.TurnsLeft);
            if (settings == null || !settings.TryCalculate(path, turns, survivors, out int stars))
            {
                Debug.LogWarning("[LevelStars] No settings/rule for scene: " + path + ". No stars saved.", this);
                return;
            }
            hasResult = true;
            resultScenePath = path;
            turnsRemaining = turns;
            charactersRemaining = survivors;
            starsEarned = stars;
            savedToJson = saveBestToJson && LevelProgressStore.RecordSceneStars(path, stars);
            if (logResults) Debug.Log($"[LevelStars] {path}: {stars}/3 | Turns left: {turns} | Survivors: {survivors}", this);
            StarsAwarded?.Invoke(stars);
            onStarsAwarded?.Invoke(stars);
        }

        public int CountSurvivingCharacters()
        {
            var counted = new HashSet<GameObject>();
            if (characterSource == CharacterSource.AssignedCharacters)
            {
                foreach (var character in assignedCharacters) CountCharacter(character, counted);
            }
            else
                foreach (var player in FindObjectsByType<PlayerMove>(FindObjectsInactive.Include))
                    if (player != null && player.TryGetComponent(out Character character)) CountCharacter(character, counted);
            return counted.Count;
        }

        private void CountCharacter(Character character, HashSet<GameObject> counted)
        {
            if (character == null || character.IsDead || character.gameObject.scene != gameObject.scene ||
                !character.TryGetComponent(out PlayerMove player)) return;
            if (!includeInactiveCharacters && (!character.isActiveAndEnabled || !player.isActiveAndEnabled)) return;
            counted.Add(character.gameObject);
        }
    }
}
