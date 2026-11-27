using ProjectD.Gameplay.Turns;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;//DO NOT REMOVE

/// <summary>
/// Coordinates selection, action resolution, and round transitions for this level.
/// Each player can be selected in any order, once per round.
/// </summary>
[DefaultExecutionOrder(-100)]
public class TurnGameManager : MonoBehaviour, ITurnController
{
    private TurnSession session;
    [Tooltip("When assigned, these rules set the turn limit and enemy delay when the level starts.")]
    [SerializeField] private ProjectD.Data.LevelTurnRules rules;
    public int MaximumTurns => maximumTurns;

    public static TurnGameManager Instance { get; private set; }
    [SerializeField] private TurnScheduleData turnScheduleData;
    [SerializeField] private CutSceneCharacter entranceCutscene;
    public bool IsIntroPlaying { get; private set; }
    public TurnScheduleData ScheduleData => turnScheduleData;
    public int RoundNumber => ElapsedTurn;

    private enum ActionMode { None, MoveTargeting, SkillTargeting }

    [Header("Fallback settings for levels without a rules asset")]
    [SerializeField, Min(1)] public int maximumTurns = 5;
    [SerializeField, Min(0f)] private float enemyPhaseDelay = 0.35f;

    private bool enableTurnShake => ProjectD.Menus.GameSettings.CameraShakeEnabled;
    [Header("Turn Screen Shake")]
    [SerializeField] private TurnScreenShake screenShake;

    [Header("Debug UI")]
    [SerializeField] private bool showTestUI = true;

    private readonly List<PlayerMove> players = new();
    private readonly HashSet<PlayerMove> finishedPlayers = new();
    private readonly HashSet<PlayerMove> movedPlayers = new();
    private PlayerMove currentPlayer;
    private int currentTurn;   // นับถอยหลัง: เริ่มที่ maximumTurns, ลดทีละ 1 เมื่อจบเทิร์น, 0 = หมดเทิร์น
    public int CurrentTurn => currentTurn; //public currentTurn (countdown)
    public int TurnsLeft => currentTurn;
    // เลขเทิร์นแบบนับขึ้น (1..maximumTurns) ใช้กับ TurnTrap และข้อความ debug
    private int ElapsedTurn => Mathf.Clamp(maximumTurns - currentTurn + 1, 1, maximumTurns);
    private bool currentPlayerSelected;
    private bool hasMoved;
    private bool waitingForMovement;
    private bool enemyPhase;
    private bool trapPhase;
    private bool gameComplete;
    private bool playerWon;
    private string gameOverReason;
    private ActionMode actionMode;
    private string status;

    public PlayerMove CurrentPlayer => currentPlayer;
    public IReadOnlyList<PlayerMove> Players => players;
    public bool IsGameComplete => gameComplete;
    public bool PlayerWon => playerWon;
    public string GameOverReason => gameOverReason;
    public bool IsMoveTargeting => actionMode == ActionMode.MoveTargeting;
    public bool IsSkillTargeting => actionMode == ActionMode.SkillTargeting;
    private bool CanCurrentPlayerReact => !gameComplete && !enemyPhase && CurrentPlayer != null &&
        currentPlayerSelected && !waitingForMovement &&
        CanPlayerReact(CurrentPlayer);

    #region AddByChitipat //review this before any change

    public event Action StateChanged;              // ยิงทุกครั้งที่ state เปลี่ยน
    public event Action<bool, string> GameEnded;   // (ชนะหรือไม่, เหตุผล)

    public bool IsEnemyPhase => enemyPhase;        // enemyPhase ยัง true ตลอด trap phase
    public bool IsWaitingForMovement => waitingForMovement;
    public string Status => status;

    public bool CanChooseMove => !gameComplete && !enemyPhase && currentPlayer != null &&
        currentPlayerSelected && !hasMoved && !waitingForMovement;

    public bool CanChooseSkill => !gameComplete && !enemyPhase && currentPlayer != null &&
        currentPlayerSelected && !waitingForMovement &&
        CanPlayerUseSkill(currentPlayer);

    public bool CanChooseReact => CanCurrentPlayerReact;
    public bool CanEndTurn => !gameComplete && !enemyPhase && !waitingForMovement;
    public bool IsPlayerFinished(PlayerMove player) => finishedPlayers.Contains(player);

    public bool CanSelectPlayer(PlayerMove player) =>
        !gameComplete && !enemyPhase && !waitingForMovement && player != null &&
        !finishedPlayers.Contains(player) && players.Contains(player);

