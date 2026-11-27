using UnityEngine;
using UnityEngine.Events;
public class FloorSwitch : MonoBehaviour
{
    [SerializeField] private DoorController door;
    [SerializeField] private PropInteractAnimation[] interactionAnimations;
    public bool IsOn { get; private set; }
    public UnityEvent onActivated = new UnityEvent();

    public bool CanActivate => !IsOn;

    public bool TryActivate(Character character)
    {
        if (character == null || IsOn) return false;
        IsOn = true;
        if (interactionAnimations != null) foreach (var animation in interactionAnimations)
            if (animation != null) animation.SetActiveState(true);
        foreach (var animation in GetComponentsInChildren<PropInteractAnimation>())
            animation.SetActiveState(true);
        if (door != null) door.SetOpen(gameObject.name, true);
        onActivated.Invoke();
        return true;
    }

    // Retained for existing UnityEvent and script references.
    public void Activate(Character character) => TryActivate(character);
}

