using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [SerializeField] private Vector3 openOffset = new Vector3(0, 3, 0);
    [SerializeField] private float speed = 3f;

    private Vector3 closedPosition;
    private readonly HashSet<string> openRequests = new();

    void Start()
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
}