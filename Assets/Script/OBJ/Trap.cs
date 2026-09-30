using UnityEngine;

public class Trap : MonoBehaviour
{
    private Tile tile;

    private void Start()
    {
        tile = GetComponent<Tile>();

        if (tile == null)
        {
            Debug.LogError(
                name + " ไม่มี Tile component!"
            );
        }
    }

    private void Update()
    {
        CheckTrap();
    }

    private void CheckTrap()
    {
        if (tile == null)
            return;

        if (!tile.IsOccupied)
            return;

        GameObject occupant = tile.Occupant;

        if (occupant == null)
            return;

        if (occupant.CompareTag("Player"))
        {
            ActivateTrap(occupant);
        }
    }

    private void ActivateTrap(GameObject target)
    {
        Debug.Log(
            "โดนกับดัก! " +
            target.name +
            " Game Over"
        );

        // Use the normal death route so the Turn Manager can release a player
        // who dies while moving and allow another player to be selected.
        if (target.TryGetComponent(out Character character))
            character.ReceiveHit();
        else
        {
            tile.ClearOccupant(target);
            Destroy(target);
        }
    }
}
