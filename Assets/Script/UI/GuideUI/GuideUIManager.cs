using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

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

    private static readonly string[] PagePrefix = { "goal", "skills", "mecha", "hazards" };
    private ProjectDInputAction _inputAction;
    private VideoPlayer video;
    private GameObject[] pages;
    private int currentPage;

    private void Awake()
    {
        _inputAction = new ProjectDInputAction();
        if (videoPlayer != null) video = videoPlayer.GetComponent<VideoPlayer>();
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