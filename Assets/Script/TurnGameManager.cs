using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the five-round player phase in TESTMAP1Version2.
/// Each player can be selected in any order, once per round.
/// </summary>
[DefaultExecutionOrder(-100)]
public class TurnGameManager : MonoBehaviour
{
    public static TurnGameManager Instance { get; private set; }

    private enum ActionMode { None, MoveTargeting, SkillTargeting }

    [Header("Turn Settings")]
    [SerializeField, Min(1)] private int maximumTurns = 5;
    [SerializeField, Min(0f)] private float enemyPhaseDelay = 0.35f;

    private readonly List<PlayerMove> players = new();
    private readonly HashSet<PlayerMove> finishedPlayers = new();
    private readonly HashSet<PlayerMove> movedPlayers = new();
    private PlayerMove currentPlayer;
    private int currentTurn = 1;
    private bool currentPlayerSelected;
    private bool hasMoved;
    private bool waitingForMovement;
    private bool enemyPhase;
    private bool gameComplete;
    private string gameOverReason;
    private ActionMode actionMode;
    private string status;

    public PlayerMove CurrentPlayer => currentPlayer;
    public bool IsMoveTargeting => actionMode == ActionMode.MoveTargeting;
    public bool IsSkillTargeting => actionMode == ActionMode.SkillTargeting;
    private bool CanCurrentPlayerReact => !gameComplete && !enemyPhase && CurrentPlayer != null &&
        currentPlayerSelected && !waitingForMovement &&
        CurrentPlayer.TryGetComponent(out Character character) && character.CanReact(CurrentPlayer.GetCurrentTile());

