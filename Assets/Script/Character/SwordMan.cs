using UnityEngine;
public class SwordMan : Character
{
    private void OnEnable()
    {
        if (GetComponent<SwordSlashTrail>() == null) gameObject.AddComponent<SwordSlashTrail>();
    }
    public override void Attack(Tile target) => Slash(target);
    public void Slash(Tile target)
    {
        if (target == null || Time.timeScale == 0 || GetComponent<PlayerMove>().IsMoving()) return;
        Vector2Int delta = target.gridPosition - CurrentLocation;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) return;
        Tile origin = GridManager.Instance.GetTile(CurrentLocation);
        if (origin == null || !RogueDoor.CanEnter(target, null) ||
            GridManager.Instance.IsBlockedByWall(origin, target)) return;
        foreach (Entity entity in FindObjectsByType<Entity>())
            if (entity != this && entity.CurrentLocation == target.gridPosition) entity.ReceiveHit();
    }

    public override void ShowSkillRange()
    {
        if (GridManager.Instance == null)
            return;

        Tile origin =
            GridManager.Instance.GetTile(CurrentLocation);

        PlayerController.Instance?.ShowSwordSkillRange(origin);
    }
}

