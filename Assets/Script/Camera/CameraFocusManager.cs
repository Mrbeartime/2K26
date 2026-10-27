using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine camera controller:
/// - Left-click selection (called from PlayerController): focus target + zoom in.
/// - Player starts walking: follow player + return to normal zoom.
/// - Hold right mouse button and drag: pan along the world XZ plane, zoom out,
///   and detach the camera from its focus without deselecting gameplay actions.
/// - Optionally clamp the camera TARGET to inspector-configured XZ bounds.
/// - Do not begin a drag on UI or move the camera while hovering UI.
/// </summary>
public class CameraFocusManager : MonoBehaviour
{
    public static CameraFocusManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CinemachineCamera gameCamera;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform cameraTarget;

    [Header("Focus")]
    [SerializeField] private float focusHeightOffset = 0.5f;
    [SerializeField, Range(0.5f, 1f)] private float focusZoomFactor = 0.85f;
    [SerializeField, Min(0.01f)] private float focusSmoothTime = 0.25f;

    [Header("Walking")]
    [SerializeField, Min(0.01f)] private float walkingSmoothTime = 0.15f;

    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.3f;

    [Header("Right Mouse Camera Pan")]
    [SerializeField] private bool enableCameraDrag = true;
    [SerializeField, Min(0.01f)] private float dragSensitivity = 1f;
    [SerializeField, Min(0f)] private float dragStartThresholdPixels = 5f;
    [SerializeField, Min(0.01f)] private float dragSmoothTime = 0.07f;
    [SerializeField] private bool zoomOutWhenDragging = true;
    [SerializeField] private bool blockDragOverUI = true;

    [Header("Camera Target Bounds (World X / Z)")]
    [SerializeField] private bool enableCameraBounds = true;
    [SerializeField] private Vector2 minimumXZ = new Vector2(-5f, -5f);
    [SerializeField] private Vector2 maximumXZ = new Vector2(5f, 5f);

    [Header("Return To Default After Drag")]
    [Tooltip("Return to the starting camera view after dragging.")]
    [SerializeField] private bool returnToDefaultAfterDrag = false;

    private Vector3 defaultPosition;

    private PlayerMove trackedPlayer;
    private PlayerMove lastObservedWalkingPlayer;
    private bool wasWalking;
    private bool manualPanMode;
    private Vector3 desiredPosition;
    private Vector3 positionVelocity;
    private float defaultLens;
    private float desiredLens;
    private float lensVelocity;
    private bool orthographic;
    private bool ready;
    private Transform cutsceneTarget;
    private bool cutsceneActive;
    private float cutsceneZoomFactor;
    private Vector3 beforeCutscenePosition;
    private float beforeCutsceneLens;

    private bool dragArmed;
    private bool isDragging;
    private Vector2 previousMousePosition;
    private Vector2 accumulatedMouseDelta;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (gameCamera == null || mainCamera == null || cameraTarget == null)
        {
            Debug.LogError("CameraFocusManager: Assign Game Camera, Main Camera, and Camera Target.", this);
            enabled = false;
            return;
        }