    public bool HasPlayerMoved(PlayerMove player) => player == CurrentPlayer ? hasMoved : movedPlayers.Contains(player);

    public bool CanPlayerUseSkill(PlayerMove player) =>
        player.TryGetComponent(out Character character) && character.CanUseSkill;

    public bool CanPlayerReact(PlayerMove player) =>
        player.TryGetComponent(out Character character) && character.CanReact(player.GetCurrentTile());

    private void RaiseStateChanged()
    {
        turnScheduleData?.SetCurrentRound(RoundNumber);
        StateChanged?.Invoke();
    }
    #endregion

    public bool IsTrapPhase => trapPhase;
    public bool CanSelectAction => !gameComplete && !enemyPhase && CurrentPlayer != null &&
        currentPlayerSelected && !waitingForMovement && actionMode == ActionMode.None;

    public bool IsPointerOverTurnUI(Vector2 screenPosition) =>
        showTestUI && TurnDebugPanel.HitTest(this, screenPosition);

    private void OnGUI()
    {
        if (showTestUI && !IsIntroPlaying) TurnDebugPanel.Draw(this);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        session = TurnSession.For(gameObject.scene);
        session.Turns = this;
        session.MoveFinished += NotifyMoveFinished;
        session.PlayerDefeated += NotifyPlayerDefeated;
        // This manager awakens before the entrance component's OnEnable.
        // isActiveAndEnabled can still be false at that point.
        IsIntroPlaying = entranceCutscene != null && entranceCutscene.enabled &&
            entranceCutscene.gameObject.activeInHierarchy;
        waitingForMovement = IsIntroPlaying;
        // Older scenes without a rules asset retain their authored overrides.
        if (rules != null)
        {
            maximumTurns = rules.TurnLimit;
            enemyPhaseDelay = rules.PhaseDelay;
        }
        currentTurn = maximumTurns;   // ตั้งใน Awake เพื่อให้ค่าพร้อมก่อน Start ของทุก script
    }

    private System.Collections.IEnumerator Start()
    {
        if (turnScheduleData == null) turnScheduleData = GetComponent<TurnScheduleData>();
        if (turnScheduleData == null) turnScheduleData = gameObject.AddComponent<TurnScheduleData>();
        turnScheduleData.RefreshFromScene(maximumTurns);
        turnScheduleData.SetCurrentRound(RoundNumber);
        foreach (var player in FindObjectsByType<PlayerMove>())
            if (player.isActiveAndEnabled && player.gameObject.scene == gameObject.scene)
                players.Add(player);
        players.Sort((left, right) => GetPlayerNumber(left).CompareTo(GetPlayerNumber(right)));

        if (IsIntroPlaying)
        {
            status = "Sword entrance...";
            RaiseStateChanged();
            yield return entranceCutscene.Play();
            IsIntroPlaying = false;
        }
        StartPlayerPhase();
    }

    private static int GetPlayerNumber(PlayerMove player)
    {
        if (player == null) return int.MaxValue;
        string playerName = player.name;
        int index = playerName.Length - 1;
        while (index >= 0 && char.IsDigit(playerName[index])) index--;
        return int.TryParse(playerName[(index + 1)..], out int number) ? number : int.MaxValue;
    }

    public PlayerMove FindNextSelectablePlayer(PlayerMove fromPlayer, bool forward, bool wrap = true)
    {
        if (gameComplete || enemyPhase || waitingForMovement || players.Count == 0) return null;

        int direction = forward ? 1 : -1;
        int currentIndex = players.IndexOf(fromPlayer);
        int index = currentIndex >= 0 ? currentIndex : (forward ? -1 : 0);
        int candidates = currentIndex >= 0 ? players.Count - 1 : players.Count;
        for (int step = 0; step < candidates; step++)
        {
            index += direction;
            if (!wrap && (index < 0 || index >= players.Count)) return null;
            index = (index + players.Count) % players.Count;
            if (CanSelectPlayer(players[index])) return players[index];
        }
        return null;
    }

    public bool TrySelectPlayer(PlayerMove player) => TrySelectPlayer(player, true);

    public bool TrySelectPlayer(PlayerMove player, bool playSelectionSound)
    {
        if (!CanSelectPlayer(player)) return false;

        // Commit the selection before StateChanged refreshes the HUD.
        actionMode = ActionMode.None;
        currentPlayer = player;
        currentPlayerSelected = true;
        hasMoved = movedPlayers.Contains(player);
        session.SelectPlayer(player, playSelectionSound);

        if (!hasMoved)
        {
            session.ShowMoveRange(player);
            status = $"{player.name} selected: Move or Skill.";
        }
        else
        {
            if (SkipExhaustedSkill()) return true;
            status = $"{player.name} selected: Skill, React, or Skip.";
        }

        RaiseStateChanged();
        return true;
    }

