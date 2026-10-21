// Tiny procedural audio, no assets. Voices are mixed in OnAudioFilterRead.
// Never throws into the game: every entry point is guarded.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AxePath
{
    public class AudioSynth : MonoBehaviour
    {
        private const int Sine = 0;
        private const int Triangle = 1;
        private const int Square = 2;
        private const int Saw = 3;

        private class Tone
        {
            public float freq;
            public float slideTo;
            public float dur;
            public float t;
            public int type;
            public float gain;
            public float phase;
            public float delay;
        }

        private class Noise
        {
            public float dur;
            public float t;
            public float cutoff;
            public float gain;
            public float lp;
            public float delay;
        }

        private readonly object sync = new object();
        private readonly List<Tone> tones = new List<Tone>();
        private readonly List<Noise> noises = new List<Noise>();
        private System.Random rng = new System.Random(1234);
        private bool windOn;
        private float windLp;
        private int rate = 48000;

        void Awake()
        {
            try
            {
                rate = AudioSettings.outputSampleRate;
                var src = gameObject.AddComponent<AudioSource>();
                var clip = AudioClip.Create("synth", rate, 1, rate, false);
                clip.SetData(new float[rate], 0);
                src.clip = clip;
                src.loop = true;
                src.volume = 1f;
                src.Play();
            }
            catch { /* silent */ }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (sync)
            {
                float dt = 1f / rate;
                for (int s = 0; s < data.Length; s += channels)
                {
                    float m = 0f;
                    for (int i = tones.Count - 1; i >= 0; i--)
                    {
                        var v = tones[i];
                        if (v.delay > 0f) { v.delay -= dt; continue; }
                        v.t += dt;
                        if (v.t >= v.dur) { tones.RemoveAt(i); continue; }
                        float k = v.t / v.dur;
                        float f = v.slideTo > 0f ? v.freq * Mathf.Pow(v.slideTo / v.freq, k) : v.freq;
                        v.phase += 2f * Mathf.PI * f * dt;
                        float p = v.phase / (2f * Mathf.PI);
                        float o;
                        if (v.type == Triangle) o = 2f * Mathf.Abs(2f * (p - Mathf.Floor(p + 0.5f))) - 1f;
                        else if (v.type == Square) o = v.phase % (2f * Mathf.PI) < Mathf.PI ? 0.7f : -0.7f;
                        else if (v.type == Saw) o = 2f * (p - Mathf.Floor(p + 0.5f));
                        else o = Mathf.Sin(v.phase);
                        float env = Mathf.Exp(-v.t * Mathf.Log(Mathf.Max(0.001f, v.gain) / 0.001f) / v.dur);
                        m += o * v.gain * env;
                    }
                    for (int i = noises.Count - 1; i >= 0; i--)
                    {
                        var v = noises[i];
                        if (v.delay > 0f) { v.delay -= dt; continue; }
                        v.t += dt;
                        if (v.t >= v.dur) { noises.RemoveAt(i); continue; }
                        float x = (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - v.t / v.dur);
                        float a = 1f - Mathf.Exp(-2f * Mathf.PI * v.cutoff * dt);
                        v.lp += a * (x - v.lp);
                        m += v.lp * v.gain;
                    }
                    if (windOn)
                    {
                        float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                        float a = 1f - Mathf.Exp(-2f * Mathf.PI * 400f * dt);
                        windLp += a * (x - windLp);
                        m += windLp * 0.05f;
                    }
                    m *= 0.5f;
                    for (int c = 0; c < channels; c++) data[s + c] = m;
                }
            }
        }

        private void Tone_(float freq, float dur, int type, float gain, float slideTo, float delay)
        {
            try
            {
                lock (sync) tones.Add(new Tone { freq = freq, dur = dur, type = type, gain = gain, slideTo = slideTo, delay = delay });
            }
            catch { }
        }

        private void Noise_(float dur, float cutoff, float gain)
        {
            try
            {
                lock (sync) noises.Add(new Noise { dur = dur, cutoff = cutoff, gain = gain });
            }
            catch { }
        }

        public void PlayThunk(float dull01)
        {
            float f = 220f - 120f * Mathf.Clamp01(dull01);
            Tone_(f, 0.12f, Triangle, 0.3f, f * 0.6f, 0f);
            Noise_(0.08f, 1200f, 0.12f);
            if (dull01 < 0.25f) Noise_(0.05f, 2800f, 0.12f);
        }

        public void PlayHit()
        {
            Noise_(0.1f, 900f, 0.2f);
            Tone_(160f, 0.1f, Square, 0.08f, 90f, 0f);
        }

        public void PlayYelp()
        {
            Tone_(640f, 0.16f, Saw, 0.16f, 180f, 0f);
            Noise_(0.12f, 2400f, 0.1f);
        }

        public void PlayKill()
        {
            Tone_(140f, 0.25f, Square, 0.14f, 50f, 0f);
            Tone_(70f, 0.3f, Sine, 0.2f, 40f, 0f);
            Noise_(0.18f, 700f, 0.2f);
        }

        public void PlayHurt()
        {
            Tone_(220f, 0.2f, Saw, 0.12f, 110f, 0f);
            Noise_(0.15f, 500f, 0.18f);
        }

        public void PlaySnarl()
        {
            Tone_(180f, 0.4f, Saw, 0.14f, 460f, 0f);
            Noise_(0.35f, 900f, 0.08f);
        }

        public void PlaySnap()
        {
            Noise_(0.06f, 3200f, 0.22f);
            Tone_(1100f, 0.05f, Square, 0.06f, 700f, 0f);
        }

        public void PlayWhoosh()
        {
            // Loud and long enough to survive the impact transient that
            // fires on the same frame: the rising tail reads post-hit.
            Noise_(0.22f, 1400f, 0.16f);
            Tone_(320f, 0.2f, Sine, 0.12f, 900f, 0f);
        }

        public void PlayGrowl()
        {
            Tone_(70f, 1.1f, Saw, 0.12f, 55f, 0f);
            Noise_(1.0f, 300f, 0.1f);
        }

        public void PlayChase()
        {
            Tone_(140f, 0.25f, Saw, 0.12f, 90f, 0f);
            Tone_(110f, 0.3f, Saw, 0.1f, 70f, 0f);
        }

        public void PlayHowl(float vol)
        {
            float g = Mathf.Clamp01(vol);
            if (g <= 0.01f) return;
            Tone_(280f, 1.8f, Triangle, 0.12f * g, 520f, 0f);
            Tone_(560f, 1.8f, Sine, 0.05f * g, 1040f, 0f);
            Noise_(1.8f, 900f, 0.03f * g);
        }

        public void PlaySharpen()
        {
            Tone_(520f, 0.2f, Sine, 0.15f, 0f, 0f);
            Tone_(780f, 0.35f, Sine, 0.15f, 0f, 0.14f);
        }

        public void PlayDeath() { Tone_(120f, 0.8f, Sine, 0.25f, 40f, 0f); }
        public void PlayClick() { Tone_(660f, 0.06f, Sine, 0.08f, 0f, 0f); }
        public void PlayStep(bool run) { Noise_(0.05f, run ? 700f : 500f, run ? 0.1f : 0.05f); }

        public void StartWind() { try { lock (sync) windOn = true; } catch { } }
        public void StopWind() { try { lock (sync) windOn = false; } catch { } }
    }
}
