// Ground truth: thousands of small things so no sightline ends on bare
// flat ground. All static detail merges into a few draw calls; counts
// live on WorldRefs for the structure tests.
using UnityEngine;

namespace AxePath
{
    public static class DetailBuilder
    {
        public static void Build(GameWorld world, Transform R, float[,] clearings)
        {
            var rng = new Mulberry(5150);
            var detail = new GameObject("Detail");
            detail.transform.SetParent(R, false);
            var grassMesh = MeshUtil.TaperMesh(0.02f, 0.16f, 0.55f);
            var fernMesh = MeshUtil.TaperMesh(0.02f, 0.5f, 0.4f);
            int grass = 0, rocks = 0, shrooms = 0;
            int guard = 0;
            while (grass < 2600 && guard++ < 30000)
            {
                float x = (rng.Next() * 2f - 1f) * 300f;
                float z = -390f + rng.Next() * 620f;
                if (Mathf.Abs(x - WorldBuilder.RiverX(z)) < 13f) continue;
                // Grass grows on soil, never on stone or trodden ground:
                // keep it off the path, the cave rock apron, the grove
                // clearing, and every village dirt patch.
                if (z > -275f && z < 215f && Mathf.Abs(x - WorldBuilder.PathX(z)) < 9f) continue;
                if (Within(x, z, 0f, -310f, 44f)) continue;
                if (Within(x, z, 72f, -340f, 24f)) continue;
                if (Within(x, z, -30f, 190f, 29f) || Within(x, z, 35f, 190f, 25f) || Within(x, z, 0f, 210f, 21f)) continue;
                if (Within(x, z, 104f, 178f, 8f) || Within(x, z, 120f, 232f, 9f)) continue;
                if (Within(x, z, 58f, 230f, 10f) || Within(x, z, -52f, 228f, 10f)) continue;
                float gy = WorldBuilder.GroundH(x, z);
                uint col = rng.Next() < 0.5f ? 0x2f5a34u : 0x3d6b3fu;
                var g = MeshUtil.Tapered(detail.transform, "GrassTuft", grassMesh, col, x, gy + 0.24f, z);
                float s = 0.8f + rng.Next() * 0.9f;
                g.transform.localScale = new Vector3(s, s, s);
                g.transform.localRotation = Quaternion.Euler((rng.Next() - 0.5f) * 14f, rng.Next() * 180f, (rng.Next() - 0.5f) * 14f);
                grass++;
                if (grass % 9 == 0)
                    MeshUtil.Tapered(detail.transform, "Fern", fernMesh, 0x24402a, x + 0.4f, gy + 0.18f, z + 0.3f);
                if (grass % 55 == 0 && rocks < 130)
                {
                    float rs = 0.3f + rng.Next() * 0.9f;
                    MeshUtil.Crag(detail.transform, "ScatterRock", rs, 0x5a6068, x, gy + rs * 0.25f, z, rng.Next() * 6f);
                    rocks++;
                }
                if (grass % 90 == 0 && shrooms < 60)
                {
                    MeshUtil.Tapered(detail.transform, "ShroomStem", grassMesh, 0xcfc4a8, x, gy + 0.12f, z);
                    MeshUtil.Ball(detail.transform, "ShroomCap", 0.14f, 0xa83a3a, x, gy + 0.3f, z, false);
                    shrooms++;
                }
            }
            // Stumps + fallen logs near the yard and along the climb.
            float[,] stumps = new float[,] { {-38f,168f},{-20f,205f},{18f,120f},{-14f,60f},{22f,-40f},{-20f,-140f},{14f,-210f} };
            var logMesh = MeshUtil.TaperMesh(0.32f, 0.32f, 3.4f);
            for (int i = 0; i < stumps.GetLength(0); i++)
            {
                float x = stumps[i, 0], z = stumps[i, 1];
                float gy = WorldBuilder.GroundH(x, z);
                MeshUtil.Tapered(detail.transform, "Stump", MeshUtil.TaperMesh(0.4f, 0.48f, 0.6f), 0x5a4630, x, gy + 0.3f, z);
                if (i % 2 == 0)
                {
                    var log = MeshUtil.Tapered(detail.transform, "FallenLog", logMesh, 0x4a3a28, x + 2.4f, gy + 0.32f, z + 0.5f);
                    log.transform.localRotation = Quaternion.Euler(0f, i * 31f, 90f);
                }
            }
            // Village fences, crates, barrels: worked wood with a purpose.
            for (int i = 0; i < 7; i++)
            {
                float x = -52f + i * 3.2f, z = 168f;
                MeshUtil.Box(detail.transform, "FenceRail", 0.12f, 0.9f, 0.12f, 0x4a3a28, x, WorldBuilder.GroundH(x, z) + 0.45f, z);
            }
            MeshUtil.Box(detail.transform, "FenceBar", 20f, 0.1f, 0.08f, 0x4a3a28, -42f, WorldBuilder.GroundH(-42f, 168f) + 0.7f, 168f);
            float[,] crates = new float[,] { {-24f,186f},{104f,172f},{38f,196f} };
            for (int i = 0; i < crates.GetLength(0); i++)
            {
                float x = crates[i, 0], z = crates[i, 1];
                float gy = WorldBuilder.GroundH(x, z);
                MeshUtil.Box(detail.transform, "Crate", 0.9f, 0.9f, 0.9f, 0x6b5136, x, gy + 0.45f, z);
                MeshUtil.Tapered(detail.transform, "Barrel", MeshUtil.TaperMesh(0.42f, 0.42f, 1f), 0x5a4630, x + 1.3f, gy + 0.5f, z);
            }
            world.refs.treeCount += 0; // detail never fakes the forest count
            Debug.Log("AXEPATH detail: grass=" + grass + " rocks=" + rocks + " shrooms=" + shrooms);
        }

        private static bool Within(float x, float z, float cx, float cz, float r)
        {
            float dx = x - cx, dz = z - cz;
            return dx * dx + dz * dz < r * r;
        }
    }
}
