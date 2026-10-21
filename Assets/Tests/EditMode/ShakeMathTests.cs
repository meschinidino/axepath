// Axe Path — impact shake math checks. The kick is presentation,
// but its guarantees (bounded, aim-preserving, decaying) are the
// whole feel; pin the math, not the pixels.
using NUnit.Framework;
using AxePath;

[TestFixture]
public class ShakeMathTests
{
    [Test]
    public void ZeroTraumaMeansZeroOffset()
    {
        for (float t = 0f; t < 2f; t += 0.13f)
            Assert.AreEqual(0f, PlayerController.ShakeOffset(0f, t), 0.0001f);
    }

    [Test]
    public void OffsetBoundedByTraumaSquared()
    {
        float[] traumas = new float[] { 0.25f, 0.55f, 0.85f, 1f };
        foreach (float tr in traumas)
        {
            float bound = tr * tr;
            for (float t = 0f; t < 5f; t += 0.07f)
                Assert.LessOrEqual(System.Math.Abs(PlayerController.ShakeOffset(tr, t)), bound + 0.0001f);
        }
    }

    [Test]
    public void OffsetIsDeterministic()
    {
        Assert.AreEqual(
            PlayerController.ShakeOffset(0.7f, 1.23f),
            PlayerController.ShakeOffset(0.7f, 1.23f));
    }

    [Test]
    public void OffsetActuallyMoves()
    {
        // A constant-zero "shake" would pass the bounds test above.
        float a = PlayerController.ShakeOffset(1f, 0.5f);
        float b = PlayerController.ShakeOffset(1f, 0.9f);
        Assert.AreNotEqual(a, b);
    }

    [Test]
    public void TraumaDecaysLinearlyAndClamps()
    {
        Assert.AreEqual(0.25f, PlayerController.TraumaAfter(0.5f, 0.1f, 0.4f), 0.0001f);
        Assert.AreEqual(0f, PlayerController.TraumaAfter(0.1f, 0.5f, 0.4f), 0.0001f);
        Assert.AreEqual(0f, PlayerController.TraumaAfter(0f, 0.1f, 0.4f), 0.0001f);
    }
}
