using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ProjectD.Menus
{
    /// <summary>Owns the additive Settings scene, including repeated open/close and early cancellation.</summary>
    internal static class InGameSettingsSession
    {
        private enum State { Closed, Loading, Open, Closing }
        private static State state;
        private static Scene scene;
        private static InGameUIManager gameUI;
        private static float resumeTimeScale;
        private static bool closeRequested;
        private static int generation;
        public static bool IsOpen => state != State.Closed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            state = State.Closed;
            scene = default;
            gameUI = null;
            closeRequested = false;
            generation++;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene unloaded)
        {
            // An external scene change must not leave the session marked Open forever.
            if (state != State.Closed && scene == unloaded) RestoreGame();
        }

        public static void Open()
        {
            string path = GameSceneManager.SettingsScene;
            if (IsOpen || GameSceneManager.IsChangingScene) return;
            if (string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError("Settings scene is missing from SceneCatalog or Build Settings: " + path);
                return;
            }
            if (SceneManager.GetActiveScene().path == path) return;
            state = State.Loading;
            closeRequested = false;
            int request = ++generation;
            resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            gameUI = null;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<InGameUIManager>())
                if (ui.gameObject.scene == SceneManager.GetActiveScene()) { gameUI = ui; break; }
            gameUI?.SetPaused(true);
            Time.timeScale = 0f;
            try
            {
                var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                operation.completed += _ => OnLoaded(request, path);
            }
            catch (Exception error) { RestoreGame(); Debug.LogException(error); }
        }
        private static void OnLoaded(int request, string path)
        {
            if (request != generation) return;
            scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid()) { RestoreGame(); return; }
            MenuScreen menu = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (var events in root.GetComponentsInChildren<EventSystem>(true)) events.gameObject.SetActive(false);
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.sortingOrder = Mathf.Max(1000, canvas.sortingOrder);
                if (menu == null) menu = root.GetComponentInChildren<MenuScreen>(true);
            }
            state = State.Open;
            if (closeRequested || menu == null) { Close(); return; }
            menu.OpenSettings();
        }
        public static void Close()
        {
            if (state == State.Loading) { closeRequested = true; return; }
            if (state != State.Open) return;
            state = State.Closing;
            int request = generation;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var overlay in root.GetComponentsInChildren<OverlayUI>(true)) overlay.DeactivateOverlay();
            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null) { RestoreGame(); return; }
            operation.completed += _ => { if (request == generation) RestoreGame(); };
        }
        public static void LeaveForSceneChange()
        {
            generation++;
            state = State.Closed;
            gameUI = null;
            scene = default;
            closeRequested = false;
        }
        private static void RestoreGame()
        {
            if (state == State.Closed) return;
            Time.timeScale = resumeTimeScale;
            gameUI?.SetPaused(false);
            gameUI = null;
            scene = default;
            closeRequested = false;
            state = State.Closed;
        }
    }
}
