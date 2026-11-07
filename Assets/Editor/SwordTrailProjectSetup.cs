using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>One-off project setup and repeatable, non-gameplay rig validation.</summary>
public static class SwordTrailProjectSetup
{
    [MenuItem("Tools/Sword Trail/Calibrate Warrior Prefab")]
    public static void Configure()
    {
        const string path = "Assets/Prefabs/ClassPlayer/WarriorModel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var trail = root.GetComponent<SwordSlashTrail>();
            if (trail == null) trail = root.AddComponent<SwordSlashTrail>();
            ClearMarkers(trail);
            typeof(SwordSlashTrail).GetMethod("ResolveMarkers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trail, null);
            var serialized = new SerializedObject(trail);
            var tip = serialized.FindProperty("swordTip").objectReferenceValue as Transform;
            var bladeBase = serialized.FindProperty("swordBase").objectReferenceValue as Transform;
            if (tip == null || bladeBase == null) throw new Exception("Sword markers could not be resolved; no prefab saved.");
            AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/3D/Animation/Warrior/WarSkillAttack.fbx"))
                if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview__")) { clip = candidate; break; }
            if (clip == null) throw new Exception("Attack clip missing.");
            const int count = 240;
            float[] speeds = new float[count];
            float[] verticalMotion = new float[count];
            Vector3 previous = Vector3.zero;
            var motion = new System.Text.StringBuilder("normalized,time,tipY,deltaY,speed\n");
            int peak = 1;
            for (int i = 0; i < count; i++)
            {
                clip.SampleAnimation(root, clip.length * i / (count - 1));
                if (i > 0)
                {
                    speeds[i] = Vector3.Distance(previous, tip.position) / (clip.length / (count - 1));
                    verticalMotion[i] = tip.position.y - previous.y;
                    if (speeds[i] > speeds[peak]) peak = i;
                    motion.AppendLine(FormattableString.Invariant($"{i / (count - 1f)},{clip.length * i / (count - 1)},{tip.position.y},{tip.position.y - previous.y},{speeds[i]}"));
                }
                previous = tip.position;
            }
            if (speeds[peak] < .01f) throw new Exception("Sword did not move when sampling clip. No calibration saved.");
            System.IO.File.WriteAllText("Temp/SwordMotion.csv", motion.ToString());
            int start = peak, end = peak;
            while (start > 1 && speeds[start - 1] > speeds[peak] * .3f) start--;
            while (end < count - 1 && speeds[end + 1] > speeds[peak] * .3f) end++;
            // This clip's measured strike is downward. Exclude the final upward preparation.
            if (verticalMotion[peak] < 0f)
                while (start < peak && verticalMotion[start] >= 0f) start++;
            float begin = (start - 1f) / (count - 1), finish = end / (count - 1f);
            Debug.Log($"SWORD AUDIT: clip={clip.name}, fps={clip.frameRate}, duration={clip.length}, bone={tip.parent.name}, peak={peak / (count - 1f):F3}, speed={speeds[peak]:F3}, proposed window={begin:F3}..{finish:F3}, blade length={Vector3.Distance(tip.position, bladeBase.position):F3}. Confirm swing visually in Game View.");
            System.IO.File.WriteAllText("Temp/SwordTrailAudit.txt", $"fps={clip.frameRate}; duration={clip.length}; bone={tip.parent.name}; window={begin}..{finish}; speed={speeds[peak]}; bladeLength={Vector3.Distance(tip.position, bladeBase.position)}");
            // Reload to discard every sampled animation pose before saving markers/settings.
            PrefabUtility.UnloadPrefabContents(root);
            root = PrefabUtility.LoadPrefabContents(path);
            trail = root.GetComponent<SwordSlashTrail>();
            if (trail == null) trail = root.AddComponent<SwordSlashTrail>();
            ClearMarkers(trail);
            typeof(SwordSlashTrail).GetMethod("ResolveMarkers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trail, null);
            serialized = new SerializedObject(trail);
            serialized.FindProperty("slashStart").floatValue = begin;
            serialized.FindProperty("slashEnd").floatValue = Mathf.Min(finish, .89f);
            serialized.FindProperty("minimumSwingSpeed").floatValue = speeds[peak] * .15f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            if (PlayerCorpseShatter.IsDue(3, 3) || !PlayerCorpseShatter.IsDue(3, 4) || PlayerCorpseShatter.IsDue(4, 4))
                throw new Exception("Corpse age regression");
            Debug.Log("SWORD SETUP PASS; corpse age 3/3, 3/4, 4/4 PASS. Visual Play Mode still required.");
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ClearMarkers(SwordSlashTrail trail)
    {
        var data = new SerializedObject(trail);
        foreach (string field in new[] { "swordTip", "swordBase" })
        {
            var property = data.FindProperty(field);
            if (property.objectReferenceValue is Transform marker)
            {
                if (marker.name != "Sword Tip" && marker.name != "Sword Base")
                    throw new Exception("Preserving custom sword marker. Calibrate manually in the Inspector.");
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }
            property.objectReferenceValue = null;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
