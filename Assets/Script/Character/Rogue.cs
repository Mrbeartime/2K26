using UnityEngine;

public class Rogue : Character
{
    public bool LockpickIsAvailable = true;
    [SerializeField, Min(1)] private int lockpickRange = 1;

    public int LockpickRange => lockpickRange;
    public bool OPDoor(RogueDoor door) => door != null && door.UnlockDoor(this);

    public bool IsDoorInRange(RogueDoor door)
    {
        if (door == null || GridManager.Instance == null) return false;
        Vector2Int doorPosition = GridManager.Instance.WorldToGrid(door.transform.position);
        Vector2Int delta = doorPosition - CurrentLocation;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= lockpickRange;
    }
}

