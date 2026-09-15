using UnityEngine;
using UnityEngine.Events;
public class RogueDoor : MonoBehaviour
{
    [SerializeField] private bool isOpen;
    public bool IsOpen => isOpen;
    public UnityEvent onOpened = new UnityEvent();
    private void Awake()
    {
        if (isOpen) HideDoor();
    }
    private void HideDoor()
    {
        foreach (Collider part in GetComponentsInChildren<Collider>()) part.enabled = false;
        foreach (Renderer part in GetComponentsInChildren<Renderer>()) part.enabled = false;
    }
    public bool CheckCharacter(Character character) => character is Rogue rogue && rogue.LockpickIsAvailable;
    public bool UnlockDoor(Rogue rogue)
    {
        if (isOpen) return true;
        if (rogue == null || !CheckCharacter(rogue)) return false;
        isOpen = true;
        HideDoor();
        onOpened.Invoke();
        return true;
    }
    public static bool CanEnter(Tile tile, Character character, bool open = false)
    {
        foreach (RogueDoor door in FindObjectsByType<RogueDoor>())
        {
            if (door.isOpen || GridManager.Instance.WorldToGrid(door.transform.position) != tile.gridPosition) continue;
            if (!door.CheckCharacter(character)) return false;
            if (open && !door.UnlockDoor(character as Rogue)) return false;
        }
        return true;
    }
}

