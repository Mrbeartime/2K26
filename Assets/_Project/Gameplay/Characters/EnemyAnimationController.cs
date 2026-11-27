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
    private bool hasChargeParameter;
    private float nextChargeCheck;
    private static readonly int ChargeHash = Animator.StringToHash("IsCharging");
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
            hasChargeParameter |= parameter.nameHash == ChargeHash && parameter.type == AnimatorControllerParameterType.Bool;
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
        nextChargeCheck = 0f;
    }

    private void Update()
    {
        if (!configured || !hasChargeParameter || animator == null ||
            !animator.isActiveAndEnabled || Time.timeScale == 0f) return;
        if (enemy.IsDead || deathStarted)
        {
            SetCharging(false);
            return;
        }
        // Limit physics/range queries while still responding during player movement.
        if (Time.time < nextChargeCheck) return;
        nextChargeCheck = Time.time + 0.1f;
        if (enemy.HasChargeTarget()) PlayCharge();
        else StopCharge();
    }

    public void PlayCharge()
    {
        if (!isActiveAndEnabled || !configured || enemy == null || enemy.IsDead || deathStarted) return;
        SetCharging(true);
    }

    public void StopCharge() => SetCharging(false);

    private void SetCharging(bool charging)
    {
        if (animator != null && hasChargeParameter)
            animator.SetBool(ChargeHash, charging);
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
        SetCharging(false);
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
        SetCharging(false);
        StopAllCoroutines();
        // A disabled visual cannot finish playing; do not leave an invisible corpse behind.
        CompleteDeath();
    }
}
