using UnityEngine;

namespace ProjectD.Data
{
    [CreateAssetMenu(menuName = "ProjectD/Levels/Turn Rules")]
    public sealed class LevelTurnRules : ScriptableObject
    {
        [SerializeField, Min(1)] private int turnLimit = 7;
        [SerializeField, Min(0f)] private float phaseDelay = 0.35f;
        public int TurnLimit => Mathf.Max(1, turnLimit);
        public float PhaseDelay => Mathf.Max(0f, phaseDelay);
    }
}
