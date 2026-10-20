// Axe Path — logic checks. Mirrors the proven node suite test for test.
using System.Collections.Generic;
using NUnit.Framework;
using AxePath;

[TestFixture]
public class TrustOrderTests
{
    [Test]
    public void StaysColdWithoutDulledAxe_ThawsWithOne()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        Assert.AreEqual("cold", Logic.TalkDwarf(w).evt);
        Assert.AreEqual(Trust.Cold, w.trust);
        w.axe.dullness = Tuning.DullThresholdCivil;
        Assert.AreEqual("thaw", Logic.TalkDwarf(w).evt);
        Assert.AreEqual(Trust.Civil, w.trust);
    }

    [Test]
    public void NeverSkipsCivilOnWayToSharpening()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        w.ironwood.have = 1;
        Assert.AreEqual(false, Logic.TurnInIronwood(w));
        Assert.AreEqual(Trust.Cold, w.trust);
    }

    [Test]
    public void CompletesCivilToSharpeningWithIronwoodAndAxe()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        w.axe.dullness = 80f;
        Logic.TalkDwarf(w);
        w.ironwood.have = 1;
        Assert.AreEqual(true, Logic.TurnInIronwood(w));
        Assert.AreEqual(Trust.Sharp, w.trust);
        Assert.AreEqual(true, w.axe.sharpened);
        Assert.AreEqual(0f, w.axe.dullness);
    }
}

[TestFixture]
public class SafeguardTests
{
    [Test]
    public void WarnsOnFirstDraw_EndsOnSecond()
    {
        var w = Logic.CreateWorld();
        Assert.AreEqual("warned", Logic.DrawNearDwarf(w));
        Assert.AreEqual(Trust.Cold, w.trust);
        Assert.AreEqual(Trust.Ended, Logic.DrawNearDwarf(w));
    }

    [Test]
    public void KillingDwarfEndsPathPermanently()
    {
        var w = Logic.CreateWorld();
        Assert.AreEqual(true, Logic.AttackDwarf(w));
        Assert.AreEqual(false, w.dwarfAlive);
        Assert.AreEqual(Trust.Ended, w.trust);
        Assert.AreEqual("dead", Logic.TalkDwarf(w).evt);
    }
}

[TestFixture]
public class DullnessAndSkillTests
{
    [Test]
    public void ChopsDullEdgeAndStepSwingUp()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        float before = w.skill.swingMs;
        for (int i = 0; i < Tuning.ChopsPerStep; i++) Logic.UseAxe(w, "chop");
        Assert.IsTrue(w.axe.dullness > 0f);
        Assert.IsTrue(w.skill.swingMs < before);
    }

    [Test]
    public void HitsQuietTheStep()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        Logic.UseAxe(w, "hit");
        Assert.IsTrue(w.skill.noiseMul < Tuning.NoiseBase);
    }

    [Test]
    public void EdgeReadsInThreeSteps()
    {
        var w = Logic.CreateWorld();
        w.axe.dullness = 0f;
        Assert.AreEqual(0, Logic.EdgeStep(w));
        w.axe.dullness = 50f;
        Assert.AreEqual(1, Logic.EdgeStep(w));
        w.axe.dullness = 100f;
        Assert.AreEqual(2, Logic.EdgeStep(w));
    }

    [Test]
    public void SharpenedEdgeHoldsFarLonger()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        w.axe.dullness = 80f;
        Logic.TalkDwarf(w);
        w.ironwood.have = 1;
        Logic.TurnInIronwood(w);
        Logic.UseAxe(w, "chop");
        Assert.IsTrue(w.axe.dullness < Tuning.DullPerChop);
    }
}

