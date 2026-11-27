using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Any active source holds the trap on. Start Raised inverts this, like DoorController.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpikeTrap))]
public class SwitchSpikeTrap : MonoBehaviour
{
    [SerializeField] private bool startRaised;
    private readonly HashSet<Object> activeRequests = new();
    private SpikeTrap trap;
    private bool started;
    private bool requestedRaised;
    public bool IsRaised => trap != null && trap.IsRaised;
    public bool DesiredRaised => startRaised ? activeRequests.Count == 0 : activeRequests.Count > 0;

    private void Awake()
    {
        trap = GetComponent<SpikeTrap>();
        if (GetComponent<TurnSpikeTrap>() != null)
        {
            Debug.LogError("Use either TurnSpikeTrap or SwitchSpikeTrap on a trap, not both.", this);
            enabled = false;
        }
    }

    private IEnumerator Start()
    {
        trap.Initialize();
        // GridManager registers tiles in Start; wait until all Start calls finish.
        yield return null;
        started = true;
        if (isActiveAndEnabled) ApplyState(true);
    }

    public void SetActive(Object source, bool active)
    {
        if (source == null) return;
        if (active) activeRequests.Add(source);
        else activeRequests.Remove(source);
        if (started && isActiveAndEnabled) ApplyState();
    }

    private void ApplyState(bool force = false)
    {
        bool desired = DesiredRaised;
        if (!force && requestedRaised == desired) return;
        requestedRaised = desired;
        trap.SetRaised(desired);
    }

    private void OnEnable()
    {
        if (trap != null) trap.enabled = true;
        if (started) ApplyState(true);
    }

    private void OnDisable()
    {
        if (trap != null) trap.enabled = false;
    }
}
