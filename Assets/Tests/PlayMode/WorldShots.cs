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
        Assert.IsNotNull(Game.MainCam);
        // The night sky must survive shader stripping into player builds
        // (the panoramic preload was once missing and builds fell back to
        // a flat clear color).
        Assert.IsNotNull(RenderSettings.skybox, "no skybox material");
        // Weather: drifting clouds plus wind-blown leaves, fireflies,
        // and ground mist, all playing.
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
