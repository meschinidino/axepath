// Entry point: builds the whole game in code on scene load. The saved scene
// stays an empty boot container; everything visible is constructed here.
using UnityEngine;

namespace AxePath
{
    public static class Game
    {
        public static GameWorld World { get; private set; }
        public static ActorRefs Actors { get; private set; }
        public static Camera MainCam { get; private set; }
        public static GameSession Session { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameObject.Find("AxePath") != null) return;
            var root = new GameObject("AxePath");

            RenderSettings.fog = true;
            RenderSettings.fogColor = MeshUtil.C(0x1a2438);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0016f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = MeshUtil.C(0x8fa3c7);
            RenderSettings.ambientEquatorColor = MeshUtil.C(0x8fa3c7);
            RenderSettings.ambientGroundColor = MeshUtil.C(0x1d2b1f);
            RenderSettings.ambientIntensity = 0.75f;

            var skyMat = SkyBuilder.BuildNightSky();
            bool hasSky = skyMat != null;
            if (hasSky) RenderSettings.skybox = skyMat;

            // Moonlight key: parked where the painted moon sits (up north,
            // ~40 degrees) so shadows fall away from the moon, not some
            // unrelated azimuth. A faint shadowless fill from the south
            // keeps the faces the climber sees readable.
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root.transform, false);
            sunGo.transform.localPosition = new Vector3(-60f, 190f, -270f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = MeshUtil.C(0x9fb8e8);
            sun.intensity = 0.8f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.LookAt(new Vector3(0f, 0f, -50f));
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.localPosition = new Vector3(60f, 140f, 220f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = MeshUtil.C(0x8a9ac0);
            fill.intensity = 0.22f;
            fill.shadows = LightShadows.None;
            fillGo.transform.LookAt(new Vector3(0f, 0f, -50f));

            World = WorldBuilder.Build(root.transform);
            WeatherBuilder.Build(root.transform, World.refs);
            Actors = ActorBuilder.Build(root.transform, World.spots);

            var anim = root.AddComponent<WorldAnimator>();
            anim.forgeLight = World.refs.forgeLight;
            anim.smokePuffs = World.refs.smokePuffs;
            anim.smokeBase = World.refs.smokeBase;
            anim.riverFoam = World.refs.riverFoam;
            anim.riverStreaks = World.refs.riverStreaks;
            anim.millWheel = World.refs.millWheel;
            anim.waterMat = World.refs.waterMat;
            anim.clouds = World.refs.clouds;
            anim.leafPS = World.refs.leafPS;
            anim.fireflyMats = World.refs.fireflyMats;

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(root.transform, false);
            var player = playerGo.AddComponent<PlayerController>();
            player.Build(new Vector3(World.spots.spawn.x, 0f, World.spots.spawn.z));
            player.SetWorld(World);
            MainCam = player.Cam;
            MainCam.clearFlags = hasSky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;

            var wolfViews = new System.Collections.Generic.List<WolfView>();
            for (int i = 0; i < World.spots.wolfHomes.Count; i++)
            {
                wolfViews.Add(root.AddComponent<WolfView>());
            }
            var npcView = root.AddComponent<NpcView>();

            var hudGo = new GameObject("Hud");
            hudGo.transform.SetParent(root.transform, false);
            var hud = hudGo.AddComponent<Hud>();
            hud.Build();

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(root.transform, false);
            var audio = audioGo.AddComponent<AudioSynth>();

            var sessionGo = new GameObject("Session");
            sessionGo.transform.SetParent(root.transform, false);
            var session = sessionGo.AddComponent<GameSession>();
            Session = session;
            session.StartSession(World, Actors, player, hud, audio, wolfViews, npcView);

            Debug.Log("AXEPATH: session ready, trees=" + World.refs.treeCount);
        }
    }
}
