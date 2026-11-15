using System.IO;
using UnityEditor;
using UnityEngine;
public static class DiaryModelSetup
{
    [MenuItem("Tools/Diary/Use Existing DairyOpen Model")]
    public static void Apply()
    {
        const string path="Assets/Prefabs/Diary/Diary.prefab";
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/OBJ-Model/Prop/DairyOpen.fbx");
        if(model==null) throw new System.InvalidOperationException("DairyOpen model is not imported.");
        Directory.CreateDirectory("Logs/DiaryModelBackup");
        if(!File.Exists("Logs/DiaryModelBackup/Diary.prefab.bak")) File.Copy(path,"Logs/DiaryModelBackup/Diary.prefab.bak");
        var root=PrefabUtility.LoadPrefabContents(path);
        try {
            if(root.transform.Find("DairyOpen Visual")!=null) return;
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(model,root.transform);
            visual.name="DairyOpen Visual";
            root.transform.localScale=Vector3.one;
            visual.transform.localPosition=Vector3.zero;
            visual.transform.localRotation=Quaternion.identity;
            var renderers=visual.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0) throw new System.InvalidOperationException("Model has no renderers.");
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float footprint=Mathf.Max(bounds.size.x,bounds.size.z);
            if(footprint<=0) throw new System.InvalidOperationException("Model bounds are empty.");
            visual.transform.localScale*=0.55f/footprint;
            bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position+=new Vector3(root.transform.position.x-bounds.center.x,root.transform.position.y-bounds.min.y,root.transform.position.z-bounds.center.z);
            bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var collider=root.GetComponent<BoxCollider>();
            collider.isTrigger=true;
            collider.center=root.transform.InverseTransformPoint(bounds.center);
            collider.size=new Vector3(bounds.size.x,Mathf.Max(.06f,bounds.size.y),bounds.size.z);
            Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(root.GetComponent<MeshFilter>());
            PrefabUtility.SaveAsPrefabAsset(root,path);
            Debug.Log($"DIARY MODEL PASS: renderers={renderers.Length}, animators={visual.GetComponentsInChildren<Animator>(true).Length}, importedColliders={visual.GetComponentsInChildren<Collider>(true).Length}, size={bounds.size}; original model preserved.");
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
