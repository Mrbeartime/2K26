using UnityEngine;
using UnityEngine.InputSystem;

public class DoorSwitch : MonoBehaviour
{
    [SerializeField] private DoorController door;
    private bool playerNear;
    private bool isOn;

    public bool CanActivate => door != null;

    void Update()
    {
        if (playerNear && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            isOn = !isOn;
            if (door != null) door.SetOpen(gameObject.name, isOn);
        }
    }

    // Called by the turn-system React action. Each use toggles the linked door.
    public bool TryActivate(Character character)
    {
        if (character == null || !CanActivate) return false;
        isOn = !isOn;
        door.SetOpen(gameObject.name, isOn);
        return true;
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
