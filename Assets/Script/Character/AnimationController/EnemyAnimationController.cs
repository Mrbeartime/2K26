using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public class EnemyAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private Enemy enemy;
    private bool configured;
    private bool deathStarted;
    private Action deathFinished;
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeadHash = Animator.StringToHash("IsDead");
    private static readonly int DeathStateHash = Animator.StringToHash("Base Layer.Death");

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        bool attack = false;
        bool death = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            attack |= parameter.nameHash == AttackHash && parameter.type == AnimatorControllerParameterType.Trigger;
            death |= parameter.nameHash == DeadHash && parameter.type == AnimatorControllerParameterType.Bool;
        }
        configured = attack && death && animator.HasState(0, DeathStateHash);
        if (!configured)
        {
            Debug.LogWarning("Enemy Animator needs Attack (Trigger), IsDead (Bool), and Base Layer.Death.", this);
            return;
        }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void OnEnable()
    {
        enemy.onAttack.AddListener(PlayAttack);
    }

    public void PlayAttack()
    {
        if (configured && !enemy.IsDead && !deathStarted && animator != null && animator.isActiveAndEnabled)
            animator.SetTrigger(AttackHash);
    }

    // Enemy retains ownership of removal; this component only reports visual completion.
    public bool TryPlayDeath(Action onFinished)
    {
        if (!configured || !isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled || deathStarted)
            return false;

        deathStarted = true;
        deathFinished = onFinished;
        animator.ResetTrigger(AttackHash);
        animator.SetBool(DeadHash, true);
        StartCoroutine(WaitForDeath());
        return true;
    }

    private IEnumerator WaitForDeath()
    {
        float entryWait = 0f;
        while (animator != null && animator.isActiveAndEnabled)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == DeathStateHash)
            {
                if (state.normalizedTime >= 1f && !animator.IsInTransition(0)) break;
            }
            else
            {
                entryWait += Time.deltaTime;
                if (entryWait >= 2f) break;
            }
            yield return null;
        }
        CompleteDeath();
    }

    private void CompleteDeath()
    {
        Action callback = deathFinished;
        deathFinished = null;
        callback?.Invoke();
    }

    private void OnDisable()
    {
        if (enemy != null) enemy.onAttack.RemoveListener(PlayAttack);
        StopAllCoroutines();
        // A disabled visual cannot finish playing; do not leave an invisible corpse behind.
        CompleteDeath();
    }
}
