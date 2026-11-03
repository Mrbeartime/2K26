using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Resolve FBX curve paths against the existing models, preserving their colliders,
// materials, switch references and authored scene transforms.
[InitializeOnLoad]
public static class PropInteractSetup
{
    private const string Folder = "Assets/3D/OBJ-Model/Prop/Animation/";
    static PropInteractSetup()
    {
        EditorApplication.delayCall += AutoSetup;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => AutoSetup();
    private static void AutoSetup()
    {
        if (SceneManager.GetActiveScene().path == "Assets/Scenes/Time/AnimScene/ModelTest 1.unity") Setup();
    }

    [MenuItem("Tools/Prop Animations/Setup Current Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var model in root.GetComponentsInChildren<Transform>(true))
        {
            if (model.GetComponentInChildren<PropInteractAnimation>(true) != null) continue;
            string sourcePath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject));
            bool lever = sourcePath == "Assets/3D/OBJ-Model/Prop/Lever_Floor.fbx";
            bool bow = sourcePath == "Assets/3D/OBJ-Model/Prop/Bow_Target.fbx";
            if ((!lever && !bow) || !PrefabUtility.IsAnyPrefabInstanceRoot(model.gameObject)) continue;
            Component leverSwitch = null;
            if (lever)
            {
                leverSwitch = model.GetComponentInParent<DoorSwitch>();
                if (leverSwitch == null) leverSwitch = model.GetComponentInParent<FloorSwitch>();
                if (leverSwitch == null)
                {
                    var grid = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GridManager>(true)).FirstOrDefault();
                    if (grid != null)
                    {
                        var switches = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                            .Where(c => c is DoorSwitch || c is FloorSwitch)
                            .Where(c => grid.WorldToGrid(c.transform.position) == grid.WorldToGrid(model.position)).ToArray();
                        if (switches.Length == 1) leverSwitch = switches[0];
                    }
                }
                if (leverSwitch == null)
                {
                    Debug.LogWarning("No unique switch on the lever's grid tile: " + model.name, model);
                    continue;
                }
            }
            if (bow && model.GetComponentInParent<ArrowTutorial>() == null) continue;

            string file = lever ? "Lever_floor_Anim" : "Bow_Target_Anim";
            var imported = AssetDatabase.LoadAllAssetsAtPath(Folder + file + ".fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (imported == null) { Debug.LogWarning("Animation clip not imported yet: " + file); continue; }
            Transform animationRoot = model;
            if (bow)
            {
                // The new target has separate stand/impact meshes; the old FBX
                // is one mesh and cannot receive those animation curves.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + file + ".fbx");
                var oldRenderers = model.GetComponentsInChildren<Renderer>(true);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, model);
                Undo.RegisterCreatedObjectUndo(visual, "Add animated bow target");
                visual.name = "Bow_Target_Animated";
                animationRoot = visual.transform;
                animationRoot.localPosition = Vector3.zero;
                animationRoot.localRotation = Quaternion.identity;
                animationRoot.localScale = Vector3.one;
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var match = oldRenderers.FirstOrDefault(r => r.sharedMaterials.Length == renderer.sharedMaterials.Length);
                    if (match != null) renderer.sharedMaterials = match.sharedMaterials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(animationRoot);
            }
            string clipPath = Folder + file + "_Interact.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = file + "_Interact", frameRate = imported.frameRate };
                var transforms = animationRoot.GetComponentsInChildren<Transform>(true);
                bool valid = true;
                int curves = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(imported))
                {
                    // Root motion belongs to the scene, not the interaction.
                    if (string.IsNullOrEmpty(binding.path)) continue;
                    string path = binding.path;
                    Transform target = animationRoot.Find(path);
                    if (target == null)
                    {
                        string leaf = path.Split('/').Last();
                        var matches = transforms.Where(t => t != animationRoot && t.name == leaf).ToArray();
                        if (matches.Length == 1) target = matches[0];
                    }
                    if (target == null)
                    {
                        Debug.LogError("Cannot bind " + file + " curve: " + path + " on " + model.name, model);
                        valid = false;
                        break;
                    }
                    var mapped = binding;
                    mapped.path = AnimationUtility.CalculateTransformPath(target, animationRoot);
                    AnimationUtility.SetEditorCurve(clip, mapped, AnimationUtility.GetEditorCurve(imported, binding));
                    curves++;
                }
                if (!valid || curves == 0)
                {
                    Object.DestroyImmediate(clip);
                    if (animationRoot != model) Undo.DestroyObjectImmediate(animationRoot.gameObject);
                    continue;
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            var animator = animationRoot.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                Debug.LogWarning("Existing Animator controller found; skipping " + model.name, model);
                if (animationRoot != model) Undo.DestroyObjectImmediate(animationRoot.gameObject);
                continue;
            }
            if (animator == null) animator = Undo.AddComponent<Animator>(animationRoot.gameObject);
            var component = Undo.AddComponent<PropInteractAnimation>(animationRoot.gameObject);
            var data = new SerializedObject(component);
            data.FindProperty("interactClip").objectReferenceValue = clip;
            data.ApplyModifiedProperties();
            if (leverSwitch != null)
            {
                var switchData = new SerializedObject(leverSwitch);
                var animations = switchData.FindProperty("interactionAnimations");
                int index = animations.arraySize;
                animations.InsertArrayElementAtIndex(index);
                animations.GetArrayElementAtIndex(index).objectReferenceValue = component;
                switchData.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(leverSwitch);
            }
            if (bow) foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(animationRoot)) continue;
                Undo.RecordObject(renderer, "Hide static target visual");
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            count++;
        }
        if (count == 0) return;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Set up " + count + " prop animations. Save the scene to keep these references.");
    }
}
