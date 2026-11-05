using UnityEngine;

/// <summary>
/// Marks the level exit. Player movement is transform-based, so the turn
/// manager checks collider overlap directly instead of relying on trigger
/// callbacks, which require a Rigidbody to be reliable.
/// </summary>
public class WinBox : MonoBehaviour
{
    public enum CompletionTarget
    {
        FromScene = 0,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5,
        SecretLevel1 = 100
    }

    [Header("Level Progress (JSON)")]
    [Tooltip("Choose which level to complete. From Scene uses this WinBox's scene path.")]
    [SerializeField] private CompletionTarget completionTarget = CompletionTarget.FromScene;
    private bool completionRecorded;

    public static bool IsReachedBy(PlayerMove player)
    {
        foreach (WinBox winBox in FindObjectsByType<WinBox>())
            if (winBox.TryComplete(player)) return true;
        return false;
    }

    public bool TryComplete(PlayerMove player)
    {
        if (!isActiveAndEnabled || player == null || !player.isActiveAndEnabled ||
            player.gameObject.scene != gameObject.scene) return false;
        if (player.TryGetComponent(out Character character) && character.IsDead) return false;
        Collider playerCollider = player.GetComponent<Collider>();
        Collider goalCollider = GetComponent<Collider>();
        if (playerCollider == null || goalCollider == null || !playerCollider.enabled || !goalCollider.enabled ||
            !goalCollider.bounds.Intersects(playerCollider.bounds)) return false;

        if (!completionRecorded)
        {
            if (completionTarget == CompletionTarget.FromScene)
                completionRecorded = ProjectD.Menus.GameSceneManager.CompleteLevelForScene(gameObject.scene.path);
            else if (completionTarget == CompletionTarget.SecretLevel1)
                completionRecorded = ProjectD.Menus.LevelProgressStore.CompleteSecret("SecretLevel1");
            else if ((int)completionTarget >= 1 && (int)completionTarget <= ProjectD.Menus.GameSceneManager.LevelCount)
                completionRecorded = ProjectD.Menus.LevelProgressStore.CompleteLevel((int)completionTarget);

            if (!completionRecorded)
                Debug.LogWarning("WinBox reached, but progress was not saved. Check Completion Target and JSON save warnings.", this);
        }
        // Reaching the exit still wins even if saving is unavailable.
        return true;
    }
}
