using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.AfterFeature
{
    /// <summary>Three editable UI stars, drawn as a mesh; no texture or font dependency.</summary>
    [AddComponentMenu("After Feature/Level Stars Graphic")]
    public sealed class LevelStarsGraphic : MaskableGraphic
    {
        [SerializeField, Range(0, 3)] private int stars;
        [SerializeField] private Color earnedColor = new Color(1f, 0.69f, 0.08f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.39f, 0.34f, 0.24f, 0.5f);
        public int Stars => stars;

        public void SetStars(int count)
        {
            count = Mathf.Clamp(count, 0, 3);
            if (stars == count) return;
            stars = count;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float cell = rect.width / 3f;
            float radius = Mathf.Max(0f, Mathf.Min(cell * 0.43f, rect.height * 0.48f));
            for (int star = 0; star < 3; star++)
            {
                var centre = new Vector2(rect.xMin + cell * (star + 0.5f), rect.center.y);
                Color tint = (star < stars ? earnedColor : emptyColor) * color;
                int first = vh.currentVertCount;
                vh.AddVert(centre, tint, Vector2.zero);
                for (int point = 0; point < 10; point++)
                {
                    float angle = (90f - point * 36f) * Mathf.Deg2Rad;
                    float size = point % 2 == 0 ? radius : radius * 0.44f;
                    vh.AddVert(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size, tint, Vector2.zero);
                }
                for (int point = 0; point < 10; point++)
                    vh.AddTriangle(first, first + 1 + point, first + 1 + (point + 1) % 10);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            stars = Mathf.Clamp(stars, 0, 3);
            raycastTarget = false;
            base.OnValidate();
        }
#endif
    }
}
