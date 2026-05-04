using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// HSL 颜色表示及 RGB 转换工具。H/S/L 均为 [0,1]。
    /// </summary>
    public struct HslColor
    {
        public float h; // 色相 [0,1]
        public float s; // 饱和度 [0,1]
        public float l; // 亮度 [0,1]
        public float a; // 透明度 [0,1]

        public static HslColor FromRgb(Color rgb)
        {
            float r = rgb.r, g = rgb.g, b = rgb.b;
            float max = Mathf.Max(Mathf.Max(r, g), b);
            float min = Mathf.Min(Mathf.Min(r, g), b);
            float delta = max - min;

            float h = 0f, s = 0f, l = (max + min) * 0.5f;

            if (delta > 1e-6f)
            {
                s = l < 0.5f ? delta / (max + min) : delta / (2f - max - min);
                if (max == r)      h = (g - b) / delta + (g < b ? 6f : 0f);
                else if (max == g) h = (b - r) / delta + 2f;
                else               h = (r - g) / delta + 4f;
                h /= 6f;
            }

            return new HslColor { h = h, s = s, l = l, a = rgb.a };
        }

        public Color ToRgb()
        {
            if (s < 1e-6f)
                return new Color(l, l, l, a);

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            return new Color(
                HueToChannel(p, q, h + 1f / 3f),
                HueToChannel(p, q, h),
                HueToChannel(p, q, h - 1f / 3f),
                a
            );
        }

        private static float HueToChannel(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
}
