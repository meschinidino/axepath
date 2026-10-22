// Axe Path — tuning-as-data checks: the JSON loader, its fallbacks,
// and the guarantee that the committed tuning.json matches code defaults.
using System.IO;
using NUnit.Framework;
using UnityEngine;
using AxePath;

[TestFixture]
public class TuningDataTests
{
    [Test]
    public void CommittedJsonMatchesCodeDefaults()
    {
        string path = Path.Combine(Application.dataPath, "Resources/tuning.json");
        Assert.IsTrue(File.Exists(path), "Assets/Resources/tuning.json missing");
        var fromFile = TuningSet.FromJson(File.ReadAllText(path));
        Assert.AreEqual(TuningSet.Default().ToJson(), fromFile.ToJson());
    }

    [Test]
    public void PartialJsonMergesOverDefaults()
    {
        var t = TuningSet.FromJson("{\"wolfHp\":7}");
        Assert.AreEqual(7, t.wolfHp);
        Assert.AreEqual(Tuning.DullPerChop, t.dullPerChop);
        Assert.AreEqual(Tuning.WolfHearWalk, t.wolfHearWalk);
    }

    [Test]
    public void CorruptEmptyAndNullFallBackToDefaults()
    {
        string expected = TuningSet.Default().ToJson();
        Assert.AreEqual(expected, TuningSet.FromJson("{nope").ToJson());
        Assert.AreEqual(expected, TuningSet.FromJson("").ToJson());
        Assert.AreEqual(expected, TuningSet.FromJson(null).ToJson());
    }

    [Test]
    public void UnknownFieldsAreIgnored()
    {
        var t = TuningSet.FromJson("{\"noSuchKnob\":1,\"wolfHp\":5}");
        Assert.AreEqual(5, t.wolfHp);
    }

    [Test]
    public void RoundTripPreservesEveryField()
    {
        var t = TuningSet.FromJson(TuningSet.Default().ToJson());
        Assert.AreEqual(TuningSet.Default().ToJson(), t.ToJson());
    }

    [Test]
    public void CreateWorldTakesHpAndSkillFromData()
    {
        var t = new TuningSet { playerHp = 9, swingBaseMs = 111f, noiseBase = 2f };
        var w = Logic.CreateWorld(t);
        Assert.AreEqual(9, w.player.hp);
        Assert.AreEqual(111f, w.skill.swingMs);
        Assert.AreEqual(2f, w.skill.noiseMul);
    }

    [Test]
    public void TurnInCountsLogsFromData()
    {
        var t = new TuningSet { ironwoodLogsNeeded = 2 };
        var w = Logic.CreateWorld(t);
        Logic.TakeYardAxe(w);
        w.axe.dullness = 90f;
        Logic.TalkDwarf(w, t);
        w.ironwood.have = 1;
        Assert.AreEqual(false, Logic.TurnInIronwood(w, t));
        w.ironwood.have = 2;
        Assert.AreEqual(true, Logic.TurnInIronwood(w, t));
        Assert.AreEqual(0, w.ironwood.have);
    }

    [Test]
    public void DeathRespawnsHpAndSkillFromData()
    {
        var t = new TuningSet { playerHp = 6, swingBaseMs = 222f };
        var w = Logic.CreateWorld(t);
        Logic.TakeYardAxe(w);
        Logic.DamagePlayer(w, 99);
        Logic.Die(w, null, t);
        Assert.AreEqual(6, w.player.hp);
        Assert.AreEqual(222f, w.skill.swingMs);
    }
}
