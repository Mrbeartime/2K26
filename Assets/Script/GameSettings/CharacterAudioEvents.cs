using UnityEngine;

namespace ProjectD.Audio
{
    [DisallowMultipleComponent]
    public sealed class CharacterAudioEvents : MonoBehaviour
    {
        private Character owner;
        private PlayerMove movement;
        private Animator animator;
        private bool footstepPlaying;
        private static readonly int WalkState = Animator.StringToHash("Walk");
        private int lastFootFrame = -1, lastSlashFrame = -1;
        private void Awake()
        {
            owner = GetComponentInParent<Character>();
            movement = owner != null ? owner.GetComponent<PlayerMove>() : null;
            animator = GetComponent<Animator>();
        }
        private bool Walking()
        {
            if (owner == null || owner.IsDead || !owner.isActiveAndEnabled || movement == null ||
                !movement.isActiveAndEnabled || !movement.IsMoving() || !movement.MovedAlongPathThisFrame ||
                Time.timeScale <= 0 || animator == null || !animator.isActiveAndEnabled || animator.speed <= 0) return false;
            return animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0).shortNameHash == WalkState
                : animator.GetCurrentAnimatorStateInfo(0).shortNameHash == WalkState;
        }
        private void LateUpdate()
        {
            if (footstepPlaying && !Walking()) StopFootstep();
        }
        private void StopFootstep()
        {
            GameAudioManager.Instance?.StopFootstep(this);
            footstepPlaying = false;
        }
        private void OnDisable() { if (footstepPlaying) StopFootstep(); }
        public void AudioFootstep()
        {
            if (!Walking() || lastFootFrame == Time.frameCount) return;
            lastFootFrame = Time.frameCount;
            if (GameAudioManager.Instance != null && GameAudioManager.Instance.PlayFootstep(this, owner.transform.position))
                footstepPlaying = true;
        }
        public void AudioSwordSlash()
        {
            if (owner is not SwordMan || owner.IsDead || Time.timeScale <= 0 || lastSlashFrame == Time.frameCount) return;
            lastSlashFrame = Time.frameCount;
            GameAudioManager.Instance?.PlaySFXAtPosition(AudioCue.SwordSlash, owner.transform.position);
        }
    }
}
