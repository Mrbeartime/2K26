using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Visual-only ribbon sampled from the animated sword, never applies damage.</summary>
[DisallowMultipleComponent]
public sealed class SwordSlashTrail : MonoBehaviour
{
    [SerializeField] private Transform swordTip;
    [SerializeField] private Transform swordBase;
    [SerializeField, InspectorName("Sword Neon Color"), ColorUsage(true, true)] private Color trailColor = new Color(1f, 0f, 0f, .85f);
    [SerializeField, InspectorName("Sword Emission Intensity (HDR)"), Range(0f, 16f)] private float emissionIntensity = 6f;
    [SerializeField, Range(.05f, 1.5f)] private float trailWidth = .8f;
    [SerializeField, Min(.01f)] private float trailLifetime = .12f;
    [SerializeField, Min(.01f)] private float trailFadeDuration = .25f;
    [SerializeField, Range(0f, 2f)] private float glowIntensity = .4f;
    [Tooltip("Normalized Skill time. Defaults are measured from WarSkillAttack's fastest swing; refine in animation preview.")]
    [SerializeField, Range(0f, 1f)] private float slashStart = .11297071f;
    [SerializeField, Range(0f, 1f)] private float slashEnd = .14225942f;
    [SerializeField, Min(0f)] private float minimumSwingSpeed = .5f;
    [SerializeField, Range(0, 16)] private int sparkCount;
    private struct Sample { public Vector3 root, tip; public float time; public int stroke; }
    private readonly List<Sample> samples = new(130);
    private readonly List<Vector3> vertices = new(256);
    private readonly List<Color> colors = new(256);
    private readonly List<Vector2> uvs = new(256);
    private readonly List<int> indices = new(762);
    private Animator animator;
    private SwordMan owner;
    private GameObject effect;
    private Mesh mesh;
    private Material material;
    private ParticleSystem sparks;
    private Mesh sparkMesh;
    private Vector3 lastTip;
    private Vector3 lastBase;
    private float lastNormalized;
    private bool tracking, emitting;
    private int stroke;
    private static readonly int Skill = Animator.StringToHash("Skill");
    private static readonly int GlowProperty = Shader.PropertyToID("_Glow");
    private static readonly int EmissionProperty = Shader.PropertyToID("_Emission");

