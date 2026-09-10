using UnityEngine;
public class SwordMan : Character
{
    [Min(1)] public int SlashDam = 1;
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
            if (entity != this && entity.CurrentLocation == target.gridPosition) entity.TakeDamage(SlashDam);
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Cube);
        effect.name = "Sword Slash";
        effect.transform.position = target.transform.position + Vector3.up * 0.6f;
        effect.transform.localScale = new Vector3(0.7f, 0.08f, 0.7f);
        effect.GetComponent<Collider>().enabled = false;
        Destroy(effect, 0.15f);
        Enemy.AfterPlayerSkill(this);
    }
}

