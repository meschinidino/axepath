// Static valley, built in code: ground, lumber yard, lean-to, forest,
// landmarks, cave, grove. Seeded RNG keeps the valley identical every load.
// Faithful port of the proven three.js slice layout (same numbers, same order).
using System.Collections.Generic;
using UnityEngine;

namespace AxePath
{
    public class Mulberry
    {
        private uint a;
        public Mulberry(uint seed) { a = seed; }
        public float Next()
        {
            unchecked
            {
                a = a + 0x6D2B79F5;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return ((t ^ (t >> 14)) >> 0) / 4294967296f;
            }
        }
    }

    public struct Circle
    {
        public float x;
        public float z;
        public float r;
        public Circle(float x, float z, float r) { this.x = x; this.z = z; this.r = r; }
    }

    public class Spots
    {
        public Vec2 spawn = new Vec2(0f, 42f);
        public Vec2 yard = new Vec2(-6f, 36f);
        public Vec2 caveMouth = new Vec2(0f, -56f);
        public Vec2 den = new Vec2(-14f, 0f);
        public Vec2 librarian = new Vec2(9.5f, 37f);
        public Vec2 dwarf = new Vec2(-2f, -61f);
    }

    public class WorldRefs
    {
        public GameObject axeMesh;
        public GameObject bookMesh;
        public GameObject ironLog;
        public GameObject cairn;
        public GameObject glowPlane;
        public Light forgeLight;
        public Light mouthLight;
        public List<GameObject> smokePuffs = new List<GameObject>();
        public List<Vector3> smokeBase = new List<Vector3>();
        public List<Vec2> blazePos = new List<Vec2>();
        public Vector3 axeStandPos = new Vector3(-6f, 0f, 36f);
        public Vector3 bookStandPos = new Vector3(7f, 0f, 38f);
        public Vector3 ironLogPos = new Vector3(16f, 0f, -68f);
        public List<Circle> peaks = new List<Circle>();
        public Vec2 guidePos = new Vec2(6.5f, -54.9f);
        public int treeCount;
    }

    public class GameWorld
    {
        public GameObject root;
        public List<Circle> colliders = new List<Circle>();
        public Spots spots = new Spots();
        public WorldRefs refs = new WorldRefs();
        public float minX = -60f;
        public float maxX = 60f;
        public float minZ = -80f;
        public float maxZ = 50f;
    }

    public static class MeshUtil
    {
        private static readonly Dictionary<uint, Material> matCache = new Dictionary<uint, Material>();
        private static readonly Dictionary<uint, Material> basicCache = new Dictionary<uint, Material>();

        public static Color C(uint hex)
        {
            return new Color(
                ((hex >> 16) & 0xff) / 255f,
                ((hex >> 8) & 0xff) / 255f,
                (hex & 0xff) / 255f, 1f);
        }

        // Cached entries can be destroyed between runs (test teardown calls
        // UnloadUnusedAssets while the static dict survives), so a dead
        // entry must be rebuilt instead of reused.
        public static Material Mat(uint hex)
        {
            if (!matCache.TryGetValue(hex, out var m) || m == null)
            {
                m = new Material(Shader.Find("Standard"));
                m.color = C(hex);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Glossiness", 0.05f);
                matCache[hex] = m;
            }
            return m;
        }

