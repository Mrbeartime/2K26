using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DoorSwitchHoverBlink : MonoBehaviour
{
    public enum HoverType
    {
        Tile,
        Object
    }

    [Header("References")]
    [SerializeField] private HoverType hoverType = HoverType.Tile;
    [SerializeField] private Tile hoverTile;
    [SerializeField] private Collider hoverCollider;
    [SerializeField] private DoorController[] targetDoors;

    [Header("Blink Settings")]
    [SerializeField] private bool enableBlink = true;
    [SerializeField] private Color blinkColor = Color.green;
    [SerializeField, Min(0.05f)]
    private float blinkInterval = 0.3f;

    private Renderer[] doorRenderers;
    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock[] blinkBlocks;

    private Camera mainCamera;
    private bool isHovering;
    private bool isBlinkVisible;

    private void Start()
    {
        mainCamera = Camera.main;

        if (hoverTile == null)
        {
            hoverTile = GetComponent<Tile>();

            if (hoverTile == null && GridManager.Instance != null)
            {
                Vector2Int pos =
                    GridManager.Instance.WorldToGrid(transform.position);

                hoverTile = GridManager.Instance.GetTile(pos);
            }
        }

        if (targetDoors == null || targetDoors.Length == 0)
            return;

        List<Renderer> renderers = new List<Renderer>();

        foreach (DoorController door in targetDoors)
        {
            if (door == null)
                continue;

            renderers.AddRange(
                door.GetComponentsInChildren<Renderer>(true)
            );
        }

        doorRenderers = renderers.ToArray();

        originalBlocks = new MaterialPropertyBlock[doorRenderers.Length];
        blinkBlocks = new MaterialPropertyBlock[doorRenderers.Length];

        for (int i = 0; i < doorRenderers.Length; i++)
        {
            Renderer renderer = doorRenderers[i];

            originalBlocks[i] = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(originalBlocks[i]);

            blinkBlocks[i] = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(blinkBlocks[i]);

            blinkBlocks[i].SetColor("_BaseColor", blinkColor);
            blinkBlocks[i].SetColor("_Color", blinkColor);
        }
    }

    private void Update()
    {
        if (!enableBlink ||
            (hoverType == HoverType.Tile && hoverTile == null) ||
            (hoverType == HoverType.Object && hoverCollider == null) ||
            targetDoors == null || targetDoors.Length == 0 ||
            mainCamera == null ||
            doorRenderers == null || Mouse.current == null ||
            Time.timeScale == 0)
        {
            StopBlink();
            return;
        }

        bool hovering = IsMouseOverTarget();

        if (!hovering)
        {
            StopBlink();
            return;
        }

        isHovering = true;

        bool visible =
            Mathf.FloorToInt(Time.time / blinkInterval) % 2 == 0;

        if (visible != isBlinkVisible)
        {
            SetBlinkColor(visible);
        }
    }

    private bool IsMouseOverTarget()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return false;

        Ray ray = mainCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            Mathf.Infinity,
            ~0,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        // =========================
        // Hover Object (ArrowTutorial)
        // =========================
        if (hoverType == HoverType.Object)
        {
            if (hits.Length == 0)
                return false;

            Collider hitCollider = hits[0].collider;

            return hitCollider == hoverCollider ||
                hitCollider.transform.IsChildOf(hoverCollider.transform);
        }

        // =========================
        // Hover Tile (DoorSwitch / PressurePlate)
        // =========================
        foreach (RaycastHit hit in hits)
        {
            Tile tile = hit.collider.GetComponentInParent<Tile>();

            if (tile != null)
                return tile == hoverTile;
        }

        return false;
    }

    private void SetBlinkColor(bool visible)
    {
        isBlinkVisible = visible;

        for (int i = 0; i < doorRenderers.Length; i++)
        {
            if (doorRenderers[i] == null)
                continue;

            doorRenderers[i].SetPropertyBlock(
                visible ? blinkBlocks[i] : originalBlocks[i]
            );
        }
    }

    private void StopBlink()
    {
        if (!isHovering && !isBlinkVisible)
            return;

        isHovering = false;
        SetBlinkColor(false);
    }

    private void OnDisable()
    {
        StopBlink();
    }
}