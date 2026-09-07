using UnityEngine;
public class Entity : MonoBehaviour
{
    public virtual Vector2Int CurrentLocation => GridManager.Instance.WorldToGrid(transform.position);
    public bool CanKill = true;
    public bool IsDead { get; private set; }
    [SerializeField, Min(1)] private int health = 1;
    public virtual void TakeArrowDamage(int damage) => TakeDamage(damage);
    public virtual void TakeDamage(int damage)
    {
        if (IsDead || !CanKill || damage <= 0) return;
        health -= damage;
        if (health <= 0) OnDeath();
    }
    public virtual void OnDeath()
    {
        if (IsDead) return;
        IsDead = true;
        if (GridManager.Instance != null)
            GridManager.Instance.GetTile(CurrentLocation)?.ClearOccupant(gameObject);
        Destroy(gameObject);
    }
}

