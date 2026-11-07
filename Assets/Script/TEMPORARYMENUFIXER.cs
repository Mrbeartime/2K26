using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TEMPORARYMENUFIXER : MonoBehaviour
{
    [SerializeField] bool Active = true;
    [SerializeField] GameObject[] gameObjects;

    ProjectDInputAction _inputAction;

    void Awake()
    {
        _inputAction = new ProjectDInputAction();
    }

    void Start()
    {
        if (Active)
            gameObjectActivator(gameObjects);
    }

    public void gameObjectActivator(GameObject[] objects)
    {
        foreach (GameObject go in objects)
            go.SetActive(true);
    }

    void OnEnable()
    {
        if (_inputAction == null) return;

        _inputAction.MenuUI.Enable();
        _inputAction.MenuUI.Exit.performed += OnExit;
    }

    void OnDisable()
    {
        if (_inputAction == null) return;

        _inputAction.MenuUI.Exit.performed -= OnExit;
        _inputAction.MenuUI.Disable();
    }

    void OnDestroy()
    {
        _inputAction?.Dispose();
    }

    void OnExit(InputAction.CallbackContext ctx)
    {
        SceneManager.LoadScene("Start");
    }
}