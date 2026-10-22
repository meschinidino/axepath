// Axe Path — master output scale. Pins the mute gate and the gain clamp
// the mixer applies to every mixed sample.
using NUnit.Framework;
using AxePath;

[TestFixture]
public class MasterGainTests
{
    [Test]
    public void MuteGatesEverything()
    {
        Assert.AreEqual(0f, AudioSynth.MasterScale(1f, true));
        Assert.AreEqual(0f, AudioSynth.MasterScale(0.5f, true));
        Assert.AreEqual(0f, AudioSynth.MasterScale(0f, true));
    }

    [Test]
    public void GainPassesThroughUnmuted()
    {
        Assert.AreEqual(1f, AudioSynth.MasterScale(1f, false), 0.0001f);
        Assert.AreEqual(0.5f, AudioSynth.MasterScale(0.5f, false), 0.0001f);
        Assert.AreEqual(0f, AudioSynth.MasterScale(0f, false), 0.0001f);
    }

    [Test]
    public void GainClampsToUnitRange()
    {
        Assert.AreEqual(1f, AudioSynth.MasterScale(2f, false), 0.0001f);
        Assert.AreEqual(0f, AudioSynth.MasterScale(-1f, false), 0.0001f);
    }
}
