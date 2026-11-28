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

        const float Pitch = 1.05f;
        const float FloorHeight = .42697f;
        const string Template = "Assets/_Project/Core/Editor/TraceSceneTemplate.unity";
        const string GoalPrefab = "Assets/_Project/Gameplay/Prefabs/Environment/TraceGoal.prefab";

        static Vector3 Position(float x, float z) => new Vector3(x * Pitch, FloorHeight, z * Pitch);

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
            if (!AssetDatabase.CopyAsset(Template, path)) throw new IOException("Cannot copy the level scene template.");
            Scene scene = default;
            bool saved = false;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Build(plan, scene);
                saved = EditorSceneManager.SaveScene(scene);
                if (!saved) throw new IOException("Unity could not save the draft scene.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (!saved) AssetDatabase.DeleteAsset(path);
            }
        }

        static void Build(TraceLevelPlan plan, Scene scene)
        {
            var roots = scene.GetRootGameObjects().ToDictionary(go => go.name, go => go.transform);
            var ground = roots["Ground"];
            var actors = roots["Actors"];
            var mechanisms = roots["Mechanisms"];
            var heroFloors = new List<(Transform actor, Tile tile)>();
            var outlined = new List<(GameObject obj, Color color)>();
            Transform firstGoal = null;
            var sources = new List<(Component component, string key)>();
            var doors = new List<(DoorController component, string key)>();
            var spikes = new List<(SwitchSpikeTrap component, string key)>();
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer < 0) throw new InvalidDataException("The project's Wall layer is missing.");

            foreach (var c in plan.cells)
            {
                if (c.ground == Ground.Void) continue;
                var pos = Position(c.x, plan.height - 1 - c.y);
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
                if (c.actor != Actor.None)
                {
                    bool hero = c.actor == Actor.Warrior || c.actor == Actor.Rogue || c.actor == Actor.Archer;
                    var model = Spawn(Actors[c.actor], actors, pos - Vector3.up * (hero ? .012f : 0), (int)c.actorFacing * 90);
                    model.transform.localScale = Vector3.one * .5f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                    if (hero) heroFloors.Add((model.transform, tile));
                    outlined.Add((model, hero ? Color.green : Color.red));
                }
                if (c.prop == Prop.None) continue;
                if (c.prop == Prop.Goal)
                {
                    var goal = Spawn(AssetDatabase.AssetPathToGUID(GoalPrefab), mechanisms, pos, 0);
                    firstGoal ??= Require<TreasureChestAnimation>(goal).transform;
                    outlined.Add((goal, Color.yellow));
                    continue;
                }
                var obj = c.prop == Prop.Plate ? floor : Spawn(Props[c.prop], mechanisms,
                    pos + Vector3.up * (c.prop == Prop.ArrowTrap ? .47f : c.prop == Prop.LinkedSpikes ? .07f : 0), (int)c.propFacing * 90);
                string key = TraceLevelPlan.Key(c.channel);
                outlined.Add((obj, key.Length == 0 ? new Color(1f, .65f, 0f) : TraceLevelPlan.LinkColor(key)));
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
                var pos = Position(e.x + (e.east ? .5f : 0), plan.height - 1 - e.y - (e.east ? 0 : .5f));
                float yaw = e.east ? 90 : 0;
                if (e.kind == Edge.Wall)
                {
                    // Level 5's two-pillar wall has a centered pivot and a scene-authored box collider.
                    var wall = Spawn("71ebf4e2d521ce4449888b0803ff9b13", mechanisms, pos + Vector3.up * .32f, yaw);
                    wall.transform.localScale = new Vector3(5.2f, 5, 5);
                    wall.layer = wallLayer;
                    wall.AddComponent<BoxCollider>().size = new Vector3(.200213f, .129061f, .025488f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(wall.transform);
                }
                else if (e.kind == Edge.BrokenWall)
                    Spawn("97d8c23d32d50994898f9f22c39b34fd", mechanisms, pos, yaw);
                else
                {
                    var gate = Spawn(e.kind == Edge.Door ? Door : RogueGate, mechanisms, pos, yaw);
                    outlined.Add((gate, e.kind == Edge.Door ? TraceLevelPlan.LinkColor(e.channel) : Color.magenta));
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
            var systems = roots["Draft systems"];
            var turns = Require<TurnGameManager>(systems.gameObject);
            SetInt(turns, "maximumTurns", plan.turns);
            var grid = new SerializedObject(Require<GridManager>(systems.gameObject));
            grid.FindProperty("tileSize").floatValue = Pitch;
            grid.ApplyModifiedPropertiesWithoutUndo();

            var outline = new SerializedObject(Require<AllOutlineColor>(roots["Outline"].gameObject));
            var entries = outline.FindProperty("elements");
            entries.arraySize = outlined.Count;
            for (int i = 0; i < outlined.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("target").objectReferenceValue = outlined[i].obj;
                entry.FindPropertyRelative("color").colorValue = outlined[i].color;
            }
            outline.ApplyModifiedPropertiesWithoutUndo();

            var intro = new SerializedObject(Require<CutSceneCharacter>(roots["CutsceneSet"].gameObject));
            var cast = intro.FindProperty("characters");
            cast.arraySize = heroFloors.Count;
            for (int i = 0; i < heroFloors.Count; i++)
            {
                var entry = cast.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("character").objectReferenceValue = heroFloors[i].actor;
                entry.FindPropertyRelative("startFloor").objectReferenceValue = heroFloors[i].tile.transform;
                entry.FindPropertyRelative("destinationFloor").objectReferenceValue = heroFloors[i].tile.transform;
            }
            intro.FindProperty("treasureChestTarget").objectReferenceValue = firstGoal;
            intro.ApplyModifiedPropertiesWithoutUndo();

            var cameraRig = roots["Board camera"];
            var focus = new SerializedObject(Require<CameraFocusManager>(cameraRig.gameObject));
            var target = (Transform)focus.FindProperty("cameraTarget").objectReferenceValue;
            var center = Position((plan.width - 1) * .5f, (plan.height - 1) * .5f);
            var delta = new Vector3(center.x - target.position.x, 0, center.z - target.position.z);
            cameraRig.position += delta;
            focus.FindProperty("minimumXZ").vector2Value = new Vector2(-Pitch, -Pitch);
            focus.FindProperty("maximumXZ").vector2Value = new Vector2(plan.width * Pitch, plan.height * Pitch);
            focus.ApplyModifiedPropertiesWithoutUndo();
            var mist = roots["Mist"].GetChild(0);
            mist.position = new Vector3(center.x, mist.position.y, center.z);
            mist.localScale *= Mathf.Max(1f, Mathf.Max(plan.width, plan.height) * Pitch / 16f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mist);
            foreach (var light in roots["Lighting"].GetComponentsInChildren<Light>())
                if (light.type == LightType.Point) light.transform.position += delta;
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
