using System;
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

    private void OnEnable()
    {
        if (bookmarkHovered == null) return;
        for (int i = 0; i < bookmarkHovered.Length; i++)
            SetBookmarkHovered(i, false);
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
            ClearContent();
            return;
        }

        GuideEntry entry = entries[index];

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
