// Per-frame life for the actors: wolf hit reaction, eye fierceness,
// NPC facing. Port of the slice actor updates.
using System.Collections.Generic;
using UnityEngine;

namespace AxePath
{
    public class WolfFx
    {
        public float hitT;
        public Vec2 hitDir;
        public float flashT;
        public float knockT;
        public Vec2 knockDir;
        public bool flashed;
        public float deadT;
        public bool corpseGone;
    }

    public class WolfView : MonoBehaviour
    {
        private GameObject root;
        private List<GameObject> eyes;
        private List<Material> furMats;
        private List<Material> eyeMats = new List<Material>();
        private WolfState brain;
        private WolfFx fx;
        private Transform[] legs = new Transform[4];
        private Transform headPivot;
        private Transform tailPivot;
        private float gaitPhase;

        public void Bind(GameObject root, List<GameObject> eyes, List<Material> furMats, WolfState brain, WolfFx fx)
        {
            this.root = root;
            this.eyes = eyes;
            this.furMats = furMats;
            this.brain = brain;
            this.fx = fx;
            string[] legNames = { "LegFL", "LegFR", "LegBL", "LegBR" };
            for (int i = 0; i < 4; i++) legs[i] = root.transform.Find(legNames[i]);
            headPivot = root.transform.Find("HeadPivot");
            tailPivot = root.transform.Find("TailPivot");
            eyeMats.Clear();
            foreach (var e in eyes)
            {
                var m = new Material(e.GetComponent<Renderer>().sharedMaterial);
                e.GetComponent<Renderer>().sharedMaterial = m;
                eyeMats.Add(m);
            }
            foreach (var m in furMats) m.EnableKeyword("_EMISSION_ON");
        }

        public void Rebind(WolfState brain, WolfFx fx)
        {
            this.brain = brain;
            this.fx = fx;
        }

        public void Tick(float dt, float t, Vector3 playerPos)
        {
            if (fx.knockT > 0f)
            {
                fx.knockT -= dt;
                float kk = Mathf.Max(0f, fx.knockT) / Tuning.WolfHitSecs;
                brain.pos.x += fx.knockDir.x * kk * Tuning.WolfKnockSpeed * dt;
                brain.pos.z += fx.knockDir.z * kk * Tuning.WolfKnockSpeed * dt;
            }
            root.transform.localPosition = new Vector3(brain.pos.x, WorldBuilder.GroundH(brain.pos.x, brain.pos.z), brain.pos.z);
            if (fx.hitT > 0f)
            {
                fx.hitT -= dt;
                float k = Mathf.Max(0f, fx.hitT) / Tuning.WolfHitSecs;
                float punch = Mathf.Sin(k * Mathf.PI);
                var rp = root.transform.localPosition;
                rp.x += fx.hitDir.x * punch * 0.55f;
                rp.z += fx.hitDir.z * punch * 0.55f;
                root.transform.localPosition = rp;
                root.transform.localScale = Vector3.one * (1f - punch * 0.14f);
            }
            else if (brain.alive && brain.state == "windup")
            {
                root.transform.localScale = new Vector3(1.06f, 0.8f, 1.06f);
            }
            else
            {
                root.transform.localScale = Vector3.one;
            }
            if (fx.flashT > 0f)
            {
                fx.flashT -= dt;
                float f = Mathf.Max(0f, fx.flashT) / Tuning.WolfFlashSecs;
                var glow = new Color(f, f * 0.3f, f * 0.2f);
                foreach (var m in furMats) m.SetColor("_EmissionColor", glow);
            }
            else if (fx.flashed)
            {
                foreach (var m in furMats) m.SetColor("_EmissionColor", Color.black);
                fx.flashed = false;
            }
            float fierce = !brain.alive ? 1f : brain.state == "windup" ? 1.6f : brain.state == "chase" ? 1.35f : brain.state == "warn" ? 1.15f : 1f;
            var eyeColor = MeshUtil.C(!brain.alive ? 0x241a1c : brain.state == "windup" ? 0xff6a2a : brain.state == "chase" ? 0xff3a1au : 0xff8a3au);
            for (int i = 0; i < eyes.Count; i++)
            {
                eyes[i].transform.localScale = Vector3.one * 0.1f * fierce;
                eyeMats[i].color = eyeColor;
            }
            if (brain.alive && (brain.state == "chase" || brain.state == "warn" || brain.state == "windup"))
            {
                FaceToward(root.transform, brain.pos.x, brain.pos.z, playerPos.x, playerPos.z, dt, 6f);
            }
            if (!brain.alive)
            {
                fx.deadT += dt;
                float tilt = Mathf.Min(1f, fx.deadT / 0.35f);
                float curY = root.transform.localRotation.eulerAngles.y;
                root.transform.localRotation = Quaternion.Euler(0f, curY, 0f) * Quaternion.Euler(0f, 0f, tilt * 80f);
            }
            if (brain.alive && brain.state != "windup")
            {
                var bp = root.transform.localPosition;
                bp.y = WorldBuilder.GroundH(bp.x, bp.z) + Mathf.Abs(Mathf.Sin(t * (brain.state == "chase" ? 10f : 3f))) * 0.06f;
                root.transform.localPosition = bp;
            }
            // Gait: diagonal pairs swing together; pace follows intent.
            float pace = !brain.alive ? 0f : brain.state == "chase" ? 11f : brain.state == "stalk" ? 6f : brain.state == "warn" ? 2.5f : 0.8f;
            gaitPhase += dt * pace;
            float amp = brain.state == "chase" ? 38f : 22f;
            for (int i = 0; i < 4; i++)
            {
                if (legs[i] == null) continue;
                float off = (i == 0 || i == 3) ? 0f : Mathf.PI;
                float swing = brain.alive ? Mathf.Sin(gaitPhase + off) * amp : 65f;
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }
            if (headPivot != null)
            {
                float pitch = !brain.alive ? 25f : brain.state == "windup" ? -18f : brain.state == "warn" ? 14f : brain.state == "chase" ? 6f : Mathf.Sin(t * 1.7f) * 4f;
                headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, Quaternion.Euler(pitch, 0f, 0f), Mathf.Min(1f, dt * 6f));
            }
            if (tailPivot != null)
            {
                float wag = brain.alive ? Mathf.Sin(gaitPhase * 0.6f) * 10f : 0f;
                tailPivot.localRotation = Quaternion.Euler(0f, wag, brain.alive ? -8f : 20f);
            }
        }

