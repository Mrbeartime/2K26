using UnityEngine;

/// <summary>
/// Marks the level exit. Player movement is transform-based, so the turn
/// manager checks collider overlap directly instead of relying on trigger
/// callbacks, which require a Rigidbody to be reliable.
/// </summary>
public class WinBox : MonoBehaviour
{
    public static bool IsReachedBy(PlayerMove player)
    {
        if (player == null) return false;

        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider == null) return false;

        foreach (WinBox winBox in FindObjectsByType<WinBox>(FindObjectsInactive.Include))
        {
            Collider goalCollider = winBox.GetComponent<Collider>();
            if (goalCollider != null && goalCollider.bounds.Intersects(playerCollider.bounds))
                return true;
        }

        return false;
    }
}
