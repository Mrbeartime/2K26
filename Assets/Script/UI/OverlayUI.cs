using UnityEngine;
using UnityEngine.UI;

public class OverlayUI : MonoBehaviour
{
    [SerializeField] private Canvas overlay;
    [SerializeField] private Image background;
    private bool opening;

    private void Awake()
    {
        overlay.overrideSorting = true;
        overlay.sortingOrder = Mathf.Max(overlay.sortingOrder, 30000);
        if (background == null || background.transform.parent != transform)
        {
            Debug.LogWarning("Overlay have no bg");
        }
        background.raycastTarget = true;
        if (!opening) gameObject.SetActive(false);
    }

    public void ActivateOverlay()
    {
        opening = true;
        gameObject.SetActive(true);
    }

    public void DeactivateOverlay() => gameObject.SetActive(false);
}
