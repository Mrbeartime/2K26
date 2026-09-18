using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [SerializeField] private Vector3 openOffset = new Vector3(0, 3, 0);
    [SerializeField] private float speed = 3f;

    private Vector3 closedPosition;
    private readonly HashSet<string> openRequests = new();

    // ให้สถานะประตูเป็น "เปิด" ทันทีที่มีการรับคำสั่ง ไม่ต้องรอโมเดลเลื่อนจนจบ
    public bool IsOpen => openRequests.Count > 0;

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
        // ยกเลิกการบล็อก Tile แบบเหมารวม เพื่อปล่อยให้ Raycast ใน GridManager เป็นตัวเช็กการชนแทน
        return true;
    }
}