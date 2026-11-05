using System.Collections;
using UnityEngine;

/// <summary>Opens the chest once when the level's turn manager reports victory.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class TreasureChestAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private TurnGameManager turnManager;
    [SerializeField] private string openTrigger = "Open";
    [SerializeField] private string openStateName = "Base Layer.Open";
    [Tooltip("Safety timeout if the Animator cannot finish the opening state.")]
    [SerializeField, Min(1f)] private float maximumOpenWait = 30f;
    private bool opened;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void OnEnable()
    {
        if (turnManager == null) turnManager = TurnGameManager.Instance;
        if (turnManager != null) turnManager.GameEnded += OnGameEnded;
    }

    private void OnDisable()
    {
        if (turnManager != null) turnManager.GameEnded -= OnGameEnded;
    }

    private void OnGameEnded(bool won, string reason)
    {
        if (won) PlayOpen();
    }

    [ContextMenu("Play Open (Play Mode)")]
    public void PlayOpen()
    {
        if (!Application.isPlaying || opened || animator == null ||
            animator.runtimeAnimatorController == null) return;
        opened = true;
        animator.SetTrigger(openTrigger);
    }

    public IEnumerator WaitForOpenCompleted()
    {
        if (!isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null) yield break;

        int stateHash = Animator.StringToHash(openStateName);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning("TreasureChestAnimation: opening state not found: " + openStateName, this);
            yield break;
        }

        PlayOpen();
        float elapsed = 0f;
        while (isActiveAndEnabled && animator != null && animator.isActiveAndEnabled)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == stateHash && state.normalizedTime >= 1f &&
                !animator.IsInTransition(0)) yield break;

            if (elapsed >= maximumOpenWait)
            {
                Debug.LogWarning("TreasureChestAnimation: opening timed out; allowing victory UI to continue.", this);
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
