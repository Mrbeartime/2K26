using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[AddComponentMenu("UI/Effects/Sprite Outline Padding")]
public sealed class SpriteOutlinePadding : BaseMeshEffect
{
    private const string SourceOutlineKeyword = "_SOURCE_IMAGE_OUTLINE";
    private const float FilterMarginPixels = 1f;
    private static readonly int ThicknessProperty = Shader.PropertyToID("_OutlineThickness");
    private Vector2 currentPadding;

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.preWillRenderCanvases += RefreshPadding;
    }

    protected override void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshPadding;
        base.OnDisable();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount != 4)
            return;

        Vector2 padding = CalculatePadding();
        currentPadding = padding;
        if (padding == Vector2.zero)
            return;

        // A Simple Image quad starts at the bottom-left, with the opposite corner at index 2.
        UIVertex bottomLeft = default;
        UIVertex topRight = default;
        vertices.PopulateUIVertex(ref bottomLeft, 0);
        vertices.PopulateUIVertex(ref topRight, 2);

        Vector2 size = topRight.position - bottomLeft.position;
        if (size.x <= 0f || size.y <= 0f)
            return;

        Vector2 center = (bottomLeft.position + topRight.position) * 0.5f;
        Vector2 uvSpan = topRight.uv0 - bottomLeft.uv0;
        Vector2 uvPadding = Vector2.Scale(uvSpan, new Vector2(padding.x / size.x, padding.y / size.y));

        // Extend the quad and UVs together so the sprite footprint stays in the same place.
        UIVertex vertex = default;
        for (int index = 0; index < vertices.currentVertCount; index++)
        {
            vertices.PopulateUIVertex(ref vertex, index);
            float xDirection = vertex.position.x < center.x ? -1f : 1f;
            float yDirection = vertex.position.y < center.y ? -1f : 1f;
            vertex.position.x += xDirection * padding.x;
            vertex.position.y += yDirection * padding.y;
            vertex.uv0.x += xDirection * uvPadding.x;
            vertex.uv0.y += yDirection * uvPadding.y;
            vertices.SetUIVertex(vertex, index);
        }
    }

    private void RefreshPadding()
    {
        Vector2 padding = CalculatePadding();
        if (padding == currentPadding)
            return;

        currentPadding = padding;
        graphic.SetVerticesDirty();
    }

    private Vector2 CalculatePadding()
    {
        if (graphic is not Image image || image.type != Image.Type.Simple || image.useSpriteMesh)
            return Vector2.zero;

        Sprite sprite = image.overrideSprite;
        Material material = image.material;
        Canvas canvas = image.canvas;
        if (!sprite || sprite.packed || !canvas || !material ||
            !material.IsKeywordEnabled(SourceOutlineKeyword) || !material.HasProperty(ThicknessProperty))
            return Vector2.zero;

        float thickness = material.GetFloat(ThicknessProperty);
        if (thickness <= 0f)
            return Vector2.zero;

        Canvas rootCanvas = canvas.rootCanvas;
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (rootCanvas.renderMode == RenderMode.WorldSpace && !camera)
        {
            camera = Camera.main;
            if (!camera)
                return Vector2.zero;
        }

        RectTransform rect = image.rectTransform;
        Vector3 center = rect.rect.center;
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(center));
        Vector2 screenX = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(center + Vector3.right)) - screenCenter;
        Vector2 screenY = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(center + Vector3.up)) - screenCenter;
        float area = Mathf.Abs(screenX.x * screenY.y - screenX.y * screenY.x);
        if (area <= 0.000001f)
            return Vector2.zero;

        // Project onto each edge's normal so non-uniform scale still leaves enough room.
        float pixelPadding = thickness + FilterMarginPixels;
        return new Vector2(
            Mathf.Ceil(pixelPadding * screenY.magnitude / area),
            Mathf.Ceil(pixelPadding * screenX.magnitude / area));
    }
}
