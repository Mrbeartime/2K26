using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [SerializeField] private DoorController[] doors;
    // Retain the old serialized reference so existing scenes/prefabs migrate safely.
    [SerializeField, HideInInspector] private DoorController door;
    private Tile tile;
    private bool isPressed;
    private string sourceId;

    private void OnValidate() => MigrateLegacyDoor();

    private void Awake()
    {
        MigrateLegacyDoor();
        sourceId = "PressurePlate:" + GetEntityId();
        tile = GetComponent<Tile>();
        if (tile == null)
            Debug.LogError(name + " ไม่มี Tile component!", this);
    }

    private void MigrateLegacyDoor()
    {
        if (door == null) return;
        if (doors == null) doors = new DoorController[0];
        if (System.Array.IndexOf(doors, door) < 0)
        {
            int index = doors.Length;
            System.Array.Resize(ref doors, index + 1);
            doors[index] = door;
        }
        door = null;
    }

    private void Update()
    {
        if (tile == null) return;
        // Any occupant can hold the plate down, matching the original behavior.
        bool hasOccupant = tile.IsOccupied;
        if (hasOccupant == isPressed) return;
        isPressed = hasOccupant;
        SetDoors(isPressed);
    }

    private void SetDoors(bool active)
    {
        if (doors == null) return;
        foreach (DoorController target in doors)
            if (target != null) target.SetOpen(sourceId, active);
    }

    private void OnDisable()
    {
        // Release only this plate's requests; other switches may still hold doors open.
        if (isPressed) SetDoors(false);
        isPressed = false;
    }
}
