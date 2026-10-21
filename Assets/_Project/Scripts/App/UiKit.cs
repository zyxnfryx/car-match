using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // uGUI port of Flock Five's IMGUI text helpers: FitFont (largest size that fits the box) and
    // StampOutlined (a thick dark stamp ring under bright text), plus a vertical gradient and a bounce-in.
    public static class UiKit
    {
        // Flock Five FitFont: shrink from 'max' until the preferred width fits.
        public static int FitFont(Text text, float width, int max, int min = 18)
        {
            int size = max;
            var settings = text.GetGenerationSettings(new Vector2(width, 0f));
            while (size > min)
            {
                settings.fontSize = size;
                float w = text.cachedTextGeneratorForLayout.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit;
                if (w <= width) break;
                size -= 2;
            }
            text.fontSize = size;
            return size;
        }

        // Flock Five StampOutlined: the outline is stamped in a ring (8 directions x 2 radii) so it reads
        // as one chunky rim, then a soft drop shadow.
        public static void StampOutlined(Text text, Color rim, float radius, Color shadow)
        {
            var sh = text.gameObject.AddComponent<Shadow>();
            sh.effectColor = shadow;
            sh.effectDistance = new Vector2(0f, -radius * 1.8f);
            for (int ring = 0; ring < 2; ring++)
            {
                float r = radius * (ring == 0 ? 1f : 0.55f);
                var o = text.gameObject.AddComponent<Outline8>();
                o.effectColor = rim;
                o.effectDistance = new Vector2(r, r);
            }
        }

        public static IEnumerator BounceIn(RectTransform rt, float delay)
        {
            if (rt == null) yield break;
            rt.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            const float dur = 0.55f;
            while (t < dur && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                // Elastic-out: overshoot, settle.
                float k = 1f - Mathf.Pow(2f, -9f * u) * Mathf.Cos(u * Mathf.PI * 3.2f);
                float squash = 1f + 0.12f * Mathf.Sin(u * Mathf.PI * 3f) * (1f - u);
                rt.localScale = new Vector3(k * squash, k / squash, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }
    }

    // Outline in 8 directions (uGUI's Outline does 4).
    public sealed class Outline8 : Shadow
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);
            int count = verts.Count;
            var d = effectDistance;
            var offsets = new[] { new Vector2(d.x, 0), new Vector2(-d.x, 0), new Vector2(0, d.y), new Vector2(0, -d.y),
                new Vector2(d.x, d.y) * 0.72f, new Vector2(-d.x, d.y) * 0.72f, new Vector2(d.x, -d.y) * 0.72f, new Vector2(-d.x, -d.y) * 0.72f };
            int needed = count * (offsets.Length + 1);
            if (verts.Capacity < needed) verts.Capacity = needed;
            int start = 0;
            for (int i = 0; i < offsets.Length; i++)
            {
                ApplyShadowZeroAlloc(verts, effectColor, start, verts.Count, offsets[i].x, offsets[i].y);
                start = verts.Count - count;
                // ApplyShadowZeroAlloc appends the shadow copy behind; keep originals last.
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }

    // Vertical two-color gradient on a Text (top -> bottom).
    public sealed class Gradient2 : BaseMeshEffect
    {
        public Color Top = Color.white;
        public Color Bottom = Color.white;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var v = new UIVertex();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                min = Mathf.Min(min, v.position.y);
                max = Mathf.Max(max, v.position.y);
            }
            float h = Mathf.Max(1f, max - min);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                // Only tint the face (white-ish) vertices; outline copies keep their rim color.
                if (v.color.r > 200 && v.color.g > 200 && v.color.b > 200)
                {
                    float u = (v.position.y - min) / h;
                    v.color = Color.Lerp(Bottom, Top, u);
                    vh.SetUIVertex(v, i);
                }
            }
        }
    }
}