    public void ChooseMove()
    {
        if (DiaryUI.IsOpen) return;
        if (gameComplete ||
            enemyPhase ||
            CurrentPlayer == null ||
            !currentPlayerSelected ||
            hasMoved ||
            waitingForMovement)
            return;

        actionMode = ActionMode.MoveTargeting;

        session.ClearHighlights();
        session.ShowMoveRange(CurrentPlayer);

        status = "Move: click a highlighted tile.";

        RaiseStateChanged();
    }

    public void MoveToTarget(Tile target)
    {
        if (!IsMoveTargeting || CurrentPlayer == null || target == null || !target.isWalkable) return;
        if (!CurrentPlayer.MoveToTile(target))
        {
            status = "Cannot move there. Choose another highlighted tile.";
            return;
        }
        movedPlayers.Add(CurrentPlayer);
        hasMoved = true;
        waitingForMovement = true;
        actionMode = ActionMode.None;
        session.ClearHighlights();
        status = CurrentPlayer.name + " is moving...";
        RaiseStateChanged();
    }

    public void CancelMoveTargeting()
    {
        if (!IsMoveTargeting || CurrentPlayer == null) return;
        actionMode = ActionMode.None;
        session.SelectPlayer(CurrentPlayer);
        status = "Move cancelled. Choose an action.";
        RaiseStateChanged();
    }

    public void ChooseSkill()
    {
        if (DiaryUI.IsOpen) return;
        if (SkipExhaustedSkill()) return;
        if (!CanChooseSkill)
        {
            status = "Skill unavailable: no uses left for this level.";
            RaiseStateChanged();
            return;
        }
        if (gameComplete ||
            enemyPhase ||
            CurrentPlayer == null ||
            !currentPlayerSelected ||
            waitingForMovement)
            return;

        actionMode = ActionMode.SkillTargeting;

        session.ClearHighlights();

        Character character =
            CurrentPlayer.GetComponent<Character>();

        character?.ShowSkillRange();

        status =
            "Skill: click a target tile or enemy. This ends " +
            CurrentPlayer.name +
            "'s action.";

        RaiseStateChanged();
    }

    public void ChooseReact()
    {
        if (DiaryUI.IsOpen) return;
        if (!CanCurrentPlayerReact) return;
        var diary = DiaryInteractable.FindAt(CurrentPlayer.GetCurrentTile());
        if (diary != null) { diary.Read(CurrentPlayer); return; }
        Character character = CurrentPlayer.GetComponent<Character>();
        if (!character.React(CurrentPlayer.GetCurrentTile())) return;

        session.PlayActionZoom(CurrentPlayer.transform);

        // เล่น Interact Animation
        CurrentPlayer.GetComponent<CharacterAnimationController>()?
            .PlayInteract();

        EndCurrentPlayerAction("React: door switch toggled");
    }

    public void ChooseSkip()
    {
        if (DiaryUI.IsOpen) return;
        if (gameComplete || enemyPhase || CurrentPlayer == null || !currentPlayerSelected ||
            waitingForMovement || actionMode != ActionMode.None) return;
        EndCurrentPlayerAction("Skip");
    }

    /// <summary>Immediately starts the enemy phase, even if players have not acted.</summary>
    public void ChooseEndTurn()
    {
        if (DiaryUI.IsOpen) return;
        if (!CanEndTurn) return;
        actionMode = ActionMode.None;   // จบเทิร์นได้แม้กำลังเลือกเป้าหมายอยู่
        currentPlayer = null;
        currentPlayerSelected = false;
        hasMoved = false;
        session.ClearHighlights();
        StartEnemyPhase();
    }

    public void CancelSkill()
    {
        if (!IsSkillTargeting) return;
        actionMode = ActionMode.None;
        session.ClearHighlights();
        status = "Skill cancelled. Choose an action.";
        RaiseStateChanged();
    }

    public void NotifyMoveStarted(PlayerMove player)
    {
        if (player != CurrentPlayer) return;
        movedPlayers.Add(player);
        hasMoved = true;
        waitingForMovement = true;
        actionMode = ActionMode.None;
        status = player.name + " is moving...";
        RaiseStateChanged();
    }

