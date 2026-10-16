// Procedural night sky: gradient, stars, and a moon, painted into an
// equirect texture for the built-in panoramic skybox. Returns null if the
// skybox shader is unavailable (caller falls back to a solid clear color).
using UnityEngine;

namespace AxePath
{
    public static class SkyBuilder
    {
        public static Material BuildNightSky()
        {
            var sh = Shader.Find("Skybox/Panoramic");
            if (sh == null) return null;
            int w = 1024;
            int h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var top = new Color(0.027f, 0.043f, 0.086f);
            var mid = new Color(0.078f, 0.106f, 0.188f);
            var hor = new Color(0.137f, 0.173f, 0.267f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                Color c = v < 0.5f
                    ? Color.Lerp(hor, mid, v * 2f)
                    : Color.Lerp(mid, top, (v - 0.5f) * 2f);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            var rng = new System.Random(42);
            for (int i = 0; i < 380; i++)
            {
                int x = rng.Next(w);
                float v = 0.25f + 0.75f * Mathf.Pow((float)rng.NextDouble(), 0.6f);
                int y = Mathf.Clamp((int)(v * h), 0, h - 1);
                float b = 0.5f + (float)rng.NextDouble() * 0.5f;
                float warm = (float)rng.NextDouble();
                var sc = new Color(b, b * (0.92f + warm * 0.08f), b * (1f - warm * 0.12f));
                tex.SetPixel(x, y, sc);
                if (b > 0.85f && x + 1 < w) tex.SetPixel(x + 1, y, sc);
            }
            int mx = (int)(w * 0.68f);
            int my = (int)(h * 0.72f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - mx;
                    float dy = (y - my) * 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < 60f)
                    {
                        Color base_ = tex.GetPixel(x, y);
                        if (d < 22f)
                        {
                            tex.SetPixel(x, y, new Color(0.91f, 0.93f, 0.95f));
                        }
                        else
                        {
                            float k = 1f - (d - 22f) / 38f;
                            tex.SetPixel(x, y, base_ + new Color(0.25f, 0.27f, 0.32f) * k * k);
                        }
                    }
                }
            }
            tex.Apply();
            var mat = new Material(sh);
            mat.SetTexture("_MainTex", tex);
            return mat;
        }
    }
}
