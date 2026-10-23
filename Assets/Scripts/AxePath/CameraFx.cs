// Cinematic camera life: zone-graded fog/ambient, breathing vignette
// pulse on damage, and a subtle handheld sway. Pure zone math is
// EditMode-testable; the MonoBehaviour only applies it.
using UnityEngine;

namespace AxePath
{
    public static class ZoneGrade
    {
        // 0 village (warm, clear), 1 forest (cool, thick), 2 cave (ember, close).
        public static float CaveWeight(float z) { return Mathf.Clamp01((-260f - z) / 30f); }
        public static float ForestWeight(float z)
        {
            float inF = Mathf.Clamp01((150f - z) / 40f);
            float outF = Mathf.Clamp01((z + 290f) / 30f);
            return Mathf.Clamp(inF * (1f - CaveWeight(z)), 0f, 1f) * (z > -290f ? 1f : outF + (1f - outF));
        }

        public static Color FogFor(float z)
        {
            Color village = MeshUtil.C(0x232f4a);
            Color forest = MeshUtil.C(0x14202e);
            Color cave = MeshUtil.C(0x2a1a12);
            Color c = Color.Lerp(village, forest, Mathf.Clamp01((150f - z) / 200f));
            return Color.Lerp(c, cave, CaveWeight(z));
        }

        public static float FogDensityFor(float z)
        {
            float d = Mathf.Lerp(0.0011f, 0.0021f, Mathf.Clamp01((150f - z) / 200f));
            return Mathf.Lerp(d, 0.0016f, CaveWeight(z));
        }

        public static float AmbientFor(float z)
        {
            float a = Mathf.Lerp(0.85f, 0.62f, Mathf.Clamp01((150f - z) / 200f));
            return Mathf.Lerp(a, 0.5f, CaveWeight(z));
        }
    }

    public class CameraFx : MonoBehaviour
    {
        private Transform camT;
        private float hurtPulse;
        private float t;
        public float HurtPulse { get { return hurtPulse; } }

        public void Bind(Camera cam) { camT = cam.transform; }
        public void PulseHurt() { hurtPulse = 1f; }

        void Update()
        {
            if (camT == null) return;
            t += Time.deltaTime;
            hurtPulse = Mathf.Max(0f, hurtPulse - Time.deltaTime * 1.6f);
            float z = camT.position.z;
            RenderSettings.fogColor = ZoneGrade.FogFor(z);
            RenderSettings.fogDensity = ZoneGrade.FogDensityFor(z);
            // Hurt pulse dips the world briefly; zone grade is the base.
            RenderSettings.ambientIntensity = ZoneGrade.AmbientFor(z) * (1f - hurtPulse * 0.35f);
        }
    }
}
