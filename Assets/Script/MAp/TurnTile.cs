using UnityEngine;

public class TurnTile : MonoBehaviour
{
    [Header("Turn Settings")]
    [SerializeField] private int appearTurn = 4;

    [Header("Rise Settings")]
    [SerializeField] private float hiddenHeight = 2f;
    [SerializeField] private float riseSpeed = 3f;

    private Tile tile;
    private Collider[] colliders;

    private Vector3 targetPosition;

    private bool isAppeared = false;
    private bool isRising = false;

    private void Awake()
    {
        // หา Tile ที่อยู่บน GameObject เดียวกัน
        tile = GetComponent<Tile>();

        // หา Collider ทั้งหมดของ Tile
        colliders = GetComponentsInChildren<Collider>(true);

        // จำตำแหน่งจริง
        targetPosition = transform.position;

        // เอา Tile ลงไปใต้พื้น
        transform.position = targetPosition + Vector3.down * hiddenHeight;

        // ปิด Collider
        SetColliders(false);

        // ทำให้ Tile เดินไม่ได้
        if (tile != null)
        {
            tile.isWalkable = false;
        }
    }

    private void Update()
    {
        if (isAppeared)
            return;

        if (TurnGameManager.Instance == null)
            return;

        // ถึง Turn ที่กำหนดแล้ว
        if (!isRising &&
            TurnGameManager.Instance.CurrentTurn >= appearTurn)
        {
            StartRise();
        }

        // กำลังลอยขึ้น
        if (isRising)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                riseSpeed * Time.deltaTime
            );

            // ถึงตำแหน่งแล้ว
            if (transform.position == targetPosition)
            {
                FinishRise();
            }
        }
    }

    private void StartRise()
    {
        isRising = true;

        // เปิด Collider ตอนเริ่มขึ้น
        SetColliders(true);
    }

    private void FinishRise()
    {
        isRising = false;
        isAppeared = true;

        transform.position = targetPosition;

        // เปิดให้เดินได้
        if (tile != null)
        {
            tile.isWalkable = true;
        }
    }

    private void SetColliders(bool enabled)
    {
        foreach (Collider col in colliders)
        {
            col.enabled = enabled;
        }
    }
}