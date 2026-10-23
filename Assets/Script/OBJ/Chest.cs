using UnityEngine;
using UnityEngine.Events;
public class Chest : MonoBehaviour
{
    [SerializeField] private bool isOpen;
    public bool IsOpen => isOpen;
    public UnityEvent onOpened = new UnityEvent();
    private bool showWin;
    private float previousTimeScale = 1f;
    public void Open(Character character)
    {
        if (character == null || isOpen || Time.timeScale == 0) return;
        isOpen = true;
        previousTimeScale = Time.timeScale;
        showWin = true;
        Time.timeScale = 0;
        onOpened.Invoke();
    }
    protected virtual void OnTriggerEnter(Collider other) => Open(other.GetComponentInParent<Character>());
    protected virtual void OnCollisionEnter(Collision other) => Open(other.collider.GetComponentInParent<Character>());
    public void Back()
    {
        if (!showWin) return;
        showWin = false;
        Time.timeScale = previousTimeScale;
    }
    protected virtual void OnDisable() => Back();
    protected virtual void OnGUI()
    {
        if (!showWin) return;
        float x = (Screen.width - 320) / 2f, y = (Screen.height - 200) / 2f;
        GUI.Box(new Rect(x, y, 320, 200), GUIContent.none);
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(x, y + 20, 320, 80), "Win", title);
        if (GUI.Button(new Rect(x + 80, y + 125, 160, 45), "Back")) Back();
    }
}

