using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectD.EditorTools.LevelDesign
{
    internal static class TraceSceneVerification
    {
        // Exercise the same save/reopen path as the window. Use a unique, disposable asset.
        [MenuItem("Tools/Level Design/Verify draft generation")]
        public static void Run()
        {
            var plan = JsonUtility.FromJson<TraceLevelPlan>(File.ReadAllText("Docs/LevelDesign/IntroTrace.json"));
            string exampleError = plan.CheckPlayable();
            Check(exampleError == null, "Page 7 example (" + exampleError + ")");
            string path = "Assets/TraceCheck-" + Guid.NewGuid().ToString("N") + ".unity";
            var original = SceneManager.GetActiveScene();
            Scene opened = default;
            try
            {
                plan.Cell(0, 7).prop = Prop.Plate;
                plan.Cell(0, 7).channel = " blue ";
                plan.Cell(1, 8).prop = Prop.LinkedSpikes;
                plan.Cell(1, 8).channel = "BLUE";
                plan.Cell(1, 7).prop = Prop.BowTarget;
                plan.Cell(1, 7).channel = "Blue";
                plan.edges[0].channel = "Unmatched";
                Check(plan.CheckPlayable() != null, "Dangling channel detected");
                plan.edges[0].channel = "Blue";
                plan.cells.Add(new TraceCell { x = 2, y = 8, ground = Ground.Rising, riseRound = 3 });
                plan.cells.Add(new TraceCell { x = 2, y = 7, ground = Ground.Floor, prop = Prop.ArrowTrap, propFacing = Facing.East, interval = 3 });
                plan.cells.Add(new TraceCell { x = 3, y = 7, ground = Ground.Floor, prop = Prop.SpikeTrap });
                plan.edges[0].startOpen = true;
                plan.edges.Add(new TraceEdge { x = -1, y = 8, east = true, kind = Edge.Wall });
                plan.edges.Add(new TraceEdge { x = 8, y = 8, east = false, kind = Edge.BrokenWall });
                plan.edges.Add(new TraceEdge { x = 0, y = 8, east = true, kind = Edge.RogueDoor, startOpen = true });
                TraceSceneBuilder.Generate(plan, path);
                Check(SceneManager.GetActiveScene() == original, "Original scene restored");
                bool refused = false;
                try { TraceSceneBuilder.Generate(plan, path); } catch (IOException) { refused = true; }
                Check(refused, "Existing scene protected");
                opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var roots = opened.GetRootGameObjects();
                T[] All<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
                var tiles = All<Tile>();
                Check(tiles.Length == plan.cells.Count && tiles.Select(t => t.transform.position).Distinct().Count() == tiles.Length, "One floor per coordinate");
                Check(All<PlayerMove>().Length == 1 && All<WinBox>().Length == 1 && All<Enemy>().Length == 1, "Actor and goal placement");
                var lever = All<DoorSwitch>().Single();
                var plate = All<PressurePlate>().Single();
                var gate = All<DoorController>().Single();
                var spike = All<SwitchSpikeTrap>().Single();
                Check(new SerializedObject(gate).FindProperty("startOpen").boolValue && All<RogueDoor>().Single().IsOpen, "Open door defaults survive saving");
                Check(All<CameraFocusManager>().Length == 1 && All<InGameUIManager>().Length == 1 &&
                    All<LevelCompleteUI>().Length == 1 && All<AllOutlineColor>().Length == 1 &&
                    All<CutSceneCharacter>().Length == 1 && All<TreasureChestAnimation>().Length == 1, "Campaign camera, HUD, intro and animated chest");
                Check(Mathf.Approximately(All<GridManager>().Single().TileSize, 1.05f), "Floor pitch");
                foreach (var tile in tiles)
                {
                    var p = tile.transform.position;
                    var gridPos = All<GridManager>().Single().WorldToGrid(p);
                    Check(Mathf.Abs(p.x - gridPos.x * 1.05f) < .001f && Mathf.Abs(p.z - gridPos.y * 1.05f) < .001f, "Tile aligned to grid");
                }
                Check(new SerializedObject(lever).FindProperty("doors").GetArrayElementAtIndex(0).objectReferenceValue == gate, "Lever to door");
                Check(new SerializedObject(plate).FindProperty("spikeTraps").GetArrayElementAtIndex(0).objectReferenceValue == spike, "Plate to spikes");
                Check(new SerializedObject(All<ArrowTutorial>().Single()).FindProperty("doors").GetArrayElementAtIndex(0).objectReferenceValue == gate, "Target to door");
                var rising = new SerializedObject(All<TurnTile>().Single());
                Check(rising.FindProperty("appearTurn").intValue == 3 && rising.FindProperty("walkableAfterRise").boolValue, "Walkable rising floor on round 3");
                var trap = All<TurnArrowTrap>().Single();
                Check(trap.IntervalTurns == 3 && Vector3.Dot(trap.transform.forward, Vector3.right) > .999f, "Arrow trap interval and facing");
                Check(new SerializedObject(All<TurnSpikeTrap>().Single()).FindProperty("targetTile").objectReferenceValue != null, "Spike trap tile");
                Check(All<TurnGameManager>().Single().MaximumTurns == 5, "Turn budget");
                Debug.Log("Trace and Paint verification passed.");
            }
            finally
            {
                if (opened.IsValid() && opened.isLoaded) EditorSceneManager.CloseScene(opened, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidDataException("Trace and Paint verification failed: " + message);
        }
    }
}
