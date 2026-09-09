using UnityEngine;

// Physical hits along a straight cardinal shot. Tiles define only the map edge.
public class ArrowProjectile : MonoBehaviour
{
    private Character owner;
    private Vector3 heading;
    private float remainingDistance;
    private float speed;
    private bool finished;
    [SerializeField, Min(0.001f)] private float hitRadius = 0.03f;
    private bool initialized;

    public void Initialize(Character source, Tile origin, Vector2Int direction, float velocity)
    {
        remainingDistance = 0f;
        finished = false;
        // Movement and collision queries are controlled here, not by model physics.
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.useGravity = false;
            body.isKinematic = true;
        }
        foreach (Collider part in GetComponentsInChildren<Collider>(true)) part.enabled = false;
        owner = source;
        heading = new Vector3(direction.x, 0, direction.y);
        speed = Mathf.Max(0.1f, velocity);
        foreach (Tile tile in FindObjectsByType<Tile>())
            remainingDistance = Mathf.Max(remainingDistance,
                Vector3.Dot(tile.transform.position - origin.transform.position, heading));
        remainingDistance += GridManager.Instance.TileSize * 0.5f;
        initialized = true;
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        Enemy.AfterPlayerSkill(owner);
        Destroy(gameObject);
    }

    private bool Hit(Collider collider)
    {
        if (owner != null && collider.transform.IsChildOf(owner.transform)) return false;
        if (collider.transform.IsChildOf(transform)) return false;
        if (GridManager.Instance.IsArrowWall(collider))
        {
            Finish();
            return true;
        }
        Entity entity = collider.GetComponentInParent<Entity>();
        if (entity == null || !entity.isActiveAndEnabled || entity.IsDead) return false;
        entity.ReceiveArrowHit();
        Finish();
        return true;
    }

    private void Update()
    {
        if (!initialized || finished || Time.timeScale == 0) return;
        if (GridManager.Instance == null) { Destroy(gameObject); return; }
        Physics.SyncTransforms();
        // Include an initial overlap, which a sphere cast alone can miss.
        Collider[] overlaps = Physics.OverlapSphere(transform.position, Mathf.Max(0.001f, hitRadius), ~0,
            QueryTriggerInteraction.Collide);
        foreach (Collider collider in overlaps)
            if (GridManager.Instance.IsArrowWall(collider) && Hit(collider)) return;
        foreach (Collider collider in overlaps)
            if (Hit(collider)) return;

        float distance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
        RaycastHit[] contacts = Physics.SphereCastAll(transform.position, Mathf.Max(0.001f, hitRadius),
            heading, distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(contacts, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit contact in contacts)
            if (Hit(contact.collider)) return;
        transform.position += heading * distance;
        remainingDistance -= distance;
        if (remainingDistance <= 0.001f) Finish();
    }
}
