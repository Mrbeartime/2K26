using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectD.EditorTools.LevelDesign
{
    internal sealed class TraceLevelWindow : EditorWindow
    {
        enum Layer { Ground, Actors, Props, Edges }
        const string Docs = "Docs/LevelDesign";
        [SerializeField] TraceLevelPlan plan = new TraceLevelPlan();
        [SerializeField] string savedJson, planPath;
        [SerializeField] Layer layer;
        [SerializeField] Ground ground = Ground.Floor;
        [SerializeField] Actor actor = Actor.Warrior;
        [SerializeField] Prop prop = Prop.Goal;
        [SerializeField] Edge edge = Edge.Door;
        [SerializeField] Facing facing;
        [SerializeField] string channel = "Blue";
        [SerializeField] int round = 2;
        [SerializeField] float opacity = .8f;
        [SerializeField] bool startOpen;
        TraceBrushIcons icons;
        Texture2D preview;
        string previewPath, imageError, status;
        Vector2 scroll;
        int strokeGroup;
        bool drawing, choosingCrop;
        Vector2? firstCorner;

        [MenuItem("Tools/Level Design/Trace and Paint")]
        static void Open()
        {
            var window = GetWindow<TraceLevelWindow>("Trace and Paint");
            window.minSize = new Vector2(710, 410);
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.projectChanged += RefreshIcons;
            if (savedJson == null || savedJson.Length == 0) savedJson = JsonUtility.ToJson(plan);
            saveChangesMessage = "Save this level plan before closing the painter?";
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.projectChanged -= RefreshIcons;
            if (preview != null) DestroyImmediate(preview);
            previewPath = null;
        }

        void RefreshIcons() { icons = null; Repaint(); }

        void OnUndo() { UpdateDirty(); Repaint(); }
        void UpdateDirty() => hasUnsavedChanges = JsonUtility.ToJson(plan) != savedJson;
        void Changed(string label) => Undo.RegisterCompleteObjectUndo(this, label);
        public override void SaveChanges() { if (SavePlan()) base.SaveChanges(); }

        void OnGUI()
        {
            icons ??= new TraceBrushIcons();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New", EditorStyles.toolbarButton)) Replace(new TraceLevelPlan(), "");
                if (GUILayout.Button("Open plan", EditorStyles.toolbarButton)) LoadPlan(EditorUtility.OpenFilePanel("Open level plan", Docs, "json"));
                if (GUILayout.Button("Page 7 example", EditorStyles.toolbarButton)) LoadPlan(Docs + "/IntroTrace.json");
                if (GUILayout.Button("Save plan as…", EditorStyles.toolbarButton)) SavePlan();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Generate draft scene…", EditorStyles.toolbarButton)) Generate();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (var side = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Width(252)))
                {
                    scroll = side.scrollPosition;
                    EditorGUIUtility.labelWidth = 93;
                    try { Controls(); }
                    finally { EditorGUIUtility.labelWidth = 0; }
                }
                var area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawBoard(area);
            }
        }

        void Controls()
        {
            GUILayout.Label("Design page", EditorStyles.boldLabel);
            GUILayout.Label(Path.GetFileName(plan.reference ?? ""), EditorStyles.miniLabel);
            if (GUILayout.Button("Choose page image…"))
            {
                string path = EditorUtility.OpenFilePanelWithFilters("Select reference", Docs + "/References", new[] { "Images", "png,jpg,jpeg" });
                if (path.Length > 0)
                {
                    Changed("Set design page");
                    plan.reference = FileUtil.GetProjectRelativePath(path);
                    if (string.IsNullOrEmpty(plan.reference))
                    {
                        string root = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        string full = Path.GetFullPath(path);
                        plan.reference = full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length).Replace('\\', '/') : full;
                    }
                    UpdateDirty();
                }
            }
            if (GUILayout.Button(choosingCrop ? "Cancel corner selection" : "Pick grid corners on page"))
            {
                choosingCrop = !choosingCrop;
                firstCorner = null;
                wantsMouseMove = choosingCrop;
            }
            if (choosingCrop)
                GUILayout.Label(firstCorner.HasValue ? "Click the opposite outer corner." : "Click an outer corner of the paper grid.", EditorStyles.wordWrappedMiniLabel);
            opacity = EditorGUILayout.Slider("Page opacity", opacity, 0, 1);
            int w = Mathf.Clamp(EditorGUILayout.DelayedIntField("Columns", plan.width), 1, 32);
            int h = Mathf.Clamp(EditorGUILayout.DelayedIntField("Rows", plan.height), 1, 32);
            if (w != plan.width || h != plan.height)
            {
                if (plan.cells.Exists(c => c.x >= w || c.y >= h) || plan.edges.Exists(e => !TraceLevelPlan.FitsEdge(e.x, e.y, e.east, e.kind, w, h)))
                    status = "Erase painted cells and edges outside the new size first.";
                else { Changed("Resize grid"); plan.width = w; plan.height = h; UpdateDirty(); }
            }
            var crop = plan.crop;
            GUILayout.Label("Image crop (fractions from top left)", EditorStyles.miniLabel);
            crop.x = Mathf.Clamp(EditorGUILayout.FloatField("Left", crop.x), 0, .99f);
            crop.y = Mathf.Clamp(EditorGUILayout.FloatField("Top", crop.y), 0, .99f);
            crop.width = Mathf.Clamp(EditorGUILayout.FloatField("Width", crop.width), .01f, 1 - crop.x);
            crop.height = Mathf.Clamp(EditorGUILayout.FloatField("Height", crop.height), .01f, 1 - crop.y);
            if (crop != plan.crop) { Changed("Align reference"); plan.crop = crop; UpdateDirty(); }
            int limit = Mathf.Max(1, EditorGUILayout.DelayedIntField("Turn limit", plan.turns));
            if (limit != plan.turns) { Changed("Set turn limit"); plan.turns = limit; UpdateDirty(); }

            EditorGUILayout.Space();
            GUILayout.Label("Paint brush", EditorStyles.boldLabel);
            layer = (Layer)EditorGUILayout.EnumPopup("Layer", layer);
            switch (layer)
            {
                case Layer.Ground: ground = (Ground)EditorGUILayout.EnumPopup("Tile", ground); break;
                case Layer.Actors: actor = icons.Palette(actor, facing); break;
                case Layer.Props: prop = icons.Palette(prop, facing); break;
                case Layer.Edges: edge = (Edge)EditorGUILayout.EnumPopup("Border", edge); break;
            }
            if (layer == Layer.Edges && TraceLevelPlan.IsDoor(edge))
                startOpen = EditorGUILayout.Toggle("Starts open", startOpen);
            if (layer == Layer.Actors || layer == Layer.Props && !TraceLevelPlan.RandomFacing(prop) && prop != Prop.Goal)
                facing = (Facing)EditorGUILayout.EnumPopup("Facing", facing);
            if (layer == Layer.Props && (TraceLevelPlan.Source(prop) || prop == Prop.LinkedSpikes) || layer == Layer.Edges && edge == Edge.Door)
                channel = EditorGUILayout.TextField("Link name", channel);
            if (layer == Layer.Ground && ground == Ground.Rising || layer == Layer.Props && (prop == Prop.ArrowTrap || prop == Prop.SpikeTrap))
                round = Mathf.Max(1, EditorGUILayout.IntField(layer == Layer.Ground ? "Appears round" : "Every N rounds", round));
            GUILayout.Label(layer == Layer.Edges ? "Click the nearest edge. Right-click erases." : "Drag to paint. Right-click erases this layer.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Check plan")) status = plan.CheckPlayable() ?? "Ready to generate.";
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            if (!string.IsNullOrEmpty(imageError)) EditorGUILayout.HelpBox(imageError, MessageType.Warning);
        }

        void LoadImage()
        {
            if (previewPath == plan.reference) return;
            previewPath = plan.reference;
            if (preview != null) DestroyImmediate(preview);
            preview = null;
            imageError = null;
            if (string.IsNullOrEmpty(previewPath)) return;
            try
            {
                var bytes = File.ReadAllBytes(previewPath);
                preview = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (!preview.LoadImage(bytes, true)) throw new IOException("Select a PNG or JPEG reference.");
            }
            catch (Exception ex)
            {
                if (preview != null) DestroyImmediate(preview);
                preview = null;
                imageError = ex.Message;
            }
        }

        void DrawBoard(Rect area)
        {
            LoadImage();
            if (choosingCrop && preview != null) { DrawCropPicker(area); return; }
            area.xMin += 22; area.yMin += 22;
            float size = Mathf.Min((area.width - 12) / plan.width, (area.height - 12) / plan.height);
            if (size < 1) return;
            var board = new Rect(0, 0, size * plan.width, size * plan.height) { center = area.center };
            EditorGUI.DrawRect(board, new Color(.12f, .13f, .15f));
            if (preview != null)
            {
                var color = GUI.color;
                GUI.color = new Color(1, 1, 1, opacity);
                var r = plan.crop;
                GUI.DrawTextureWithTexCoords(board, preview, new Rect(r.x, 1 - r.yMax, r.width, r.height));
                GUI.color = color;
            }
            var line = new Color(.2f, .36f, .4f, .8f);
            for (int x = 0; x <= plan.width; x++) EditorGUI.DrawRect(new Rect(board.x + x * size, board.y, 1, board.height), line);
            for (int y = 0; y <= plan.height; y++) EditorGUI.DrawRect(new Rect(board.x, board.y + y * size, board.width, 1), line);
            var label = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Clamp((int)(size / 4.5f), 8, 15) };
            label.normal.textColor = Color.white;
            for (int x = 0; x < plan.width; x++) GUI.Label(new Rect(board.x + x * size, board.y - 20, size, 19), (x + 1).ToString(), label);
            for (int y = 0; y < plan.height; y++) GUI.Label(new Rect(board.x - 22, board.y + y * size, 21, size), (y + 1).ToString(), label);
            foreach (var c in plan.cells)
            {
                var rect = new Rect(board.x + c.x * size + 1, board.y + c.y * size + 1, size - 1, size - 1);
                EditorGUI.DrawRect(rect, c.ground == Ground.Void ? new Color(.8f,.1f,.1f,.5f) :
                    c.ground == Ground.Rising ? new Color(.72f,.47f,.1f,.42f) : new Color(.08f,.55f,.42f,.32f));
                bool both = c.prop != Prop.None && c.actor != Actor.None;
                float side = size * (both ? .47f : .76f);
                Rect IconAt(float x) => new Rect(x - side / 2, rect.center.y - side / 2, side, side);
                if (c.actor != Actor.None) icons.Draw(IconAt(both ? rect.x + size * .26f : rect.center.x), c.actor, c.actorFacing);
                if (c.prop != Prop.None) icons.Draw(IconAt(both ? rect.x + size * .74f : rect.center.x), c.prop, c.propFacing);
                string note = c.ground == Ground.Rising ? "R" + c.riseRound :
                    c.prop == Prop.SpikeTrap || c.prop == Prop.ArrowTrap ? "/" + c.interval : c.channel;
                if (!string.IsNullOrEmpty(note))
                {
                    var strip = new Rect(rect.x, rect.yMax - Mathf.Min(14, size / 3), rect.width, Mathf.Min(14, size / 3));
                    EditorGUI.DrawRect(strip, new Color(0, 0, 0, .75f));
                    GUI.Label(strip, note, label);
                }
                GUI.Label(rect, new GUIContent("", $"{TraceLevelPlan.Position(c.x, c.y)} • {c.actor} {c.actorFacing} • {c.prop} • {c.channel}"));
            }
            foreach (var e in plan.edges)
            {
                var mark = e.east ? new Rect(board.x + (e.x + 1) * size - 3, board.y + e.y * size, 6, size)
                    : new Rect(board.x + e.x * size, board.y + (e.y + 1) * size - 3, size, 6);
                EditorGUI.DrawRect(mark, TraceLevelPlan.IsWall(e.kind) ? Color.gray : e.kind == Edge.Door ? TraceLevelPlan.LinkColor(e.channel) : Color.magenta);
                if (e.kind == Edge.BrokenWall || e.startOpen)
                {
                    var gap = mark;
                    if (e.east) { gap.y += mark.height * .35f; gap.height *= .3f; }
                    else { gap.x += mark.width * .35f; gap.width *= .3f; }
                    EditorGUI.DrawRect(gap, e.kind == Edge.BrokenWall ? new Color(.46f,.3f,.2f) : Color.black);
                }
                GUI.Label(mark, new GUIContent("", e.kind + " " + e.channel + (TraceLevelPlan.IsDoor(e.kind) ? (e.startOpen ? " (open)" : " (closed)") : "")));
            }
            PaintInput(board, size);
        }

        void DrawCropPicker(Rect area)
        {
            float scale = Mathf.Min((area.width - 12) / preview.width, (area.height - 12) / preview.height);
            if (scale <= 0) return;
            var page = new Rect(0, 0, preview.width * scale, preview.height * scale) { center = area.center };
            GUI.DrawTexture(page, preview);
            EditorGUIUtility.AddCursorRect(page, MouseCursor.ArrowPlus);
            var ev = Event.current;
            var point = new Vector2(Mathf.Clamp01((ev.mousePosition.x - page.x) / page.width),
                Mathf.Clamp01((ev.mousePosition.y - page.y) / page.height));
            if (firstCorner.HasValue)
            {
                Vector2 a = Vector2.Min(firstCorner.Value, point);
                Vector2 b = Vector2.Max(firstCorner.Value, point);
                var selection = Rect.MinMaxRect(page.x + a.x * page.width, page.y + a.y * page.height,
                    page.x + b.x * page.width, page.y + b.y * page.height);
                EditorGUI.DrawRect(selection, new Color(0, .6f, .9f, .3f));
                if (ev.type == EventType.MouseMove) Repaint();
            }
            if (ev.type != EventType.MouseDown || ev.button != 0 || !page.Contains(ev.mousePosition)) return;
            if (firstCorner.HasValue)
            {
                Vector2 a = Vector2.Min(firstCorner.Value, point);
                Vector2 b = Vector2.Max(firstCorner.Value, point);
                if (b.x - a.x >= .01f && b.y - a.y >= .01f)
                {
                    Changed("Align design grid");
                    plan.crop = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
                    UpdateDirty();
                    choosingCrop = wantsMouseMove = false;
                    firstCorner = null;
                }
            }
            else firstCorner = point;
            ev.Use(); Repaint();
        }

        void PaintInput(Rect board, float size)
        {
            var ev = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (ev.type == EventType.MouseUp && drawing)
            {
                drawing = false; GUIUtility.hotControl = 0; Undo.CollapseUndoOperations(strokeGroup); ev.Use(); return;
            }
            if (!board.Contains(ev.mousePosition) || ev.button > 1) return;
            if (ev.type == EventType.MouseDown)
            {
                drawing = true; GUIUtility.hotControl = id;
                Undo.IncrementCurrentGroup(); strokeGroup = Undo.GetCurrentGroup(); Changed("Paint level");
            }
            else if (ev.type != EventType.MouseDrag || !drawing) return;
            var cellPos = (ev.mousePosition - board.position) / size;
            int x = Mathf.FloorToInt(cellPos.x), y = Mathf.FloorToInt(cellPos.y);
            bool erase = ev.button == 1;
            if (layer == Layer.Edges)
            {
                float dx = cellPos.x - x - .5f, dy = cellPos.y - y - .5f;
                bool east = Mathf.Abs(dx) > Mathf.Abs(dy);
                if (east && dx < 0) x--;
                if (!east && dy < 0) y--;
                if (TraceLevelPlan.FitsEdge(x, y, east, erase || edge == Edge.None ? Edge.Wall : edge, plan.width, plan.height))
                {
                    var old = plan.Border(x, y, east);
                    if (old != null) plan.edges.Remove(old);
                    if (!erase && edge != Edge.None) plan.edges.Add(new TraceEdge { x = x, y = y, east = east, kind = edge, startOpen = TraceLevelPlan.IsDoor(edge) && startOpen, channel = edge == Edge.Door ? channel.Trim() : "" });
                }
            }
            else
            {
                var c = plan.Cell(x, y);
                if (c == null) { c = new TraceCell { x = x, y = y }; plan.cells.Add(c); }
                switch (layer)
                {
                    case Layer.Ground: c.ground = erase ? Ground.Void : ground; c.riseRound = round; break;
                    case Layer.Actors: c.actor = erase ? Actor.None : actor; c.actorFacing = facing; break;
                    case Layer.Props:
                        if (!erase && TraceLevelPlan.RandomFacing(prop))
                        {
                            if (c.prop != prop) c.propFacing = (Facing)UnityEngine.Random.Range(0, 4);
                        }
                        else c.propFacing = prop == Prop.Goal ? Facing.North : facing;
                        c.prop = erase ? Prop.None : prop; c.interval = round;
                        c.channel = TraceLevelPlan.Source(c.prop) || c.prop == Prop.LinkedSpikes ? channel.Trim() : "";
                        break;
                }
                if (layer != Layer.Ground && !erase && (c.prop != Prop.None || c.actor != Actor.None) && c.ground == Ground.Void) c.ground = Ground.Floor;
                if (c.ground == Ground.Void && c.actor == Actor.None && c.prop == Prop.None) plan.cells.Remove(c);
            }
            UpdateDirty(); status = null; ev.Use(); Repaint();
        }

        void Replace(TraceLevelPlan next, string path)
        {
            if (hasUnsavedChanges && !EditorUtility.DisplayDialog("Replace current plan?", "The current plan has unsaved edits.", "Discard and open", "Cancel")) return;
            Undo.ClearUndo(this);
            plan = next; planPath = path; savedJson = JsonUtility.ToJson(plan); hasUnsavedChanges = false; status = null;
            choosingCrop = wantsMouseMove = false; firstCorner = null;
            GUI.FocusControl(null); Repaint();
        }

        void LoadPlan(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var next = JsonUtility.FromJson<TraceLevelPlan>(File.ReadAllText(path));
                if (next == null) throw new InvalidDataException("Empty plan.");
                var error = next.FormatError();
                if (error != null) throw new InvalidDataException(error);
                Replace(next, path);
            }
            catch (Exception ex) { status = ex.Message; }
        }

        bool SavePlan()
        {
            string file = EditorUtility.SaveFilePanel("Save level plan", string.IsNullOrEmpty(planPath) ? Docs : Path.GetDirectoryName(planPath),
                string.IsNullOrEmpty(planPath) ? "LevelPlan" : Path.GetFileNameWithoutExtension(planPath), "json");
            if (file.Length == 0) return false;
            try
            {
                File.WriteAllText(file, JsonUtility.ToJson(plan, true));
                planPath = file; savedJson = JsonUtility.ToJson(plan); hasUnsavedChanges = false; status = "Saved " + Path.GetFileName(file);
                return true;
            }
            catch (Exception ex) { status = ex.Message; return false; }
        }

        void Generate()
        {
            status = plan.CheckPlayable();
            if (status != null) return;
            string path = EditorUtility.SaveFilePanelInProject("Generate draft scene", "LevelDraft", "unity", "Choose a new scene path.");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                TraceSceneBuilder.Generate(plan, path);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
                status = "Generated " + path + ". Open this scene by itself to play.";
            }
            catch (OperationCanceledException ex) { status = ex.Message; }
            catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
        }
    }
}
