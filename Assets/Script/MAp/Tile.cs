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
    [SerializeField] private GameObject highlightObject; // ลาก Mesh/Quad สีฟ้ามาใส่ช่องนี้

    public void ToggleHighlight(bool show)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(show);
            if (show) ClearHighlightColorOverride();
        }
    }

    // Used while the player is deciding whether to confirm a move destination.
    public void ToggleMovePreview(bool show)
    {
        if (highlightObject == null) return;
        highlightObject.SetActive(show);
        if (!show)
        {
            ClearHighlightColorOverride();
            return;
        }

        foreach (Renderer renderer in highlightObject.GetComponentsInChildren<Renderer>(true))
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", Color.red);
            properties.SetColor("_Color", Color.red);
            renderer.SetPropertyBlock(properties);
        }
    }

    private void ClearHighlightColorOverride()
    {
        foreach (Renderer renderer in highlightObject.GetComponentsInChildren<Renderer>(true))
        {
            renderer.SetPropertyBlock(null);
        }
    }
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

    // ใส่คน/Enemy ลง Tile
    public bool SetOccupant(GameObject newOccupant)
    {
        if (IsOccupied && occupant != newOccupant)
        {
            return false;
        }

        occupant = newOccupant;

        return true;
    }

    // เอาคนออกจาก Tile
    public void ClearOccupant(GameObject target)
    {
        if (occupant == target)
        {
            occupant = null;
        }
    }
}
