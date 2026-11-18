using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates an isolated Level 5 atmosphere study. No runtime controller is needed.</summary>
public static class Level5MistSceneSetup
{
    public const string ScenePath = "Assets/Scenes/MainScene/Level/Level5_Mist.unity";
    public const string MaterialPath = "Assets/Materials/Level5Mist.mat";

    [MenuItem("Tools/Atmosphere/Create Level 5 Mist Scene")]
    public static void Create()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene/Level/Level5.unity");
        // Save under a different path before touching any objects from the source level.
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot save mist scene.");
        Shader shader = Shader.Find("ProjectD/Drifting Mist");
        if (shader == null) throw new InvalidOperationException("Drifting Mist shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Level 5 Mist" };
            material.SetColor("_Color", new Color(0.12f, 0.30f, 0.33f, 1f));
            material.SetFloat("_Density", 0.50f);
            material.SetFloat("_Scale", 4f);
            material.SetFloat("_Speed", 0.18f);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        var tiles = Object.FindObjectsByType<Tile>(FindObjectsInactive.Include);
        Bounds bounds = new Bounds(tiles[0].transform.position, Vector3.one);
        foreach (Tile tile in tiles) bounds.Encapsulate(new Bounds(tile.transform.position, Vector3.one));
        var root = new GameObject("Drifting Mist");
        root.transform.position = new Vector3(bounds.center.x, 0, bounds.center.z);
        AddSheet(root.transform, material, "Mist - Lower", -1.65f, bounds.size.x + 10f, bounds.size.z + 10f);
        AddSheet(root.transform, material, "Mist - Upper", -0.45f, bounds.size.x + 7f, bounds.size.z + 7f);

        Camera camera = Camera.main;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.006f, 0.014f, 0.016f, 1f);
        // Request depth on this camera, including when using the Mobile quality preset.
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        RenderSettings.fog = false;
        EditorSceneManager.SaveScene(scene);
        // Allow Retry/scene reload from Play Mode without adding a level to the campaign catalog.
        if (!EditorBuildSettings.scenes.Any(item => item.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
        AssetDatabase.SaveAssetIfDirty(material);
        Selection.activeGameObject = root;
        Debug.Log($"[Level5Mist] Created {ScenePath}; tile bounds={bounds}; two sheets, one material, no runtime scripts.");
    }

    private static void AddSheet(Transform root, Material material, string name, float height, float width, float depth)
    {
        GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sheet.name = name;
        Object.DestroyImmediate(sheet.GetComponent<Collider>());
        sheet.transform.SetParent(root, false);
        sheet.transform.localPosition = new Vector3(0, height, 0);
        sheet.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        sheet.transform.localScale = new Vector3(width, depth, 1);
        MeshRenderer renderer = sheet.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

}
