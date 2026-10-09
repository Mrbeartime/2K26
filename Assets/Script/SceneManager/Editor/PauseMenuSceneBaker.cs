using System.IO;
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
    public static class PauseMenuSceneBaker
    {
        static PauseMenuSceneBaker() => EditorApplication.update += BakeWhenEditorIsReady;

        private static void BakeWhenEditorIsReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorApplication.update -= BakeWhenEditorIsReady;
            AddMissingPauseMenus();
        }

        [MenuItem("Tools/Project D/Add Missing Editable Pause Menus")]
        public static void AddMissingPauseMenus()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            for (int level = 1; level <= 5; level++)
                AddToScene($"Assets/Scenes/TestLevel/Level{level}.unity");
        }

        private static void AddToScene(string path)
        {
            if (!File.Exists(path)) return;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<PauseMenu>() != null)
                {
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                    return;
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject controllerObject = new GameObject("Pause Menu Controller");
            SceneManager.MoveGameObjectToScene(controllerObject, scene);
            PauseMenu controller = controllerObject.AddComponent<PauseMenu>();

            GameObject canvasObject = new GameObject("Pause Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 640);
            scaler.matchWidthOrHeight = 0.5f;

            Button pauseButtonComponent = CreateButton(canvasObject.transform, "Pause Button", "Pause", 0, font, controller.PauseGame);
            RectTransform pauseButtonRect = pauseButtonComponent.GetComponent<RectTransform>();
            pauseButtonRect.anchorMin = pauseButtonRect.anchorMax = new Vector2(1f, 1f);
            pauseButtonRect.pivot = new Vector2(1f, 1f);
            pauseButtonRect.anchoredPosition = new Vector2(-24f, -24f);
            pauseButtonRect.sizeDelta = new Vector2(120f, 44f);

            GameObject pausePanel = CreateOverlay("Pause Panel", canvasObject.transform);
            CreateText(pausePanel.transform, "Title", "Paused", 160, font, 36);
            CreateButton(pausePanel.transform, "Resume Button", "Resume", 70, font, controller.ResumeGame);
            CreateButton(pausePanel.transform, "Level Select Button", "Level Select", 6, font, controller.ReturnToLevelSelect);
            CreateButton(pausePanel.transform, "Settings Button", "Settings", -58, font, controller.OpenSettings);

            GameObject settingsPanel = CreateOverlay("Settings Panel", canvasObject.transform);
            CreateText(settingsPanel.transform, "Title", "Settings", 135, font, 36);
            Button soundButton = CreateButton(settingsPanel.transform, "Sound Button", "Sound: On", 64, font, controller.ToggleSound);
            Button fullscreenButton = CreateButton(settingsPanel.transform, "Fullscreen Button", "Fullscreen: On", 0, font, controller.ToggleFullscreen);
            CreateButton(settingsPanel.transform, "Back Button", "Back", -80, font, controller.CloseSettings);

            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("pauseButton").objectReferenceValue = pauseButtonComponent.gameObject;
            serialized.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            serialized.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            serialized.FindProperty("soundLabel").objectReferenceValue = soundButton.GetComponentInChildren<Text>();
            serialized.FindProperty("fullscreenLabel").objectReferenceValue = fullscreenButton.GetComponentInChildren<Text>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            bool hasEventSystem = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<EventSystem>(true) != null) hasEventSystem = true;
            if (!hasEventSystem)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private static GameObject CreateOverlay(string name, Transform parent)
        {
            GameObject panel = CreateUIObject(name, parent, typeof(Image));
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            return panel;
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
