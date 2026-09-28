using System.Collections;
using UnityEngine;

public class TurnSpikeTrap : TurnTrap
{
    [SerializeField, Min(1)] private int intervalTurns = 2;
    [Tooltip("Optional. If empty, use the grid tile beneath this object.")]
    [SerializeField] private Tile targetTile;
    [SerializeField] private Renderer basePlate;
    private Transform spikes;
    private Mesh spikeMesh;
    private Tile watchedTile;

    public override int IntervalTurns => Mathf.Max(1, intervalTurns);
    public bool IsRaised { get; private set; }

    private void Awake()
    {
        if (basePlate == null)
            basePlate = TurnTrapVisuals.Box(transform, "Base plate", Vector3.zero,
                new Vector3(0.8f, 0.05f, 0.8f), null, new Color(0.75f, 0.28f, 0.06f))
                .GetComponent<Renderer>();
        spikes = new GameObject("Retracting spikes").transform;
        spikes.SetParent(transform, false);
        spikes.localPosition = Vector3.up * 0.025f;
        spikeMesh = TurnTrapVisuals.SpikeMesh();
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
        {
            var spike = new GameObject("Spike", typeof(MeshFilter), typeof(MeshRenderer));
            spike.transform.SetParent(spikes, false);
            spike.transform.localPosition = new Vector3(x * 0.25f, 0, z * 0.25f);
            spike.GetComponent<MeshFilter>().sharedMesh = spikeMesh;
            spike.GetComponent<Renderer>().sharedMaterial = basePlate.sharedMaterial;
            TurnTrapVisuals.Tint(spike.GetComponent<Renderer>(), new Color(0.85f, 0.88f, 0.92f));
        }
        SetRaised(false);
    }

    private void OnEnable()
    {
        SpikeTrapEvents.LowerRequested += OnLowerRequested;
        if (watchedTile != null)
        {
            watchedTile.OccupantChanged += DamageOccupant;
            DamageOccupant(watchedTile.Occupant);
        }
    }

    protected override IEnumerator Activate()
    {
        // A later turn must not retract spikes that are already raised.
        if (IsRaised) yield break;
        if (GridManager.Instance == null) yield break;
        Tile tile = targetTile != null ? targetTile :
            GridManager.Instance.GetTile(GridManager.Instance.WorldToGrid(transform.position));
        if (tile == null)
        {
            Debug.LogWarning("Spike trap must be placed on a Tile.", this);
            yield break;
        }

        if (watchedTile != tile)
        {
            if (watchedTile != null) watchedTile.OccupantChanged -= DamageOccupant;
            watchedTile = tile;
            watchedTile.OccupantChanged += DamageOccupant;
        }

        SetRaised(true);
        DamageOccupant(tile.Occupant);
        // Finish resolving the turn immediately; the raised hazard persists.
        yield break;
    }

    private void DamageOccupant(GameObject occupant)
    {
        if (!isActiveAndEnabled || !IsRaised || occupant == null) return;
        if (TurnGameManager.Instance != null && TurnGameManager.Instance.IsGameComplete) return;
        if (occupant.TryGetComponent(out Character player)) player.ReceiveHit();
    }

    private void OnLowerRequested(TurnSpikeTrap trap)
    {
        if (trap == this) SetRaised(false);
    }

    private void SetRaised(bool raised)
    {
        IsRaised = raised;
        if (spikes != null) spikes.localScale = new Vector3(1, raised ? 1 : 0.08f, 1);
        TurnTrapVisuals.Tint(basePlate, raised ? Color.red : new Color(0.75f, 0.28f, 0.06f));
    }

    private void OnDisable()
    {
        SpikeTrapEvents.LowerRequested -= OnLowerRequested;
        if (watchedTile != null) watchedTile.OccupantChanged -= DamageOccupant;
    }
    private void OnDestroy() { if (spikeMesh != null) Destroy(spikeMesh); }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 0.35f, 0.1f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(0.8f, 0.2f, 0.8f));
    }
}
