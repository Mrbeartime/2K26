using UnityEngine;
public sealed class DiaryInteractable : MonoBehaviour
{
    [SerializeField] private string diaryId = "entrance-note";
    [SerializeField] private string diaryTitle = "Dungeon Note";
    [SerializeField, TextArea(5, 30)] private string diaryBody = "Read carefully. The switches control the doors.\nYou may return to this note at any time.";
    [SerializeField, TextArea(5, 30), Tooltip("Leave empty to display the preserved Diary Body as one page.")] private string[] pages = System.Array.Empty<string>();
    [SerializeField] private DiaryUI uiPrefab;
    private static DiaryUI view;
    public static DiaryInteractable FindAt(Tile tile)
    {
        if (tile == null || GridManager.Instance == null) return null;
        foreach (var diary in FindObjectsByType<DiaryInteractable>())
            if (diary.isActiveAndEnabled && diary.uiPrefab != null && GridManager.Instance.WorldToGrid(diary.transform.position) == tile.gridPosition) return diary;
        return null;
    }
    public bool Read(PlayerMove reader)
    {
        if (DiaryUI.IsOpen || uiPrefab == null || !isActiveAndEnabled ||
            reader == null || !reader.isActiveAndEnabled || reader.IsMoving() ||
            GridManager.Instance == null) return false;
        Tile tile = reader.GetCurrentTile();
        if (tile == null || GridManager.Instance.WorldToGrid(transform.position) != tile.gridPosition)
            return false;
        if (!reader.TryGetComponent<Character>(out var character) || character.IsDead) return false;
        if (view == null) view = Instantiate(uiPrefab);
        view.Open(diaryTitle, pages, diaryBody);
        return true;
    }
}
