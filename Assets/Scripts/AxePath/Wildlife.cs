// Ambient ecology: deer graze and flee, rabbits dart, birds circle the
// thermals. No loot, no markers — life that makes the forest feel
// watched over, and teaches the noise rules before the wolves do.
using System.Collections.Generic;
using UnityEngine;

namespace AxePath
{
    public class Wildlife : MonoBehaviour
    {
        private class Deer
        {
            public GameObject root; public GameObject head;
            public Vector3 home; public float phase; public float fleeT;
        }
        private class Bird { public GameObject root; public float r; public float h; public float ph; public float sp; }

        private class Rabbit { public GameObject root; public Vector3 home; public Vector3 pos; public float fleeT; public float hopPh; }
        private readonly List<Deer> deer = new List<Deer>();
        private readonly List<Rabbit> rabbits = new List<Rabbit>();
        private readonly List<Bird> birds = new List<Bird>();
        private Transform playerT;
        private float t;

        public static Wildlife Build(Transform parent, Transform player)
        {
            var w = parent.gameObject.AddComponent<Wildlife>();
            w.playerT = player;
            var rng = new Mulberry(777001);
            float[,] deerSpots = new float[,] { { -60f, 120f }, { 55f, 40f }, { -35f, -60f }, { 60f, -150f }, { -55f, -230f } };
            for (int i = 0; i < deerSpots.GetLength(0); i++)
            {
                float x = deerSpots[i, 0], z = deerSpots[i, 1];
                var d = new GameObject("Deer");
                d.transform.SetParent(parent, false);
                d.transform.localPosition = new Vector3(x, WorldBuilder.GroundH(x, z), z);
                MeshUtil.Box(d.transform, "Body", 0.55f, 0.6f, 1.25f, 0x7a5a40, 0f, 0.95f, 0f);
                var head = new GameObject("DeerHead");
                head.transform.SetParent(d.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.45f, 0.75f);
                MeshUtil.Box(head.transform, "Skull", 0.3f, 0.34f, 0.42f, 0x7a5a40, 0f, 0.1f, 0.1f);
                if (i % 2 == 0)
                {
                    MeshUtil.Box(head.transform, "Antler", 0.06f, 0.5f, 0.06f, 0xcfc4a8, -0.14f, 0.45f, 0f);
                    MeshUtil.Box(head.transform, "Antler", 0.06f, 0.5f, 0.06f, 0xcfc4a8, 0.14f, 0.45f, 0f);
                }
                MeshUtil.Box(d.transform, "LegsF", 0.42f, 0.75f, 0.13f, 0x5f4630, 0f, 0.38f, 0.4f);
                MeshUtil.Box(d.transform, "LegsB", 0.42f, 0.75f, 0.13f, 0x5f4630, 0f, 0.38f, -0.4f);
                MeshUtil.Ball(d.transform, "Tail", 0.11f, 0xe8e4d8, 0f, 1.15f, -0.68f, false);
                w.deer.Add(new Deer { root = d, head = head, home = d.transform.localPosition, phase = rng.Next() * 6.28f });
            }
            for (int i = 0; i < 8; i++)
            {
                float x = -120f + rng.Next() * 240f;
                float z = -260f + rng.Next() * 460f;
                if (Mathf.Abs(x - WorldBuilder.RiverX(z)) < 16f) x += 30f;
                var r = new GameObject("Rabbit");
                r.transform.SetParent(parent, false);
                r.transform.localPosition = new Vector3(x, WorldBuilder.GroundH(x, z), z);
                MeshUtil.Ball(r.transform, "Body", 0.16f, 0x8a7a68, 0f, 0.18f, 0f, false);
                MeshUtil.Ball(r.transform, "Head", 0.11f, 0x8a7a68, 0f, 0.3f, 0.16f, false);
                MeshUtil.Box(r.transform, "Ears", 0.17f, 0.22f, 0.05f, 0x8a7a68, 0f, 0.48f, 0.14f);
                w.rabbits.Add(new Rabbit { root = r, home = r.transform.localPosition, pos = r.transform.localPosition, hopPh = rng.Next() * 6.28f });
            }
            for (int i = 0; i < 7; i++)
            {
                var b = new GameObject("Bird");
                b.transform.SetParent(parent, false);
                MeshUtil.Box(b.transform, "Wings", 1.9f, 0.05f, 0.28f, 0x23262c, 0f, 0f, 0f);
                MeshUtil.Box(b.transform, "Body", 0.22f, 0.14f, 0.6f, 0x2c2f34, 0f, 0f, 0f);
                w.birds.Add(new Bird { root = b, r = 45f + rng.Next() * 70f, h = 55f + rng.Next() * 45f, ph = rng.Next() * 6.28f, sp = 0.14f + rng.Next() * 0.12f });
            }
            Debug.Log("AXEPATH wildlife: deer=" + w.deer.Count + " rabbits=" + w.rabbits.Count + " birds=" + w.birds.Count);
            return w;
        }