[TestFixture]
public class DeathAndCorpseRunTests
{
    [Test]
    public void ThreeHitsKill_DeathWritesNewNobodyIntoSameWorld()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        w.axe.dullness = 90f;
        Logic.TalkDwarf(w);
        for (int i = 0; i < Tuning.WolfHitsToKill; i++) Logic.DamagePlayer(w, 1);
        Assert.AreEqual(false, w.player.alive);
        Assert.AreEqual(true, Logic.Die(w, new DropPos { x = 10f, z = 20f }));
        Assert.AreEqual(true, w.player.alive);
        Assert.AreEqual(2, w.generation);
        Assert.AreEqual(Trust.Cold, w.trust);
        Assert.AreEqual(false, w.axe.have);
        Assert.AreEqual(10f, w.axe.droppedAt.x);
        Assert.AreEqual(20f, w.axe.droppedAt.z);
        Assert.AreEqual(0, w.ironwood.have);
    }

    [Test]
    public void ReclaimsDroppedAxe_OrTakesFreshYardAxeWhenLost()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        Logic.DamagePlayer(w, 99);
        Logic.Die(w, new DropPos { x = 1f, z = 2f });
        Assert.AreEqual(true, Logic.ReclaimAxe(w));
        Assert.AreEqual(true, w.axe.have);
        Logic.DamagePlayer(w, 99);
        Logic.Die(w, new DropPos { x = 3f, z = 4f });
        w.axe.droppedAt = null;
        Assert.AreEqual(true, Logic.FreshYardAxeAvailable(w));
    }

    [Test]
    public void DeadDwarfStaysDeadForNewNobody()
    {
        var w = Logic.CreateWorld();
        Logic.AttackDwarf(w);
        Logic.DamagePlayer(w, 99);
        Logic.Die(w, null);
        Assert.AreEqual(false, w.dwarfAlive);
        Assert.AreEqual(Trust.Ended, w.trust);
        Assert.AreEqual("dead", Logic.TalkDwarf(w).evt);
    }
}

[TestFixture]
public class SaveRoundTripTests
{
    [Test]
    public void SerializesAndParsesWorldUnchanged()
    {
        var w = Logic.CreateWorld();
        Logic.TakeYardAxe(w);
        Logic.TakeBook(w);
        w.axe.dullness = 55f;
        string text = Logic.SerializeWorld(w, "probe");
        var parsed = Logic.ParseSave(text);
        Assert.AreEqual(true, parsed.ok);
        Assert.AreEqual(55f, parsed.world.axe.dullness);
        Assert.AreEqual(true, parsed.world.book.have);
        Assert.AreEqual("probe", parsed.label);
    }

    [Test]
    public void RejectsGarbageForeignGamesAndWrongVersions()
    {
        Assert.AreEqual(false, Logic.ParseSave("not json").ok);
        Assert.AreEqual(false, Logic.ParseSave("{\"app\":\"other\"}").ok);
        var w = Logic.CreateWorld();
        string text = Logic.SerializeWorld(w, "").Replace("\"version\":" + Meta.SaveVersion, "\"version\":99");
        var parsed = Logic.ParseSave(text);
        Assert.AreEqual(false, parsed.ok);
        StringAssert.Contains("version 99", parsed.error);
    }

    [Test]
    public void RejectsBrokenFieldsOneByOne()
    {
        var w = Logic.CreateWorld();
        string text = Logic.SerializeWorld(w, "").Replace("\"trust\":\"cold\"", "\"trust\":\"pope\"");
        var parsed = Logic.ParseSave(text);
        Assert.AreEqual(false, parsed.ok);
        StringAssert.Contains("trust", parsed.error);
    }

    [Test]
    public void CapsUntrustedEchoesInErrorsAndLabels()
    {
        var w = Logic.CreateWorld();
        string big = new string('x', 10000);
        string text = Logic.SerializeWorld(w, "").Replace("\"version\":" + Meta.SaveVersion, "\"version\":\"" + big + "\"");
        var parsed = Logic.ParseSave(text);
        Assert.AreEqual(false, parsed.ok);
        Assert.LessOrEqual(parsed.error.Length, 200);
        string text2 = Logic.SerializeWorld(w, big);
        var parsed2 = Logic.ParseSave(text2);
        Assert.AreEqual(true, parsed2.ok);
        Assert.LessOrEqual(parsed2.label.Length, 120);
    }

