using System.Collections.Generic;
using UnityEngine;

public class RangedEnemy : Enemy
{
    [Header("Arrow")]
    [SerializeField] private GameObject arrowPrefab;
    [Tooltip("Like TurnArrowTrap: aim blue +Z down the firing lane. If empty, use this enemy's transform.")]
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0.1f)] private float arrowSpeed = 12f;
    [SerializeField, Min(1)] private int rangeTiles = 8;
    [Tooltip("Visual model rotation only. For a model pointing along +X, set Y to -90.")]
    [SerializeField] private Vector3 arrowRotationOffset;

    protected override void React()
    {
        if (IsDead || !Aggression || Time.timeScale == 0 || GridManager.Instance == null) return;
        if (arrowPrefab == null)
        {
            Debug.LogWarning("RangedEnemy: ใส่ Arrow Prefab ก่อน", this);
            return;
        }
        Transform muzzle = firePoint != null ? firePoint : transform;
        GameObject arrow = Instantiate(arrowPrefab, muzzle.position,
            muzzle.rotation * Quaternion.Euler(arrowRotationOffset));
        ArrowProjectile projectile = arrow.GetComponent<ArrowProjectile>();
        if (projectile == null) projectile = arrow.AddComponent<ArrowProjectile>();
        projectile.enabled = true;
        projectile.Initialize(this, muzzle.forward, arrowSpeed,
            Mathf.Max(1, rangeTiles) * GridManager.Instance.TileSize);
        arrow.SetActive(true);
        onAttack.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Transform muzzle = firePoint != null ? firePoint : transform;
        float tileSize = GridManager.Instance != null ? GridManager.Instance.TileSize : 1f;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(muzzle.position, muzzle.forward * Mathf.Max(1, rangeTiles) * tileSize);
        Gizmos.DrawWireSphere(muzzle.position, 0.03f);
    }

    public override List<Tile> GetAttackRange()
    {
        List<Tile> tiles = new List<Tile>();

        if (GridManager.Instance == null)
            return tiles;

        Tile origin = GridManager.Instance.GetTile(CurrentLocation);

        if (origin == null)
            return tiles;

        Transform muzzle = firePoint != null ? firePoint : transform;

        Vector3 forward = muzzle.forward;

        // แปลงทิศของ firePoint ให้เป็นทิศบน Grid
        Vector2Int direction;

        if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
        {
            direction = new Vector2Int(
                forward.x > 0 ? 1 : -1,
                0
            );
        }
        else
        {
            direction = new Vector2Int(
                0,
                forward.z > 0 ? 1 : -1
            );
        }

        Tile current = origin;

        for (int i = 0; i < rangeTiles; i++)
        {
            Tile next =
                GridManager.Instance.GetTile(
                    current.gridPosition + direction
                );

            if (next == null)
                break;

            // ลูกธนูเจอกำแพง = Range จบตรงนั้น
            if (GridManager.Instance.IsBlockedByWall(
                current,
                next,
                null,
                true))
            {
                break;
            }

            tiles.Add(next);
            current = next;
        }

        return tiles;
    }
}
