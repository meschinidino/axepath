using NUnit.Framework;
using AxePath;

[TestFixture]
public class SurvivalTests
{
    [Test]
    public void WolfLootFeedsAndHealsButNeverOverfills()
    {
        var w = Logic.CreateWorld();
        w.player.hp = 1;
        Logic.LootWolf(w);
        Assert.AreEqual(1, w.wolfPelts);
        Assert.AreEqual(1, w.wolfMeat);
        Assert.IsTrue(Logic.EatMeat(w));
        Assert.AreEqual(2, w.player.hp);
        Assert.AreEqual(0, w.wolfMeat);
        Assert.IsFalse(Logic.EatMeat(w));
        Logic.Forage(w);
        Assert.AreEqual(2, w.berries);
        Assert.IsTrue(Logic.EatBerries(w));
        Assert.AreEqual(3, w.player.hp);
        Logic.Forage(w);
        Assert.IsFalse(Logic.EatBerries(w), "full health must not waste food");
    }

    [Test]
    public void StaminaDrainsOnSprintAndSwingRegensAtRest()
    {
        float s = Logic.StaminaStep(100f, true, false, 1f);
        Assert.AreEqual(100f - Tuning.StaminaSprintDrain, s, 0.01f);
        s = Logic.StaminaStep(s, false, true, 0f);
        Assert.AreEqual(100f - Tuning.StaminaSprintDrain - Tuning.StaminaSwingCost, s, 0.01f);
        s = Logic.StaminaStep(s, false, false, 10f);
        Assert.AreEqual(Tuning.StaminaMax, s, 0.01f);
        Assert.AreEqual(0f, Logic.StaminaStep(1f, true, true, 5f), 0.01f);
    }

    [Test]
    public void GuardNeedsBreathAndPackNeedsEarshot()
    {
        Assert.IsTrue(Logic.BlockStopsBite(true, 50f));
        Assert.IsFalse(Logic.BlockStopsBite(true, 0f));
        Assert.IsFalse(Logic.BlockStopsBite(false, 50f));
        Assert.IsTrue(Logic.PackMateAlerts("idle", 30f));
        Assert.IsTrue(Logic.PackMateAlerts("warn", 48f));
        Assert.IsFalse(Logic.PackMateAlerts("chase", 10f));
        Assert.IsFalse(Logic.PackMateAlerts("idle", 90f));
    }

    [Test]
    public void LootSurvivesSaveRoundTripAndOldSavesDefaultToEmpty()
    {
        var w = Logic.CreateWorld();
        Logic.LootWolf(w); Logic.Forage(w);
        var back = Logic.ParseSave(Logic.SerializeWorld(w, "test"));
        Assert.IsTrue(back.ok, back.error);
        Assert.AreEqual(1, back.world.wolfPelts);
        Assert.AreEqual(2, back.world.berries);
    }
}
