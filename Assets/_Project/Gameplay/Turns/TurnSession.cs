using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectD.Gameplay.Turns
{
    public interface IReadOnlyTurnState
    {
        event Action StateChanged;
        event Action<bool, string> GameEnded;
        int MaximumTurns { get; }
        int RoundNumber { get; }
        int TurnsLeft { get; }
        PlayerMove CurrentPlayer { get; }
        IReadOnlyList<PlayerMove> Players { get; }
        TurnScheduleData ScheduleData { get; }
        bool IsGameComplete { get; }
        bool IsIntroPlaying { get; }
        bool IsEnemyPhase { get; }
        bool IsTrapPhase { get; }
        bool CanSelectAction { get; }
        bool PlayerWon { get; }
        string GameOverReason { get; }
        string Status { get; }
        bool IsWaitingForMovement { get; }
        bool IsMoveTargeting { get; }
        bool IsSkillTargeting { get; }
        bool CanEndTurn { get; }
        bool IsPointerOverTurnUI(Vector2 screenPosition);
        bool CanChooseMove { get; }
        bool CanChooseReact { get; }
        bool CanChooseSkill { get; }
        bool CanPlayerReact(PlayerMove player);
        bool CanPlayerUseSkill(PlayerMove player);
        bool CanSelectPlayer(PlayerMove player);
        bool HasPlayerMoved(PlayerMove player);
        bool IsPlayerFinished(PlayerMove player);
        PlayerMove FindNextSelectablePlayer(PlayerMove fromPlayer, bool forward, bool wrap = true);
    }

    public interface ITurnCommands
    {
        bool TrySelectPlayer(PlayerMove player, bool playSelectionSound = true);
        void ChooseMove();
        void ChooseSkip();
        void RestartCurrentScene();
        void ChooseReact();
        void MoveToTarget(Tile tile);
        void UseSkillOn(Tile tile);
        void UseSkillOn(RogueDoor door);
        void ChooseSkill();
        void ChooseEndTurn();
        void CancelSkill();
        void CancelMoveTargeting();
        bool SkipExhaustedSkill();
    }

    public interface ITurnController : IReadOnlyTurnState, ITurnCommands { }

    internal interface IWorldHighlights
    {
        void ClearHighlights();
        void ClearActionPreview();
        void BeginActionPreview();
        void ShowInteractPreview(PlayerMove player);
        void ShowMoveRange(PlayerMove player, bool preview = false);
    }

    internal interface IGameplayOverlay
    {
        bool BlocksGameplayInput { get; }
        bool IsPreviewingAction { get; }
        bool IsInteractMode { get; }
        InputAction Select { get; }
        InputAction ShowMap { get; }
        InputAction CameraMove { get; }
        InputAction Zoom { get; }
        void OpenActionSelection(PlayerMove player, bool playSelectionSound, bool confirmMove);
        IEnumerator AnimateTurnProgress(IReadOnlyTurnState turns);
    }

    // Each additive scene owns its subscriptions. A menu can never handle a level's events.
    internal sealed class TurnSession
    {
        private static readonly Dictionary<Scene, TurnSession> scenes = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            scenes.Clear();
            SceneManager.sceneUnloaded -= Remove;
            SceneManager.sceneUnloaded += Remove;
        }
        private static void Remove(Scene scene) => scenes.Remove(scene);
        internal static TurnSession For(Scene scene)
        {
            if (!scenes.TryGetValue(scene, out var session))
                scenes.Add(scene, session = new TurnSession());
            return session;
        }

        internal ITurnController Turns { get; set; }
        internal IWorldHighlights Highlights { get; set; }
        internal IGameplayOverlay Overlay { get; set; }
        internal bool ModalInputBlocked { get; set; }
        internal event Action RoundAdvanced;
        internal void AdvanceRound() => RoundAdvanced?.Invoke();
        internal event Action<PlayerMove, bool> PlayerSelected;
        internal event Action<PlayerMove> MoveRangeRequested;
        internal event Action HighlightsCleared;
        internal event Action<Character, bool> SkillPreviewRequested;
        internal event Action<Transform> ActionPerformed;
        internal event Action<PlayerMove> MoveFinished;
        internal event Action<PlayerMove> PlayerDefeated;

        internal void SelectPlayer(PlayerMove player, bool sound = true) => PlayerSelected?.Invoke(player, sound);
        internal void ShowMoveRange(PlayerMove player) => MoveRangeRequested?.Invoke(player);
        internal void ClearHighlights() => HighlightsCleared?.Invoke();
        internal void PreviewSkill(Character actor, bool preview) => SkillPreviewRequested?.Invoke(actor, preview);
        internal void PlayActionZoom(Transform target) => ActionPerformed?.Invoke(target);
        internal void FinishMove(PlayerMove player) => MoveFinished?.Invoke(player);
        internal void Defeat(PlayerMove player) => PlayerDefeated?.Invoke(player);
    }
}