    [Test]
    public void SurvivesHostileVersionAndLabelShapes()
    {
        var w = Logic.CreateWorld();
        string text = Logic.SerializeWorld(w, "").Replace("\"version\":" + Meta.SaveVersion, "\"version\":5000000000");
        var parsed = Logic.ParseSave(text);
        Assert.AreEqual(false, parsed.ok);
        StringAssert.Contains("version", parsed.error);
        string text2 = Logic.SerializeWorld(w, "").Replace("\"label\":\"", "\"label\":{\"x\":1},\"zz\":\"");
        var parsed2 = Logic.ParseSave(text2);
        Assert.AreEqual(true, parsed2.ok);
        Assert.AreEqual("", parsed2.label);
        string astral = new string('y', 63) + char.ConvertFromUtf32(0x1F600) + new string('y', 100);
        string text3 = Logic.SerializeWorld(w, "").Replace("\"version\":" + Meta.SaveVersion, "\"version\":\"" + astral + "\"");
        var parsed3 = Logic.ParseSave(text3);
        Assert.AreEqual(false, parsed3.ok);
        for (int i = 0; i < parsed3.error.Length; i++)
        {
            if (char.IsHighSurrogate(parsed3.error[i]))
                Assert.IsTrue(i + 1 < parsed3.error.Length && char.IsLowSurrogate(parsed3.error[i + 1]));
        }
        string text4 = Logic.SerializeWorld(w, "").Replace("\"label\":\"", "\"label\":new Foo(),\"zz\":\"");
        var parsed4 = Logic.ParseSave(text4);
        Assert.AreEqual(true, parsed4.ok);
        Assert.AreEqual("", parsed4.label);
    }

    [Test]
    public void SurvivesHostileAppTrustAndSavedAtShapes()
    {
        var w = Logic.CreateWorld();
        string text = Logic.SerializeWorld(w, "probe");
        var legit = Logic.ParseSave(text);
        Assert.AreEqual(true, legit.ok);
        Assert.AreEqual("probe", legit.label);
        string appObj = text.Replace("\"app\":\"axe-path\"", "\"app\":{\"x\":1}");
        var parsed = Logic.ParseSave(appObj);
        Assert.AreEqual(false, parsed.ok);
        StringAssert.Contains("another game", parsed.error);
        string trustArr = text.Replace("\"trust\":\"cold\"", "\"trust\":[\"cold\"]");
        var parsed2 = Logic.ParseSave(trustArr);
        Assert.AreEqual(false, parsed2.ok);
        StringAssert.Contains("trust", parsed2.error);
        string trustCtor = text.Replace("\"trust\":\"cold\"", "\"trust\":new Foo()");
        var parsed3 = Logic.ParseSave(trustCtor);
        Assert.AreEqual(false, parsed3.ok);
        string dateObj = text.Replace("\"savedAt\":\"", "\"savedAt\":{\"x\":1},\"zz\":\"");
        var parsed4 = Logic.ParseSave(dateObj);
        Assert.AreEqual(true, parsed4.ok);
        Assert.AreEqual("", parsed4.savedAt);
        string dateBig = text.Replace("\"savedAt\":\"", "\"savedAt\":\"" + new string('s', 10000));
        var parsed5 = Logic.ParseSave(dateBig);
        Assert.AreEqual(true, parsed5.ok);
        Assert.LessOrEqual(parsed5.savedAt.Length, 64);
    }
}

[TestFixture]
public class SaverFallbackTests
{
    internal class RefusingBackend : ISaveBackend
    {
        public bool refused = true;
        private readonly Dictionary<string, string> store = new Dictionary<string, string>();
        public string GetItem(string key) { return store.ContainsKey(key) ? store[key] : null; }
        public void SetItem(string key, string value)
        {
            if (refused) throw new System.Exception("denied");
            store[key] = value;
        }
    }

