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
        Assert.AreEqual(300, Game.World.refs.treeCount);
        Assert.IsTrue(Game.World.colliders.Count > 220, "colliders: " + Game.World.colliders.Count);
        Assert.IsTrue(Game.World.refs.blazePos.Count >= 1, "no blaze marks");
        Assert.IsNotNull(GameObject.Find("Dwarf"));
        Assert.IsNotNull(GameObject.Find("Librarian"));
        Assert.IsNotNull(GameObject.Find("Wolf"));
        Assert.IsNotNull(GameObject.Find("Forge"));
        Assert.IsNotNull(GameObject.Find("Anvil"));
        Assert.IsNotNull(Game.MainCam);
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
        // Uphill contract: village flat, cave plateau high, ramp between.
        Assert.AreEqual(0f, WorldBuilder.GroundH(0f, 42f));
        Assert.AreEqual(8f, WorldBuilder.GroundH(0f, -60f));
        float mid = WorldBuilder.GroundH(0f, -11f);
        Assert.IsTrue(mid > 2f && mid < 6f, "ramp mid: " + mid);
    }

    // Synchronous RenderTexture capture: ScreenCapture's end-of-frame write
    // races the player Tick that re-seats the camera, so every viewpoint
    // used to land on the same frame. This renders exactly what is posed.
    private static IEnumerator Shot(string name, Vector3 pos, Vector3 euler, bool showAxe)
    {
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
    public IEnumerator ShotSpawn() { yield return Shot("pm-spawn", new Vector3(0f, 1.7f, 42f), new Vector3(0f, 180f, 0f), true); }

    [UnityTest]
    public IEnumerator ShotWolf() { yield return Shot("pm-wolf", new Vector3(-14f, 1.6f, 4.5f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotDwarf() { yield return Shot("pm-dwarf", new Vector3(-2f, 1.8f, -56.5f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotMouth() { yield return Shot("pm-mouth", new Vector3(0f, 2.5f, -48f), new Vector3(0f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotVista() { yield return Shot("pm-vista", new Vector3(0f, 4.5f, -14f), new Vector3(-8f, 180f, 0f), false); }

    [UnityTest]
    public IEnumerator ShotGrove() { yield return Shot("pm-grove", new Vector3(8f, 2.2f, -56f), new Vector3(0f, 141f, 0f), false); }


    [UnityTest]
    public IEnumerator ShotPeakTop() { yield return Shot("pm-peaktop", new Vector3(0f, 12f, -40f), new Vector3(-15f, 180f, 0f), false); }
}
