using ProjectD.Gameplay.Turns;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour, IWorldHighlights
{
    public static PlayerController Instance;

    private PlayerMove selectedPlayer;

    private Enemy selectedEnemy;
    private TurnArrowTrap selectedArrowTrap;

    // Read-only preview selection for UI; this does not select a turn actor or spend an action.
    public GameObject SelectedHazard
    {
        get
        {
            if (selectedEnemy != null && !selectedEnemy.IsDead && selectedEnemy.isActiveAndEnabled)
                return selectedEnemy.gameObject;
            if (selectedArrowTrap != null && selectedArrowTrap.isActiveAndEnabled)
                return selectedArrowTrap.gameObject;
            return null;
        }
    }
    private Camera mainCamera;
    private bool pendingRightClickAttack;
    private Vector2 rightClickOrigin;
    private float rightClickTime;
    [SerializeField, Min(0.05f)] private float interactDoubleClickInterval = 0.3f;
    private Tile pendingInteractTile;
    private PlayerMove pendingInteractPlayer;
    private float lastInteractClickTime;
    // เก็บรายการช่องที่กำลังแสดงสีอยู่
    private List<Tile> currentHighlightedTiles = new List<Tile>();
    private readonly List<Tile> actionPreviewTiles = new();
    private bool actionPreviewActive;
    private bool dangerOverviewVisible;
    private readonly HashSet<Tile> dangerOverviewTiles = new();
    private readonly HashSet<Tile> nextDangerOverviewTiles = new();
    private float nextDangerRefresh;
    private TurnSession session;

    private void OnEnable()
    {
        session = TurnSession.For(gameObject.scene);
        session.Highlights = this;
        session.PlayerSelected += SelectPlayer;
        session.HighlightsCleared += ClearHighlights;
        session.MoveRangeRequested += ShowSelectedMoveRange;
        session.SkillPreviewRequested += DrawSkillRange;
    }

    private void ShowSelectedMoveRange(PlayerMove player) => ShowMoveRange(player);

    private void DrawSkillRange(Character actor, bool preview)
    {
        ClearHighlightLayer(preview);
        foreach (Tile tile in SkillTargets.For(actor))
            AddHighlight(tile, Tile.HighlightType.Skill, preview);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        HandleDangerOverviewInput();
        if (session.ModalInputBlocked) { ResetInteractClick(); pendingRightClickAttack = false; return; }
        if ((session.Turns != null && session.Turns.IsIntroPlaying) ||
            Time.timeScale == 0 ||
            session.Overlay?.BlocksGameplayInput == true ||
            (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
        {
            ResetInteractClick();
            pendingRightClickAttack = false;
            return;
        }
        if (session.Turns != null)
        {
            HandleTurnInput(session.Turns);
            return;
        }

        HandleClick();
        HandleAttack();
    }

    private void LateUpdate()
    {
        if (dangerOverviewVisible && Time.unscaledTime >= nextDangerRefresh)
            RefreshDangerOverview();
        if (selectedArrowTrap == null) return;
        if (!selectedArrowTrap.isActiveAndEnabled)
        {
            ClearHighlights();
            return;
        }
        RefreshArrowTrapHighlights();
    }

    private void HandleDangerOverviewInput()
    {
        var turns = session.Turns;
        if (turns != null && turns.IsGameComplete)
        {
            HideDangerOverview();
            return;
        }
        if (session.ModalInputBlocked || Time.timeScale == 0f ||
            (turns != null && turns.IsIntroPlaying) ||
            session.Overlay?.BlocksGameplayInput == true) return;

        InputAction showMap = session.Overlay?.ShowMap;
        if (!(showMap?.WasPressedThisFrame() ?? (Keyboard.current?.tabKey.wasPressedThisFrame == true))) return;
        var camera = CameraFocusManager.Instance;
        if (camera != null && !camera.CanAcceptManualInput) return;
        if (dangerOverviewVisible) HideDangerOverview();
        else
        {
            dangerOverviewVisible = true;
            RefreshDangerOverview();
            camera?.ResetToStartingView();
        }
    }

    private void RefreshDangerOverview()
    {
        nextDangerRefresh = Time.unscaledTime + 0.15f;
        nextDangerOverviewTiles.Clear();
        foreach (var enemy in FindObjectsByType<Enemy>())
            if (enemy.isActiveAndEnabled && !enemy.IsDead && enemy.gameObject.scene == gameObject.scene)
                nextDangerOverviewTiles.UnionWith(enemy.GetAttackRange());
        foreach (var trap in FindObjectsByType<TurnArrowTrap>())
            if (trap.isActiveAndEnabled && trap.gameObject.scene == gameObject.scene)
                nextDangerOverviewTiles.UnionWith(trap.GetAttackRange());
        foreach (var trap in FindObjectsByType<SpikeTrap>())
            if (trap.isActiveAndEnabled && trap.gameObject.scene == gameObject.scene && trap.TargetTile != null)
                nextDangerOverviewTiles.Add(trap.TargetTile);
        foreach (var trap in FindObjectsByType<Trap>())
            if (trap.isActiveAndEnabled && trap.gameObject.scene == gameObject.scene && trap.TryGetComponent(out Tile tile))
                nextDangerOverviewTiles.Add(tile);

        foreach (Tile tile in dangerOverviewTiles)
            if (tile != null && !nextDangerOverviewTiles.Contains(tile)) tile.SetDangerOverview(false);
        foreach (Tile tile in nextDangerOverviewTiles)
            if (tile != null) tile.SetDangerOverview(true);
        dangerOverviewTiles.Clear();
        dangerOverviewTiles.UnionWith(nextDangerOverviewTiles);
    }

    private void HideDangerOverview()
    {
        dangerOverviewVisible = false;
        foreach (Tile tile in dangerOverviewTiles)
            if (tile != null) tile.SetDangerOverview(false);
        dangerOverviewTiles.Clear();
        nextDangerOverviewTiles.Clear();
    }

    private void RefreshArrowTrapHighlights()
    {
        List<Tile> range = selectedArrowTrap.GetAttackRange();
        foreach (Tile tile in currentHighlightedTiles)
            if (tile != null && !range.Contains(tile)) tile.HideHighlight();
        foreach (Tile tile in range)
            if (!currentHighlightedTiles.Contains(tile)) tile.ShowHighlight(Tile.HighlightType.Enemy, actionPreviewActive);
        currentHighlightedTiles = range;
    }

    private void HandleTurnInput(ITurnController turnManager)
    {
        if (!CanInteractFromWorld(turnManager) ||
            pendingInteractPlayer != turnManager.CurrentPlayer)
            ResetInteractClick();

        Mouse mouse = Mouse.current;
        if (mouse == null || mainCamera == null) return;

        IGameplayOverlay hud = session.Overlay;
        InputAction select = hud?.Select;
        if (!(select?.WasPressedThisFrame() ?? mouse.leftButton.wasPressedThisFrame)) return;
        if (hud?.IsPreviewingAction == true &&
            (turnManager.IsMoveTargeting || turnManager.IsSkillTargeting)) return;

        Vector2 mousePosition = mouse.position.ReadValue();
        if (turnManager.IsPointerOverTurnUI(mousePosition))
        {
            ResetInteractClick();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        if (HandleInteractClick(turnManager, hits)) return;

        foreach (RaycastHit hit in hits)
        {
            // =========================
            // คลิกหน้าไม้เพื่อดู Attack Range
            // =========================
            TurnArrowTrap clickedTrap =
                hit.collider.GetComponentInParent<TurnArrowTrap>();

            if (clickedTrap != null)
            {
                // ไม่เปิด Preview ขณะกำลังใช้ Skill
                if (!turnManager.IsSkillTargeting)
                {
                    SelectArrowTrap(clickedTrap);
                    return;
                }
            }
            // =========================
            // คลิก Enemy
            // =========================
            Enemy clickedEnemy =
                hit.collider.GetComponentInParent<Enemy>();

            if (clickedEnemy != null)
            {
                // ถ้ากำลังใช้ Skill
                // Enemy ต้องเป็นเป้าหมาย Skill ตามระบบเดิม
                if (turnManager.IsSkillTargeting)
                {
                    Tile enemyTile =
                        GridManager.Instance.GetTile(
                            clickedEnemy.CurrentLocation
                        );

                    if (enemyTile != null)
                        turnManager.UseSkillOn(enemyTile);

                    return;
                }

                // ถ้าไม่ได้ใช้ Skill
                // แค่เปิด/ปิด Enemy Range
                SelectEnemy(clickedEnemy);
                return;
            }

            // =========================
            // คลิก Player เดิม
            // =========================
            PlayerMove clickedPlayer = hit.collider.GetComponentInParent<PlayerMove>();
            if (clickedPlayer != null)
            {
                if (turnManager.IsSkillTargeting)
                {
                    turnManager.UseSkillOn(clickedPlayer.GetCurrentTile());
                    return;
                }
                if (turnManager.TrySelectPlayer(clickedPlayer) && turnManager.CurrentPlayer == clickedPlayer)
                    hud?.OpenActionSelection(clickedPlayer, playSelectionSound: false, confirmMove: true);
                return;
            }

            RogueDoor door = hit.collider.GetComponentInParent<RogueDoor>();
            if (door != null && turnManager.IsSkillTargeting)
            {
                turnManager.UseSkillOn(door);
                return;
            }

            Tile tile = hit.collider.GetComponentInParent<Tile>();
            if (tile == null) continue;

            if (tile.isWalkable && turnManager.IsMoveTargeting && selectedPlayer == turnManager.CurrentPlayer)
            {
                turnManager.MoveToTarget(tile);
                return;
            }

            if (turnManager.IsSkillTargeting)
            {
                turnManager.UseSkillOn(tile);
                return;
            }
        }
    }

    private bool CanInteractFromWorld(ITurnController turnManager)
    {
        return turnManager.CanChooseReact && session.Overlay?.IsInteractMode == true;
    }

    private bool HandleInteractClick(ITurnController turnManager, RaycastHit[] hits)
    {
        Tile target = null;
        if (CanInteractFromWorld(turnManager) &&
            GridManager.Instance != null)
        {
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                // Preserve enemy/trap previews and selection of other characters.
                if (collider.GetComponentInParent<Enemy>() != null ||
                    collider.GetComponentInParent<TurnArrowTrap>() != null) break;

                PlayerMove player = collider.GetComponentInParent<PlayerMove>();
                if (player != null)
                {
                    if (player == turnManager.CurrentPlayer) target = player.GetCurrentTile();
                    break;
                }

                Component prop = collider.GetComponentInParent<DoorSwitch>();
                if (prop == null) prop = collider.GetComponentInParent<DiaryInteractable>();
                if (prop == null) prop = collider.GetComponentInParent<FloorSwitch>();
                // Some lever models are separate scene objects on the switch's tile.
                if (prop == null) prop = collider.GetComponentInParent<PropInteractAnimation>();
                if (prop != null)
                {
                    target = GridManager.Instance.GetTile(
                        GridManager.Instance.WorldToGrid(prop.transform.position));
                    break;
                }

                target = collider.GetComponentInParent<Tile>();
                if (target != null) break;
            }
        }

        if (target == null || target != turnManager.CurrentPlayer?.GetCurrentTile())
        {
            ResetInteractClick();
            return false;
        }

        if (pendingInteractTile == target && pendingInteractPlayer == turnManager.CurrentPlayer &&
            Time.unscaledTime - lastInteractClickTime <= interactDoubleClickInterval)
        {
            ResetInteractClick();
            turnManager.ChooseReact();
        }
        else
        {
            pendingInteractTile = target;
            pendingInteractPlayer = turnManager.CurrentPlayer;
            lastInteractClickTime = Time.unscaledTime;
        }
        return true;
    }

    private void ResetInteractClick()
    {
        pendingInteractTile = null;
        pendingInteractPlayer = null;
    }

    private void OnDisable()
    {
        if (ReferenceEquals(session.Highlights, this)) session.Highlights = null;
        session.PlayerSelected -= SelectPlayer;
        session.HighlightsCleared -= ClearHighlights;
        session.MoveRangeRequested -= ShowSelectedMoveRange;
        session.SkillPreviewRequested -= DrawSkillRange;
        HideDangerOverview();
        ResetInteractClick();
        ClearActionPreview();
    }

    private void HandleClick()
    {
        if (Mouse.current == null || mainCamera == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // คลิก Player
            PlayerMove clickedPlayer = hit.collider.GetComponentInParent<PlayerMove>();
            if (clickedPlayer != null)
            {
                SelectPlayer(clickedPlayer);
                return;
            }

            // คลิก Tile
            Tile clickedTile = hit.collider.GetComponent<Tile>();
            if (clickedTile != null)
            {
                // ถ้าเป็นช่องที่เดินไม่ได้ ให้เมินไปเลย
                if (!clickedTile.isWalkable) return;

                if (selectedPlayer != null)
                {
                    // สั่งเดิน และปิดสีไฮไลต์ทั้งหมด
                    selectedPlayer.MoveToTile(clickedTile);
                    if (selectedPlayer.IsMoving()) ClearHighlights();
                }
                return;
            }
        }
    }

    public void SelectPlayer(PlayerMove player) => SelectPlayer(player, true);

    public void SelectPlayer(PlayerMove player, bool playSelectionSound)
    {
        if (playSelectionSound && player != null && selectedPlayer != player)
            ProjectD.Audio.GameAudioManager.Instance?.PlayUI(ProjectD.Audio.AudioCue.CharacterSelect);
        ClearHighlights();

        selectedPlayer = player;

        if (player == null)
            return;

        // เพิ่ม: เลื่อนกล้องไปหา Player และ Zoom In
        CameraFocusManager.Instance?.FocusCharacter(player);

        Debug.Log("Selected: " + player.name);
    }

    public void ShowMoveRange(PlayerMove player, bool preview = false)
    {
        ClearHighlightLayer(preview);

        if (player == null ||
            GridManager.Instance == null ||
            Pathfinder.Instance == null)
            return;

        Tile playerTile = player.GetCurrentTile();

        if (playerTile == null)
            return;

        foreach (Tile tile in Pathfinder.Instance.GetAllReachableTiles(playerTile, player.GetComponent<Character>()))
            AddHighlight(tile, Tile.HighlightType.Move, preview);
    }

    public void ShowInteractPreview(PlayerMove player)
    {
        BeginActionPreview();
        Tile tile = player.GetCurrentTile();
        if (tile != null) AddHighlight(tile, Tile.HighlightType.Skill, true);
    }

    private void HandleAttack()
    {
        if (Mouse.current == null || mainCamera == null || selectedPlayer == null) return;
        // Reserve held/dragged right clicks for camera rotation; a quick tap still attacks.
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            pendingRightClickAttack = true;
            rightClickOrigin = Mouse.current.position.ReadValue();
            rightClickTime = Time.unscaledTime;
            return;
        }
        if (Vector2.Distance(Mouse.current.position.ReadValue(), rightClickOrigin) >= 5f ||
            Time.unscaledTime - rightClickTime > 0.25f)
            pendingRightClickAttack = false;
        if (!Mouse.current.rightButton.wasReleasedThisFrame) return;
        bool attack = pendingRightClickAttack;
        pendingRightClickAttack = false;
        if (!attack) return;
        if (GridManager.Instance == null) return;
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Entity targetEntity = hit.collider.GetComponentInParent<Entity>();
            if (targetEntity != null && targetEntity.gameObject != selectedPlayer.gameObject)
            {
                if (targetEntity is ArrowTutorial && selectedPlayer.TryGetComponent(out ArcherMan archer))
                {
                    archer.BowShotAt(hit.point);
                    return;
                }
                Tile targetTile = GridManager.Instance.GetTile(targetEntity.CurrentLocation);
                selectedPlayer.GetComponent<Character>()?.Attack(targetTile);
                return;
            }
            Tile tile = hit.collider.GetComponentInParent<Tile>();
            if (tile == null) continue;
            selectedPlayer.GetComponent<Character>()?.Attack(tile);
            return;
        }
    }

    public bool IsSkillTileHighlighted(Tile tile) =>
        tile != null && currentHighlightedTiles.Contains(tile) &&
        tile.IsHighlightShown(Tile.HighlightType.Skill);

    public void ClearHighlights()
    {
        selectedEnemy = null;
        selectedArrowTrap = null;
        foreach (Tile tile in currentHighlightedTiles)
        {
            if (tile != null)
                tile.HideHighlight();
        }
        currentHighlightedTiles.Clear();
    }

    public void ClearActionPreview()
    {
        ClearPreviewTiles();
        SetActionPreviewActive(false);
    }

    public void BeginActionPreview()
    {
        SetActionPreviewActive(true);
        ClearPreviewTiles();
    }

    private void SetActionPreviewActive(bool active)
    {
        if (actionPreviewActive == active) return;
        actionPreviewActive = active;
        foreach (Tile tile in currentHighlightedTiles)
            if (tile != null) tile.SetMainHighlightSuppressed(active);
    }

    private void ClearPreviewTiles()
    {
        foreach (Tile tile in actionPreviewTiles)
            if (tile != null) tile.SetPreviewHighlight(null);
        actionPreviewTiles.Clear();
    }

    internal void ClearHighlightLayer(bool preview)
    {
        if (preview) BeginActionPreview();
        else ClearHighlights();
    }

    private void AddHighlight(Tile tile, Tile.HighlightType type, bool preview)
    {
        if (preview)
        {
            actionPreviewTiles.Add(tile);
            tile.SetPreviewHighlight(type);
        }
        else
        {
            currentHighlightedTiles.Add(tile);
            tile.ShowHighlight(type, actionPreviewActive);
        }
    }

    public void ShowRogueSkillRange(Tile origin, int range, bool preview = false)
    {
        ClearHighlightLayer(preview);
        if (!preview && !CanShowSelectedSkill()) return;
        foreach (Tile tile in SkillTargets.RogueRange(origin, range))
            AddHighlight(tile, Tile.HighlightType.Skill, preview);
    }

    private bool CanShowSelectedSkill()
    {
        ITurnController manager = session.Turns;
        PlayerMove player = manager != null ? manager.CurrentPlayer : selectedPlayer;
        return player != null && player.TryGetComponent(out Character character) &&
            character.CanUseSkill && (manager == null || manager.CanChooseSkill);
    }

    public void ShowSwordSkillRange(Tile origin, bool preview = false)
    {
        ClearHighlightLayer(preview);
        foreach (Tile tile in SkillTargets.Lanes(origin, false))
            AddHighlight(tile, Tile.HighlightType.Skill, preview);
    }

    public void ShowArcherSkillRange(Tile origin, bool preview = false)
    {
        ClearHighlightLayer(preview);
        if (!preview && !CanShowSelectedSkill()) return;
        foreach (Tile tile in SkillTargets.Lanes(origin, true))
            AddHighlight(tile, Tile.HighlightType.Skill, preview);
    }
    public void SelectEnemy(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return;

        ClearActionPreview();
        // =========================
        // กดตัวเดิมซ้ำ = ปิด
        // =========================
        if (selectedEnemy == enemy)
        {
            ClearHighlights();
            selectedEnemy = null;

            Debug.Log("Enemy Range OFF");
            return;
        }

        CameraFocusManager.Instance?.FocusObject(enemy.transform); // Camera

        // =========================
        // เลือก Enemy ตัวใหม่
        // =========================
        ClearHighlights();

        selectedArrowTrap = null; //เพื่อไม่ให้การเลือก Enemy กับหน้าไม้สลับกันแล้วสถานะค้าง
        selectedEnemy = enemy;

        List<Tile> attackRange = enemy.GetAttackRange();

        foreach (Tile tile in attackRange)
        {
            currentHighlightedTiles.Add(tile);
            tile.ShowHighlight(Tile.HighlightType.Enemy);
        }

        Debug.Log(
            $"Enemy Selected: {enemy.name} | Range: {attackRange.Count} tiles"
        );
    }

    public void SelectArrowTrap(TurnArrowTrap trap)
    {
        if (trap == null)
            return;

        ClearActionPreview();
        if (selectedArrowTrap == trap)
        {
            ClearHighlights();
            selectedArrowTrap = null;
            return;
        }

        ClearHighlights();

        selectedEnemy = null;
        selectedArrowTrap = trap;

        RefreshArrowTrapHighlights();

        CameraFocusManager.Instance?.FocusObject(trap.transform);
    }
}
