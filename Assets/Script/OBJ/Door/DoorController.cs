using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private bool startOpen = false; // ⭐ ติ๊กถูกถ้าอยากให้ประตูเปิดค้างไว้ตั้งแต่เริ่มเกม
    [SerializeField] private Vector3 openOffset = new Vector3(0, 3, 0);
    [SerializeField] private float speed = 3f;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private readonly HashSet<string> activeRequests = new();
    private bool cutsceneClosed;
    private bool audioMoving;
    private bool audioDirectionOpen;
    public bool IsFullyClosed => !IsOpen &&
        (transform.localPosition - closedPosition).sqrMagnitude < 0.000001f;

    // ⭐ ถ้าระบบตั้งให้เปิดแต่แรก การมีคนมากดสวิตช์ (activeRequests > 0) จะแปลว่าสั่ง "ปิดประตู"
    public bool IsOpen => !cutsceneClosed && (startOpen ? activeRequests.Count == 0 : activeRequests.Count > 0);

    // Keep the entrance shut after the cinematic, without discarding switch requests.
    public void CloseForCutscene()
    {
        cutsceneClosed = true;
        if (speed <= 0f || !isActiveAndEnabled)
        {
            StopMovementAudio();
            transform.localPosition = closedPosition;
        }
    }

    public void ReleaseCutsceneClose()
    {
        cutsceneClosed = false;
    }
    public void StopMovementAudio() => ProjectD.Audio.GameAudioManager.Instance?.StopDoorAudio(this);
    private void OnDisable() { StopMovementAudio(); audioMoving = false; }
    void Awake()
    {
        // ยึดตำแหน่งที่คุณจัดวางโมเดลไว้ใน Scene เป็น "ตำแหน่งตอนปิดทึบ" เสมอ (จะได้กะระยะวางกำแพงง่ายๆ)
        closedPosition = transform.localPosition;
        openPosition = transform.localPosition + openOffset;

        // ถ้าตั้งให้ Start Open พอเริ่มเกมปุ๊บ ให้ประตูเด้งขึ้นไปอยู่ตำแหน่งเปิด (เลื่อนไปตาม openOffset) ทันที
        if (startOpen)
        {
            transform.localPosition = openPosition;
        }
    }

    void Update()
    {
        bool opening = IsOpen;
        Vector3 target = opening ? openPosition : closedPosition;
        Vector3 previousPosition = transform.localPosition;
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition, target, speed * Time.deltaTime);
        bool moved = transform.localPosition != previousPosition;
        if (moved && (!audioMoving || audioDirectionOpen != opening))
        {
            StopMovementAudio();
            ProjectD.Audio.GameAudioManager.Instance?.PlayDoorAudio(this, transform.position);
        }
        if (!moved || transform.localPosition == target) StopMovementAudio();
        audioMoving = moved && transform.localPosition != target;
        audioDirectionOpen = opening;
    }
    public void SetOpen(string sourceId, bool isActive)
    {
        // isActive คือสัญญาณที่ส่งมาจาก Switch หรือ PressurePlate
        if (isActive)
            activeRequests.Add(sourceId);
        else
            activeRequests.Remove(sourceId);
    }

    // ยกเลิกการบล็อก Tile แบบเหมารวม ปล่อยให้ GridManager คุม
    public static bool CanEnter(Tile tile)
    {
        return true;
    }
}
