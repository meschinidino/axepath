// PlayMode verification: the code-built world exists with the right contents,
// and viewpoint screenshots land on disk for human review.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AxePath;

public class WorldShots
{
    [UnityTest]
    public IEnumerator WorldStructure()
    {
        yield return null;
        Assert.IsNotNull(Game.World, "Boot did not build the world");
        Assert.IsTrue(Game.World.refs.treeCount >= 1450, "trees: " + Game.World.refs.treeCount);
        Assert.IsTrue(Game.World.colliders.Count > 1200, "colliders: " + Game.World.colliders.Count);
        // Mark audit: every blaze spot attached a plaque, each within
        // sight of the walking line.
        Assert.AreEqual(6, Game.World.refs.blazePos.Count);
        foreach (var bp in Game.World.refs.blazePos)
        {
            Assert.IsTrue(WorldBuilder.NearPath(bp.x, bp.z, 30f), "blaze off-route: " + bp.x + "," + bp.z);
        }
        // Each plaque sits on the south face of its trunk: the climb
        // walks toward -z, so the marks face the approaching walker.
        int faced = 0;
        foreach (var tr in GameObject.FindObjectsOfType<Transform>())
        {
            if (tr.name != "Blaze") continue;
            faced++;
            float dz = 0f;
            foreach (var bp in Game.World.refs.blazePos)
            {
                float dx = tr.position.x - bp.x;
                float dzz = tr.position.z - bp.z;
                if (dx * dx + dzz * dzz < 4f) { dz = dzz; break; }
            }
            Assert.Greater(dz, 0f, "plaque not on the walker's face");
        }
        Assert.AreEqual(6, faced, "blaze meshes: " + faced);
        Assert.IsTrue(Game.World.refs.flowerCount >= 400, "flowers: " + Game.World.refs.flowerCount);
        Assert.AreEqual(350, Game.World.refs.bushCount);
        Assert.IsNotNull(GameObject.Find("Dwarf"));
        Assert.IsNotNull(GameObject.Find("Librarian"));
        Assert.IsNotNull(GameObject.Find("Wolf"));
        Assert.IsNotNull(GameObject.Find("Wolf4"), "pack incomplete");
        Assert.IsNotNull(GameObject.Find("Forge"));
        Assert.IsNotNull(GameObject.Find("Anvil"));
        Assert.IsNotNull(GameObject.Find("RiverWater"));
        Assert.IsNotNull(GameObject.Find("MillWheel"));
        // The wheel must touch the mill's east wall (half-width 3,
        // mirroring the Walls box) and sit over open water (ribbon
        // half-width 5.5 around RiverX): a lone hut plus a wheel on
        // sand reads as two stray objects.
        var mill = GameObject.Find("Mill");
        var wheel = GameObject.Find("MillWheel");
        float wallFace = mill.transform.position.x + 3f;
        float wheelFace = wheel.transform.position.x - 0.2f;
        Assert.LessOrEqual(wheelFace - wallFace, 1f, "wheel detached from mill");
        float wet = Mathf.Abs(wheel.transform.position.x - WorldBuilder.RiverX(wheel.transform.position.z));
        Assert.LessOrEqual(wet, 5.5f, "wheel out of the channel");
        Assert.IsNotNull(GameObject.Find("Bridge"));
        Assert.IsNotNull(GameObject.Find("Chimney"));
        // The peak's skirt must clear the cave hall: rock starts
        // behind the forge, so the mouth looks in on the dwarf
        // instead of a walk-through wall.
        var peak = Game.World.refs.cavePeak;
        float peakR = Game.World.refs.cavePeakR;
        Assert.LessOrEqual(peak.z + peakR, -320f, "peak skirt walls the mouth");
        // Forge smoke vents clear of the rock, off the peak axis.
        foreach (var sb in Game.World.refs.smokeBase)
        {
            float pdx = sb.x - peak.x;
            float pdz = sb.z - peak.z;
            Assert.Greater(pdx * pdx + pdz * pdz, (peakR + 2f) * (peakR + 2f), "smoke inside the peak");
        }
        // Animated path lock: puffs rise SmokeRise bending downwind, and
        // the whole path must clear the peak cone, not just the bases.
        // The cone base sits 2 under grade, mirroring the RockCone call.
        float peakH = Game.World.refs.cavePeakH;
        float coneBase = WorldBuilder.PlateauH - 2f;
        for (int i = 0; i < Game.World.refs.smokePuffs.Count; i++)
        {
            var puff = Game.World.refs.smokePuffs[i];
            float pr = puff.transform.localScale.x * 0.5f;
            var sb = Game.World.refs.smokeBase[i];
            for (float rise = 0f; rise <= WorldAnimator.SmokeRise; rise += 5f)
            {
                float px = sb.x + rise * WorldAnimator.SmokeDrift;
                float py = sb.y + rise;
                if (py < coneBase || py > coneBase + peakH) continue;
                float rr = peakR * (1f - (py - coneBase) / peakH);
                float dx = px - peak.x;
                float dz = sb.z - peak.z;
                float clear = Mathf.Sqrt(dx * dx + dz * dz) - rr - pr;
                Assert.Greater(clear, 2f, "smoke path nears peak: " + clear);
            }
        }
        Assert.IsNotNull(Game.MainCam);
        // The night sky must survive shader stripping into player builds
        // (the panoramic preload was once missing and builds fell back to
        // a flat clear color).
        Assert.IsNotNull(RenderSettings.skybox, "no skybox material");
        // Weather: drifting clouds plus wind-blown leaves, fireflies,
        // and ground mist, all playing.
        // Berries must sit on foliage: parented under the scaled bush
        // they inherit its scale and land units outside it, reading as
        // stemless red blooms. Nearest-bush distance bounds them.
        var bushes = new List<Transform>();
        var berries = new List<Transform>();
        foreach (var tr in GameObject.FindObjectsOfType<Transform>())
        {
            if (tr.name == "Bush") bushes.Add(tr);
            else if (tr.name == "Berry") berries.Add(tr);
        }
        Assert.AreEqual(350, bushes.Count);
        Assert.AreEqual(177, berries.Count);
        foreach (var berry in berries)
        {
            float best = float.MaxValue;
            foreach (var bush in bushes)
            {
                float dx = berry.position.x - bush.position.x;
                float dz = berry.position.z - bush.position.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best) best = d;
            }
            Assert.LessOrEqual(best, 1.5f, "berry adrift of foliage: " + best);
        }
        Assert.AreEqual(10, Game.World.refs.clouds.Count);
        Assert.IsNotNull(GameObject.Find("Clouds"));
        var leaves = GameObject.Find("WindLeaves");
        Assert.IsNotNull(leaves);
        var leafPS = leaves.GetComponent<ParticleSystem>();
        Assert.IsNotNull(leafPS);
        Assert.IsTrue(leafPS.isPlaying, "leaves not playing");
        Assert.AreEqual(1600, leafPS.main.maxParticles);
        Assert.IsNotNull(GameObject.Find("FirefliesLow"));
        Assert.IsNotNull(GameObject.Find("FirefliesGrove"));
        Assert.AreEqual(2, Game.World.refs.fireflyMats.Count);
        // Every bloom planted with a stem, none hovering; the river
        // carries current streaks and glints.
        Assert.AreEqual(430, Game.World.refs.flowerCount);
        var stems = GameObject.Find("Flowers");
        Assert.IsNotNull(stems);
        Assert.AreEqual(80, Game.World.refs.riverStreaks.Count);
        // Journey furniture: the mid-leg waystone and the book nook.
        Assert.IsNotNull(GameObject.Find("Waystone"));
        Assert.IsNotNull(GameObject.Find("BookNook"));
        Assert.IsNull(GameObject.Find("LeanTo"));
        // The path winds: fixed at both ends, bending both ways between.
        Assert.AreEqual(0f, WorldBuilder.PathX(210f));
        Assert.AreEqual(0f, WorldBuilder.PathX(-270f));
        float maxX = 0f;
        float minX = 0f;
        for (float z = 200f; z >= -260f; z -= 20f)
        {
            float px = WorldBuilder.PathX(z);
            if (px > maxX) maxX = px;
            if (px < minX) minX = px;
        }
        Assert.Greater(maxX, 20f, "no east bend");
        Assert.Less(minX, -20f, "no west bend");
        // The grove must stand clear of every peak base (the log was
        // once buried inside the east slope).
        Assert.IsTrue(Game.World.refs.peaks.Count == 3, "peaks: " + Game.World.refs.peaks.Count);
        foreach (var pk in Game.World.refs.peaks)
        {
            float dx = Game.World.refs.ironLogPos.x - pk.x;
            float dz = Game.World.refs.ironLogPos.z - pk.z;
            Assert.IsTrue(dx * dx + dz * dz > pk.r * pk.r, "log inside peak base");
        }
        // Peak meshes must exist in the scene, not just in refs.
        var found = new List<GameObject>();
        foreach (Transform c in Game.World.root.transform)
        {
            if (c.name == "Peak") found.Add(c.gameObject);
        }
        Assert.AreEqual(3, found.Count, "scene peaks");
        foreach (var p in found)
        {
            var rends = p.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(3, rends.Length, "peak parts at " + p.transform.position);
            foreach (var r in rends)
            {
                Assert.IsNotNull(r.sharedMaterial);
                Assert.IsTrue(r.enabled, "peak renderer off");
                Assert.IsTrue(r.bounds.size.magnitude > 1f, "peak bounds");
            }
            Assert.IsTrue(p.activeInHierarchy, "peak inactive");
        }
        // The cave mountain: cones above and around the mouth.
        var cones = new List<GameObject>();
        foreach (Transform c in Game.World.root.transform)
        {
            if (c.name == "Mountain") cones.Add(c.gameObject);
        }
        Assert.AreEqual(7, cones.Count, "mountain cones");
        // Uphill contract: village flat, cave plateau high, ramp between.
        Assert.AreEqual(0f, WorldBuilder.GroundH(0f, 210f));
        Assert.AreEqual(40f, WorldBuilder.GroundH(0f, -300f));
        float mid = WorldBuilder.GroundH(0f, -55f);
        Assert.IsTrue(mid > 10f && mid < 30f, "ramp mid: " + mid);
        // River contract: a fordable carve east of the village, flat deck
        // under the bridge.
        float rx = WorldBuilder.RiverX(100f);
        Assert.IsTrue(rx > 80f && rx < 180f, "river x: " + rx);
        Assert.IsTrue(WorldBuilder.GroundH(rx, 100f) < WorldBuilder.BaseH(100f) - 0.5f, "no channel");
        Assert.AreEqual(WorldBuilder.BaseH(WorldBuilder.BridgeZ),
            WorldBuilder.GroundH(WorldBuilder.RiverX(WorldBuilder.BridgeZ), WorldBuilder.BridgeZ),
            "bridge grade");
        // Cave-mouth corners: the side-wall front faces (z=-277, x
        // 9..17 and -17..-9 per the CaveWall boxes in BuildCave) must
        // be fully capped — no clip-through into the wall ends — while
        // the guide mark on the east face (13,-276.4) stays walkably
        // close. The corner discs once sat ON the face plane and
        // jutted 4u into open ground: an invisible wall 3.85u short
        // of the mark. Player radius is the 0.45 in
        // PlayerController.Tick's push-out. Pure geometry on the real
        // collider list: Tick reads live key input, undrivable here.
        const float pRad = 0.45f;
        var cols = Game.World.colliders;
        // (a) The blocked interval on the x=13 approach line that
        // holds the wall face must start within 2.5u of the mark.
        // Merged to a fixed point: the face interval spans the corner
        // plus wall discs, while blockers further south (rocks) must
        // not count.
        float faceZ = -277f;
        float markZ = Game.World.refs.guidePos.z;
        float lo = faceZ;
        float hi = faceZ;
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var c in cols)
            {
                float dx = 13f - c.x;
                float rr = c.r + pRad;
                if (Mathf.Abs(dx) >= rr) continue;
                float h = Mathf.Sqrt(rr * rr - dx * dx);
                float a = c.z - h;
                float b = c.z + h;
                if (b < lo || a > hi) continue;
                if (a < lo) { lo = a; grew = true; }
                if (b > hi) { hi = b; grew = true; }
            }
        }
        Assert.Greater(hi, faceZ, "face interval missing on x=13");
        Assert.LessOrEqual(hi - markZ, 2.5f, "guide approach blocked at " + hi);
        // (b) Every sample of both front faces, plus the seam between
        // the corner and wall discs, sits inside some expanded
        // collider: no clip-through at the wall ends.
        float[,] capped = new float[,] {
            {9f, faceZ}, {11f, faceZ}, {13f, faceZ}, {15f, faceZ}, {17f, faceZ},
            {-9f, faceZ}, {-11f, faceZ}, {-13f, faceZ}, {-15f, faceZ}, {-17f, faceZ},
            {13f, -281f}, {-13f, -281f},
        };
        for (int i = 0; i < capped.GetLength(0); i++)
        {
            float sx = capped[i, 0];
            float sz = capped[i, 1];
            bool hit = false;
            foreach (var c in cols)
            {
                float dx = sx - c.x;
                float dz = sz - c.z;
                float rr = c.r + pRad;
                if (dx * dx + dz * dz <= rr * rr) { hit = true; break; }
            }
            Assert.IsTrue(hit, "wall-end gap at " + sx + "," + sz);
        }
        // (c) The mouth itself stays open down the middle.
        foreach (float mz in new float[] { -272f, -279f, -285f })
        {
            bool walkable = true;
            foreach (var c in cols)
            {
                float dz = mz - c.z;
                float rr = c.r + pRad;
                if (c.x * c.x + dz * dz < rr * rr) { walkable = false; break; }
            }
            Assert.IsTrue(walkable, "mouth blocked at 0," + mz);
        }
    }

    // Synchronous RenderTexture capture: ScreenCapture's end-of-frame write
    // races the player Tick that re-seats the camera, so every viewpoint
    // used to land on the same frame. This renders exactly what is posed.
    private static IEnumerator Shot(string name, Vector3 pos, Vector3 euler, bool showAxe)
    {
        // Batchmode renders on the Null device, which segfaults inside
        // Unity's billboard particle code (6000.6.6f1): screenshots only
        // hold in the editor. Same convention as SessionFlow's capture.
        if (Application.isBatchMode) yield break;
        var cam = Game.MainCam;
        Assert.IsNotNull(cam, "no main camera");
        pos.y += WorldBuilder.GroundH(pos.x, pos.z);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.Euler(euler);
        var axeView = GameObject.Find("AxeView");
        bool axeWas = axeView != null && axeView.activeSelf;
        if (axeView != null) axeView.SetActive(showAxe && axeWas);
        // No yield between posing and Render: a frame would let the
        // player Tick re-seat the camera to the eye.
        string full = Path.Combine(Application.dataPath, "Screenshots/" + name + ".png");
        try { if (File.Exists(full)) File.Delete(full); } catch { }
        var rt = RenderTexture.GetTemporary(1024, 512, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        cam.targetTexture = prev;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(full, tex.EncodeToPNG());
        Object.Destroy(tex);
        if (axeView != null) axeView.SetActive(axeWas);
        Assert.IsTrue(File.Exists(full), "screenshot missing: " + name);
        cam.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        cam.transform.localRotation = Quaternion.identity;
        yield return null;
    }

    [UnityTest]
    public IEnumerator MillWheelTurnsWithCurrent()
    {
        yield return null;
        var wheel = GameObject.Find("MillWheel");
        Assert.IsNotNull(wheel);
        // The current runs +z; the dipped bottom must travel +z, which
        // is a negative rotation about X. Relative quaternion over a
        // realtime wait: immune to absolute angle and frame rate.
        var q0 = wheel.transform.localRotation;
        yield return new WaitForSeconds(0.3f);
        var dq = Quaternion.Inverse(q0) * wheel.transform.localRotation;
        Assert.Less(dq.x, 0f, "wheel bottom travels -z, against the +z current");
        Assert.Greater(dq.x, -0.3f, "wheel spun impossibly far");
    }

    [UnityTest]
    public IEnumerator ShotSpawn() { yield return Shot("pm-spawn", new Vector3(0f, 1.7f, 210f), new Vector3(0f, 180f, 0f), true); }

    [UnityTest]
    public IEnumerator ShotWolf() { yield return Shot("pm-wolf", new Vector3(-70f, 1.6f, 4.5f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotDwarf() { yield return Shot("pm-dwarf", new Vector3(-4f, 1.8f, -307f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotMouth() { yield return Shot("pm-mouth", new Vector3(0f, 5f, -250f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotVista() { yield return Shot("pm-vista", new Vector3(0f, 4.5f, -70f), new Vector3(-8f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotGrove() { yield return Shot("pm-grove", new Vector3(88f, 2.2f, -352f), new Vector3(0f, 304f, 0f), false); }


    [UnityTest]
    public IEnumerator ShotPeakTop() { yield return Shot("pm-peaktop", new Vector3(0f, 60f, -200f), new Vector3(-15f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotRiver() { yield return Shot("pm-river", new Vector3(70f, 3f, 200f), new Vector3(0f, 110f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotMountain() { yield return Shot("pm-mountain", new Vector3(0f, 3f, -240f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotNightSky() { yield return Shot("pm-nightsky", new Vector3(0f, 6f, 210f), new Vector3(-45f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotFlowers() { yield return Shot("pm-flowers", new Vector3(20f, 1f, 229f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotRiverClose() { yield return Shot("pm-riverclose", new Vector3(123.5f, 8f, 150f), new Vector3(90f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotCaveEast() { yield return Shot("pm-caveeast", new Vector3(8f, 2f, -268f), new Vector3(0f, 149f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotLibrary() { yield return Shot("pm-library", new Vector3(35f, 1.7f, 194f), new Vector3(0f, 180f, 0f), false); }

    // Regression: the mouth glow quad once rendered as an opaque white
    // wall for anyone inside the cave looking out. The fixed view is the
    // night forest with a small bright fraction.
    [UnityTest]
    public IEnumerator CaveMouthNotWalled()
    {
        // Pixel assert needs a real GPU (see Shot): the batchmode Null
        // renderer segfaults on billboard particles.
        if (Application.isBatchMode) yield break;
        var cam = Game.MainCam;
        Assert.IsNotNull(cam, "no main camera");
        var pos = new Vector3(-6f, 1.65f, -310f);
        pos.y += WorldBuilder.GroundH(pos.x, pos.z);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var rt = RenderTexture.GetTemporary(1024, 512, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        cam.targetTexture = prev;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        string full = Path.Combine(Application.dataPath, "Screenshots/dbg-caveout.png");
        try { if (File.Exists(full)) File.Delete(full); } catch { }
        File.WriteAllBytes(full, tex.EncodeToPNG());
        int white = 0;
        var px = tex.GetPixels();
        foreach (var p in px) if (p.r > 0.92f && p.g > 0.92f && p.b > 0.92f) white++;
        Object.Destroy(tex);
        cam.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        cam.transform.localRotation = Quaternion.identity;
        Assert.LessOrEqual((float)white / px.Length, 0.25f, "white fraction: " + ((float)white / px.Length));
        yield return null;
    }
}
