// Axe Path — gated perf sampler. With -perfLog on the command line,
// logs one PERF line (fps, draw calls, triangles, memory) to the
// player log every 120 frames, from boot through title and play.
// Without the flag it self-disables and costs nothing.
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace AxePath
{
    public class PerfLog : MonoBehaviour
    {
        private ProfilerRecorder drawRec;
        private ProfilerRecorder triRec;
        private int frames;
        private float dtSum;

        void Awake()
        {
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-perfLog") want = true;
                if (a == "-noShadows") QualitySettings.shadows = ShadowQuality.Disable;
            }
            if (!want) { enabled = false; return; }
            drawRec = OpenCounter(ProfilerCategory.Render, new string[] { "Draw Calls", "Draw Calls Count" });
            triRec = OpenCounter(ProfilerCategory.Render, new string[] { "Triangles" });
            Debug.Log("PERFLOG: sampling every 120 frames (quality=" + QualitySettings.GetQualityLevel() +
                " " + Screen.width + "x" + Screen.height + ")");
        }

        void OnDestroy()
        {
            drawRec.Dispose();
            triRec.Dispose();
        }

        void Update()
        {
            dtSum += Time.unscaledDeltaTime;
            frames++;
            if (frames < 120) return;
            float fps = frames / Mathf.Max(0.001f, dtSum);
            long draws = (drawRec.Valid) ? drawRec.LastValue : -1;
            long tris = (triRec.Valid) ? triRec.LastValue : -1;
            Debug.Log("PERFLOG: fps=" + fps.ToString("F1") + " draws=" + draws + " tris=" + tris +
                " monoMB=" + (Profiler.GetMonoHeapSizeLong() / 1048576f).ToString("F1") +
                " gfxMB=" + (Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f).ToString("F1"));
            frames = 0;
            dtSum = 0f;
        }

        private static ProfilerRecorder OpenCounter(ProfilerCategory cat, string[] names)
        {
            foreach (var n in names)
            {
                try
                {
                    var r = ProfilerRecorder.StartNew(cat, n);
                    if (r.Valid) return r;
                    r.Dispose();
                }
                catch { }
            }
            return default(ProfilerRecorder);
        }
    }
}
