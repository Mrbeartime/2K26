using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class OverlayUI : MonoBehaviour
{
    [SerializeField] private Canvas overlay;
    [SerializeField] private Image background;
    [Tooltip("UI control to receive keyboard/gamepad navigation when this overlay opens. If empty, the first active interactable control inside the overlay is used.")]
    [SerializeField] private Selectable firstSelectedControl;
    private bool opening;
    private BackdropBlurCapture backdropBlur;

    private ProjectDInputAction input;
    private InputSystemUIInputModule uiModule;
    private InputActionAsset previousActions;
    private bool verticalNavigationHeld;

    public bool CancelPressedThisFrame => input != null && input.UI.Cancel.WasPressedThisFrame();

    private void Awake()
    {
        input = new ProjectDInputAction();
        overlay.overrideSorting = true;
        overlay.sortingOrder = Mathf.Max(overlay.sortingOrder, 30000);
        if (background == null || background.transform.parent != transform)
        {
            Debug.LogWarning("Overlay have no bg");
        }
        background.raycastTarget = true;
        backdropBlur = background.GetComponent<BackdropBlurCapture>();
        if (backdropBlur == null) backdropBlur = background.gameObject.AddComponent<BackdropBlurCapture>();
        if (!opening) gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (input == null) return;
        input.UI.Enable();
    }

    private void OnDisable()
    {
        verticalNavigationHeld = false;
        StopAllCoroutines();
        if (overlay != null) overlay.enabled = true;
        if (uiModule != null && uiModule.actionsAsset == input.asset)
            uiModule.actionsAsset = previousActions;
        uiModule = null;
        previousActions = null;
        if (input != null && input.UI.enabled) input.UI.Disable();
    }

    private void OnDestroy()
    {
        input?.Dispose();
    }

    private void LateUpdate()
    {
        if (input == null) return;

        bool navigatingVertically = Mathf.Abs(input.UI.Navigate.ReadValue<Vector2>().y) > 0.5f;
        bool startedNavigation = navigatingVertically && !verticalNavigationHeld;
        verticalNavigationHeld = navigatingVertically;

        // Restore focus after the EventSystem has processed this frame, so the
        // first direction press selects the first control without also moving past it.
        if (!startedNavigation || CancelPressedThisFrame || !overlay.enabled ||
            uiModule == null || uiModule.actionsAsset != input.asset ||
            EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null)
            return;

        SelectFirstControl();
    }

    public void ActivateOverlay()
    {
        opening = true;
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(OpenAfterBackdropCapture());
    }

    private IEnumerator OpenAfterBackdropCapture()
    {
        // Capture a newly rendered frame without this overlay, never the previous
        // frame which may still contain its controls (including slider handles).
        overlay.enabled = false;
        yield return new WaitForEndOfFrame();
        backdropBlur?.CaptureBackdrop();
        overlay.enabled = true;
        yield return SelectOverlayControl();
    }

    public void DeactivateOverlay()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        gameObject.SetActive(false);
    }

    private IEnumerator SelectOverlayControl()
    {
        // MenuScreen enables the Settings panel immediately after this method, so wait until all
        // controls are active before assigning keyboard/gamepad focus.
        yield return null;

        if (EventSystem.current == null) yield break;

        // Enabling a separate action map alone does not route input to Unity UI.
        // Bind the active EventSystem to this overlay's project action asset.
        if (uiModule == null)
        {
            uiModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (uiModule != null)
            {
                previousActions = uiModule.actionsAsset;
                uiModule.actionsAsset = input.asset;
                // Rebinding releases the previous module's action subscriptions.
                // Ensure the new map is enabled after that handover finishes.
                input.UI.Enable();
            }
        }

        SelectFirstControl();
    }

    private void SelectFirstControl()
    {
        Selectable control = firstSelectedControl;
        if (control == null || !control.isActiveAndEnabled || !control.IsInteractable())
        {
            control = null;
            foreach (Selectable candidate in GetComponentsInChildren<Selectable>(true))
            {
                if (candidate.isActiveAndEnabled && candidate.IsInteractable())
                {
                    control = candidate;
                    break;
                }
            }
        }

        if (control != null)
        {
            EventSystem.current.SetSelectedGameObject(control.gameObject);
            control.Select();
        }
    }
}
