using UnityEngine;
[RequireComponent(typeof(PlayerMove))]
public class Character : Entity
{
    [Header("Movement Range")]
    [Tooltip("Maximum number of tiles per move command, including detours around walls.")]
    [SerializeField, Min(0)] private int movementRange = 3;
    public int MovementRange => Mathf.Max(0, movementRange);

    public virtual void Interact(Tile tile)
    {
        if (tile == null) return;
        foreach (Chest chest in FindObjectsByType<Chest>())
            if (GridManager.Instance.WorldToGrid(chest.transform.position) == tile.gridPosition) OPChest(chest);
    }
    public void OPChest(Chest chest) => chest.Open(this);
    public void OPSwitch(FloorSwitch item) => item.Activate(this);

    public bool CanReact(Tile tile)
    {
        if (tile == null || GridManager.Instance == null) return false;
        foreach (DoorSwitch item in FindObjectsByType<DoorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.CanActivate)
                return true;
        foreach (FloorSwitch item in FindObjectsByType<FloorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.CanActivate)
                return true;
        return false;
    }

    public bool React(Tile tile)
    {
        if (tile == null || GridManager.Instance == null) return false;
        foreach (DoorSwitch item in FindObjectsByType<DoorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.TryActivate(this))
                return true;
        foreach (FloorSwitch item in FindObjectsByType<FloorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.TryActivate(this))
                return true;
        return false;
    }
    public virtual void Attack(Tile target) { }
}

