// Procedural PBR detail: every flat color gains grain, fiber, and relief
// without a single imported asset. Textures are generated once at boot
// (value-noise fBm), tile seamlessly, and tint through Material.color so
// the existing hex palette stays the art direction.
using UnityEngine;

namespace AxePath
{
    public static class TextureFactory
    {
        private static Texture2D grainTex;
        private static Texture2D barkTex;
        private static Texture2D rockTex;
        private static Texture2D groundTex;
        private static Texture2D dirtTex;
        private static Texture2D bumpTex;
        private static bool ready;

        // Tileable value noise: lattice wrapped so edges repeat.
        private static float Hash(int x, int y, int period, int seed)
        {
            int h = (x % period + period) % period * 374761393 + (y % period + period) % period * 668265263 + seed * 974634;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        private static float Noise(float u, float v, int cells, int seed)
        {
            float fx = u * cells, fy = v * cells;
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            float a = Hash(ix, iy, cells, seed), b = Hash(ix + 1, iy, cells, seed);
            float c = Hash(ix, iy + 1, cells, seed), d = Hash(ix + 1, iy + 1, cells, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Fbm(float u, float v, int seed)
        {
            float s = 0f, amp = 0.55f, tot = 0f;
            int[] cells = { 4, 8, 16, 32 };
            for (int i = 0; i < cells.Length; i++)
            {
                s += Noise(u, v, cells[i], seed + i * 7) * amp;
                tot += amp; amp *= 0.55f;
            }
            return s / tot;
        }

        private static Texture2D Make(int size, System.Func<float, float, Color> fn, bool bump = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 4;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = fn(x / (float)size, y / (float)size);
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        public static void Ensure()
        {
            if (ready) return;
            ready = true;
            const int S = 256;
            // Neutral grain: multiplies any tint, reads as fiber/dirt/ash.
            grainTex = Make(S, (u, v) =>
            {
                float n = Fbm(u, v, 11) * 0.55f + Fbm(u * 3f % 1f, v * 3f % 1f, 23) * 0.45f;
                float g = 0.72f + n * 0.5f;
                return new Color(g, g, g);
            });
            // Bark: vertical fiber streaks.
            barkTex = Make(S, (u, v) =>
            {
                float streak = Fbm(u * 6f % 1f, v * 0.7f % 1f, 31);
                float n = Fbm(u, v, 47) * 0.4f + streak * 0.6f;
                float g = 0.55f + n * 0.75f;
                return new Color(g, g * 0.97f, g * 0.92f);
            });
            // Rock: blocky strata + speckle.
            rockTex = Make(S, (u, v) =>
            {
                float band = 0.5f + 0.5f * Mathf.Sin((v * 5f + Fbm(u, v, 53) * 1.4f) * 6.28318f);
                float n = Fbm(u, v, 59) * 0.65f + band * 0.35f;
                float g = 0.6f + n * 0.62f;
                return new Color(g, g, g * 1.03f);
            });
            // Ground: moss/earth mottling with warm dry patches.
            groundTex = Make(S, (u, v) =>
            {
                float n = Fbm(u, v, 67);
                float dry = Mathf.SmoothStep(0.55f, 0.8f, Fbm(u + 0.37f, v + 0.11f, 71));
                Color moss = new Color(0.62f, 0.78f, 0.55f);
                Color earth = new Color(0.95f, 0.82f, 0.62f);
                Color c = Color.Lerp(moss, earth, dry * 0.55f);
                float g = 0.68f + n * 0.55f;
                return new Color(c.r * g, c.g * g, c.b * g);
            });
            // Dirt: warm earth mottling with darker trodden patches.
            // No moss green: paths must read brown against the ground,
            // and stay dark under moonlight (the old shared wood-brown
            // blew out to a cream sheet on the flat discs).
            dirtTex = Make(S, (u, v) =>
            {
                float n = Fbm(u, v, 97);
                float patch = Mathf.SmoothStep(0.45f, 0.75f, Fbm(u + 0.13f, v + 0.41f, 101));
                Color light = new Color(0.58f, 0.47f, 0.35f);
                Color dark = new Color(0.27f, 0.20f, 0.14f);
                Color c = Color.Lerp(light, dark, patch);
                float g = 0.60f + n * 0.65f;
                return new Color(c.r * g, c.g * g, c.b * g);
            });
            bumpTex = Make(S, (u, v) =>
            {
                float n = Fbm(u, v, 83);
                return new Color(n, n, n);
            });
        }

        public static Texture2D Grain { get { Ensure(); return grainTex; } }
        public static Texture2D Bark { get { Ensure(); return barkTex; } }
        public static Texture2D Rock { get { Ensure(); return rockTex; } }
        public static Texture2D Ground { get { Ensure(); return groundTex; } }
        public static Texture2D Dirt { get { Ensure(); return dirtTex; } }

        // Applies the right detail family to a Standard material.
        public static void Dress(Material m, string family)
        {
            Ensure();
            if (m == null) return;
            Texture2D t = grainTex;
            if (family == "bark") t = barkTex;
            else if (family == "rock") t = rockTex;
            else if (family == "ground") t = groundTex;
            else if (family == "dirt") t = dirtTex;
            m.mainTexture = t;
            m.SetTexture("_BumpMap", bumpTex);
            m.SetFloat("_BumpScale", family == "rock" ? 0.6f : 0.35f);
            m.EnableKeyword("_NORMALMAP");
        }

        // Family from the palette's role: browns read as wood, grays as stone.
        public static string FamilyFor(uint hex)
        {
            float r = ((hex >> 16) & 0xff) / 255f, g = ((hex >> 8) & 0xff) / 255f, b = (hex & 0xff) / 255f;
            float spread = Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
            if (spread < 0.08f && r > 0.25f) return "rock";
            if (r > b + 0.08f && r > 0.25f) return "bark";
            return "grain";
        }
    }
}
