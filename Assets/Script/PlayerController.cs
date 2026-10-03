using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    private PlayerMove selectedPlayer;

    private Enemy selectedEnemy;
    private Camera mainCamera;
    // เก็บรายการช่องที่กำลังแสดงสีอยู่
    private List<Tile> currentHighlightedTiles = new List<Tile>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Time.timeScale == 0) return;
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (TurnGameManager.Instance != null)
        {
            HandleTurnInput(TurnGameManager.Instance);
            return;
        }

        HandleClick();
        HandleAttack();
    }

    private void HandleTurnInput(TurnGameManager turnManager)
    {
        if (Mouse.current == null || mainCamera == null ||
            !Mouse.current.leftButton.wasPressedThisFrame) return;

        if (turnManager.IsPointerOverTurnUI(Mouse.current.position.ReadValue())) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // =========================
            // คลิก Enemy
            // =========================
            Enemy clickedEnemy =
                hit.collider.GetComponentInParent<Enemy>();

            if (clickedEnemy != null)
            {
                // ถ้ากำลังใช้ Skill
                // Enemy ต้องเป็นเป้าหมาย Skill ตามระบบเดิม
                if (turnManager.IsSkillTargeting)
                {
                    Tile enemyTile =
                        GridManager.Instance.GetTile(
                            clickedEnemy.CurrentLocation
                        );

                    if (enemyTile != null)
                        turnManager.UseSkillOn(enemyTile);

                    return;
                }

                // ถ้าไม่ได้ใช้ Skill
                // แค่เปิด/ปิด Enemy Range
                SelectEnemy(clickedEnemy);
                return;
            }

            // =========================
            // คลิก Player เดิม
            // =========================
            PlayerMove clickedPlayer = hit.collider.GetComponentInParent<PlayerMove>();
            if (clickedPlayer != null)
            {
                if (turnManager.IsSkillTargeting)
                {
                    turnManager.UseSkillOn(clickedPlayer.GetCurrentTile());
                    return;
                }
                turnManager.TrySelectPlayer(clickedPlayer);
                return;
            }

            RogueDoor door = hit.collider.GetComponentInParent<RogueDoor>();
            if (door != null && turnManager.IsSkillTargeting)
            {
                turnManager.UseSkillOn(door);
                return;
            }

            Tile tile = hit.collider.GetComponentInParent<Tile>();
            if (tile == null) continue;

            if (turnManager.IsMoveTargeting && selectedPlayer == turnManager.CurrentPlayer && tile.isWalkable)
            {
                turnManager.MoveToTarget(tile);
                return;
            }

            if (turnManager.IsSkillTargeting)
            {
                turnManager.UseSkillOn(tile);
                return;
            }
        }
    }

    private void HandleClick()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // คลิก Player
            PlayerMove clickedPlayer = hit.collider.GetComponentInParent<PlayerMove>();
            if (clickedPlayer != null)
            {
                SelectPlayer(clickedPlayer);
                return;
            }

            // คลิก Tile
            Tile clickedTile = hit.collider.GetComponent<Tile>();
            if (clickedTile != null)
            {
                // ถ้าเป็นช่องที่เดินไม่ได้ ให้เมินไปเลย
                if (!clickedTile.isWalkable) return;

                if (selectedPlayer != null)
                {
                    // สั่งเดิน และปิดสีไฮไลต์ทั้งหมด
                    selectedPlayer.MoveToTile(clickedTile);
                    if (selectedPlayer.IsMoving()) ClearHighlights();
                }
                return;
            }
        }
    }

    public void SelectPlayer(PlayerMove player)
    {
        ClearHighlights();

        selectedPlayer = player;

        if (player == null)
            return;

        Debug.Log("Selected: " + player.name);
    }

    public void ShowMoveRange(PlayerMove player)
    {
        ClearHighlights();

        if (player == null ||
            GridManager.Instance == null ||
            Pathfinder.Instance == null)
            return;

        Tile playerTile = player.GetCurrentTile();

        if (playerTile == null)
            return;

        currentHighlightedTiles =
            Pathfinder.Instance.GetAllReachableTiles(
                playerTile,
                player.GetComponent<Character>()
            );

        foreach (Tile tile in currentHighlightedTiles)
        {
            tile.ShowHighlight(Tile.HighlightType.Move);
        }
    }

    private void HandleAttack()
    {
        if (Mouse.current == null || mainCamera == null || selectedPlayer == null ||
            !Mouse.current.rightButton.wasPressedThisFrame) return;
        if (GridManager.Instance == null) return;
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Entity targetEntity = hit.collider.GetComponentInParent<Entity>();
            if (targetEntity != null && targetEntity.gameObject != selectedPlayer.gameObject)
            {
                if (targetEntity is ArrowTutorial && selectedPlayer.TryGetComponent(out ArcherMan archer))
                {
                    archer.BowShotAt(hit.point);
                    return;
                }
                Tile targetTile = GridManager.Instance.GetTile(targetEntity.CurrentLocation);
                selectedPlayer.GetComponent<Character>()?.Attack(targetTile);
                return;
            }
            Tile tile = hit.collider.GetComponentInParent<Tile>();
            if (tile == null) continue;
            selectedPlayer.GetComponent<Character>()?.Attack(tile);
            return;
        }
    }

    public void ClearHighlights()
    {
        foreach (Tile tile in currentHighlightedTiles)
        {
            if (tile != null)
                tile.HideHighlight();
        }
        currentHighlightedTiles.Clear();
    }

    public void ShowRogueSkillRange(Tile origin, int range)
    {
        ClearHighlights();
        if (origin == null) return;
        foreach (Tile tile in FindObjectsByType<Tile>())
        {
            Vector2Int delta = tile.gridPosition - origin.gridPosition;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) > range) continue;
            currentHighlightedTiles.Add(tile);
            tile.ShowHighlight(Tile.HighlightType.Skill);
        }
    }

    public void ShowSwordSkillRange(Tile origin)
    {
        ClearHighlights();

        if (origin == null || GridManager.Instance == null)
            return;

        Vector2Int[] directions =
        {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

        foreach (Vector2Int direction in directions)
        {
            Tile tile =
                GridManager.Instance.GetTile(
                    origin.gridPosition + direction
                );

            if (tile == null)
                continue;

            // มีกำแพง/ประตูขวาง
            if (GridManager.Instance.IsBlockedByWall(
                origin,
                tile))
                continue;

            currentHighlightedTiles.Add(tile);

            tile.ShowHighlight(
                Tile.HighlightType.Skill
            );
        }
    }

    public void ShowArcherSkillRange(Tile origin)
    {
        ClearHighlights();

        if (origin == null || GridManager.Instance == null)
            return;

        Vector2Int[] directions =
        {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

        foreach (Vector2Int direction in directions)
        {
            Tile currentTile = origin;

            while (true)
            {
                Tile nextTile =
                    GridManager.Instance.GetTile(
                        currentTile.gridPosition + direction
                    );

                // สุดขอบ Map
                if (nextTile == null)
                    break;

                // เจอกำแพงหรือประตู → หยุดเส้นนี้
                if (GridManager.Instance.IsBlockedByWall(
                    currentTile,
                    nextTile,
                    null,
                    true))
                {
                    break;
                }

                currentHighlightedTiles.Add(nextTile);

                nextTile.ShowHighlight(
                    Tile.HighlightType.Skill
                );

                currentTile = nextTile;
            }
        }
    }
    public void SelectEnemy(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return;

        // =========================
        // กดตัวเดิมซ้ำ = ปิด
        // =========================
        if (selectedEnemy == enemy)
        {
            ClearHighlights();
            selectedEnemy = null;

            Debug.Log("Enemy Range OFF");
            return;
        }

        // =========================
        // เลือก Enemy ตัวใหม่
        // =========================
        ClearHighlights();

        selectedEnemy = enemy;

        List<Tile> attackRange = enemy.GetAttackRange();

        foreach (Tile tile in attackRange)
        {
            currentHighlightedTiles.Add(tile);
            tile.ShowHighlight(Tile.HighlightType.Enemy);
        }

        Debug.Log(
            $"Enemy Selected: {enemy.name} | Range: {attackRange.Count} tiles"
        );
    }
}
