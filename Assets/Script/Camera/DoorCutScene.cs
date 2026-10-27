using System.Collections;
using UnityEngine;

public class DoorCutScene : MonoBehaviour
{
    [SerializeField] private DoorController door;
    [SerializeField, Min(0f)] private float pauseBeforeClose = 0.3f;
    [SerializeField, Min(0f)] private float pauseAfterClose = 0.4f;

    public IEnumerator Play()
    {
        if (door == null || !door.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("DoorCutScene: assign an active DoorController.", this);
            yield break;
        }

        yield return new WaitForSeconds(pauseBeforeClose);
        if (!isActiveAndEnabled || door == null) yield break;
        door.CloseForCutscene();
        while (isActiveAndEnabled && door != null && !door.IsFullyClosed)
        {
            // A disabled controller cannot animate; finish its closing pose instead.
            if (!door.isActiveAndEnabled) door.CloseForCutscene();
            yield return null;
        }
        if (isActiveAndEnabled && door != null)
            yield return new WaitForSeconds(pauseAfterClose);
    }
}
