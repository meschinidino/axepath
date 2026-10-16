// First-person body: mouse look, WASD run, circle collision, head bob,
// and the visible axe with its swing animation. Port of the slice player.
using System;
using UnityEngine;

namespace AxePath
{
    public class PlayerController : MonoBehaviour
    {
        public const float WalkSpeed = 4.2f;
        public const float RunSpeed = 7.2f;
        public const float EyeHeight = 1.65f;
        public const float LookSens = 0.004f;

        public Camera Cam { get; private set; }
        public float yaw;
        public float pitch;
        public bool running;
        public event Action OnStep;

        private GameObject axeView;
        private Material edgeMat;
        private float swingT;
        private float swingDur = 0.4f;
        private float bobT;
        private float stepAcc;
        private GameWorld world;

        public void Build(Vector3 spawn)
        {
            transform.position = spawn;
            yaw = 0f;
            pitch = 0f;
            ApplyLook();

            var camGo = new GameObject("Eye");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = MeshUtil.C(0x1a2438);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            Cam = cam;

            axeView = new GameObject("AxeView");
            axeView.transform.SetParent(camGo.transform, false);
            axeView.transform.localPosition = new Vector3(0.32f, -0.34f, 0.55f);
            axeView.transform.localRotation = Quaternion.Euler(10f, -24f, 14f);
            var handleMesh = MeshUtil.TaperMesh(0.022f, 0.026f, 0.7f);
            MeshUtil.Tapered(axeView.transform, "Handle", handleMesh, 0x6b5136, 0f, -0.05f, 0f);
            // Blade in profile: cheek runs forward (+Z), edge is the small
            // bright lip at the front-bottom corner that leads the swing.
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            var headMat = MeshUtil.FreshMat(0x3f434a);
            headMat.SetFloat("_Metallic", 0.55f);
            headMat.SetFloat("_Glossiness", 0.45f);
            head.GetComponent<Renderer>().sharedMaterial = headMat;
            head.transform.SetParent(axeView.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.3f, 0.06f);
            head.transform.localScale = new Vector3(0.055f, 0.13f, 0.16f);
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "Edge";
            edgeMat = MeshUtil.FreshMat(0xe8ecf0);
            edgeMat.SetFloat("_Metallic", 0.6f);
            edgeMat.SetFloat("_Glossiness", 0.55f);
            edge.GetComponent<Renderer>().sharedMaterial = edgeMat;
            edge.transform.SetParent(axeView.transform, false);
            edge.transform.localPosition = new Vector3(0f, 0.285f, 0.155f);
            edge.transform.localScale = new Vector3(0.06f, 0.12f, 0.035f);
            axeView.SetActive(false);
        }

        public void SetWorld(GameWorld w) { world = w; }

        public void Look(float dx, float dy)
        {
            yaw += dx * LookSens;
            pitch -= dy * LookSens;
            pitch = Mathf.Clamp(pitch, -1.45f, 1.45f);
            ApplyLook();
        }

        private void ApplyLook()
        {
            transform.rotation = Quaternion.Euler(0f, 180f + yaw * 57.29578f, 0f);
            if (Cam != null) Cam.transform.localRotation = Quaternion.Euler(pitch * 57.29578f, 0f, 0f);
        }

        public void Tick(float dt)
        {
            bool w = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool s = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool a = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool d = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            float f = (w ? 1f : 0f) - (s ? 1f : 0f);
            float st = (d ? 1f : 0f) - (a ? 1f : 0f);
            running = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && f > 0f;
            float speed = running ? RunSpeed : WalkSpeed;
            float sin = Mathf.Sin(yaw);
            float cos = Mathf.Cos(yaw);
            float mx = (-st * cos - f * sin) * speed * dt;
            float mz = (st * sin - f * cos) * speed * dt;
            var pos = transform.position;
            pos.x += mx;
            pos.z += mz;
            if (world != null)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (var c in world.colliders)
                    {
                        float dx = pos.x - c.x;
                        float dz = pos.z - c.z;
                        float d2 = dx * dx + dz * dz;
                        float min = c.r + 0.45f;
                        if (d2 < min * min && d2 > 1e-8f)
                        {
                            float dd = Mathf.Sqrt(d2);
                            float push = (min - dd) / dd;
                            pos.x += dx * push;
                            pos.z += dz * push;
                        }
                    }
                }
                pos.x = Mathf.Clamp(pos.x, world.minX, world.maxX);
                pos.z = Mathf.Clamp(pos.z, world.minZ, world.maxZ);
            }
            pos.y = WorldBuilder.GroundH(pos.x, pos.z);
            transform.position = pos;

            bool moving = f != 0f || st != 0f;
            if (moving)
            {
                bobT += dt * (running ? 11f : 8f);
                stepAcc += dt * (running ? 2.2f : 1.6f);
                if (stepAcc > 1f)
                {
                    stepAcc = 0f;
                    if (OnStep != null) OnStep();
                }
                Cam.transform.localPosition = new Vector3(0f, EyeHeight + Mathf.Sin(bobT) * 0.045f, 0f);
            }
            else
            {
                Cam.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            }

            if (swingT > 0f)
            {
                swingT -= dt;
                float k = 1f - Mathf.Max(0f, swingT) / swingDur;
                float arc = Mathf.Sin(k * Mathf.PI);
                // Chop travels down-forward so the edge leads into the target.
                // (Negative X swung the head back toward the face: the old
                // "hits with the bottom" read.)
                axeView.transform.localRotation = Quaternion.Euler(10f + arc * 62f, -24f, 14f);
                var ap = axeView.transform.localPosition;
                ap.z = 0.55f + arc * 0.34f;
                axeView.transform.localPosition = ap;
            }
            else
            {
                axeView.transform.localRotation = Quaternion.Euler(10f, -24f, 14f);
                var ap = axeView.transform.localPosition;
                ap.z = 0.55f;
                axeView.transform.localPosition = ap;
            }
        }

        public bool TrySwing(float swingMs)
        {
            if (swingT > 0f) return false;
            swingDur = swingMs / 1000f;
            swingT = swingDur;
            return true;
        }

        public void SetAxeEdge(int step, bool have)
        {
            axeView.SetActive(have);
            if (!have) return;
            uint[] colors = new uint[] { 0xcfd4d6, 0x8a8f94, 0x4a3f33 };
            edgeMat.color = MeshUtil.C(colors[Mathf.Clamp(step, 0, 2)]);
        }

        public Vec2 EyeXZ() { return new Vec2(transform.position.x, transform.position.z); }

        public Vec2 Facing() { return new Vec2(-Mathf.Sin(yaw), -Mathf.Cos(yaw)); }

        public void Teleport(float x, float z, float newYaw)
        {
            transform.position = new Vector3(x, WorldBuilder.GroundH(x, z), z);
            yaw = newYaw;
            pitch = 0f;
            ApplyLook();
        }

        public void Respawn(Vector3 spawn)
        {
            Teleport(spawn.x, spawn.z, 0f);
        }
    }
}
