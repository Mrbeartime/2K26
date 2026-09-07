using UnityEngine;
using UnityEngine.Events;
public class FloorSwitch : MonoBehaviour
{
    public bool IsOn { get; private set; }
    public UnityEvent onActivated = new UnityEvent();
    public void Activate(Character character)
    {
        if (character == null || IsOn) return;
        IsOn = true;
        onActivated.Invoke();
    }
}

