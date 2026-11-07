using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectD.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonAudioFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private bool clickSound = true;
        [SerializeField] private bool hoverSound = true;
        private Button button;
        private bool levelHover;
        private bool levelClickBound;
        private bool pointerInside;
        public static void BindLevelHover(Button target, bool enabled = true)
        {
            if (target == null) return;
            var feedback = target.GetComponent<ButtonAudioFeedback>();
            if (feedback == null) feedback = target.gameObject.AddComponent<ButtonAudioFeedback>();
            feedback.levelHover = enabled;
            if (!feedback.levelClickBound)
            {
                target.onClick.AddListener(feedback.LevelClick);
                feedback.levelClickBound = true;
            }
        }
        private Transform audioScope;
        private int lastClickFrame = -1;
        public static void BindMenu(GameObject root)
        {
            if (root == null) return;
            foreach (var target in root.GetComponentsInChildren<Button>(true))
            {
                var feedback = target.GetComponent<ButtonAudioFeedback>();
                if (feedback == null) feedback = target.gameObject.AddComponent<ButtonAudioFeedback>();
                feedback.audioScope = root.transform;
            }
        }
        private bool InScope => audioScope != null && transform.IsChildOf(audioScope);
        private void Awake() => button = GetComponent<Button>();
        public void OnPointerEnter(PointerEventData data)
        {
            if (pointerInside) return;
            pointerInside = true;
            if (levelHover && button.IsActive() && button.IsInteractable())
            {
                GameAudioManager.Instance?.PlayLevelHover();
                return;
            }
            if (InScope && hoverSound && button.IsActive() && button.IsInteractable())
                GameAudioManager.Instance?.PlayUI(AudioCue.ButtonHover);
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Click();
        }
        public void OnPointerExit(PointerEventData data) => pointerInside = false;
        private void OnDisable() => pointerInside = false;
        public void OnSubmit(BaseEventData data) => Click();
        private void Click()
        {
            if (levelHover) return; // Level buttons use Button.onClick, after its interactability check.
            // A successful Button callback may have just hidden its own panel.
            if (InScope && clickSound && button.enabled && button.IsInteractable() && lastClickFrame != Time.frameCount)
            {
                lastClickFrame = Time.frameCount;
                GameAudioManager.Instance?.PlayUI(AudioCue.ButtonClick);
            }
        }
        private void LevelClick()
        {
            // The successful Button callback can hide/destroy its UI before this listener runs.
            if (!levelHover || !clickSound || lastClickFrame == Time.frameCount) return;
            lastClickFrame = Time.frameCount;
            GameAudioManager.Instance?.PlayLevelClick();
        }
    }
}
