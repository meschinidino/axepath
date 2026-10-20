// Sky life, built in code: drifting cloud banks plus wind/weather
// particle systems (streaming leaves, fireflies). Everything is opaque
// geometry or opaque-shader particles: transparent shaders do not
// survive this project's player builds.
using System.Collections.Generic;
using UnityEngine;

namespace AxePath
{
    public static class WeatherBuilder
    {
        public static void Build(Transform parent, WorldRefs refs)
        {
            BuildClouds(parent, refs);
            refs.leafPS = BuildLeaves(parent);
            refs.fireflyMats = new List<Material>();
            BuildFireflies(parent, "FirefliesLow", new Vector3(0f, 3f, 150f), true, refs);
            BuildFireflies(parent, "FirefliesGrove", new Vector3(72f, 43f, -340f), false, refs);
            // No ground-mist meshes: opaque fog-colored slabs read as dark
            // blobs near any bright light (mouth, grove, camp), and this map
            // has lights and slopes everywhere mist would live. Leaves,
            // fireflies, clouds, and river streaks carry the weather.
        }

        private static void BuildClouds(Transform parent, WorldRefs refs)
        {
            var group = new GameObject("Clouds");
            group.transform.SetParent(parent, false);
            var cloudMat = MeshUtil.FreshMat(0x8a93b8);
            cloudMat.EnableKeyword("_EMISSION_ON");
            cloudMat.SetColor("_EmissionColor", MeshUtil.C(0x1a2440));
            var topMat = MeshUtil.FreshMat(0xb8c4e0);
            topMat.EnableKeyword("_EMISSION_ON");
            topMat.SetColor("_EmissionColor", MeshUtil.C(0x2a3450));
            var rand = new Mulberry(9001);
            for (int i = 0; i < 10; i++)
            {
                float cx = -400f + i * 85f + (rand.Next() - 0.5f) * 40f;
                float cz = -420f + rand.Next() * 700f;
                float cy = 130f + rand.Next() * 70f;
                var cloud = new GameObject("Cloud");
                cloud.transform.SetParent(group.transform, false);
                cloud.transform.localPosition = new Vector3(cx, cy, cz);
                int puffs = 5 + (int)(rand.Next() * 3f);
                for (int p = 0; p < puffs; p++)
                {
                    float pr = 8f + rand.Next() * 8f;
                    var puff = MeshUtil.Ball(cloud.transform, "Puff", pr, 0x8a93b8,
                        (rand.Next() - 0.5f) * 40f, (rand.Next() - 0.5f) * 6f, (rand.Next() - 0.5f) * 24f, false);
                    puff.transform.localScale = new Vector3(pr * 2f, pr * 0.55f, pr * 2f);
                    puff.GetComponent<Renderer>().sharedMaterial = cloudMat;
                }
                // Moonlit cap: one pale puff riding the top.
                var cap = MeshUtil.Ball(cloud.transform, "Cap", 7f, 0xb8c4e0, 0f, 7f, 0f, false);
                cap.transform.localScale = new Vector3(22f, 5f, 16f);
                cap.GetComponent<Renderer>().sharedMaterial = topMat;
                refs.clouds.Add(cloud);
            }
        }

        private static ParticleSystem NewSystem(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static ParticleSystem BuildLeaves(Transform parent)
        {
            var ps = NewSystem(parent, "WindLeaves", new Vector3(0f, 30f, -70f));
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 70f;
            main.maxParticles = 1600;
            main.startLifetime = new ParticleSystem.MinMaxCurve(40f, 70f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(MeshUtil.C(0xd9c98a), MeshUtil.C(0x9aa0a8));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 25f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(620f, 50f, 660f);
            shape.boxThickness = new Vector3(1f, 1f, 1f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = 7f;
            vel.y = -0.4f;
            vel.z = 1f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            noise.frequency = 0.8f;
            noise.scrollSpeed = 1f;
            noise.damping = true;
            noise.octaveCount = 2;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            // Tinting rides the COLOR vertex stream, which Unlit/Color
            // ignores — the particle unlit shader reads it.
            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetFloat("_Mode", 0f);
            mat.SetColor("_Color", Color.white);
            var psr = ps.gameObject.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = mat;
            ps.Play();
            return ps;
        }

        private static void BuildFireflies(Transform parent, string name, Vector3 pos, bool boxNotSphere, WorldRefs refs)
        {
            var ps = NewSystem(parent, name, pos);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 9f;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;
            var shape = ps.shape;
            shape.enabled = true;
            if (boxNotSphere)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(300f, 6f, 150f);
                shape.boxThickness = new Vector3(1f, 1f, 1f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 25f;
            }
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;
            // Flat warm color is all fireflies need; the blink is a global
            // material pulse in the animator, out of phase per system.
            var mat = MeshUtil.FreshMat(0xd9e86a);
            mat.shader = Shader.Find("Unlit/Color");
            var psr = ps.gameObject.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = mat;
            refs.fireflyMats.Add(mat);
            ps.Play();
        }

    }
}