        public static void FaceToward(Transform tr, float fromX, float fromZ, float toX, float toZ, float dt, float rate)
        {
            float want = Mathf.Atan2(toX - fromX, toZ - fromZ);
            float cur = tr.localRotation.eulerAngles.y / 57.29578f;
            float d = want - cur;
            while (d > Mathf.PI) d -= Mathf.PI * 2f;
            while (d < -Mathf.PI) d += Mathf.PI * 2f;
            float turn = Mathf.Clamp(d, -rate * dt, rate * dt);
            tr.localRotation = Quaternion.Euler(0f, (cur + turn) * 57.29578f, 0f);
        }
    }

    public class NpcView : MonoBehaviour
    {
        private GameObject dwarf;
        private GameObject librarian;

        public void Bind(GameObject dwarf, GameObject librarian)
        {
            this.dwarf = dwarf;
            this.librarian = librarian;
        }

        public void Tick(float dt, float t, Vector3 playerPos, bool dwarfAlive)
        {
            if (dwarfAlive && dwarf.activeSelf)
            {
                var dp = dwarf.transform.localPosition;
                dp.y = WorldBuilder.GroundH(dp.x, dp.z) + Mathf.Sin(t * 1.4f) * 0.02f;
                dwarf.transform.localPosition = dp;
                var forgeArm = dwarf.transform.Find("ForgeArm");
                if (Near(dwarf.transform.localPosition, playerPos, 3.4f))
                {
                    WolfView.FaceToward(dwarf.transform, dp.x, dp.z, playerPos.x, playerPos.z, dt, 8f);
                    if (forgeArm != null) forgeArm.localRotation = Quaternion.Slerp(forgeArm.localRotation, Quaternion.Euler(-12f, 0f, 0f), Mathf.Min(1f, dt * 5f));
                }
                else
                {
                    EaseYaw(dwarf.transform, 0.4f, dt);
                    // Hammer loop: raise slow, strike fast, rest, repeat.
                    if (forgeArm != null)
                    {
                        float cyc = (t % 2.4f) / 2.4f;
                        float ang = cyc < 0.55f ? Mathf.Lerp(-8f, -68f, cyc / 0.55f) : cyc < 0.68f ? Mathf.Lerp(-68f, 6f, (cyc - 0.55f) / 0.13f) : 6f - (cyc - 0.68f) * 40f;
                        forgeArm.localRotation = Quaternion.Euler(ang, 0f, 0f);
                    }
                }
            }
            if (Near(librarian.transform.localPosition, playerPos, 3.4f))
            {
                var lp = librarian.transform.localPosition;
                WolfView.FaceToward(librarian.transform, lp.x, lp.z, playerPos.x, playerPos.z, dt, 8f);
            }
            else
            {
                librarian.transform.localRotation = Quaternion.Euler(0f, (-0.5f + Mathf.Sin(t * 0.7f) * 0.08f) * 57.29578f, 0f);
            }
        }

        private static bool Near(Vector3 o, Vector3 p, float r)
        {
            float dx = p.x - o.x;
            float dz = p.z - o.z;
            return dx * dx + dz * dz < r * r;
        }

        private static void EaseYaw(Transform tr, float target, float dt)
        {
            float cur = tr.localRotation.eulerAngles.y / 57.29578f;
            float d = target - cur;
            while (d > Mathf.PI) d -= Mathf.PI * 2f;
            while (d < -Mathf.PI) d += Mathf.PI * 2f;
            tr.localRotation = Quaternion.Euler(0f, (cur + d * Mathf.Min(1f, dt * 3f)) * 57.29578f, 0f);
        }
    }
}
