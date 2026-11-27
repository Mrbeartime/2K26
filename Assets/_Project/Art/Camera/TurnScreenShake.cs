using System.Collections;
using UnityEngine;

public class TurnScreenShake : MonoBehaviour
{
    [Header("Screen Shake Settings")]
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.2f;
    [SerializeField, Min(0f)] private float shakeStrength = 0.08f;

    private Coroutine shakeCoroutine;
    private Vector3 originalPosition;
    private bool isShaking;

    public void PlayShake()
    {
        if (!isActiveAndEnabled)
            return;

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            transform.localPosition = originalPosition;
        }

        originalPosition = transform.localPosition;
        shakeCoroutine = StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float progress = elapsed / shakeDuration;
            float strength = shakeStrength * (1f - progress);

            Vector2 offset = Random.insideUnitCircle * strength;

            transform.localPosition = originalPosition +
                new Vector3(offset.x, offset.y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition;
        isShaking = false;
        shakeCoroutine = null;
    }

    private void OnDisable()
    {
        if (isShaking)
            transform.localPosition = originalPosition;

        shakeCoroutine = null;
        isShaking = false;
    }
}