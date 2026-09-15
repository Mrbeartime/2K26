using System.Collections.Generic;
using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [SerializeField] private DoorController door;
    private Tile tile;

    // เก็บสถานะว่าสวิตช์ถูกเหยียบอยู่หรือไม่ จะได้ไม่ส่งคำสั่งซ้ำๆ ทุกเฟรม
    private bool isPressed = false;

    //private readonly HashSet<Collider> playersOnPlate = new();

    //void OnTriggerEnter(Collider other)
    //{
    //    if (other.CompareTag("Player"))
    //    {
    //        playersOnPlate.Add(other);
    //        door.SetOpen(gameObject.name, true);
    //    }
    //}

    //void OnTriggerExit(Collider other)
    //{
    //    if (other.CompareTag("Player"))
    //    {
    //        playersOnPlate.Remove(other);

    //        if (playersOnPlate.Count == 0)
    //            door.SetOpen(gameObject.name, false);
    //    }
    //}

    private void Start()
    {
        // อิงการทำงานจาก Tile Component แบบเดียวกับ Trap
        tile = GetComponent<Tile>();

        if (tile == null)
        {
            Debug.LogError(name + " ไม่มี Tile component!");
        }
    }

    private void Update()
    {
        CheckPlate();
    }

    private void CheckPlate()
    {
        if (tile == null)
            return;

        // เช็กแค่ว่ามีคนหรือของอยู่บน Tile หรือไม่ (ใครเหยียบก็ได้ ไม่จำกัด Tag)
        bool hasOccupant = tile.IsOccupied;

        // ถ้าสถานะการเหยียบเปลี่ยนไปจากเฟรมที่แล้ว
        if (hasOccupant != isPressed)
        {
            isPressed = hasOccupant;

            if (isPressed)
            {
                // มีสิ่งมีชีวิตหรือวัตถุมาเหยียบ -> สั่งเปิดประตู
                door.SetOpen(gameObject.name, true);
            }
            else
            {
                // ไม่มีอะไรเหยียบอยู่เลย -> สั่งปิดประตู
                door.SetOpen(gameObject.name, false);
            }
        }
    }
}