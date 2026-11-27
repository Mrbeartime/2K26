using System;
using ProjectD.Audio;
using ProjectD.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GuideUIManager : MonoBehaviour
{
    // One guide topic: title + body text + (optional) video
    [Serializable]
    private class GuideEntry
    {
        public string title;
        public VideoClip video;
    }

    [Header("Pages (order: 0 Goal, 1 Skills, 2 Mecha, 3 Hazards)")]
    [SerializeField] private GameObject GoalPage;
    [SerializeField] private GameObject SkillsPage;
    [SerializeField] private GameObject MechaPage;
    [SerializeField] private GameObject HazardsPage;

    [Header("Topics of each page")]
    [SerializeField] private GuideEntry[] GoalEntries;
    [SerializeField] private GuideEntry[] SkillsEntries;
    [SerializeField] private GuideEntry[] MechaEntries;
    [SerializeField] private GuideEntry[] HazardsEntries;

    [Header("Content display (shared by all pages)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("Needs a VideoPlayer component (Play On Awake off). Hidden when the topic has no video")]
    [SerializeField] private GameObject videoPlayer;

    [SerializeField] private Animator[] bookmarks;
    private Button[] bookmarkButtons;
    private bool[] bookmarkHovered;
    private static readonly int Raised = Animator.StringToHash("raised");

    private static readonly string[] PagePrefix = { "goal", "skills", "mecha", "hazards" };
    private ProjectDInputAction _inputAction;
    private VideoPlayer video;
    private GameObject[] pages;
    private int currentPage;

    private void Awake()
    {
        _inputAction = new ProjectDInputAction();
        SetupBookmarks();
        SetupButtonSounds();
        if (videoPlayer != null) video = videoPlayer.GetComponent<VideoPlayer>();
    }

    // Animator owns movement; this manager forwards hover and page state.
    private void SetupBookmarks()
    {
        int count = bookmarks == null ? 0 : bookmarks.Length;
        bookmarkButtons = new Button[count];
        bookmarkHovered = new bool[count];
        for (int i = 0; i < count; i++)
        {
            if (bookmarks[i] == null) continue;
            int index = i;
            bookmarkButtons[i] = bookmarks[i].GetComponent<Button>();
            var trigger = bookmarks[i].GetComponent<EventTrigger>();
            if (trigger == null) trigger = bookmarks[i].gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => SetBookmarkHovered(index, true));
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => SetBookmarkHovered(index, false));
            trigger.triggers.Add(exit);
        }
    }

    // All Guide buttons use the TurnPages sound (cues GuideTurnPage / GuideTurnPageHover).
    private void SetupButtonSounds()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            ButtonAudioFeedback.BindCustom(button, AudioCue.GuideTurnPageHover, AudioCue.GuideTurnPage);
            if (Array.IndexOf(bookmarkButtons, button) < 0)
                StyleTitleButton(button);
        }
    }

    // Same selected / hover look as the Start menu buttons: faint white highlight behind the button + bold label.
    private static void StyleTitleButton(Button button)
    {
        if (button.targetGraphic is Image image) image.color = Color.white;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.2f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.5f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.35f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        if (button.GetComponent<SelectedButtonBold>() == null)
            button.gameObject.AddComponent<SelectedButtonBold>();
    }

    private bool restoreNavigationEvents;
    private int currentTitle;
    private TMP_Text currentTitleLabel;

    // Keyboard: A/D change bookmark, W/S change title (shown immediately). ESC is handled by the scene/overlay.
    // The EventSystem's own WASD navigation is turned off meanwhile so keys are not handled twice.
    private void Update()
    {
        // The title on the right follows its button's label, which is translated by Unity Localization.
        if (titleText != null && currentTitleLabel != null && !string.IsNullOrEmpty(currentTitleLabel.text) &&
            titleText.text != currentTitleLabel.text)
            titleText.text = currentTitleLabel.text;

        Keyboard k = Keyboard.current;
        if (k == null || pages == null) return;

        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) StepPage(-1);
        else if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) StepPage(1);
        else if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) StepTitle(-1);
        else if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) StepTitle(1);
    }

    private void StepPage(int step)
    {
        int count = pages.Length;
        ChangePage((currentPage + step + count) % count);
        GameAudioManager.Instance?.PlayLevelClick(AudioCue.GuideTurnPage);
        SelectTitle(0);
    }

    private Button[] CurrentTitleButtons()
    {
        GameObject page = pages[currentPage];
        if (page == null) return Array.Empty<Button>();
        return Array.FindAll(page.GetComponentsInChildren<Button>(false), b => b.IsInteractable());
    }

    private int HighlightedTitleIndex(Button[] titles)
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected == null ? -1 : Array.FindIndex(titles, b => b.gameObject == selected);
    }

    private void SelectTitle(int index)
    {
        Button[] titles = CurrentTitleButtons();
        if (titles.Length == 0 || EventSystem.current == null) return;
        Button target = titles[Mathf.Clamp(index, 0, titles.Length - 1)];
        EventSystem.current.SetSelectedGameObject(target.gameObject);
        // Highlighting a title also shows it, so no confirm key is needed.
        int entry = Array.IndexOf(pages[currentPage].GetComponentsInChildren<Button>(false), target);
        if (entry >= 0) ChangeTitle(entry);
    }

    private void StepTitle(int step)
    {
        Button[] titles = CurrentTitleButtons();
        if (titles.Length == 0) return;
        int current = HighlightedTitleIndex(titles);
        // First press only highlights the first title.
        SelectTitle(current < 0 ? 0 : (current + step + titles.Length) % titles.Length);
    }

    // The body text comes from GuideText(_en).txt, so show it again when the language changes.
    private void RefreshLanguage() => ChangeTitle(currentTitle);

    private void OnEnable()
    {
        GameLanguage.Changed += RefreshLanguage;
        if (EventSystem.current != null && EventSystem.current.sendNavigationEvents)
        {
            EventSystem.current.sendNavigationEvents = false;
            restoreNavigationEvents = true;
        }
        if (bookmarkHovered == null) return;
        for (int i = 0; i < bookmarkHovered.Length; i++)
            SetBookmarkHovered(i, false);
        // Start only runs once; reload the current clip when reopening the guide.
        if (pages != null) ChangeTitle(currentTitle);
    }

    private void SetBookmarkHovered(int index, bool value)
    {
        bookmarkHovered[index] = value;
        RefreshBookmark(index);
    }

    private void RefreshBookmark(int index)
    {
        if (bookmarks[index] == null || bookmarks[index].runtimeAnimatorController == null) return;
        bool interactable = bookmarkButtons[index] != null && bookmarkButtons[index].IsInteractable();
        bookmarks[index].SetBool(Raised, interactable && (bookmarkHovered[index] || index == currentPage));
    }

    private void Start()
    {
        pages = new[] { GoalPage, SkillsPage, MechaPage, HazardsPage };
        ChangePage(0);
    }

    private void OnDisable()
    {
        GameLanguage.Changed -= RefreshLanguage;
        if (restoreNavigationEvents && EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
        restoreNavigationEvents = false;
        if (video != null) video.Stop();
    }

    // ---- Page (tab buttons call this: 0 Goal, 1 Skills, 2 Mecha, 3 Hazards) ----
    public void ChangePage(int index)
    {
        if (pages == null || index < 0 || index >= pages.Length) return;

        currentPage = index;
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);

        if (bookmarks != null)
            for (int i = 0; i < bookmarks.Length; i++)
                RefreshBookmark(i);

        ChangeTitle(0);
    }

    // ---- Topic inside the current page (topic buttons call this) ----
    public void ChangeTitle(int index)
    {
        GuideEntry[] entries = GetEntries(currentPage);
        if (entries == null || index < 0 || index >= entries.Length)
        {
            currentTitleLabel = null;
            ClearContent();
            return;
        }

        currentTitle = index;
        GuideEntry entry = entries[index];
        Button[] titleButtons = pages != null && pages[currentPage] != null
            ? pages[currentPage].GetComponentsInChildren<Button>(true)
            : Array.Empty<Button>();
        currentTitleLabel = index < titleButtons.Length ? titleButtons[index].GetComponentInChildren<TMP_Text>(true) : null;

        if (titleText != null) titleText.text = entry.title;
        if (bodyText != null) 
        {
            string key = PagePrefix[currentPage] + (index + 1).ToString("00"); 
            bodyText.text = GuideTextDatabase.Get(key) ?? "";
        }
        LoadVideo(entry.video);
    }

    private GuideEntry[] GetEntries(int page) => page switch
    {
        0 => GoalEntries,
        1 => SkillsEntries,
        2 => MechaEntries,
        3 => HazardsEntries,
        _ => null
    };

    private void ClearContent()
    {
        if (titleText != null) titleText.text = "";
        if (bodyText != null) bodyText.text = "";
        LoadVideo(null);
    }

    // Plays the clip in videoPlayer; no clip = stop and hide the player
    private void LoadVideo(VideoClip clip)
    {
        if (video == null) return;

        video.Stop();
        if (clip == null)
        {
            videoPlayer.SetActive(false);
            return;
        }

        videoPlayer.SetActive(true);
        video.clip = clip;
        video.Play();
    }
}
