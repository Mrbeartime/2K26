using UnityEngine;
public class ArcherMan : Character
{
    [Header("Arrow")]
    [Tooltip("Optional arrow prefab. Point the model along the root's local +Z axis.")]
    [SerializeField] private GameObject arrowPrefab;
    [Tooltip("Model rotation relative to the firing direction. For a model pointing along +X, use Y = -90.")]
    [SerializeField] private Vector3 arrowRotationOffset;
    [SerializeField] private float arrowHeight = 0.6f;
    [SerializeField, Min(0.1f)] private float arrowSpeed = 12f;
    public override void Attack(Tile target) => BowShot(target);
    public void BowShot(Tile target)
    {
        if (target == null || Time.timeScale == 0 || GetComponent<PlayerMove>().IsMoving()) return;
        Vector2Int delta = target.gridPosition - CurrentLocation;
        // Only accept a tile in the same row or column.
        if (delta == Vector2Int.zero) return;
        if (delta.x != 0 && delta.y != 0)
        {
            Debug.Log("ยิงได้ 4 ทิศ: ให้ Player2 อยู่แถวหรือคอลัมน์เดียวกับเป้าก่อน", this);
            return;
        }
        Vector2Int direction = new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
        Fire(direction);
    }

    public void BowShotAt(Vector3 targetPosition)
    {
        if (Time.timeScale == 0 || GetComponent<PlayerMove>().IsMoving()) return;
        Vector3 delta = targetPosition - transform.position;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.z) < 0.01f) return;
        Vector2Int direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)
            ? new Vector2Int(System.Math.Sign(delta.x), 0)
            : new Vector2Int(0, System.Math.Sign(delta.z));
        Fire(direction);
    }

    private void Fire(Vector2Int direction)
    {
        Tile origin = GridManager.Instance.GetTile(CurrentLocation);
        if (origin == null) return;
        Vector3 position = origin.transform.position + Vector3.up * arrowHeight;
        Quaternion rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y));
        GameObject arrow;
        if (arrowPrefab != null)
        {
            arrow = Instantiate(arrowPrefab, position, rotation * Quaternion.Euler(arrowRotationOffset));
        }
        else
        {
            // Keep existing scenes playable until a model prefab is assigned.
            arrow = new GameObject("Arrow");
            arrow.transform.SetPositionAndRotation(position, rotation);
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            model.transform.SetParent(arrow.transform, false);
            model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            model.transform.localScale = new Vector3(0.06f, 0.3f, 0.06f);
        }
        ArrowProjectile projectile = arrow.GetComponent<ArrowProjectile>();
        if (projectile == null) projectile = arrow.AddComponent<ArrowProjectile>();
        projectile.enabled = true;
        projectile.Initialize(this, origin, direction, arrowSpeed);
        arrow.SetActive(true);
    }
}

