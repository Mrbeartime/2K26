using UnityEngine;
using UnityEngine.InputSystem;

public class DoorSwitch : MonoBehaviour
{
    // เปลี่ยนจาก door ตัวเดียวเป็น Array (เพิ่ม [])
    [SerializeField] private DoorController[] doors;
    [SerializeField] private SwitchSpikeTrap[] spikeTraps;
    [SerializeField] private PropInteractAnimation[] interactionAnimations;

    private bool playerNear;
    private bool isOn;

    // เช็กว่ามีประตูในลิสต์อย่างน้อย 1 บานหรือไม่
    public bool CanActivate =>
        (doors != null && System.Array.Exists(doors, door => door != null)) ||
        (spikeTraps != null && System.Array.Exists(spikeTraps, trap => trap != null));

    void Update()
    {
        if (playerNear && CanActivate && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            isOn = !isOn;
            ToggleAllDoors();
        }
    }

    // เรียกใช้ตอนกด React ใน TurnGameManager
    public bool TryActivate(Character character)
    {
        if (character == null || !CanActivate) return false;

        isOn = !isOn;
        ToggleAllDoors();

        return true;
    }

    // ฟังก์ชันสั่งเปิด/ปิด ประตูทุกบานที่อยู่ในลิสต์
    private void ToggleAllDoors()
    {
        SendSpikeState(isOn);
        if (interactionAnimations != null) foreach (var animation in interactionAnimations)
            if (animation != null) animation.SetActiveState(isOn);
        foreach (var animation in GetComponentsInChildren<PropInteractAnimation>())
            animation.SetActiveState(isOn);
        if (doors != null) foreach (DoorController door in doors)
        {
            if (door != null)
            {
                door.SetOpen(gameObject.name, isOn);
            }
        }
    }

    private void OnEnable()
    {
        SendSpikeState(isOn);
    }

    private void OnDisable()
    {
        SendSpikeState(false);
    }

    private void SendSpikeState(bool active)
    {
        if (spikeTraps != null) foreach (SwitchSpikeTrap trap in spikeTraps)
            if (trap != null) trap.SetActive(this, active);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = false;
    }
}