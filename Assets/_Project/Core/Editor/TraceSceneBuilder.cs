using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectD.EditorTools.LevelDesign
{
    internal static class TraceSceneBuilder
    {
        static readonly Dictionary<Actor, string> Actors = new Dictionary<Actor, string>
        {
            { Actor.Warrior, "d03d15cda553e7949968a75713e4e15b" }, { Actor.Rogue, "1a5e4f9264de7804ebb985c9d5f830d7" },
            { Actor.Archer, "b0a47ca329ccd2947a9175be509e8e12" }, { Actor.SwordSkeleton, "b28cbef201156964cbac15d5283257f2" },
            { Actor.BowSkeleton, "7880236cd575bfb45bacba8dc109d5e6" }
        };
        static readonly Dictionary<Prop, string> Props = new Dictionary<Prop, string>
        {
            { Prop.Plate, "a86fc552fc68aec478862f0fa754540f" }, { Prop.Lever, "45d373b6de4d61c4cac147d35d8584d0" },
            { Prop.BowTarget, "0dbbf88bac5e2dc4d9ed6d7384daf67e" }, { Prop.ArrowTrap, "cd2f7dbdd51292e44a6ae71d3239777c" },
            { Prop.SpikeTrap, "51c3c3a0bb008a64b8402be1cf02e626" }, { Prop.LinkedSpikes, "833ca0617e244ea46a4af1a4dba48be8" }
        };
        const string Floor = "9f528ff0f28713849a4a3e4b55398653", Rising = "fcda09e383fd1f44ebb9724be6ba05ae";
        const string Door = "75cb688ba49959a458059b7d40033bd9", RogueGate = "2bb745459d1080a41b5bf2035c6bfbe1";

        internal static void Generate(TraceLevelPlan plan, string path)
        {
            string problem = plan.CheckPlayable();
            if (problem != null) throw new InvalidDataException(problem);
            if (EditorApplication.isPlayingOrWillChangePlaymode || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                Path.GetExtension(path) != ".unity" || path.Split('/').Contains("..") || File.Exists(path) || File.Exists(path + ".meta"))
                throw new IOException("Choose a new .unity file under Assets in Edit Mode.");

            if (!Application.isBatchMode && !EditorSceneManager.EnsureUntitledSceneHasBeenSaved(
                "Unity needs a saved starting scene to create a separate draft. Save the untitled scene to continue."))
                throw new OperationCanceledException("Draft generation cancelled.");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Build(plan);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Unity could not save the draft scene.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Build(TraceLevelPlan plan)
        {
            var ground = new GameObject("Ground").transform;
            var actors = new GameObject("Actors").transform;
            var mechanisms = new GameObject("Mechanisms").transform;
            var sources = new List<(Component component, string key)>();
            var doors = new List<(DoorController component, string key)>();
            var spikes = new List<(SwitchSpikeTrap component, string key)>();
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer < 0) throw new InvalidDataException("The project's Wall layer is missing.");

            foreach (var c in plan.cells)
            {
                if (c.ground == Ground.Void) continue;
                var pos = new Vector3(c.x, 0, plan.height - 1 - c.y);
                var floor = Spawn(c.prop == Prop.Plate ? Props[Prop.Plate] : c.ground == Ground.Rising ? Rising : Floor, ground, pos, c.prop == Prop.Plate ? (int)c.propFacing * 90 : 0);
                var tile = Require<Tile>(floor);
                if (c.ground == Ground.Rising)
                {
                    var rise = new SerializedObject(Require<TurnTile>(floor));
                    rise.FindProperty("appearTurn").intValue = c.riseRound;
                    rise.FindProperty("walkableAfterRise").boolValue = true;
                    rise.FindProperty("canShowHighlightAfterRise").boolValue = true;
                    rise.FindProperty("blockArrowsAfterRise").boolValue = false;
                    rise.ApplyModifiedPropertiesWithoutUndo();
                }
                if (c.actor != Actor.None) Spawn(Actors[c.actor], actors, pos, (int)c.actorFacing * 90);
                if (c.prop == Prop.None) continue;
                if (c.prop == Prop.Goal)
                {
                    var goal = Box("Goal", mechanisms, pos + Vector3.up * .14f, new Vector3(.8f, .28f, .8f), 0);
                    goal.GetComponent<BoxCollider>().isTrigger = true;
                    goal.AddComponent<WinBox>();
                    continue;
                }
                var obj = c.prop == Prop.Plate ? floor : Spawn(Props[c.prop], mechanisms,
                    pos + Vector3.up * (c.prop == Prop.ArrowTrap ? .45f : .02f), (int)c.propFacing * 90);
                string key = TraceLevelPlan.Key(c.channel);
                switch (c.prop)
                {
                    case Prop.Plate: sources.Add((Require<PressurePlate>(obj), key)); break;
                    case Prop.Lever:
                        obj.transform.localScale = Vector3.one * .6323f; // Lever proportions from Level 1.
                        PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
                        var trigger = obj.AddComponent<BoxCollider>();
                        trigger.isTrigger = true;
                        trigger.size = new Vector3(.8f, 1, .8f);
                        sources.Add((obj.AddComponent<DoorSwitch>(), key));
                        break;
                    case Prop.BowTarget: sources.Add((Require<ArrowTutorial>(obj), key)); break;
                    case Prop.ArrowTrap: SetInt(Require<TurnArrowTrap>(obj), "intervalTurns", c.interval); break;
                    case Prop.SpikeTrap:
                        var timed = Require<TurnSpikeTrap>(obj);
                        SetInt(timed, "intervalTurns", c.interval);
                        SetTile(timed, tile);
                        break;
                    case Prop.LinkedSpikes:
                        spikes.Add((Require<SwitchSpikeTrap>(obj), key));
                        SetTile(Require<SpikeTrap>(obj), tile);
                        break;
                }
            }
            foreach (var e in plan.edges)
            {
                var pos = new Vector3(e.x + (e.east ? .5f : 0), 0, plan.height - 1 - e.y - (e.east ? 0 : .5f));
                float yaw = e.east ? 90 : 0;
                if (e.kind == Edge.Wall)
                    Box("Wall", mechanisms, pos + Vector3.up * .5f, new Vector3(1, 1, .13f), yaw).layer = wallLayer;
                else if (e.kind == Edge.BrokenWall)
                    Spawn("97d8c23d32d50994898f9f22c39b34fd", mechanisms, pos, yaw);
                else
                {
                    var gate = Spawn(e.kind == Edge.Door ? Door : RogueGate, mechanisms, pos, yaw);
                    Component controller;
                    if (e.kind == Edge.Door)
                    {
                        var door = Require<DoorController>(gate);
                        doors.Add((door, TraceLevelPlan.Key(e.channel)));
                        controller = door;
                    }
                    else controller = Require<RogueDoor>(gate);
                    var data = new SerializedObject(controller);
                    data.FindProperty(e.kind == Edge.Door ? "startOpen" : "isOpen").boolValue = e.startOpen;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            foreach (var source in sources)
            {
                var matched = doors.Where(d => d.key == source.key).Select(d => d.component).ToArray();
                SetLinks(source.component, "doors", matched);
                var hover = source.component.GetComponentInChildren<DoorSwitchHoverBlink>(true);
                if (hover != null) SetLinks(hover, "targetDoors", matched);
                if (!(source.component is ArrowTutorial))
                    SetLinks(source.component, "spikeTraps", spikes.Where(s => s.key == source.key).Select(s => s.component).ToArray());
            }
            var systems = new GameObject("Draft systems");
            SetInt(systems.AddComponent<GridManager>(), "wallLayer", 1 << wallLayer);
            systems.AddComponent<Pathfinder>();
            systems.AddComponent<PlayerController>();
            systems.AddComponent<TurnGameManager>().maximumTurns = plan.turns;
            var camera = new GameObject("Board camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(plan.height, plan.width) * .65f + 1;
            var center = new Vector3((plan.width - 1) * .5f, 0, (plan.height - 1) * .5f);
            camera.transform.rotation = Quaternion.Euler(68, 0, 0);
            camera.transform.position = center - camera.transform.forward * (plan.height + 12);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        static GameObject Spawn(string guid, Transform parent, Vector3 position, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) throw new InvalidDataException("Missing level palette asset " + guid);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            // DoorSwitch uses its name to identify simultaneous activation requests.
            obj.name = $"{asset.name} ({position.x:G}, {position.z:G})";
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            return obj;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, float yaw)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            obj.transform.localScale = size;
            return obj;
        }

        static T Require<T>(GameObject obj) where T : Component =>
            obj.GetComponentInChildren<T>(true) ?? throw new InvalidDataException(obj.name + " needs " + typeof(T).Name);

        static void SetInt(Component target, string field, int value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).intValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetTile(Component target, Tile tile)
        {
            var data = new SerializedObject(target);
            data.FindProperty("targetTile").objectReferenceValue = tile;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLinks(Component target, string field, Component[] matches)
        {
            var data = new SerializedObject(target);
            var list = data.FindProperty(field);
            list.arraySize = matches.Length;
            for (int i = 0; i < matches.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = matches[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
