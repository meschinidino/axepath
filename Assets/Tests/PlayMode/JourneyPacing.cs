// Journey pacing: pins the tune the climb is balanced on — dullness
// earned per chop, wolf audibility from the path walking vs running,
// and the escape inequality (you can always outrun a chase).
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AxePath;

public class JourneyPacing
{
    [UnitySetUp]
    public IEnumerator WaitForBoot()
    {
        yield return TestBoot.WaitForReady();
    }

    private static float Face(float fx, float fz, float tx, float tz)
    {
        return Mathf.Atan2(-(tx - fx), -(tz - fz));
    }

    [UnityTest]
    public IEnumerator ChopPacing()
    {
        var S = Game.Session;
        S.NewGame(9);
        S.Player.Teleport(-30f, 178.5f, 0f);
        S.TryInteract();
        Assert.IsTrue(S.W.axe.have);
        // Six chops at the landmark pine: trees never deplete.
        for (int i = 0; i < 6; i++)
        {
            S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
            S.TryAttack();
            yield return new WaitForSeconds(0.7f);
        }
        Assert.AreEqual(6, S.W.skill.chops);
        Assert.AreEqual(54f, S.W.axe.dullness);
        Assert.AreEqual(590, S.W.skill.swingMs);
        Debug.Log("PROBE-CHOP dullness=" + S.W.axe.dullness + " swingMs=" + S.W.skill.swingMs);
        // Six more: dullness caps at 100, swing keeps stepping.
        for (int i = 0; i < 6; i++)
        {
            S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
            S.TryAttack();
            yield return new WaitForSeconds(0.7f);
        }
        Assert.AreEqual(12, S.W.skill.chops);
        Assert.AreEqual(100f, S.W.axe.dullness);
        Assert.AreEqual(470, S.W.skill.swingMs);
        Debug.Log("PROBE-CHOP12 dullness=" + S.W.axe.dullness + " swingMs=" + S.W.skill.swingMs);
        S.ToTitle();
    }

    [UnityTest]
    public IEnumerator WolfPressure()
    {
        var S = Game.Session;
        S.NewGame(9);
        // A walker on the path nearest the den (58u out) passes unheard.
        S.Player.Teleport(-12f, 0f, 0f);
        yield return new WaitForSeconds(1f);
        Assert.AreEqual("idle", S.Wolves[0].brain.state);
        Assert.AreEqual("idle", S.Wolves[1].brain.state);
        Assert.AreEqual("idle", S.Wolves[2].brain.state);
        Debug.Log("PROBE-WOLF walker-on-path: all idle");
        // Step 25u from the den and the pack notices within a second.
        S.Player.Teleport(-45f, 0f, 0f);
        bool noticed = false;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 1.5f)
        {
            foreach (var u in S.Wolves)
            {
                if (u.brain.state == "warn" || u.brain.state == "chase") noticed = true;
            }
            if (noticed) break;
            yield return null;
        }
        Assert.IsTrue(noticed, "pack ignored a walker at 25u");
        Debug.Log("PROBE-WOLF walker-at-25u: noticed");
        // A runner on the path (58u) is heard: Logic-level, deterministic.
        var wolf = Logic.CreateWolf(-70f, 0f);
        var o = Logic.WolfStep(wolf, -12f, 0f, true, 1f, 0.1f, null);
        CollectionAssert.Contains(o.events, "growl");
        // Escape inequality: chase must stay slower than the player's run.
        Assert.Less(Tuning.WolfChaseSpeed, 7.2f, "wolves outrun the player");
        Debug.Log("PROBE-WOLF runner-heard chase=" + Tuning.WolfChaseSpeed);
        S.ToTitle();
    }
}
