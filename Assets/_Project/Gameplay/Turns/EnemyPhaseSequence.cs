using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectD.Gameplay.Turns
{
    internal static class EnemyPhaseSequence
    {
        internal static IEnumerator Run(Scene scene, TurnSession session, IReadOnlyTurnState turns,
            float enemyPhaseDelay, Action<bool, string> setPhase, Action complete)
        {
            yield return new WaitForSeconds(enemyPhaseDelay);
            // Finish the last player's shot before enemies decide whether they are alive.
            while (!turns.IsGameComplete && HasFlyingArrows(scene)) yield return null;
            if (turns.IsGameComplete) yield break;

            var hud = session.Overlay;
            if (hud != null)
                yield return hud.AnimateTurnProgress(turns);
            if (turns.IsGameComplete) yield break;

            setPhase(true, "Trap Phase: resolving turn " + turns.RoundNumber + "...");
            // Resolve the current round number, not the next one. Waiting on each
            // trap keeps input locked until spikes retract and arrows finish flying.
            foreach (TurnTrap trap in UnityEngine.Object.FindObjectsByType<TurnTrap>())
            {
                if (turns.IsGameComplete) yield break;
                if (trap != null && trap.isActiveAndEnabled && trap.gameObject.scene == scene)
                    yield return trap.ResolveTurn(turns.RoundNumber);   // ส่งเลขเทิร์นแบบนับขึ้นเหมือนเดิม กับดักจะได้ไม่เปลี่ยนพฤติกรรม
            }
            if (turns.IsGameComplete) yield break;
            setPhase(false, null);
            while (!turns.IsGameComplete && HasFlyingArrows(scene)) yield return null;
            if (turns.IsGameComplete) yield break;
            setPhase(false, "Enemy Phase...");
            // React and Patrol are synchronous; the spawned arrows resolve asynchronously.
            Enemy.RunEnemyPhase();
            yield return null;
            while (!turns.IsGameComplete && HasFlyingArrows(scene)) yield return null;
            if (turns.IsGameComplete) yield break;

            setPhase(false, "Player Corpse Cleanup...");
            var effects = PlayerCorpseShatter.BeginCleanup(turns, turns.RoundNumber);
            while (!turns.IsGameComplete && effects.Exists(effect => effect != null && effect.isActiveAndEnabled))
                yield return null;
            if (turns.IsGameComplete) yield break;

            complete();
        }

        private static bool HasFlyingArrows(Scene scene)
        {
            foreach (ArrowProjectile arrow in UnityEngine.Object.FindObjectsByType<ArrowProjectile>())
                if (arrow.isActiveAndEnabled && arrow.gameObject.scene == scene && arrow.IsFlying)
                    return true;
            return false;
        }
    }
}
