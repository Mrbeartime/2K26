using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;

    private List<Tile> currentPath;
    private int pathIndex;

    private Tile currentTile;

    private bool isMoving;

    private void Start()
    {
        FindStartingTile();
    }

    private void Update()
    {
        MoveAlongPath();
    }

    private void FindStartingTile()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError(
                "ไม่พบ GridManager!"
            );

            return;
        }

        Vector2Int playerGridPosition =
            GridManager.Instance.WorldToGrid(
                transform.position
            );

        currentTile =
            GridManager.Instance.GetTile(
                playerGridPosition
            );

        if (currentTile == null)
        {
            Debug.LogError(
                name +
                " ไม่ได้อยู่บน Tile!"
            );

            return;
        }

        // ลงทะเบียนว่าตัวละครอยู่บน Tile นี้
        if (!currentTile.SetOccupant(gameObject))
        {
            Debug.LogError(
                name +
                " พยายามยืนบน Tile ที่มีคนอยู่แล้ว!"
            );
        }
    }

    public bool MoveToTile(Tile targetTile)
    {
        if (Time.timeScale == 0 || isMoving)
            return false;
        if (GridManager.Instance == null || Pathfinder.Instance == null)
        {
            Debug.LogWarning("เดินไม่ได้: ต้องมี GridManager และ Pathfinder ในฉาก", this);
            return false;
        }

        if (currentTile == null)
        {
            FindStartingTile();
        }

        if (currentTile == null)
            return false;

        if (targetTile == null)
            return false;

        // =========================
        // ตรวจว่าปลายทางมีคนอยู่ไหม
        // =========================

        if (targetTile.IsOccupied)
        {
            Debug.Log(
                "Tile นี้มี " +
                targetTile.Occupant.name +
                " อยู่แล้ว!"
            );

            return false;
        }

        // =========================
        // หา Path
        // =========================

        currentPath =
            Pathfinder.Instance.FindPath(
                currentTile,
                targetTile,
                GetComponent<Character>()
            );

        if (currentPath == null)
        {
            Debug.Log(
                "ไม่มีเส้นทางไปที่ " +
                targetTile.name
            );

            return false;
        }

        // The path includes the starting tile; only transitions cost movement.
        Character character = GetComponent<Character>();
        if (character == null || currentPath.Count - 1 > character.MovementRange)
        {
            currentPath = null;
            return false;
        }

        pathIndex = 0;

        // Path ตัวแรกคือ Tile ที่เรายืนอยู่
        if (currentPath.Count > 0 &&
            currentPath[0] == currentTile)
        {
            pathIndex = 1;
        }

        // ป้องกันกรณี Path มี Tile ที่ถูกยึดอยู่ระหว่างทาง
        for (int i = pathIndex;
             i < currentPath.Count;
             i++)
        {
            Tile tile = currentPath[i];

            if (tile.IsOccupied)
            {
                Debug.Log(
                    "เส้นทางถูกขวางโดย " +
                    tile.Occupant.name
                );

                currentPath = null;
                return false;
            }
        }

        isMoving = true;
        return true;
    }

    private void MoveAlongPath()
    {
        if (Time.timeScale == 0 || !isMoving)
            return;

        if (currentPath == null)
            return;

        if (pathIndex >= currentPath.Count)
        {
            FinishMove();
            return;
        }

        Tile nextTile =
            currentPath[pathIndex];

        if (nextTile.IsOccupied || !RogueDoor.CanEnter(nextTile, GetComponent<Character>(), true)) { FinishMove(); return; }
        Vector3 targetPosition =
            nextTile.transform.position;

        targetPosition.y =
            transform.position.y;

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

        // ถึง Tile แล้ว
        if (Vector3.Distance(
            transform.position,
            targetPosition
        ) < 0.01f)
        {
            transform.position =
                targetPosition;

            // เอา Player ออกจาก Tile เก่า
            currentTile.ClearOccupant(
                gameObject
            );

            // เปลี่ยน Tile ปัจจุบัน
            currentTile =
                nextTile;

            // ใส่ Player ลง Tile ใหม่
            currentTile.SetOccupant(
                gameObject
            );

            GetComponent<Character>()?.Interact(currentTile);
            pathIndex++;

            // เดินถึงปลายทางแล้ว
            if (pathIndex >= currentPath.Count)
            {
                FinishMove();
            }
        }
    }

    public Tile GetCurrentTile()
    {
        return currentTile;
    }

    public bool IsMoving()
    {
        return isMoving;
    }

    private void FinishMove()
    {
        isMoving = false;
        currentPath = null;
        TurnGameManager.Instance?.NotifyMoveFinished(this);
    }
}
