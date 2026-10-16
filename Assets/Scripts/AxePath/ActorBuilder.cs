// Living figures, gray-box bodies with the approved silhouettes.
// Faithful port of the slice actors (same numbers).
using System.Collections.Generic;
using UnityEngine;

namespace AxePath
{
    public class ActorRefs
    {
        public GameObject dwarf;
        public GameObject librarian;
        public GameObject wolf;
        public List<GameObject> wolfEyes = new List<GameObject>();
        public List<Material> wolfFurMats = new List<Material>();
    }

    public static class ActorBuilder
    {
        private static GameObject Part(Transform parent, string name, float w, float h, float d, uint color, float x, float y, float z, List<Material> mats)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var m = MeshUtil.FreshMat(color);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (mats != null) mats.Add(m);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localScale = new Vector3(w, h, d);
            return go;
        }

        public static ActorRefs Build(Transform parent, Spots spots)
        {
            var refs = new ActorRefs();
            var actors = new GameObject("Actors");
            actors.transform.SetParent(parent, false);

            // Dwarf.
            var dwarf = new GameObject("Dwarf");
            dwarf.transform.SetParent(actors.transform, false);
            dwarf.transform.localPosition = new Vector3(spots.dwarf.x, WorldBuilder.GroundH(spots.dwarf.x, spots.dwarf.z), spots.dwarf.z);
            dwarf.transform.localRotation = Quaternion.Euler(0f, 0.4f * 57.3f, 0f);
            MeshUtil.Box(dwarf.transform, "Torso", 0.9f, 0.9f, 0.6f, 0x5a4630, 0f, 0.75f, 0f);
            MeshUtil.Box(dwarf.transform, "Apron", 0.7f, 0.7f, 0.1f, 0x2c2f34, 0f, 0.7f, 0.32f);
            MeshUtil.Ball(dwarf.transform, "Head", 0.3f, 0xb08a68, 0f, 1.45f, 0f, false);
            var beardMesh = MeshUtil.TaperMesh(0.02f, 0.24f, 0.7f);
            var beard = MeshUtil.Tapered(dwarf.transform, "Beard", beardMesh, 0xcfc4a8, 0f, 1.05f, 0.22f);
            beard.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            MeshUtil.Box(dwarf.transform, "Arm", 0.22f, 0.5f, 0.22f, 0x5a4630, -0.58f, 0.7f, 0f);
            MeshUtil.Box(dwarf.transform, "Arm", 0.22f, 0.5f, 0.22f, 0x5a4630, 0.58f, 0.7f, 0f);
            MeshUtil.Box(dwarf.transform, "Boot", 0.26f, 0.3f, 0.26f, 0x2c2f34, -0.2f, 0.15f, 0f);
            MeshUtil.Box(dwarf.transform, "Boot", 0.26f, 0.3f, 0.26f, 0x2c2f34, 0.2f, 0.15f, 0f);
            refs.dwarf = dwarf;

            // Librarian.
            var librarian = new GameObject("Librarian");
            librarian.transform.SetParent(actors.transform, false);
            librarian.transform.localPosition = new Vector3(spots.librarian.x, WorldBuilder.GroundH(spots.librarian.x, spots.librarian.z), spots.librarian.z);
            librarian.transform.localRotation = Quaternion.Euler(0f, -0.5f * 57.3f, 0f);
            var robeMesh = MeshUtil.TaperMesh(0.02f, 0.55f, 1.7f);
            MeshUtil.Tapered(librarian.transform, "Robe", robeMesh, 0x3a4a6b, 0f, 0.85f, 0f);
            MeshUtil.Ball(librarian.transform, "Head", 0.26f, 0xc09a78, 0f, 1.85f, 0f, false);
            MeshUtil.Box(librarian.transform, "Book", 0.5f, 0.08f, 0.3f, 0xd9cfae, 0f, 1.1f, 0.3f);
            refs.librarian = librarian;

            // Wolf (faces +z like the slice; the game orients it at runtime).
            uint fur = 0x23262c;
            var wolf = new GameObject("Wolf");
            wolf.transform.SetParent(actors.transform, false);
            wolf.transform.localPosition = new Vector3(spots.den.x, WorldBuilder.GroundH(spots.den.x, spots.den.z), spots.den.z);
            var furMats = refs.wolfFurMats;
            Part(wolf.transform, "Body", 0.55f, 0.5f, 1.3f, fur, 0f, 0.62f, 0f, furMats);
            Part(wolf.transform, "Head", 0.42f, 0.4f, 0.5f, fur, 0f, 0.95f, 0.8f, furMats);
            Part(wolf.transform, "Snout", 0.2f, 0.2f, 0.3f, 0x1a1d22, 0f, 0.85f, 1.15f, furMats);
            var eyeL = MeshUtil.Ball(wolf.transform, "Eye", 0.05f, 0xff8a3a, -0.13f, 1.02f, 1.02f, true);
            var eyeR = MeshUtil.Ball(wolf.transform, "Eye", 0.05f, 0xff8a3a, 0.13f, 1.02f, 1.02f, true);
            refs.wolfEyes.Add(eyeL);
            refs.wolfEyes.Add(eyeR);
            Part(wolf.transform, "Tail", 0.12f, 0.12f, 0.7f, fur, 0f, 0.75f, -0.9f, furMats);
            float[,] legs = new float[,] { {-0.2f,0.4f},{0.2f,0.4f},{-0.2f,-0.4f},{0.2f,-0.4f} };
            for (int i = 0; i < 4; i++)
            {
                Part(wolf.transform, "Leg", 0.14f, 0.5f, 0.14f, fur, legs[i, 0], 0.25f, legs[i, 1], furMats);
            }
            refs.wolf = wolf;

            return refs;
        }
    }
}
