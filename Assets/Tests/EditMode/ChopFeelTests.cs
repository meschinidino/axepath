// Axe Path — chop feel math checks: the trunk shudder curve and the
// chip flight step are presentation, but their guarantees (bounded,
// decaying, ballistic) are the whole read; pin the math, not the pixels.
using NUnit.Framework;
using UnityEngine;
using AxePath;

[TestFixture]
public class ChopFeelTests
{
    [Test]
    public void ShudderSilentAtRestAndPastDuration()
    {
        Assert.AreEqual(0f, GameSession.ShudderAngle(0f, 0.4f, 0.045f));
        Assert.AreEqual(0f, GameSession.ShudderAngle(0.4f, 0.4f, 0.045f));
        Assert.AreEqual(0f, GameSession.ShudderAngle(1f, 0.4f, 0.045f));
        Assert.AreEqual(0f, GameSession.ShudderAngle(-0.1f, 0.4f, 0.045f));
    }

    [Test]
    public void ShudderBoundedAndActuallyMoves()
    {
        bool moved = false;
        for (float t = 0.01f; t < 0.4f; t += 0.02f)
        {
            float a = GameSession.ShudderAngle(t, 0.4f, 0.045f);
            Assert.LessOrEqual(System.Math.Abs(a), 0.045f + 0.0001f);
            if (System.Math.Abs(a) > 0.001f) moved = true;
        }
        Assert.IsTrue(moved, "shudder never left zero");
    }

    [Test]
    public void ChipStepFallsAndDrags()
    {
        var pos = new Vector3(0f, 0f, 0f);
        var vel = new Vector3(2f, 0f, 0f);
        GameSession.ChipStep(ref pos, ref vel, 0.1f, 14f);
        Assert.Less(vel.y, 0f);
        Assert.Less(System.Math.Abs(vel.x), 2f);
        Assert.Greater(pos.x, 0f);
        Assert.Less(pos.y, 0f);
    }
}
