// Axe Path — thematic impact math. Pins the surface rim/edge rule,
// the surface->burst mapping, and the per-kind landing response.
using NUnit.Framework;
using UnityEngine;
using AxePath;

[TestFixture]
public class ImpactFxTests
{
    [Test]
    public void RimIsClosestPointOnDisc()
    {
        var c = new Circle(5f, 5f, 2f, SurfaceKind.Stone);
        GameSession.SurfaceRim(5f, 9f, 0f, -1f, c, out float qx, out float qz);
        Assert.AreEqual(5f, qx, 0.001f);
        Assert.AreEqual(7f, qz, 0.001f);
    }

    [Test]
    public void RayDiscFindsNearRim()
    {
        bool hit = GameSession.RayDisc(0f, 8f, 0f, -1f, 0f, 0f, 6f, 10f, out float qx, out float qz);
        Assert.IsTrue(hit);
        Assert.AreEqual(0f, qx, 0.001f);
        Assert.AreEqual(6f, qz, 0.001f);
    }

    [Test]
    public void RayDiscMissesBesideAndBeyond()
    {
        Assert.IsFalse(GameSession.RayDisc(0f, 8f, 1f, 0f, 0f, 0f, 6f, 10f, out _, out _), "sideways");
        Assert.IsFalse(GameSession.RayDisc(0f, 20f, 0f, -1f, 0f, 0f, 6f, 5f, out _, out _), "past maxT");
    }

    [Test]
    public void RayDiscExitsWhenInside()
    {
        bool hit = GameSession.RayDisc(0f, 0f, 0f, -1f, 0f, 0f, 6f, 10f, out float qx, out float qz);
        Assert.IsTrue(hit);
        Assert.AreEqual(0f, qx, 0.001f);
        Assert.AreEqual(-6f, qz, 0.001f);
    }

    [Test]
    public void EdgeReadsRimNotCenter()
    {
        // Split-rock scale: center 8u off, rim 2u off, facing it.
        var c = new Circle(0f, 0f, 6f, SurfaceKind.Stone);
        float edge = GameSession.SurfaceEdge(0f, 8f, 0f, -1f, c, 2.6f);
        Assert.AreEqual(2f, edge, 0.001f);
    }

    [Test]
    public void EdgeRejectsBehindAndBeyond()
    {
        var c = new Circle(0f, 0f, 6f, SurfaceKind.Stone);
        Assert.AreEqual(-1f, GameSession.SurfaceEdge(0f, 8f, 0f, 1f, c, 2.6f), "facing away");
        Assert.AreEqual(-1f, GameSession.SurfaceEdge(0f, 10f, 0f, -1f, c, 2.6f), "rim out of reach");
    }

    [Test]
    public void MountainEdgeIsHittable()
    {
        var c = new Circle(-58f, -296f, 21f, SurfaceKind.Stone);
        float edge = GameSession.SurfaceEdge(-58f, -272.5f, 0f, -1f, c, 2.6f);
        Assert.AreEqual(2.5f, edge, 0.001f);
    }

    [Test]
    public void EyeOnRimReadsZero()
    {
        var c = new Circle(0f, 0f, 2.5f, SurfaceKind.Stone);
        float edge = GameSession.SurfaceEdge(0f, 2.5f, 0f, -1f, c, 2.6f);
        Assert.AreEqual(0f, edge, 0.001f);
    }

    [Test]
    public void SurfaceMapsToBurstKind()
    {
        Assert.AreEqual(MoteKind.Chip, GameSession.MoteKindFor(SurfaceKind.Wood));
        Assert.AreEqual(MoteKind.Spark, GameSession.MoteKindFor(SurfaceKind.Stone));
        Assert.AreEqual(MoteKind.Spark, GameSession.MoteKindFor(SurfaceKind.Metal));
    }

    [Test]
    public void NearPointPullsTowardEye()
    {
        GameSession.NearPoint(0f, 0f, 0f, 3f, 0.35f, out float qx, out float qz);
        Assert.AreEqual(0f, qx, 0.001f);
        Assert.AreEqual(0.35f, qz, 0.001f);
    }

    [Test]
    public void NearPointClampsAtEyeAndCoincident()
    {
        GameSession.NearPoint(0f, 0f, 0f, 0.2f, 0.35f, out float qx, out float qz);
        Assert.AreEqual(0f, qx, 0.001f);
        Assert.AreEqual(0.2f, qz, 0.001f);
        GameSession.NearPoint(5f, 5f, 5f, 5f, 0.35f, out qx, out qz);
        Assert.AreEqual(5f, qx, 0.001f);
        Assert.AreEqual(5f, qz, 0.001f);
    }

    [Test]
    public void CorpseHidesOnceAfterCorpseSecs()
    {
        Assert.IsFalse(GameSession.ShouldHideCorpse(true, false, 99f, 2.5f), "living");
        Assert.IsFalse(GameSession.ShouldHideCorpse(false, false, 0f, 2.5f), "fresh kill lies there");
        Assert.IsFalse(GameSession.ShouldHideCorpse(false, false, 2.49f, 2.5f), "not yet");
        Assert.IsTrue(GameSession.ShouldHideCorpse(false, false, 2.5f, 2.5f), "remains clear");
        Assert.IsFalse(GameSession.ShouldHideCorpse(false, true, 99f, 2.5f), "already gone");
    }

    [Test]
    public void FadeShrinksOnlyAtEndOfLife()
    {
        Assert.AreEqual(1f, GameSession.FadeScale(1.2f), 0.001f);
        Assert.AreEqual(1f, GameSession.FadeScale(0.2f), 0.001f);
        Assert.AreEqual(0.5f, GameSession.FadeScale(0.1f), 0.001f);
        Assert.AreEqual(0f, GameSession.FadeScale(0f), 0.001f);
        Assert.AreEqual(0f, GameSession.FadeScale(-1f), 0.001f);
    }

    [Test]
    public void LandingDiffersByKind()
    {
        var chip = new Vector3(4f, -2f, 0f);
        Assert.IsTrue(GameSession.MoteLand(MoteKind.Chip, ref chip));
        Assert.AreEqual(1.6f, chip.x, 0.001f);
        Assert.AreEqual(0f, chip.y);
        var blood = new Vector3(4f, -2f, 0f);
        Assert.IsTrue(GameSession.MoteLand(MoteKind.Blood, ref blood));
        Assert.AreEqual(Vector3.zero, blood);
        var spark = new Vector3(4f, -2f, 0f);
        Assert.IsFalse(GameSession.MoteLand(MoteKind.Spark, ref spark));
    }
}
