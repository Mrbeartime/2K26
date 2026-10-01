using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("Tile Settings")]
    public bool isWalkable = true;

    [Header("Grid Position")]
    public Vector2Int gridPosition;

    [Header("Occupant")]
    [SerializeField] private GameObject occupant;

    [Header("Highlight")]
    [SerializeField] private GameObject highlightObject;

    [Header("Highlight Colors")]
    [SerializeField] private Color moveHighlightColor = Color.cyan;
    [SerializeField] private Color skillHighlightColor = Color.yellow;

    public enum HighlightType
    {
        Move,
        Skill
    }

    // ใช้อันนี้เวลาต้องการเปิด Highlight พร้อมกำหนดประเภท
    public void ShowHighlight(HighlightType type)
    {
        if (highlightObject == null)
            return;

        highlightObject.SetActive(true);

        Color targetColor = moveHighlightColor;

        switch (type)
        {
            case HighlightType.Move:
                targetColor = moveHighlightColor;
                break;

            case HighlightType.Skill:
                targetColor = skillHighlightColor;
                break;
        }

        SetHighlightColor(targetColor);
    }

    // ปิด Highlight
    public void HideHighlight()
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(false);
        }
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

    private void SetHighlightColor(Color color)
    {
        foreach (Renderer renderer in
                 highlightObject.GetComponentsInChildren<Renderer>(true))
        {
            MaterialPropertyBlock properties =
                new MaterialPropertyBlock();

            renderer.GetPropertyBlock(properties);

            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);

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

        occupant = newOccupant;

        return true;
    }

    public void ClearOccupant(GameObject target)
    {
        if (occupant == target)
        {
            occupant = null;
        }
    }
}