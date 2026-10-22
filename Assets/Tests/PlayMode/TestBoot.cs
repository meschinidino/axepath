// Shared PlayMode preamble: staged boot finishes a few frames after
// scene load, so every suite waits here instead of assuming a sync boot.
using System.Collections;
using NUnit.Framework;
using UnityEngine;

public static class TestBoot
{
    public static IEnumerator WaitForReady(float timeoutSecs = 30f)
    {
        float t0 = Time.realtimeSinceStartup;
        while (AxePath.Game.Session == null && Time.realtimeSinceStartup - t0 < timeoutSecs)
            yield return null;
        Assert.IsNotNull(AxePath.Game.Session, "Boot did not finish in " + timeoutSecs + "s");
        Assert.IsNotNull(AxePath.Game.World, "Boot did not build the world");
    }

    // Contact-timed impact: call right after a TryAttack that started a
    // swing. Returns right after the resolving Update, so HitStop, trauma,
    // chips and counters read exact, undecayed values.
    public static IEnumerator SwingToContact(AxePath.GameSession S, float timeoutSecs = 5f)
    {
        float t0 = Time.realtimeSinceStartup;
        while (S.Player.SwingK < AxePath.PlayerController.ContactK && Time.realtimeSinceStartup - t0 < timeoutSecs)
            yield return null;
        Assert.GreaterOrEqual(S.Player.SwingK, AxePath.PlayerController.ContactK, "swing never reached contact");
    }
}
