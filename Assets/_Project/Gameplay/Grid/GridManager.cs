using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    private static GridManager instance;
    public static GridManager Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<GridManager>();
            return instance;
        }
    }

    [Header("Grid Settings")]
    [SerializeField] private float tileSize = 1f;
    public float TileSize => tileSize;

    [Header("Path Blocking")]
    [SerializeField] private LayerMask wallLayer;

    private Dictionary<Vector2Int, Tile> tiles =
        new Dictionary<Vector2Int, Tile>();

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start()
    {
        RegisterAllTiles();
    }

    private void RegisterAllTiles()
    {
        tiles.Clear();
        Tile[] allTiles =
            FindObjectsByType<Tile>(
                FindObjectsSortMode.None
            );

        foreach (Tile tile in allTiles)
        {
            Vector2Int gridPos =
                WorldToGrid(
                    tile.transform.position
                );

            tile.gridPosition = gridPos;

            if (!tiles.ContainsKey(gridPos))
            {
                tiles.Add(gridPos, tile);
            }
            else
            {
                Debug.LogWarning(
                    "มี Tile ซ้อนกันที่ Grid Position: " +
                    gridPos
                );
            }
        }

        Debug.Log(
            "Registered Tiles: " +
            tiles.Count
        );
    }

    public Vector2Int WorldToGrid(
        Vector3 worldPosition)
    {
        int x =
            Mathf.RoundToInt(
                worldPosition.x / tileSize
            );

        int z =
            Mathf.RoundToInt(
                worldPosition.z / tileSize
            );

        return new Vector2Int(x, z);
    }

    public Tile GetTile(
        Vector2Int position)
    {
        // PlayerMove.Start may run before this component's Start.
        if (tiles.Count == 0) RegisterAllTiles();
        if (tiles.TryGetValue(
            position,
            out Tile tile))
        {
            return tile;
        }

        return null;
    }

    public List<Tile> GetNeighbours(Tile tile, Character character = null)
    {
        List<Tile> neighbours = new List<Tile>();

        Vector2Int[] directions =
        {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighbourPosition =
                tile.gridPosition + direction;

            Tile neighbour =
                GetTile(neighbourPosition);

            if (neighbour == null)
                continue;

            if (!RogueDoor.CanEnter(neighbour, character))
            {
                Debug.Log(
                    "[Grid] " +
                    tile.name +
                    " -> " +
                    neighbour.name +
                    " : BLOCKED BY ROGUE DOOR"
                );

                continue;
            }

            if (!neighbour.isWalkable)
            {
                Debug.Log(
                    "[Grid] " +
                    tile.name +
                    " -> " +
                    neighbour.name +
                    " : NOT WALKABLE"
                );

                continue;
            }

            if (neighbour.IsOccupied)
            {
                Debug.Log(
                    "[Grid] " +
                    tile.name +
                    " -> " +
                    neighbour.name +
                    " : OCCUPIED BY " +
                    neighbour.Occupant.name
                );

                continue;
            }

            if (IsBlockedByWall(tile, neighbour, character))
            {
                Debug.Log(
                    "[Grid] " +
                    tile.name +
                    " -> " +
                    neighbour.name +
                    " : BLOCKED BY WALL"
                );

                continue;
            }

            neighbours.Add(neighbour);
        }

        return neighbours;
    }
    public bool IsArrowWall(Collider collider)
    {
        DoorController targetDoor = collider.GetComponentInParent<DoorController>();
        if (targetDoor != null) return !targetDoor.IsOpen;
        RogueDoor door = collider.GetComponentInParent<RogueDoor>();
        if (door != null) return !door.IsOpen;
        return !collider.isTrigger && collider.GetComponentInParent<Entity>() == null &&
            (wallLayer.value & (1 << collider.gameObject.layer)) != 0;
    }

    public bool IsBlockedByWall(Tile from, Tile to, Character character = null, bool forArrow = false)
    {
        if (from == null || to == null) return false;

        Vector3 start = from.transform.position + (Vector3.up * 0.5f);
        Vector3 end = to.transform.position + (Vector3.up * 0.5f);
        Vector3 direction = (end - start).normalized;

        // หดระยะเลเซอร์ลงนิดนึง ป้องกันการยิงชนศูนย์กลางของช่องถัดไป
        float distance = Vector3.Distance(start, end) - 0.05f;

        // ⭐ แก้ปัญหา One-Way: ยิงเลเซอร์ทั้ง "ขาไป" และ "ขากลับ"
        List<RaycastHit> allHits = new List<RaycastHit>();
        allHits.AddRange(Physics.RaycastAll(start, direction, distance, wallLayer, QueryTriggerInteraction.Ignore));
        allHits.AddRange(Physics.RaycastAll(end, -direction, distance, wallLayer, QueryTriggerInteraction.Ignore));

        bool blocked = false;

        foreach (RaycastHit hit in allHits)
        {
            Collider col = hit.collider;

            // 1. ถ้าเลเซอร์ชนแผ่นพื้น Tile ให้มองข้าม
            if (col.gameObject == from.gameObject || col.gameObject == to.gameObject) continue;

            // 2. ถ้าชนสิ่งมีชีวิต (ผู้เล่น/ศัตรู) ให้ทะลุผ่านไปเลย
            if (col.GetComponentInParent<Entity>() != null) continue;

            // 3. เช็กประตู RogueDoor
            RogueDoor rogueDoor = col.GetComponentInParent<RogueDoor>();
            if (rogueDoor != null)
            {
                if (rogueDoor.IsOpen) continue; // ถ้าเปิดแล้ว เดินผ่านได้
                blocked = true; // ⭐ ถ้าประตูปิด บล็อกทุกคน! (โจรจะเดินทะลุไม่ได้แล้ว ต้องกดสกิลก่อน)
                break;
            }

            // 4. เช็กประตูธรรมดา DoorController
            DoorController normalDoor = col.GetComponentInParent<DoorController>();
            if (normalDoor != null)
            {
                if (normalDoor.IsOpen) continue; // ถ้าเปิดแล้ว เดินผ่านได้
                blocked = true; // ถ้าประตูปิด บล็อกทันที!
                break;
            }

            // 5. ถ้าชนกับกำแพงธรรมดา หรืออะไรก็ตามที่อยู่ใน Layer Wall
            blocked = true;
            break;
        }

        // วาดเส้นให้ดูใน Scene ว่าติดกำแพงหรือไม่ (เขียว = ผ่านได้, แดง = ติดกำแพง)
        Debug.DrawLine(start, end, blocked ? Color.red : Color.green, 2f);

        return blocked;
    }
}


