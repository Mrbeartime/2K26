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
    private bool hasTriggered; // เพิ่มตัวแปรป้องกันการสั่งจบเทิร์นซ้ำ
    private float heldTime;

    public void OnPointerDown(PointerEventData eventData)
    {
        isHolding = true;
        hasTriggered = false; // รีเซ็ตสถานะเมื่อเริ่มกดใหม่
        heldTime = 0f;

        HoldStarted?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData) => CancelHold();
    public void OnPointerExit(PointerEventData eventData) => CancelHold();

    private void Update()
    {
        // ถ้าไม่ได้กด หรือ ทำงานจบไปแล้ว(แต่ยังกดค้างอยู่) ให้หยุดอัปเดตเวลา
        if (!isHolding || hasTriggered) return;

        heldTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(heldTime / holdDuration);
        if (fillImage != null) fillImage.fillAmount = t;
        HoldProgress?.Invoke(t);

        if (t >= 1f)
        {
            hasTriggered = true; // ล็อกไว้ไม่ให้รันซ้ำ

            // สั่งจบเทิร์น
            HoldCompleted?.Invoke();

            // หมายเหตุ: เอาคำสั่ง HoldCanceled และการรีเซ็ตค่าออกไปจากตรงนี้
            // เพื่อให้ปุ่มยังคงยุบตัวและหลอดโหลดเต็ม 100% จนกว่าจะปล่อยเมาส์
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

            // ส่งสัญญาณให้ปุ่มค่อยๆ เด้งกลับขึ้นมา เมื่อผู้เล่นยกนิ้วออกจากเมาส์เท่านั้น
            HoldCanceled?.Invoke();
        }
    }

    private void OnDisable() => CancelHold();
}