using System.IO;
using ProjectD.Menus;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectD.EditorTools
{
    [InitializeOnLoad]
    public static class MenuSceneBaker
    {
        private const string StartPath = "Assets/Scenes/Mon/SceneGame/Start.unity";
        private const string SelectPath = "Assets/Scenes/Mon/SceneGame/LevelSelect.unity";

        static MenuSceneBaker() => EditorApplication.update += BakeWhenEditorIsReady;

        private static void BakeWhenEditorIsReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorApplication.update -= BakeWhenEditorIsReady;
            BakeMissingMenuScenes();
        }

        [MenuItem("Tools/Project D/Rebuild Editable Menu Scenes")]
        public static void RebuildMenuScenes()
        {
            Bake(StartPath, MenuScreen.Page.Start, true);
            Bake(SelectPath, MenuScreen.Page.LevelSelect, true);
            Debug.Log("Editable menu scenes rebuilt.");
        }

        private static void BakeMissingMenuScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Bake(StartPath, MenuScreen.Page.Start, false);
            Bake(SelectPath, MenuScreen.Page.LevelSelect, false);
        }

        private static void Bake(string path, MenuScreen.Page page, bool rebuild)
        {
            if (!File.Exists(path)) return;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            MenuScreen controller = null;
            bool hasCanvas = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (controller == null) controller = root.GetComponent<MenuScreen>();
                if (root.name == "Canvas") hasCanvas = true;
            }
            if (hasCanvas && !rebuild)
            {
                if (EnsureMenuCamera(scene)) EditorSceneManager.SaveScene(scene);
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            if (controller == null)
            {
                GameObject root = new GameObject(page == MenuScreen.Page.Start ? "Start Menu" : "LevelSelect Menu");
                SceneManager.MoveGameObjectToScene(root, scene);
                controller = root.AddComponent<MenuScreen>();
            }

            if (rebuild)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root != controller.gameObject) Object.DestroyImmediate(root);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 640);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject background = CreateUIObject("Background", canvasObject.transform, typeof(Image));
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.19f);

            GameObject mainPanel = CreateUIObject("Main Panel", canvasObject.transform);
            Stretch(mainPanel.GetComponent<RectTransform>());
            GameObject settingsPanel = CreateUIObject("Settings Panel", canvasObject.transform);
            Stretch(settingsPanel.GetComponent<RectTransform>());

            Button[] levelButtons = null;
            Text[] levelLabels = null;
            if (page == MenuScreen.Page.Start)
            {
                CreateButton(mainPanel.transform, "Start Button", "Start", 64, font, controller.OpenLevelSelect);
                CreateButton(mainPanel.transform, "Settings Button", "Settings", 0, font, controller.OpenSettings);
                CreateButton(mainPanel.transform, "Exit Button", "Exit", -64, font, controller.ExitGame);
            }
            else
            {
                CreateText(mainPanel.transform, "Title", "Select Level", 225, font, 32);
                levelButtons = new Button[GameSceneManager.LevelCount];
                levelLabels = new Text[GameSceneManager.LevelCount];
                UnityAction[] actions = { controller.OpenLevel1, controller.OpenLevel2, controller.OpenLevel3, controller.OpenLevel4, controller.OpenLevel5 };
                for (int i = 0; i < GameSceneManager.LevelCount; i++)
                {
                    levelButtons[i] = CreateButton(mainPanel.transform, $"Level {i + 1} Button", $"Level {i + 1}", 160 - i * 60, font, actions[i]);
                    levelLabels[i] = levelButtons[i].GetComponentInChildren<Text>();
                }
                CreateButton(mainPanel.transform, "Settings Button", "Settings", -150, font, controller.OpenSettings);
                CreateButton(mainPanel.transform, "Back Button", "Back", -210, font, controller.OpenStart);
            }

            CreateText(settingsPanel.transform, "Title", "Settings", 135, font, 32);
            Button soundButton = CreateButton(settingsPanel.transform, "Sound Button", "Sound: On", 64, font, controller.ToggleSound);
            Button fullscreenButton = CreateButton(settingsPanel.transform, "Fullscreen Button", "Fullscreen: On", 0, font, controller.ToggleFullscreen);
            CreateButton(settingsPanel.transform, "Back Button", "Back", -80, font, controller.CloseSettings);
            settingsPanel.SetActive(false);

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            EnsureMenuCamera(scene);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("page").enumValueIndex = (int)page;
            serialized.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            serialized.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            serialized.FindProperty("soundLabel").objectReferenceValue = soundButton.GetComponentInChildren<Text>();
            serialized.FindProperty("fullscreenLabel").objectReferenceValue = fullscreenButton.GetComponentInChildren<Text>();
            SerializedProperty buttons = serialized.FindProperty("levelButtons");
            SerializedProperty labels = serialized.FindProperty("levelLabels");
            buttons.arraySize = levelButtons?.Length ?? 0;
            labels.arraySize = levelLabels?.Length ?? 0;
            for (int i = 0; levelButtons != null && i < levelButtons.Length; i++)
            {
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = levelButtons[i];
                labels.GetArrayElementAtIndex(i).objectReferenceValue = levelLabels[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private static bool EnsureMenuCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<Camera>(true) != null) return false;

            GameObject cameraObject = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.14f, 0.16f, 0.19f);
            camera.cullingMask = 0;
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            foreach (System.Type component in components) obj.AddComponent(component);
            obj.layer = LayerMask.NameToLayer("UI");
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static Button CreateButton(Transform parent, string name, string label, float y, Font font, UnityAction action)
        {
            GameObject obj = CreateUIObject(name, parent, typeof(Image), typeof(Button));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 48);
            rect.anchoredPosition = new Vector2(0, y);
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.85f, 0.87f, 0.9f);
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            Text text = CreateText(obj.transform, "Text", label, 0, font, 24);
            Stretch(text.rectTransform);
            text.color = Color.black;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string label, float y, Font font, int size)
        {
            GameObject obj = CreateUIObject(name, parent, typeof(Text));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320, 48);
            rect.anchoredPosition = new Vector2(0, y);
            Text text = obj.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
