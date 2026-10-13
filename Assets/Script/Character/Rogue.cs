using UnityEngine;

public class Rogue : Character
{
    [Header("Skill Limit Per Level")]
    [Tooltip("-1 = unlimited; 0 = disabled. Resets when the level is loaded again.")]
    [SerializeField, Min(-1)] private int skillUsesPerLevel = -1;
    public override int SkillLimit => skillUsesPerLevel;
    public bool LockpickIsAvailable = true;
    [SerializeField, Min(1)] private int lockpickRange = 1;

    public int LockpickRange => lockpickRange;
    public bool OPDoor(RogueDoor door)
    {
        if (!CanUseSkill || Time.timeScale == 0 || GetComponent<PlayerMove>().IsMoving() ||
            door == null || door.IsOpen || !IsDoorInRange(door)) return false;
        if (!door.UnlockDoor(this)) return false;
        RecordSkillUse();
        return true;
    }

    public bool IsDoorInRange(RogueDoor door)
    {
        if (door == null || GridManager.Instance == null) return false;
        Vector2Int doorPosition = GridManager.Instance.WorldToGrid(door.transform.position);
        Vector2Int delta = doorPosition - CurrentLocation;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= lockpickRange;
    }

    public override void ShowSkillRange()
    {
        if (GridManager.Instance == null)
            return;

        Tile origin =
            GridManager.Instance.GetTile(CurrentLocation);

        PlayerController.Instance?.ShowRogueSkillRange(
            origin,
            LockpickRange
        );
    }
}

