// Axe Path — perf probe. Measures, never gates tightly: prints
// PERF lines (fps, draw calls, triangles, memory, object counts)
// at representative spots so reviews use numbers, not vibes.
// Bounds are 10x-loose tripwires against total collapse, not targets.
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using UnityEngine.TestTools;
using AxePath;

public class PerfProbe
{
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return TestBoot.WaitForReady();
    }

    private static float Face(float fx, float fz, float tx, float tz)
    {
        return Mathf.Atan2(-(tx - fx), -(tz - fz));
    }

    private static ProfilerRecorder drawRec;
    private static ProfilerRecorder setPassRec;
    private static ProfilerRecorder triRec;

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

    private static long ReadCounter(ref ProfilerRecorder r)
    {
        try { return r.Valid ? r.LastValue : -1; }
        catch { return -1; }
    }

    private static IEnumerator SampleFrames(string name, int frames, StringBuilder sb)
    {
        for (int i = 0; i < 30; i++) yield return null; // settle
        float dtSum = 0f;
        for (int i = 0; i < frames; i++)
        {
            dtSum += Time.unscaledDeltaTime;
            yield return null;
        }
        float fps = frames / Mathf.Max(0.001f, dtSum);
        long draws = ReadCounter(ref drawRec);
        long tris = ReadCounter(ref triRec);
        long setPass = ReadCounter(ref setPassRec);
        sb.AppendFormat("{0}: fps={1:F1} draws={2} setpass={3} tris={4} monoMB={5:F1} gfxMB={6:F1}\n",
            name, fps, draws, setPass, tris,
            Profiler.GetMonoHeapSizeLong() / 1048576f,
            Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f);
        Assert.Greater(fps, 5f, name + ": frame loop stalled");
    }

    [UnityTest]
    public IEnumerator MeasureSpots()
    {
        var S = Game.Session;
        S.NewGame(4);
        S.W.axe.have = true;
        drawRec = OpenCounter(ProfilerCategory.Render, new string[] { "Draw Calls", "Draw Calls Count" });
        setPassRec = OpenCounter(ProfilerCategory.Render, new string[] { "SetPass Calls", "SetPass Calls Count" });
        triRec = OpenCounter(ProfilerCategory.Render, new string[] { "Triangles" });
        int renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;
        int total = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
        var sb = new StringBuilder();
        sb.AppendFormat("objects: transforms={0} meshrenderers={1} trees={2}\n", total, renderers, Game.World.refs.treeCount);
        S.Player.Teleport(0f, 210f, Face(0f, 210f, 0f, 150f));
        yield return SampleFrames("spawn-look-south", 90, sb);
        S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
        yield return SampleFrames("landmark-pine", 90, sb);
        S.Player.Teleport(104f, 184f, Face(104f, 184f, 104f, 178f));
        yield return SampleFrames("mill", 90, sb);
        S.Player.Teleport(0f, -280f, Face(0f, -280f, 0f, -315f));
        yield return SampleFrames("cave-mouth", 90, sb);
        S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        yield return SampleFrames("combat-motes-live", 60, sb);
        S.Wolves[0].brain.state = "chase";
        S.Wolves[0].brain.pos = new Vec2(-70f, 2f);
        S.Player.Teleport(-70f, 3.5f, Face(-70f, 3.5f, -70f, 2f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        yield return SampleFrames("wolf-fight", 60, sb);
        Debug.Log("PERF-BEGIN\n" + sb.ToString() + "PERF-END");
        drawRec.Dispose();
        setPassRec.Dispose();
        triRec.Dispose();
    }
}
