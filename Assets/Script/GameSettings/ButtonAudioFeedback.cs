using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectD.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonAudioFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private bool clickSound = true;
        [SerializeField] private bool hoverSound = true;
        private Button button;
        private bool levelHover;
        private AudioCue levelHoverCue = AudioCue.Pause;
        public static void BindBellHover(Button target)
        {
            if (target == null) return;
            BindLevelHover(target);
            target.GetComponent<ButtonAudioFeedback>().levelHoverCue = AudioCue.BellHover;
        }
        private bool levelClickBound;
        private bool pointerInside;
        private bool selected;
        private bool pointerPress;
        private int lastHoverFrame = -1;
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
                if (!feedback.levelClickBound)
                {
                    target.onClick.AddListener(feedback.LevelClick);
                    feedback.levelClickBound = true;
                }
            }
        }
        private bool InScope => audioScope != null && transform.IsChildOf(audioScope);
        private void Awake() => button = GetComponent<Button>();
        public void OnPointerEnter(PointerEventData data)
        {
            if (pointerInside) return;
            pointerInside = true;
            Hover();
        }
        private void Hover()
        {
            if (lastHoverFrame == Time.frameCount) return;
            lastHoverFrame = Time.frameCount;
            if (levelHover && button.IsActive() && button.IsInteractable())
            {
                GameAudioManager.TimingEvent(levelHoverCue);
                GameAudioManager.Instance?.PlayLevelHover(levelHoverCue);
                return;
            }
            if (InScope && hoverSound && button.IsActive() && button.IsInteractable())
                GameAudioManager.Instance?.PlayUI(GameAudioManager.TimingEvent(AudioCue.ButtonHover));
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !button.IsActive() || !button.IsInteractable()) return;
            pointerPress = true;
            Click();
        }
        public void OnPointerExit(PointerEventData data) => pointerInside = false;
        public void OnSelect(BaseEventData data)
        {
            if (selected) return;
            selected = true;
            if (!(data is PointerEventData)) Hover();
        }
        public void OnDeselect(BaseEventData data) { selected = false; pointerPress = false; }
        private void OnDisable() { pointerInside = false; selected = false; }
        public void OnSubmit(BaseEventData data) { pointerPress = false; Click(); }
        private void Click()
        {
            // A successful Button callback may have just hidden its own panel.
            if ((InScope || levelHover) && clickSound && button.enabled && button.IsInteractable() && lastClickFrame != Time.frameCount)
            {
                lastClickFrame = Time.frameCount;
                GameAudioManager.Instance?.PlayLevelClick();
            }
        }
        private void LevelClick()
        {
            // The successful Button callback can hide/destroy its UI before this listener runs.
            if ((!levelHover && !InScope) || pointerPress) return;
            Click();
        }
    }
}
