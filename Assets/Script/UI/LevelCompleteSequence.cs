using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plays the Level Complete result sequence on the LevelCompletePanel:
/// 1. Resolve shows and the turn number counts down from the maximum to the turns left.
/// 2. Every character available at the start of the level appears.
/// 3. A star stamp lands on each character that died (one by one, within a set time).
/// 4. Hold, then PaperSheet fades in while its scale shrinks from large to the authored size.
/// 5. Victory: ClearStamp pops in. Defeat (turns ran out or everyone died): FailStamp pops in instead.
/// Any click or key skips straight to the end of the sequence. It can sit on any active object (LevelCompleteUI passes it the panel to search).
/// </summary>
[DisallowMultipleComponent]
public class LevelCompleteSequence : MonoBehaviour
{
    [Header("Resolve")]
    [Tooltip("The Resolve object. Auto-found by name if empty.")]
    [SerializeField] private GameObject resolveRoot;
    [Tooltip("The TurnLeft text. Auto-found under 'TurnLeft' if empty.")]
    [SerializeField] private TMP_Text turnLeftText;
    [SerializeField] private LevelCompleteCharacterIcons characterIcons;
    [SerializeField] private string turnLeftFormat = "Turn Left : {0}";

    [Header("Paper")]
    [Tooltip("The PaperSheet object. Auto-found by name if empty. Its authored scale is the final scale.")]
    [SerializeField] private RectTransform paperSheet;
    [Tooltip("The ClearStamp object (victory only). Auto-found by name if empty. Its authored scale is the final scale.")]
    [SerializeField] private RectTransform clearStamp;
    [Tooltip("The FailStamp object (defeat only). Auto-found by name if empty. Set it up like ClearStamp: CanvasGroup + Animator with a 'PopIn' state, inactive at the start.")]
    [SerializeField] private RectTransform failStamp;
    [Tooltip("Fade Resolve out while the PaperSheet appears.")]
    [SerializeField] private bool hideResolveWhenPaperShows = true;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0f)] private float turnCountDuration = 1f;
    [SerializeField, Min(0f), Tooltip("Pause after the turn count-down finishes, before the characters show.")]
    private float afterTurnCountHold = 1f;
    [SerializeField, Min(0f), Tooltip("Total time for all star stamps together, split evenly.")]
    private float stampTotalDuration = 1f;
    [SerializeField, Min(0f)] private float holdDuration = 3f;
    [SerializeField, Min(0.01f), Tooltip("Pop-in length. Only used when the object has no Animator (otherwise the clip decides).")]
    private float paperDuration = 0.5f;
    [SerializeField, Min(0f), Tooltip("Seconds after the PaperSheet has appeared before the ClearStamp pops in (victory only).")]
    private float clearStampDelay = 1f;
    [SerializeField, Min(1f), Tooltip("Start scale multiple. Only used when the object has no Animator (otherwise the clip decides).")]
    private float paperStartScale = 5f;
    [SerializeField, Min(0f), Tooltip("Ignore skip input briefly so the last gameplay input can't skip it.")]
    private float skipInputDelay = 0.3f;
    [SerializeField, Min(0f), Tooltip("Fade-in of the whole result panel before the sequence starts.")]
    private float panelFadeDuration = 0.5f;

    private CanvasGroup panelGroup;
    private Transform searchRoot;
    private Transform SearchRoot => searchRoot != null ? searchRoot : transform;
    private Coroutine routine;
    private bool skipRequested;
    private Vector3 failStampScale = Vector3.one;
    private float startedAt;
    private Vector3 paperScale = Vector3.one;
    private bool paperScaleCached;
    private Vector3 stampScale = Vector3.one;
    private const string PopInState = "PopIn";

    /// <summary>True when nothing is playing (the whole sequence, including the ClearStamp, has finished).</summary>
    public bool IsFinished => routine == null;

    /// <summary>Starts the sequence. Call after the panel has been activated.</summary>
    public void Play(int maximumTurns, int turnsLeft, bool won, Transform panelRoot = null)
    {
        if (routine != null) StopCoroutine(routine);
        searchRoot = panelRoot;
        panelGroup = panelRoot != null ? GetGroup(panelRoot.gameObject) : null;
        if (panelGroup != null) panelGroup.alpha = 0f;   // hidden before the first frame is drawn
        ResolveReferences();
        routine = StartCoroutine(Run(Mathf.Max(0, maximumTurns), Mathf.Max(0, turnsLeft), won));
    }

    private void Update()
    {
        if (routine == null || skipRequested) return;
        if (Time.unscaledTime - startedAt < skipInputDelay) return;
        if (AnyInputPressed()) skipRequested = true;
    }

    private IEnumerator Run(int maximumTurns, int turnsLeft, bool won)
    {
        skipRequested = false;
        startedAt = Time.unscaledTime;

        CachePaperScale();
        if (paperSheet != null) paperSheet.gameObject.SetActive(false);
        if (clearStamp != null) clearStamp.gameObject.SetActive(false);
        if (failStamp != null) failStamp.gameObject.SetActive(false);
        if (resolveRoot != null)
        {
            resolveRoot.SetActive(true);
            GetGroup(resolveRoot).alpha = 1f;
        }

        if (characterIcons != null)
        {
            characterIcons.Prepare();
            characterIcons.SetVisible(false);
        }
        SetTurnText(maximumTurns);

        // 1) The whole panel fades in first.
        if (panelGroup != null)
        {
            for (float t = 0f; t < panelFadeDuration; t += Time.unscaledDeltaTime)
            {
                panelGroup.alpha = t / panelFadeDuration;
                yield return null;
            }
            panelGroup.alpha = 1f;
        }

        // 2) Turn counter counts down from the maximum to the turns left.
        for (float t = 0f; t < turnCountDuration && !skipRequested; t += Time.unscaledDeltaTime)
        {
            SetTurnText(Mathf.RoundToInt(Mathf.Lerp(maximumTurns, turnsLeft, t / turnCountDuration)));
            yield return null;
        }
        SetTurnText(turnsLeft);

        // Short pause once the count-down has finished.
        for (float t = 0f; t < afterTurnCountHold && !skipRequested; t += Time.unscaledDeltaTime)
            yield return null;

        // 3) All characters available at the start of the level.
        if (characterIcons != null)
        {
            characterIcons.SetVisible(true);
            yield return characterIcons.WaitUntilShown(() => skipRequested);
            if (skipRequested) characterIcons.SetVisible(true, true);
        }

        // 4) Stamp the ones that died, one by one.
        List<int> dead = characterIcons != null ? characterIcons.FindDeadSlots() : new List<int>();
        if (dead.Count > 0 && !skipRequested)
        {
            float slotTime = stampTotalDuration / dead.Count;
            foreach (int index in dead)
            {
                for (float t = 0f; t < slotTime && !skipRequested; t += Time.unscaledDeltaTime)
                {
                    characterIcons.SetStamp(index, t / slotTime);
                    yield return null;
                }
                characterIcons.SetStamp(index, 1f);
                if (skipRequested) break;
            }
        }
        // Make the final state correct even when skipped.
        foreach (int index in dead) characterIcons.SetStamp(index, 1f);

        // 5) Hold.
        for (float t = 0f; t < holdDuration && !skipRequested; t += Time.unscaledDeltaTime)
            yield return null;

        // 6) PaperSheet: opacity 0 -> 1 and scale from large down to the authored scale.
        if (paperSheet != null)
        {
            var resolveGroup = resolveRoot != null ? GetGroup(resolveRoot) : null;
            yield return PopIn(paperSheet, paperScale, p =>
            {
                if (hideResolveWhenPaperShows && resolveGroup != null) resolveGroup.alpha = 1f - p;
            });
        }
        if (hideResolveWhenPaperShows && resolveRoot != null) resolveRoot.SetActive(false);

        // 7) The stamp pops in the same way, a moment after the paper: Clear on victory, Fail on defeat.
        var stamp = won ? clearStamp : failStamp;
        if (stamp != null)
        {
            for (float t = 0f; t < clearStampDelay && !skipRequested; t += Time.unscaledDeltaTime)
                yield return null;
            yield return PopIn(stamp, won ? stampScale : failStampScale, null);
        }
        routine = null;
    }

    /// <summary>
    /// Opacity 0 -> 1 while the scale shrinks to the final scale. Plays the object's Animator 'PopIn' state
    /// when it has one; otherwise falls back to the same animation in code.
    /// </summary>
    private IEnumerator PopIn(RectTransform target, Vector3 finalScale, System.Action<float> onProgress)
    {
        var group = GetGroup(target.gameObject);

        if (target.TryGetComponent(out Animator animator) && animator.runtimeAnimatorController != null)
        {
            target.gameObject.SetActive(true);
            animator.Play(PopInState, 0, 0f);
            animator.Update(0f);
            while (true)
            {
                if (skipRequested)
                {
                    animator.Play(PopInState, 0, 1f);   // jump to the final frame
                    animator.Update(0f);
                    break;
                }
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(PopInState) || info.normalizedTime >= 1f) break;
                onProgress?.Invoke(group.alpha);
                yield return null;
            }
            onProgress?.Invoke(1f);
            yield break;
        }

        Vector3 startScale = finalScale * paperStartScale;

        group.alpha = 0f;
        target.localScale = startScale;
        target.gameObject.SetActive(true);

        for (float t = 0f; t < paperDuration && !skipRequested; t += Time.unscaledDeltaTime)
        {
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / paperDuration), 3f);
            group.alpha = p;
            target.localScale = Vector3.LerpUnclamped(startScale, finalScale, p);
            onProgress?.Invoke(p);
            yield return null;
        }
        group.alpha = 1f;
        target.localScale = finalScale;
        onProgress?.Invoke(1f);
    }

    private void SetTurnText(int value)
    {
        if (turnLeftText != null) turnLeftText.text = string.Format(turnLeftFormat, value);
    }

    private void CachePaperScale()
    {
        if (paperScaleCached) return;
        if (paperSheet != null) paperScale = paperSheet.localScale;   // the scale authored in the editor
        if (clearStamp != null) stampScale = clearStamp.localScale;
        if (failStamp != null) failStampScale = failStamp.localScale;
        paperScaleCached = true;
    }

    private void ResolveReferences()
    {
        if (resolveRoot == null) resolveRoot = FindChild("Resolve")?.gameObject;
        if (paperSheet == null) paperSheet = FindChild("PaperSheet") as RectTransform;
        if (clearStamp == null) clearStamp = FindChild("ClearStamp") as RectTransform;
        if (failStamp == null) failStamp = FindChild("FailStamp") as RectTransform;
        if (turnLeftText == null)
        {
            Transform turnLeft = FindChild("TurnLeft");
            if (turnLeft != null) turnLeftText = turnLeft.GetComponentInChildren<TMP_Text>(true);
        }
        // Only the Resolve row; the PaperSheet has its own icon row that this sequence must not touch.
        if (characterIcons == null && resolveRoot != null)
            characterIcons = resolveRoot.GetComponentInChildren<LevelCompleteCharacterIcons>(true);
    }

    private Transform FindChild(string childName)
    {
        foreach (var child in SearchRoot.GetComponentsInChildren<Transform>(true))
            if (child.name == childName) return child;
        return null;
    }

    private static CanvasGroup GetGroup(GameObject target)
    {
        if (!target.TryGetComponent(out CanvasGroup group)) group = target.AddComponent<CanvasGroup>();
        return group;
    }

    /// <summary>Any key, mouse button, gamepad button or touch pressed this frame.</summary>
    public static bool AnyInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
        var gamepad = Gamepad.current;
        if (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)) return true;
        return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
    }
}
