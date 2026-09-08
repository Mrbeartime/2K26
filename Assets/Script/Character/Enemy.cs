using UnityEngine;
using UnityEngine.Events;

public class Enemy : Entity
{
    [Header("Enemy")]
    public bool Aggression = true;
    [SerializeField, Min(1)] private int attackDamage = 1;
    [SerializeField] private bool patrolEnabled;
    [SerializeField] private Tile[] patrolPoints;
    public UnityEvent onAttack = new UnityEvent();
    public UnityEvent onDeath = new UnityEvent();
    private Tile occupiedTile;
    private int patrolIndex;

    private void Start()
    {
        if (GridManager.Instance == null) return;
        occupiedTile = GridManager.Instance.GetTile(CurrentLocation);
        if (occupiedTile == null || !occupiedTile.SetOccupant(gameObject))
        {
            occupiedTile = null;
            Debug.LogWarning("Enemy ต้องวางกลาง Tile ว่าง", this);
        }
    }

    // Called once after a valid sword swing or after an arrow finishes travelling.
    public static void AfterPlayerSkill(Character player)
    {
        if (player == null || player.IsDead || Time.timeScale == 0) return;
        foreach (Enemy enemy in FindObjectsByType<Enemy>())
        {
            if (enemy != null) enemy.React();
        }
    }

    private void React()
    {
        if (IsDead || !Aggression || GridManager.Instance == null) return;
        Tile from = GridManager.Instance.GetTile(CurrentLocation);
        if (from == null) return;
        bool attacked = false;
        foreach (Character target in FindObjectsByType<Character>())
        {
            if (target == null || target.IsDead) continue;
            Vector2Int delta = target.CurrentLocation - CurrentLocation;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) continue;
            Tile to = GridManager.Instance.GetTile(target.CurrentLocation);
            if (to == null || !RogueDoor.CanEnter(from, null) || !RogueDoor.CanEnter(to, null) ||
                GridManager.Instance.IsBlockedByWall(from, to)) continue;
            target.TakeDamage(attackDamage);
            attacked = true;
        }
        // One attack event for the whole surrounding attack, not per victim.
        if (attacked) onAttack.Invoke();
        else if (patrolEnabled) Patrol();
    }

    // Patrol takes one grid step per player skill; it never attacks a second time.
    public void Patrol()
    {
        if (IsDead || Time.timeScale == 0 || occupiedTile == null ||
            Pathfinder.Instance == null || patrolPoints == null || patrolPoints.Length == 0) return;
        patrolIndex %= patrolPoints.Length;
        Tile destination = patrolPoints[patrolIndex];
        if (destination == null) { patrolIndex++; return; }
        if (destination == occupiedTile)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            destination = patrolPoints[patrolIndex];
        }
        if (destination == null) return;
        var path = Pathfinder.Instance.FindPath(occupiedTile, destination);
        if (path == null || path.Count < 2) return;
        Tile next = path[1];
        if (!next.SetOccupant(gameObject)) return;
        occupiedTile.ClearOccupant(gameObject);
        occupiedTile = next;
        Vector3 position = next.transform.position;
        position.y = transform.position.y;
        transform.position = position;
    }

    public override void OnDeath()
    {
        if (IsDead) return;
        base.OnDeath();
        if (occupiedTile != null) occupiedTile.ClearOccupant(gameObject);
        onDeath.Invoke();
    }

    private void OnDestroy()
    {
        if (occupiedTile != null) occupiedTile.ClearOccupant(gameObject);
    }
}
