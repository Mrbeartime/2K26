using System.Collections;
using UnityEngine;

public class TurnArrowTrap : TurnTrap
{
    [SerializeField, Min(1)] private int intervalTurns = 3;
    [Tooltip("Aim the blue (+Z) axis down the firing lane, at character height.")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Renderer launcher;
    [SerializeField, Min(0.1f)] private float arrowSpeed = 8f;
    [SerializeField, Min(1)] private int rangeTiles = 8;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.06f;
    private GameObject activeArrow;

    public override int IntervalTurns => Mathf.Max(1, intervalTurns);

    private void Awake()
    {
        if (launcher == null)
            launcher = TurnTrapVisuals.Box(transform, "Launcher", new Vector3(0, 0, -0.2f),
                new Vector3(0.35f, 0.35f, 0.4f), null, new Color(0.6f, 0.3f, 0.08f))
                .GetComponent<Renderer>();
    }

    protected override IEnumerator Activate()
    {
        if (GridManager.Instance == null) yield break;
        Transform muzzle = firePoint != null ? firePoint : transform;
        Vector3 direction = muzzle.forward.normalized;
        float remaining = Mathf.Max(1, rangeTiles) * GridManager.Instance.TileSize;
        activeArrow = new GameObject("Turn trap arrow");
        activeArrow.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
        TurnTrapVisuals.Box(activeArrow.transform, "Shaft", new Vector3(0, 0, -0.18f),
            new Vector3(0.04f, 0.04f, 0.36f), launcher.sharedMaterial, new Color(0.8f, 0.55f, 0.18f));
        TurnTrapVisuals.Box(activeArrow.transform, "Arrowhead", Vector3.zero,
            new Vector3(0.11f, 0.04f, 0.1f), launcher.sharedMaterial, Color.white);
        TurnTrapVisuals.Tint(launcher, Color.red);
        try
        {
            while (isActiveAndEnabled && activeArrow != null && remaining > 0)
            {
                if (TurnGameManager.Instance != null && TurnGameManager.Instance.IsGameComplete) yield break;
                if (Time.timeScale == 0) { yield return null; continue; }
                Physics.SyncTransforms();
                Vector3 position = activeArrow.transform.position;
                float distance = Mathf.Min(Mathf.Max(0.1f, arrowSpeed) * Time.deltaTime, remaining);
                if (HitAt(position, direction, distance)) yield break;
                activeArrow.transform.position += direction * distance;
                remaining -= distance;
                yield return null;
            }
        }
        finally { ClearArrow(); }
    }

    private bool HitAt(Vector3 position, Vector3 direction, float distance)
    {
        float radius = Mathf.Max(0.01f, hitRadius);
        Collider[] overlaps = Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Collide);
        // A closed wall at the muzzle blocks the shot before damaging a player behind it.
        foreach (Collider part in overlaps)
            if (!part.transform.IsChildOf(transform) && GridManager.Instance.IsArrowWall(part)) return true;
        foreach (Collider part in overlaps)
            if (Hit(part)) return true;

        RaycastHit[] hits = Physics.SphereCastAll(position, radius, direction, distance, ~0,
            QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
            if (Hit(hit.collider)) return true;
        return false;
    }

    private bool Hit(Collider part)
    {
        if (part.transform.IsChildOf(transform)) return false;
        if (GridManager.Instance.IsArrowWall(part)) return true;
        Character player = part.GetComponentInParent<Character>();
        if (player == null || player.IsDead || !player.isActiveAndEnabled) return false;
        player.ReceiveHit();
        return true;
    }

    private void ClearArrow()
    {
        if (activeArrow != null) Destroy(activeArrow);
        activeArrow = null;
        TurnTrapVisuals.Tint(launcher, new Color(0.6f, 0.3f, 0.08f));
    }

    private void OnDisable() => ClearArrow();

    private void OnDrawGizmosSelected()
    {
        Transform muzzle = firePoint != null ? firePoint : transform;
        float size = GridManager.Instance != null ? GridManager.Instance.TileSize : 1f;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(muzzle.position, muzzle.forward * rangeTiles * size);
        Gizmos.DrawWireSphere(muzzle.position, hitRadius);
    }
}
