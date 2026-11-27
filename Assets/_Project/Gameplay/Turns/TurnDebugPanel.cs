using UnityEngine;

namespace ProjectD.Gameplay.Turns
{
    internal static class TurnDebugPanel
    {

        internal static bool HitTest(IReadOnlyTurnState state, Vector2 screenPosition)
        {
            Vector2 guiPosition = new(screenPosition.x, Screen.height - screenPosition.y);

            if (state.IsGameComplete)
            {
                float x = (Screen.width - 360f) * 0.5f;
                float y = (Screen.height - 180f) * 0.5f;
                return new Rect(x + 105f, y + 120f, 150f, 36f).Contains(guiPosition);
            }

            // The panel background and labels are intentionally click-through so they
            // never block map tiles. Only actionable buttons capture a map click.
            if (state.IsMoveTargeting || state.IsSkillTargeting)
                return new Rect(30f, 126f, 180f, 28f).Contains(guiPosition);

            bool canAct = state.CanSelectAction;
            if (canAct && state.CanChooseMove && new Rect(30f, 126f, 100f, 28f).Contains(guiPosition)) return true;
            if (canAct && new Rect(140f, 126f, 100f, 28f).Contains(guiPosition)) return true;
            if (state.CanChooseReact && new Rect(250f, 126f, 100f, 28f).Contains(guiPosition)) return true;
            if (canAct && new Rect(360f, 126f, 100f, 28f).Contains(guiPosition)) return true;
            return state.CanEndTurn && !state.IsMoveTargeting && !state.IsSkillTargeting &&
                new Rect(470f, 126f, 100f, 28f).Contains(guiPosition);
        }

        internal static void Draw(ITurnController state)
        {
            const float width = 570f;
            GUI.Box(new Rect(16, 16, width, 180), "TURN MANAGER");
            GUI.Label(new Rect(Screen.width - 230f, 16f, 210f, 32f),
                "Turns Left: " + Mathf.Max(0, state.TurnsLeft),
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight
                });
            GUI.Label(new Rect(30, 48, 400, 24), "Turn " + state.RoundNumber + " / " + state.MaximumTurns);
            GUI.Label(new Rect(30, 72, 400, 24), state.IsTrapPhase ? "Active: Traps" : state.IsEnemyPhase ? "Active: Enemy" : "Active: " + (state.CurrentPlayer == null ? "Choose a Player" : state.CurrentPlayer.name));
            GUI.Label(new Rect(30, 96, 410, 24), state.Status ?? "Preparing...");

            if (state.IsMoveTargeting)
            {
                if (GUI.Button(new Rect(30, 126, 180, 28), "Cancel Move")) state.CancelMoveTargeting();
            }
            else if (state.IsSkillTargeting)
            {
                if (GUI.Button(new Rect(30, 126, 180, 28), "Cancel Skill")) state.CancelSkill();
            }
            else
            {
                GUI.enabled = state.CanSelectAction && state.CanChooseMove;
                if (GUI.Button(new Rect(30, 126, 100, 28), "Move")) state.ChooseMove();

                GUI.enabled = state.CanSelectAction;
                bool actionEnabled = GUI.enabled;
                GUI.enabled = actionEnabled && state.CanChooseSkill;
                Character skillUser = state.CurrentPlayer != null ? state.CurrentPlayer.GetComponent<Character>() : null;
                string skillLabel = skillUser != null && skillUser.SkillLimit >= 0
                    ? "Skill (" + skillUser.RemainingSkills + ")" : "Skill";
                if (GUI.Button(new Rect(140, 126, 100, 28), skillLabel)) state.ChooseSkill();
                GUI.enabled = actionEnabled;
                if (state.CanChooseReact && GUI.Button(new Rect(250, 126, 100, 28), "React")) state.ChooseReact();
                if (GUI.Button(new Rect(360, 126, 100, 28), "Skip")) state.ChooseSkip();

                GUI.enabled = state.CanEndTurn && !state.IsMoveTargeting && !state.IsSkillTargeting;
                if (GUI.Button(new Rect(470, 126, 100, 28), "End Turn")) state.ChooseEndTurn();
            }
            GUI.enabled = true;

            if (!state.IsGameComplete) return;
            float x = (Screen.width - 360f) * 0.5f;
            float y = (Screen.height - 180f) * 0.5f;
            GUI.Box(new Rect(x, y, 360f, 180f), GUIContent.none);
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(x, y + 22f, 360f, 50f), state.PlayerWon ? "VICTORY!" : "GAME OVER", titleStyle);
            GUI.Label(new Rect(x + 20f, y + 75f, 320f, 28f), state.GameOverReason, new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            });
            if (GUI.Button(new Rect(x + 105f, y + 120f, 150f, 36f), "Restart")) state.RestartCurrentScene();
        }
    }
}
