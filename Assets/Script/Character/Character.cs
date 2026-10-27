using UnityEngine;
using System.Collections;
[RequireComponent(typeof(PlayerMove))]
public class Character : Entity
{
    public virtual int SkillLimit => -1;
    [Header("Skill Runtime State")]
    [SerializeField, Tooltip("Runtime usage counter. Do not edit during play.")]
    private int skillsUsed;
    public int SkillsUsed => skillsUsed;
    public int RemainingSkills => SkillLimit < 0 ? -1 : Mathf.Max(0, SkillLimit - SkillsUsed);
    public bool CanUseSkill => !IsDead && (SkillLimit < 0 || RemainingSkills > 0);

    [Header("Death Animation")]
    [SerializeField] private Animator deathAnimator;
    [Header("Corpse")]
    [Tooltip("World-space visual offset after death; does not change the firing lane.")]
    [SerializeField] private Vector3 corpseOffset = new Vector3(0.2f, 0f, 0.2f);
    [SerializeField] private Vector3 fallbackCorpseRotation = new Vector3(0f, 0f, 90f);
    public bool IsFreshCorpse { get; private set; }
    public Bounds CorpseSightBounds { get; private set; }
    public void ExpireCorpseSight() => IsFreshCorpse = false;

    protected void RecordSkillUse()
    {
        skillsUsed++;
        Debug.Log(name + " skill used. Remaining: " + RemainingSkills + " / " + SkillLimit, this);
    }

    private void Awake() => skillsUsed = 0;
    [Header("Movement Range")]
    [Tooltip("Maximum number of tiles per move command, including detours around walls.")]
    [SerializeField, Min(0)] private int movementRange = 3;
    public int MovementRange => Mathf.Max(0, movementRange);

    public virtual void Interact(Tile tile)
    {
        if (tile == null) return;
        foreach (Chest chest in FindObjectsByType<Chest>())
            if (GridManager.Instance.WorldToGrid(chest.transform.position) == tile.gridPosition) OPChest(chest);
    }
    public void OPChest(Chest chest) => chest.Open(this);
    public void OPSwitch(FloorSwitch item) => item.Activate(this);

    public bool CanReact(Tile tile)
    {
        if (tile == null || GridManager.Instance == null) return false;
        foreach (DoorSwitch item in FindObjectsByType<DoorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.CanActivate)
                return true;
        foreach (FloorSwitch item in FindObjectsByType<FloorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.CanActivate)
                return true;
        return false;
    }

    public bool React(Tile tile)
    {
        if (tile == null || GridManager.Instance == null) return false;
        foreach (DoorSwitch item in FindObjectsByType<DoorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.TryActivate(this))
                return true;
        foreach (FloorSwitch item in FindObjectsByType<FloorSwitch>())
            if (GridManager.Instance.WorldToGrid(item.transform.position) == tile.gridPosition && item.TryActivate(this))
                return true;
        return false;
    }

    protected override void RemoveAfterDeath()
    {
        Bounds bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one * 0.5f);
        bool found = false;
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col.enabled)
            {
                if (!found) bounds = col.bounds;
                else bounds.Encapsulate(col.bounds);
                found = true;
            }
            col.enabled = false;
        }
        CorpseSightBounds = bounds;
        IsFreshCorpse = true;
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        PlayerMove movement = GetComponent<PlayerMove>();
        if (movement != null) { movement.StopAllCoroutines(); movement.enabled = false; }
        if (TryGetComponent(out CharacterFacing facing)) facing.enabled = false;
        if (TryGetComponent(out CharacterAnimationController animationController)) animationController.enabled = false;
        PlayDeathVisuals();
        PlayerCorpseShatter shatter = GetComponent<PlayerCorpseShatter>();
        if (shatter == null) shatter = gameObject.AddComponent<PlayerCorpseShatter>();
        shatter.RegisterDeath(TurnGameManager.Instance);
    }

    protected virtual void PlayDeathVisuals()
    {
        transform.position += corpseOffset;

        // หา Animator
        if (deathAnimator == null)
            deathAnimator = GetComponentInChildren<Animator>();

        // Use a resting pose when there is no death animation.
        if (deathAnimator == null)
        {
            transform.rotation *= Quaternion.Euler(fallbackCorpseRotation);
            return;
        }

        // ต้องมี State ชื่อ Death
        int deathState = Animator.StringToHash("Base Layer.Death");

        if (!deathAnimator.HasState(0, deathState))
        {
            Debug.LogWarning("ไม่พบ Death State ใน Animator", this);
            deathAnimator.enabled = false;
            transform.rotation *= Quaternion.Euler(fallbackCorpseRotation);
            return;
        }

        StartCoroutine(PlayDeathThenKeepCorpse());
    }

    private IEnumerator PlayDeathThenKeepCorpse()
    {
        // ให้ Animation เดินต่อได้แม้ Time.timeScale = 0
        deathAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // หยุดสถานะเดิน
        deathAnimator.SetBool("IsMoving", false);

        // เล่น Death Animation จากจุดเริ่มต้นทันที
        deathAnimator.Play("Death", 0, 0f);

        yield return null;

        // รอจน Animation เล่นครบ 1 รอบ
        // มี Timeout ป้องกันการรอค้างหาก Animator ผิดปกติ
        float timeout = 15f;

        while (deathAnimator != null && timeout > 0f)
        {
            AnimatorStateInfo state =
                deathAnimator.GetCurrentAnimatorStateInfo(0);

            if (state.IsName("Base Layer.Death") &&
                state.normalizedTime >= 1f &&
                !deathAnimator.IsInTransition(0))
            {
                break;
            }

            timeout -= Time.unscaledDeltaTime;

            yield return null;
        }

        // Preserve the final pose as a non-blocking corpse.
        if (deathAnimator != null) deathAnimator.enabled = false;
    }

    public virtual void ShowSkillRange()
    {
    }
    public virtual void Attack(Tile target) { }
}

