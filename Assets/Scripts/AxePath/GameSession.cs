// Game flow: title slots, play tick, interactions, combat, death, saves.
// Port of the slice wiring with the same lines and rules.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AxePath
{
    public class FileBackend : ISaveBackend
    {
        private string PathFor(string key) { return Path.Combine(Application.persistentDataPath, key + ".json"); }
        public string GetItem(string key)
        {
            string p = PathFor(key);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        public void SetItem(string key, string value) { File.WriteAllText(PathFor(key), value); }
    }

    public class DialogueState
    {
        public string name;
        public List<string> lines;
        public int index;
        public Action onDone;
        public float anchorX;
        public float anchorZ;
    }

    public class InteractTarget
    {
        public string id;
        public int bi;
        public float x;
        public float z;
        public string label;
        public float reach = 3.4f;
    }

    public class WolfUnit
    {
        public WolfState brain;
        public WolfFx fx;
        public int hp;
        public WolfView view;
        public float flinchT;
    }

    public class TreeShake
    {
        public Transform tr;
        public float t;
        public Quaternion baseRot;
    }

    public enum MoteKind { Chip, Blood, Spark }

    public class Mote
    {
        public GameObject go;
        public Vector3 vel;
        public float life;
        public float grav;
        public Vector3 spin;
        public Vector3 baseScale;
        public MoteKind kind;
    }

    public class GameSession : MonoBehaviour
    {
        public enum State { Title, Play, Paused }

        public const string LookSensPrefKey = "axepath.looksens";
        public const string VolumePrefKey = "axepath.volume";
        public const string MutePrefKey = "axepath.mute";

        public State CurrentState { get; private set; }
        public WorldState W { get; private set; }
        public List<WolfUnit> Wolves { get; private set; }
        public PlayerController Player { get; private set; }
        public Hud Hud { get; private set; }
        public AudioSynth Audio { get { return audio; } }

        // v1 needs a desktop with mouse and keys; handhelds get one notice.
        public static bool IsHandheld()
        {
            return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
        }

        private GameWorld world;
        private ActorRefs actors;
        private AudioSynth audio;
        private List<WolfView> wolfViews;
        private NpcView npcView;
        private Saver saver;
        private float wolfCooldown;
        private float snapCooldown;
        private float hitStop;
        public float HitStop { get { return hitStop; } }
        private bool swingWaiting;
        private WolfUnit pendingWolf;
        private Circle? pendingTree;
        private Circle? pendingSurface;
        private bool pendingDwarfKill;
        private readonly List<TreeShake> treeShakes = new List<TreeShake>();
        private readonly List<Mote> motes = new List<Mote>();
        private Transform moteRoot;
        public int MoteCount { get { return motes.Count; } }
        public int MotesOf(MoteKind kind) { int n = 0; foreach (var m in motes) if (m.kind == kind) n++; return n; }
        public Vector3 MotePosOf(MoteKind kind)
        {
            foreach (var m in motes) if (m.kind == kind && m.go != null) return m.go.transform.localPosition;
            return Vector3.zero;
        }
        public int TreeShakeCount { get { return treeShakes.Count; } }
        private int slot = 1;
        private DialogueState dialogue;
        private bool bookOpen;
        private float lostT;
        private bool visitedYard;
        private bool visitedMouth;
        private int strangerShownGen;
        private float promptT;
        private float playT;
        private float howlT;
        private DropPos pendingDeathPos;
        private readonly HashSet<int> foraged = new HashSet<int>();
        private float musicT;

        public void StartSession(GameWorld world, ActorRefs actors, PlayerController p, Hud h, AudioSynth audio, List<WolfView> wolfViews, NpcView npcView)
        {
            this.world = world;
            this.actors = actors;
            Player = p;
            Hud = h;
            this.audio = audio;
            this.wolfViews = wolfViews;
            this.npcView = npcView;
            saver = new Saver(new FileBackend());
            Wolves = new List<WolfUnit>();
            for (int i = 0; i < world.spots.wolfHomes.Count; i++)
            {
                var home = world.spots.wolfHomes[i];
                var unit = new WolfUnit
                {
                    brain = Logic.CreateWolf(home.x, home.z),
                    fx = new WolfFx(),
                    hp = 2,
                    view = wolfViews[i],
                };
                Wolves.Add(unit);
                wolfViews[i].Bind(actors.wolfPack[i], actors.wolfPackEyes[i], actors.wolfPackFurMats[i], unit.brain, unit.fx);
            }
            npcView.Bind(actors.dwarf, actors.librarian);
            Player.OnStep += () => audio.PlayStep(Player.running);
            Hud.OnWake += () => { audio.PlayClick(); Wake(); };
            Hud.OnBookClose += () => { audio.PlayClick(); bookOpen = false; Hud.CloseBook(); };
            Hud.OnExport += () => Export();
            Hud.OnFirstLock += () => { audio.PlayClick(); Hud.ShowFirstLock(false); LockCursor(); };
            Hud.OnPauseResume += () => { if (CurrentState == State.Paused) Resume(); };
            Hud.OnPauseQuit += () => { if (CurrentState == State.Paused) ToTitle(); };
            Hud.OnLookSens += SetLookSens;
            LoadLookSens();
            Hud.OnVolume += SetVolume;
            Hud.OnMuteToggle += ToggleMute;
            LoadVolume();
            CurrentState = State.Title;
            Hud.RenderSlots((s) => saver.Describe(s), (s, info) => PickSlotWith(s, info));
            Hud.ShowTitle(true);
            if (IsHandheld()) Hud.ShowModal(Lines.DesktopOnlyTitle, Lines.DesktopOnlyBody, null, null);
        }

        private void PickSlotWith(int s, SlotDesc info)
        {
            audio.PlayClick();
            if (info.state == "filled")
            {
                var parsed = Logic.ParseSave(saver.Load(s));
                if (!parsed.ok)
                {
                    Hud.ShowModal(Lines.ModalBrokenSave, parsed.error + Lines.ModalBrokenSuffix, () => NewGame(s), null);
                    return;
                }
                Continue(s, parsed.world);
            }
            else
            {
                NewGame(s);
            }
        }

        public void NewGame(int s)
        {
            W = Logic.CreateWorld();
            saver.Save(s, Logic.SerializeWorld(W, Lines.LabelGen1Nobody));
            CommonStart(s);
        }

        public void Continue(int s, WorldState w)
        {
            W = w;
            CommonStart(s);
        }

        private void CommonStart(int s)
        {
            ClearSwing();
            slot = s;
            CurrentState = State.Play;
            Player.Respawn(new Vector3(world.spots.spawn.x, 0f, world.spots.spawn.z));
            ResetPack();
            wolfCooldown = 0f;
            ClearMotes();
            lostT = 0f;
            howlT = 12f;
            visitedYard = false;
            visitedMouth = false;
            strangerShownGen = 0;
            dialogue = null;
            bookOpen = false;
            Hud.CloseAllScreens();
            RefreshHud();
            Hud.ShowHud(true);
            audio.StartWind();
            LockCursor();
            Hud.ShowFirstLock(true);
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // A batchmode run (automated tests) has no window and no cursor:
        // lock requests are silently ignored there, so the sim must not
        // wait on them. Real players never run batchmode.
        private bool IsLocked() { return Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked; }

        private bool OverlaysOpen()
        {
            return dialogue != null || bookOpen || Hud.IsModalOpen() || Hud.IsDeathOpen();
        }

        private bool Paused()
        {
            // Dialogue stays live: the world keeps moving and walking more
            // than a few steps away cancels the talk. Book, modal, death,
            // and an unlocked cursor still freeze the sim.
            return CurrentState != State.Play || bookOpen || Hud.IsModalOpen() || Hud.IsDeathOpen() || !IsLocked();
        }

        private void RefreshHud()
        {
            Hud.SetEdge(Logic.EdgeStep(W), W.axe.have);
            Player.SetAxeEdge(Logic.EdgeStep(W), W.axe.have);
            Hud.SetHp(W.player.hp);
            Hud.SetHands(W.axe.have, W.book.have, W.ironwood.have);
            Hud.SetStamina(Player.stamina / Tuning.StaminaMax, Player.sneaking, Player.blocking);
            Hud.SetLoot(W.wolfPelts, W.wolfMeat, W.berries);
            Hud.SetBanner(saver.sessionOnly ? Lines.BannerNoSave : "");
            RefsSync();
        }

        private void RefsSync()
        {
            var r = world.refs;
            r.axeMesh.SetActive(W.axe.yard);
            r.bookMesh.SetActive(!W.book.have);
            r.ironLog.SetActive(!W.ironwood.turnedIn && W.ironwood.have < 1);
            var c = W.axe.droppedAt;
            r.cairn.SetActive(c != null);
            if (c != null) r.cairn.transform.localPosition = new Vector3(c.x, WorldBuilder.GroundH(c.x, c.z), c.z);
            actors.dwarf.SetActive(W.dwarfAlive);
            for (int i = 0; i < Wolves.Count; i++)
            {
                actors.wolfPack[i].SetActive(Wolves[i].brain.alive || !Wolves[i].fx.corpseGone);
            }
        }

        private void ResetPack()
        {
            for (int i = 0; i < Wolves.Count; i++)
            {
                var home = world.spots.wolfHomes[i];
                Wolves[i].brain = Logic.CreateWolf(home.x, home.z);
                Wolves[i].fx = new WolfFx();
                Wolves[i].hp = 2;
                Wolves[i].flinchT = 0f;
                Wolves[i].view.Rebind(Wolves[i].brain, Wolves[i].fx);
            }
        }

        private string Autosave(string label)
        {
            if (W == null) return "none";
            var res = saver.Save(slot, Logic.SerializeWorld(W, label));
            RefreshHud();
            return res.status;
        }

        private void Say(string name, List<string> lines, Action onDone)
        {
            var p = Player.EyeXZ();
            dialogue = new DialogueState { name = name, lines = lines, index = 0, onDone = onDone, anchorX = p.x, anchorZ = p.z };
            Hud.ShowDialogue(name, lines[0]);
            Hud.SetPrompt("", 0f);
        }

        private void SayOne(string name, string line) { Say(name, new List<string> { line }, null); }

        public void AdvanceDialogue()
        {
            if (dialogue == null) return;
            audio.PlayClick();
            dialogue.index += 1;
            if (dialogue.index >= dialogue.lines.Count)
            {
                var done = dialogue.onDone;
                dialogue = null;
                Hud.HideDialogue();
                if (done != null) done();
            }
            else
            {
                Hud.ShowDialogue(dialogue.name, dialogue.lines[dialogue.index]);
            }
        }

        private static float Gdist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public InteractTarget InteractTarget()
        {
            var p = Player.EyeXZ();
            var cands = new List<InteractTarget>();
            var spots = world.spots;
            var refs = world.refs;
            if (!W.axe.have && W.axe.yard)
            {
                cands.Add(new InteractTarget { id = "axe", x = refs.axeStandPos.x, z = refs.axeStandPos.z, label = Lines.PromptAxe });
            }
            if (!W.book.have)
            {
                cands.Add(new InteractTarget { id = "bookstand", x = refs.bookStandPos.x, z = refs.bookStandPos.z, label = Lines.PromptBookTake });
            }
            else
            {
                cands.Add(new InteractTarget { id = "readbook", x = refs.bookStandPos.x, z = refs.bookStandPos.z, label = Lines.PromptBookRead });
            }
            cands.Add(new InteractTarget { id = "librarian", x = spots.librarian.x, z = spots.librarian.z, label = Lines.PromptLibrarian });
            if (W.dwarfAlive)
            {
                cands.Add(new InteractTarget { id = "dwarf", x = spots.dwarf.x, z = spots.dwarf.z, label = Lines.PromptDwarf });
            }
            else
            {
                cands.Add(new InteractTarget { id = "dwarfnote", x = spots.dwarf.x, z = spots.dwarf.z, label = Lines.PromptDwarfNote });
            }
            if (!W.ironwood.turnedIn && W.ironwood.have < 1)
            {
                cands.Add(new InteractTarget { id = "ironwood", x = refs.ironLogPos.x, z = refs.ironLogPos.z, label = Lines.PromptIronwood, reach = 5f });
            }
            if (W.axe.droppedAt != null)
            {
                cands.Add(new InteractTarget { id = "cairn", x = W.axe.droppedAt.x, z = W.axe.droppedAt.z, label = Lines.PromptCairn });
            }
            for (int i = 0; i < refs.blazePos.Count; i++)
            {
                cands.Add(new InteractTarget { id = "blaze", bi = i, x = refs.blazePos[i].x, z = refs.blazePos[i].z, label = Lines.PromptBlaze });
            }
            // The mark sits inside its wall corner's disc, so the default
            // 3.4 reach can never land from a walkable spot: extended like
            // the ironwood log's. Closest approach is ~2u off the edge.
            cands.Add(new InteractTarget { id = "guide", x = refs.guidePos.x, z = refs.guidePos.z, label = Lines.PromptGuide, reach = 4.5f });
            cands.Add(new InteractTarget { id = "waystone", x = refs.waystonePos.x, z = refs.waystonePos.z, label = Lines.PromptWaystone });
            for (int i = 0; i < refs.bushPos.Count; i++)
            {
                if (foraged.Contains(i)) continue;
                cands.Add(new InteractTarget { id = "forage", bi = i, x = refs.bushPos[i].x, z = refs.bushPos[i].z, label = Lines.PromptForage, reach = 2.8f });
            }
            InteractTarget best = null;
            float bestD = float.MaxValue;
            foreach (var c in cands)
            {
                float d = Gdist(p.x, p.z, c.x, c.z);
                if (d < c.reach && d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        public void TryInteract()
        {
            var t = InteractTarget();
            if (t == null) return;
            audio.PlayClick();
            if (t.id == "axe")
            {
                if (Logic.TakeYardAxe(W))
                {
                    RefreshHud();
                    Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelAxeTaken);
                    Say("You", new List<string> {
                        Lines.YouAxeHeavy,
                        Lines.YouAxeYours,
                    }, null);
                }
                return;
            }
            if (t.id == "bookstand" || t.id == "readbook")
            {
                Logic.TakeBook(W);
                RefreshHud();
                bookOpen = true;
                Hud.ShowBook(Lines.BookTitle, Lines.BookSketch);
                return;
            }
            if (t.id == "librarian")
            {
                bool first = !W.flags.metLibrarian;
                W.flags.metLibrarian = true;
                Logic.TakeBook(W);
                SayOne("Librarian", first ? Lines.LibrarianRoute : Lines.LibrarianReplay);
                return;
            }
            if (t.id == "dwarf")
            {
                var res = Logic.TalkDwarf(W);
                if (res.evt == "thaw")
                {
                    Say("Dwarf", new List<string> { Lines.DwarfCold, Lines.DwarfArrival }, null);
                    Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelCivil);
                }
                else if (res.evt == "cold")
                {
                    if (W.generation > 1 && strangerShownGen != W.generation)
                    {
                        strangerShownGen = W.generation;
                        Say("Dwarf", new List<string> { Lines.DwarfStranger, Lines.DwarfCold }, null);
                    }
                    else SayOne("Dwarf", Lines.DwarfCold);
                }
                else if (res.evt == "idle-civil")
                {
                    if (W.ironwood.have >= 1 && W.axe.have)
                    {
                        if (Logic.TurnInIronwood(W))
                        {
                            audio.PlaySharpen();
                            RefreshHud();
                            Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelSharpened);
                            SayOne("Dwarf", Lines.DwarfSharpening);
                        }
                    }
                    else SayOne("Dwarf", Lines.DwarfCivil);
                }
                else if (res.evt == "idle-sharp") SayOne("Dwarf", Lines.DwarfSharpening);
                else if (res.evt == "refused") SayOne("Dwarf", Lines.DwarfRefused);
                RefreshHud();
                return;
            }
            if (t.id == "dwarfnote")
            {
                Hud.ShowModal(Lines.ModalColdForge, Lines.DwarfDeadNote, null, null);
                return;
            }
            if (t.id == "ironwood")
            {
                W.ironwood.have += 1;
                RefreshHud();
                SayOne("You", Lines.YouIronwood);
                return;
            }
            if (t.id == "cairn")
            {
                if (Logic.ReclaimAxe(W))
                {
                    RefreshHud();
                    SayOne("You", Lines.YouReclaim);
                }
                return;
            }
            if (t.id == "blaze")
            {
                SayOne("You", Lines.Blaze[t.bi % Lines.Blaze.Length]);
            }
            if (t.id == "guide")
            {
                SayOne("You", Lines.GuideEast);
            }
            if (t.id == "waystone")
            {
                SayOne("You", Lines.WaystoneRune);
            }
            if (t.id == "forage")
            {
                foraged.Add(t.bi);
                Logic.Forage(W);
                RefreshHud();
                Hud.SetPrompt("Berries gathered - press R to eat.", 1.6f);
            }
        }

        private Circle? NearestTree(float maxD)
        {
            var p = Player.EyeXZ();
            var f = Player.Facing();
            Circle? best = null;
            float bestD = maxD;
            foreach (var c in world.colliders)
            {
                if (c.r > 0.8f || c.kind != SurfaceKind.Wood) continue;
                float dx = c.x - p.x;
                float dz = c.z - p.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bestD && d > 0.5f)
                {
                    float dot = (dx * f.x + dz * f.z) / (d == 0f ? 1f : d);
                    if (dot > 0.5f) { bestD = d; best = c; }
                }
            }
            return best;
        }

        // Nearest big collider (r > 0.8, the complement of the tree
        // set) whose rim sits inside reach and the facing cone. Hut
        // walls, mill walls, split rock, waystone, mountains.
        private Circle? NearestSurface(float maxD)
        {
            var p = Player.EyeXZ();
            var f = Player.Facing();
            Circle? best = null;
            float bestD = maxD;
            foreach (var c in world.colliders)
            {
                if (c.r <= 0.8f && c.kind == SurfaceKind.Wood) continue;
                float edge = SurfaceEdge(p.x, p.z, f.x, f.z, c, bestD);
                if (edge >= 0f) { bestD = edge; best = c; }
            }
            return best;
        }

        // Shudder angle as a pure function of elapsed t: a decaying
        // wobble, silent at t=0 and gone past dur. EditMode-tested.
        public static float ShudderAngle(float t, float dur, float mag)
        {
            if (t <= 0f || t >= dur) return 0f;
            return mag * (1f - t / dur) * Mathf.Sin(t * 43f);
        }

        // Mote flight step: ballistic with linear drag. EditMode-tested.
        public static void MoteStep(ref Vector3 pos, ref Vector3 vel, float dt, float gravity)
        {
            vel.y -= gravity * dt;
            vel *= 1f - Mathf.Min(0.9f, 1.8f * dt);
            pos += vel * dt;
        }

        // Landing response per kind, pure: chips bounce damped, blood
        // sticks, sparks snuff out. Returns false when the mote dies.
        // EditMode-tested.
        public static bool MoteLand(MoteKind kind, ref Vector3 vel)
        {
            if (kind == MoteKind.Spark) return false;
            if (kind == MoteKind.Blood) vel = Vector3.zero;
            else { vel *= 0.4f; vel.y = 0f; }
            return true;
        }

        // Struck surface -> burst kind. Stone and metal both throw
        // sparks; only wood bursts chips. EditMode-tested.
        public static MoteKind MoteKindFor(SurfaceKind surface)
        {
            return surface == SurfaceKind.Wood ? MoteKind.Chip : MoteKind.Spark;
        }

        // Closest rim point of a big collider to the eye, pure. When the
        // eye sits on the center the rim point leads with the facing.
        // EditMode-tested.
        public static void SurfaceRim(float px, float pz, float fx, float fz, Circle c, out float qx, out float qz)
        {
            float dx = px - c.x;
            float dz = pz - c.z;
            float dc = Mathf.Sqrt(dx * dx + dz * dz);
            if (dc <= 0.001f) { qx = c.x + c.r * fx; qz = c.z + c.r * fz; return; }
            qx = c.x + dx / dc * c.r;
            qz = c.z + dz / dc * c.r;
        }

        // Look-line meets disc, pure: nearest rim point along the
        // facing ray within maxT. Facing must be unit length. Falls
        // back to the far exit when the eye starts inside.
        // EditMode-tested.
        public static bool RayDisc(float px, float pz, float fx, float fz, float cx, float cz, float r, float maxT, out float qx, out float qz)
        {
            qx = 0f;
            qz = 0f;
            float ox = px - cx;
            float oz = pz - cz;
            float b = ox * fx + oz * fz;
            float c = ox * ox + oz * oz - r * r;
            float disc = b * b - c;
            if (disc < 0f) return false;
            float t = -b - Mathf.Sqrt(disc);
            if (t < 0f) t = -b + Mathf.Sqrt(disc);
            if (t < 0f || t > maxT) return false;
            qx = px + fx * t;
            qz = pz + fz * t;
            return true;
        }

        // Edge distance from the eye to a big collider's rim, gated on
        // the rim point sitting inside the facing cone: -1 when out of
        // reach or behind. Big rocks read by their edge, not their
        // center — a mountain's heart is never in axe reach.
        // EditMode-tested.
        public static float SurfaceEdge(float px, float pz, float fx, float fz, Circle c, float reach)
        {
            float dx = px - c.x;
            float dz = pz - c.z;
            float edge = Mathf.Sqrt(dx * dx + dz * dz) - c.r;
            if (edge > reach) return -1f;
            SurfaceRim(px, pz, fx, fz, c, out float qx, out float qz);
            float rx = qx - px;
            float rz = qz - pz;
            float rd = Mathf.Sqrt(rx * rx + rz * rz);
            if (rd < 0.001f) return Mathf.Max(0f, edge);
            if ((rx * fx + rz * fz) / rd < 0.5f) return -1f;
            return Mathf.Max(0f, edge);
        }

        private void BurstMotes(float ox, float oy, float oz, Vector3 away, MoteKind kind, int count)
        {
            if (motes.Count > 48) return;
            if (moteRoot == null)
            {
                var g = new GameObject("Motes");
                g.transform.SetParent(world.root.transform, false);
                g.transform.localPosition = Vector3.zero;
                moteRoot = g.transform;
            }
            if (away.sqrMagnitude < 0.01f)
            {
                var f = Player.Facing();
                away = new Vector3(f.x, 0f, f.z);
            }
            away.Normalize();
            for (int i = 0; i < count; i++)
            {
                GameObject go;
                float sp, up, life, grav;
                var spin = Vector3.zero;
                if (kind == MoteKind.Chip)
                {
                    float s = 0.06f + UnityEngine.Random.value * 0.05f;
                    uint col = i % 2 == 0 ? 0x7a5230U : 0xd9a75eU;
                    go = MeshUtil.Box(moteRoot, "Chip", s, s * 0.7f, s, col, ox, oy, oz);
                    sp = 2f + UnityEngine.Random.value * 2.5f;
                    up = 2.5f + UnityEngine.Random.value * 2f;
                    life = Tuning.ChipLifeSecs;
                    grav = Tuning.ChipGravity;
                    spin = new Vector3(
                        (UnityEngine.Random.value - 0.5f) * 720f,
                        (UnityEngine.Random.value - 0.5f) * 720f,
                        (UnityEngine.Random.value - 0.5f) * 720f);
                }
                else if (kind == MoteKind.Blood)
                {
                    float r = 0.04f + UnityEngine.Random.value * 0.035f;
                    go = MeshUtil.Ball(moteRoot, "Blood", r, 0xbc1e1e, ox, oy, oz, false);
                    sp = 2f + UnityEngine.Random.value * 2.5f;
                    up = 2.5f + UnityEngine.Random.value * 1.5f;
                    life = 1.2f;
                    grav = 16f;
                }
                else
                {
                    float r = 0.015f + UnityEngine.Random.value * 0.015f;
                    uint col = i % 2 == 0 ? 0xffd34dU : 0xff7a2aU;
                    go = MeshUtil.Ball(moteRoot, "Spark", r, col, ox, oy, oz, true);
                    sp = 3f + UnityEngine.Random.value * 3.5f;
                    up = 1.5f + UnityEngine.Random.value * 1.5f;
                    life = 0.45f;
                    grav = 9f;
                }
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var vel = away * sp + new Vector3(
                    (UnityEngine.Random.value - 0.5f) * 2.4f,
                    up,
                    (UnityEngine.Random.value - 0.5f) * 2.4f);
                motes.Add(new Mote { go = go, vel = vel, life = life, grav = grav, spin = spin, baseScale = go.transform.localScale, kind = kind });
            }
        }

        // Near-face point: target center pulled toward the eye by the
        // body's half-width, so blood starts at hide, not heart. Never
        // overshoots past the eye. EditMode-tested.
        public static void NearPoint(float tx, float tz, float px, float pz, float pull, out float qx, out float qz)
        {
            float dx = px - tx;
            float dz = pz - tz;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d <= 0.001f) { qx = tx; qz = tz; return; }
            float k = Mathf.Min(pull, d) / d;
            qx = tx + dx * k;
            qz = tz + dz * k;
        }

        // Corpse watch, pure: a fresh kill stays keeled over for
        // CorpseSecs, then the remains clear. EditMode-tested.
        public static bool ShouldHideCorpse(bool alive, bool gone, float deadT, float corpseSecs)
        {
            return !alive && !gone && deadT >= corpseSecs;
        }

        private Vector3 EyePos()
        {
            if (Game.MainCam != null) return Game.MainCam.transform.position;
            var p = Player.EyeXZ();
            return new Vector3(p.x, PlayerController.EyeHeight, p.z);
        }

        // Aim point on a collider: where the look-line meets the
        // shrunken disc (visuals run ~0.7 of their fat movement radii),
        // else the closest rim point when facing past the edge. Aiming
        // at the closest rim stages bursts beside the player on big
        // rocks and grazes past thin trunks.
        private void AimPoint(Circle c, float px, float pz, float fx, float fz, out float qx, out float qz)
        {
            if (RayDisc(px, pz, fx, fz, c.x, c.z, c.r * 0.7f, Tuning.AxeReach + c.r, out qx, out qz)) return;
            SurfaceRim(px, pz, fx, fz, c, out qx, out qz);
        }

        // Confirmation ray: from the eye toward the aim point; the
        // first face along that line is where the eye sees the axe
        // land. Own axe and dirt never count, and a hit far past the
        // mark is the forest behind, not the target — the fallback
        // stands in whenever the ray finds nothing sensible. The burst
        // sits a breath back off the face so motes never spawn
        // embedded at grazing angles.
        private Vector3 StrikeOrigin(Vector3 aim, Vector3 fall)
        {
            var cam = Game.MainCam;
            if (cam == null) return fall;
            Vector3 from = cam.transform.position;
            Vector3 dir = aim - from;
            float wantD = dir.magnitude;
            if (wantD < 0.01f) return fall;
            dir /= wantD;
            RaycastHit best = default(RaycastHit);
            bool found = false;
            foreach (var h in Physics.RaycastAll(from, dir, wantD + 2.5f))
            {
                if (h.distance < 1f) continue;
                if (h.normal.y > 0.6f) continue;
                if (!found || h.distance < best.distance) { best = h; found = true; }
            }
            if (!found) return fall;
            if ((best.point - aim).magnitude > 3f) return fall;
            return best.point - dir * 0.12f + best.normal * 0.06f;
        }

        private void BurstBlood(float x, float z, int count, float height)
        {
            float gy = WorldBuilder.GroundH(x, z);
            var p = Player.EyeXZ();
            NearPoint(x, z, p.x, p.z, 0.35f, out float qx, out float qz);
            Vector3 o = StrikeOrigin(new Vector3(x, gy + height, z), new Vector3(qx, gy + height, qz));
            var eye = EyePos();
            BurstMotes(o.x, o.y, o.z, new Vector3(o.x - eye.x, 0f, o.z - eye.z), MoteKind.Blood, count);
        }

        private void ShakeTree(Circle c)
        {
            var tr = TreeAt(c.x, c.z);
            if (tr == null) return;
            foreach (var sh in treeShakes) if (sh.tr == tr) return;
            treeShakes.Add(new TreeShake { tr = tr, t = 0f, baseRot = tr.localRotation });
        }

        private Transform TreeAt(float x, float z)
        {
            Transform best = null;
            float bestD = 1.2f;
            var forest = world.root.transform.Find("Forest");
            if (forest != null)
            {
                for (int i = 0; i < forest.childCount; i++)
                {
                    var ch = forest.GetChild(i);
                    if (ch.name != "Tree") continue;
                    float d = Mathf.Sqrt((ch.localPosition.x - x) * (ch.localPosition.x - x) + (ch.localPosition.z - z) * (ch.localPosition.z - z));
                    if (d < bestD) { bestD = d; best = ch; }
                }
            }
            for (int i = 0; i < world.root.transform.childCount; i++)
            {
                var ch = world.root.transform.GetChild(i);
                if (ch.name != "LandmarkPine") continue;
                float d = Mathf.Sqrt((ch.localPosition.x - x) * (ch.localPosition.x - x) + (ch.localPosition.z - z) * (ch.localPosition.z - z));
                if (d < bestD) { bestD = d; best = ch; }
            }
            return best;
        }

        private void TickMotes(float dt)
        {
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var c = motes[i];
                c.life -= dt;
                if (c.life <= 0f || c.go == null)
                {
                    if (c.go != null) Destroy(c.go);
                    motes.RemoveAt(i);
                    continue;
                }
                var pos = c.go.transform.localPosition;
                MoteStep(ref pos, ref c.vel, dt, c.grav);
                float gy = WorldBuilder.GroundH(pos.x, pos.z) + 0.03f;
                if (pos.y < gy)
                {
                    pos.y = gy;
                    if (!MoteLand(c.kind, ref c.vel)) { Destroy(c.go); motes.RemoveAt(i); continue; }
                }
                c.go.transform.localPosition = pos;
                c.go.transform.localScale = c.baseScale * FadeScale(c.life);
                c.go.transform.Rotate(c.spin.x * dt, c.spin.y * dt, c.spin.z * dt);
            }
        }

        // End-of-life shrink, pure: full size until the last 0.2s,
        // then linear to nothing so motes wink out instead of popping.
        // EditMode-tested.
        public static float FadeScale(float life)
        {
            if (life <= 0f) return 0f;
            if (life >= 0.2f) return 1f;
            return life / 0.2f;
        }

        private void TickTreeShakes(float dt)
        {
            for (int i = treeShakes.Count - 1; i >= 0; i--)
            {
                var sh = treeShakes[i];
                if (sh.tr == null) { treeShakes.RemoveAt(i); continue; }
                sh.t += dt;
                if (sh.t >= Tuning.TreeShudderSecs)
                {
                    sh.tr.localRotation = sh.baseRot;
                    treeShakes.RemoveAt(i);
                    continue;
                }
                float ax = ShudderAngle(sh.t, Tuning.TreeShudderSecs, Tuning.TreeShudderMag);
                float az = ShudderAngle(sh.t + 0.13f, Tuning.TreeShudderSecs, Tuning.TreeShudderMag);
                sh.tr.localRotation = sh.baseRot * Quaternion.Euler(ax * 57.29578f, 0f, az * 57.29578f);
            }
        }

        private void ClearMotes()
        {
            foreach (var c in motes) if (c.go != null) Destroy(c.go);
            motes.Clear();
            if (moteRoot != null) { Destroy(moteRoot.gameObject); moteRoot = null; }
            foreach (var sh in treeShakes) if (sh.tr != null) sh.tr.localRotation = sh.baseRot;
            treeShakes.Clear();
            hitStop = 0f;
        }

        private bool Ahead(float x, float z, float range)
        {
            var p = Player.EyeXZ();
            var f = Player.Facing();
            float dx = x - p.x;
            float dz = z - p.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d > range) return false;
            return (dx * f.x + dz * f.z) / (d == 0f ? 1f : d) > 0.4f;
        }

        public void TryAttack()
        {
            if (!W.axe.have)
            {
                Hud.SetPrompt(Lines.PromptNoAxe, 1.6f);
                return;
            }
            if (!Player.TrySwing(W.skill.swingMs * 0.8f)) return;
            audio.PlayWhoosh();
            WolfUnit target = FindWolfTarget();
            if (target != null)
            {
                // Impact lands at animation contact, not on click: the
                // swing commits now, the axe connects ~1/3s later.
                pendingWolf = target;
                pendingTree = null;
                pendingSurface = null;
                pendingDwarfKill = false;
                swingWaiting = true;
                return;
            }
            var ds = world.spots.dwarf;
            if (W.dwarfAlive && Ahead(ds.x, ds.z, Tuning.AxeReach + 0.6f))
            {
                if (W.trust == Trust.Sharp)
                {
                    SayOne("Dwarf", Lines.DwarfWrist);
                    return;
                }
                if (W.trust == Trust.Ended)
                {
                    pendingWolf = null;
                    pendingTree = null;
                    pendingSurface = null;
                    pendingDwarfKill = true;
                    swingWaiting = true;
                    return;
                }
                var res = Logic.DrawNearDwarf(W);
                if (res == "warned") SayOne("Dwarf", Lines.DwarfWarnDraw);
                else if (res == Trust.Ended)
                {
                    SayOne("Dwarf", Lines.DwarfRefusedDone);
                    Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelRefused);
                }
                RefreshHud();
                return;
            }
            if (!W.dwarfAlive) return;
            var tree = NearestTree(Tuning.AxeReach);
            if (tree.HasValue)
            {
                pendingWolf = null;
                pendingTree = tree;
                pendingSurface = null;
                pendingDwarfKill = false;
                swingWaiting = true;
                return;
            }
            var surf = NearestSurface(Tuning.AxeReach);
            if (surf.HasValue)
            {
                pendingWolf = null;
                pendingTree = null;
                pendingSurface = surf;
                pendingDwarfKill = false;
                swingWaiting = true;
                return;
            }
            audio.PlayThunk(0.05f);
        }

        // Contact resolution: the axe arrives (SwingK >= ContactK) and the
        // committed swing connects. Range re-validated — facing committed
        // at click, so turning mid-swing can't cancel, but walking out of
        // reach can.
        private void ResolveSwing()
        {
            swingWaiting = false;
            var p = Player.EyeXZ();
            var wolf = pendingWolf;
            var tree = pendingTree;
            var surface = pendingSurface;
            bool dwarfKill = pendingDwarfKill;
            pendingWolf = null;
            pendingTree = null;
            pendingSurface = null;
            pendingDwarfKill = false;
            if (W == null || !W.player.alive) return;
            if (wolf != null)
            {
                if (!wolf.brain.alive || Gdist(p.x, p.z, wolf.brain.pos.x, wolf.brain.pos.z) > Tuning.AxeReach) return;
                audio.PlayHit();
                audio.PlayYelp(Tuning.YelpDelaySecs);
                Hud.FlashCross();
                hitStop = Tuning.HitStopSecs;
                Player.Kick(Tuning.HitShake);
                wolf.hp -= 1;
                wolf.fx.hitT = Tuning.WolfHitSecs;
                wolf.fx.flashT = Tuning.WolfFlashSecs;
                wolf.fx.flashed = true;
                float wdx = wolf.brain.pos.x - p.x;
                float wdz = wolf.brain.pos.z - p.z;
                float wd = Mathf.Sqrt(wdx * wdx + wdz * wdz);
                if (wd == 0f) wd = 1f;
                wolf.fx.hitDir = new Vec2(wdx / wd, wdz / wd);
                wolf.fx.knockT = Tuning.WolfHitSecs;
                wolf.fx.knockDir = new Vec2(wdx / wd, wdz / wd);
                wolfCooldown = Mathf.Max(wolfCooldown, Tuning.WolfStaggerSecs);
                if (wolf.hp <= 0)
                {
                    wolf.brain.alive = false;
                    Logic.UseAxe(W, "hit");
                    Logic.LootWolf(W);
                    RefreshHud();
                    Hud.SetPrompt(Lines.PromptWolfStill, 2.2f);
                    hitStop = Tuning.KillStopSecs;
                    Player.Kick(Tuning.KillShake);
                    audio.PlayKill();
                    BurstBlood(wolf.brain.pos.x, wolf.brain.pos.z, 20, 0.75f);
                    Hud.FlashCross(Tuning.KillFlashSecs);
                }
                else
                {
                    wolf.flinchT = Tuning.WolfFlinchSecs;
                    BurstBlood(wolf.brain.pos.x, wolf.brain.pos.z, 12, 0.75f);
                    Logic.UseAxe(W, "chop");
                    RefreshHud();
                    Hud.SetPrompt(Lines.PromptWolfSnarls, 1.6f);
                }
                return;
            }
            if (dwarfKill)
            {
                var ds = world.spots.dwarf;
                if (W.dwarfAlive && W.trust == Trust.Ended && Gdist(p.x, p.z, ds.x, ds.z) <= Tuning.AxeReach + 0.6f)
                {
                    if (Logic.AttackDwarf(W))
                    {
                        audio.PlayHit();
                        BurstBlood(ds.x, ds.z, 12, 0.9f);
                        RefreshHud();
                        Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelDwarfDead);
                        Hud.ShowModal(Lines.ModalForgeCold, Lines.DwarfDeadNote, null, null);
                    }
                }
                return;
            }
            if (tree.HasValue)
            {
                if (Gdist(p.x, p.z, tree.Value.x, tree.Value.z) > Tuning.AxeReach) return;
                Logic.UseAxe(W, "chop");
                audio.PlayThunk(W.axe.dullness / 100f);
                Hud.FlashCross();
                hitStop = Tuning.ChopStopSecs;
                Player.Kick(Tuning.ChopShake);
                var f = Player.Facing();
                SurfaceRim(p.x, p.z, f.x, f.z, tree.Value, out float qx, out float qz);
                float qy = WorldBuilder.GroundH(qx, qz);
                AimPoint(tree.Value, p.x, p.z, f.x, f.z, out float ax, out float az);
                Vector3 o = StrikeOrigin(new Vector3(ax, qy + 1.3f, az), new Vector3(qx, qy + 1.3f, qz));
                var eye = EyePos();
                BurstMotes(o.x, o.y, o.z, new Vector3(o.x - eye.x, 0f, o.z - eye.z), MoteKind.Chip, 7);
                ShakeTree(tree.Value);
                RefreshHud();
                Hud.SetPrompt(Lines.PromptChips, 1.6f);
            }
            if (surface.HasValue)
            {
                var c = surface.Value;
                var f = Player.Facing();
                if (SurfaceEdge(p.x, p.z, f.x, f.z, c, Tuning.AxeReach) < 0f) return;
                SurfaceRim(p.x, p.z, f.x, f.z, c, out float qx, out float qz);
                float qy = WorldBuilder.GroundH(qx, qz);
                AimPoint(c, p.x, p.z, f.x, f.z, out float ax, out float az);
                var eye = EyePos();
                Logic.UseAxe(W, "chop");
                RefreshHud();
                if (MoteKindFor(c.kind) == MoteKind.Chip)
                {
                    audio.PlayThunk(W.axe.dullness / 100f);
                    Vector3 o = StrikeOrigin(new Vector3(ax, qy + 1.3f, az), new Vector3(qx, qy + 1.3f, qz));
                    BurstMotes(o.x, o.y, o.z, new Vector3(o.x - eye.x, 0f, o.z - eye.z), MoteKind.Chip, 7);
                    Hud.SetPrompt(Lines.PromptChips, 1.6f);
                }
                else
                {
                    audio.PlayClang(c.kind == SurfaceKind.Metal ? 0.85f : 0.25f);
                    Vector3 o = StrikeOrigin(new Vector3(ax, qy + 1.2f, az), new Vector3(qx, qy + 1.2f, qz));
                    BurstMotes(o.x, o.y, o.z, new Vector3(eye.x - o.x, 0f, eye.z - o.z), MoteKind.Spark, 10);
                    Hud.SetPrompt(Lines.PromptSparks, 1.6f);
                }
                Hud.FlashCross();
                hitStop = Tuning.ChopStopSecs;
                Player.Kick(Tuning.ChopShake);
            }
        }

        private void ClearSwing()
        {
            swingWaiting = false;
            pendingWolf = null;
            pendingTree = null;
            pendingSurface = null;
            pendingDwarfKill = false;
        }

        // Nearest live wolf inside the axe cone. Shared by the swing
        // commit and the crosshair hot-state so both agree on "in reach".
        private WolfUnit FindWolfTarget()
        {
            var p = Player.EyeXZ();
            WolfUnit target = null;
            float targetD = float.MaxValue;
            foreach (var u in Wolves)
            {
                if (!u.brain.alive) continue;
                if (!Ahead(u.brain.pos.x, u.brain.pos.z, Tuning.AxeReach)) continue;
                float d = Gdist(p.x, p.z, u.brain.pos.x, u.brain.pos.z);
                if (d < targetD) { targetD = d; target = u; }
            }
            return target;
        }

        private void Die()
        {
            ClearSwing();
            var p = Player.EyeXZ();
            audio.PlayDeath();
            audio.StopWind();
            UnlockCursor();
            Hud.ShowHud(false);
            Hud.HideDialogue();
            dialogue = null;
            Hud.ShowDeath(
                Lines.DeathCopyPrefix + (W.deaths + 1) + ".",
                W.dwarfAlive ? "" : Lines.DwarfDeadNote);
            pendingDeathPos = new DropPos { x = p.x, z = p.z };
        }

        public void DebugDamage(int n)
        {
            if (W == null) return;
            if (Logic.DamagePlayer(W, n)) Die();
        }

        public void CloseBookPublic()
        {
            bookOpen = false;
            Hud.CloseBook();
        }

        public void ToTitle()
        {
            ClearSwing();
            if (W != null) Autosave(Lines.LabelGenPrefix + W.generation);
            CurrentState = State.Title;
            dialogue = null;
            bookOpen = false;
            UnlockCursor();
            audio.StopWind();
            Hud.CloseAllScreens();
            Hud.ShowHud(false);
            Hud.RenderSlots((s) => saver.Describe(s), (s, info) => PickSlotWith(s, info));
            Hud.SetTitleMessage("");
            Hud.ShowTitle(true);
        }

        // Esc toggle: back to the live session in place. Unlike Continue,
        // this never respawns or resets anything mid-run. Refuses when no
        // living session exists (boot title).
        public void Resume()
        {
            if (W == null || !W.player.alive) return;
            audio.PlayClick();
            CurrentState = State.Play;
            Hud.CloseAllScreens();
            RefreshHud();
            Hud.ShowHud(true);
            audio.StartWind();
            LockCursor();
            Hud.ShowFirstLock(true);
        }

        public void HandleTitleEscape()
        {
            if (Hud.IsModalOpen()) { audio.PlayClick(); Hud.CloseModal(); }
            else Resume();
        }

        // Esc from live play lands here, not the title: the sim freezes
        // (Paused() covers any non-Play state) and the overlay offers
        // resume, sensitivity, and quit-to-title. No autosave: pause is
        // transient, quitting to title saves.
        public void Pause()
        {
            audio.PlayClick();
            CurrentState = State.Paused;
            UnlockCursor();
            audio.StopWind();
            Hud.CloseAllScreens();
            Hud.ShowHud(false);
            Hud.ShowPause(true);
        }

        public void HandlePlayEscape()
        {
            if (bookOpen) { bookOpen = false; Hud.CloseBook(); }
            else if (dialogue == null && !Hud.IsModalOpen() && !Hud.IsDeathOpen()) Pause();
        }

        private void LoadLookSens()
        {
            float v = PlayerPrefs.GetFloat(LookSensPrefKey, PlayerController.DefaultLookSens);
            PlayerController.LookSens = Mathf.Clamp(v, PlayerController.LookSensMin, PlayerController.LookSensMax);
            Hud.SetLookSens(PlayerController.LookSens);
        }

        private void SetLookSens(float v)
        {
            PlayerController.LookSens = Mathf.Clamp(v, PlayerController.LookSensMin, PlayerController.LookSensMax);
            PlayerPrefs.SetFloat(LookSensPrefKey, PlayerController.LookSens);
            PlayerPrefs.Save();
        }

        private void LoadVolume()
        {
            audio.MasterGain = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
            Hud.SetVolume(audio.MasterGain);
            audio.Muted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
            Hud.SetMuted(audio.Muted);
        }

        public void SetVolume(float v)
        {
            audio.MasterGain = v;
            PlayerPrefs.SetFloat(VolumePrefKey, audio.MasterGain);
            PlayerPrefs.Save();
        }

        public void ToggleMute()
        {
            audio.Muted = !audio.Muted;
            PlayerPrefs.SetInt(MutePrefKey, audio.Muted ? 1 : 0);
            PlayerPrefs.Save();
            Hud.SetMuted(audio.Muted);
        }

        public void Wake()
        {
            ClearSwing();
            Hud.CloseDeath();
            Logic.Die(W, pendingDeathPos ?? new DropPos { x = 0f, z = 42f });
            pendingDeathPos = null;
            Player.Respawn(new Vector3(world.spots.spawn.x, 0f, world.spots.spawn.z));
            ResetPack();
            wolfCooldown = 0f;
            ClearMotes();
            lostT = 0f;
            howlT = 12f;
            visitedYard = false;
            visitedMouth = false;
            Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelNobody);
            RefreshHud();
            Hud.ShowHud(true);
            audio.StartWind();
            Hud.ShowFirstLock(true);
        }

        private void Export()
        {
            audio.PlayClick();
            var raw = saver.Load(slot);
            if (raw == null)
            {
                Hud.SetTitleMessage(Lines.ExportEmpty);
                return;
            }
            string path = Path.Combine(Application.persistentDataPath, "axe-path-slot-" + slot + "-export.json");
            File.WriteAllText(path, raw);
            Hud.SetTitleMessage(Lines.ExportDonePrefix + path);
        }

        // Tabbing away pauses instead of freezing mid-frame: with
        // runInBackground the loop keeps ticking, so focus loss opens
        // the pause menu the moment the window goes inactive.
        void OnApplicationFocus(bool focus)
        {
            if (Logic.ShouldAutoPause(CurrentState == State.Play, focus)) Pause();
        }

        void Update()
        {
            if (CurrentState == State.Title)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) HandleTitleEscape();
                return;
            }
            if (CurrentState == State.Paused)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Resume();
                return;
            }
            if (CurrentState != State.Play) return;
            if (Input.GetKeyDown(KeyCode.Escape)) HandlePlayEscape();
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (dialogue != null) AdvanceDialogue();
                else if (bookOpen) { bookOpen = false; Hud.CloseBook(); }
                else if (IsLocked()) TryInteract();
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (bookOpen) { audio.PlayClick(); bookOpen = false; Hud.CloseBook(); }
                else if (dialogue != null) AdvanceDialogue();
                else if (IsLocked() && !OverlaysOpen()) TryAttack();
            }
            if (Input.GetKeyDown(KeyCode.F) && W != null)
            {
                if (Logic.EatMeat(W)) { audio.PlayClick(); RefreshHud(); Hud.SetPrompt(Lines.PromptAte, 1.6f); Autosave(Lines.LabelGenPrefix + W.generation); }
            }
            if (Input.GetKeyDown(KeyCode.R) && W != null)
            {
                if (Logic.EatBerries(W)) { audio.PlayClick(); RefreshHud(); Hud.SetPrompt(Lines.PromptAte, 1.6f); Autosave(Lines.LabelGenPrefix + W.generation); }
            }
            if (IsLocked())
            {
                float dx = Input.GetAxis("Mouse X");
                float dy = Input.GetAxis("Mouse Y");
                if (dx != 0f || dy != 0f) Player.Look(dx, dy);
            }
            if (dialogue != null)
            {
                var dp = Player.EyeXZ();
                if (Gdist(dp.x, dp.z, dialogue.anchorX, dialogue.anchorZ) > 4.5f)
                {
                    dialogue = null;
                    Hud.HideDialogue();
                }
            }
            if (Paused())
            {
                Hud.ShowFirstLock(!IsLocked() && !OverlaysOpen() && !Hud.IsDeathOpen());
                // Lock-pause (unfocused window) must not strand an axe
                // mid-swing: settle the swing and let its contact land.
                // Wolves, time, and movement stay frozen.
                if (CurrentState == State.Play && !bookOpen && !Hud.IsModalOpen() && !Hud.IsDeathOpen() && W != null && W.player.alive)
                {
                    Player.AdvanceSwing(Mathf.Min(0.05f, Time.deltaTime));
                    if (swingWaiting && Player.SwingK >= PlayerController.ContactK) ResolveSwing();
                }
                return;
            }
            Hud.ShowFirstLock(false);
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            playT += dt;
            Player.Tick(dt);
            // Hit-stop: your motion keeps recovering (swing cadence stays
            // pinned) while the world holds its breath for a few frames.
            if (hitStop > 0f)
            {
                hitStop = Mathf.Max(0f, hitStop - dt);
                return;
            }
            var p = Player.EyeXZ();
            var spots = world.spots;
            if (!visitedYard && Gdist(p.x, p.z, spots.yard.x, spots.yard.z) < 6f)
            {
                visitedYard = true;
                Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelYard);
            }
            if (!visitedMouth && Gdist(p.x, p.z, spots.caveMouth.x, spots.caveMouth.z) < 6f)
            {
                visitedMouth = true;
                Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelCave);
            }
            bool inside = p.x > -220f && p.x < 220f && p.z > -380f && p.z < 240f;
            if (!inside)
            {
                lostT += dt;
                if (lostT > 150f)
                {
                    lostT = 0f;
                    Player.Teleport(0f, 210f, 0f);
                    if (W.axe.have)
                    {
                        W.axe.have = false;
                        W.axe.droppedAt = new DropPos { x = 7f, z = 210f };
                    }
                    Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelStumbled);
                    RefreshHud();
                    Hud.SetPrompt(Lines.PromptStumbled, 3.0f);
                }
            }
            else lostT = 0f;

            wolfCooldown = Mathf.Max(0f, wolfCooldown - dt);
            snapCooldown = Mathf.Max(0f, snapCooldown - dt);
            // The pack shares one attack cooldown: three wolves cornering
            // you still wound at the lone-wolf rate.
            foreach (var u in Wolves)
            {
                var wres = Logic.WolfStep(u.brain, p.x, p.z, Player.running, W.skill.noiseMul * Player.NoiseMul, dt, null);
                foreach (var ev in wres.events)
                {
                    if (ev == "growl")
                    {
                        audio.PlayGrowl();
                        Hud.SetPrompt(Lines.PromptGrowl, 2.5f);
                    }
                    else if (ev == "chase") audio.PlayChase();
                    else if (ev == "windup") audio.PlaySnarl();
                    else if (ev == "whiff") audio.PlaySnap();
                    else if (ev == "attack" && wolfCooldown <= 0f)
                    {
                        wolfCooldown = 1.2f;
                        if (!Logic.WolfHitsPlayer(Player.AirY, Tuning.WolfLungeHeight))
                        {
                            audio.PlaySnap();
                            Hud.SetPrompt(Lines.PromptDodged, 1.6f);
                        }
                        else if (Logic.BlockStopsBite(Player.blocking, Player.stamina))
                        {
                            Player.DrainBlock();
                            audio.PlayClang(0.3f);
                            Player.Kick(Tuning.ChopShake);
                            Hud.SetPrompt(Lines.PromptBlocked, 1.6f);
                            RefreshHud();
                        }
                        else
                        {
                            audio.PlayHurt();
                            Hud.HurtFlash();
                            hitStop = Tuning.HurtStopSecs;
                            Player.Kick(Tuning.HurtShake);
                            if (Logic.DamagePlayer(W, 1))
                            {
                                Die();
                                return;
                            }
                            RefreshHud();
                        }
                    }
                    else if (ev == "attack" && snapCooldown <= 0f)
                    {
                        // Gated lunge (staggered or pack cooldown): the teeth
                        // still snap, so the crouch never reads as a broken
                        // hit. Shared cooldown keeps the pack from chattering.
                        snapCooldown = 0.3f;
                        audio.PlaySnap();
                    }
                }
                u.flinchT = Mathf.Max(0f, u.flinchT - dt);
                if (wres.hasMove && u.flinchT <= 0f)
                {
                    float sp = u.brain.state == "chase" ? Tuning.WolfChaseSpeed : 2.2f;
                    u.brain.pos.x += wres.move.x * sp * dt;
                    u.brain.pos.z += wres.move.z * sp * dt;
                }
            }
            // Pack law: a chase drags nearby denmates into the hunt.
            foreach (var chaser in Wolves)
            {
                if (!chaser.brain.alive || chaser.brain.state != "chase") continue;
                foreach (var mate in Wolves)
                {
                    if (mate == chaser || !mate.brain.alive) continue;
                    float dd = Gdist(chaser.brain.pos.x, chaser.brain.pos.z, mate.brain.pos.x, mate.brain.pos.z);
                    if (Logic.PackMateAlerts(mate.brain.state, dd)) { mate.brain.state = "warn"; mate.brain.warnT = 0.7f; }
                }
            }
            Hud.SetStamina(Player.stamina / Tuning.StaminaMax, Player.sneaking, Player.blocking);
            musicT -= dt;
            if (musicT <= 0f) { musicT = 0.5f; audio.TickMusic(0.5f, p.z < -260f); }

            howlT -= dt;
            if (howlT <= 0f)
            {
                howlT = UnityEngine.Random.Range(18f, 30f);
                if (dialogue == null)
                {
                    float nearest = float.MaxValue;
                    foreach (var u in Wolves)
                    {
                        if (!u.brain.alive) continue;
                        float d = Gdist(p.x, p.z, u.brain.pos.x, u.brain.pos.z);
                        if (d < nearest) nearest = d;
                    }
                    if (nearest > 40f && nearest < 300f)
                    {
                        audio.PlayHowl(1f - (nearest - 40f) / 260f);
                        Hud.SetPrompt(Lines.PromptHowl, 2.5f);
                    }
                }
            }

            promptT -= dt;
            if (promptT <= 0f && dialogue == null && !Hud.PromptHeld())
            {
                promptT = 0.2f;
                var t = InteractTarget();
                Hud.SetPrompt(t != null ? t.label : "", 0f);
            }

            for (int i = 0; i < Wolves.Count; i++)
            {
                var u = Wolves[i];
                u.view.Tick(dt, playT, Player.transform.position);
                if (ShouldHideCorpse(u.brain.alive, u.fx.corpseGone, u.fx.deadT, Tuning.CorpseSecs))
                {
                    u.fx.corpseGone = true;
                    actors.wolfPack[i].SetActive(false);
                }
            }
            npcView.Tick(dt, playT, Player.transform.position, W.dwarfAlive);
            TickMotes(dt);
            TickTreeShakes(dt);
            if (swingWaiting && Player.SwingK >= PlayerController.ContactK) ResolveSwing();
            Hud.SetCrossHot(!OverlaysOpen() && (FindWolfTarget() != null || NearestTree(Tuning.AxeReach).HasValue || NearestSurface(Tuning.AxeReach).HasValue));
        }
    }
}
