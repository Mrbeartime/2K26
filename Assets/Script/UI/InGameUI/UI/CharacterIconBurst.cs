using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterIconBurst : MonoBehaviour
{
    [SerializeField] private Image particleTemplate;
    [SerializeField, Min(1)] private int particleCount = 14;
    [SerializeField, Min(0.01f)] private float duration = 0.4f;
    [SerializeField, Min(0f)] private float spread = 72f;

    private IEnumerator Start()
    {
        if (particleTemplate == null)
        {
            Destroy(gameObject);
            yield break;
        }

        var particles = new (Image image, RectTransform rect, Vector2 offset)[particleCount];
        var random = new System.Random();
        float startAngle = (float)random.NextDouble() * 2f * Mathf.PI;
        float angleStep = 2f * Mathf.PI / particles.Length;
        for (int i = 0; i < particles.Length; i++)
        {
            float angle = startAngle + i * angleStep;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Image image = Instantiate(particleTemplate, transform, false);
            RectTransform rect = image.rectTransform;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            rect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.2f, (float)random.NextDouble());
            image.gameObject.SetActive(true);
            float distance = Mathf.Lerp(spread * 0.5f, spread, (float)random.NextDouble());
            particles[i] = (image, rect, direction * distance);
        }

        Vector2 origin = particleTemplate.rectTransform.anchoredPosition;
        Color color = particleTemplate.color;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = elapsed / duration;
            float travel = Mathf.Sin(progress * Mathf.PI * 0.5f);
            Color faded = color;
            faded.a *= 1f - progress;
            foreach (var particle in particles)
            {
                particle.rect.anchoredPosition = origin + particle.offset * travel;
                particle.image.color = faded;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
