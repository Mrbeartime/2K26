using System.Collections;
using UnityEngine;

public class TurnSpikeTrap : TurnTrap
{
    [SerializeField, Min(1)] private int intervalTurns = 2;
    [Tooltip("Optional. If empty, use the grid tile beneath this object.")]
    [SerializeField] private Tile targetTile;
    [SerializeField] private Renderer basePlate;
    [Tooltip("Optional imported model containing SM_Spikes_Move_Y and SM_Spike_Base.")]
    [SerializeField] private Transform modelRoot;
    [SerializeField, Min(0f)] private float raiseDuration = 0.2f;
    private Transform spikes;
    private Vector3 raisedPosition;
    private Vector3 loweredPosition;
    private Vector3 raisedScale;
    private int visualVersion;
    private int lastResolvedRound = -1;
    private Mesh spikeMesh;
    private Tile watchedTile;

    public override int IntervalTurns => Mathf.Max(1, intervalTurns);
    public bool IsRaised { get; private set; }

    private void Awake()
    {
        if (modelRoot != null)
        {
            foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
                if (child.name == "SM_Spikes_Move_Y") spikes = child;
            if (spikes != null)
            {
                // Move only the metal spikes; keep the authored model and materials.
                raisedPosition = spikes.localPosition;
                raisedScale = spikes.localScale;
                Renderer spikeRenderer = spikes.GetComponentInChildren<Renderer>();
                Renderer modelBase = null;
                foreach (Renderer renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name == "SM_Spike_Base") modelBase = renderer;
                float travel = spikeRenderer != null ? spikeRenderer.bounds.size.y : 0.6f;
                if (spikeRenderer != null && modelBase != null)
                    travel = Mathf.Max(0f, spikeRenderer.bounds.max.y - modelBase.bounds.max.y) + 0.01f;
                loweredPosition = raisedPosition + spikes.parent.InverseTransformVector(Vector3.down * travel);
                SetRaised(false);
                return;
            }
            Debug.LogWarning("Spike model is missing SM_Spikes_Move_Y; using placeholder visuals.", this);
            modelRoot = null;
        }
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
        raisedScale = spikes.localScale;
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

    public override IEnumerator ResolveTurn(int turn)
    {
        if (!isActiveAndEnabled || turn <= 0 || lastResolvedRound == turn) yield break;
        lastResolvedRound = turn;

        // Retraction must run even on rounds that are not activation rounds.
        // Keep LastTriggeredTurn as the last rise for the countdown UI.
        if (IsRaised && turn > LastTriggeredTurn)
        {
            IsRaised = false;
            int version = ++visualVersion;
            float elapsed = 0f;
            while (elapsed < raiseDuration)
            {
                if (!isActiveAndEnabled || version != visualVersion) yield break;
                elapsed += Time.deltaTime;
                ApplySpikeHeight(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / raiseDuration)));
                yield return null;
            }
            if (isActiveAndEnabled && version == visualVersion) SetRaised(false);
            yield break;
        }

        yield return base.ResolveTurn(turn);
    }

    protected override IEnumerator Activate()
    {
        if (IsRaised) yield break;
        if (targetTile == null && GridManager.Instance == null) yield break;
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

        int version = ++visualVersion;
        float elapsed = 0f;
        while (elapsed < raiseDuration)
        {
            if (!isActiveAndEnabled || version != visualVersion) yield break;
            elapsed += Time.deltaTime;
            ApplySpikeHeight(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / raiseDuration)));
            yield return null;
        }
        if (!isActiveAndEnabled || version != visualVersion) yield break;
        SetRaised(true);
        DamageOccupant(tile.Occupant);
        // Damage lands when the rise finishes; retract at the next round's end.
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
        visualVersion++;
        IsRaised = raised;
        ApplySpikeHeight(raised ? 1f : 0f);
        if (modelRoot == null)
            TurnTrapVisuals.Tint(basePlate, raised ? Color.red : new Color(0.75f, 0.28f, 0.06f));
    }

    private void ApplySpikeHeight(float height)
    {
        if (spikes == null) return;
        if (modelRoot != null)
            spikes.localPosition = Vector3.Lerp(loweredPosition, raisedPosition, height);
        else
            spikes.localScale = new Vector3(raisedScale.x, raisedScale.y * Mathf.Lerp(0.08f, 1f, height), raisedScale.z);
    }

    private void OnDisable()
    {
        SetRaised(IsRaised);
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
