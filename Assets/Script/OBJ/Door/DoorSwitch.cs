using UnityEngine;
using UnityEngine.InputSystem;

public class DoorSwitch : MonoBehaviour
{
    [SerializeField] private DoorController door;
    private bool playerNear;
    private bool isOn;

    public bool CanActivate => door != null && !isOn;

    void Update()
    {
        if (playerNear && Keyboard.current.eKey.wasPressedThisFrame)
        {
            isOn = !isOn;
            if (door != null) door.SetOpen(gameObject.name, isOn);
        }
    }

    // Called by the turn-system React action. A switch is activated once and opens its linked door.
    public bool TryActivate(Character character)
    {
        if (character == null || !CanActivate) return false;
        isOn = true;
        door.SetOpen(gameObject.name, true);
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
