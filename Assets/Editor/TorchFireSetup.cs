using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class TorchFireSetup
{
    private const string Folder = "Assets/Prefabs/Effects";
    private const string PrefabPath = Folder + "/TorchFire.prefab";

    static TorchFireSetup()
    {
        if (!Application.isBatchMode) EditorApplication.delayCall += CreateWhenReady;
    }

    private static void CreateWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CreateWhenReady;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();
    }

    [MenuItem("Tools/Torch/Create Fire Prefab")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.Log("Torch prefab already exists: " + PrefabPath);
            return;
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/OBJ-Model/Prop/Torch.fbx");
        if (model == null) throw new InvalidOperationException("Torch.fbx is missing.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Effects");
        var shader = Shader.Find("2K26/TorchParticle");
        if (shader == null) throw new InvalidOperationException("Torch particle shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/TorchFlame.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, Folder + "/TorchFlame.mat");
        }

        var root = new GameObject("TorchFire");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            Bounds bounds = new Bounds();
            bool found = false;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) throw new InvalidOperationException("Torch model has no renderer.");
            float height = Mathf.Max(0.1f, bounds.size.y);
            var anchor = new GameObject("FirePoint - adjust to torch tip").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(bounds.center.x, bounds.max.y - height * 0.04f, bounds.center.z);
            var flame = MakeParticles(anchor, "Flame", material, height, false);
            var sparks = MakeParticles(anchor, "Embers", material, height, true);
            var lightObject = new GameObject("Warm Light");
            lightObject.transform.SetParent(anchor, false);
            lightObject.transform.localPosition = Vector3.up * height * 0.12f;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.42f, 0.1f);
            light.intensity = 2f;
            light.range = height * 4f;
            light.shadows = LightShadows.None;
            var controller = root.AddComponent<TorchFire>();
            var data = new SerializedObject(controller);
            data.FindProperty("fireLight").objectReferenceValue = light;
            var systems = data.FindProperty("particles");
            systems.arraySize = 2;
            systems.GetArrayElementAtIndex(0).objectReferenceValue = flame;
            systems.GetArrayElementAtIndex(1).objectReferenceValue = sparks;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Torch prefab created: {PrefabPath}; model bounds: {bounds}");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        ValidatePrefab();
    }

    private static ParticleSystem MakeParticles(Transform parent, string name, Material material, float size, bool embers)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.duration = 2f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(embers ? 0.7f : 0.35f, embers ? 1.3f : 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.35f, size * (embers ? 0.85f : 0.6f));
        main.startSize = new ParticleSystem.MinMaxCurve(size * (embers ? 0.012f : 0.12f), size * (embers ? 0.025f : 0.23f));
        main.startColor = Color.white;
        main.maxParticles = embers ? 24 : 64;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission;
        emission.rateOverTime = embers ? 6f : 48f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = embers ? 12f : 6f;
        shape.radius = size * (embers ? 0.045f : 0.065f);
        var color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f),
            new GradientColorKey(new Color(1f, 0.3f, 0.025f), 0.45f),
            new GradientColorKey(new Color(0.65f, 0.035f, 0.005f), 1f)
        }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.12f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var scale = system.sizeOverLifetime;
        scale.enabled = true;
        scale.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
        var noise = system.noise;
        noise.enabled = true;
        noise.strength = size * 0.08f;
        noise.frequency = 2f;
        noise.scrollSpeed = 0.7f;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    public static void ValidatePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || prefab.GetComponent<TorchFire>() == null) throw new Exception("Missing torch prefab/controller.");
        if (prefab.GetComponentsInChildren<ParticleSystem>().Length != 2) throw new Exception("Missing flame or embers.");
        foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>())
            if (renderer.sharedMaterial == null || ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)) throw new Exception("Invalid particle shader.");
        Debug.Log("[Torch validation PASS] Flame, embers, controller and shader are ready.");
    }
}
