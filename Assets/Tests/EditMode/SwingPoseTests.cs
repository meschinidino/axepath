// Axe Path — swing pose curve checks. The animation is presentation,
// but its beats (windup dip, accelerating chop, contact peak, settle
// to rest) are the whole read; pin the curve, not the pixels.
using NUnit.Framework;
using AxePath;

[TestFixture]
public class SwingPoseTests
{
    private static void Pose(float k, out float rotX, out float x, out float z)
    {
        PlayerController.SwingPose(k, out rotX, out x, out z);
    }

    [Test]
    public void BeatsLandAtRestWindupContactAndRest()
    {
        float rx, x, z;
        Pose(0f, out rx, out x, out z);
        Assert.AreEqual(10f, rx, 0.001f);
        Assert.AreEqual(0.32f, x, 0.001f);
        Assert.AreEqual(0.55f, z, 0.001f);
        Pose(0.28f, out rx, out x, out z);
        Assert.AreEqual(-26f, rx, 0.001f);
        Assert.AreEqual(0.38f, x, 0.001f);
        Assert.AreEqual(0.40f, z, 0.001f);
        Pose(0.62f, out rx, out x, out z);
        Assert.AreEqual(74f, rx, 0.001f);
        Assert.AreEqual(0.02f, x, 0.001f);
        Assert.AreEqual(1.02f, z, 0.001f);
        Pose(1f, out rx, out x, out z);
        Assert.AreEqual(10f, rx, 0.001f);
        Assert.AreEqual(0.32f, x, 0.001f);
        Assert.AreEqual(0.55f, z, 0.001f);
    }

    [Test]
    public void WindupDipsBelowRestThenChopAcceleratesToContact()
    {
        float rx, x, z;
        Pose(0.14f, out rx, out x, out z);
        Assert.Less(rx, 10f, "no pull-back mid-windup");
        Assert.Less(z, 0.55f, "no draw-in mid-windup");
        Assert.Greater(x, 0.32f, "no cock-out mid-windup");
        float prevRx = -26f;
        float prevX = 0.38f;
        float prevZ = 0.40f;
        float[] ks = new float[] { 0.35f, 0.45f, 0.55f, 0.6f };
        foreach (float k in ks)
        {
            Pose(k, out rx, out x, out z);
            Assert.Greater(rx, prevRx, "chop stalls at k=" + k);
            Assert.Less(x, prevX, "sweep stalls at k=" + k);
            Assert.Greater(z, prevZ, "push stalls at k=" + k);
            prevRx = rx;
            prevX = x;
            prevZ = z;
        }
        float stepEarly, stepLate, xx, zz;
        Pose(0.35f, out stepEarly, out xx, out zz);
        Pose(0.45f, out rx, out xx, out zz);
        float earlyGain = rx - stepEarly;
        Pose(0.55f, out stepLate, out xx, out zz);
        Pose(0.62f, out rx, out xx, out zz);
        float lateGain = (rx - stepLate) * (0.1f / 0.07f);
        Assert.Greater(lateGain, earlyGain, "chop does not accelerate into contact");
    }

    [Test]
    public void ContactIsThePeakAndSettleReturnsToRest()
    {
        float rx, x, z;
        Pose(0.5f, out rx, out x, out z);
        float midRx = rx;
        float midX = x;
        float midZ = z;
        Pose(0.62f, out rx, out x, out z);
        Assert.Greater(rx, midRx, "contact is not the rotation peak");
        Assert.Less(x, midX, "contact is not the center crossing");
        Assert.Greater(z, midZ, "contact is not the push peak");
        Pose(0.8f, out rx, out x, out z);
        Assert.Less(rx, 74f, "settle does not recoil");
        Assert.Greater(rx, 10f, "settle snaps to rest");
        Assert.Greater(x, 0.02f, "settle does not drift out");
        float rx2, x2, z2;
        Pose(0.9f, out rx2, out x2, out z2);
        Assert.Less(rx2, rx, "settle is not monotonic");
        Assert.Greater(rx2, 10f, "settle undershoots rest");
        Assert.Less(rx2, 20f, "settle lands nowhere near rest");
        Assert.Greater(x2, x, "settle is not monotonic outward");
        Assert.Less(x2, 0.32f, "settle overshoots rest");
    }
}
