using UnityEngine;
public class Entity : MonoBehaviour
{
    public virtual Vector2Int CurrentLocation => GridManager.Instance.WorldToGrid(transform.position);
    public bool CanKill = true;
    public bool IsDead { get; private set; }
    public virtual void ReceiveArrowHit() => ReceiveHit();
    public virtual void ReceiveHit()
    {
        if (IsDead || !CanKill) return;
        OnDeath();
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

