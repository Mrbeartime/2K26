using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCorpseShatter : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float shatterDuration = 2.8f;
    [SerializeField, Range(12, 1500)] private int fragmentCount = 900;
    [SerializeField, Min(0)] private float explosionForce = 0.63f;
    [SerializeField, Min(0)] private float fragmentRotationSpeed = 100f;
    [SerializeField, ColorUsage(true, true)] private Color crystalColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField, InspectorName("Glow Intensity"), Range(0f, 0.6f)] private float emissionIntensity = 0.35f;
    [SerializeField, Min(0.05f)] private float particleLifetime = 0.55f;
    [SerializeField, Min(0)] private float glowFlashIntensity = 0.1f;
    [Tooltip("Optional visual root; empty includes all meshes below this player, including equipment.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("1 = the saved character collider dimensions, centered on the current corpse. Does not enlarge individual fragments.")]
    [SerializeField, Range(0.5f, 1.5f)] private float effectScale = 1f;
    [Tooltip("Fragment size relative to player height. Maximum 20%.")]
    [SerializeField, Range(0.01f, 0.20f)] private float fragmentSize = 0.05f;
    [Tooltip("Maximum drift as a fraction of player height.")]
    [SerializeField, Range(0f, 0.25f)] private float spreadRadius = 0.12f;
    [SerializeField] private bool diagnosticLogs = true;
    private static readonly HashSet<PlayerCorpseShatter> corpses = new();
    private TurnGameManager manager;
    private bool registered;
    public int DeathRound { get; private set; }
    public bool HasShattered { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => corpses.Clear();

    public void RegisterDeath(TurnGameManager source)
    {
        if (registered || source == null || !TryGetComponent(out Character character) || !character.IsDead ||
            !(character is Rogue || character is ArcherMan || character is SwordMan)) return;
        manager = source;
        DeathRound = source.RoundNumber;
        registered = true;
        corpses.Add(this);
    }

    public static bool IsDue(int deathRound, int completedRound) => completedRound >= deathRound + 1;

    public static void CompletePlayerPhase(TurnGameManager source, int completedRound)
    {
        foreach (PlayerCorpseShatter corpse in new List<PlayerCorpseShatter>(corpses))
        {
            if (corpse == null) { corpses.Remove(corpse); continue; }
            if (corpse.manager != source || corpse.HasShattered || !IsDue(corpse.DeathRound, completedRound)) continue;
            corpse.HasShattered = true;
            corpses.Remove(corpse);
            try { corpse.PlayVisual(); }
            catch (System.Exception error) { Debug.LogException(error, corpse); }
        }
    }

    private void PlayVisual()
    {
        CrystalShatterVisual.Create(visualRoot != null ? visualRoot : transform,
            Mathf.Max(0.1f, shatterDuration), Mathf.Clamp(fragmentCount, 12, 1500),
            explosionForce, fragmentRotationSpeed, crystalColor, emissionIntensity,
            particleLifetime, glowFlashIntensity, effectScale, fragmentSize, spreadRadius, diagnosticLogs);
    }

    [ContextMenu("Preview Shatter (Play Mode, dead player only)")]
    private void Preview()
    {
        if (!Application.isPlaying || HasShattered || !TryGetComponent(out Character character) || !character.IsDead) return;
        HasShattered = true;
        corpses.Remove(this);
        PlayVisual();
    }

    private void OnDestroy() => corpses.Remove(this);
}
