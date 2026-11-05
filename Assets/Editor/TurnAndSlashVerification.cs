using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Opt-in Play Mode smoke test; never saves scene changes.</summary>
public static class TurnAndSlashVerification
{
    [MenuItem("Tools/Sword Trail/Verify Neon Rendering")]
    public static void VerifyNeon()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        TurnGameManager.Instance.StartCoroutine(NeonCapture());
    }

    private static IEnumerator NeonCapture()
    {
        var sword = Object.FindAnyObjectByType<SwordMan>();
        var lights = Object.FindObjectsByType<Light>();
        var states = Array.ConvertAll(lights, light => light.enabled);
        var ambient = RenderSettings.ambientLight;
        var ambientMode = RenderSettings.ambientMode;
        float reflection = RenderSettings.reflectionIntensity;
        var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        volume.profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom);
        float bloomIntensity = bloom.intensity.value;
        var lines = new List<string>();
        try
        {
            foreach (string shaderName in new[] { "RedSwordTrail", "CorpseCrystal" })
                lines.Add(shaderName + " shaderError=" + ShaderUtil.ShaderHasError(Resources.Load<Shader>(shaderName)));
            lines.Add($"CameraHDR={Camera.main.allowHDR}; Bloom intensity={bloomIntensity}, threshold={bloom.threshold.value}, scatter={bloom.scatter.value}");
            for (int repeat = 0; repeat < 3; repeat++)
            {
                sword.GetComponent<CharacterAnimationController>().PlaySkill();
                yield return new WaitForSeconds(.29f);
                if (repeat == 0)
                {
                    Time.timeScale = 0;
                    yield return new WaitForEndOfFrame();
                    SaveNeonFrame("SwordBright");
                    foreach (Light light in lights) light.enabled = false;
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = Color.black; RenderSettings.reflectionIntensity = 0;
                    yield return new WaitForEndOfFrame(); SaveNeonFrame("SwordDark");
                    bloom.intensity.value = 0;
                    yield return new WaitForEndOfFrame(); SaveNeonFrame("SwordDarkNoBloom");
                    bloom.intensity.value = bloomIntensity;
                    var trail = sword.GetComponent<SwordSlashTrail>();
                    var tick = (Action)Delegate.CreateDelegate(typeof(Action), trail, typeof(SwordSlashTrail).GetMethod("LateUpdate", Private));
                    for (int warm = 0; warm < 10; warm++) tick();
                    long allocated = GC.GetAllocatedBytesForCurrentThread();
                    for (int frame = 0; frame < 200; frame++) tick();
                    lines.Add("Trail steady-state managed bytes/200 updates=" + (GC.GetAllocatedBytesForCurrentThread() - allocated));
                    for (int i = 0; i < lights.Length; i++) lights[i].enabled = states[i];
                    RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
                    RenderSettings.reflectionIntensity = reflection;
                    Time.timeScale = 1;
                }
                yield return new WaitForSeconds(2f);
            }
            int ribbons = 0;
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>()) if (filter.name == "Red Sword Trail") ribbons++;
            lines.Add("Ribbon objects after 3 skills=" + ribbons);
            var rogue = Object.FindAnyObjectByType<Rogue>();
            rogue.OnDeath();
            yield return new WaitForSeconds(2f);
            typeof(PlayerCorpseShatter).GetMethod("Preview", Private).Invoke(rogue.GetComponent<PlayerCorpseShatter>(), null);
            yield return new WaitForSeconds(1.2f);
            Time.timeScale = 0;
            yield return new WaitForEndOfFrame(); SaveNeonFrame("ShatterBright");
            foreach (Light light in lights) light.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black; RenderSettings.reflectionIntensity = 0;
            yield return new WaitForEndOfFrame(); SaveNeonFrame("ShatterDark");
            bloom.intensity.value = 0;
            yield return new WaitForEndOfFrame(); SaveNeonFrame("ShatterDarkNoBloom");
            bloom.intensity.value = bloomIntensity;
            var effect = Object.FindAnyObjectByType<CrystalShatterVisual>();
            var shardTick = (Action)Delegate.CreateDelegate(typeof(Action), effect, typeof(CrystalShatterVisual).GetMethod("Update", Private));
            for (int warm = 0; warm < 10; warm++) shardTick();
            long shardBytes = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 200; frame++) shardTick();
            lines.Add("Shatter steady-state managed bytes/200 updates=" + (GC.GetAllocatedBytesForCurrentThread() - shardBytes));
            Time.timeScale = 1;
            yield return new WaitForSeconds(2f);
            lines.Add("Shatter objects after completion=" + Object.FindObjectsByType<CrystalShatterVisual>().Length);
        }
        finally
        {
            Time.timeScale = 1; bloom.intensity.value = bloomIntensity;
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = states[i];
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            RenderSettings.reflectionIntensity = reflection;
            System.IO.File.WriteAllLines("Temp/NeonVerification.txt", lines);
            Debug.Log("NEON VERIFICATION FINISHED: Temp/NeonVerification.txt");
        }
    }

    private static void SaveNeonFrame(string name)
    {
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes("Temp/Neon" + name + ".png", texture.EncodeToPNG());
        Object.Destroy(texture);
    }
    private static double previewDeadline;
    [MenuItem("Tools/Sword Trail/Preview and Pause Trail")]
    public static void Preview()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        var sword = Object.FindAnyObjectByType<SwordMan>();
        if (sword == null || sword.IsDead) return;
        sword.GetComponent<CharacterAnimationController>().PlaySkill();
        previewDeadline = EditorApplication.timeSinceStartup + 4;
        EditorApplication.update -= PauseTrail;
        EditorApplication.update += PauseTrail;
    }

    private static void PauseTrail()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > previewDeadline)
        { EditorApplication.update -= PauseTrail; return; }
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>())
        {
            if (filter.name != "Red Sword Trail" || filter.sharedMesh.vertexCount < 16) continue;
            EditorApplication.update -= PauseTrail;
            EditorApplication.isPaused = true;
            var renderer = filter.GetComponent<Renderer>();
            Debug.Log($"TRAIL PREVIEW: vertices={filter.sharedMesh.vertexCount}, bounds={renderer.bounds}, shaderErrors={ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)}");
            return;
        }
    }
    private static readonly List<string> report = new();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Sword Trail/Verify Current Play Session")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            throw new Exception("Enter Play Mode and wait for the intro before verification.");
        var manager = TurnGameManager.Instance;
        if (manager == null || manager.IsGameComplete || manager.IsIntroPlaying || manager.IsEnemyPhase)
            throw new Exception("A ready player phase is required.");
        report.Clear();
        manager.StartCoroutine(Check(manager));
    }

    private static void Pass(bool condition, string message)
    {
        report.Add((condition ? "PASS " : "FAIL ") + message);
        System.IO.File.WriteAllLines("Temp/TurnAndSlashVerification.txt", report);
        if (!condition) throw new Exception(message);
        Debug.Log("[Turn/Slash verification] " + message);
    }

    private static IEnumerator Check(TurnGameManager manager)
    {
        var sword = Object.FindAnyObjectByType<SwordMan>();
        var rogue = Object.FindAnyObjectByType<Rogue>();
        var archer = Object.FindAnyObjectByType<ArcherMan>();
        Pass(sword != null && rogue != null && archer != null, "All three player classes present");
        var animator = sword.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        sword.GetComponent<CharacterAnimationController>().PlaySkill();
        float until = Time.time + 2.5f;
        int peakVertices = 0;
        while (Time.time < until)
        {
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>())
                if (filter.name == "Red Sword Trail" && filter.sharedMesh != null)
                    peakVertices = Mathf.Max(peakVertices, filter.sharedMesh.vertexCount);
            yield return null;
        }
        Pass(peakVertices >= 4, "Real Skill animation generated ribbon vertices: " + peakVertices);
        Pass(!sword.IsDead && !rogue.IsDead && !archer.IsDead, "Visual-only skill preview caused no damage");
        // Let real traps/enemies resolve, keeping the last survivor alive for lifecycle assertions.
        bool originalCanKill = sword.CanKill;
        sword.CanKill = false;
        int phase = 0, verifiedRounds = 0;
        Action phaseCheck = () =>
        {
            if (manager.Status.StartsWith("Trap Phase:")) phase = 1;
            else if (manager.Status == "Enemy Phase...")
            {
                Pass(phase == 1, "Trap phase precedes enemy phase");
                foreach (TurnTrap trap in Object.FindObjectsByType<TurnTrap>())
                    Pass(!trap.IsResolving, "Trap resolution finished before enemies: " + trap.name);
                foreach (ArrowProjectile shot in Object.FindObjectsByType<ArrowProjectile>())
                    Pass(!shot.IsFlying, "Trap projectile finished before enemies");
                phase = 2;
            }
            else if (manager.Status == "Player Corpse Cleanup...")
            {
                Pass(phase == 2, "Enemy phase precedes corpse cleanup");
                foreach (ArrowProjectile shot in Object.FindObjectsByType<ArrowProjectile>())
                    Pass(!shot.IsFlying, "Enemy projectile finished before cleanup");
                phase = 3; verifiedRounds++;
            }
        };
        manager.StateChanged += phaseCheck;
        var shooterObject = new GameObject("Verification Shooter");
        var shooter = shooterObject.AddComponent<RangedEnemy>(); shooter.enabled = false;
        var otherObject = new GameObject("Verification Other Shooter");
        var other = otherObject.AddComponent<RangedEnemy>(); other.enabled = false;
        var arrowObject = new GameObject("Verification Arrow");
        var arrow = arrowObject.AddComponent<ArrowProjectile>();
        arrow.Initialize(shooter, Vector3.forward, 10f, 1f);
        typeof(ArrowProjectile).GetMethod("Hit", Private).Invoke(arrow, new object[] { archer.GetComponent<Collider>() });
        var kills = (HashSet<Character>)typeof(RangedEnemy).GetField("killedPlayers", Private).GetValue(shooter);
        var otherKills = (HashSet<Character>)typeof(RangedEnemy).GetField("killedPlayers", Private).GetValue(other);
        Pass(archer.IsDead && kills.Contains(archer) && !otherKills.Contains(archer), "Projectile death ownership is per shooter");
        var template = new GameObject("Verification Arrow Template"); template.SetActive(false);
        typeof(RangedEnemy).GetField("arrowPrefab", Private).SetValue(shooter, template);
        typeof(RangedEnemy).GetField("arrowPrefab", Private).SetValue(other, template);
        shooter.transform.position = other.transform.position = new Vector3(10000, 10000, 10000);
        var sight = typeof(RangedEnemy).GetMethod("HasAdventurerInSight", Private);
        var boundsProperty = typeof(Character).GetProperty("CorpseSightBounds");
        Bounds originalBounds = archer.CorpseSightBounds;
        boundsProperty.SetValue(archer, new Bounds(shooter.transform.position + Vector3.forward, Vector3.one * .5f));
        Pass(!(bool)sight.Invoke(shooter, new object[] { shooter.transform }) &&
            (bool)sight.Invoke(other, new object[] { other.transform }), "Real LOS excludes own kill and includes another shooter's fresh corpse");
        boundsProperty.SetValue(archer, originalBounds);
        rogue.OnDeath();
        Pass(!kills.Contains(rogue), "Non-projectile death is not attributed to ranged shooter");
        Bounds rogueBounds = rogue.CorpseSightBounds;
        boundsProperty.SetValue(rogue, new Bounds(shooter.transform.position + Vector3.forward, Vector3.one * .5f));
        Pass((bool)sight.Invoke(shooter, new object[] { shooter.transform }), "Real LOS still accepts a fresh corpse killed by another source");
        boundsProperty.SetValue(rogue, rogueBounds);
        Object.Destroy(template);
        int deathRound = manager.RoundNumber;
        Pass(archer.GetComponent<PlayerCorpseShatter>().DeathRound == deathRound, "DeathRound captured from manager");
        manager.ChooseEndTurn();
        until = Time.realtimeSinceStartup + 12;
        while (manager.IsEnemyPhase && !manager.IsGameComplete && Time.realtimeSinceStartup < until) yield return null;
        Pass(!manager.IsGameComplete && manager.RoundNumber == deathRound + 1, "Next round starts with no due corpse wait");
        Pass(!archer.GetComponent<PlayerCorpseShatter>().HasShattered && !rogue.GetComponent<PlayerCorpseShatter>().HasShattered,
            "Fresh corpses retained in death round");
        manager.ChooseEndTurn();
        int maximumEffects = 0;
        float effectStarted = -1;
        until = Time.realtimeSinceStartup + 12;
        while (manager.IsEnemyPhase && !manager.IsGameComplete && Time.realtimeSinceStartup < until)
        {
            int count = Object.FindObjectsByType<CrystalShatterVisual>().Length;
            maximumEffects = Mathf.Max(maximumEffects, count);
            if (count > 0)
            {
                if (effectStarted < 0) effectStarted = Time.time;
                if (manager.CanChooseMove || manager.CanChooseSkill) { Pass(false, "Input unlocked during cleanup"); yield break; }
            }
            yield return null;
        }
        Pass(maximumEffects == 2, "Two due corpses shatter concurrently");
        Pass(!manager.IsGameComplete && manager.RoundNumber == deathRound + 2 &&
            Object.FindObjectsByType<CrystalShatterVisual>().Length == 0, "Next player phase waits for actual VFX destruction");
        Pass(effectStarted >= 0 && Time.time - effectStarted >= 2.7f, "Original shatter duration retained");
        Pass(archer != null && rogue != null && kills.Contains(archer), "Player references and killer record survive visual cleanup");
        manager.StateChanged -= phaseCheck;
        Pass(verifiedRounds == 2, "Two complete Trap/Enemy/Cleanup sequences verified");
        sword.CanKill = originalCanKill;
        sword.OnDeath();
        Pass(manager.IsGameComplete, "Last-player death preserves Game Over");
        report.Add("LIMIT: Last survivor was invulnerable during lifecycle checks. Scene unload, varied animation speeds and all facing directions are not exercised by this test.");
        System.IO.File.WriteAllLines("Temp/TurnAndSlashVerification.txt", report);
        Debug.Log("TURN/SLASH VERIFICATION COMPLETE. Exit Play Mode to restore the scene.");
    }
}
