using UnityEngine;
using UnityEngine.UI;

namespace Neonflow
{
    public class TrackingOverlay : MaskableGraphic
    {
        public Recognition Source;
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        void Update()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Source == null || !Source.Tracked)
                return;
            var f = Source.Latest;
            if (f.pose?.Length > 16)
            {
                int[] edges =
                {
                    11,
                    12,
                    11,
                    13,
                    13,
                    15,
                    12,
                    14,
                    14,
                    16
                };
                for (int i = 0; i < edges.Length; i += 2)
                    Line(vh, f.pose[edges[i]], f.pose[edges[i + 1]], new Color32(20, 240, 230, 255));
            }

            Hand(vh, f.left);
            Hand(vh, f.right);
        }

        void Hand(VertexHelper vh, Landmark[] h)
        {
            if (h?.Length != 21)
                return;
            for (int f = 0; f < 5; f++)
            {
                int b = f == 0 ? 1 : 5 + (f - 1) * 4;
                Line(vh, h[0], h[b], new Color32(255, 70, 170, 255));
                for (int k = 0; k < 3; k++)
                    Line(vh, h[b + k], h[b + k + 1], new Color32(255, 70, 170, 255));
            }
        }

        void Line(VertexHelper vh, Landmark a, Landmark b, Color32 c)
        {
            if (a.v < .5 || b.v < .5)
                return;
            var r = rectTransform.rect;
            Vector2 p = new Vector2(r.xMin + a.x * r.width, r.yMax - a.y * r.height), q = new Vector2(r.xMin + b.x * r.width, r.yMax - b.y * r.height);
            var n = new Vector2(-(q - p).y, (q - p).x).normalized * 1.8f;
            int i = vh.currentVertCount;
            vh.AddVert(p - n, c, Vector2.zero);
            vh.AddVert(p + n, c, Vector2.zero);
            vh.AddVert(q + n, c, Vector2.zero);
            vh.AddVert(q - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
