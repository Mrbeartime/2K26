
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class CrystalShatterVisual : MonoBehaviour
{
    private struct Triangle
    {
        public Vector3 a, b, c;
    }

    private readonly List<Mesh> ownedMeshes = new();
    private readonly List<GameObject> silhouettes = new();
    private readonly List<Renderer> capturedSources = new();

    private Mesh shards;
    private Material material, silhouetteMaterial;
    private Color[] colors;
    private float[] releaseTimes;
    private float maxEdge, maxDrift, effectScale, spreadRadius;
    private Bounds bodyBounds;
    private bool diagnostics, peakLogged, committed, launched;
    private int originalTriangleCount;
    private ParticleSystem sparkles;
    private bool[] emitted;
    private Vector3[] rest, vertices, centers, velocity, axes;
    private float[] angularSpeeds;
    private float duration, elapsed, glow, playerHeight;

    [Header("SAO Crystal Shatter Settings")]
    [Tooltip("Fragment size relative to character height")]
    [SerializeField, Range(0.01f, 0.20f)]
    private float fragmentSize = 0.05f;

    public float MeasuredMaximumShardEdge { get; private set; }
    public float AllowedMaximumShardEdge => maxEdge;

    public static void Create(
        Transform root,
        float duration,
        int count,
        float force,
        float rotation,
        Color color,
        float emission,
        float particleLifetime,
        float flash,
        float effectScale = 1f,
        float fragmentSize = .05f,
        float spreadRadius = .12f,
        bool diagnosticLogs = true)
    {
        Shader shader = Resources.Load<Shader>("CorpseCrystal");

        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning(
                "CorpseCrystal shader missing; retaining corpse visuals.",
                root
            );
            return;
        }

        GameObject effect = new GameObject(
            "Player Corpse Crystal Shatter"
        );

        effect.transform.SetPositionAndRotation(
            Vector3.zero,
            Quaternion.identity
        );
        effect.transform.localScale = Vector3.one;

        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
            effect, root.gameObject.scene
        );

        var visual = effect.AddComponent<CrystalShatterVisual>();

        visual.duration = duration;
        visual.glow = Mathf.Clamp(flash, 0f, .15f);
        visual.effectScale = Mathf.Clamp(effectScale, .5f, 1.5f);
        visual.spreadRadius = Mathf.Clamp(spreadRadius, 0f, .25f);
        visual.diagnostics = diagnosticLogs;

        // Read persistent Inspector setting from Player.
        visual.fragmentSize = Mathf.Clamp(
        fragmentSize, 0.01f, 0.20f
        );

        effect.layer = root.gameObject.layer;

        visual.material = new Material(shader);
        visual.material.SetColor(
            "_CrystalColor",
            new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                1f
            )
        );
        visual.material.SetFloat(
            "_Emission", Mathf.Clamp(emission, 0f, .6f)
        );

        visual.silhouetteMaterial = new Material(visual.material);
        visual.silhouetteMaterial.SetFloat("_EdgeStrength", 0f);
        visual.silhouetteMaterial.SetFloat("_BrightnessLimit", .85f);
        visual.silhouetteMaterial.SetFloat("_UseDissolve", 1f);

        try
        {
            visual.Build(
                root,
                Mathf.Clamp(count, 12, 1500),
                force,
                rotation,
                particleLifetime
            );
        }
        catch (System.Exception error)
        {
            Debug.LogException(error, root);
            Destroy(effect);
        }
    }

    private void Build(
        Transform root,
        int limit,
        float force,
        float rotation,
        float particleLifetime)
    {
        var triangles = new List<Triangle>();
        var sources = new List<Renderer>();

        Character owner = root.GetComponentInParent<Character>();
        bool hasBodyBounds = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                if (diagnostics)
                {
                    Debug.Log(
                        $"[Shatter skipped] {renderer.name}",
                        renderer
                    );
                }
                continue;
            }

            Mesh captured = null;

            try
            {
                if (renderer is SkinnedMeshRenderer skin &&
                    skin.sharedMesh != null)
                {
                    captured = new Mesh();

                    // Preserve animated pose without applying scale twice.
                    skin.BakeMesh(captured, true);
                }
                else if (
                    renderer is MeshRenderer &&
                    renderer.TryGetComponent(out MeshFilter filter) &&
                    filter.sharedMesh != null)
                {
                    if (!filter.sharedMesh.isReadable)
                    {
                        Debug.LogWarning(
                            $"Enable Read/Write for {filter.sharedMesh.name}",
                            renderer
                        );
                        continue;
                    }

                    captured = Instantiate(filter.sharedMesh);
                }

                if (captured == null)
                    continue;

                ownedMeshes.Add(captured);

                Vector3[] points = captured.vertices;

                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = renderer.transform.TransformPoint(points[i]);
                }

                captured.vertices = points;

                Color[] bodyColors = new Color[points.Length];
                for (int i = 0; i < bodyColors.Length; i++)
                    bodyColors[i] = Color.white;

                captured.colors = bodyColors;
                captured.RecalculateBounds();

                if (!IsFinite(captured.bounds.center) ||
                    !IsFinite(captured.bounds.size) ||
                    points.Length == 0)
                {
                    continue;
                }

                if (!hasBodyBounds)
                {
                    bodyBounds = captured.bounds;
                    hasBodyBounds = true;
                }
                else
                {
                    bodyBounds.Encapsulate(captured.bounds);
                }

                captured.RecalculateNormals();

                for (int sub = 0; sub < captured.subMeshCount; sub++)
                {
                    if (captured.GetTopology(sub) != MeshTopology.Triangles)
                        continue;

                    int[] indices = captured.GetTriangles(sub);

                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        Triangle t = new Triangle
                        {
                            a = points[indices[i]],
                            b = points[indices[i + 1]],
                            c = points[indices[i + 2]]
                        };

                        if (Vector3.Cross(t.b - t.a, t.c - t.a)
                            .sqrMagnitude > 0.000000001f)
                        {
                            triangles.Add(t);
                        }
                    }
                }

                GameObject snapshot = new GameObject(
                    "Crystal silhouette"
                );

                snapshot.layer = renderer.gameObject.layer;
                snapshot.transform.SetParent(transform, false);

                snapshot.AddComponent<MeshFilter>().sharedMesh = captured;

                MeshRenderer frozen = snapshot.AddComponent<MeshRenderer>();

                Material[] materials = new Material[captured.subMeshCount];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = silhouetteMaterial;

                frozen.sharedMaterials = materials;
                frozen.shadowCastingMode = ShadowCastingMode.Off;
                frozen.allowOcclusionWhenDynamic = false;

                silhouettes.Add(snapshot);
                sources.Add(renderer);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning(
                    $"Shatter capture failed: {error.Message}",
                    renderer
                );
            }
        }

        if (triangles.Count == 0 || !hasBodyBounds)
        {
            Debug.LogWarning(
                "[Shatter] No usable triangles captured.",
                root
            );

            Destroy(gameObject);
            return;
        }

        // Keep the real death animation pose.
        // Never reshape the mesh to collider bounds.

        float poseLength = Mathf.Max(
            bodyBounds.size.x,
            Mathf.Max(bodyBounds.size.y, bodyBounds.size.z)
        );

        float standingHeight = owner != null
            ? owner.CorpseSightBounds.size.y
            : 0f;

        playerHeight =
            standingHeight >= poseLength * .4f &&
            standingHeight <= poseLength * 2f
                ? standingHeight
                : poseLength;

        if (playerHeight < .0001f)
        {
            Destroy(gameObject);
            return;
        }

        // Allow fragments up to 20% of character height.
        maxEdge = playerHeight * .20f;
        maxDrift = playerHeight * spreadRadius * effectScale;

        originalTriangleCount = triangles.Count;

        int count = limit;

        colors = new Color[count * 3];
        releaseTimes = new float[count];
        emitted = new bool[count];
        vertices = new Vector3[count * 3];
        rest = new Vector3[count * 3];
        centers = new Vector3[count];
        velocity = new Vector3[count];
        axes = new Vector3[count];
        angularSpeeds = new float[count];

        Vector2[] uv = new Vector2[count * 3];
        int[] indicesOut = new int[count * 3];

        Vector3 center = Vector3.zero;

        foreach (Triangle t in triangles)
            center += (t.a + t.b + t.c) / 3f;

        center /= triangles.Count;

        var random = new System.Random(721);

        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Triangle t in triangles)
        {
            minY = Mathf.Min(
                minY, Mathf.Min(t.a.y, Mathf.Min(t.b.y, t.c.y))
            );
            maxY = Mathf.Max(
                maxY, Mathf.Max(t.a.y, Mathf.Max(t.b.y, t.c.y))
            );
        }

        silhouetteMaterial.SetFloat("_MinY", minY);
        silhouetteMaterial.SetFloat(
            "_Height", Mathf.Max(.01f, maxY - minY)
        );

        float[] cumulativeArea = new float[triangles.Count];
        float totalArea = 0f;

        for (int i = 0; i < triangles.Count; i++)
        {
            Triangle t = triangles[i];
            totalArea += Vector3.Cross(
                t.b - t.a, t.c - t.a
            ).magnitude * .5f;

            cumulativeArea[i] = totalArea;
        }

        for (int i = 0; i < count; i++)
        {
            float area =
                (i + (float)random.NextDouble()) /
                count * totalArea;

            int triangleIndex = System.Array.BinarySearch(
                cumulativeArea, area
            );

            if (triangleIndex < 0)
                triangleIndex = ~triangleIndex;

            Triangle t = triangles[
                Mathf.Min(triangleIndex, triangles.Count - 1)
            ];

            // Inspector-selected fragment size: 0.01 to 0.20.
            float targetEdge = playerHeight * Mathf.Clamp(
                fragmentSize * (
                    .8f + .2f * (float)random.NextDouble()
                ),
                .01f,
                .20f
            );

            for (int level = 0;
                 level < 24 && LongestEdge(t) > targetEdge;
                 level++)
            {
                t = Subdivide(t, random.Next(4));
            }

            Vector3 midpoint = (t.a + t.b + t.c) / 3f;

            float originalEdge = LongestEdge(t);

            if (originalEdge > .000001f)
            {
                float factor = targetEdge / originalEdge;

                t.a = midpoint + (t.a - midpoint) * factor;
                t.b = midpoint + (t.b - midpoint) * factor;
                t.c = midpoint + (t.c - midpoint) * factor;
            }

            float edgeLength = LongestEdge(t);

            if (!IsFinite(midpoint) ||
                !float.IsFinite(edgeLength) ||
                edgeLength > maxEdge * 1.001f)
            {
                t = new Triangle
                {
                    a = center,
                    b = center,
                    c = center
                };

                midpoint = center;
                edgeLength = 0f;
            }

            MeasuredMaximumShardEdge = Mathf.Max(
                MeasuredMaximumShardEdge, edgeLength
            );

            // Position matches the baked animation.
            centers[i] = midpoint;

            float height = Mathf.InverseLerp(
                minY, maxY, midpoint.y
            );

            releaseTimes[i] = duration * (
                .12f + .5f * Mathf.Clamp01(
                    height * .8f +
                    (float)random.NextDouble() * .2f
                )
            );

            axes[i] = new Vector3(
                (float)random.NextDouble() - .5f,
                (float)random.NextDouble() - .5f,
                (float)random.NextDouble() - .5f
            ).normalized;

            velocity[i] = (
                (midpoint - center).normalized * .4f +
                axes[i] * .1f +
                Vector3.up * .3f
            ) * Mathf.Min(force, playerHeight * .2f) *
                (.5f + (float)random.NextDouble());

            angularSpeeds[i] =
                rotation * (.5f + (float)random.NextDouble());

            rest[i * 3] = t.a - midpoint;
            rest[i * 3 + 1] = t.b - midpoint;
            rest[i * 3 + 2] = t.c - midpoint;

            vertices[i * 3] = centers[i] + rest[i * 3];
            vertices[i * 3 + 1] = centers[i] + rest[i * 3 + 1];
            vertices[i * 3 + 2] = centers[i] + rest[i * 3 + 2];

            uv[i * 3] = Vector2.zero;
            uv[i * 3 + 1] = Vector2.right;
            uv[i * 3 + 2] = Vector2.up;
        }

        for (int i = 0; i < indicesOut.Length; i++)
            indicesOut[i] = i;

        shards = new Mesh
        {
            name = "Corpse triangle shards"
        };

        ownedMeshes.Add(shards);
        shards.MarkDynamic();

        for (int i = 0; i < colors.Length; i++)
            colors[i] = new Color(1, 1, 1, 0);

        shards.vertices = vertices;
        shards.colors = colors;
        shards.uv = uv;
        shards.triangles = indicesOut;
        shards.RecalculateBounds();

        gameObject.AddComponent<MeshFilter>().sharedMesh = shards;

        MeshRenderer shardRenderer =
            gameObject.AddComponent<MeshRenderer>();

        if (sources.Count > 0)
            gameObject.layer = sources[0].gameObject.layer;

        shardRenderer.allowOcclusionWhenDynamic = false;
        shardRenderer.sharedMaterial = material;
        shardRenderer.shadowCastingMode = ShadowCastingMode.Off;

        AddSparkles(center, particleLifetime, count);

        capturedSources.AddRange(sources);
        committed = true;

        foreach (Renderer source in sources)
            source.enabled = false;

        if (diagnostics)
            LogRenderState("created");
    }

    private void AddSparkles(
        Vector3 center,
        float lifetime,
        int count)
    {
        GameObject go = new GameObject("Crystal sparkles");
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        go.layer = gameObject.layer;

        ParticleSystem particles = go.AddComponent<ParticleSystem>();

        particles.Stop(
            true, ParticleSystemStopBehavior.StopEmittingAndClear
        );

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = duration;
        main.startLifetime = Mathf.Min(lifetime, duration * .75f);
        main.startSpeed = .08f;
        main.startSize = .009f;
        main.maxParticles = Mathf.Min(count, 100);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startSize3D = false;

        var emission = particles.emission;
        emission.enabled = false;

        var shape = particles.shape;
        shape.enabled = false;

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.Linear(0, 1, 1, 0)
        );

        var tint = particles.colorOverLifetime;
        tint.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0),
                new GradientColorKey(Color.white, 1)
            },
            new[]
            {
                new GradientAlphaKey(0, 0),
                new GradientAlphaKey(1, .15f),
                new GradientAlphaKey(0, 1)
            }
        );

        tint.color = gradient;

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = material;

        Mesh sparkle = new Mesh
        {
            name = "Crystal sparkle triangle"
        };

        sparkle.vertices = new[]
        {
            new Vector3(-.5f, -.4f, 0),
            new Vector3(.5f, -.4f, 0),
            new Vector3(0, .6f, 0)
        };

        sparkle.colors = new[]
        {
            Color.white, Color.white, Color.white
        };

        sparkle.uv = new[]
        {
            Vector2.zero, Vector2.right, Vector2.up
        };

        sparkle.triangles = new[] { 0, 1, 2 };
        sparkle.RecalculateBounds();

        ownedMeshes.Add(sparkle);

        pr.renderMode = ParticleSystemRenderMode.Mesh;
        pr.mesh = sparkle;

        particles.Play();
        sparkles = particles;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }

    private static float LongestEdge(Triangle t)
    {
        return Mathf.Max(
            (t.a - t.b).magnitude,
            Mathf.Max(
                (t.b - t.c).magnitude,
                (t.c - t.a).magnitude
            )
        );
    }

    private static Triangle Subdivide(Triangle t, int child)
    {
        Vector3 ab = (t.a + t.b) * .5f;
        Vector3 bc = (t.b + t.c) * .5f;
        Vector3 ca = (t.c + t.a) * .5f;

        switch (child)
        {
            case 0:
                return new Triangle { a = t.a, b = ab, c = ca };

            case 1:
                return new Triangle { a = ab, b = t.b, c = bc };

            case 2:
                return new Triangle { a = ca, b = bc, c = t.c };

            default:
                return new Triangle { a = ab, b = bc, c = ca };
        }
    }

    private void Update()
    {
        // When attached to a Player prefab, this component stores
        // Inspector settings only. The spawned effect runs the animation.
        if (shards == null)
            return;

        elapsed += Time.deltaTime;

        float warmup = duration * .15f;

        float flash = elapsed < warmup
            ? elapsed / warmup
            : 1f - Mathf.Clamp01(
                (elapsed - warmup) / (duration * .25f)
            );

        material.SetFloat("_Flash", glow * flash);
        silhouetteMaterial.SetFloat("_Flash", 0f);

        float dissolve = Mathf.Clamp01(
            (elapsed / duration - .12f) / .5f
        );

        silhouetteMaterial.SetFloat("_Dissolve", dissolve);

        if (dissolve >= 1f && !launched)
        {
            launched = true;

            foreach (GameObject silhouette in silhouettes)
            {
                if (silhouette != null)
                    silhouette.SetActive(false);
            }
        }

        for (int i = 0; i < centers.Length; i++)
        {
            float t = Mathf.Max(
                0f, elapsed - releaseTimes[i]
            );

            float progress = Mathf.Clamp01(
                t / (duration - releaseTimes[i])
            );

            float fade = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(.35f, 1f, progress)
            );

            float scale = Mathf.Lerp(1f, .25f, progress);

            Quaternion rotation = Quaternion.AngleAxis(
                angularSpeeds[i] * t,
                axes[i]
            );

            Vector3 drift =
                velocity[i] *
                (.55f * (1f - Mathf.Exp(-t / .55f))) +
                Vector3.up * (.06f * t);

            Vector3 position = centers[i] +
                Vector3.ClampMagnitude(drift, maxDrift);

            float alpha = elapsed < releaseTimes[i]
                ? 0f
                : fade * Mathf.Clamp01(t / .08f);

            float sparkle = .85f +
                .15f * Mathf.Sin(t * 9f + i * 2.4f);

            for (int j = 0; j < 3; j++)
            {
                vertices[i * 3 + j] =
                    position +
                    rotation * rest[i * 3 + j] * scale;

                colors[i * 3 + j] = new Color(
                    sparkle, sparkle, 1f, alpha
                );
            }

            if (!emitted[i] && t > 0f)
            {
                emitted[i] = true;

                if (sparkles != null && i % 9 == 0)
                {
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = centers[i],
                        startSize = playerHeight * .006f,
                        velocity =
                            Vector3.up * (playerHeight * .025f)
                    };

                    sparkles.Emit(emit, 1);
                }
            }
        }

        shards.colors = colors;
        shards.vertices = vertices;
        shards.RecalculateBounds();

        if (!peakLogged && elapsed >= duration * .5f)
        {
            peakLogged = true;

            if (diagnostics)
                LogRenderState("mid-effect");
        }

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    private void LogRenderState(string stage)
    {
        float alpha = 0f;
        int visibleFragments = 0;

        for (int i = 0; i < colors.Length; i += 3)
        {
            alpha = Mathf.Max(alpha, colors[i].a);

            if (colors[i].a > .05f)
                visibleFragments++;
        }

        Renderer render = GetComponent<Renderer>();

        Debug.Log(
            $"[Shatter {stage}] " +
            $"sourceTriangles={originalTriangleCount}, " +
            $"fragments={centers.Length}, " +
            $"visibleFragments={visibleFragments}, " +
            $"maxAlpha={alpha:F3}, " +
            $"bodyBounds={bodyBounds}, " +
            $"height={playerHeight:F3}, " +
            $"fragmentSize={fragmentSize:F3}, " +
            $"maxEdge={MeasuredMaximumShardEdge:F4}/{maxEdge:F4}, " +
            $"shaderSupported={material.shader.isSupported}",
            this
        );

        foreach (Camera camera in Camera.allCameras)
        {
            bool mask =
                (camera.cullingMask & (1 << gameObject.layer)) != 0;

            bool frustum = GeometryUtility.TestPlanesAABB(
                GeometryUtility.CalculateFrustumPlanes(camera),
                render.bounds
            );

            Debug.Log(
                $"[Shatter camera] {camera.name}, " +
                $"enabled={camera.isActiveAndEnabled}, " +
                $"layerIncluded={mask}, " +
                $"frustumIntersects={frustum}",
                camera
            );
        }
    }

    [ContextMenu("Log World-space Shard Measurements")]
    private void LogMeasurements()
    {
        Debug.Log(
            $"Shatter height={playerHeight:F4}, " +
            $"max edge={MeasuredMaximumShardEdge:F4}, " +
            $"limit={maxEdge:F4}, " +
            $"drift cap={maxDrift:F4}, " +
            $"fragment size={fragmentSize:F3}, " +
            $"bounds={shards?.bounds}",
            this
        );
    }

    private void OnDestroy()
    {
        if (!committed)
        {
            foreach (Renderer source in capturedSources)
            {
                if (source != null)
                    source.enabled = true;
            }
        }

        foreach (Mesh mesh in ownedMeshes)
        {
            if (mesh != null)
                Destroy(mesh);
        }

        if (material != null)
            Destroy(material);

        if (silhouetteMaterial != null)
            Destroy(silhouetteMaterial);
    }
}