    public void NotifyMoveFinished(PlayerMove player)
    {
        if (player != CurrentPlayer || !waitingForMovement)
            return;

        waitingForMovement = false;

        if (WinBox.IsReachedBy(player))
        {
            ShowVictory();
            return;
        }

        // Skill range is shown by ChooseSkill when that action is selected.
        if (SkipExhaustedSkill()) return;

        status =
            "Move complete. Choose Skill, React, or Skip.";

        RaiseStateChanged();
    }

    public void NotifyPlayerDefeated(PlayerMove player)
    {
        if (gameComplete || player == null || player.gameObject.scene != gameObject.scene) return;

        players.Remove(player);
        players.RemoveAll(other => other == null ||
            (other.TryGetComponent(out Character character) && character.IsDead));
        finishedPlayers.Remove(player);
        movedPlayers.Remove(player);

        // Traps/enemies can kill the last player when no character is selected.
        if (players.Count == 0)
        {
            currentPlayer = null;
            currentPlayerSelected = false;
            waitingForMovement = false;
            actionMode = ActionMode.None;
            ShowGameOver("All Players were defeated.");
            return;
        }

        if (player != CurrentPlayer)
        {
            RaiseStateChanged();   // ตัวที่ตายไม่ใช่ตัวที่เลือกอยู่ แต่ไอคอนต้องอัปเดต
            return;
        }

        currentPlayer = null;
        currentPlayerSelected = false;
        hasMoved = false;
        waitingForMovement = false;
        actionMode = ActionMode.None;
        session.ClearHighlights();

        status = player.name + " was defeated. Select another Player.";
        RaiseStateChanged();
    }

    public void UseSkillOn(Tile target)
    {
        if (!IsSkillTargeting || CurrentPlayer == null || target == null) return;
        // Validate against the same board query used to draw the targeting range.
        if (!SkillTargets.Contains(CurrentPlayer, target))
            return;

        Character character = CurrentPlayer.GetComponent<Character>();
        if (character == null || !character.CanUseSkill)
        {
            status = "No skill uses left for this level.";
            RaiseStateChanged();
            return;
        }
        if (character is Rogue rogue)
        {
            foreach (RogueDoor door in FindObjectsByType<RogueDoor>())
            {
                if (GridManager.Instance.WorldToGrid(door.transform.position) == target.gridPosition)
                {
                    UseSkillOn(door);
                    return;
                }
            }
            status = "Rogue Skill: click the locked door.";
            return;
        }

        int usesBefore = character.SkillsUsed;
        // หันไปทางเป้าหมาย Skill
        CurrentPlayer.GetComponent<CharacterFacing>()?
            .FaceTowards(target.transform.position, true);

        // โจมตีตามระบบเดิม
        character.Attack(target);

        if (character is ArcherMan && character.SkillsUsed == usesBefore) return;

        session.PlayActionZoom(CurrentPlayer.transform);

        CurrentPlayer.GetComponent<CharacterAnimationController>()?
    .PlaySkill();
        EndCurrentPlayerAction("Skill");
    }

    public void UseSkillOn(RogueDoor door)
    {
        if (!IsSkillTargeting || CurrentPlayer == null || door == null) return;
        if (GridManager.Instance == null) return;
        Tile target = GridManager.Instance.GetTile(
            GridManager.Instance.WorldToGrid(door.transform.position));
        if (!SkillTargets.Contains(CurrentPlayer, target)) return;

        if (CurrentPlayer.GetComponent<Rogue>() is not Rogue rogue)
        {
            status = "Only Rogue can unlock a door.";
            return;
        }
        if (!rogue.IsDoorInRange(door))
        {
            status = "Rogue Skill: the door must be within " + rogue.LockpickRange + " tile(s).";
            return;
        }

        // หันหน้าเข้าหาประตูก่อนใช้ Skill
        CurrentPlayer.GetComponent<CharacterFacing>()?
            .FaceTowards(door.transform.position, true);

        if (!rogue.OPDoor(door))
        {
            status = "Rogue cannot unlock this door.";
            return;
        }
        session.PlayActionZoom(CurrentPlayer.transform);
        CurrentPlayer.GetComponent<CharacterAnimationController>()?
    .PlaySkill();
        EndCurrentPlayerAction("Rogue Skill");
    }

