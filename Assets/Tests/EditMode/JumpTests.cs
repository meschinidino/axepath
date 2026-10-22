// Axe Path — hop physics and lunge-dodge rule. Pins the integration
// trajectory over the committed tuning and the teeth-height boundary.
using NUnit.Framework;
using AxePath;

[TestFixture]
public class JumpTests
{
    [Test]
    public void HopRisesAndLands()
    {
        float y = 0f, vy = Tuning.JumpSpeed;
        float apex = 0f;
        float t = 0f;
        float dt = 1f / 60f;
        while ((y > 0f || vy != 0f) && t < 5f)
        {
            PlayerController.TickJump(ref y, ref vy, dt, Tuning.JumpGravity);
            if (y > apex) apex = y;
            t += dt;
        }
        Assert.AreEqual(0f, y);
        Assert.AreEqual(0f, vy);
        // Analytic apex v0^2/2g and airtime 2v0/g, within a frame of slop.
        float expectApex = Tuning.JumpSpeed * Tuning.JumpSpeed / (2f * Tuning.JumpGravity);
        float expectAir = 2f * Tuning.JumpSpeed / Tuning.JumpGravity;
        Assert.AreEqual(expectApex, apex, 0.06f);
        Assert.AreEqual(expectAir, t, 1f / 30f);
    }

    [Test]
    public void HopClearsLungeHeightWithMargin()
    {
        float apex = Tuning.JumpSpeed * Tuning.JumpSpeed / (2f * Tuning.JumpGravity);
        Assert.Greater(apex, Tuning.WolfLungeHeight);
    }

    [Test]
    public void LungeConnectsBelowTeethHeightOnly()
    {
        Assert.IsTrue(Logic.WolfHitsPlayer(0f, Tuning.WolfLungeHeight));
        Assert.IsTrue(Logic.WolfHitsPlayer(Tuning.WolfLungeHeight - 0.01f, Tuning.WolfLungeHeight));
        Assert.IsFalse(Logic.WolfHitsPlayer(Tuning.WolfLungeHeight, Tuning.WolfLungeHeight));
        Assert.IsFalse(Logic.WolfHitsPlayer(99f, Tuning.WolfLungeHeight));
    }
}
