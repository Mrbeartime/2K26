using System.Collections;
using TMPro;
using UnityEngine;

namespace ProjectD.UI
{
    internal sealed class TurnCounterText
    {
        private readonly MonoBehaviour owner;
        private readonly TMP_Text label;
        private readonly float turnTextStartSize, turnTextEndSize, turnTextSlideDistance;
        private readonly float turnTextOutDuration, turnTextInDuration;
        private Color? turnLimitNormalColor;
        private int? turnTextTarget;
        private Color turnTextTargetColor;
        private float turnTextTargetSize;
        private float turnTextRestX;
        private Coroutine turnTextRoutine;

        internal TurnCounterText(MonoBehaviour owner, TMP_Text label, float startSize, float endSize,
            float slideDistance, float outDuration, float inDuration)
        {
            this.owner = owner;
            this.label = label;
            turnTextStartSize = startSize;
            turnTextEndSize = endSize;
            turnTextSlideDistance = slideDistance;
            turnTextOutDuration = outDuration;
            turnTextInDuration = inDuration;
        }

        internal void FinishAnimation()
        {
            if (StopTurnTextAnimation()) ApplyTurnLimitText();
        }

        internal void Refresh(int turnsLeft, int maximumTurns)
        {
            if (label == null) return;

            Color normalColor = turnLimitNormalColor ??= label.color;
            turnTextTargetColor = normalColor;
            if (turnsLeft < Mathf.CeilToInt(maximumTurns / 4f))
                turnTextTargetColor = Color.red;
            else if (turnsLeft < Mathf.FloorToInt(maximumTurns / 2f))
                turnTextTargetColor = Color.yellow;
            turnTextTargetColor.a = normalColor.a;
            float turnProgress = 1f - (float)turnsLeft / maximumTurns;
            turnTextTargetSize = Mathf.Lerp(turnTextStartSize, turnTextEndSize, turnProgress);

            bool changed = turnTextTarget != turnsLeft;
            if (!changed && turnTextRoutine != null) return;
            bool animate = changed && turnTextTarget.HasValue && owner.isActiveAndEnabled && Application.isPlaying &&
                (turnTextOutDuration > 0f || turnTextInDuration > 0f);
            turnTextTarget = turnsLeft;
            StopTurnTextAnimation();

            if (animate)
            {
                turnTextRestX = label.rectTransform.anchoredPosition.x;
                turnTextRoutine = owner.StartCoroutine(AnimateTurnText());
            }
            else
            {
                ApplyTurnLimitText();
            }
        }

        private IEnumerator AnimateTurnText()
        {
            yield return SlideTurnText(false);
            ApplyTurnLimitText();
            SetTurnTextVisual(-turnTextSlideDistance, 0f);
            yield return SlideTurnText(true);
            ApplyTurnLimitText();
            turnTextRoutine = null;
        }

        private IEnumerator SlideTurnText(bool entering)
        {
            float duration = entering ? turnTextInDuration : turnTextOutDuration;
            float distance = entering ? -turnTextSlideDistance : turnTextSlideDistance;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float opacity = entering ? progress : 1f - progress;
                SetTurnTextVisual(distance * (1f - opacity), opacity);
                yield return null;
            }
            SetTurnTextVisual(entering ? 0f : distance, entering ? 1f : 0f);
        }

        private bool StopTurnTextAnimation()
        {
            if (turnTextRoutine == null) return false;
            owner.StopCoroutine(turnTextRoutine);
            turnTextRoutine = null;
            SetTurnTextVisual(0f, 1f);
            return true;
        }

        private void ApplyTurnLimitText()
        {
            if (label == null) return;
            label.text = turnTextTarget.Value.ToString();
            label.color = turnTextTargetColor;
            label.fontSize = turnTextTargetSize;
        }

        private void SetTurnTextVisual(float offset, float opacity)
        {
            if (label == null) return;
            RectTransform rect = label.rectTransform;
            Vector2 position = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(turnTextRestX + offset, position.y);
            label.alpha = turnTextTargetColor.a * opacity;
        }
    }
}
