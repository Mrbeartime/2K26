using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(TurnGameManager))]
public class DebugSkipTurn : MonoBehaviour
{
    private TurnGameManager turnGameManager;

    private void Awake()
    {
        turnGameManager = GetComponent<TurnGameManager>();
    }

    private void Update()
    {
        if (Keyboard.current?.yKey.wasPressedThisFrame != true ||
            Time.timeScale == 0f || NewInGameUIManager.Instance?.BlocksGameplayInput == true)
            return;

        turnGameManager.ChooseEndTurn();
    }
}
