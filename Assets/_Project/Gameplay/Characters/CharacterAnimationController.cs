using ProjectD.Gameplay.Turns;
using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    private PlayerMove playerMove;
    private Character character;
    private bool hasChargeParameter;
    private static readonly int ChargeHash = Animator.StringToHash("IsCharging");
    public Animator CharacterAnimator => animator;

    private static readonly int IsMoving =
        Animator.StringToHash("IsMoving");

    private static readonly int UseSkill =
        Animator.StringToHash("UseSkill");

    private static readonly int InteractHash =
    Animator.StringToHash("Interact");

    private static readonly int DieHash =
    Animator.StringToHash("Die");

    private bool isDead = false;

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        character = GetComponent<Character>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                hasChargeParameter |= parameter.nameHash == ChargeHash &&
                    parameter.type == AnimatorControllerParameterType.Bool;
        if (animator != null && animator.GetComponent<ProjectD.Audio.CharacterAudioEvents>() == null)
            animator.gameObject.AddComponent<ProjectD.Audio.CharacterAudioEvents>();
    }

    private void Update()
    {
        if (animator == null || playerMove == null)
            return;

        // กำลังเดิน = Walk
        // หยุดเดิน = Idle
        bool moving = playerMove.IsMoving();
        animator.SetBool(IsMoving, !isDead && moving);
        var manager = TurnSession.For(gameObject.scene).Turns;
        bool shouldCharge = !isDead && character != null && character.CanUseSkill && !moving &&
            manager != null && manager.CurrentPlayer == playerMove &&
            manager.IsSkillTargeting && manager.CanChooseSkill;
        if (shouldCharge) PlayCharge();
        else StopCharge();
    }

    public void PlayCharge()
    {
        if (!isActiveAndEnabled || isDead || character == null || character.IsDead) return;
        SetCharging(true);
    }

    public void StopCharge() => SetCharging(false);

    private void SetCharging(bool charging)
    {
        if (animator != null && hasChargeParameter)
            animator.SetBool(ChargeHash, charging);
    }

    private void OnDisable() => SetCharging(false);

    public void PlaySkill()
    {
        if (animator == null)
            return;

        SetCharging(false);
        animator.SetTrigger(UseSkill);
    }

    public void PlayInteract()
    {
        if (animator == null)
            return;

        SetCharging(false);
        animator.SetTrigger(InteractHash);
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;
        SetCharging(false);

        if (animator == null)
            return;

        animator.SetBool("IsMoving", false);
        animator.ResetTrigger("UseSkill");
        animator.ResetTrigger("Interact");
        animator.SetTrigger(DieHash);
    }
}