    public bool IsPointerOverTurnUI(Vector2 screenPosition)
    {
        Vector2 guiPosition = new(screenPosition.x, Screen.height - screenPosition.y);

        if (gameComplete)
        {
            float x = (Screen.width - 360f) * 0.5f;
            float y = (Screen.height - 180f) * 0.5f;
            return new Rect(x + 105f, y + 120f, 150f, 36f).Contains(guiPosition);
        }

        // The panel background and labels are intentionally click-through so they
        // never block map tiles. Only actionable buttons capture a map click.
        if (IsMoveTargeting || IsSkillTargeting)
            return new Rect(30f, 126f, 180f, 28f).Contains(guiPosition);

        bool canAct = !enemyPhase && CurrentPlayer != null && currentPlayerSelected &&
            !waitingForMovement && actionMode == ActionMode.None;
        if (canAct && !hasMoved && new Rect(30f, 126f, 120f, 28f).Contains(guiPosition)) return true;
        if (canAct && new Rect(160f, 126f, 120f, 28f).Contains(guiPosition)) return true;
        if (CanCurrentPlayerReact && new Rect(290f, 126f, 120f, 28f).Contains(guiPosition)) return true;
        return canAct && new Rect(420f, 126f, 120f, 28f).Contains(guiPosition);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        players.AddRange(FindObjectsByType<PlayerMove>(FindObjectsSortMode.None));
        players.Sort((left, right) => GetPlayerNumber(left).CompareTo(GetPlayerNumber(right)));

        if (players.Count != 3)
            Debug.LogWarning("TurnGameManager ต้องการ PlayerMove 3 ตัว: Player1, Player2, Player3", this);

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

    public bool TrySelectPlayer(PlayerMove player)
    {
        if (gameComplete || enemyPhase || waitingForMovement || actionMode != ActionMode.None ||
            player == null || finishedPlayers.Contains(player)) return false;
        currentPlayer = player;
        PlayerController.Instance?.SelectPlayer(player);
        currentPlayerSelected = true;
        hasMoved = movedPlayers.Contains(player);
        status = player.name + " selected: choose Move, Skill, or React.";
        return true;
    }

    public void ChooseMove()
    {
        if (gameComplete || enemyPhase || CurrentPlayer == null || !currentPlayerSelected || hasMoved || waitingForMovement) return;
        actionMode = ActionMode.MoveTargeting;
        PlayerController.Instance?.SelectPlayer(CurrentPlayer);
        status = "Move: click a highlighted tile.";
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
        PlayerController.Instance?.ClearHighlights();
        status = CurrentPlayer.name + " is moving...";
    }

    public void CancelMoveTargeting()
    {
        if (!IsMoveTargeting || CurrentPlayer == null) return;
        actionMode = ActionMode.None;
        PlayerController.Instance?.SelectPlayer(CurrentPlayer);
        status = "Move cancelled. Choose an action.";
    }

    public void ChooseSkill()
    {
        if (gameComplete || enemyPhase || CurrentPlayer == null || !currentPlayerSelected || waitingForMovement) return;
        actionMode = ActionMode.SkillTargeting;
        PlayerController.Instance?.ClearHighlights();
        if (CurrentPlayer.GetComponent<Rogue>() is Rogue rogue)
            PlayerController.Instance?.ShowRogueSkillRange(CurrentPlayer.GetCurrentTile(), rogue.LockpickRange);
        status = "Skill: click a target tile or enemy. This ends " + CurrentPlayer.name + "'s action.";
    }

    public void ChooseReact()
    {
        if (!CanCurrentPlayerReact) return;
        Character character = CurrentPlayer.GetComponent<Character>();
        if (!character.React(CurrentPlayer.GetCurrentTile())) return;
        EndCurrentPlayerAction("React: door switch activated");
    }

    public void ChooseSkip()
    {
        if (gameComplete || enemyPhase || CurrentPlayer == null || !currentPlayerSelected ||
            waitingForMovement || actionMode != ActionMode.None) return;
        EndCurrentPlayerAction("Skip");
    }

    public void CancelSkill()
    {
        if (!IsSkillTargeting) return;
        actionMode = ActionMode.None;
        PlayerController.Instance?.ClearHighlights();
        status = "Skill cancelled. Choose an action.";
    }

    public void NotifyMoveStarted(PlayerMove player)
    {
        if (player != CurrentPlayer) return;
        movedPlayers.Add(player);
        hasMoved = true;
        waitingForMovement = true;
        actionMode = ActionMode.None;
        status = player.name + " is moving...";
    }

    public void NotifyMoveFinished(PlayerMove player)
    {
        if (player != CurrentPlayer || !waitingForMovement) return;
        waitingForMovement = false;
        status = "Move complete. Choose Skill, React, or Skip.";
    }

    public void UseSkillOn(Tile target)
    {
        if (!IsSkillTargeting || CurrentPlayer == null || target == null) return;
        Character character = CurrentPlayer.GetComponent<Character>();
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

        character?.Attack(target);
        EndCurrentPlayerAction("Skill");
    }

    public void UseSkillOn(RogueDoor door)
    {
        if (!IsSkillTargeting || CurrentPlayer == null || door == null) return;
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
        if (!rogue.OPDoor(door))
        {
            status = "Rogue cannot unlock this door.";
            return;
        }
        EndCurrentPlayerAction("Rogue Skill");
    }

    private void EndCurrentPlayerAction(string actionName)
    {
        actionMode = ActionMode.None;
        waitingForMovement = false;
        PlayerController.Instance?.ClearHighlights();
        finishedPlayers.Add(CurrentPlayer);
        currentPlayer = null;
        currentPlayerSelected = false;

        if (finishedPlayers.Count >= players.Count)
        {
            StartEnemyPhase();
            return;
        }

        status = actionName + " complete. Select another Player.";
    }

    private void StartEnemyPhase()
    {
        enemyPhase = true;
        status = "Enemy Phase...";
        StartCoroutine(RunEnemyPhase());
    }

    private System.Collections.IEnumerator RunEnemyPhase()
    {
        yield return new WaitForSeconds(enemyPhaseDelay);
        Enemy.RunEnemyPhase();
        yield return null;
        enemyPhase = false;
        currentTurn++;
        if (currentTurn > maximumTurns)
        {
            ShowGameOver("Turn limit reached.");
            yield break;
        }
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
        status = "Turn " + currentTurn + "/" + maximumTurns + ": select any Player.";
    }

    private void ShowGameOver(string reason)
    {
        gameComplete = true;
        gameOverReason = reason;
        status = "GAME OVER: " + reason;
        PlayerController.Instance?.ClearHighlights();
    }

    private void RestartCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        const float width = 570f;
        GUI.Box(new Rect(16, 16, width, 180), "TURN MANAGER");
        GUI.Label(new Rect(30, 48, 400, 24), "Turn " + currentTurn + " / " + maximumTurns);
        GUI.Label(new Rect(30, 72, 400, 24), enemyPhase ? "Active: Enemy" : "Active: " + (CurrentPlayer == null ? "Choose a Player" : CurrentPlayer.name));
        GUI.Label(new Rect(30, 96, 410, 24), status ?? "Preparing...");

        if (IsMoveTargeting)
        {
            if (GUI.Button(new Rect(30, 126, 180, 28), "Cancel Move")) CancelMoveTargeting();
        }
        else if (IsSkillTargeting)
        {
            if (GUI.Button(new Rect(30, 126, 180, 28), "Cancel Skill")) CancelSkill();
        }
        else
        {
            GUI.enabled = !gameComplete && !enemyPhase && CurrentPlayer != null && currentPlayerSelected && !hasMoved && !waitingForMovement && actionMode == ActionMode.None;
            if (GUI.Button(new Rect(30, 126, 120, 28), "Move")) ChooseMove();

            GUI.enabled = !gameComplete && !enemyPhase && CurrentPlayer != null && currentPlayerSelected && !waitingForMovement && actionMode == ActionMode.None;
            if (GUI.Button(new Rect(160, 126, 120, 28), "Skill")) ChooseSkill();
            if (CanCurrentPlayerReact && GUI.Button(new Rect(290, 126, 120, 28), "React")) ChooseReact();
            if (GUI.Button(new Rect(420, 126, 120, 28), "Skip")) ChooseSkip();
        }
        GUI.enabled = true;

        if (!gameComplete) return;
        float x = (Screen.width - 360f) * 0.5f;
        float y = (Screen.height - 180f) * 0.5f;
        GUI.Box(new Rect(x, y, 360f, 180f), GUIContent.none);
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(x, y + 22f, 360f, 50f), "GAME OVER", titleStyle);
        GUI.Label(new Rect(x + 20f, y + 75f, 320f, 28f), gameOverReason, new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter
        });
        if (GUI.Button(new Rect(x + 105f, y + 120f, 150f, 36f), "Restart")) RestartCurrentScene();
    }
}