    public bool SkipExhaustedSkill()
    {
        if (gameComplete || enemyPhase || waitingForMovement || CurrentPlayer == null ||
            !currentPlayerSelected) return false;
        Character character = CurrentPlayer.GetComponent<Character>();
        if (character == null || character.SkillLimit < 0 || character.RemainingSkills > 0) return false;
        // Exhausting the signature skill must not consume Move or Interact.
        if (CanChooseMove || CanCurrentPlayerReact)
        {
            if (actionMode == ActionMode.SkillTargeting)
            {
                actionMode = ActionMode.None;
                session.ClearHighlights();
                status = "No skills left. Choose Move, Interact, or Skip.";
                RaiseStateChanged();
            }
            return false;
        }
        EndCurrentPlayerAction("No skills left: skipped");
        return true;
    }

    private void LateUpdate()
    {
        if (Time.timeScale == 0 || waitingForMovement || CurrentPlayer == null) return;
        if (hasMoved || actionMode == ActionMode.SkillTargeting) SkipExhaustedSkill();
    }

    private void EndCurrentPlayerAction(string actionName)
    {
        actionMode = ActionMode.None;
        waitingForMovement = false;
        session.ClearHighlights();
        finishedPlayers.Add(CurrentPlayer);
        currentPlayer = null;
        currentPlayerSelected = false;

        if (finishedPlayers.Count >= players.Count)
        {
            StartEnemyPhase();
            return;
        }

        status = actionName + " complete. Select another Player.";
        RaiseStateChanged();
    }

    private void StartEnemyPhase()
    {
        if (gameComplete || enemyPhase) return;
        // Both manual End Turn and automatic completion pass this boundary.
        enemyPhase = true;
        actionMode = ActionMode.None;
        session.ClearHighlights();
        status = "Resolving turn...";
        StartCoroutine(RunEnemyPhase());
        RaiseStateChanged();
    }

    private System.Collections.IEnumerator RunEnemyPhase() =>
        EnemyPhaseSequence.Run(gameObject.scene, session, this, enemyPhaseDelay, SetEnemyPhase, FinishEnemyPhase);

    private void SetEnemyPhase(bool resolvingTraps, string message)
    {
        trapPhase = resolvingTraps;
        if (message == null) return;
        status = message;
        RaiseStateChanged();
    }

    private void FinishEnemyPhase()
    {
        currentTurn--;
        enemyPhase = false;
        if (currentTurn <= 0)
        {
            ShowGameOver("Turn limit reached.");
            return;
        }
        PlayTurnShake();
        StartPlayerPhase();
    }

    private void StartPlayerPhase()
    {
        players.RemoveAll(player => player == null ||
            (player.TryGetComponent(out Character character) && character.IsDead));
        if (players.Count == 0)
        {
            ShowGameOver("All Players were defeated.");
            return;
        }
        finishedPlayers.Clear();
        movedPlayers.Clear();
        currentPlayer = null;
        currentPlayerSelected = false;
        hasMoved = false;
        waitingForMovement = false;
        actionMode = ActionMode.None;
        status = "Turn " + ElapsedTurn + "/" + maximumTurns + ": select any Player.";
        if (ElapsedTurn > 1) session.AdvanceRound();
        RaiseStateChanged();
    }

    private void ShowGameOver(string reason)
    {
        if (gameComplete) return;
        gameComplete = true;
        actionMode = ActionMode.None;
        playerWon = false;
        gameOverReason = reason;
        status = "GAME OVER: " + reason;
        session.ClearHighlights();

        GameEnded?.Invoke(false, reason);
        RaiseStateChanged();
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.MoveFinished -= NotifyMoveFinished;
            session.PlayerDefeated -= NotifyPlayerDefeated;
            if (ReferenceEquals(session.Turns, this)) session.Turns = null;
        }
        if (Instance == this) Instance = null;
    }

    private void ShowVictory()
    {
        if (gameComplete) return;
        gameComplete = true;
        playerWon = true;
        gameOverReason = "You reached the exit.";
        actionMode = ActionMode.None;
        status = "VICTORY: the exit was reached.";
        session.ClearHighlights();

        //เพื่อบันทึกว่าผ่านด่านแล้ว
        ProjectD.Menus.GameSceneManager.CompleteActiveLevel();

        // แจ้งให้LevelCompleteUI แสดงหน้าจอชนะ
        GameEnded?.Invoke(true, gameOverReason);
        RaiseStateChanged();
    }

    public void RestartCurrentScene()
    {
        var scene = gameObject.scene;
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(scene.path) && SceneUtility.GetBuildIndexByScenePath(scene.path) < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void PlayTurnShake()
    {
        if (!enableTurnShake)
            return;

        screenShake?.PlayShake();
    }
}

