using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float holdDuration = 1f;

    [Tooltip("Optional. An Image with Type = Filled, used as a progress ring or bar.")]
    public Image fillImage;

    public event Action HoldCompleted;
    public event Action<float> HoldProgress;

    public event Action HoldStarted;
    public event Action HoldCanceled;

    private bool isHolding;
    private bool hasTriggered;
    private float heldTime;

    public void OnPointerDown(PointerEventData eventData)
    {
        isHolding = true;
        hasTriggered = false;
        heldTime = 0f;

        HoldStarted?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData) => CancelHold();
    public void OnPointerExit(PointerEventData eventData) => CancelHold();

    private void Update()
    {
        if (!isHolding || hasTriggered) return;

        heldTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(heldTime / holdDuration);
        if (fillImage != null) fillImage.fillAmount = t;
        HoldProgress?.Invoke(t);

        if (t >= 1f)
        {
            hasTriggered = true;

            HoldCompleted?.Invoke();

        }
    }

    private void CancelHold()
    {
        if (isHolding)
        {
            isHolding = false;
            hasTriggered = false;
            heldTime = 0f;
            if (fillImage != null) fillImage.fillAmount = 0f;
            HoldProgress?.Invoke(0f);

            HoldCanceled?.Invoke();
        }
    }

    private void OnDisable() => CancelHold();
}