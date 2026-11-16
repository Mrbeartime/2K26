using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("Tile Settings")]
    public bool isWalkable = true;

    [Header("Grid Position")]
    public Vector2Int gridPosition;

    [Header("Occupant")]
    [SerializeField] private GameObject occupant;
    public event System.Action<GameObject> OccupantChanged;

    [Header("Highlight")]
    [Tooltip("Allow Move, Skill, and Enemy highlights on this tile. Disabling this also prevents skill targeting that requires a visible highlight.")]
    [SerializeField] private bool canShowHighlight = true;
    [SerializeField] private GameObject highlightObject;

    [Header("Highlight Colors")]
    [SerializeField] private Color moveHighlightColor = Color.cyan;
    [SerializeField] private Color skillHighlightColor = Color.yellow;
    [SerializeField] private Color enemyHighlightColor = Color.red;

    [Header("Highlight Opacity")]
    [Range(0f, 1f)]
    [SerializeField] private float moveHighlightOpacity = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float skillHighlightOpacity = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float enemyHighlightOpacity = 1f;

    [Header("Highlight Glow")]
    [SerializeField, Min(0f)] private float moveGlow = 1.5f;
    [SerializeField, Min(0f)] private float skillGlow = 2.5f;
    [SerializeField, Min(0f)] private float enemyGlow = 3f;

    public enum HighlightType
    {
        Move,
        Skill,
        Enemy
    }

    private HighlightType currentHighlightType;
    private bool highlightRequested;
    private HighlightType? previewHighlightType;

    public void SetCanShowHighlight(bool allowed)
    {
        canShowHighlight = allowed;
        RefreshHighlight();
    }

    public bool IsHighlightShown(HighlightType type) =>
        highlightRequested && canShowHighlight && highlightObject != null &&
        highlightObject.activeInHierarchy && currentHighlightType == type;

    private void Awake()
    {
        if (!canShowHighlight) HideHighlight();
    }

    // ใช้อันนี้เวลาต้องการเปิด Highlight พร้อมกำหนดประเภท
    public void ShowHighlight(HighlightType type)
    {
        highlightRequested = true;
        currentHighlightType = type;
        RefreshHighlight();
    }

    internal void SetPreviewHighlight(HighlightType? type)
    {
        previewHighlightType = type;
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        if (highlightObject == null) return;

        bool visible = canShowHighlight && (previewHighlightType.HasValue || highlightRequested);
        highlightObject.SetActive(visible);
        if (!visible) return;

        var (color, opacity, glow) = (previewHighlightType ?? currentHighlightType) switch
        {
            HighlightType.Move => (moveHighlightColor, moveHighlightOpacity, moveGlow),
            HighlightType.Skill => (skillHighlightColor, skillHighlightOpacity, skillGlow),
            HighlightType.Enemy => (enemyHighlightColor, enemyHighlightOpacity, enemyGlow),
            _ => (moveHighlightColor, moveHighlightColor.a, 0f)
        };
        color.a = opacity;
        SetHighlightColor(color, glow);
    }

    // ปิด Highlight
    public void HideHighlight()
    {
        highlightRequested = false;
        RefreshHighlight();
    }

    // เก็บ ToggleHighlight เดิมไว้
    // ป้องกัน Script อื่นที่ใช้อยู่พัง
    public void ToggleHighlight(bool show)
    {
        if (show)
        {
            ShowHighlight(HighlightType.Move);
        }
        else
        {
            HideHighlight();
        }
    }

    private void SetHighlightColor(Color color, float glow)
    {
        if (highlightObject == null)
            return;


        foreach (Renderer renderer in
                 highlightObject.GetComponentsInChildren<Renderer>(true))
        {
            MaterialPropertyBlock properties =
                new MaterialPropertyBlock();

            renderer.GetPropertyBlock(properties);

            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);

            //Add glowww
            Color emission = color;
            emission.a = 1f;

            properties.SetColor("_EmissionColor", emission * glow);

            renderer.SetPropertyBlock(properties);
        }
    }

    // =========================
    // Occupant
    // =========================

    public bool IsOccupied
    {
        get
        {
            return occupant != null;
        }
    }

    public GameObject Occupant
    {
        get
        {
            return occupant;
        }
    }

    public bool SetOccupant(GameObject newOccupant)
    {
        if (IsOccupied && occupant != newOccupant)
        {
            return false;
        }

        if (occupant == newOccupant) return true;
        occupant = newOccupant;
        OccupantChanged?.Invoke(occupant);

        return true;
    }

    public void ClearOccupant(GameObject target)
    {
        if (occupant == target)
        {
            occupant = null;
            OccupantChanged?.Invoke(null);
        }
    }
}