        orthographic = mainCamera.orthographic;
        var lens = gameCamera.Lens;
        defaultLens = orthographic ? lens.OrthographicSize : lens.FieldOfView;
        desiredLens = defaultLens;
        desiredPosition = cameraTarget.position;
        defaultPosition = cameraTarget.position;
        desiredPosition = defaultPosition;
        ready = true;
    }

    public void FocusCharacter(PlayerMove player)
    {
        if (!ready || player == null) return;

        EndManualPan();
        trackedPlayer = player;
        wasWalking = player.IsMoving();
        desiredPosition = ClampToBounds(player.transform.position + Vector3.up * focusHeightOffset);
        desiredLens = wasWalking ? defaultLens : defaultLens * focusZoomFactor;
    }

    public void FocusObject(Transform target)
    {
        if (!ready || target == null) return;

        EndManualPan();
        trackedPlayer = null;
        wasWalking = false;
        desiredPosition = ClampToBounds(target.position + Vector3.up * focusHeightOffset);
        desiredLens = defaultLens * focusZoomFactor;
    }

    public bool BeginCutscene(Transform target, float zoomFactor)
    {
        if (!ready || !isActiveAndEnabled || target == null || cutsceneActive) return false;
        beforeCutscenePosition = desiredPosition;
        beforeCutsceneLens = desiredLens;
        cutsceneTarget = target;
        cutsceneZoomFactor = Mathf.Clamp(zoomFactor, 0.5f, 1f);
        cutsceneActive = true;
        EndManualPan();
        positionVelocity = Vector3.zero;
        lensVelocity = 0f;
        return true;
    }

    public void EndCutscene()
    {
        if (!cutsceneActive) return;
        cutsceneActive = false;
        cutsceneTarget = null;
        desiredPosition = beforeCutscenePosition;
        desiredLens = beforeCutsceneLens;
        positionVelocity = Vector3.zero;
        lensVelocity = 0f;
    }

    public void SetCutsceneTarget(Transform target)
    {
        if (cutsceneActive && target != null) cutsceneTarget = target;
    }

    private void LateUpdate()
    {
        if (!ready) return;
        if (cutsceneActive)
        {
            CancelDragInput();
            if (cutsceneTarget == null) EndCutscene();
            else
            {
                // Cinematic motion does not set PlayerMove.IsMoving().
                // Follow the actor directly, including outside gameplay pan bounds.
                desiredPosition = cutsceneTarget.position + Vector3.up * focusHeightOffset;
                desiredLens = defaultLens * cutsceneZoomFactor;
                ApplyCamera(walkingSmoothTime);
                return;
            }
        }
        if (TurnGameManager.Instance != null && TurnGameManager.Instance.IsIntroPlaying)
        {
            CancelDragInput();
            return;
        }

        if (Time.timeScale == 0f ||
            (TurnGameManager.Instance != null && TurnGameManager.Instance.IsGameComplete) ||
            (InGameUIManager.Instance != null && InGameUIManager.Instance.IsPaused))
        {
            CancelDragInput();
            return;
        }

        HandleRightMouseDrag();
        DetectNewWalkingAction();

        bool walking = trackedPlayer != null && trackedPlayer.IsMoving();

        if (walking && !manualPanMode)
        {
            desiredPosition = ClampToBounds(trackedPlayer.transform.position + Vector3.up * focusHeightOffset);
            desiredLens = defaultLens;
            wasWalking = true;
        }
        else if (wasWalking)
        {
            // Character just stopped: leave the camera near the last position, zoomed out.
            if (trackedPlayer != null)
                desiredPosition = ClampToBounds(trackedPlayer.transform.position + Vector3.up * focusHeightOffset);
            desiredLens = defaultLens;
            wasWalking = false;
        }

        desiredPosition = ClampToBounds(desiredPosition);
        float smoothTime = isDragging ? dragSmoothTime
            : walking && !manualPanMode ? walkingSmoothTime : focusSmoothTime;

        ApplyCamera(smoothTime);
    }

    private void ApplyCamera(float smoothTime)
    {
        cameraTarget.position = Vector3.SmoothDamp(
            cameraTarget.position,
            desiredPosition,
            ref positionVelocity,
            smoothTime);

        var lens = gameCamera.Lens;
        float currentLens = orthographic ? lens.OrthographicSize : lens.FieldOfView;
        float nextLens = Mathf.SmoothDamp(currentLens, desiredLens, ref lensVelocity, zoomSmoothTime);

        if (orthographic) lens.OrthographicSize = Mathf.Max(0.01f, nextLens);
        else lens.FieldOfView = Mathf.Clamp(nextLens, 1f, 179f);

        gameCamera.Lens = lens;
    }

    private void DetectNewWalkingAction()
    {
        PlayerMove walkingPlayer = null;
        TurnGameManager turnManager = TurnGameManager.Instance;

        if (turnManager != null && turnManager.CurrentPlayer != null &&
            turnManager.CurrentPlayer.IsMoving())
        {
            walkingPlayer = turnManager.CurrentPlayer;
        }
        else if (turnManager == null && trackedPlayer != null && trackedPlayer.IsMoving())
        {
            walkingPlayer = trackedPlayer;
        }

        // Trigger only on the first frame of a new walk. If the player drags
        // the camera DURING that walk, do not immediately steal control back.
        if (walkingPlayer != lastObservedWalkingPlayer)
        {
            if (walkingPlayer != null && !isDragging && !dragArmed)
            {
                trackedPlayer = walkingPlayer;
                manualPanMode = false;
                desiredLens = defaultLens;
            }

            lastObservedWalkingPlayer = walkingPlayer;
        }
    }

    private void HandleRightMouseDrag()
    {
        Mouse mouse = Mouse.current;
        if (!enableCameraDrag || mouse == null)
        {
            if (isDragging && returnToDefaultAfterDrag)
                ReturnToDefaultView();

            CancelDragInput();
            return;
        }

        if (mouse.rightButton.wasReleasedThisFrame || !mouse.rightButton.isPressed)
        {
            if (isDragging && returnToDefaultAfterDrag)
                ReturnToDefaultView();

            CancelDragInput();
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();

        if (mouse.rightButton.wasPressedThisFrame)
        {
            // Block the WHOLE gesture if the right press started over UI.
            dragArmed = !blockDragOverUI || !IsPointerOverUI();
            isDragging = false;
            previousMousePosition = mousePosition;
            accumulatedMouseDelta = Vector2.zero;
            return;
        }

        if (!dragArmed) return;

        Vector2 delta = mousePosition - previousMousePosition;
        previousMousePosition = mousePosition;

        // Pause movement over UI without accumulating a jump for the next frame.
        if (blockDragOverUI && IsPointerOverUI())
        {
            accumulatedMouseDelta = Vector2.zero;
            return;
        }

        Vector2 previousPoint = mousePosition - delta;
        if (!isDragging)
        {
            accumulatedMouseDelta += delta;
            if (accumulatedMouseDelta.magnitude < dragStartThresholdPixels) return;

            isDragging = true;
            manualPanMode = true;
            trackedPlayer = null;
            wasWalking = false;
            if (zoomOutWhenDragging) desiredLens = defaultLens;

            // Apply the full movement since pressing down once the threshold is met.
            previousPoint = mousePosition - accumulatedMouseDelta;
            accumulatedMouseDelta = Vector2.zero;
        }

        // Project BOTH mouse positions onto a flat plane using the SAME camera
        // frame. This handles rotated/isometric and perspective cameras correctly.
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, cameraTarget.position.y, 0f));
        Ray fromRay = mainCamera.ScreenPointToRay(previousPoint);
        Ray toRay = mainCamera.ScreenPointToRay(mousePosition);

        if (!groundPlane.Raycast(fromRay, out float fromDistance) ||
            !groundPlane.Raycast(toRay, out float toDistance)) return;

        Vector3 fromWorld = fromRay.GetPoint(fromDistance);
        Vector3 toWorld = toRay.GetPoint(toDistance);
        Vector3 shift = (fromWorld - toWorld) * dragSensitivity;
        shift.y = 0f;

        desiredPosition = ClampToBounds(desiredPosition + shift);
    }

    private void ReturnToDefaultView()
    {
        trackedPlayer = null;
        manualPanMode = false;
        wasWalking = false;

        desiredPosition = ClampToBounds(defaultPosition);
        desiredLens = defaultLens;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void EndManualPan()
    {
        manualPanMode = false;
        CancelDragInput();
    }

    private void CancelDragInput()
    {
        dragArmed = false;
        isDragging = false;
        accumulatedMouseDelta = Vector2.zero;
    }

    private Vector3 ClampToBounds(Vector3 position)
    {
        if (!enableCameraBounds) return position;

        position.x = Mathf.Clamp(position.x,
            Mathf.Min(minimumXZ.x, maximumXZ.x),
            Mathf.Max(minimumXZ.x, maximumXZ.x));
        position.z = Mathf.Clamp(position.z,
            Mathf.Min(minimumXZ.y, maximumXZ.y),
            Mathf.Max(minimumXZ.y, maximumXZ.y));
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!enableCameraBounds) return;

        float y = cameraTarget != null ? cameraTarget.position.y : 0f;
        float left = Mathf.Min(minimumXZ.x, maximumXZ.x);
        float right = Mathf.Max(minimumXZ.x, maximumXZ.x);
        float bottom = Mathf.Min(minimumXZ.y, maximumXZ.y);
        float top = Mathf.Max(minimumXZ.y, maximumXZ.y);

        Vector3 a = new Vector3(left, y, bottom);
        Vector3 b = new Vector3(right, y, bottom);
        Vector3 c = new Vector3(right, y, top);
        Vector3 d = new Vector3(left, y, top);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