        void Update()
        {
            t += Time.deltaTime;
            Vector3 pp = playerT != null ? playerT.position : Vector3.zero;
            foreach (var d in deer)
            {
                Vector3 p = d.root.transform.localPosition;
                float distP = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(pp.x, 0f, pp.z));
                if (distP < 14f) d.fleeT = 1.6f;
                if (d.fleeT > 0f)
                {
                    d.fleeT -= Time.deltaTime;
                    Vector3 away = (p - new Vector3(pp.x, p.y, pp.z)).normalized;
                    p += away * 6.5f * Time.deltaTime;
                    d.root.transform.rotation = Quaternion.LookRotation(away);
                    d.head.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                }
                else
                {
                    // Graze: slow wander near home, head dips to the grass.
                    float wx = d.home.x + Mathf.Sin(t * 0.11f + d.phase) * 7f;
                    float wz = d.home.z + Mathf.Cos(t * 0.09f + d.phase * 1.3f) * 7f;
                    Vector3 want = new Vector3(wx, p.y, wz);
                    p = Vector3.MoveTowards(p, want, 0.7f * Time.deltaTime);
                    Vector3 dir = want - p; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.04f) d.root.transform.rotation = Quaternion.Slerp(d.root.transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 2f);
                    float dip = Mathf.Sin(t * 0.45f + d.phase) > 0.25f ? 32f : -6f;
                    d.head.transform.localRotation = Quaternion.Slerp(d.head.transform.localRotation, Quaternion.Euler(dip, 0f, 0f), Time.deltaTime * 2.5f);
                }
                p.x = Mathf.Clamp(p.x, -290f, 290f); p.z = Mathf.Clamp(p.z, -390f, 240f);
                p.y = WorldBuilder.GroundH(p.x, p.z) + (d.fleeT > 0f ? Mathf.Abs(Mathf.Sin(t * 9f)) * 0.12f : 0f);
                d.root.transform.localPosition = p;
            }
            for (int i = 0; i < rabbits.Count; i++)
            {
                var r = rabbits[i];
                // Continuous movement state: the rabbit is somewhere, then
                // moves. It never gets re-placed by a formula, so it can
                // never appear to teleport.
                Vector3 flat = new Vector3(r.pos.x, 0f, r.pos.z);
                Vector3 flatPlayer = new Vector3(pp.x, 0f, pp.z);
                float distP = Vector3.Distance(flat, flatPlayer);
                if (distP < 5f) r.fleeT = 1.1f;
                Vector3 want;
                float speed;
                if (r.fleeT > 0f)
                {
                    r.fleeT -= Time.deltaTime;
                    Vector3 away = (flat - flatPlayer);
                    if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
                    want = r.pos + away.normalized * 6f;
                    speed = 6.5f;
                }
                else
                {
                    want = r.home + new Vector3(Mathf.Sin(t * 0.3f + i * 2.1f) * 3f, 0f, Mathf.Cos(t * 0.24f + i) * 3f);
                    speed = 1.4f;
                }
                Vector3 before = r.pos;
                r.pos = Vector3.MoveTowards(r.pos, new Vector3(want.x, r.pos.y, want.z), speed * Time.deltaTime);
                Vector3 moved = r.pos - before; moved.y = 0f;
                if (moved.sqrMagnitude > 0.000001f) r.root.transform.rotation = Quaternion.Slerp(r.root.transform.rotation, Quaternion.LookRotation(moved.normalized), Time.deltaTime * 10f);
                float hopRate = r.fleeT > 0f ? 7f : 2.4f;
                float hop = Mathf.Max(0f, Mathf.Sin(t * hopRate + r.hopPh));
                r.pos.y = WorldBuilder.GroundH(r.pos.x, r.pos.z);
                r.pos.x = Mathf.Clamp(r.pos.x, -290f, 290f); r.pos.z = Mathf.Clamp(r.pos.z, -390f, 240f);
                r.root.transform.localPosition = new Vector3(r.pos.x, r.pos.y + hop * 0.22f, r.pos.z);
            }
            foreach (var b in birds)
            {
                float a = t * b.sp + b.ph;
                Vector3 c = new Vector3(Mathf.Cos(a) * b.r, 0f, -60f + Mathf.Sin(a) * b.r * 0.6f);
                Vector3 p = new Vector3(c.x, WorldBuilder.GroundH(c.x, c.z) + b.h + Mathf.Sin(t * 0.7f + b.ph) * 3f, c.z);
                b.root.transform.localPosition = p;
                b.root.transform.rotation = Quaternion.Euler(0f, -a * 57.29578f, Mathf.Sin(t * 2.2f + b.ph) * 7f);
            }
        }
    }
}
