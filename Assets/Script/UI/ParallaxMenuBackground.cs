using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

    public class ParallaxMenuBackground : MonoBehaviour
    {
        private const string LayerRootName = "Parallax Background Layers";

        [Header("Assign scene artwork")]
        [Tooltip("The furthest background artwork. It moves only a little.")]
        [SerializeField] private RectTransform background;
        [Tooltip("The middle-distance artwork, such as clouds, mountains, or decorations.")]
        [SerializeField] private RectTransform parallaxLayer1;
        [Tooltip("The nearest artwork, such as foreground props or characters.")]
        [SerializeField] private RectTransform parallaxLayer2;

        [Header("Motion")]
        [SerializeField, Range(4f, 200f)] private float pointerRange = 24f;
        [SerializeField, Range(0.05f, 1f)] private float smoothTime = 0.18f;

        [Header("Layer movement multiplier")]
        [Tooltip("Movement multiplier for Background. 0 means it stays still.")]
        [SerializeField, Range(0f, 2f)] private float backgroundMultiplier = 1.50f;
        [Tooltip("Movement multiplier for Parallax Layer 1.")]
        [SerializeField, Range(0f, 2f)] private float layer1Multiplier = 1.75f;
        [Tooltip("Movement multiplier for Parallax Layer 2.")]
        [SerializeField, Range(0f, 2f)] private float layer2Multiplier = 2f;

        [Header("Background edges")]
        [Tooltip("Limits Background movement to its oversized area so the screen never shows an empty edge.")]
        [SerializeField] private bool preventBackgroundGaps = true;

        private RectTransform[] layers;
        private Vector2[] startPositions;
        private Vector2[] velocities;
        private float[] depthMultipliers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            AddToMenuScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AddToMenuScene(scene);
        }

        private static void AddToMenuScene(Scene scene)
        {
            string name = scene.name;
            if (name != "Start" && name != "LevelSelec" && name != "LevelSelector") return;

            Canvas canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null) break;
            }
            if (canvas == null || canvas.GetComponent<ParallaxMenuBackground>() != null) return;
            canvas.gameObject.AddComponent<ParallaxMenuBackground>();
        }

        private void Awake()
        {
            if (background != null || parallaxLayer1 != null || parallaxLayer2 != null)
                UseAssignedLayers();
            else
                BuildLayers();
        }

        private void UseAssignedLayers()
        {
            var assignedLayers = new System.Collections.Generic.List<RectTransform>(3);
            var assignedDepths = new System.Collections.Generic.List<float>(3);

            if (background != null)
            {
                assignedLayers.Add(background);
                assignedDepths.Add(backgroundMultiplier);
            }
            if (parallaxLayer1 != null)
            {
                assignedLayers.Add(parallaxLayer1);
                assignedDepths.Add(layer1Multiplier);
            }
            if (parallaxLayer2 != null)
            {
                assignedLayers.Add(parallaxLayer2);
                assignedDepths.Add(layer2Multiplier);
            }

            layers = assignedLayers.ToArray();
            depthMultipliers = assignedDepths.ToArray();
            startPositions = new Vector2[layers.Length];
            velocities = new Vector2[layers.Length];
            for (int i = 0; i < layers.Length; i++) startPositions[i] = layers[i].anchoredPosition;
        }

        private void BuildLayers()
        {
            Image source = FindBackgroundImage();
            if (source == null || transform.Find(LayerRootName) != null) return;

            var root = new GameObject(LayerRootName, typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(source.transform.parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            // The same scene illustration is softly repeated at different scales.
            // This preserves each scene's artwork while creating depth without new assets.
            float[] scales = { 1.07f, 1.14f, 1.22f };
            float[] alphas = { 0.13f, 0.10f, 0.07f };
            layers = new RectTransform[scales.Length];
            startPositions = new Vector2[scales.Length];
            velocities = new Vector2[scales.Length];
            depthMultipliers = new[] { 0.33f, 0.66f, 1f };

            for (int i = 0; i < scales.Length; i++)
            {
                var layer = new GameObject($"Depth Layer {i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = layer.GetComponent<RectTransform>();
                rect.SetParent(root.transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = source.rectTransform.rect.size * scales[i];

                Image image = layer.GetComponent<Image>();
                image.sprite = source.sprite;
                image.type = source.type;
                image.preserveAspect = source.preserveAspect;
                image.color = new Color(1f, 1f, 1f, alphas[i]);
                image.raycastTarget = false;

                layers[i] = rect;
                startPositions[i] = rect.anchoredPosition;
            }
        }

        private Image FindBackgroundImage()
        {
            foreach (Image image in GetComponentsInChildren<Image>(true))
                if (image.name == "Background" && image.sprite != null) return image;
            return null;
        }

        private void Update()
        {
            if (layers == null || Screen.width == 0 || Screen.height == 0) return;

            // This project uses the new Input System, so do not read from the
            // legacy UnityEngine.Input API. A missing mouse keeps the scene centred.
            Vector2 mousePosition = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 normalizedPointer = new Vector2(
                mousePosition.x / Screen.width - 0.5f,
                mousePosition.y / Screen.height - 0.5f);

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                float depth = depthMultipliers[i];
                Vector2 drift = normalizedPointer * pointerRange * depth;
                drift.y += Mathf.Sin(Time.unscaledTime * (0.38f + i * 0.08f) + i) * (2f + i);
                Vector2 targetPosition = startPositions[i] + drift;
                if (preventBackgroundGaps && layers[i] == background)
                    targetPosition = ClampToBackgroundBounds(targetPosition, startPositions[i]);

                layers[i].anchoredPosition = Vector2.SmoothDamp(
                    layers[i].anchoredPosition,
                    targetPosition,
                    ref velocities[i],
                    smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }
        }

        private Vector2 ClampToBackgroundBounds(Vector2 target, Vector2 restingPosition)
        {
            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null || background == null) return target;

            // Background needs to be larger than the Canvas for any visible movement.
            // The available oversized area becomes the maximum legal movement.
            Vector2 canvasSize = canvasRect.rect.size;
            Vector3 scale = background.localScale;
            Vector2 backgroundSize = new Vector2(
                background.rect.width * Mathf.Abs(scale.x),
                background.rect.height * Mathf.Abs(scale.y));
            Vector2 maxOffset = new Vector2(
                Mathf.Max(0f, (backgroundSize.x - canvasSize.x) * 0.5f),
                Mathf.Max(0f, (backgroundSize.y - canvasSize.y) * 0.5f));

            return new Vector2(
                Mathf.Clamp(target.x, restingPosition.x - maxOffset.x, restingPosition.x + maxOffset.x),
                Mathf.Clamp(target.y, restingPosition.y - maxOffset.y, restingPosition.y + maxOffset.y));
        }

    }

