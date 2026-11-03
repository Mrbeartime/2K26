using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Played by TurnGameManager before the first player phase. Tile occupancy stays
// at the authored destination; this entrance never spends a move or fires traps.
public class CutSceneCharacter : MonoBehaviour
{
    [System.Serializable]
    public class CharacterEntry
    {
        public Transform character;
        public Transform startFloor;
        public Transform destinationFloor;
    }

    [SerializeField, Tooltip("All characters enter together. The cutscene waits for every character to arrive.")]
    private CharacterEntry[] characters;
    [Header("Before Cutscene Ends")]
    [SerializeField] private DoorCutScene closingDoor;

    // Preserve existing scenes/prefabs when upgrading from the single actor version.
    [SerializeField, HideInInspector] private Transform swordsman;
    [SerializeField, HideInInspector] private Transform startFloor;
    [SerializeField, HideInInspector] private Transform destinationFloor;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.6f;
    [SerializeField, Min(0f)] private float arrivalPause = 0.3f;
    [SerializeField, Min(0f)] private float openingPause = 0.6f;
    [Header("Cutscene Camera")]
    [SerializeField, Range(0.5f, 1f)] private float cameraZoomFactor = 0.85f;
    private CameraFocusManager cutsceneCamera;

    private sealed class ActorState
    {
        public Transform actor;
        public Vector3 endPosition;
        public Vector3 offset;
        public List<Tile> route;
        public int nextStep = 1;
        public CharacterFacing facing;
        public CharacterAnimationController animationController;
        public Animator animator;
        public bool animationWasEnabled;
        public bool rootMotionWasEnabled;
        public bool arrived;
    }

    private readonly List<ActorState> actors = new();
    private Transform groupCameraTarget;
    private bool prepared;
    private readonly Dictionary<Transform, List<Renderer>> hiddenRenderers = new();
    private readonly List<CanvasGroup> hiddenUI = new();
    private readonly List<InGameUIManager> disabledUIControllers = new();
    private static readonly int Moving = Animator.StringToHash("IsMoving");

