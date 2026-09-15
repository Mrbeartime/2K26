using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    private PlayerMove selectedPlayer;
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
        ClearHighlights(); // ล้างสีเก่าทิ้งก่อน
        selectedPlayer = null;
        if (player == null) return;
        if (GridManager.Instance == null || Pathfinder.Instance == null)
        {
            string missing = GridManager.Instance == null ? "GridManager" : "Pathfinder";
            Debug.LogWarning("เลือกตัวละครไม่ได้: เพิ่ม " + missing +
                " component บน GameObject ที่เปิดใช้งานในฉากก่อน", this);
            return;
        }
        selectedPlayer = player;
        if (player.IsMoving()) return;
        Debug.Log("Selected: " + player.name);

        // เปลี่ยนมาใช้ GridManager แปลงพิกัดตัวละครหาแผ่นพื้นแทนการยิง Raycast
        Vector2Int gridPos = GridManager.Instance.WorldToGrid(player.transform.position);
        Tile playerTile = GridManager.Instance.GetTile(gridPos);

        if (playerTile != null)
        {
            // ดึงเฉพาะช่องที่เดินเชื่อมถึงกันได้จริงๆ (ไม่ทะลุกำแพง)
            currentHighlightedTiles = Pathfinder.Instance.GetAllReachableTiles(playerTile, player.GetComponent<Character>());

            foreach (Tile tile in currentHighlightedTiles)
            {
                tile.ToggleHighlight(true);
            }
        }
        else
        {
            Debug.LogWarning("หาจุดที่ Player ยืนอยู่ไม่เจอ! เช็กพิกัด: " + gridPos);
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
            if (tile != null) tile.ToggleHighlight(false);
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
            tile.ToggleHighlight(true);
        }
    }
}