    private void Awake()
    {
        owner = GetComponent<SwordMan>();
        animator = GetComponentInChildren<Animator>();
        if (owner == null) { enabled = false; return; }
        ResolveMarkers();
        Shader shader = Resources.Load<Shader>("RedSwordTrail");
        if (shader == null) { Debug.LogWarning("RedSwordTrail shader missing", this); enabled = false; return; }
        effect = new GameObject("Red Sword Trail");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
        effect.layer = gameObject.layer;
        mesh = new Mesh { name = "Animated Sword Ribbon" };
        mesh.MarkDynamic();
        effect.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = effect.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        material = new Material(shader);
        renderer.sharedMaterial = material;
        var sparkObject = new GameObject("Sword Sparks");
        sparkObject.layer = effect.layer;
        sparkObject.transform.SetParent(effect.transform, false);
        sparks = sparkObject.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false; main.loop = false; main.maxParticles = 32;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = .2f; main.startSpeed = 0f;
        var emission = sparks.emission; emission.enabled = false;
        var shape = sparks.shape; shape.enabled = false;
        sparkMesh = new Mesh { name = "Sword Spark Triangle" };
        sparkMesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(0, .5f, 0) };
        sparkMesh.uv = new[] { new Vector2(.2f, 0), new Vector2(.8f, 0), new Vector2(.5f, 1) };
        sparkMesh.colors = new[] { Color.white, Color.white, Color.white };
        sparkMesh.triangles = new[] { 0, 1, 2 };
        var sparkRenderer = sparkObject.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        sparkRenderer.mesh = sparkMesh; sparkRenderer.sharedMaterial = material;
        var fade = sparks.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
    }

    // The prefab may supply explicit markers. Otherwise use the baked blade and its deform bone.
    private void ResolveMarkers()
    {
        if (swordTip != null && swordBase != null) return;
        Transform bone = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "DEF-sword.R") { bone = child; break; }
        if (bone == null) return;
        foreach (SkinnedMeshRenderer blade in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (blade.name != "Sword" || blade.sharedMesh == null) continue;
            int boneIndex = System.Array.IndexOf(blade.bones, bone);
            if (boneIndex < 0 || boneIndex >= blade.sharedMesh.bindposes.Length) continue;
            // Bind pose converts mesh coordinates directly into this deform bone's local space.
            // Baking followed by TransformPoint can double-apply imported rig scale.
            Matrix4x4 bind = blade.sharedMesh.bindposes[boneIndex];
            Bounds bounds = blade.sharedMesh.bounds;
            Vector3 ext = bounds.extents;
            int axis = ext.x > ext.y ? (ext.x > ext.z ? 0 : 2) : (ext.y > ext.z ? 1 : 2);
            Vector3 a = bounds.center, b = bounds.center;
            a[axis] -= ext[axis]; b[axis] += ext[axis];
            a = bone.TransformPoint(bind.MultiplyPoint3x4(a));
            b = bone.TransformPoint(bind.MultiplyPoint3x4(b));
            // The grip is nearest the sword bone origin. Exclude the pommel from the ribbon.
            if ((b - bone.position).sqrMagnitude < (a - bone.position).sqrMagnitude) (a, b) = (b, a);
            if (swordBase == null) swordBase = Marker("Sword Base", bone, Vector3.Lerp(a, b, .25f));
            if (swordTip == null) swordTip = Marker("Sword Tip", bone, b);
            return;
        }
    }

    private static Transform Marker(string label, Transform parent, Vector3 position)
    {
        var marker = new GameObject(label).transform;
        marker.SetParent(parent, false);
        marker.position = position;
        return marker;
    }

    private void LateUpdate()
    {
        if (mesh == null || animator == null || swordTip == null || swordBase == null) return;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool skill = state.shortNameHash == Skill && !animator.IsInTransition(0);
        float normalized = state.normalizedTime;
        float dt = Time.deltaTime;
        float speed = tracking && dt > 0f ? Vector3.Distance(lastTip, swordTip.position) / dt : 0f;
        bool emit = skill && !owner.IsDead && animator.enabled && animator.speed > 0f && dt > 0f &&
            tracking && normalized > lastNormalized && slashEnd > slashStart &&
            normalized >= slashStart && lastNormalized <= slashEnd &&
            speed >= minimumSwingSpeed * Mathf.Abs(animator.speed * state.speed * state.speedMultiplier);
        float delta = Mathf.Max(.000001f, normalized - lastNormalized);
        float endFraction = Mathf.Clamp01((slashEnd - lastNormalized) / delta);
        Vector3 tipNow = Vector3.Lerp(lastTip, swordTip.position, endFraction);
        Vector3 baseNow = Vector3.Lerp(lastBase, swordBase.position, endFraction);
        if (emit && !emitting)
        {
            stroke++;
            float startFraction = Mathf.Clamp01((slashStart - lastNormalized) / delta);
            Vector3 startTip = Vector3.Lerp(lastTip, swordTip.position, startFraction);
            Vector3 startBase = Vector3.Lerp(lastBase, swordBase.position, startFraction);
            samples.Add(new Sample { root = Vector3.Lerp(startTip, startBase, trailWidth),
                tip = startTip, time = Time.time, stroke = stroke });
            for (int i = 0; i < sparkCount; i++)
                sparks.Emit(new ParticleSystem.EmitParams { position = swordTip.position,
                    startColor = trailColor, startSize = Vector3.Distance(swordBase.position, swordTip.position) * .035f,
                    velocity = Random.insideUnitSphere * .3f, startLifetime = .2f }, 1);
        }
        if (emit)
        {
            // World-space samples prevent parent scaling and motion from moving old trail sections.
            samples.Add(new Sample { root = Vector3.Lerp(tipNow, baseNow, trailWidth),
                tip = tipNow, time = Time.time, stroke = stroke });
        }
        emitting = emit;
        tracking = skill;
        lastTip = swordTip.position;
        lastBase = swordBase.position;
        lastNormalized = normalized;
        float life = Mathf.Max(.01f, trailLifetime) + Mathf.Max(.01f, trailFadeDuration);
        int expired = 0;
        while (expired < samples.Count && Time.time - samples[expired].time >= life) expired++;
        if (expired > 0) samples.RemoveRange(0, expired);
        if (samples.Count > 128) samples.RemoveRange(0, samples.Count - 128);
        DrawRibbon();
    }

    private void DrawRibbon()
    {
        if (samples.Count == 0 && mesh.vertexCount == 0) return;
        vertices.Clear(); colors.Clear(); uvs.Clear(); indices.Clear();
        Bounds bounds = samples.Count > 0 ? new Bounds(samples[0].tip, Vector3.zero) : new Bounds();
        for (int i = 0; i < samples.Count; i++)
        {
            Sample sample = samples[i];
            float fade = 1f - Mathf.Clamp01((Time.time - sample.time - trailLifetime) / Mathf.Max(.01f, trailFadeDuration));
            float taper = i == 0 || i == samples.Count - 1 ? .15f : 1f;
            Vector3 center = (sample.root + sample.tip) * .5f;
            vertices.Add(Vector3.Lerp(center, sample.root, taper));
            vertices.Add(Vector3.Lerp(center, sample.tip, taper));
            bounds.Encapsulate(sample.root); bounds.Encapsulate(sample.tip);
            Color color = trailColor; color.a *= fade * fade * (3f - 2f * fade);
            colors.Add(color); colors.Add(color);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
            if (i == 0 || samples[i - 1].stroke != sample.stroke) continue;
            int n = i * 2;
            indices.Add(n - 2); indices.Add(n - 1); indices.Add(n);
            indices.Add(n); indices.Add(n - 1); indices.Add(n + 1);
        }
        mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0, false); mesh.bounds = bounds;
        material.SetFloat(GlowProperty, glowIntensity);
        material.SetFloat(EmissionProperty, emissionIntensity);
    }

    private void OnDisable()
    {
        tracking = emitting = false;
        samples.Clear();
        if (mesh != null) mesh.Clear();
    }

    private void OnDestroy()
    {
        if (effect != null) Destroy(effect);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
        if (sparkMesh != null) Destroy(sparkMesh);
    }
}
