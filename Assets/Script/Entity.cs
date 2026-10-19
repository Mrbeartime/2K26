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

        // ถือว่าตายทันที
        IsDead = true;

        // คืนช่อง Tile
        if (GridManager.Instance != null)
        {
            GridManager.Instance
                .GetTile(CurrentLocation)?
                .ClearOccupant(gameObject);
        }

        // แจ้ง Turn Manager ว่า Player ตายแล้ว
        if (TryGetComponent(out PlayerMove player))
        {
            TurnGameManager.Instance?
                .NotifyPlayerDefeated(player);
        }

        // แยกการลบออกจากระบบตาย
        RemoveAfterDeath();
    }

    protected virtual void RemoveAfterDeath()
    {
        // Entity ทั่วไปยังลบทันทีเหมือนเดิม
        Destroy(gameObject);
    }
}

