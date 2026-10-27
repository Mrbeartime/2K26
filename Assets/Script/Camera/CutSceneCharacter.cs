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

    [SerializeField, Tooltip("Characters enter one at a time in array order.")]
    private CharacterEntry[] characters;

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

    private Vector3 endPosition;
    private CharacterAnimationController animationController;
    private Animator animator;
    private bool animationWasEnabled;
    private bool rootMotionWasEnabled;
    private bool prepared;
    private Transform currentActor;
    private bool actorPrepared;
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
                yield return PlayCharacter(entry);
            }
        }
        finally { Restore(); }
        Debug.Log("Character cutscene completed; gameplay UI restored.", this);
    }

    private IEnumerator PlayCharacter(CharacterEntry entry)
    {
        Transform actor = entry.character;
        if (actor == null) yield break;
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
            yield break;
        }

        currentActor = actor;
        endPosition = actor.position;
        actorPrepared = true;
        animationController = actor.GetComponent<CharacterAnimationController>();
        animationWasEnabled = animationController != null && animationController.enabled;
        if (animationController != null) animationController.enabled = false;
        animator = actor.GetComponentInChildren<Animator>();
        if (animator != null) { rootMotionWasEnabled = animator.applyRootMotion; animator.applyRootMotion = false; }
        CharacterFacing facing = actor.GetComponent<CharacterFacing>();
        Vector3 offset = endPosition - destinationFloor.position;
        actor.position = startFloor.position + offset;
        if (route.Count > 1) facing?.FaceTowards(route[1].transform.position + offset, true);
        CameraFocusManager cameraManager = CameraFocusManager.Instance;
        if (cutsceneCamera != null) cutsceneCamera.SetCutsceneTarget(actor);
        else if (cameraManager != null && cameraManager.BeginCutscene(actor, cameraZoomFactor))
            cutsceneCamera = cameraManager;
        ShowRenderers(actor);
        Debug.Log($"Cutscene {actor.name}: {startFloor.name} -> {destinationFloor.name} ({route.Count - 1} steps).", this);
        yield return new WaitForSeconds(openingPause);
        if (!prepared || !isActiveAndEnabled || actor == null) yield break;
        if (animator != null) animator.SetBool(Moving, true);

        for (int i = 1; i < route.Count && actor != null && prepared && isActiveAndEnabled; i++)
        {
            Vector3 target = route[i].transform.position + offset;
            facing?.FaceTowards(target);
            while (actor != null && prepared && isActiveAndEnabled && Vector3.Distance(actor.position, target) > 0.001f)
            {
                actor.position = Vector3.MoveTowards(actor.position, target, Mathf.Max(0.1f, walkSpeed) * Time.deltaTime);
                yield return null;
            }
        }
        if (animator != null) animator.SetBool(Moving, false);
        yield return new WaitForSeconds(arrivalPause);
        RestoreActor();
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
        RestoreActor();
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

    private void RestoreActor()
    {
        if (!actorPrepared) return;
        actorPrepared = false;
        if (currentActor != null) currentActor.position = endPosition;
        if (animator != null) { animator.SetBool(Moving, false); animator.applyRootMotion = rootMotionWasEnabled; }
        if (animationController != null) animationController.enabled = animationWasEnabled;
        currentActor = null;
        animator = null;
        animationController = null;
    }

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
