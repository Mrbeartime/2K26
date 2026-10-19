using UnityEngine;

public class CharacterAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    private PlayerMove playerMove;

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

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        if (animator == null || playerMove == null)
            return;

        // กำลังเดิน = Walk
        // หยุดเดิน = Idle
        animator.SetBool(IsMoving, playerMove.IsMoving());
    }

    public void PlaySkill()
    {
        if (animator == null)
            return;

        animator.SetTrigger(UseSkill);
    }

    public void PlayInteract()
    {
        if (animator == null)
            return;

        animator.SetTrigger(InteractHash);
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;

        if (animator == null)
            return;

        animator.SetBool("IsMoving", false);
        animator.ResetTrigger("UseSkill");
        animator.ResetTrigger("Interact");
        animator.SetTrigger(DieHash);
    }
}