using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [SerializeField] private Vector3 openOffset = new Vector3(0, 3, 0);
    [SerializeField] private float speed = 3f;

    private Vector3 closedPosition;
    private readonly HashSet<string> openRequests = new();
    public bool IsOpen => openRequests.Count > 0 &&
        Vector3.Distance(transform.localPosition, closedPosition + openOffset) < 0.01f;
    public Vector3 ClosedWorldPosition => transform.parent != null
        ? transform.parent.TransformPoint(closedPosition) : closedPosition;

    void Awake()
    {
        closedPosition = transform.localPosition;
    }

    void Update()
    {
        Vector3 target = openRequests.Count > 0
            ? closedPosition + openOffset
            : closedPosition;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition, target, speed * Time.deltaTime);
    }

    public void SetOpen(string sourceId, bool shouldOpen)
    {
        if (shouldOpen)
            openRequests.Add(sourceId);
        else
            openRequests.Remove(sourceId);
    }

    public static bool CanEnter(Tile tile)
    {
        foreach (DoorController door in FindObjectsByType<DoorController>())
            if (!door.IsOpen && GridManager.Instance.WorldToGrid(door.ClosedWorldPosition) == tile.gridPosition)
                return false;
        return true;
    }
}
