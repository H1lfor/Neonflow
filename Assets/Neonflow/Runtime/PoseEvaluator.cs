using System;
using UnityEngine;

namespace Neonflow
{
    [Serializable]
    public class Landmark
    {
        public float x, y, z, v;
        public Vector2 XY => new Vector2(x, y);
    }

    [Serializable]
    public class AudioDevice
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class TrackingFrame
    {
        public string kind, error, command, preview;
        public int epoch;
        public double time;
        public Landmark[] pose, left, right;
        public AudioDevice[] devices;
    }

    public static class PoseEvaluator
    {
        // Screen coordinates, shoulder midpoint origin, shoulder width one, Y up.
        public static Vector2[] Template(string id)
        {
            switch (id)
            {
                case "arms_up":
                    return V(-.65f, .65f, -.65f, 1.35f, .65f, .65f, .65f, 1.35f);
                case "arms_v":
                    return V(-.95f, .45f, -1.35f, 1f, .95f, .45f, 1.35f, 1f);
                case "arms_t":
                    return V(-1.1f, 0, -1.75f, 0, 1.1f, 0, 1.75f, 0);
                case "elbows_up":
                    return V(-1.05f, 0, -1.05f, .8f, 1.05f, 0, 1.05f, .8f);
                case "cross_chest":
                    return V(-.65f, -.5f, .4f, -.25f, .65f, -.5f, -.4f, -.25f);
                case "clap":
                    return V(-.7f, -.4f, -.06f, -.15f, .7f, -.4f, .06f, -.15f);
                case "reach_left":
                    return V(-1.1f, 0, -1.7f, .05f, 0, -.25f, -1.15f, -.3f);
                case "reach_right":
                    return V(0, -.25f, 1.15f, -.3f, 1.1f, 0, 1.7f, .05f);
                case "diagonal_left":
                    return V(-1f, .45f, -1.5f, .95f, 1f, -.5f, 1.5f, -1f);
                case "diagonal_right":
                    return V(-1f, -.5f, -1.5f, -1f, 1f, .45f, 1.5f, .95f);
                default:
                    return V(-.65f, -.65f, -.65f, -1.3f, .65f, -.65f, .65f, -1.3f);
            }
        }

        static Vector2[] V(params float[] a)
        {
            var r = new Vector2[a.Length / 2];
            for (int i = 0; i < r.Length; i++)
                r[i] = new Vector2(a[2 * i], a[2 * i + 1]);
            return r;
        }

        public static float Evaluate(Cue c, TrackingFrame f, float neutral, float fullAccuracy = .64f)
        {
            if (f?.pose == null || f.pose.Length < 17)
                return 0;
            var p = f.pose;
            if (p[11].v < .5 || p[12].v < .5)
                return 0;
            float w = Vector2.Distance(p[11].XY, p[12].XY);
            if (w < .06)
                return 0;
            var center = (p[11].XY + p[12].XY) * .5f;
            var target = Template(c.pose);
            float body = 0, visibleBody = 1;
            if (c.pose.StartsWith("shoulders"))
            {
                if (p[0].v < .5)
                    return 0;
                float dist = (center.y - p[0].y) / w;
                float expected = neutral + (c.pose == "shoulders_up" ? -.16f : .08f);
                body = Mathf.Clamp01(1 - Math.Abs(dist - expected) / .22f);
            }
            else
            {
                int[] joints =
                {
                    13,
                    15,
                    14,
                    16
                };
                int required = 0, visible = 0;
                for (int i = 0; i < 4; i++)
                {
                    if ((i == 1 || i == 3) && target[i].y < -.8f)
                        continue;
                    required++;
                    var a = p[joints[i]];
                    if (a.v >= .5f)
                        visible++;
                    var pos = (a.XY - center) / w;
                    pos.y = -pos.y;
                    body += a.v < .5 ? 0 : Mathf.Clamp01(1 - Vector2.Distance(pos, target[i]) / .85f);
                }

                body /= required;
                visibleBody = (float)visible / required;
            }

            float total = body * 2, weight = 2, available = visibleBody * 2;
            if (!string.IsNullOrEmpty(c.left))
            {
                total += Hand(c.left, f.left);
                if (HandVisible(f.left))
                    available++;
                weight++;
            }

            if (!string.IsNullOrEmpty(c.right))
            {
                total += Hand(c.right, f.right);
                if (HandVisible(f.right))
                    available++;
                weight++;
            }

            // Boost the live HUD and scoring together, retaining the penalty for missing required tracking.
            return Mathf.Min(available / weight, Mathf.Clamp01(total / weight / Mathf.Clamp(fullAccuracy, .01f, 1)));
        }

        public static bool[] Fingers(string id)
        {
            switch (id)
            {
                case "open":
                    return new[]
                    {
                        true,
                        true,
                        true,
                        true,
                        true
                    };
                case "point":
                case "point_side":
                    return new[]
                    {
                        false,
                        true,
                        false,
                        false,
                        false
                    };
                case "peace":
                    return new[]
                    {
                        false,
                        true,
                        true,
                        false,
                        false
                    };
                case "three":
                    return new[]
                    {
                        false,
                        true,
                        true,
                        true,
                        false
                    };
                case "four":
                    return new[]
                    {
                        false,
                        true,
                        true,
                        true,
                        true
                    };
                case "thumb_up":
                    return new[]
                    {
                        true,
                        false,
                        false,
                        false,
                        false
                    };
                case "rock":
                    return new[]
                    {
                        false,
                        true,
                        false,
                        false,
                        true
                    };
                case "l_shape":
                    return new[]
                    {
                        true,
                        true,
                        false,
                        false,
                        false
                    };
                default:
                    return new bool[5];
            }
        }

        static bool HandVisible(Landmark[] h)
        {
            if (h == null || h.Length != 21)
                return false;
            foreach (var x in h)
                if (x == null || x.x < 0 || x.x > 1 || x.y < 0 || x.y > 1)
                    return false;
            return true;
        }

        public static float Hand(string id, Landmark[] h)
        {
            if (!HandVisible(h))
                return 0;
            var wanted = Fingers(id);
            float score = 0;
            for (int f = 0; f < 5; f++)
            {
                int tip = f == 0 ? 4 : 8 + (f - 1) * 4;
                int joint = tip - 2;
                float ratio = Vector2.Distance(h[tip].XY, h[0].XY) / Math.Max(.001f, Vector2.Distance(h[joint].XY, h[0].XY));
                float extended = f == 0 ? Mathf.InverseLerp(1.05f, 1.45f, ratio) : Mathf.InverseLerp(1.05f, 1.55f, ratio);
                score += wanted[f] ? extended : 1 - extended;
            }

            score /= 5;
            if (id == "point" || id == "point_side" || id == "thumb_up")
            {
                int tip = id == "thumb_up" ? 4 : 8;
                Vector2 d = (h[tip].XY - h[tip - 2].XY).normalized;
                float dir = id == "point_side" ? Math.Abs(d.x) : Math.Max(0, -d.y);
                score *= Mathf.InverseLerp(.35f, .85f, dir);
            }

            return score;
        }
    }
}