        public static Material FreshMat(uint hex)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = C(hex);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.1f);
            return m;
        }

        public static Material Basic(uint hex)
        {
            if (!basicCache.TryGetValue(hex, out var m) || m == null)
            {
                m = new Material(Shader.Find("Unlit/Color"));
                m.color = C(hex);
                basicCache[hex] = m;
            }
            return m;
        }

        public static Material Transparent(uint hex, float alpha)
        {
            var m = new Material(Shader.Find("Unlit/Transparent"));
            var c = C(hex);
            c.a = alpha;
            m.color = c;
            return m;
        }

        public static GameObject Box(Transform parent, string name, float w, float h, float d, uint color, float x, float y, float z)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localScale = new Vector3(w, h, d);
            return go;
        }

        public static GameObject Ball(Transform parent, string name, float r, uint color, float x, float y, float z, bool unlit)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.GetComponent<Renderer>().sharedMaterial = unlit ? Basic(color) : Mat(color);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localScale = new Vector3(r * 2f, r * 2f, r * 2f);
            return go;
        }

        // Tapered cylinder / cone from the built-in primitive (guaranteed winding).
        public static Mesh TaperMesh(float topR, float botR, float h)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var mf = tmp.GetComponent<MeshFilter>();
            Mesh m = mf.mesh;
            Vector3[] v = m.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                float t = Mathf.Clamp01((v[i].y + 1f) / 2f);
                float want = Mathf.Lerp(botR, topR, t) / 0.5f;
                v[i].x *= want;
                v[i].z *= want;
                v[i].y *= h / 2f;
            }
            m.vertices = v;
            m.RecalculateNormals();
            m.RecalculateBounds();
            GameObject.DestroyImmediate(tmp);
            return m;
        }

        public static GameObject Tapered(Transform parent, string name, Mesh mesh, uint color, float x, float y, float z)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(color);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            return go;
        }
    }

    public static class WorldBuilder
    {
        // Uphill grade: the village (z>=30) is flat at 0, the forest ramps
        // up 8m to the cave plateau (z<=-52, flat at 8). Everything that
        // stands on the ground samples this; colliders stay 2D.
        public const float PlateauH = 8f;
        public static float GroundH(float x, float z)
        {
            if (z >= 30f) return 0f;
            if (z <= -52f) return PlateauH;
            float t = (30f - z) / 82f;
            t = t * t * (3f - 2f * t);
            return PlateauH * t;
        }
        public static void BuildGround(Transform parent)
        {
            int nx = 12, nz = 28;
            float x0 = -75f, x1 = 75f, z0 = -85f, z1 = 65f;
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uvs = new Vector2[verts.Length];
            var idx = new int[nx * nz * 6];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)nx);
                    float z = Mathf.Lerp(z0, z1, j / (float)nz);
                    verts[j * (nx + 1) + i] = new Vector3(x, GroundH(x, z), z);
                    uvs[j * (nx + 1) + i] = new Vector2(i / (float)nx, j / (float)nz);
                }
            int k = 0;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    idx[k++] = a; idx[k++] = c; idx[k++] = b;
                    idx[k++] = b; idx[k++] = c; idx[k++] = d;
                }
            var m = new Mesh();
            m.vertices = verts; m.uv = uvs; m.triangles = idx;
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = MeshUtil.Mat(0x24382a);
        }
        public static GameWorld Build(Transform parent)
        {
            var world = new GameWorld();
            var root = new GameObject("World");
            root.transform.SetParent(parent, false);
            world.root = root;
            var R = root.transform;
            void Solid(float x, float z, float r) { world.colliders.Add(new Circle(x, z, r)); }
            var refs = world.refs;

            // Ground plus dirt patches.
            BuildGround(R);
            var dirtMesh = MeshUtil.TaperMesh(1f, 1f, 0.04f);
            void Dirt(float x, float z, float r)
            {
                var p = MeshUtil.Tapered(R, "Dirt", dirtMesh, 0x4a3d28, x, GroundH(x, z) + 0.02f, z);
                p.transform.localScale = new Vector3(r, 1f, r);
                float slope = (GroundH(x, z + 1f) - GroundH(x, z - 1f)) * 0.5f;
                p.transform.localRotation = Quaternion.Euler(-Mathf.Atan(slope) * 57.29578f, 0f, 0f);
            }
            Dirt(-6f, 38f, 7f);
            Dirt(8f, 38f, 6f);
            Dirt(0f, 42f, 5f);
            Dirt(0f, -58f, 9f);
            Dirt(16f, -68f, 7f);
            var rockPatch = MeshUtil.Tapered(R, "RockPatch", dirtMesh, 0x3a3f46, 0f, GroundH(0f, -62f) + 0.015f, -62f);
            rockPatch.transform.localScale = new Vector3(10f, 1f, 10f);

            // Walking path.
            float[,] route = new float[,] {
                {0,42},{0,36},{-1,30},{0,24},{1,18},{1,12},{2,6},
                {1,0},{1,-8},{0,-16},{0,-24},{-1,-32},{0,-40},{0,-48},{0,-54},
            };
            for (int i = 0; i < route.GetLength(0); i++) Dirt(route[i, 0], route[i, 1], 2.2f);

            // Lumber yard: log piles, axe stand, the yard axe.
            var logMesh = MeshUtil.TaperMesh(0.35f, 0.35f, 4f);
            float[,] piles = new float[,] { {-10f, 38f}, {-9f, 40.5f}, {-3f, 40f} };
            for (int p = 0; p < 3; p++)
            {
                float lx = piles[p, 0];
                float lz = piles[p, 1];
                for (int i = 0; i < 3; i++)
                {
                    var log = MeshUtil.Tapered(R, "Log", logMesh, 0x5a4630, lx, 0.4f + (i == 2 ? 0.62f : 0f), lz + (i == 0 ? -0.4f : i == 1 ? 0.4f : 0f));
                    log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                Solid(lx, lz, 2.2f);
            }
            var stand = new GameObject("Stand");
            stand.transform.SetParent(R, false);
            stand.transform.localPosition = new Vector3(-6f, 0f, 36f);
            MeshUtil.Box(stand.transform, "Post", 0.18f, 1.1f, 0.18f, 0x4a3a28, -0.3f, 0.55f, 0f);
            MeshUtil.Box(stand.transform, "Post", 0.18f, 1.1f, 0.18f, 0x4a3a28, 0.3f, 0.55f, 0f);
            MeshUtil.Box(stand.transform, "Bar", 0.9f, 0.12f, 0.3f, 0x4a3a28, 0f, 1.05f, 0f);
            var yardAxe = new GameObject("YardAxe");
            yardAxe.transform.SetParent(stand.transform, false);
            yardAxe.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            yardAxe.transform.localRotation = Quaternion.Euler(0f, 0f, 28.6f);
            var handleMesh = MeshUtil.TaperMesh(0.05f, 0.06f, 1.1f);
            MeshUtil.Tapered(yardAxe.transform, "Handle", handleMesh, 0x6b5136, 0f, 0f, 0f);
            MeshUtil.Box(yardAxe.transform, "Head", 0.34f, 0.22f, 0.08f, 0x9aa0a6, 0.14f, 0.42f, 0f);
            Solid(-6f, 36f, 0.9f);
            refs.axeMesh = yardAxe;

            // Library lean-to.
            var lean = new GameObject("LeanTo");
            lean.transform.SetParent(R, false);
            lean.transform.localPosition = new Vector3(7f, 0f, 38f);
            MeshUtil.Box(lean.transform, "Post", 0.25f, 2.6f, 0.25f, 0x4a3a28, -1.6f, 1.3f, -1f);
            MeshUtil.Box(lean.transform, "Post", 0.25f, 2.6f, 0.25f, 0x4a3a28, 1.6f, 1.3f, -1f);
            MeshUtil.Box(lean.transform, "Post", 0.25f, 2.2f, 0.25f, 0x4a3a28, -1.6f, 1.1f, 1f);
            MeshUtil.Box(lean.transform, "Post", 0.25f, 2.2f, 0.25f, 0x4a3a28, 1.6f, 1.1f, 1f);
            var roof = MeshUtil.Box(lean.transform, "Roof", 4.4f, 0.15f, 3f, 0x5a4630, 0f, 2.6f, 0f);
            roof.transform.localRotation = Quaternion.Euler(6.9f, 0f, 0f);
            MeshUtil.Box(lean.transform, "Desk", 0.7f, 1.0f, 0.5f, 0x3a2f22, 0f, 0.5f, -0.3f);
            refs.bookMesh = MeshUtil.Box(lean.transform, "Book", 0.5f, 0.1f, 0.36f, 0xd9cfae, 0f, 1.08f, -0.3f);
            Solid(7f, 38f, 1.6f);

            // Two village huts.
            var hutRoofMesh = MeshUtil.TaperMesh(0.02f, 3.1f, 1.8f);
            float[,] huts = new float[,] { {-16f, 44f}, {16f, 44f} };
            for (int i = 0; i < 2; i++)
            {
                float hx = huts[i, 0];
                float hz = huts[i, 1];
                var hut = new GameObject("Hut");
                hut.transform.SetParent(R, false);
                hut.transform.localPosition = new Vector3(hx, 0f, hz);
                MeshUtil.Box(hut.transform, "Walls", 4f, 2.4f, 3.4f, 0x4f4234, 0f, 1.2f, 0f);
                var hr = MeshUtil.Tapered(hut.transform, "Roof", hutRoofMesh, 0x3a2f22, 0f, 3.3f, 0f);
                hr.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                Solid(hx, hz, 3.2f);
            }

            // Forest: seeded positions (same RNG order as the slice).
            var rand = new Mulberry(1337);
            float[,] clearings = new float[,] {
                {-6,38,10},{8,38,9},{0,42,7},{0,-58,13},
                {16,-68,10},{-14,0,7},{2,5,5},{0,28,7},
            };
            var treePos = new List<Vector3>();
            int guard = 0;
            while (treePos.Count < 210 && guard++ < 4000)
            {
                float x = (rand.Next() * 2f - 1f) * 62f;
                float z = -78f + rand.Next() * 118f;
                if (Mathf.Abs(x) < 5f && z > -62f && z < 32f) continue;
                bool bad = false;
                for (int c = 0; c < clearings.GetLength(0); c++)
                {
                    float dx = x - clearings[c, 0];
                    float dz = z - clearings[c, 1];
                    float cr = clearings[c, 2];
                    if (dx * dx + dz * dz < cr * cr) { bad = true; break; }
                }
                if (!bad) treePos.Add(new Vector3(x, z, 0.8f + rand.Next() * 0.7f));
            }
            var trunkMesh = MeshUtil.TaperMesh(0.28f, 0.42f, 3.2f);
            var canopyMesh = MeshUtil.TaperMesh(0.02f, 2.1f, 5.2f);
            var forest = new GameObject("Forest");
            forest.transform.SetParent(R, false);
            foreach (var tp in treePos)
            {
                var g = new GameObject("Tree");
                g.transform.SetParent(forest.transform, false);
                g.transform.localPosition = new Vector3(tp.x, GroundH(tp.x, tp.y), tp.y);
                // Variety is hashed from position so the RNG stream is untouched.
                float h1 = Mathf.Abs(Mathf.Sin(tp.x * 12.9898f + tp.y * 78.233f) * 43758.5453f) % 1f;
                float h2 = Mathf.Abs(Mathf.Sin(tp.x * 39.346f + tp.y * 11.135f) * 24634.6345f) % 1f;
                g.transform.localScale = new Vector3(tp.z, tp.z * (0.88f + h1 * 0.36f), tp.z);
                g.transform.localRotation = Quaternion.Euler((h1 - 0.5f) * 6f, rand.Next() * 180f, (h2 - 0.5f) * 6f);
                MeshUtil.Tapered(g.transform, "Trunk", trunkMesh, 0x4a3a28, 0f, 1.6f, 0f);
                int pick = (((int)(tp.x * 7f + tp.y * 13f)) % 3 + 3) % 3;
                uint canopyCol = pick == 0 ? 0x41704au : (pick == 1 ? 0x2e5233u : 0x38663bu);
                MeshUtil.Tapered(g.transform, "Canopy", canopyMesh, canopyCol, 0f, 5.4f, 0f);
                Solid(tp.x, tp.y, 0.5f);
            }
            refs.treeCount = treePos.Count;

            // Three landmark pines.
            var landTrunk = MeshUtil.TaperMesh(0.4f, 0.6f, 4.4f);
            var landCanopy = MeshUtil.TaperMesh(0.02f, 3f, 7f);
            float[,] lands = new float[,] { {0f, 30f}, {-5f, 27.5f}, {5f, 27f} };
            for (int i = 0; i < 3; i++)
            {
                float x = lands[i, 0];
                float z = lands[i, 1];
                var g = new GameObject("LandmarkPine");
                g.transform.SetParent(R, false);
                g.transform.localPosition = new Vector3(x, GroundH(x, z), z);
                MeshUtil.Tapered(g.transform, "Trunk", landTrunk, 0x4a3a28, 0f, 2.2f, 0f);
                MeshUtil.Tapered(g.transform, "Canopy", landCanopy, 0x35603a, 0f, 7f, 0f);
                Solid(x, z, 0.8f);
            }

            // Split rock (boxes stand in for the faceted rock until the art pass).
            var rock1 = MeshUtil.Box(R, "Rock", 2.6f, 2.2f, 2.2f, 0x5a6068, 2f, GroundH(2f, 5f) + 1.1f, 5f);
            rock1.transform.localRotation = Quaternion.Euler(0f, 30f, 8f);
            var rock2 = MeshUtil.Box(R, "Rock", 1.6f, 1.3f, 1.4f, 0x4a5058, 3.6f, GroundH(3.6f, 6.2f) + 0.65f, 6.2f);
            rock2.transform.localRotation = Quaternion.Euler(0f, -20f, -6f);
            Solid(2f, 5f, 2.2f);

            // Axe blazes on trunks along the path.
            float[,] blazeSpots = new float[,] { {-3.4f,22f},{3.2f,14f},{-3.2f,2f},{3.4f,-10f},{-3.2f,-24f},{3.2f,-38f} };
            for (int i = 0; i < blazeSpots.GetLength(0); i++)
            {
                float x = blazeSpots[i, 0];
                float z = blazeSpots[i, 1];
                Vector3 best = Vector3.zero;
                float bestD = 1e9f;
                bool found = false;
                foreach (var tp in treePos)
                {
                    float dx = tp.x - x;
                    float dz = tp.y - z;
                    float d = dx * dx + dz * dz;
                    if (d < bestD) { bestD = d; best = tp; found = true; }
                }
                if (!found || bestD > 30f) continue;
                refs.blazePos.Add(new Vec2(best.x, best.y));
                var blaze = MeshUtil.Box(R, "Blaze", 0.3f, 0.5f, 0.06f, 0xd9a441, best.x, GroundH(best.x, best.y) + 1.5f, best.y + 0.42f);
                blaze.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xd9a441);
            }

            // Guide mark: a previous climber's east-turn, cut on the wall
            // face the path walks into. The grove hides around the corner.
            var guide = MeshUtil.Box(R, "GuideMark", 0.34f, 0.56f, 0.06f, 0xd9a441, 6.5f, GroundH(6.5f, -54.9f) + 1.6f, -54.9f);
            guide.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xd9a441);
            refs.guidePos = new Vec2(6.5f, -54.9f);

            // Cave shell.
            uint stone = 0x565c66;
            MeshUtil.Box(R, "CaveWall", 5f, 9f, 14f, stone, -6.5f, GroundH(-6.5f, -62f) + 4.5f, -62f);
            MeshUtil.Box(R, "CaveWall", 5f, 9f, 14f, stone, 6.5f, GroundH(6.5f, -62f) + 4.5f, -62f);
            MeshUtil.Box(R, "CaveWall", 18f, 9f, 4f, stone, 0f, GroundH(0f, -69f) + 4.5f, -69f);
            MeshUtil.Box(R, "CaveRoof", 18f, 3f, 16f, stone, 0f, GroundH(0f, -62f) + 9.5f, -62f);
            Solid(-6.5f, -62f, 4.5f);
            Solid(6.5f, -62f, 4.5f);
            // Back-wall collar: the old mountain row used to block this side.
            Solid(-6f, -69f, 3.5f);
            Solid(0f, -69f, 3.5f);
            Solid(6f, -69f, 3.5f);
            // Front corners of the side walls; the mouth stays open.
            Solid(-6.5f, -56f, 2.2f);
            Solid(6.5f, -56f, 2.2f);
            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "MouthGlow";
            glow.GetComponent<Renderer>().sharedMaterial = MeshUtil.Transparent(0xc1502e, 0.28f);
            glow.transform.SetParent(R, false);
            glow.transform.localPosition = new Vector3(0f, GroundH(0f, -57.4f) + 3f, -57.4f);
            glow.transform.localScale = new Vector3(7f, 6f, 1f);
            refs.glowPlane = glow;
            var mouth = new GameObject("MouthLight");
            mouth.transform.SetParent(R, false);
            mouth.transform.localPosition = new Vector3(0f, GroundH(0f, -56f) + 3.5f, -56f);
            var mouthLight = mouth.AddComponent<Light>();
            mouthLight.type = LightType.Point;
            mouthLight.color = MeshUtil.C(0xc1502e);
            mouthLight.intensity = 2.2f;
            mouthLight.range = 14f;
            refs.mouthLight = mouthLight;

            // Forge, anvil.
            var forge = new GameObject("Forge");
            forge.transform.SetParent(R, false);
            forge.transform.localPosition = new Vector3(3f, GroundH(3f, -63f), -63f);
            MeshUtil.Box(forge.transform, "Body", 1.8f, 1.1f, 1.2f, 0x3a3f46, 0f, 0.55f, 0f);
            var coals = MeshUtil.Box(forge.transform, "Coals", 1.4f, 0.25f, 0.9f, 0xff7a2a, 0f, 1.15f, 0f);
            coals.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xff7a2a);
            Solid(3f, -63f, 1.4f);
            var forgeLightGo = new GameObject("ForgeLight");
            forgeLightGo.transform.SetParent(R, false);
            forgeLightGo.transform.localPosition = new Vector3(3f, GroundH(3f, -63f) + 2.2f, -63f);
            var forgeLight = forgeLightGo.AddComponent<Light>();
            forgeLight.type = LightType.Point;
            forgeLight.color = MeshUtil.C(0xff8a3a);
            forgeLight.intensity = 3f;
            forgeLight.range = 12f;
            refs.forgeLight = forgeLight;
            var anvil = new GameObject("Anvil");
            anvil.transform.SetParent(R, false);
            anvil.transform.localPosition = new Vector3(0.5f, GroundH(0.5f, -64.5f), -64.5f);
            MeshUtil.Box(anvil.transform, "Base", 0.5f, 0.5f, 0.5f, 0x2c2f34, 0f, 0.25f, 0f);
            MeshUtil.Box(anvil.transform, "Top", 1.1f, 0.28f, 0.4f, 0x3a3f46, 0f, 0.62f, 0f);
            Solid(0.5f, -64.5f, 0.85f);

            // Smoke column (positions consume the same RNG count as the slice).
            var smokePos = new List<Vector3>();
            for (int i = 0; i < 42; i++)
            {
                float sx = (rand.Next() - 0.5f) * 1.6f;
                float sy = 8f + rand.Next() * 16f;
                float sz = -58f + (rand.Next() - 0.5f) * 1.6f;
                smokePos.Add(new Vector3(sx, GroundH(sx, sz) + sy, sz));
            }
            // Matte gray spheres: the transparent cubes rendered as opaque
            // white blocks, so the smoke is solid stylized puffs instead.
            for (int i = 0; i < 8; i++)
            {
                float pr = 0.45f + (smokePos[i].x + 0.8f) * 0.25f;
                var puff = MeshUtil.Ball(R, "Smoke", pr, 0x6a7078,
                    smokePos[i].x, smokePos[i].y, smokePos[i].z, false);
                refs.smokePuffs.Add(puff);
                refs.smokeBase.Add(smokePos[i]);
            }

            // Ironwood grove past the cave. Pale trunks carry a faint warm
            // shimmer and dark crowns so the grove reads at night.
            var ironMesh = MeshUtil.TaperMesh(0.26f, 0.36f, 5.5f);
            var ironBark = MeshUtil.FreshMat(0xcfc4a8);
            ironBark.EnableKeyword("_EMISSION_ON");
            ironBark.SetColor("_EmissionColor", new Color(0.22f, 0.18f, 0.12f));
            var ironCrownMesh = MeshUtil.TaperMesh(0.02f, 1.5f, 3.2f);
            float[,] grove = new float[,] { {14f,-66f},{18f,-66f},{13f,-70f},{19f,-70f},{16f,-72f},{12f,-68f} };
            for (int i = 0; i < grove.GetLength(0); i++)
            {
                float x = grove[i, 0];
                float z = grove[i, 1];
                var iw = MeshUtil.Tapered(R, "Ironwood", ironMesh, 0xcfc4a8, x, GroundH(x, z) + 2.75f, z);
                iw.GetComponent<Renderer>().sharedMaterial = ironBark;
                MeshUtil.Tapered(R, "IronCrown", ironCrownMesh, 0x1f3a2a, x, GroundH(x, z) + 6.4f, z);
                Solid(x, z, 0.45f);
            }
            var ironLogMesh = MeshUtil.TaperMesh(0.32f, 0.32f, 2.6f);
            var ironLog = MeshUtil.Tapered(R, "IronLog", ironLogMesh, 0xcfc4a8, 16f, GroundH(16f, -68f) + 0.33f, -68f);
            ironLog.transform.localRotation = Quaternion.Euler(0f, 0.4f * 57.3f, 90f);
            var ironGlow = MeshUtil.FreshMat(0xcfc4a8);
            ironGlow.EnableKeyword("_EMISSION_ON");
            ironGlow.SetColor("_EmissionColor", new Color(0.38f, 0.32f, 0.2f));
            ironLog.GetComponent<Renderer>().sharedMaterial = ironGlow;
            refs.ironLog = ironLog;
            // Cool light pooled over the grove: the landmark the dwarf's
            // words point at, visible through the trees from the approach.
            var groveLightGo = new GameObject("GroveLight");
            groveLightGo.transform.SetParent(R, false);
            groveLightGo.transform.localPosition = new Vector3(16f, GroundH(16f, -68f) + 3.5f, -68f);
            var groveLight = groveLightGo.AddComponent<Light>();
            groveLight.type = LightType.Point;
            groveLight.color = MeshUtil.C(0xbfd0e8);
            groveLight.intensity = 2.4f;
            groveLight.range = 20f;

            // Cairn (hidden until a corpse run begins).
            var cairn = new GameObject("Cairn");
            cairn.transform.SetParent(R, false);
            float[,] stones = new float[,] { {0f,0.2f,0f,0.5f},{0.1f,0.55f,0.05f,0.38f},{-0.05f,0.85f,0f,0.27f} };
            for (int i = 0; i < 3; i++)
            {
                MeshUtil.Ball(cairn.transform, "Stone", stones[i, 3], 0x6a7078, stones[i, 0], stones[i, 1], stones[i, 2], false);
            }
            cairn.SetActive(false);
            refs.cairn = cairn;

            // Wolf den dressing: pale bones.
            float[,] bones = new float[,] { {-14.5f,0.5f},{-13.2f,-0.8f},{-15f,-1.5f} };
            for (int i = 0; i < 3; i++)
            {
                var b = MeshUtil.Box(R, "Bone", 0.7f, 0.12f, 0.12f, 0xcfc4a8, bones[i, 0], GroundH(bones[i, 0], bones[i, 1]) + 0.08f, bones[i, 1]);
                b.transform.localRotation = Quaternion.Euler(0f, rand.Next() * 180f, 0f);
            }

            // The mountain: tiered stylized peaks looming beyond the north
            // bound. Bases sit outside playable space so no slope is ever
            // walk-through (the old footprint swallowed the cave and the
            // ironwood grove). Fixed offsets, no RNG.
            void Peak(float x, float z, float r, float h)
            {
                refs.peaks.Add(new Circle(x, z, r));
                var g = new GameObject("Peak");
                g.transform.SetParent(R, false);
                g.transform.localPosition = new Vector3(x, GroundH(x, z), z);
                g.transform.localRotation = Quaternion.Euler(0f, (x * 7f + z * 3f) % 60f, 0f);
                var baseMesh = MeshUtil.TaperMesh(0.02f, r, h * 0.62f);
                MeshUtil.Tapered(g.transform, "Base", baseMesh, 0x646c7a, 0f, h * 0.31f, 0f);
                var midMesh = MeshUtil.TaperMesh(0.02f, r * 0.62f, h * 0.42f);
                MeshUtil.Tapered(g.transform, "Mid", midMesh, 0x585f6c, 1.2f, h * 0.55f, -0.8f);
                var capMesh = MeshUtil.TaperMesh(0.02f, r * 0.28f, h * 0.3f);
                MeshUtil.Tapered(g.transform, "Cap", capMesh, 0xa8b4cc, 1.6f, h * 0.78f, -1f);
            }
            Peak(0f, -104f, 24f, 40f);
            Peak(-54f, -96f, 15f, 26f);
            Peak(54f, -96f, 15f, 26f);
            float[,] talus = new float[,] { {-14f,-70f,2.2f},{10f,-73f,1.8f},{-6f,-72f,1.4f},{20f,-66f,2.6f},{-22f,-66f,2.4f} };
            for (int i = 0; i < talus.GetLength(0); i++)
            {
                var t = MeshUtil.Box(R, "Talus", talus[i, 2], talus[i, 2] * 0.8f, talus[i, 2], 0x5a6068, talus[i, 0], GroundH(talus[i, 0], talus[i, 1]) + talus[i, 2] * 0.35f, talus[i, 1]);
                t.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
                Solid(talus[i, 0], talus[i, 1], talus[i, 2] * 0.7f);
            }
            // (No footprint colliders: the peaks stand outside the bounds,
            // and the cave walls below carry their own.)

            // Second forest band on its own RNG stream, so the original
            // layout above is untouched bit-for-bit.
            var rand2 = new Mulberry(777);
            int extra = 0;
            int guard2 = 0;
            while (extra < 90 && guard2++ < 2000)
            {
                float x = (rand2.Next() * 2f - 1f) * 62f;
                float z = -78f + rand2.Next() * 118f;
                if (Mathf.Abs(x) < 5f && z > -62f && z < 32f) continue;
                bool bad = false;
                for (int c = 0; c < clearings.GetLength(0); c++)
                {
                    float dx = x - clearings[c, 0];
                    float dz = z - clearings[c, 1];
                    float cr = clearings[c, 2];
                    if (dx * dx + dz * dz < cr * cr) { bad = true; break; }
                }
                if (bad) continue;
                float s = 0.8f + rand2.Next() * 0.7f;
                var g = new GameObject("Tree");
                g.transform.SetParent(forest.transform, false);
                g.transform.localPosition = new Vector3(x, GroundH(x, z), z);
                g.transform.localScale = Vector3.one * s;
                g.transform.localRotation = Quaternion.Euler(0f, rand2.Next() * 180f, 0f);
                MeshUtil.Tapered(g.transform, "Trunk", trunkMesh, 0x4a3a28, 0f, 1.6f, 0f);
                uint cc = rand2.Next() < 0.5f ? 0x2e5233u : 0x38663bu;
                MeshUtil.Tapered(g.transform, "Canopy", canopyMesh, cc, 0f, 5.4f, 0f);
                Solid(x, z, 0.5f);
                extra++;
            }
            refs.treeCount = treePos.Count + extra;

            // Undergrowth bushes (no colliders; walk-through).
            var bushes = new GameObject("Bushes");
            bushes.transform.SetParent(R, false);
            int nb = 0;
            int guard3 = 0;
            while (nb < 40 && guard3++ < 1200)
            {
                float x = (rand2.Next() * 2f - 1f) * 58f;
                float z = -76f + rand2.Next() * 112f;
                if (Mathf.Abs(x) < 4f && z > -62f && z < 32f) continue;
                bool bad = false;
                for (int c = 0; c < clearings.GetLength(0); c++)
                {
                    float dx = x - clearings[c, 0];
                    float dz = z - clearings[c, 1];
                    float cr = clearings[c, 2] * 0.7f;
                    if (dx * dx + dz * dz < cr * cr) { bad = true; break; }
                }
                if (bad) continue;
                float br = 0.7f + rand2.Next() * 0.5f;
                var b = MeshUtil.Ball(bushes.transform, "Bush", br, 0x24402a, x, GroundH(x, z) + br * 0.5f, z, false);
                b.transform.localScale = new Vector3(br * 2f, br * 1.1f, br * 2f);
                nb++;
            }

            // Approach dressing: fixed rocks, pines, and dirt so the walk
            // to the cave is not an empty plain. All clear of the path.
            float[,] aRocks = new float[,] { {8f,-24f,1.6f},{-9f,-30f,2.2f},{10f,-38f,1.4f},{-8f,-44f,1.8f},{9f,-50f,2f},{-11f,-52f,1.5f} };
            for (int i = 0; i < aRocks.GetLength(0); i++)
            {
                float sz = aRocks[i, 2];
                var t = MeshUtil.Box(R, "ApproachRock", sz, sz * 0.75f, sz, 0x5a6068, aRocks[i, 0], GroundH(aRocks[i, 0], aRocks[i, 1]) + sz * 0.3f, aRocks[i, 1]);
                t.transform.localRotation = Quaternion.Euler(0f, i * 53f, 0f);
                Solid(aRocks[i, 0], aRocks[i, 1], sz * 0.6f);
            }
            float[,] aPines = new float[,] { {8f,-28f},{-9f,-35f},{11f,-43f},{-10f,-48f},{8f,-53f},{-8f,-26f} };
            for (int i = 0; i < aPines.GetLength(0); i++)
            {
                float x = aPines[i, 0];
                float z = aPines[i, 1];
                var g = new GameObject("Tree");
                g.transform.SetParent(forest.transform, false);
                g.transform.localPosition = new Vector3(x, GroundH(x, z), z);
                MeshUtil.Tapered(g.transform, "Trunk", trunkMesh, 0x4a3a28, 0f, 1.6f, 0f);
                MeshUtil.Tapered(g.transform, "Canopy", canopyMesh, 0x2e5233, 0f, 5.4f, 0f);
                Solid(x, z, 0.5f);
            }
            Dirt(8f, -28f, 3f);
            Dirt(-9f, -35f, 3f);
            Dirt(0f, -48f, 4f);
            Dirt(5f, -52f, 3f);
            Dirt(-6f, -20f, 3f);
            Dirt(10f, -44f, 3f);

            return world;
        }
    }

    public class WorldAnimator : MonoBehaviour
    {
        public Light forgeLight;
        public List<GameObject> smokePuffs;
        public List<Vector3> smokeBase;
        private float t;

        void Update()
        {
            t += Time.deltaTime;
            if (forgeLight != null)
            {
                forgeLight.intensity = 3f + Mathf.Sin(t * 9f) * 0.4f + Mathf.Sin(t * 23f) * 0.25f;
            }
            if (smokePuffs != null)
            {
                for (int i = 0; i < smokePuffs.Count; i++)
                {
                    var p = smokePuffs[i];
                    if (p == null) continue;
                    var pos = p.transform.localPosition;
                    pos.y += Time.deltaTime * 1.2f;
                    float lo = i < smokeBase.Count ? smokeBase[i].y : pos.y;
                    if (pos.y > lo + 18f) pos.y = lo;
                    pos.x += Mathf.Sin(t + i) * Time.deltaTime * 0.3f;
                    p.transform.localPosition = pos;
                }
            }
        }
    }
}
