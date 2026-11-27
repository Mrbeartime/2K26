using UnityEngine;

public class GenBlock : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private float tileSize = 1f;

    [Header("Tile")]
    [SerializeField] private GameObject tilePrefab;

    private Tile[,] tiles;

    private void Start()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        tiles = new Tile[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 position = new Vector3(
                    x * tileSize,
                    0f,
                    z * tileSize
                );

                GameObject tileObject = Instantiate(
                    tilePrefab,
                    position,
                    Quaternion.identity,
                    transform
                );

                Tile tile = tileObject.GetComponent<Tile>();

                tile.gridPosition = new Vector2Int(x, z);

                tiles[x, z] = tile;
            }
        }
    }

    public Tile GetTile(int x, int z)
    {
        if (x < 0 || x >= width ||
            z < 0 || z >= height)
        {
            return null;
        }

        return tiles[x, z];
    }
}