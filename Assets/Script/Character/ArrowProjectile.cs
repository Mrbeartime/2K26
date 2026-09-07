using UnityEngine;
// Advances cell by cell to the map edge, regardless of the distance clicked.
public class ArrowProjectile : MonoBehaviour
{
    private Character owner;
    private Tile current, next;
    private Vector2Int direction;
    private int damage;
    private float speed;
    private bool finished;
    private void Finish()
    {
        if (finished) return;
        finished = true;
        next = null;
        Enemy.AfterPlayerSkill(owner);
        Destroy(gameObject);
    }
    public void Initialize(Character source, Tile origin, Vector2Int heading, int amount, float velocity)
    {
        owner = source; current = origin; direction = heading; damage = amount; speed = velocity;
        Advance();
    }
    private void Advance()
    {
        next = GridManager.Instance.GetTile(current.gridPosition + direction);
        if (next == null) { Finish(); return; }
        if (!next.isWalkable || !RogueDoor.CanEnter(next, null) ||
            GridManager.Instance.IsBlockedByWall(current, next, forArrow: true))
        {
            Debug.Log("ธนูถูกขวางก่อนถึงช่อง " + next.gridPosition +
                " ตรวจ Is Walkable, ประตู และ Collider ใน Wall Layer", next);
            Finish();
        }
    }
    private void Update()
    {
        if (Time.timeScale == 0 || next == null) return;
        Vector3 destination = next.transform.position + Vector3.up * 0.6f;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, destination) > 0.01f) return;
        bool hit = false;
        foreach (Entity entity in FindObjectsByType<Entity>())
        {
            if (entity == owner || entity.CurrentLocation != next.gridPosition) continue;
            entity.TakeArrowDamage(damage);
            hit = true;
        }
        if (hit) { Finish(); return; }
        current = next;
        Advance();
    }
}