    public IEnumerator Play()
    {
        if (prepared) yield break;
        MigrateLegacyEntry();
        if (characters == null || characters.Length == 0) yield break;
        prepared = true;
        HideUI();
        try
        {
            var entries = new List<CharacterEntry>();
            var seen = new HashSet<Transform>();
            foreach (CharacterEntry entry in characters)
            {
                if (entry == null || entry.character == null || entry.startFloor == null ||
                    entry.destinationFloor == null || !entry.character.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning("Cutscene entry skipped: assign an active character and both floors.", this);
                    continue;
                }
                if (!seen.Add(entry.character))
                {
                    Debug.LogWarning($"Duplicate cutscene character skipped: {entry.character.name}.", this);
                    continue;
                }
                entries.Add(entry);
                var renderers = new List<Renderer>();
                foreach (Renderer renderer in entry.character.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { renderers.Add(renderer); renderer.enabled = false; }
                hiddenRenderers.Add(entry.character, renderers);
            }

            // All actors register their authored gameplay tiles before being moved.
            yield return null;
            foreach (CharacterEntry entry in entries)
            {
                if (!prepared || !isActiveAndEnabled) yield break;
                PrepareCharacter(entry);
            }
            if (actors.Count > 0)
            {
                groupCameraTarget = new GameObject("Cutscene Group Camera Target").transform;
                UpdateGroupCameraTarget();
                CameraFocusManager cameraManager = CameraFocusManager.Instance;
                if (cameraManager != null && cameraManager.BeginCutscene(groupCameraTarget, cameraZoomFactor))
                    cutsceneCamera = cameraManager;
                yield return new WaitForSeconds(openingPause);
                if (!prepared || !isActiveAndEnabled) yield break;

                foreach (ActorState actor in actors)
                    if (actor.animator != null) actor.animator.SetBool(Moving, actor.route.Count > 1);

                bool everyoneArrived = false;
                while (!everyoneArrived)
                {
                    if (!prepared || !isActiveAndEnabled) yield break;
                    everyoneArrived = true;
                    foreach (ActorState actor in actors)
                    {
                        AdvanceActor(actor);
                        if (!actor.arrived) everyoneArrived = false;
                    }
                    UpdateGroupCameraTarget();
                    if (!everyoneArrived) yield return null;
                }
                yield return new WaitForSeconds(arrivalPause);
            }
            if (prepared && isActiveAndEnabled && closingDoor != null && closingDoor.isActiveAndEnabled)
                yield return closingDoor.Play();
        }
        finally { Restore(); }
        Debug.Log("Character cutscene completed; gameplay UI restored.", this);
    }

    private void PrepareCharacter(CharacterEntry entry)
    {
        Transform actor = entry.character;
        if (actor == null) return;
        Transform startFloor = entry.startFloor;
        Transform destinationFloor = entry.destinationFloor;
        Tile start = startFloor.GetComponent<Tile>();
        Tile destination = destinationFloor.GetComponent<Tile>();
        PlayerMove movement = actor.GetComponent<PlayerMove>();
        List<Tile> route = FindRoute(start, destination, actor.GetComponent<Character>());
        if (movement == null || movement.GetCurrentTile() != destination || route == null)
        {
            Debug.LogWarning($"Cutscene entry {actor.name} cannot play: start={start?.name}, destination={destination?.name}, current={movement?.GetCurrentTile()?.name}, routeFound={route != null}.", this);
            ShowRenderers(actor);
            return;
        }

        var state = new ActorState
        {
            actor = actor,
            endPosition = actor.position,
            offset = actor.position - destinationFloor.position,
            route = route,
            facing = actor.GetComponent<CharacterFacing>(),
            animationController = actor.GetComponent<CharacterAnimationController>(),
            animator = actor.GetComponentInChildren<Animator>()
        };
        state.animationWasEnabled = state.animationController != null && state.animationController.enabled;
        state.rootMotionWasEnabled = state.animator != null && state.animator.applyRootMotion;
        actors.Add(state);
        if (state.animationController != null) state.animationController.enabled = false;
        if (state.animator != null) state.animator.applyRootMotion = false;
        actor.position = startFloor.position + state.offset;
        if (route.Count > 1) state.facing?.FaceTowards(route[1].transform.position + state.offset, true);
        ShowRenderers(actor);
        Debug.Log($"Cutscene {actor.name}: {startFloor.name} -> {destinationFloor.name} ({route.Count - 1} steps).", this);
    }

    private void AdvanceActor(ActorState state)
    {
        if (state.arrived) return;
        if (state.actor == null) { state.arrived = true; return; }
        if (state.nextStep < state.route.Count)
        {
            Vector3 target = state.route[state.nextStep].transform.position + state.offset;
            state.facing?.FaceTowards(target);
            state.actor.position = Vector3.MoveTowards(state.actor.position, target, Mathf.Max(0.1f, walkSpeed) * Time.deltaTime);
            if (Vector3.Distance(state.actor.position, target) <= 0.001f)
                state.nextStep++;
        }
        if (state.nextStep >= state.route.Count)
        {
            state.actor.position = state.endPosition;
            state.arrived = true;
            if (state.animator != null) state.animator.SetBool(Moving, false);
        }
    }

    private void UpdateGroupCameraTarget()
    {
        if (groupCameraTarget == null) return;
        Vector3 total = Vector3.zero;
        int count = 0;
        foreach (ActorState state in actors)
            if (state.actor != null) { total += state.actor.position; count++; }
        if (count > 0) groupCameraTarget.position = total / count;
    }

    private List<Tile> FindRoute(Tile start, Tile destination, Character character)
    {
        if (start == null || destination == null || GridManager.Instance == null) return null;
        var queue = new Queue<Tile>();
        var previous = new Dictionary<Tile, Tile> { [start] = null };
        queue.Enqueue(start);
        Vector2Int[] directions = { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left };
        while (queue.Count > 0)
        {
            Tile tile = queue.Dequeue();
            if (tile == destination)
            {
                var route = new List<Tile>();
                for (Tile step = tile; step != null; step = previous[step]) route.Add(step);
                route.Reverse();
                return route;
            }
            foreach (Vector2Int direction in directions)
            {
                Tile next = GridManager.Instance.GetTile(tile.gridPosition + direction);
                // Other heroes are already registered on the staging floor.
                // Their reserved gameplay tiles must not cancel this entrance.
                if (next == null || previous.ContainsKey(next) || !next.isWalkable ||
                    !RogueDoor.CanEnter(next, character) || GridManager.Instance.IsBlockedByWall(tile, next, character)) continue;
                previous[next] = tile;
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private void ShowRenderers(Transform actor)
    {
        if (!hiddenRenderers.TryGetValue(actor, out var renderers)) return;
        foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = true;
        hiddenRenderers.Remove(actor);
    }

    private void HideUI()
    {
        // A dedicated group preserves existing canvas/group settings and lets
        // UI objects finish their normal lifecycle without showing the HUD.
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas.gameObject.scene != gameObject.scene) continue;
            CanvasGroup group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            hiddenUI.Add(group);
        }
        foreach (InGameUIManager ui in FindObjectsByType<InGameUIManager>())
        {
            if (ui.gameObject.scene != gameObject.scene || !ui.enabled) continue;
            disabledUIControllers.Add(ui);
            ui.enabled = false;
        }
    }

    private void Restore()
    {
        if (!prepared) return;
        prepared = false;
        if (cutsceneCamera != null) cutsceneCamera.EndCutscene();
        cutsceneCamera = null;
        foreach (ActorState state in actors)
        {
            if (state.actor != null) state.actor.position = state.endPosition;
            if (state.animator != null)
            {
                state.animator.SetBool(Moving, false);
                state.animator.applyRootMotion = state.rootMotionWasEnabled;
            }
            if (state.animationController != null) state.animationController.enabled = state.animationWasEnabled;
        }
        actors.Clear();
        if (groupCameraTarget != null) Destroy(groupCameraTarget.gameObject);
        groupCameraTarget = null;
        foreach (var renderers in hiddenRenderers.Values)
            foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = true;
        hiddenRenderers.Clear();
        foreach (CanvasGroup group in hiddenUI)
        {
            if (group == null) continue;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            Destroy(group);
        }
        hiddenUI.Clear();
        foreach (InGameUIManager ui in disabledUIControllers) if (ui != null) ui.enabled = true;
        disabledUIControllers.Clear();
    }

    private void OnDisable() => Restore();

    private void OnValidate() => MigrateLegacyEntry();

    private void MigrateLegacyEntry()
    {
        if (characters != null && characters.Length > 0) return;
        if (swordsman == null) return;
        characters = new[] { new CharacterEntry { character = swordsman, startFloor = startFloor, destinationFloor = destinationFloor } };
        swordsman = null;
        startFloor = null;
        destinationFloor = null;
    }
}
