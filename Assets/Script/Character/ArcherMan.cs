using UnityEngine;
public class ArcherMan : Character
{
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
        Tile origin = GridManager.Instance.GetTile(CurrentLocation);
        if (origin == null) return;
        GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrow.name = "Arrow";
        arrow.GetComponent<Collider>().enabled = false;
        arrow.transform.localScale = new Vector3(0.06f, 0.3f, 0.06f);
        arrow.transform.position = origin.transform.position + Vector3.up * 0.6f;
        arrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(direction.x, 0, direction.y));
        arrow.AddComponent<ArrowProjectile>().Initialize(this, origin, direction, arrowSpeed);
    }
}

