using UnityEngine;
using UnityEditor;

public static class CorpseShatterValidation
{
    private static double captureStarted;
    private static float effectSeenAt = -1f;

    [MenuItem("Tools/Corpse Shatter/End Turn and Pause Shatter")]
    public static void EndTurnAndPauseShatter()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        var manager = Object.FindAnyObjectByType<TurnGameManager>();
        if (manager == null) return;
        captureStarted = EditorApplication.timeSinceStartup;
        effectSeenAt = -1f;
        EditorApplication.update -= PauseForInspection;
        EditorApplication.update += PauseForInspection;
        manager.ChooseEndTurn();
    }

    private static void PauseForInspection()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - captureStarted > 15)
        {
            EditorApplication.update -= PauseForInspection;
            return;
        }
        var effect = Object.FindAnyObjectByType<CrystalShatterVisual>();
        if (effect == null) return;
        if (effectSeenAt < 0f) effectSeenAt = Time.time;
        if (Time.time - effectSeenAt < 1.1f) return;
        EditorApplication.update -= PauseForInspection;
        EditorApplication.isPaused = true;
        MeasureLiveFragmentMeshes();
    }

    [MenuItem("Tools/Corpse Shatter/Measure Live Fragment Meshes")]
    public static void MeasureLiveFragmentMeshes()
    {
        var effects = Object.FindObjectsByType<CrystalShatterVisual>();
        if (effects.Length == 0) { Debug.LogWarning("No live shatter effects. Pause Play Mode during the effect, then run this check."); return; }
        foreach (var effect in effects)
        {
            Mesh mesh = effect.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            Vector3[] points = mesh.vertices;
            int[] indices = mesh.triangles;
            float longest = 0f;
            for (int i = 0; i < indices.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    Vector3 a = effect.transform.TransformPoint(points[indices[i + j]]);
                    Vector3 b = effect.transform.TransformPoint(points[indices[i + (j + 1) % 3]]);
                    longest = Mathf.Max(longest, Vector3.Distance(a, b));
                }
            if (!float.IsFinite(longest) || longest > effect.AllowedMaximumShardEdge * 1.001f)
                throw new System.Exception($"Oversized live shard: {longest}, limit={effect.AllowedMaximumShardEdge}");
            if (mesh.colors.Length != points.Length) throw new System.Exception("Shard color/vertex count mismatch");
            Debug.Log($"[Shatter live mesh PASS] vertices={points.Length}, longest world edge={longest:F5}, limit={effect.AllowedMaximumShardEdge:F5}, scale={effect.transform.lossyScale}, world bounds={effect.GetComponent<Renderer>().bounds}", effect);
        }
    }

    [MenuItem("Tools/Corpse Shatter/Inspect Player Renderers")]
    public static void InspectRenderers()
    {
        foreach (string path in new[] { "Assets/Prefabs/ClassPlayer/RogueModel.prefab", "Assets/Prefabs/ClassPlayer/ArcherModel.prefab", "Assets/Prefabs/ClassPlayer/WarriorModel.prefab" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                Debug.Log($"[CorpseInspect] {path}: {renderer.name} {renderer.GetType().Name}, vertices={mesh?.vertexCount}, readable={mesh?.isReadable}, materials={renderer.sharedMaterials.Length}");
            }
        }
    }

    [MenuItem("Tools/Corpse Shatter/Validate Round Boundaries")]
    public static void ValidateRoundBoundaries()
    {
        for (int death = 1; death <= 20; death++)
            for (int completed = 1; completed <= 21; completed++)
                if (PlayerCorpseShatter.IsDue(death, completed) != (completed > death))
                    throw new System.Exception($"Incorrect shatter round: death={death}, phase={completed}");
        Debug.Log("[CorpseShatter] PASS: 420 round-boundary cases. Run Play Mode to verify lifecycle and visuals.");
    }
}
