using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using TMPro;
public sealed class DiaryUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text pageCounter;
    private InputAction previousPageAction;
    private InputAction nextPageAction;
    private string[] pages;
    private string legacyBody;
    private int pageIndex;
    private int lastPageChangeFrame;
    private int PageCount => pages != null && pages.Length > 0 ? pages.Length : 1;
    private static DiaryUI active;
    public static bool IsOpen => active != null;
    private float previousScale;
    private GameObject previousFocus;
    private InGameUIManager[] controllers;
    private bool[] enabledStates;
    private ProjectDInputAction input;
    private InputSystemUIInputModule module;
    private UnityEngine.InputSystem.InputActionAsset previousActions;
    private int openedFrame;
    private void Awake() {
        input = new ProjectDInputAction();
        closeButton.onClick.AddListener(Close);
        previousButton.onClick.AddListener(PreviousPage);
        nextButton.onClick.AddListener(NextPage);
        previousPageAction = new InputAction("DiaryPrevious", InputActionType.Button, "<Keyboard>/leftArrow");
        nextPageAction = new InputAction("DiaryNext", InputActionType.Button, "<Keyboard>/rightArrow");
        previousPageAction.performed += OnPreviousPage;
        nextPageAction.performed += OnNextPage;
        panel.SetActive(false);
    }
    private void OnPreviousPage(InputAction.CallbackContext context) => PreviousPage();
    private void OnNextPage(InputAction.CallbackContext context) => NextPage();
    public void PreviousPage() => ChangePage(-1);
    public void NextPage() => ChangePage(1);
    private void ChangePage(int offset) {
        if(active != this || Time.frameCount == openedFrame || Time.frameCount == lastPageChangeFrame) return;
        int target = Mathf.Clamp(pageIndex + offset, 0, PageCount - 1);
        if(target == pageIndex) return;
        pageIndex = target;
        lastPageChangeFrame = Time.frameCount;
        RefreshPage();
    }
    private void RefreshPage() {
        body.text = pages != null && pages.Length > 0 ? pages[pageIndex] ?? string.Empty : legacyBody;
        pageCounter.text = $"Page {pageIndex + 1} / {PageCount}";
        previousButton.interactable = pageIndex > 0;
        nextButton.interactable = pageIndex < PageCount - 1;
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        if(EventSystem.current != null) {
            var selected = EventSystem.current.currentSelectedGameObject;
            if((selected == previousButton.gameObject && !previousButton.interactable) ||
               (selected == nextButton.gameObject && !nextButton.interactable)) closeButton.Select();
        }
    }
    public void Open(string heading, string[] content, string fallbackBody)
    {
        if (IsOpen) return;
        active = this; openedFrame = Time.frameCount;
        previousScale = Time.timeScale;
        controllers = FindObjectsByType<InGameUIManager>(); enabledStates = new bool[controllers.Length];
        for(int i=0;i<controllers.Length;i++) { enabledStates[i]=controllers[i].enabled; controllers[i].enabled=false; }
        if(EventSystem.current != null) {
            previousFocus=EventSystem.current.currentSelectedGameObject;
            module=EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if(module != null) { previousActions=module.actionsAsset; module.actionsAsset=input.asset; }
        }
        input.UI.Enable(); Time.timeScale=0;
        pages=content; legacyBody=fallbackBody; pageIndex=0; lastPageChangeFrame=-1;
        title.text=heading; panel.SetActive(true);
        RefreshPage();
        previousPageAction.Enable(); nextPageAction.Enable();
        closeButton.Select();
    }
    private void Update() {
        if(active != this || Time.frameCount == openedFrame) return;
        if(input.UI.Cancel.WasPressedThisFrame()) Close();
        else { float y=input.UI.Navigate.ReadValue<Vector2>().y; if(Mathf.Abs(y)>.5f) scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+y*Time.unscaledDeltaTime*.5f); }
    }
    public void Close() {
        if(active != this || Time.frameCount == openedFrame) return;
        Release(); panel.SetActive(false);
    }
    private void Release() {
        if(active != this) return;
        active=null; Time.timeScale=previousScale;
        previousPageAction.Disable(); nextPageAction.Disable();
        pages=null; legacyBody=null;
        if(module != null && module.actionsAsset==input.asset) module.actionsAsset=previousActions;
        input.UI.Disable();
        for(int i=0;controllers!=null && i<controllers.Length;i++) if(controllers[i]!=null) controllers[i].enabled=enabledStates[i];
        if(EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousFocus != null && previousFocus.activeInHierarchy ? previousFocus : null);
        module=null; previousFocus=null;
    }
    private void OnDisable() => Release();
    private void OnDestroy() { Release(); input?.Dispose(); previousPageAction?.Dispose(); nextPageAction?.Dispose(); }
}
