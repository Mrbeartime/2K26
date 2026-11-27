using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EnemyDustVisual : MonoBehaviour
{
    [SerializeField, Range(.2f, 6f)] private float duration = 2f;
    [SerializeField, Range(16, 400)] private int particleCount = 160;
    [SerializeField, Range(.01f, .15f)] private float dustSize = .035f;
    [SerializeField, Range(0f, 1f)] private float driftSpeed = .15f;
    [SerializeField] private Shader dustShader;
    [SerializeField] private bool diagnosticLogs;
    [SerializeField] private Color dustColor = new Color(.32f, .30f, .28f, .9f);

    private bool started, playing;
    private float elapsed;
    private int emitted;
    private ParticleSystem dust;
    private readonly List<Material> materials = new();
    private readonly List<Mesh> bakedMeshes = new();
    private readonly List<Renderer> shells = new();
    private readonly List<Vector3> points = new();
    private MaterialPropertyBlock properties;
    private static readonly int Progress = Shader.PropertyToID("_Progress");

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
    }

    public void Begin()
    {
        if (started || !TryGetComponent<Enemy>(out var enemy) || !enemy.IsDead) return;
        started = true;
        Shader shader = dustShader != null ? dustShader : Resources.Load<Shader>("EnemyDust");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("Enemy Dust: shader missing or unsupported.", this);
            return;
        }

        var root = new GameObject("Enemy Dust");
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        var effect = root.AddComponent<EnemyDustVisual>();
        effect.duration = duration;
        effect.particleCount = particleCount;
        effect.dustSize = dustSize;
        effect.driftSpeed = driftSpeed;
        effect.dustColor = dustColor;
        effect.diagnosticLogs = diagnosticLogs;
        root.layer = gameObject.layer;

        try
        {
            effect.Capture(gameObject, shader);
            effect.playing = true;
            ProjectD.Audio.GameAudioManager.Instance?.PlaySFXAtPosition(
                ProjectD.Audio.AudioCue.EnemyDustDeath, transform.position);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            Destroy(root);
        }
    }

    private void Capture(GameObject source, Shader shader)
    {
        var transforms = new Dictionary<Transform, Transform>();
        var renderers = source.GetComponentsInChildren<Renderer>(true);
        if (diagnosticLogs) Debug.Log($"Enemy Dust capture {source.name}: renderers={renderers.Length}", source);
        foreach (Renderer original in renderers)
        {
            if (!original.enabled || !original.gameObject.activeInHierarchy)
            {
                if (diagnosticLogs) Debug.Log($"Enemy Dust skipped {original.name}: enabled={original.enabled}, active={original.gameObject.activeInHierarchy}", source);
                continue;
            }

            Mesh mesh = null;

            if (original is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh();
                bakedMeshes.Add(mesh);

                // Bake in renderer-local space; the copied transform chain applies scale once.
                skinned.BakeMesh(mesh, true);
                mesh.RecalculateBounds();
            }
            else if (original is MeshRenderer &&
                     original.TryGetComponent<MeshFilter>(out var filter))
            {
                mesh = filter.sharedMesh;
            }

            if (mesh == null || mesh.vertexCount == 0) continue;

            var shell = new GameObject("Dust Surface", typeof(MeshFilter), typeof(MeshRenderer));
            shell.layer = original.gameObject.layer;
            shell.transform.SetParent(CopyTransform(original.transform, transforms), false);

            shell.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shell.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var sourceMaterials = original.sharedMaterials;
            var copies = new Material[sourceMaterials.Length];
            for (int i = 0; i < copies.Length; i++)
            {
                var material = new Material(shader);
                materials.Add(material);
                copies[i] = material;
                var old = sourceMaterials[i];
                material.SetFloat("_Progress", 0f);
                material.SetFloat("_AlphaClip", old != null && old.HasProperty("_AlphaClip") ? old.GetFloat("_AlphaClip") : 0f);
                material.SetFloat("_Cutoff", old != null && old.HasProperty("_Cutoff") ? old.GetFloat("_Cutoff") : .5f);

                if (old != null && old.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", old.GetTexture("_BaseMap"));
                    material.SetTextureScale("_BaseMap", old.GetTextureScale("_BaseMap"));
                    material.SetTextureOffset("_BaseMap", old.GetTextureOffset("_BaseMap"));
                }
                if (old != null && old.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", old.GetColor("_BaseColor"));
            }

            renderer.sharedMaterials = copies;
            shells.Add(renderer);
            if (diagnosticLogs)
                Debug.Log($"Enemy Dust surface {original.name}: type={original.GetType().Name}, readable={mesh.isReadable}, vertices={mesh.vertexCount}, source={original.bounds}, snapshot={renderer.bounds}, sourceScale={original.transform.lossyScale}, snapshotScale={shell.transform.lossyScale}, materials={copies.Length}, enabled={renderer.enabled}", renderer);

            if (mesh.isReadable)
            {
                var vertices = mesh.vertices;
                int step = Mathf.Max(1, vertices.Length / particleCount);
                for (int i = 0; i < vertices.Length && points.Count < 2048; i += step)
                    points.Add(shell.transform.TransformPoint(vertices[i]));
            }
        }
        if (shells.Count == 0 || points.Count == 0)
            Debug.LogWarning($"Enemy Dust: surfaces={shells.Count}, sampled points={points.Count}. Check enabled renderers and mesh readability.", this);

        dust = gameObject.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = dust.main;
        main.loop = false; main.playOnAwake = false;
        main.maxParticles = particleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = duration * .3f;
        main.startSpeed = 0;
        main.startColor = dustColor;
        // Keep particle size bounded by the captured body, independent of parent scale.
        if (shells.Count > 0)
        {
            Bounds bounds = shells[0].bounds;
            foreach (var shell in shells) bounds.Encapsulate(shell.bounds);
            float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            dustSize = Mathf.Min(dustSize, extent * .04f);
        }
        main.startSize = dustSize;
        var emission = dust.emission; emission.enabled = false;
        var shape = dust.shape; shape.enabled = false;
        var color = dust.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(1, .4f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var dustMaterial = new Material(shader);
        materials.Add(dustMaterial);
        dustMaterial.SetFloat("_Dust", 1);
        dustMaterial.renderQueue = 3000;
        var particleRenderer = dust.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = dustMaterial;
        dust.Play();
        if (diagnosticLogs)
            Debug.Log($"Enemy Dust created: surfaces={shells.Count}, points={points.Count}, particleLimit={particleCount}, size={dustSize}, alpha={dustColor.a}, duration={duration}", this);
    }

    private Transform CopyTransform(Transform source, Dictionary<Transform, Transform> copies)
    {
        if (copies.TryGetValue(source, out var copy)) return copy;
        Transform parent = source.parent != null ? CopyTransform(source.parent, copies) : transform;
        copy = new GameObject("Dust Pose").transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        copies.Add(source, copy);
        return copy;
    }

    private void Update()
    {
        if (!playing) return;
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / (duration * .8f));
        properties.SetFloat(Progress, progress);
        foreach (var shell in shells) if (shell != null) shell.SetPropertyBlock(properties);
        int target = Mathf.FloorToInt(Mathf.Clamp01(elapsed / (duration * .7f)) * particleCount);
        while (emitted < target && points.Count > 0)
        {
            var emit = new ParticleSystem.EmitParams {
                position = points[Random.Range(0, points.Count)],
                velocity = (Random.insideUnitSphere * .4f + Vector3.up * .6f) * driftSpeed,
                startSize = dustSize * Random.Range(.6f, 1f),
                rotation = Random.Range(0, 360)
            };
            dust.Emit(emit, 1);
            emitted++;
        }
        if (elapsed >= duration)
        {
            if (diagnosticLogs) Debug.Log($"Enemy Dust finished: elapsed={elapsed:F3}, emitted={emitted}, alive={dust.particleCount}, surfaces={shells.Count}", this);
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        foreach (var material in materials) if (material != null) Destroy(material);
        foreach (var mesh in bakedMeshes) if (mesh != null) Destroy(mesh);
    }
}
