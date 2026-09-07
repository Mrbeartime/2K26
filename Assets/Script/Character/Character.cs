using UnityEngine;
[RequireComponent(typeof(PlayerMove))]
public class Character : Entity
{
    public virtual void Interact(Tile tile)
    {
        if (tile == null) return;
        foreach (Chest chest in FindObjectsByType<Chest>())
            if (GridManager.Instance.WorldToGrid(chest.transform.position) == tile.gridPosition) OPChest(chest);
        foreach (FloorSwitch item in FindObjectsByType<FloorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition) OPSwitch(item);
    }
    public void OPChest(Chest chest) => chest.Open(this);
    public void OPSwitch(FloorSwitch item) => item.Activate(this);
    public virtual void Attack(Tile target) { }
}

