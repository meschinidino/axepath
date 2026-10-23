// Static valley, built in code: ground, river, lumber village, mill,
// forest, landmarks, mountain cave, grove. Seeded RNG keeps the valley
// identical every load. Layout is the proven slice footprint grown 5x,
// Riverwood-style: the village sits on a riverbend with a mill and bridge.
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

    // What the axe strikes: wood bursts chips, stone and metal
    // throw sparks. Tagged at placement — radius can't tell a wooden
    // hut (r=3.2) from a rock (r=2.5).
    public enum SurfaceKind { Wood, Stone, Metal }

    public struct Circle
    {
        public float x;
        public float z;
        public float r;
        public SurfaceKind kind;
        public Circle(float x, float z, float r, SurfaceKind kind) { this.x = x; this.z = z; this.r = r; this.kind = kind; }
    }

    public class Spots
    {
        public Vec2 spawn = new Vec2(0f, 210f);
        public Vec2 yard = new Vec2(-30f, 180f);
        public Vec2 caveMouth = new Vec2(0f, -280f);
        public Vec2 den = new Vec2(-70f, 0f);
        public Vec2 librarian = new Vec2(48f, 185f);
        public Vec2 dwarf = new Vec2(-4f, -312f);
        // Wolf pack homes: three at the main den, one ranging the east
        // bank, one haunting the last leg of the climb.
        public List<Vec2> wolfHomes = new List<Vec2> {
            new Vec2(-70f, 0f), new Vec2(-74f, 4f), new Vec2(-66f, -4f),
            new Vec2(95f, -60f), new Vec2(-45f, -200f),
        };
    }

    public class WorldRefs
    {
        public GameObject axeMesh;
        public GameObject bookMesh;
        public GameObject ironLog;
        public GameObject cairn;
        public List<GameObject> landmarkPines = new List<GameObject>();
        public Light forgeLight;
        public Light mouthLight;
        public List<GameObject> smokePuffs = new List<GameObject>();
        public List<Vector3> smokeBase = new List<Vector3>();
        public Vec2 cavePeak = new Vec2(0f, -375f);
        public float cavePeakR = 55f;
        public float cavePeakH = 130f;
        public List<GameObject> riverFoam = new List<GameObject>();
        public List<GameObject> riverStreaks = new List<GameObject>();
        public GameObject millWheel;
        public Material waterMat;
        public List<GameObject> clouds = new List<GameObject>();
        public ParticleSystem leafPS;
        public List<Material> fireflyMats = new List<Material>();
        public List<Vec2> blazePos = new List<Vec2>();
        public List<Vec2> bushPos = new List<Vec2>();
        public List<GameObject> lanternFlames = new List<GameObject>();
        public Vector3 axeStandPos = new Vector3(-30f, 0f, 180f);
        public Vector3 bookStandPos = new Vector3(35f, 0f, 190f);
        public Vector3 ironLogPos = new Vector3(72f, 0f, -340f);
        public List<Circle> peaks = new List<Circle>();
        public Vec2 guidePos = new Vec2(13f, -276.4f);
        public Vec2 waystonePos = new Vec2(-10f, -80f);
        public int treeCount;
        public int flowerCount;
        public int bushCount;
    }

    public class GameWorld
    {
        public GameObject root;
        public List<Circle> colliders = new List<Circle>();
        public Spots spots = new Spots();
        public WorldRefs refs = new WorldRefs();
        public float minX = -300f;
        public float maxX = 300f;
        public float minZ = -400f;
        public float maxZ = 250f;
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
                m.SetFloat("_Glossiness", 0.32f);
                TextureFactory.Dress(m, TextureFactory.FamilyFor(hex));
                matCache[hex] = m;
            }
            return m;
        }

        public static Material FreshMat(uint hex)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = C(hex);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.38f);
            TextureFactory.Dress(m, TextureFactory.FamilyFor(hex));
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
            return Tapered(parent, name, mesh, Mat(color), x, y, z);
        }

        public static GameObject Tapered(Transform parent, string name, Mesh mesh, Material mat, float x, float y, float z)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            return go;
        }

        // Shaped rock: two flattened, offset stones read as one crag
        // instead of a ball. Flat facets come from the sphere's own
        // segments at low scale contrast; rotation sells the rest.
        public static GameObject Crag(Transform parent, string name, float r, uint color, float x, float y, float z, float seed)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(x, y, z);
            g.transform.localRotation = Quaternion.Euler(0f, seed * 57f % 360f, 0f);
            var a = Ball(g.transform, "CragA", r, color, 0f, 0f, 0f, false);
            a.transform.localScale = new Vector3(r * 2.1f, r * 1.25f, r * 1.7f);
            var b = Ball(g.transform, "CragB", r * 0.62f, color, r * 0.55f, r * 0.32f, r * 0.2f, false);
            b.transform.localScale = new Vector3(r * 1.3f, r * 0.85f, r * 1.1f);
            return g;
        }

        // Timber frame: dark beams on the corners and eaves of a box so
        // a wall reads as built carpentry, not a colored cube.
        public static void TimberFrame(Transform parent, float w, float h, float d, float yBase)
        {
            uint beam = 0x2c2118;
            float hw = w / 2f, hd = d / 2f;
            Box(parent, "Beam", 0.18f, h, 0.18f, beam, -hw, yBase + h / 2f, -hd);
            Box(parent, "Beam", 0.18f, h, 0.18f, beam, hw, yBase + h / 2f, -hd);
            Box(parent, "Beam", 0.18f, h, 0.18f, beam, -hw, yBase + h / 2f, hd);
            Box(parent, "Beam", 0.18f, h, 0.18f, beam, hw, yBase + h / 2f, hd);
            Box(parent, "Beam", w + 0.15f, 0.16f, 0.16f, beam, 0f, yBase + h - 0.1f, -hd);
            Box(parent, "Beam", w + 0.15f, 0.16f, 0.16f, beam, 0f, yBase + h - 0.1f, hd);
        }

        // Solid prism roof: two slopes plus closed gable ends, so the
        // roof has a body from every angle instead of two floating
        // sheets with daylight under them.
        public static Mesh PrismMesh(float w, float rise, float d)
        {
            float hw = w / 2f, hd = d / 2f;
            var v = new Vector3[] {
                new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd),
                new Vector3(hw, 0f, hd), new Vector3(-hw, 0f, hd),
                new Vector3(0f, rise, -hd), new Vector3(0f, rise, hd),
            };
            // Quads split into outward-wound triangles.
            var tris = new int[] {
                0, 5, 4, 0, 3, 5,   // left slope
                1, 5, 2, 1, 4, 5,   // right slope
                0, 4, 1,            // front gable
                2, 5, 3,            // back gable
                0, 1, 2, 0, 2, 3,   // underside
            };
            var mesh = new Mesh();
            mesh.vertices = v;
            mesh.triangles = tris;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(v[i].x + v[i].z, v[i].y + v[i].z);
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void PitchedRoof(Transform parent, string name, float w, float rise, float d, uint color, float y, float cx = 0f, float cz = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(cx, y, cz);
            go.AddComponent<MeshFilter>().sharedMesh = PrismMesh(w, rise, d);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color);
            Box(parent, "Ridge", 0.24f, 0.18f, d, 0x2c2118, cx, y + rise + 0.05f, cz);
        }
    }

    public static class WorldBuilder
    {
        // Uphill grade: the village (z>=150) is flat at 0, the forest ramps
        // up 40m to the cave plateau (z<=-260, flat at 40). Everything that
        // stands on the ground samples GroundH; colliders stay 2D.
        public const float PlateauH = 40f;
        public const float RiverZ0 = -420f;
        public const float RiverZ1 = 320f;
        public const float BridgeZ = 205f;

        // The walking path: an S-curving line from the village to the cave
        // mouth, so no leg of the journey sees all the way to the next.
        // Waypoints as (z, x), south to north.
        private static readonly float[,] pathWaypoints = new float[,] {
            {210f,0f},{180f,-8f},{150f,0f},{120f,18f},{90f,30f},{60f,12f},
            {30f,10f},{0f,-12f},{-40f,-28f},{-80f,-18f},{-120f,8f},
            {-160f,20f},{-200f,5f},{-240f,-5f},{-270f,0f},
        };

        public static float PathX(float z)
        {
            int n = pathWaypoints.GetLength(0);
            if (z >= pathWaypoints[0, 0]) return pathWaypoints[0, 1];
            for (int i = 0; i < n - 1; i++)
            {
                float z0 = pathWaypoints[i, 0];
                float z1 = pathWaypoints[i + 1, 0];
                if (z <= z0 && z >= z1)
                {
                    float t = (z0 - z) / (z0 - z1);
                    return pathWaypoints[i, 1] + (pathWaypoints[i + 1, 1] - pathWaypoints[i, 1]) * t;
                }
            }
            return pathWaypoints[n - 1, 1];
        }

        public static bool NearPath(float x, float z, float half)
        {
            if (z <= -290f || z >= 160f) return false;
            return Mathf.Abs(x - PathX(z)) < half;
        }

        // The river runs the whole valley north to south, east of the path,
        // bending past the village like Riverwood's. Analytic so the ground
        // carve, the water ribbon, and every avoidance check agree.
        public static float RiverX(float z)
        {
            return 125f + 25f * Mathf.Sin(z * 0.008f + 2f) + 10f * Mathf.Sin(z * 0.021f);
        }

        public static float BaseH(float z)
        {
            if (z >= 150f) return 0f;
            if (z <= -260f) return PlateauH;
            float t = (150f - z) / 410f;
            t = t * t * (3f - 2f * t);
            return PlateauH * t;
        }

        public static float GroundH(float x, float z)
        {
            float h = BaseH(z);
            // River channel: a shallow fordable carve, flattened under the
            // bridge so the deck sits at grade and feet never clip through.
            float dx = Mathf.Abs(x - RiverX(z));
            if (dx < 14f)
            {
                float prof = 0.5f + 0.5f * Mathf.Cos(dx / 14f * Mathf.PI);
                float bz = Mathf.Abs(z - BridgeZ);
                float bcut = bz < 6f ? bz / 6f : 1f;
                bcut = bcut * bcut * (3f - 2f * bcut);
                h -= 1.3f * prof * bcut;
            }
            return h;
        }

        public static void BuildGround(Transform parent)
        {
            int nx = 160, nz = 100;
            float x0 = -375f, x1 = 375f, z0 = -425f, z1 = 325f;
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
            var groundMat = MeshUtil.FreshMat(0x24382a);
            TextureFactory.Dress(groundMat, "ground");
            groundMat.mainTextureScale = new Vector2(26f, 26f);
            groundMat.SetTextureScale("_BumpMap", new Vector2(26f, 26f));
            // No bump on the ground: at grazing angles a repeating
            // normal map swims white/green as you walk.
            groundMat.SetFloat("_BumpScale", 0f);
            // Near-matte: smooth ground under a hard moon throws one
            // huge white specular sheet across the whole field.
            groundMat.SetFloat("_Glossiness", 0.04f);
            go.AddComponent<MeshRenderer>().sharedMaterial = groundMat;
        }

        // Shared build state threaded through the per-area builders below.
        // One instance per Build call; forest/cave/backdrop share one RNG
        // stream (seed 1337) while everything new gets its own, so the old
        // layout's texture survives under the new scale.
        private sealed class BuildCtx
        {
            public readonly GameWorld world;
            public readonly Transform R;
            public Mesh dirtMesh;
            public Material dirtMat;
            public Mesh trunkMesh;
            public Mesh canopyMesh;
            public Mesh oakTrunkMesh;
            public Mesh birchCanopyMesh;
            public Mulberry rand;
            public float[,] clearings;
            public GameObject forest;
            public List<Vector3> treePos;

            public BuildCtx(GameWorld world, Transform R)
            {
                this.world = world;
                this.R = R;
            }

            public void Solid(float x, float z, float r, SurfaceKind kind) { world.colliders.Add(new Circle(x, z, r, kind)); }

            // Overlapping discs stack at different top heights (default
            // 0.04): coplanar neighbors z-fight, worse at long range.
            public void Dirt(float x, float z, float r, float top = 0.04f)
            {
                var p = MeshUtil.Tapered(R, "Dirt", dirtMesh, dirtMat, x, GroundH(x, z) + top - 0.02f, z);
                p.transform.localScale = new Vector3(r, 1f, r);
                float slope = (GroundH(x, z + 1f) - GroundH(x, z - 1f)) * 0.5f;
                p.transform.localRotation = Quaternion.Euler(-Mathf.Atan(slope) * 57.29578f, 0f, 0f);
            }

            public void Peak(float x, float z, float r, float h)
            {
                world.refs.peaks.Add(new Circle(x, z, r, SurfaceKind.Stone));
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

            // A single rock cone for the cave mountain. Base sits below grade
            // so the skirt meets the ground with no floating rim. Colliders
            // are the caller's job: some cones (the high peak over the cave
            // roof) are visual-only by design.
            public void RockCone(float x, float z, float r, float h, float baseY, bool snow)
            {
                var g = new GameObject("Mountain");
                g.transform.SetParent(R, false);
                g.transform.localPosition = new Vector3(x, baseY, z);
                g.transform.localRotation = Quaternion.Euler(0f, (x * 13f + z * 7f) % 60f, 0f);
                var rockMesh = MeshUtil.TaperMesh(0.02f, r, h);
                MeshUtil.Tapered(g.transform, "Rock", rockMesh, 0x5a6068, 0f, h * 0.5f, 0f);
                if (snow)
                {
                    // Cap sits slightly proud of the rock taper: an exact
                    // match z-fights along the whole upper cone.
                    var snowMesh = MeshUtil.TaperMesh(0.02f, r * 0.37f, h * 0.34f);
                    MeshUtil.Tapered(g.transform, "Snow", snowMesh, 0xa8b4cc, 0f, h * 0.83f, 0f);
                }
            }
        }

        // Staged construction for the boot loading screen: the stager runs
        // one Step per frame. Stage order and RNG draws match Build exactly.
        public class StagedBuild
        {
            public static readonly string[] StageNames = new string[]
            {
                "Laying the ground",
                "Raising the village",
                "Running the river",
                "Growing the forest",
                "Cutting the cave",
                "Lighting the grove",
                "Raising the peaks",
                "Filling the wilds",
                "Scattering petals",
                "Dressing the wilds",
                "Merging statics",
            };

            public readonly GameWorld world;
            private readonly BuildCtx ctx;
            private int stage;

            public int StageCount { get { return StageNames.Length; } }
            public int Stage { get { return stage; } }

            public StagedBuild(Transform parent)
            {
                world = new GameWorld();
                var root = new GameObject("World");
                root.transform.SetParent(parent, false);
                world.root = root;
                ctx = new BuildCtx(world, root.transform);
            }

            // Runs the next stage. Returns true while stages remain after it.
            public bool Step()
            {
                switch (stage)
                {
                    case 0: BuildShared(ctx); break;
                    case 1: BuildVillage(ctx); break;
                    case 2: BuildRiver(ctx); break;
                    case 3: BuildForest(ctx); break;
                    case 4: BuildCave(ctx); break;
                    case 5: BuildGrove(ctx); break;
                    case 6: BuildBackdrop(ctx); break;
                    case 7: BuildInfill(ctx); break;
                    case 8: BuildFlowers(ctx); break;
                    case 9: DetailBuilder.Build(ctx.world, ctx.R, ctx.clearings); break;
                    case 10: MergeStatics(ctx); break;
                    default: break;
                }
                stage++;
                return stage < StageNames.Length;
            }
        }

        public static GameWorld Build(Transform parent)
        {
            var b = new StagedBuild(parent);
            while (b.Step()) { }
            return b.world;
        }

        private static void BuildShared(BuildCtx ctx)
        {
            var R = ctx.R;

            // Ground plus dirt patches.
            BuildGround(R);
            ctx.dirtMesh = MeshUtil.TaperMesh(1f, 1f, 0.04f);
            // Dedicated dirt material: the discs once shared the
            // wood-brown Mat cache entry, whose pale bark texture blew
            // out to a cream sheet under moon+lantern light and read
            // as broken white ground. Dark earth, tiled 3x per disc
            // so the cap UV shows mottling instead of one flat smear.
            ctx.dirtMat = MeshUtil.FreshMat(0x5f4629);
            TextureFactory.Dress(ctx.dirtMat, "dirt");
            ctx.dirtMat.mainTextureScale = new Vector2(4f, 4f);
            ctx.dirtMat.SetTextureScale("_BumpMap", new Vector2(4f, 4f));
            ctx.dirtMat.SetFloat("_Glossiness", 0.12f);
            ctx.Dirt(-30f, 190f, 28f, 0.10f);
            ctx.Dirt(35f, 190f, 24f, 0.16f);
            ctx.Dirt(0f, 210f, 20f, 0.22f);
            ctx.Dirt(0f, -292f, 36f, 0.10f);
            ctx.Dirt(72f, -340f, 22f, 0.22f);
            var rockPatch = MeshUtil.Tapered(R, "RockPatch", ctx.dirtMesh, 0x3a3f46, 0f, GroundH(0f, -310f) + 0.14f, -310f);
            rockPatch.transform.localScale = new Vector3(40f, 1f, 40f);

            // Walking path: dirt discs sampled along the winding line.
            for (float z = 210f; z >= -270f; z -= 20f) ctx.Dirt(PathX(z), z, 9f);
            // East turn to the grove: a fainter fork past the cave mouth.
            ctx.Dirt(30f, -290f, 8f);
            ctx.Dirt(48f, -305f, 8f);
            ctx.Dirt(60f, -322f, 8f);
        }

        private static void BuildVillage(BuildCtx ctx)
        {
            var R = ctx.R;
            var refs = ctx.world.refs;

            // Lumber yard: log piles, axe stand, the yard axe.
            var logMesh = MeshUtil.TaperMesh(0.35f, 0.35f, 4f);
            float[,] piles = new float[,] { {-50f, 190f}, {-45f, 202f}, {-15f, 200f} };
            for (int p = 0; p < 3; p++)
            {
                float lx = piles[p, 0];
                float lz = piles[p, 1];
                for (int i = 0; i < 3; i++)
                {
                    var log = MeshUtil.Tapered(R, "Log", logMesh, 0x5a4630, lx, 0.4f + (i == 2 ? 0.62f : 0f), lz + (i == 0 ? -0.4f : i == 1 ? 0.4f : 0f));
                    log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                ctx.Solid(lx, lz, 2.2f, SurfaceKind.Wood);
            }
            var stand = new GameObject("Stand");
            stand.transform.SetParent(R, false);
            stand.transform.localPosition = new Vector3(-30f, 0f, 180f);
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
            ctx.Solid(-30f, 180f, 0.9f, SurfaceKind.Wood);
            refs.axeMesh = yardAxe;

            // Book nook: open shelves under a cap, replacing the old
            // lean-to. Same spot, same book id, same reach: the takeable
            // book waits on the lectern out front.
            var nook = new GameObject("BookNook");
            nook.transform.SetParent(R, false);
            nook.transform.localPosition = new Vector3(35f, 0f, 190f);
            MeshUtil.Box(nook.transform, "Back", 3.6f, 2.8f, 0.3f, 0x4f4234, 0f, 1.4f, -1.2f);
            MeshUtil.Box(nook.transform, "Side", 0.3f, 2.8f, 2.6f, 0x4f4234, -1.8f, 1.4f, 0f);
            MeshUtil.Box(nook.transform, "Side", 0.3f, 2.8f, 2.6f, 0x4f4234, 1.8f, 1.4f, 0f);
            MeshUtil.Box(nook.transform, "Roof", 4.4f, 0.2f, 3.2f, 0x3a2f22, 0f, 2.9f, 0f);
            uint[] shelfCols = new uint[] { 0x7a4a3a, 0x3a5a7a, 0x4a7a3a, 0x7a6a3a, 0x5a3a7a };
            for (int s = 0; s < 3; s++)
            {
                float sy = 0.7f + s * 0.75f;
                MeshUtil.Box(nook.transform, "Shelf", 3.3f, 0.1f, 0.5f, 0x5a4630, 0f, sy, -0.9f);
                for (int b = 0; b < 8; b++)
                {
                    uint bc = shelfCols[(s * 8 + b) % 5];
                    MeshUtil.Box(nook.transform, "Tome", 0.28f, 0.5f, 0.36f, bc, -1.25f + b * 0.36f, sy + 0.3f, -0.9f);
                }
            }
            MeshUtil.Box(nook.transform, "Lectern", 0.7f, 1.1f, 0.5f, 0x3a2f22, 0f, 0.55f, 1.2f);
            refs.bookMesh = MeshUtil.Box(nook.transform, "Book", 0.5f, 0.1f, 0.36f, 0xd9cfae, 0f, 1.15f, 1.2f);
            ctx.Solid(35f, 190f, 2.6f, SurfaceKind.Wood);

            // Two village huts: timber frame, plank walls, pitched roofs.
            float[,] huts = new float[,] { {-80f, 220f}, {80f, 220f} };
            for (int i = 0; i < 2; i++)
            {
                float hx = huts[i, 0];
                float hz = huts[i, 1];
                var hut = new GameObject("Hut");
                hut.transform.SetParent(R, false);
                hut.transform.localPosition = new Vector3(hx, 0f, hz);
                MeshUtil.Box(hut.transform, "Walls", 4f, 2.4f, 3.4f, 0x4f4234, 0f, 1.2f, 0f);
                MeshUtil.Box(hut.transform, "StoneBase", 4.3f, 0.5f, 3.7f, 0x565c66, 0f, 0.25f, 0f);
                MeshUtil.TimberFrame(hut.transform, 4f, 2.4f, 3.4f, 0f);
                MeshUtil.PitchedRoof(hut.transform, "Roof", 4.6f, 1.5f, 4.2f, 0x3a2f22, 2.4f);
                // Lit window + door: the village is lived-in, not a diorama.
                var win = MeshUtil.Box(hut.transform, "WindowGlow", 0.7f, 0.8f, 0.08f, 0xffb45e, 0.9f, 1.35f, 1.74f);
                win.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xffb45e);
                MeshUtil.Box(hut.transform, "Door", 0.9f, 1.9f, 0.1f, 0x2c2118, -0.9f, 0.95f, 1.72f);
                MeshUtil.Box(hut.transform, "ChimneyStone", 0.6f, 1.4f, 0.6f, 0x565c66, 1.1f, 3.4f, -0.6f);
                ctx.Solid(hx, hz, 3.2f, SurfaceKind.Wood);
            }

            // Lantern posts: warm pools guiding spawn -> yard -> library.
            float[,] posts = new float[,] { {-12f, 198f}, {12f, 196f}, {-28f, 170f}, {30f, 178f}, {0f, 150f} };
            for (int i = 0; i < posts.GetLength(0); i++)
            {
                float lx = posts[i, 0], lz = posts[i, 1];
                float gy = GroundH(lx, lz);
                var post = new GameObject("LanternPost");
                post.transform.SetParent(R, false);
                post.transform.localPosition = new Vector3(lx, gy, lz);
                MeshUtil.Box(post.transform, "PostWood", 0.16f, 2.6f, 0.16f, 0x3a2f22, 0f, 1.3f, 0f);
                MeshUtil.Box(post.transform, "ArmWood", 0.7f, 0.12f, 0.12f, 0x3a2f22, 0.3f, 2.55f, 0f);
                var flame = MeshUtil.Ball(post.transform, "LanternFlame", 0.16f, 0xffc46b, 0.6f, 2.25f, 0f, true);
                refs.lanternFlames.Add(flame);
                var lg = new GameObject("LanternLight");
                lg.transform.SetParent(post.transform, false);
                lg.transform.localPosition = new Vector3(0.6f, 2.25f, 0f);
                var ll = lg.AddComponent<Light>();
                ll.type = LightType.Point; ll.color = MeshUtil.C(0xffa04a);
                ll.intensity = 1.6f; ll.range = 16f;
                ctx.Solid(lx, lz, 0.35f, SurfaceKind.Wood);
            }

            // The chapel: stone nave, steep roof, bell tower. The village
            // has a conscience and a landmark you can navigate by.
            var chapel = new GameObject("Chapel");
            chapel.transform.SetParent(R, false);
            chapel.transform.localPosition = new Vector3(58f, 0f, 230f);
            MeshUtil.Box(chapel.transform, "Nave", 5f, 3.4f, 8f, 0x6a7078, 0f, 1.7f, 0f);
            MeshUtil.Box(chapel.transform, "StoneBase", 5.4f, 0.6f, 8.4f, 0x565c66, 0f, 0.3f, 0f);
            MeshUtil.PitchedRoof(chapel.transform, "Roof", 5.8f, 2.2f, 8.8f, 0x2c3440, 3.4f);
            MeshUtil.Box(chapel.transform, "Tower", 2.2f, 7.5f, 2.2f, 0x6a7078, 0f, 3.75f, -4.6f);
            MeshUtil.PitchedRoof(chapel.transform, "TowerRoof", 2.8f, 1.6f, 2.8f, 0x2c3440, 7.5f, 0f, -4.6f);
            MeshUtil.Box(chapel.transform, "BellArch", 1.1f, 1.5f, 0.15f, 0x1a1d22, 0f, 6.4f, -5.75f);
            MeshUtil.Tapered(chapel.transform, "Bell", MeshUtil.TaperMesh(0.12f, 0.42f, 0.7f), 0xd9a441, 0f, 6.35f, -5.9f);
            // Nave windows + buttresses: a stone box from every side.
            for (int s = -1; s <= 1; s += 2)
            {
                var cw = MeshUtil.Box(chapel.transform, "WindowGlow", 0.08f, 1.1f, 0.6f, 0xffd98a, s * 2.56f, 2f, -1.8f);
                cw.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xffd98a);
                var cw2 = MeshUtil.Box(chapel.transform, "WindowGlow", 0.08f, 1.1f, 0.6f, 0xffd98a, s * 2.56f, 2f, 1.8f);
                cw2.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xffd98a);
                MeshUtil.Box(chapel.transform, "Buttress", 0.5f, 2.6f, 0.6f, 0x565c66, s * 2.75f, 1.3f, -3.4f);
                MeshUtil.Box(chapel.transform, "Buttress", 0.5f, 2.6f, 0.6f, 0x565c66, s * 2.75f, 1.3f, 3.4f);
            }
            MeshUtil.Ball(chapel.transform, "RoseWindow", 0.55f, 0xffd98a, 0f, 2.6f, 4.06f, true);
            MeshUtil.Box(chapel.transform, "ChapelDoor", 1.2f, 2.2f, 0.12f, 0x2c2118, 0f, 1.1f, 4.03f);
            MeshUtil.Box(chapel.transform, "CrossV", 0.1f, 0.9f, 0.1f, 0xd9cfae, 0f, 9.6f, -4.6f);
            MeshUtil.Box(chapel.transform, "CrossH", 0.55f, 0.1f, 0.1f, 0xd9cfae, 0f, 9.65f, -4.6f);
            ctx.Solid(58f, 230f, 4.2f, SurfaceKind.Stone);
            // Graveyard: the permadeath promise, written in small stones.
            for (int i = 0; i < 6; i++)
            {
                float gx = 51f + (i % 3) * 1.6f, gz = 236f + (i / 3) * 2.2f;
                MeshUtil.Box(chapel.transform, "Gravestone", 0.55f, 0.8f, 0.12f, 0x565c66, gx - 58f, 0.4f, gz - 230f);
            }

            // The inn: two floors, porch, hanging sign. Where travellers
            // and rumors both sleep. (Ours keeps its own name and face.)
            var inn = new GameObject("Inn");
            inn.transform.SetParent(R, false);
            inn.transform.localPosition = new Vector3(-52f, 0f, 228f);
            MeshUtil.Box(inn.transform, "Walls", 7f, 4.6f, 5f, 0x5a4630, 0f, 2.3f, 0f);
            MeshUtil.Box(inn.transform, "StoneBase", 7.4f, 0.6f, 5.4f, 0x565c66, 0f, 0.3f, 0f);
            MeshUtil.TimberFrame(inn.transform, 7f, 4.6f, 5f, 0f);
            MeshUtil.PitchedRoof(inn.transform, "Roof", 7.8f, 2f, 5.8f, 0x3a2f22, 4.6f);
            MeshUtil.Box(inn.transform, "PorchRoof", 5f, 0.14f, 1.8f, 0x3a2f22, 0f, 2.7f, 3.1f);
            MeshUtil.Box(inn.transform, "PorchPost", 0.16f, 2.6f, 0.16f, 0x2c2118, -2.3f, 1.3f, 3.8f);
            MeshUtil.Box(inn.transform, "PorchPost", 0.16f, 2.6f, 0.16f, 0x2c2118, 2.3f, 1.3f, 3.8f);
            var innWin = MeshUtil.Box(inn.transform, "WindowGlow", 0.8f, 0.9f, 0.08f, 0xffb45e, -1.8f, 1.5f, 2.54f);
            innWin.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xffb45e);
            var innWin2 = MeshUtil.Box(inn.transform, "WindowGlow", 0.8f, 0.9f, 0.08f, 0xffb45e, 1.8f, 3.4f, 2.54f);
            innWin2.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xffb45e);
            MeshUtil.Box(inn.transform, "InnDoor", 1.1f, 2.1f, 0.12f, 0x2c2118, 0.4f, 1.05f, 2.53f);
            MeshUtil.Box(inn.transform, "SignPost", 0.16f, 3f, 0.16f, 0x2c2118, 3.4f, 1.5f, 3.2f);
            MeshUtil.Box(inn.transform, "SignArm", 1.1f, 0.12f, 0.12f, 0x2c2118, 2.95f, 2.85f, 3.2f);
            var sign = MeshUtil.Box(inn.transform, "InnSign", 0.9f, 0.7f, 0.08f, 0xd9a441, 2.55f, 2.3f, 3.2f);
            sign.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xd9a441);
            MeshUtil.Box(inn.transform, "ChimneyStone", 0.7f, 1.8f, 0.7f, 0x565c66, -2.4f, 5.4f, -1f);
            ctx.Solid(-52f, 228f, 4.4f, SurfaceKind.Wood);
        }

        // Flat ribbon mesh from left/right edge samples. Double-sided: the
        // winding that reads correctly from one bank mirrors from the other.
        private static GameObject Strip(Transform parent, string name, Material mat, List<Vector3> left, List<Vector3> right)
        {
            int n = left.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i * 2] = left[i];
                verts[i * 2 + 1] = right[i];
                uvs[i * 2] = new Vector2(0f, i / (float)n);
                uvs[i * 2 + 1] = new Vector2(1f, i / (float)n);
            }
            var idx = new int[(n - 1) * 6];
            int k = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                idx[k++] = a; idx[k++] = c; idx[k++] = b;
                idx[k++] = b; idx[k++] = c; idx[k++] = d;
            }
            var m = new Mesh();
            m.vertices = verts; m.uv = uvs; m.triangles = idx;
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void BuildRiver(BuildCtx ctx)
        {
            var R = ctx.R;
            var refs = ctx.world.refs;

            // Water ribbon down the carved channel, sand on both slopes. The
            // water is opaque Standard (never Unlit/Transparent: that renders
            // white); the shimmer is metalness plus an animator pulse.
            var water = MeshUtil.FreshMat(0x2a4a6b);
            water.SetFloat("_Metallic", 0.55f);
            water.SetFloat("_Glossiness", 0.7f);
            water.EnableKeyword("_EMISSION_ON");
            water.SetColor("_EmissionColor", new Color(0.03f, 0.07f, 0.11f));
            water.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            refs.waterMat = water;
            var sand = MeshUtil.FreshMat(0x8a7a5a);
            sand.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            var wl = new List<Vector3>();
            var wr = new List<Vector3>();
            var sl = new List<Vector3>();
            var sl2 = new List<Vector3>();
            var sr = new List<Vector3>();
            var sr2 = new List<Vector3>();
            for (float z = RiverZ0; z <= RiverZ1; z += 8f)
            {
                float cx = RiverX(z);
                float wy = BaseH(z) - 0.72f;
                wl.Add(new Vector3(cx - 5.5f, wy, z));
                wr.Add(new Vector3(cx + 5.5f, wy, z));
                float sy = BaseH(z) - 0.15f;
                sl.Add(new Vector3(cx - 14.5f, sy, z));
                sl2.Add(new Vector3(cx - 5.5f, sy - 0.45f, z));
                sr.Add(new Vector3(cx + 5.5f, sy - 0.45f, z));
                sr2.Add(new Vector3(cx + 14.5f, sy, z));
            }
            Strip(R, "RiverWater", water, wl, wr);
            Strip(R, "RiverSandW", sand, sl, sl2);
            Strip(R, "RiverSandE", sand, sr, sr2);

            // Drifting foam flecks, animated south by the WorldAnimator.
            var foamRand = new Mulberry(2024);
            for (int i = 0; i < 20; i++)
            {
                float z = RiverZ0 + foamRand.Next() * (RiverZ1 - RiverZ0);
                float x = RiverX(z) + (foamRand.Next() - 0.5f) * 7f;
                refs.riverFoam.Add(MeshUtil.Ball(R, "Foam", 0.35f, 0xcfe0ea, x, BaseH(z) - 0.55f, z, true));
            }

            // Current streaks and moonlit glints riding the surface, plus
            // dark stones along the banks: the texture the flat ribbon
            // lacks. Streaks drift south with the flow (see WorldAnimator).
            for (int i = 0; i < 64; i++)
            {
                float z = RiverZ0 + foamRand.Next() * (RiverZ1 - RiverZ0);
                float x = RiverX(z) + (foamRand.Next() - 0.5f) * 7f;
                float len = 2.5f + foamRand.Next() * 2f;
                var s = MeshUtil.Box(R, "Streak", 0.5f, 0.04f, len, 0x7fa8c9, x, BaseH(z) - 0.66f, z);
                s.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0x7fa8c9);
                refs.riverStreaks.Add(s);
            }
            for (int i = 0; i < 16; i++)
            {
                float z = RiverZ0 + foamRand.Next() * (RiverZ1 - RiverZ0);
                float x = RiverX(z) + (foamRand.Next() - 0.5f) * 7f;
                var g = MeshUtil.Box(R, "Glint", 0.6f, 0.04f, 0.6f, 0xcfe4f8, x, BaseH(z) - 0.66f, z);
                g.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xcfe4f8);
                refs.riverStreaks.Add(g);
            }
            for (int i = 0; i < 24; i++)
            {
                float z = RiverZ0 + foamRand.Next() * (RiverZ1 - RiverZ0);
                float side = foamRand.Next() < 0.5f ? -1f : 1f;
                float x = RiverX(z) + side * (8f + foamRand.Next() * 5f);
                float rock = 0.5f + foamRand.Next() * 0.6f;
                MeshUtil.Ball(R, "BankRock", rock, 0x3a3f46, x, GroundH(x, z) + rock * 0.3f, z, false);
            }

            // Bridge at the bend by the village. The channel flattens under
            // it (see GroundH), so feet cross at grade and water runs below.
            float bx = RiverX(BridgeZ);
            var bridge = new GameObject("Bridge");
            bridge.transform.SetParent(R, false);
            bridge.transform.localPosition = new Vector3(bx, 0f, BridgeZ);
            MeshUtil.Box(bridge.transform, "Deck", 28f, 0.3f, 4.5f, 0x6b5136, 0f, -0.1f, 0f);
            MeshUtil.Box(bridge.transform, "Beam", 28f, 0.35f, 0.35f, 0x4a3a28, 0f, 0.35f, -2f);
            MeshUtil.Box(bridge.transform, "Beam", 28f, 0.35f, 0.35f, 0x4a3a28, 0f, 0.35f, 2f);
            for (int i = -2; i <= 2; i++)
            {
                MeshUtil.Box(bridge.transform, "Post", 0.3f, 1.2f, 0.3f, 0x4a3a28, i * 6f, 0.5f, -2f);
                MeshUtil.Box(bridge.transform, "Post", 0.3f, 1.2f, 0.3f, 0x4a3a28, i * 6f, 0.5f, 2f);
            }
            ctx.Dirt(bx - 16f, BridgeZ, 6f);
            ctx.Dirt(bx + 16f, BridgeZ, 6f);

            // Lumber mill on the west bank, wheel dipping in the channel.
            // The wheel must sit over open water (ribbon half-width 5.5
            // around RiverX) AND touch the east wall: at x=88 the wheel
            // stood on sand 9u from the building, reading as two lone
            // objects. Dirt shrinks to a bank-appropriate patch.
            var mill = new GameObject("Mill");
            mill.transform.SetParent(R, false);
            mill.transform.localPosition = new Vector3(104f, 0f, 178f);
            MeshUtil.Box(mill.transform, "Walls", 6f, 4f, 5f, 0x4f4234, 0f, 2f, 0f);
            var millRoofMesh = MeshUtil.TaperMesh(0.02f, 4.6f, 2.4f);
            var mr = MeshUtil.Tapered(mill.transform, "Roof", millRoofMesh, 0x3a2f22, 0f, 5.2f, 0f);
            mr.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            ctx.Solid(104f, 178f, 4.5f, SurfaceKind.Wood);
            ctx.Dirt(104f, 178f, 6f);
            var wheel = new GameObject("MillWheel");
            wheel.transform.SetParent(R, false);
            wheel.transform.localPosition = new Vector3(107.5f, 1.5f, 178f);
            var discMesh = MeshUtil.TaperMesh(3f, 3f, 0.4f);
            var disc = MeshUtil.Tapered(wheel.transform, "Disc", discMesh, 0x5a4630, 0f, 0f, 0f);
            disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * 0.0174533f;
                var pad = MeshUtil.Box(wheel.transform, "Paddle", 0.5f, 1.1f, 0.9f, 0x6b5136, 0f, Mathf.Cos(a) * 3f, Mathf.Sin(a) * 3f);
                pad.transform.localRotation = Quaternion.Euler(-a * 57.29578f, 0f, 0f);
            }
            refs.millWheel = wheel;

            // Riverside camp across the bridge: cold fire ring, log benches,
            // a faint warm light. No gameplay; a reason to cross over.
            var camp = new GameObject("Camp");
            camp.transform.SetParent(R, false);
            camp.transform.localPosition = new Vector3(120f, 0f, 232f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * 0.0174533f;
                MeshUtil.Ball(camp.transform, "FireStone", 0.3f, 0x6a7078, Mathf.Cos(a) * 1.1f, 0.2f, Mathf.Sin(a) * 1.1f, false);
            }
            var benchMesh = MeshUtil.TaperMesh(0.3f, 0.3f, 2.4f);
            var b1 = MeshUtil.Tapered(camp.transform, "Bench", benchMesh, 0x5a4630, 2.6f, 0.3f, 0f);
            b1.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var b2 = MeshUtil.Tapered(camp.transform, "Bench", benchMesh, 0x5a4630, -2.6f, 0.3f, 0f);
            b2.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var campLightGo = new GameObject("CampLight");
            campLightGo.transform.SetParent(camp.transform, false);
            campLightGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var campLight = campLightGo.AddComponent<Light>();
            campLight.type = LightType.Point;
            campLight.color = MeshUtil.C(0xff9a4a);
            campLight.intensity = 1.5f;
            campLight.range = 14f;
            ctx.Dirt(120f, 232f, 8f);
        }

        // 0 pine, 1 broadleaf, 2 birch. Oaks crowd the warm south near home;
        // dark pines take the climb; birches flash pale in between.
        private static int TreeKind(float z, float h)
        {
            if (z > 100f) return h < 0.5f ? 1 : (h < 0.8f ? 0 : 2);
            if (z < -100f) return h < 0.8f ? 0 : 2;
            return h < 0.55f ? 0 : (h < 0.8f ? 1 : 2);
        }

        private static bool InClearing(float[,] clearings, float x, float z, float mul)
        {
            for (int c = 0; c < clearings.GetLength(0); c++)
            {
                float dx = x - clearings[c, 0];
                float dz = z - clearings[c, 1];
                float cr = clearings[c, 2] * mul;
                if (dx * dx + dz * dz < cr * cr) return true;
            }
            return false;
        }

        private static void PlantTree(BuildCtx ctx, Transform forest, float x, float z, float s, Mulberry rng)
        {
            var g = new GameObject("Tree");
            g.transform.SetParent(forest, false);
            g.transform.localPosition = new Vector3(x, GroundH(x, z), z);
            // Variety is hashed from position so the RNG stream is untouched.
            float h1 = Mathf.Abs(Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f) % 1f;
            float h2 = Mathf.Abs(Mathf.Sin(x * 39.346f + z * 11.135f) * 24634.6345f) % 1f;
            g.transform.localScale = new Vector3(s, s * (0.88f + h1 * 0.36f), s);
            g.transform.localRotation = Quaternion.Euler((h1 - 0.5f) * 6f, rng.Next() * 180f, (h2 - 0.5f) * 6f);
            int kind = TreeKind(z, h2);
            if (kind == 1)
            {
                MeshUtil.Tapered(g.transform, "Trunk", ctx.oakTrunkMesh, 0x4a3a28, 0f, 2f, 0f);
                uint leaf = h1 < 0.5f ? 0x2f5a34u : 0x3d6b3fu;
                MeshUtil.Ball(g.transform, "Crown", 2.2f, leaf, 0f, 5f, 0f, false);
                MeshUtil.Ball(g.transform, "Crown", 1.6f, leaf, 0.8f, 6.4f, 0.4f, false);
            }
            else if (kind == 2)
            {
                MeshUtil.Tapered(g.transform, "Trunk", ctx.trunkMesh, 0xcfc8b8, 0f, 1.6f, 0f);
                MeshUtil.Tapered(g.transform, "Canopy", ctx.birchCanopyMesh, 0x4a7a4a, 0f, 4.6f, 0f);
            }
            else
            {
                MeshUtil.Tapered(g.transform, "Trunk", ctx.trunkMesh, 0x4a3a28, 0f, 1.6f, 0f);
                int pick = (((int)(x * 7f + z * 13f)) % 3 + 3) % 3;
                uint canopyCol = pick == 0 ? 0x41704au : (pick == 1 ? 0x2e5233u : 0x38663bu);
                // Tiered pine: three shrinking skirts read as branches
                // where one cone read as a party hat.
                MeshUtil.Tapered(g.transform, "Canopy", ctx.canopyMesh, canopyCol, 0f, 4.1f, 0f);
                var tier2 = MeshUtil.Tapered(g.transform, "Canopy", ctx.canopyMesh, canopyCol, 0.15f, 5.6f, 0.1f);
                tier2.transform.localScale = new Vector3(0.68f, 0.72f, 0.68f);
                var tier3 = MeshUtil.Tapered(g.transform, "Canopy", ctx.canopyMesh, canopyCol, -0.1f, 6.9f, -0.08f);
                tier3.transform.localScale = new Vector3(0.42f, 0.55f, 0.42f);
            }
            ctx.Solid(x, z, 0.5f, SurfaceKind.Wood);
        }

        private static void BuildForest(BuildCtx ctx)
        {
            var R = ctx.R;
            var refs = ctx.world.refs;

            // Forest: seeded positions. The path corridor and the river stay
            // clear; clearings keep the named places open.
            var rand = new Mulberry(1337);
            ctx.rand = rand;
            float[,] clearings = new float[,] {
                {-30,190,45},{35,190,40},{0,210,35},{95,180,25},{104,205,20},{120,232,16},{58,230,11},{-52,228,11},
                {0,-300,60},{72,-340,50},{-70,0,30},{95,-60,25},{-45,-200,25},
                {10,25,22},{0,142,32},
            };
            ctx.clearings = clearings;
            var treePos = new List<Vector3>();
            ctx.treePos = treePos;
            int guard = 0;
            while (treePos.Count < 1000 && guard++ < 30000)
            {
                float x = (rand.Next() * 2f - 1f) * 310f;
                float z = -395f + rand.Next() * 625f;
                if (NearPath(x, z, 25f)) continue;
                if (Mathf.Abs(x - RiverX(z)) < 17f) continue;
                if (InClearing(clearings, x, z, 1f)) continue;
                treePos.Add(new Vector3(x, z, 0.8f + rand.Next() * 0.7f));
            }
            ctx.trunkMesh = MeshUtil.TaperMesh(0.28f, 0.42f, 3.2f);
            ctx.canopyMesh = MeshUtil.TaperMesh(0.02f, 2.1f, 5.2f);
            ctx.oakTrunkMesh = MeshUtil.TaperMesh(0.4f, 0.55f, 4f);
            ctx.birchCanopyMesh = MeshUtil.TaperMesh(0.02f, 1.4f, 4.5f);
            var forest = new GameObject("Forest");
            forest.transform.SetParent(R, false);
            ctx.forest = forest;
            foreach (var tp in treePos) PlantTree(ctx, forest.transform, tp.x, tp.y, tp.z, rand);

            // Three landmark pines.
            var landTrunk = MeshUtil.TaperMesh(0.6f, 0.9f, 6.6f);
            var landCanopy = MeshUtil.TaperMesh(0.02f, 4.5f, 10.5f);
            float[,] lands = new float[,] { {0f, 150f}, {-25f, 137f}, {25f, 135f} };
            for (int i = 0; i < 3; i++)
            {
                float x = lands[i, 0];
                float z = lands[i, 1];
                var g = new GameObject("LandmarkPine");
                g.transform.SetParent(R, false);
                g.transform.localPosition = new Vector3(x, GroundH(x, z), z);
                ctx.world.refs.landmarkPines.Add(g);
                MeshUtil.Tapered(g.transform, "Trunk", landTrunk, 0x4a3a28, 0f, 3.3f, 0f);
                MeshUtil.Tapered(g.transform, "Canopy", landCanopy, 0x35603a, 0f, 10.5f, 0f);
                ctx.Solid(x, z, 0.8f, SurfaceKind.Wood);
            }

            // Split rock.
            var rock1 = MeshUtil.Box(R, "Rock", 7.8f, 6.6f, 6.6f, 0x5a6068, 10f, GroundH(10f, 25f) + 3.3f, 25f);
            rock1.transform.localRotation = Quaternion.Euler(0f, 30f, 8f);
            var rock2 = MeshUtil.Box(R, "Rock", 4.8f, 3.9f, 4.2f, 0x4a5058, 14.5f, GroundH(14.5f, 28f) + 1.9f, 28f);
            rock2.transform.localRotation = Quaternion.Euler(0f, -20f, -6f);
            ctx.Solid(10f, 25f, 6f, SurfaceKind.Stone);

            // Waystone: a rune-cut standing stone where the middle leg
            // bends, so each leg of the climb gets its own landmark.
            var waystone = new GameObject("Waystone");
            waystone.transform.SetParent(R, false);
            waystone.transform.localPosition = new Vector3(-10f, GroundH(-10f, -80f), -80f);
            var wslab = MeshUtil.Box(waystone.transform, "Slab", 2.5f, 7f, 1.5f, 0x5a6068, 0f, 3.5f, 0f);
            wslab.transform.localRotation = Quaternion.Euler(0f, 12f, 3f);
            var wsrune = MeshUtil.Box(waystone.transform, "Rune", 0.5f, 0.9f, 0.1f, 0xd9a441, 0f, 3f, 0.78f);
            wsrune.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xd9a441);
            refs.waystonePos = new Vec2(-10f, -80f);
            ctx.Solid(-10f, -80f, 2.5f, SurfaceKind.Stone);
            ctx.Dirt(-10f, -80f, 6f);

            // Axe blazes on trunks along the path.
            float[,] blazeSpots = new float[,] { {5f,75f},{6f,110f},{26.7f,40f},{-40f,-30f},{17.5f,-110f},{-3.5f,-180f} };
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
                if (!found || bestD > 225f)
                {
                    // No trunk close by: plant a pine for the plaque, on
                    // its own RNG stream so nothing downstream shifts.
                    float px = x + 4f;
                    PlantTree(ctx, forest.transform, px, z, 1.2f, new Mulberry((uint)(9000 + i)));
                    treePos.Add(new Vector3(px, z, 1.2f));
                    best = new Vector3(px, z, 1.2f);
                }
                refs.blazePos.Add(new Vec2(best.x, best.y));
                // Seated on the bark: the trunk radius scales with the tree,
                // so a fixed offset buries plaques in fat trunks.
                var blaze = MeshUtil.Box(R, "Blaze", 0.3f, 0.5f, 0.06f, 0xd9a441, best.x, GroundH(best.x, best.y) + 1.5f, best.y + 0.42f * best.z + 0.05f);
                blaze.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xd9a441);
            }

            // Guide mark: a previous climber's east-turn, cut on the wall
            // face the path walks into. The grove hides around the corner.
            var guide = MeshUtil.Box(R, "GuideMark", 0.5f, 0.8f, 0.1f, 0xd9a441, 13f, GroundH(13f, -276.4f) + 2.2f, -276.4f);
            guide.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xd9a441);
            refs.guidePos = new Vec2(13f, -276.4f);
        }

        private static void BuildCave(BuildCtx ctx)
        {
            var R = ctx.R;
            var refs = ctx.world.refs;
            var rand = ctx.rand;
            float gy = PlateauH;

            // Cave shell: a roomy hall, still boxes until the art pass.
            // The roof and back wall overhang the side walls: flush faces
            // z-fight along the whole top edge (a phantom "wall" shimmer
            // visible only from outside).
            uint stone = 0x565c66;
            MeshUtil.Box(R, "CaveWall", 8f, 16f, 50f, stone, -13f, gy + 8f, -302f);
            MeshUtil.Box(R, "CaveWall", 8f, 16f, 50f, stone, 13f, gy + 8f, -302f);
            MeshUtil.Box(R, "CaveWall", 36f, 16f, 8f, stone, 0f, gy + 8f, -329f);
            MeshUtil.Box(R, "CaveRoof", 38f, 4f, 60f, stone, 0f, gy + 15f, -305f);
            // Stone arch around the mouth.
            MeshUtil.Box(R, "ArchJamb", 2f, 13f, 3f, stone, -9.5f, gy + 6.5f, -278f);
            MeshUtil.Box(R, "ArchJamb", 2f, 13f, 3f, stone, 9.5f, gy + 6.5f, -278f);
            MeshUtil.Box(R, "ArchLintel", 23f, 3f, 4f, stone, 0f, gy + 13.5f, -278f);
            ctx.Solid(-13f, -285f, 7f, SurfaceKind.Stone);
            ctx.Solid(-13f, -302f, 7f, SurfaceKind.Stone);
            ctx.Solid(-13f, -319f, 7f, SurfaceKind.Stone);
            ctx.Solid(13f, -285f, 7f, SurfaceKind.Stone);
            ctx.Solid(13f, -302f, 7f, SurfaceKind.Stone);
            ctx.Solid(13f, -319f, 7f, SurfaceKind.Stone);
            // Seal the lens gaps between the big discs: 17u spacing with
            // r7 discs left a walkable 3u slot mid-wall on both sides.
            ctx.Solid(-13f, -293.5f, 5f, SurfaceKind.Stone);
            ctx.Solid(-13f, -310.5f, 5f, SurfaceKind.Stone);
            ctx.Solid(13f, -293.5f, 5f, SurfaceKind.Stone);
            ctx.Solid(13f, -310.5f, 5f, SurfaceKind.Stone);
            ctx.Solid(-10f, -329f, 5f, SurfaceKind.Stone);
            ctx.Solid(-5f, -329f, 4.5f, SurfaceKind.Stone);
            ctx.Solid(0f, -329f, 5f, SurfaceKind.Stone);
            ctx.Solid(5f, -329f, 4.5f, SurfaceKind.Stone);
            ctx.Solid(10f, -329f, 5f, SurfaceKind.Stone);
            // Front corners of the side walls, tucked just inside the
            // face plane (z=-277): centered ON the face they jutted 4u
            // into open ground, an invisible wall 3.85u short of the
            // guide mark. r=4.2 still caps both front corners (corner
            // reach 4.47 < 4.65 with the 0.45 player radius) while the
            // mark clears to 2.05u; the mouth stays open.
            ctx.Solid(-13f, -279f, 4.2f, SurfaceKind.Stone);
            ctx.Solid(13f, -279f, 4.2f, SurfaceKind.Stone);
            // No glow quad here: Unlit/Transparent renders opaque white and
            // walled the mouth shut for anyone inside looking out. The
            // MouthLight below carries the ember look on its own.
            var mouth = new GameObject("MouthLight");
            mouth.transform.SetParent(R, false);
            mouth.transform.localPosition = new Vector3(0f, gy + 7f, -279f);
            var mouthLight = mouth.AddComponent<Light>();
            mouthLight.type = LightType.Point;
            mouthLight.color = MeshUtil.C(0xc1502e);
            mouthLight.intensity = 3f;
            mouthLight.range = 50f;
            refs.mouthLight = mouthLight;

            // The mountain the cave is cut into. Flanks carry colliders; the
            // high peak hangs over the roof (visual only) and the roof tucks
            // under its skirt. The peak stands well back: cones are hollow
            // shells, invisible from inside, so a skirt crossing the hall
            // reads as a solid wall from the mouth (walkable straight
            // through). Rock starts behind the forge, ~44u deep.
            ctx.RockCone(-58f, -296f, 42f, 95f, gy - 2f, false);
            ctx.Solid(-58f, -296f, 34f, SurfaceKind.Stone);
            // East flank stays narrow: the grove footpath threads past it,
            // and the visual cone (not just the collider) must clear the
            // corridor, or walkers clip through the mountainside.
            ctx.RockCone(66f, -284f, 30f, 85f, gy - 2f, false);
            ctx.Solid(66f, -284f, 23f, SurfaceKind.Stone);
            ctx.RockCone(0f, -375f, 55f, 130f, gy - 2f, true);
            ctx.Solid(0f, -375f, 38f, SurfaceKind.Stone);
            refs.cavePeak = new Vec2(0f, -375f);
            refs.cavePeakR = 55f;
            refs.cavePeakH = 130f;
            ctx.RockCone(-35f, -368f, 55f, 120f, gy - 2f, true);
            ctx.Solid(-35f, -368f, 42f, SurfaceKind.Stone);
            // North-right cone stays compact and back: the grove and its
            // light live in the dell south-east of it, and a fat cone here
            // swallows both the grove and the wayfinding glow.
            ctx.RockCone(20f, -385f, 40f, 120f, gy - 2f, true);
            ctx.Solid(20f, -385f, 30f, SurfaceKind.Stone);
            ctx.RockCone(-75f, -345f, 45f, 100f, gy - 2f, true);
            ctx.Solid(-75f, -345f, 34f, SurfaceKind.Stone);
            // Far-east cone: the river threads the gap between this and the
            // north-right cone on its way down from the heights.
            ctx.RockCone(150f, -380f, 50f, 110f, gy - 2f, true);
            ctx.Solid(150f, -380f, 38f, SurfaceKind.Stone);

            // Forge, anvil.
            var forge = new GameObject("Forge");
            forge.transform.SetParent(R, false);
            forge.transform.localPosition = new Vector3(5f, gy, -315f);
            MeshUtil.Box(forge.transform, "Body", 1.8f, 1.1f, 1.2f, 0x3a3f46, 0f, 0.55f, 0f);
            var coals = MeshUtil.Box(forge.transform, "Coals", 1.4f, 0.25f, 0.9f, 0xff7a2a, 0f, 1.15f, 0f);
            coals.GetComponent<Renderer>().sharedMaterial = MeshUtil.Basic(0xff7a2a);
            ctx.Solid(5f, -315f, 1.4f, SurfaceKind.Metal);
            var forgeLightGo = new GameObject("ForgeLight");
            forgeLightGo.transform.SetParent(R, false);
            forgeLightGo.transform.localPosition = new Vector3(5f, gy + 4f, -315f);
            var forgeLight = forgeLightGo.AddComponent<Light>();
            forgeLight.type = LightType.Point;
            forgeLight.color = MeshUtil.C(0xff8a3a);
            forgeLight.intensity = 3f;
            forgeLight.range = 16f;
            refs.forgeLight = forgeLight;
            var anvil = new GameObject("Anvil");
            anvil.transform.SetParent(R, false);
            anvil.transform.localPosition = new Vector3(1f, gy, -318f);
            MeshUtil.Box(anvil.transform, "Base", 0.5f, 0.5f, 0.5f, 0x2c2f34, 0f, 0.25f, 0f);
            MeshUtil.Box(anvil.transform, "Top", 1.1f, 0.28f, 0.4f, 0x3a3f46, 0f, 0.62f, 0f);
            ctx.Solid(1f, -318f, 0.85f, SurfaceKind.Metal);

            // Chimney over the forge: the smoke gets a visible source and
            // rises clear of the rock instead of up the peak's silhouette.
            MeshUtil.Box(R, "Chimney", 1.2f, 3f, 1.2f, stone, 5f, gy + 17.5f, -315f);
            // Smoke column above the chimney (positions consume the same
            // RNG stream the old slice used, longer now for the taller hill).
            var smokePos = new List<Vector3>();
            for (int i = 0; i < 42; i++)
            {
                float sx = 5f + (rand.Next() - 0.5f) * 3f;
                float sy = 18f + rand.Next() * 30f;
                float sz = -315f + (rand.Next() - 0.5f) * 3f;
                smokePos.Add(new Vector3(sx, gy + sy, sz));
            }
            // Matte gray puffs rising from a tight column: the transparent
            // cubes rendered as opaque white blocks, so the smoke is solid
            // stylized puffs instead. Light enough to read against the
            // dark peak face, not just the sky.
            for (int i = 0; i < 14; i++)
            {
                float pr = 1.2f + (smokePos[i].x + 1.5f) * 0.25f;
                var puff = MeshUtil.Ball(R, "Smoke", pr, 0x6a7078,
                    smokePos[i].x, smokePos[i].y, smokePos[i].z, false);
                refs.smokePuffs.Add(puff);
                refs.smokeBase.Add(smokePos[i]);
            }
        }

        private static void BuildGrove(BuildCtx ctx)
        {
            var R = ctx.R;
            var refs = ctx.world.refs;

            // Ironwood grove past the cave. Pale trunks carry a faint warm
            // shimmer and dark crowns so the grove reads at night.
            var ironMesh = MeshUtil.TaperMesh(0.26f, 0.36f, 5.5f);
            var ironBark = MeshUtil.FreshMat(0xcfc4a8);
            ironBark.EnableKeyword("_EMISSION_ON");
            ironBark.SetColor("_EmissionColor", new Color(0.22f, 0.18f, 0.12f));
            var ironCrownMesh = MeshUtil.TaperMesh(0.02f, 1.5f, 3.2f);
            float[,] grove = new float[,] { {62f,-330f},{82f,-330f},{57f,-345f},{87f,-345f},{72f,-352f},{52f,-340f} };
            for (int i = 0; i < grove.GetLength(0); i++)
            {
                float x = grove[i, 0];
                float z = grove[i, 1];
                var iw = MeshUtil.Tapered(R, "Ironwood", ironMesh, 0xcfc4a8, x, GroundH(x, z) + 2.75f, z);
                iw.GetComponent<Renderer>().sharedMaterial = ironBark;
                MeshUtil.Tapered(R, "IronCrown", ironCrownMesh, 0x1f3a2a, x, GroundH(x, z) + 6.4f, z);
                ctx.Solid(x, z, 0.45f, SurfaceKind.Wood);
            }
            var ironLogMesh = MeshUtil.TaperMesh(0.32f, 0.32f, 2.6f);
            var ironLog = MeshUtil.Tapered(R, "IronLog", ironLogMesh, 0xcfc4a8, 72f, GroundH(72f, -340f) + 0.33f, -340f);
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
            groveLightGo.transform.localPosition = new Vector3(72f, GroundH(72f, -340f) + 8f, -340f);
            var groveLight = groveLightGo.AddComponent<Light>();
            groveLight.type = LightType.Point;
            groveLight.color = MeshUtil.C(0xbfd0e8);
            groveLight.intensity = 2.4f;
            groveLight.range = 70f;

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
        }

        private static void BuildBackdrop(BuildCtx ctx)
        {
            var R = ctx.R;
            var rand = ctx.rand;

            // Wolf den dressing: pale bones.
            float[,] bones = new float[,] { {-72f,2f},{-67f,-3f},{-75f,-4f} };
            for (int i = 0; i < 3; i++)
            {
                var b = MeshUtil.Box(R, "Bone", 0.7f, 0.12f, 0.12f, 0xcfc4a8, bones[i, 0], GroundH(bones[i, 0], bones[i, 1]) + 0.08f, bones[i, 1]);
                b.transform.localRotation = Quaternion.Euler(0f, rand.Next() * 180f, 0f);
            }

            // The far ranges: tiered stylized peaks looming beyond the north
            // bound. Bases sit outside playable space so no slope is ever
            // walk-through. Fixed offsets, no RNG.
            ctx.Peak(0f, -520f, 120f, 200f);
            ctx.Peak(-270f, -480f, 75f, 130f);
            ctx.Peak(270f, -480f, 75f, 130f);
            // Talus dots the open skirts only: anything inside a cone
            // footprint is buried and invisible.
            float[,] talus = new float[,] { {-25f,-272f,7f},{30f,-266f,9f},{-45f,-250f,11f},{100f,-330f,13f},{-60f,-250f,10f} };
            for (int i = 0; i < talus.GetLength(0); i++)
            {
                var t = MeshUtil.Box(R, "Talus", talus[i, 2], talus[i, 2] * 0.8f, talus[i, 2], 0x5a6068, talus[i, 0], GroundH(talus[i, 0], talus[i, 1]) + talus[i, 2] * 0.35f, talus[i, 1]);
                t.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
                ctx.Solid(talus[i, 0], talus[i, 1], talus[i, 2] * 0.7f, SurfaceKind.Stone);
            }
            // (No footprint colliders: the peaks stand outside the bounds,
            // and the cave walls below carry their own.)
        }

        private static void BuildInfill(BuildCtx ctx)
        {
            var R = ctx.R;
            var refs = ctx.world.refs;
            var clearings = ctx.clearings;
            var forest = ctx.forest;
            var treePos = ctx.treePos;

            // Second forest band on its own RNG stream, so the original
            // layout above is untouched bit-for-bit.
            var rand2 = new Mulberry(777);
            int extra = 0;
            int guard2 = 0;
            while (extra < 450 && guard2++ < 15000)
            {
                float x = (rand2.Next() * 2f - 1f) * 310f;
                float z = -395f + rand2.Next() * 625f;
                if (NearPath(x, z, 25f)) continue;
                if (Mathf.Abs(x - RiverX(z)) < 17f) continue;
                if (InClearing(clearings, x, z, 1f)) continue;
                float s = 0.8f + rand2.Next() * 0.7f;
                PlantTree(ctx, forest.transform, x, z, s, rand2);
                treePos.Add(new Vector3(x, z, s));
                extra++;
            }
            refs.treeCount = treePos.Count;

            // Undergrowth bushes (no colliders; walk-through). Every sixth
            // one carries berries.
            var bushes = new GameObject("Bushes");
            bushes.transform.SetParent(R, false);
            int nb = 0;
            int guard3 = 0;
            while (nb < 350 && guard3++ < 12000)
            {
                float x = (rand2.Next() * 2f - 1f) * 300f;
                float z = -390f + rand2.Next() * 630f;
                if (NearPath(x, z, 20f)) continue;
                if (Mathf.Abs(x - RiverX(z)) < 15f) continue;
                if (InClearing(clearings, x, z, 0.7f)) continue;
                float br = 0.7f + rand2.Next() * 0.7f;
                var b = MeshUtil.Ball(bushes.transform, "Bush", br, 0x24402a, x, GroundH(x, z) + br * 0.5f, z, false);
                b.transform.localScale = new Vector3(br * 2f, br * 1.1f, br * 2f);
                if (nb % 6 == 0) refs.bushPos.Add(new Vec2(x, z));
                if (nb % 6 == 0)
                {
                    // Berries parent to the unscaled group, not the bush:
                    // as children they inherit its scale and land units
                    // outside the foliage. No new RNG draws here, so the
                    // shared stream stays bit-identical downstream.
                    for (int i = 0; i < 3; i++)
                    {
                        float a = rand2.Next() * 6.283f;
                        float brow = br * 0.95f;
                        MeshUtil.Ball(bushes.transform, "Berry", 0.12f, 0xa83a3a,
                            x + Mathf.Cos(a) * brow, GroundH(x, z) + br * 0.6f + i * 0.12f,
                            z + Mathf.Sin(a) * brow, true);
                    }
                }
                nb++;
            }
            refs.bushCount = nb;

            // Approach dressing: fixed rocks, pines, and dirt so the walk
            // to the cave is not an empty plain. All clear of the path.
            float[,] aRocks = new float[,] { {40f,-120f,4f},{-45f,-150f,5.5f},{50f,-190f,3.5f},{-40f,-220f,4.5f},{45f,-250f,5f},{-30f,-252f,3.8f} };
            for (int i = 0; i < aRocks.GetLength(0); i++)
            {
                float sz = aRocks[i, 2];
                var t = MeshUtil.Box(R, "ApproachRock", sz, sz * 0.75f, sz, 0x5a6068, aRocks[i, 0], GroundH(aRocks[i, 0], aRocks[i, 1]) + sz * 0.3f, aRocks[i, 1]);
                t.transform.localRotation = Quaternion.Euler(0f, i * 53f, 0f);
                ctx.Solid(aRocks[i, 0], aRocks[i, 1], sz * 0.6f, SurfaceKind.Stone);
            }
            float[,] aPines = new float[,] { {40f,-140f},{-45f,-175f},{55f,-215f},{-50f,-240f},{40f,-265f},{-40f,-130f} };
            for (int i = 0; i < aPines.GetLength(0); i++)
            {
                float x = aPines[i, 0];
                float z = aPines[i, 1];
                var g = new GameObject("Tree");
                g.transform.SetParent(forest.transform, false);
                g.transform.localPosition = new Vector3(x, GroundH(x, z), z);
                MeshUtil.Tapered(g.transform, "Trunk", ctx.trunkMesh, 0x4a3a28, 0f, 1.6f, 0f);
                MeshUtil.Tapered(g.transform, "Canopy", ctx.canopyMesh, 0x2e5233, 0f, 5.4f, 0f);
                ctx.Solid(x, z, 0.5f, SurfaceKind.Wood);
            }
            ctx.Dirt(40f, -140f, 12f, 0.22f);
            ctx.Dirt(-45f, -175f, 12f, 0.22f);
            ctx.Dirt(0f, -240f, 16f, 0.22f);
            ctx.Dirt(25f, -260f, 12f, 0.22f);
            ctx.Dirt(-30f, -100f, 12f, 0.22f);
            ctx.Dirt(50f, -220f, 12f, 0.22f);
        }

        private static void PlantFlower(Transform parent, Mesh stemMesh, float x, float z, uint col)
        {
            float gy = GroundH(x, z);
            MeshUtil.Tapered(parent, "Stem", stemMesh, 0x2f5a34, x, gy + 0.15f, z);
            MeshUtil.Ball(parent, "Flower", 0.11f, col, x, gy + 0.32f, z, true);
        }

        private static void BuildFlowers(BuildCtx ctx)
        {
            var R = ctx.R;
            var refs = ctx.world.refs;
            var clearings = ctx.clearings;

            // Flower drifts: white, gold, red, and blue trailing the path
            // edges. Every bloom gets a stem so none hover. Walk-through;
            // no colliders.
            var rand3 = new Mulberry(4242);
            var drift = new GameObject("Flowers");
            drift.transform.SetParent(R, false);
            var stemMesh = MeshUtil.TaperMesh(0.025f, 0.035f, 0.3f);
            uint[] cols = new uint[] { 0xe8e4d8, 0xd9b441, 0xa83a3a, 0x6a8ac9 };
            int nf = 0;
            int guard = 0;
            while (nf < 420 && guard++ < 12000)
            {
                float cx = (rand3.Next() * 2f - 1f) * 290f;
                float cz = -380f + rand3.Next() * 620f;
                if (NearPath(cx, cz, 8f)) continue;
                if (Mathf.Abs(cx - RiverX(cz)) < 14f) continue;
                if (InClearing(clearings, cx, cz, 0.5f)) continue;
                for (int i = 0; i < 6 && nf < 420; i++)
                {
                    float x = cx + (rand3.Next() - 0.5f) * 8f;
                    float z = cz + (rand3.Next() - 0.5f) * 8f;
                    float pick = rand3.Next();
                    uint col = pick < 0.3f ? cols[0] : (pick < 0.6f ? cols[1] : (pick < 0.78f ? cols[2] : cols[3]));
                    PlantFlower(drift.transform, stemMesh, x, z, col);
                    nf++;
                }
            }
            // Meadow ring by the village: fixed dressing (and a fixed
            // viewpoint target) so the flowers read up close.
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * 0.0174533f;
                PlantFlower(drift.transform, stemMesh, 20f + Mathf.Cos(a) * 3f, 225f + Mathf.Sin(a) * 3f, cols[i % 4]);
                nf++;
            }
            refs.flowerCount = nf;
        }

        // Merge pass: folds every static mesh under R into one mesh per
        // material, so the forest draws in dozens of calls instead of
        // thousands. Same materials and world transforms, so the look
        // is identical; one MeshCollider per merged mesh keeps the
        // strike raycast honest. Animated (smoke, foam, wheel, clouds),
        // toggled (axe, book, log, cairn), shaken (landmarks) and
        // assert-pinned (blazes, water, chimney, bushes, peaks)
        // subtrees stay live.
        private static readonly string[] MergeSkipNames = new string[]
        {
            "Blaze", "RiverWater", "Chimney", "Bush", "Berry", "Peak",
        };

        private static bool MergeSkipped(string name)
        {
            foreach (var n in MergeSkipNames) if (name == n) return true;
            return false;
        }

        private static void MergeStatics(BuildCtx ctx)
        {
            var refs = ctx.world.refs;
            var skip = new HashSet<Transform>();
            AddSkips(skip, refs.smokePuffs);
            AddSkips(skip, refs.riverFoam);
            AddSkips(skip, refs.riverStreaks);
            AddSkips(skip, refs.clouds);
            AddSkips(skip, refs.landmarkPines);
            AddSkip(skip, refs.millWheel);
            AddSkip(skip, refs.axeMesh);
            AddSkip(skip, refs.bookMesh);
            AddSkip(skip, refs.ironLog);
            AddSkip(skip, refs.cairn);
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var victims = new List<GameObject>();
            CollectMergeables(ctx.R, skip, groups, victims, true);
            var mergedRoot = new GameObject("MergedStatics");
            mergedRoot.transform.SetParent(ctx.R, false);
            int made = 0;
            foreach (var kv in groups)
            {
                var mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("Merged");
                go.transform.SetParent(mergedRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                made++;
            }
            foreach (var v in victims) Object.Destroy(v);
            Debug.Log("AXEPATH: merged " + victims.Count + " statics into " + made + " meshes");
        }

        private static void AddSkip(HashSet<Transform> skip, GameObject go)
        {
            if (go != null) skip.Add(go.transform);
        }

        private static void AddSkips(HashSet<Transform> skip, List<GameObject> gos)
        {
            if (gos == null) return;
            foreach (var go in gos) AddSkip(skip, go);
        }

        private static void CollectMergeables(Transform t, HashSet<Transform> skip, Dictionary<Material, List<CombineInstance>> groups, List<GameObject> victims, bool isRoot)
        {
            if (!isRoot && (skip.Contains(t) || MergeSkipped(t.name))) return;
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf != null && mr != null && mf.sharedMesh != null && mr.sharedMaterial != null)
            {
                List<CombineInstance> list;
                if (!groups.TryGetValue(mr.sharedMaterial, out list))
                {
                    list = new List<CombineInstance>();
                    groups[mr.sharedMaterial] = list;
                }
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = t.localToWorldMatrix });
                victims.Add(t.gameObject);
            }
            for (int i = 0; i < t.childCount; i++) CollectMergeables(t.GetChild(i), skip, groups, victims, false);
        }
    }

    public class WorldAnimator : MonoBehaviour
    {
        public Light forgeLight;
        public List<GameObject> smokePuffs;
        public List<Vector3> smokeBase;
        public List<GameObject> riverFoam;
        public List<GameObject> riverStreaks;
        public GameObject millWheel;
        public Material waterMat;
        public List<GameObject> clouds;
        public ParticleSystem leafPS;
        public List<Material> fireflyMats;
        // Smoke loop: each puff rises SmokeRise units, bending downwind
        // (+x, with the clouds) by SmokeDrift per unit risen. Shared with
        // the structure test, which locks the path clear of the peak.
        public const float SmokeRise = 70f;
        public const float SmokeDrift = 0.7f;
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
                    pos.y += Time.deltaTime * 2f;
                    float lo = i < smokeBase.Count ? smokeBase[i].y : pos.y;
                    if (pos.y > lo + SmokeRise) pos.y = lo;
                    // Chimney smoke bends downwind (+x, with the clouds)
                    // as it rises, instead of climbing the peak's face.
                    float bx = i < smokeBase.Count ? smokeBase[i].x : pos.x;
                    pos.x = bx + (pos.y - lo) * SmokeDrift + Mathf.Sin(t * 0.9f + i * 1.3f) * 0.8f;
                    p.transform.localPosition = pos;
                }
            }
            if (riverFoam != null)
            {
                foreach (var f in riverFoam)
                {
                    if (f == null) continue;
                    var pos = f.transform.localPosition;
                    pos.z += Time.deltaTime * 3f;
                    if (pos.z > WorldBuilder.RiverZ1) pos.z = WorldBuilder.RiverZ0;
                    pos.x = WorldBuilder.RiverX(pos.z) + Mathf.Sin(t * 0.7f + pos.z * 0.05f) * 2.5f;
                    pos.y = WorldBuilder.BaseH(pos.z) - 0.55f;
                    f.transform.localPosition = pos;
                }
            }
            if (riverStreaks != null)
            {
                for (int i = 0; i < riverStreaks.Count; i++)
                {
                    var s = riverStreaks[i];
                    if (s == null) continue;
                    var pos = s.transform.localPosition;
                    pos.z += Time.deltaTime * 4.5f;
                    if (pos.z > WorldBuilder.RiverZ1) pos.z = WorldBuilder.RiverZ0;
                    pos.x = WorldBuilder.RiverX(pos.z) + Mathf.Sin(t * 0.9f + i * 1.7f) * 3f;
                    pos.y = WorldBuilder.BaseH(pos.z) - 0.66f;
                    s.transform.localPosition = pos;
                }
            }
            if (millWheel != null)
            {
                // Undershot wheel: the current runs +z, so the dipped
                // bottom must travel +z — negative about X. (Positive
                // drove the bottom -z, against the current.)
                millWheel.transform.localRotation *= Quaternion.Euler(Time.deltaTime * -30f, 0f, 0f);
            }
            if (waterMat != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 1.3f);
                waterMat.SetColor("_EmissionColor", new Color(0.03f, 0.07f, 0.11f) * (0.7f + 0.6f * k));
            }
            // One global gust steers every weather system: 0 calm, 1 gale.
            float gust = 0.55f + 0.45f * (0.6f * Mathf.Sin(t * 0.23f) + 0.4f * Mathf.Sin(t * 0.71f + 1.3f));
            if (clouds != null)
            {
                foreach (var c in clouds)
                {
                    if (c == null) continue;
                    var pos = c.transform.localPosition;
                    pos.x += Time.deltaTime * (1.5f + 2f * gust);
                    if (pos.x > 450f) pos.x = -450f;
                    c.transform.localPosition = pos;
                }
            }
            if (leafPS != null)
            {
                var vel = leafPS.velocityOverLifetime;
                vel.x = 3f + 7f * gust;
                var noise = leafPS.noise;
                noise.strength = new ParticleSystem.MinMaxCurve(0.5f, 0.5f + 1.5f * gust);
            }
            if (fireflyMats != null)
            {
                for (int i = 0; i < fireflyMats.Count; i++)
                {
                    var m = fireflyMats[i];
                    if (m == null) continue;
                    float blink = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 2.2f + i * 3.14159f));
                    m.color = MeshUtil.C(0xd9e86a) * blink;
                }
            }
        }
    }
}