    [Test]
    public void SavesAndLoadsThroughWorkingBackend()
    {
        var saver = new Saver(new MemoryBackend());
        Assert.AreEqual("saved", saver.Save(1, "hello").status);
        Assert.AreEqual("hello", saver.Load(1));
        Assert.AreEqual(false, saver.sessionOnly);
    }

    [Test]
    public void FallsBackToSessionOnlyWhenWritesRefuse_RecoversAfter()
    {
        var backend = new RefusingBackend();
        var saver = new Saver(backend);
        Assert.AreEqual("session-only", saver.Save(1, "x").status);
        Assert.AreEqual(true, saver.sessionOnly);
        backend.refused = false;
        Assert.AreEqual("saved", saver.Save(1, "x").status);
        Assert.AreEqual(false, saver.sessionOnly);
    }

    [Test]
    public void DescribesSlotsAsEmptyFilledOrBroken()
    {
        var saver = new Saver(new MemoryBackend());
        Assert.AreEqual("empty", saver.Describe(1).state);
        saver.Save(1, Logic.SerializeWorld(Logic.CreateWorld(), "run"));
        Assert.AreEqual("filled", saver.Describe(1).state);
        saver.Save(2, "garbage");
        Assert.AreEqual("broken", saver.Describe(2).state);
    }

    [Test]
    public void OffersThreeSlots()
    {
        Assert.AreEqual(3, Meta.SaveSlots);
    }
}

[TestFixture]
public class WolfBrainTests
{
    [Test]
    public void WarnsBeforeChasing()
    {
        var wolf = Logic.CreateWolf(0f, 0f);
        var t = new TuningSet { wolfWarnSecs = 3f };
        var o = Logic.WolfStep(wolf, 0f, 5f, false, 1f, 0.1f, t);
        CollectionAssert.AreEqual(new string[] { "growl" }, o.events);
        Assert.AreEqual("warn", wolf.state);
        o = Logic.WolfStep(wolf, 0f, 5f, false, 1f, 3.1f, t);
        CollectionAssert.AreEqual(new string[] { "chase" }, o.events);
        Assert.AreEqual("chase", wolf.state);
    }

    [Test]
    public void CalmsWhenPlayerLeavesEarshotDuringWarning()
    {
        var wolf = Logic.CreateWolf(0f, 0f);
        Logic.WolfStep(wolf, 0f, 5f, false, 1f, 0.1f, null);
        var o = Logic.WolfStep(wolf, 0f, 60f, false, 1f, 0.1f, null);
        CollectionAssert.AreEqual(new string[] { "calm" }, o.events);
        Assert.AreEqual("idle", wolf.state);
    }

    [Test]
    public void AttacksInRangeAndDropsChasePastLeash()
    {
        var wolf = Logic.CreateWolf(0f, 0f);
        wolf.state = "chase";
        var o = Logic.WolfStep(wolf, 0f, 1f, false, 1f, 0.1f, null);
        CollectionAssert.AreEqual(new string[] { "attack" }, o.events);
        wolf.pos = new Vec2(0f, 200f);
        o = Logic.WolfStep(wolf, 0f, 200f, false, 1f, 0.1f, null);
        CollectionAssert.AreEqual(new string[] { "leash" }, o.events);
        Assert.AreEqual("return", wolf.state);
    }

    [Test]
    public void RunningIsHeardFartherThanWalking()
    {
        float near = Tuning.WolfHearWalk + 1f;
        var w1 = Logic.CreateWolf(0f, 0f);
        Logic.WolfStep(w1, 0f, near, false, 1f, 0.1f, null);
        Assert.AreEqual("idle", w1.state);
        var w2 = Logic.CreateWolf(0f, 0f);
        Logic.WolfStep(w2, 0f, near, true, 1f, 0.1f, null);
        Assert.AreEqual("warn", w2.state);
    }
}
