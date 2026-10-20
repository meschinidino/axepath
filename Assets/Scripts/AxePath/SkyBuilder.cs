// Procedural night sky: gradient, Milky Way band, stars, and a moon,
// painted into an equirect texture for the built-in panoramic skybox.
// Returns null if the skybox shader is unavailable (caller falls back
// to a solid clear color).
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
            var top = new Color(0.012f, 0.02f, 0.05f);
            var mid = new Color(0.05f, 0.07f, 0.14f);
            var hor = new Color(0.10f, 0.13f, 0.22f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                Color c = v < 0.5f
                    ? Color.Lerp(hor, mid, v * 2f)
                    : Color.Lerp(mid, top, (v - 0.5f) * 2f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    // Milky Way: a great circle reads as a sine in equirect.
                    float band = 0.62f + 0.18f * Mathf.Sin(u * 6.28318f + 0.6f);
                    float bd = (v - band) / 0.085f;
                    float lift = Mathf.Exp(-bd * bd);
                    // Faint warm kiss low on one azimuth: far-off hearths.
                    float az = 0.5f + 0.5f * Mathf.Cos((u - 0.15f) * 6.28318f);
                    float hz = Mathf.Exp(-v * v / 0.004f) * az * az;
                    tex.SetPixel(x, y, c + new Color(0.055f, 0.06f, 0.10f) * lift
                        + new Color(0.10f, 0.055f, 0.02f) * hz);
                }
            }
            var rng = new System.Random(42);
            for (int i = 0; i < 1000; i++)
            {
                int x = rng.Next(w);
                float u = x / (float)(w - 1);
                // Two-thirds of the stars crowd the Milky Way band.
                float v;
                if (i % 3 != 0)
                {
                    float band = 0.62f + 0.18f * Mathf.Sin(u * 6.28318f + 0.6f);
                    v = band + ((float)rng.NextDouble() - 0.5f) * 0.22f;
                }
                else
                {
                    v = 0.12f + 0.88f * Mathf.Pow((float)rng.NextDouble(), 0.6f);
                }
                int y = Mathf.Clamp((int)(v * h), 0, h - 1);
                float b = 0.45f + (float)rng.NextDouble() * 0.55f;
                float warm = (float)rng.NextDouble();
                var sc = new Color(b, b * (0.88f + warm * 0.12f), b * (1.02f - warm * 0.22f));
                tex.SetPixel(x, y, sc);
                if (b > 0.8f)
                {
                    // Bright stars bloom and a few throw a cross sparkle.
                    if (x + 1 < w) tex.SetPixel(x + 1, y, sc * 0.7f);
                    if (y + 1 < h) tex.SetPixel(x, y + 1, sc * 0.5f);
                    if (b > 0.94f)
                    {
                        if (x + 2 < w) tex.SetPixel(x + 2, y, sc * 0.4f);
                        if (x - 1 >= 0) tex.SetPixel(x - 1, y, sc * 0.4f);
                        if (y + 2 < h) tex.SetPixel(x, y + 2, sc * 0.4f);
                        if (y - 1 >= 0) tex.SetPixel(x, y - 1, sc * 0.4f);
                    }
                }
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
                    if (d < 90f)
                    {
                        Color base_ = tex.GetPixel(x, y);
                        if (d < 26f)
                        {
                            // Disc with a soft terminator: full face, dim limb.
                            float limb = 1f - 0.25f * (d / 26f) * (d / 26f);
                            tex.SetPixel(x, y, new Color(0.92f, 0.94f, 0.96f) * limb);
                        }
                        else
                        {
                            float k = 1f - (d - 26f) / 64f;
                            tex.SetPixel(x, y, base_ + new Color(0.30f, 0.32f, 0.38f) * k * k);
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
