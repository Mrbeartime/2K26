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
}
