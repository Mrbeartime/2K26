using UnityEngine;

/// <summary>Controls the flame, embers and warm flickering light of a torch.</summary>
[DisallowMultipleComponent]
public sealed class TorchFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] particles;
    [SerializeField] private Light fireLight;
    [SerializeField] private bool lit = true;
    [SerializeField, Min(0f)] private float intensity = 2f;
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.2f;
    [SerializeField, Min(0f)] private float flickerSpeed = 8f;
    private float seed;

    public bool IsLit => lit;

    private void Awake() => seed = Random.Range(0f, 1000f);
    private void OnEnable() => SetLit(lit);

    public void SetLit(bool value)
    {
        lit = value;
        if (particles != null)
            foreach (var system in particles)
            {
                if (system == null) continue;
                if (lit && isActiveAndEnabled) system.Play(true);
                else system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        if (fireLight != null) fireLight.enabled = lit && isActiveAndEnabled;
    }

    private void Update()
    {
        if (!lit || fireLight == null) return;
        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed) * 2f - 1f;
        fireLight.intensity = intensity * (1f + noise * flickerAmount);
    }

    private void OnDisable()
    {
        if (fireLight != null) fireLight.enabled = false;
        if (particles == null) return;
        foreach (var system in particles)
            if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
