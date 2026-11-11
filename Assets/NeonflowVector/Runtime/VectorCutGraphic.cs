using UnityEngine;
using UnityEngine.UI;

namespace Neonflow.VectorUI
{
    // Ratio is tan(angle), independent of width. The fringe is measured in screen pixels.
    [RequireComponent(typeof(CanvasRenderer), typeof(RectTransform))]
    public class VectorCutGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        public const float MenuSlope = .2679492f;
        [Range(0, .45f)]
        public float cutRatio = .36f;
        public bool accent;
        public Color accentColor = new Color(0, .91f, .95f, 1);
        Vector2 pixelScale = Vector2.one;
        public void RefreshScreenMetrics()
        {
            var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var origin = RectTransformUtility.WorldToScreenPoint(cam, transform.TransformPoint(Vector3.zero));
            var scale = new Vector2((RectTransformUtility.WorldToScreenPoint(cam, transform.TransformPoint(Vector3.right)) - origin).magnitude, (RectTransformUtility.WorldToScreenPoint(cam, transform.TransformPoint(Vector3.up)) - origin).magnitude);
            scale.x = Mathf.Max(.0001f, scale.x);
            scale.y = Mathf.Max(.0001f, scale.y);
            if ((pixelScale - scale).sqrMagnitude > .000001f)
            {
                pixelScale = scale;
                SetVerticesDirty();
            }
        }

        void LateUpdate() => RefreshScreenMetrics();
        public float CutDistance => Mathf.Min(rectTransform.rect.height * cutRatio * pixelScale.y / pixelScale.x, Mathf.Max(0, rectTransform.rect.width - 1));

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float c = CutDistance;
            float fringe = GetComponent<Mask>() ? 0 : 1.25f / pixelScale.x;
            Polygon(vh, new[] { new Vector2(r.xMin + c, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - c, r.yMin), new Vector2(r.xMin, r.yMin) }, color, fringe);
            if (accent)
            {
                float w = Mathf.Min(10, r.width * .025f);
                Polygon(vh, new[] { new Vector2(r.xMin + c, r.yMax), new Vector2(r.xMin + c + w, r.yMax), new Vector2(r.xMin + w, r.yMin), new Vector2(r.xMin, r.yMin) }, accentColor, fringe);
            }
        }

        static void Polygon(VertexHelper vh, Vector2[] p, Color tint, float fringe)
        {
            int start = vh.currentVertCount;
            var outer = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                var a = (p[i] - p[(i + 3) % 4]).normalized;
                var b = (p[(i + 1) % 4] - p[i]).normalized;
                var na = new Vector2(-a.y, a.x);
                var nb = new Vector2(-b.y, b.x);
                var offset = (na + nb) * (fringe * .5f / Mathf.Max(.01f, 1 + Vector2.Dot(na, nb)));
                outer[i] = p[i] + offset;
                vh.AddVert(p[i] - offset, tint, Vector2.zero);
            }

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
            if (fringe <= 0)
                return;
            var clear = tint;
            clear.a = 0;
            for (int i = 0; i < 4; i++)
                vh.AddVert(outer[i], clear, Vector2.zero);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                vh.AddTriangle(start + i, start + 4 + i, start + 4 + j);
                vh.AddTriangle(start + i, start + 4 + j, start + j);
            }
        }

        public bool IsRaycastLocationValid(Vector2 p, Camera cam)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, p, cam, out var q))
                return false;
            var r = rectTransform.rect;
            float c = CutDistance, u = Mathf.InverseLerp(r.yMin, r.yMax, q.y);
            return q.y >= r.yMin && q.y <= r.yMax && q.x >= r.xMin + c * u && q.x <= r.xMax - c + c * u;
        }
    }
}
