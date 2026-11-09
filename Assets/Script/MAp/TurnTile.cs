using UnityEngine;

public class TurnTile : MonoBehaviour, IFixedTurnScheduledObject
{
    [Header("Turn Settings")]
    [Tooltip("Round number when the tile starts rising (1 = first round).")]
    [SerializeField, Min(1)] private int appearTurn = 4;
    private int AppearanceRound => Mathf.Max(1, appearTurn);
    public string ScheduledActionName => "Appearance in round " + AppearanceRound;

    // Record the one-time appearance, not every round the tile stays visible.
    public bool IsScheduledOnTurn(int roundNumber, int maximumRounds) =>
        roundNumber == AppearanceRound && roundNumber <= maximumRounds;

    [Header("Rise Settings")]
    [SerializeField] private float hiddenHeight = 2f;
    [SerializeField] private float riseSpeed = 3f;

    [Header("After Rising")]
    [Tooltip("Allow walking on this Tile after it finishes rising.")]
    [SerializeField] private bool walkableAfterRise = true;

    [Tooltip("Switch this object and its collider objects to the Wall layer after rising. When disabled, keep their existing layers.")]
    [SerializeField] private bool blockArrowsAfterRise = false;

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
            TurnGameManager.Instance.RoundNumber >= AppearanceRound)
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

        if (blockArrowsAfterRise)
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0)
            {
                gameObject.layer = wallLayer;
                foreach (Collider col in colliders)
                {
                    if (col != null) col.gameObject.layer = wallLayer;
                }
            }
            else
            {
                Debug.LogWarning("TurnTile cannot block arrows because the Wall layer is missing.", this);
            }
        }

        // Apply the configured walkability after reaching the final position.
        if (tile != null)
        {
            tile.isWalkable = walkableAfterRise;
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
