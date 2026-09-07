using System.Collections.Generic;
using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    private static Pathfinder instance;
    public static Pathfinder Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<Pathfinder>();
            return instance;
        }
    }

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public List<Tile> FindPath(
        Tile startTile,
        Tile targetTile, Character character = null)
    {
        if (startTile == null ||
            targetTile == null)
        {
            return null;
        }

        List<Tile> openSet =
            new List<Tile>();

        HashSet<Tile> closedSet =
            new HashSet<Tile>();

        Dictionary<Tile, Tile> cameFrom =
            new Dictionary<Tile, Tile>();

        Dictionary<Tile, int> gScore =
            new Dictionary<Tile, int>();

        Dictionary<Tile, int> fScore =
            new Dictionary<Tile, int>();

        openSet.Add(startTile);

        gScore[startTile] = 0;

        fScore[startTile] =
            GetDistance(
                startTile,
                targetTile
            );

        while (openSet.Count > 0)
        {
            Tile current =
                GetLowestScoreTile(
                    openSet,
                    fScore
                );

            if (current == targetTile)
            {
                return ReconstructPath(
                    cameFrom,
                    current
                );
            }

            openSet.Remove(current);

            closedSet.Add(current);

            List<Tile> neighbours =
                GridManager.Instance
                    .GetNeighbours(current, character);

            foreach (Tile neighbour in neighbours)
            {
                if (closedSet.Contains(neighbour))
                    continue;

                int tentativeGScore =
                    gScore[current] + 1;

                if (!gScore.ContainsKey(neighbour) ||
                    tentativeGScore < gScore[neighbour])
                {
                    cameFrom[neighbour] = current;

                    gScore[neighbour] =
                        tentativeGScore;

                    fScore[neighbour] =
                        tentativeGScore +
                        GetDistance(
                            neighbour,
                            targetTile
                        );

                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                    }
                }
            }
        }

        // หาเส้นทางไม่เจอ
        return null;
    }

    private Tile GetLowestScoreTile(
        List<Tile> tiles,
        Dictionary<Tile, int> fScore)
    {
        Tile lowestTile = tiles[0];

        int lowestScore =
            fScore.ContainsKey(lowestTile)
                ? fScore[lowestTile]
                : int.MaxValue;

        foreach (Tile tile in tiles)
        {
            int score =
                fScore.ContainsKey(tile)
                    ? fScore[tile]
                    : int.MaxValue;

            if (score < lowestScore)
            {
                lowestScore = score;
                lowestTile = tile;
            }
        }

        return lowestTile;
    }

    private List<Tile> ReconstructPath(
        Dictionary<Tile, Tile> cameFrom,
        Tile current)
    {
        List<Tile> path =
            new List<Tile>();

        path.Add(current);

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();

        return path;
    }

    private int GetDistance(
        Tile a,
        Tile b)
    {
        int distanceX =
            Mathf.Abs(
                a.gridPosition.x -
                b.gridPosition.x
            );

        int distanceZ =
            Mathf.Abs(
                a.gridPosition.y -
                b.gridPosition.y
            );

        return distanceX + distanceZ;
    }

    //hightlight
    public List<Tile> GetAllReachableTiles(Tile startTile, Character character = null)
    {
        List<Tile> reachable = new List<Tile>();
        if (startTile == null) return reachable;

        Queue<Tile> queue = new Queue<Tile>();
        HashSet<Tile> visited = new HashSet<Tile>();

        queue.Enqueue(startTile);
        visited.Add(startTile);
        reachable.Add(startTile); // นับช่องที่ตัวเองยืนอยู่ด้วย

        while (queue.Count > 0)
        {
            Tile current = queue.Dequeue();

            // ใช้ GetNeighbours จาก GridManager ของคุณ (ซึ่งเช็กกำแพงให้อยู่แล้ว!)
            List<Tile> neighbours = GridManager.Instance.GetNeighbours(current, character);

            foreach (Tile neighbour in neighbours)
            {
                if (!visited.Contains(neighbour))
                {
                    visited.Add(neighbour);
                    queue.Enqueue(neighbour);
                    reachable.Add(neighbour);
                }
            }
        }

        return reachable;
    }
}

