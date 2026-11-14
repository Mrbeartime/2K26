using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine camera controller:
/// - Left-click selection (called from PlayerController): focus target + zoom in.
/// - Player starts walking: follow player + return to normal zoom.
/// - Hold right mouse and drag to orbit; hold middle mouse and drag to pan along the ground plane.
/// - Optionally clamp the camera TARGET to inspector-configured XZ bounds.
/// - Mouse wheel zooms temporarily, then smoothly returns to the default view.
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

    [Header("Camera Rotation and Middle Mouse Pan")]
    [SerializeField] private bool enableCameraDrag = true;
    [SerializeField, Min(0.01f)] private float dragSensitivity = 1f;
    [SerializeField, Min(0.01f)] private float rotationSensitivity = 0.2f;
    [SerializeField, Range(1f, 89f)] private float minimumPitch = 15f;
    [SerializeField, Range(1f, 89f)] private float maximumPitch = 80f;
    private ProjectDInputAction cameraInputs;
    private float cameraYaw;
    private float cameraPitch;
    private bool isPanning;
    private bool panArmed;
    private Vector2 accumulatedPanDelta;
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
    private Vector2 accumulatedMouseDelta;

    [Header("Mouse Scroll Zoom")]
    [SerializeField] private bool enableScrollZoom = true;

    [SerializeField, Min(0.01f)]
    private float scrollZoomSpeed = 0.1f;

    [SerializeField, Range(0.1f, 1f)]
    private float minZoomFactor = 0.5f;

    [SerializeField, Min(1f)]
    private float maxZoomFactor = 1.5f;

    [SerializeField] private bool returnToDefaultAfterScroll = true;

    [SerializeField, Min(0f)]
    private float scrollReturnDelay = 0.8f;

    [SerializeField] private bool blockScrollOverUI = true;

    private bool scrollReturnPending;
    private float lastScrollTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        cameraInputs = new ProjectDInputAction();
    }

    private void OnEnable()
    {
        cameraInputs?.Camera.Enable();
    }

    private void OnDisable()
    {
        cameraInputs?.Camera.Disable();
        isPanning = false;
        CancelDragInput();
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
        cameraYaw = gameCamera.transform.eulerAngles.y;
        cameraPitch = Mathf.DeltaAngle(0f, gameCamera.transform.eulerAngles.x);
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
        scrollReturnPending = false;
    }

    public void FocusObject(Transform target)
    {
        if (!ready || target == null) return;

        EndManualPan();
        trackedPlayer = null;
        wasWalking = false;
        desiredPosition = ClampToBounds(target.position + Vector3.up * focusHeightOffset);
        desiredLens = defaultLens * focusZoomFactor;
        scrollReturnPending = false;
    }

    public bool BeginCutscene(Transform target, float zoomFactor)
    {
        if (!ready || !isActiveAndEnabled || target == null || cutsceneActive) return false;
        beforeCutscenePosition = desiredPosition;
        beforeCutsceneLens = desiredLens;
        cutsceneTarget = target;
        cutsceneZoomFactor = Mathf.Clamp(zoomFactor, 0.5f, 1f);
        cutsceneActive = true;
        scrollReturnPending = false;
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
        HandleMiddleMousePan();
        DetectNewWalkingAction();

        bool walking = trackedPlayer != null && trackedPlayer.IsMoving();

        if (walking && !manualPanMode && !scrollReturnPending)
        {
            desiredPosition = ClampToBounds(trackedPlayer.transform.position + Vector3.up * focusHeightOffset);
            if (!scrollReturnPending) desiredLens = defaultLens;
            wasWalking = true;
        }
        else if (wasWalking && !scrollReturnPending)
        {
            // Character just stopped: leave the camera near the last position, zoomed out.
            if (trackedPlayer != null)
                desiredPosition = ClampToBounds(trackedPlayer.transform.position + Vector3.up * focusHeightOffset);
            if (!scrollReturnPending) desiredLens = defaultLens;
            wasWalking = false;
        }
        // Mouse Scroll Zoom
        HandleMouseWheelZoom();

        desiredPosition = ClampToBounds(desiredPosition);
        float smoothTime = isDragging || isPanning ? dragSmoothTime
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
            if (walkingPlayer != null && !isDragging && !dragArmed && !isPanning && !panArmed && !scrollReturnPending)
            {
                trackedPlayer = walkingPlayer;
                manualPanMode = false;
                if (!scrollReturnPending) desiredLens = defaultLens;
            }

            lastObservedWalkingPlayer = walkingPlayer;
        }
    }

    private void HandleRightMouseDrag()
    {
        if (!enableCameraDrag || cameraInputs == null || !cameraInputs.Camera.Rotate.IsPressed())
        {
            CancelRotationInput();
            return;
        }
        if (cameraInputs.Camera.Rotate.WasPressedThisFrame())
        {
            dragArmed = !blockDragOverUI || !IsPointerOverUI();
            isDragging = false;
            accumulatedMouseDelta = Vector2.zero;
            return;
        }
        if (!dragArmed) return;
        if (blockDragOverUI && IsPointerOverUI())
        {
            accumulatedMouseDelta = Vector2.zero;
            return;
        }
        Vector2 delta = cameraInputs.Camera.Look.ReadValue<Vector2>();
        if (!isDragging)
        {
            accumulatedMouseDelta += delta;
            if (accumulatedMouseDelta.magnitude < dragStartThresholdPixels) return;
            isDragging = true;
            delta = accumulatedMouseDelta;
            accumulatedMouseDelta = Vector2.zero;
        }
        scrollReturnPending = false;
        cameraYaw += delta.x * rotationSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch - delta.y * rotationSensitivity,
            Mathf.Min(minimumPitch, maximumPitch), Mathf.Max(minimumPitch, maximumPitch));
        // PositionComposer uses this orientation to orbit around its tracking target.
        gameCamera.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
    }

    private void HandleMiddleMousePan()
    {
        if (!enableCameraDrag || cameraInputs == null || Mouse.current == null ||
            !cameraInputs.Camera.Pan.IsPressed())
        {
            if (isPanning && returnToDefaultAfterDrag) ReturnToDefaultView();
            CancelPanInput();
            return;
        }
        if (cameraInputs.Camera.Pan.WasPressedThisFrame())
        {
            panArmed = !blockDragOverUI || !IsPointerOverUI();
            isPanning = false;
            accumulatedPanDelta = Vector2.zero;
            return;
        }
        if (!panArmed) return;
        if (blockDragOverUI && IsPointerOverUI())
        {
            accumulatedPanDelta = Vector2.zero;
            return;
        }

        Vector2 delta = cameraInputs.Camera.Look.ReadValue<Vector2>();
        if (!isPanning)
        {
            accumulatedPanDelta += delta;
            if (accumulatedPanDelta.magnitude < dragStartThresholdPixels) return;
            isPanning = true;
            delta = accumulatedPanDelta;
            accumulatedPanDelta = Vector2.zero;
            manualPanMode = true;
            trackedPlayer = null;
            wasWalking = false;
            if (zoomOutWhenDragging) desiredLens = defaultLens;
        }
        scrollReturnPending = false;

        // Project both screen axes onto the map using the same camera frame.
        // Dragging vertically moves across the map, keeping target height unchanged.
        Vector2 pointer = Mouse.current.position.ReadValue();
        Plane ground = new Plane(Vector3.up, new Vector3(0f, cameraTarget.position.y, 0f));
        Ray from = mainCamera.ScreenPointToRay(pointer - delta);
        Ray to = mainCamera.ScreenPointToRay(pointer);
        if (!ground.Raycast(from, out float fromDistance) ||
            !ground.Raycast(to, out float toDistance)) return;
        Vector3 shift = (from.GetPoint(fromDistance) - to.GetPoint(toDistance)) * dragSensitivity;
        shift.y = 0f;
        desiredPosition = ClampToBounds(desiredPosition + shift);
    }

    private void ReturnToDefaultView()
    {
        scrollReturnPending = false;
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
        CancelRotationInput();
        CancelPanInput();
    }

    private void CancelPanInput()
    {
        panArmed = false;
        isPanning = false;
        accumulatedPanDelta = Vector2.zero;
    }

    private void CancelRotationInput()
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

    private void HandleMouseWheelZoom()
    {
        if (!enableScrollZoom || Mouse.current == null)
        {
            scrollReturnPending = false;
            return;
        }

        // ไม่ให้ระบบ Zoom ชนกับการลากกล้อง
        if (isDragging || dragArmed || isPanning || panArmed)
        {
            scrollReturnPending = false;
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;

        // Current Input System normalizes wheel steps already. Only legacy
        // Windows platform-specific input needs conversion from 120 units.
        if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
            (Application.platform == RuntimePlatform.WindowsEditor ||
             Application.platform == RuntimePlatform.WindowsPlayer))
            scroll /= 120f;

        if (Mathf.Abs(scroll) > 0.01f &&
            (!blockScrollOverUI || !IsPointerOverUI()))
        {
            // หมุนขึ้น = Zoom In
            // หมุนลง = Zoom Out
            float zoomChange =
                scroll * defaultLens * scrollZoomSpeed;

            float previousLens = desiredLens;
            desiredLens = Mathf.Clamp(
                desiredLens - zoomChange,
                defaultLens * minZoomFactor,
                defaultLens * maxZoomFactor
            );

            ZoomTowardsPointer(Mouse.current.position.ReadValue(), previousLens, desiredLens);

            lastScrollTime = Time.unscaledTime;
            scrollReturnPending = true;
        }

        // หลังหยุดหมุนเมาส์ กลับกล้อง Default
        if (scrollReturnPending && returnToDefaultAfterScroll &&
            Time.unscaledTime - lastScrollTime >= scrollReturnDelay)
        {
            ReturnToDefaultView();
        }
    }

    private void ZoomTowardsPointer(Vector2 screenPosition, float previousLens, float nextLens)
    {
        if (Mathf.Approximately(previousLens, nextLens)) return;

        // Use the same horizontal map plane as camera panning. Comparing the
        // cursor with the view centre also works with the isometric camera tilt.
        Plane mapPlane = new Plane(Vector3.up, new Vector3(0f, cameraTarget.position.y, 0f));
        Ray pointerRay = mainCamera.ScreenPointToRay(screenPosition);
        Ray centreRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!mapPlane.Raycast(pointerRay, out float pointerDistance) ||
            !mapPlane.Raycast(centreRay, out float centreDistance)) return;

        float ratio = orthographic
            ? nextLens / Mathf.Max(0.01f, previousLens)
            : Mathf.Tan(Mathf.Clamp(nextLens, 1f, 179f) * Mathf.Deg2Rad * 0.5f) /
              Mathf.Tan(Mathf.Clamp(previousLens, 1f, 179f) * Mathf.Deg2Rad * 0.5f);
        Vector3 shift = (pointerRay.GetPoint(pointerDistance) - centreRay.GetPoint(centreDistance)) * (1f - ratio);
        shift.y = 0f;
        desiredPosition = ClampToBounds(desiredPosition + shift);
    }

    private void OnDestroy()
    {
        cameraInputs?.Disable();
        cameraInputs?.Dispose();
        if (Instance == this) Instance = null;
    }

}
