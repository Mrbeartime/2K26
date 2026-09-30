using UnityEngine;
using UnityEngine.InputSystem;

public class DoorSwitch : MonoBehaviour
{
    // เปลี่ยนจาก door ตัวเดียวเป็น Array (เพิ่ม [])
    [SerializeField] private DoorController[] doors;

    private bool playerNear;
    private bool isOn;

    // เช็กว่ามีประตูในลิสต์อย่างน้อย 1 บานหรือไม่
    public bool CanActivate => doors != null && doors.Length > 0;

    void Update()
    {
        if (playerNear && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
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
        foreach (DoorController door in doors)
        {
            if (door != null)
            {
                door.SetOpen(gameObject.name, isOn);
            }
        }
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