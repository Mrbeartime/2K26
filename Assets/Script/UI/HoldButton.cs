using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Min(0.01f)] private float holdDuration = 1f;
    [SerializeField, Min(0f)] private float completedFillDuration;

    [Tooltip("Optional. An Image with Type = Filled, used as a progress ring or bar.")]
    public Image fillImage;

    public event Action HoldCompleted;
    public event Action<float> HoldProgress;

    public event Action HoldStarted;
    public event Action HoldCanceled;

    private enum HoldSource { None, Pointer, Keyboard }

    private HoldSource holdSource;
    private int pointerId;
    private bool hasTriggered;
    private double holdStartedAt;
    private double completedFillUntil;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || holdSource != HoldSource.None) return;
        pointerId = eventData.pointerId;
        BeginHold(HoldSource.Pointer);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) OnPointerExit(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (holdSource == HoldSource.Pointer && pointerId == eventData.pointerId) CancelHold();
    }

    internal void BeginKeyboardHold() => BeginHold(HoldSource.Keyboard);

    internal void CancelKeyboardHold()
    {
        if (holdSource == HoldSource.Keyboard) CancelHold();
    }

    private void BeginHold(HoldSource source)
    {
        if (!isActiveAndEnabled || holdSource != HoldSource.None) return;

        holdSource = source;
        hasTriggered = false;
        holdStartedAt = Time.unscaledTimeAsDouble;
        completedFillUntil = 0d;
        SetProgress(0f);
        HoldStarted?.Invoke();
    }

    private void Update()
    {
        double now = Time.unscaledTimeAsDouble;
        if (completedFillUntil > 0d && now >= completedFillUntil)
        {
            completedFillUntil = 0d;
            SetProgress(0f);
        }

        if (holdSource == HoldSource.None || hasTriggered) return;

        float progress = Mathf.Clamp01((float)(now - holdStartedAt) / Mathf.Max(0.01f, holdDuration));
        SetProgress(progress);
        if (progress < 1f) return;

        hasTriggered = true;
        if (completedFillDuration > 0f) completedFillUntil = now + completedFillDuration;
        HoldCompleted?.Invoke();
    }

    private void SetProgress(float progress)
    {
        if (fillImage != null) fillImage.fillAmount = progress;
        HoldProgress?.Invoke(progress);
    }

    internal void CancelHold()
    {
        if (holdSource == HoldSource.None) return;

        holdSource = HoldSource.None;
        hasTriggered = false;
        if (completedFillUntil == 0d) SetProgress(0f);
        HoldCanceled?.Invoke();
    }

    private void OnDisable()
    {
        completedFillUntil = 0d;
        if (holdSource != HoldSource.None) CancelHold();
        else if (fillImage != null) fillImage.fillAmount = 0f;
    }
}
