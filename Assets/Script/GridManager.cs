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
        List<Tile> neighbours =
            new List<Tile>();

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

            if (!RogueDoor.CanEnter(neighbour, character)) continue;
            if (!neighbour.isWalkable)
                continue;

            if (neighbour.IsOccupied)
                continue;

            // ⭐ ตรวจว่ามีกำแพงขวางระหว่างสอง Tile หรือไม่
            if (IsBlockedByWall(tile, neighbour, character))
                continue;

            neighbours.Add(neighbour);
        }

        return neighbours;
    }
    public bool IsBlockedByWall(
    Tile from,
    Tile to, Character character = null, bool forArrow = false)
    {
        Vector3 start =
            from.transform.position;

        start.y += 0.5f;

        Vector3 direction =
            (to.transform.position -
             from.transform.position).normalized;

        float distance =
            Vector3.Distance(
                from.transform.position,
                to.transform.position
            );

        bool blocked = false;
        foreach (RaycastHit hit in Physics.RaycastAll(start, direction, distance, wallLayer, QueryTriggerInteraction.Ignore))
        {
            RogueDoor door = hit.collider.GetComponentInParent<RogueDoor>();
            if (door != null && (door.IsOpen || door.CheckCharacter(character))) continue;
            // Entity colliders are arrow targets, not walls. Doors still block.
            if (forArrow && door == null && hit.collider.GetComponentInParent<Entity>() != null) continue;
            blocked = true;
            break;
        }
        Debug.DrawRay(
            start,
            direction * distance,
            blocked ? Color.red : Color.green,
            1f
        );

        return blocked;
    }
}


