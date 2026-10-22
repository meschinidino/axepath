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
    }

    public class TreeShake
    {
        public Transform tr;
        public float t;
        public Quaternion baseRot;
    }

    public class Chip
    {
        public GameObject go;
        public Vector3 vel;
        public float life;
        public Vector3 spin;
    }

    public class GameSession : MonoBehaviour
    {
        public enum State { Title, Play, Paused }

        public const string LookSensPrefKey = "axepath.looksens";

        public State CurrentState { get; private set; }
        public WorldState W { get; private set; }
        public TuningSet Tuning { get; private set; }
        public List<WolfUnit> Wolves { get; private set; }
        public PlayerController Player { get; private set; }
        public Hud Hud { get; private set; }

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
        private readonly List<TreeShake> treeShakes = new List<TreeShake>();
        private readonly List<Chip> chips = new List<Chip>();
        private Transform chipRoot;
        public int ChipCount { get { return chips.Count; } }
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
            Tuning = LoadTuning();
            Player.Tuning = Tuning;
            Wolves = new List<WolfUnit>();
            for (int i = 0; i < world.spots.wolfHomes.Count; i++)
            {
                var home = world.spots.wolfHomes[i];
                var unit = new WolfUnit
                {
                    brain = Logic.CreateWolf(home.x, home.z),
                    fx = new WolfFx(),
                    hp = Tuning.wolfHp,
                    view = wolfViews[i],
                };
                Wolves.Add(unit);
                wolfViews[i].Bind(actors.wolfPack[i], actors.wolfPackEyes[i], actors.wolfPackFurMats[i], unit.brain, unit.fx);
                wolfViews[i].Tuning = Tuning;
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
            CurrentState = State.Title;
            Hud.RenderSlots((s) => saver.Describe(s), (s, info) => PickSlotWith(s, info));
            Hud.ShowTitle(true);
        }

        // Balance comes from tuning.json (Resources), edited as data.
        // A missing or unreadable asset degrades to default balance.
        private static TuningSet LoadTuning()
        {
            try
            {
                var asset = Resources.Load<TextAsset>("tuning");
                if (asset == null || string.IsNullOrEmpty(asset.text)) return TuningSet.Default();
                return TuningSet.FromJson(asset.text);
            }
            catch
            {
                return TuningSet.Default();
            }
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
            W = Logic.CreateWorld(Tuning);
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
            slot = s;
            CurrentState = State.Play;
            Player.Respawn(new Vector3(world.spots.spawn.x, 0f, world.spots.spawn.z));
            ResetPack();
            wolfCooldown = 0f;
            ClearChopFx();
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
                actors.wolfPack[i].SetActive(Wolves[i].brain.alive);
            }
        }

        private void ResetPack()
        {
            for (int i = 0; i < Wolves.Count; i++)
            {
                var home = world.spots.wolfHomes[i];
                Wolves[i].brain = Logic.CreateWolf(home.x, home.z);
                Wolves[i].fx = new WolfFx();
                Wolves[i].hp = Tuning.wolfHp;
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
                var res = Logic.TalkDwarf(W, Tuning);
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
                        if (Logic.TurnInIronwood(W, Tuning))
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
        }

        private Circle? NearestTree(float maxD)
        {
            var p = Player.EyeXZ();
            var f = Player.Facing();
            Circle? best = null;
            float bestD = maxD;
            foreach (var c in world.colliders)
            {
                if (c.r > 0.8f) continue;
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

        // Shudder angle as a pure function of elapsed t: a decaying
        // wobble, silent at t=0 and gone past dur. EditMode-tested.
        public static float ShudderAngle(float t, float dur, float mag)
        {
            if (t <= 0f || t >= dur) return 0f;
            return mag * (1f - t / dur) * Mathf.Sin(t * 43f);
        }

        // Chip flight step: ballistic with linear drag. EditMode-tested.
        public static void ChipStep(ref Vector3 pos, ref Vector3 vel, float dt, float gravity)
        {
            vel.y -= gravity * dt;
            vel *= 1f - Mathf.Min(0.9f, 1.8f * dt);
            pos += vel * dt;
        }

        private void BurstChips(Circle c, Vec2 from)
        {
            if (chips.Count > 48) return;
            if (chipRoot == null)
            {
                var g = new GameObject("Chips");
                g.transform.SetParent(world.root.transform, false);
                g.transform.localPosition = Vector3.zero;
                chipRoot = g.transform;
            }
            float gy = WorldBuilder.GroundH(c.x, c.z);
            var away = new Vector3(c.x - from.x, 0f, c.z - from.z);
            if (away.sqrMagnitude < 0.01f)
            {
                var f = Player.Facing();
                away = new Vector3(f.x, 0f, f.z);
            }
            away.Normalize();
            for (int i = 0; i < 5; i++)
            {
                float s = 0.05f + UnityEngine.Random.value * 0.05f;
                var go = MeshUtil.Box(chipRoot, "Chip", s, s * 0.7f, s, 0x7a5230, c.x, gy + 1.3f, c.z);
                Destroy(go.GetComponent<Collider>());
                float sp = 2f + UnityEngine.Random.value * 2.5f;
                var vel = away * sp + new Vector3(
                    (UnityEngine.Random.value - 0.5f) * 2.4f,
                    2.5f + UnityEngine.Random.value * 2f,
                    (UnityEngine.Random.value - 0.5f) * 2.4f);
                var spin = new Vector3(
                    (UnityEngine.Random.value - 0.5f) * 720f,
                    (UnityEngine.Random.value - 0.5f) * 720f,
                    (UnityEngine.Random.value - 0.5f) * 720f);
                chips.Add(new Chip { go = go, vel = vel, life = Tuning.chipLifeSecs, spin = spin });
            }
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

        private void TickChips(float dt)
        {
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                var c = chips[i];
                c.life -= dt;
                if (c.life <= 0f || c.go == null)
                {
                    if (c.go != null) Destroy(c.go);
                    chips.RemoveAt(i);
                    continue;
                }
                var pos = c.go.transform.localPosition;
                ChipStep(ref pos, ref c.vel, dt, Tuning.chipGravity);
                float gy = WorldBuilder.GroundH(pos.x, pos.z) + 0.03f;
                if (pos.y < gy) { pos.y = gy; c.vel *= 0.4f; c.vel.y = 0f; }
                c.go.transform.localPosition = pos;
                c.go.transform.Rotate(c.spin.x * dt, c.spin.y * dt, c.spin.z * dt);
            }
        }

        private void TickTreeShakes(float dt)
        {
            for (int i = treeShakes.Count - 1; i >= 0; i--)
            {
                var sh = treeShakes[i];
                if (sh.tr == null) { treeShakes.RemoveAt(i); continue; }
                sh.t += dt;
                if (sh.t >= Tuning.treeShudderSecs)
                {
                    sh.tr.localRotation = sh.baseRot;
                    treeShakes.RemoveAt(i);
                    continue;
                }
                float ax = ShudderAngle(sh.t, Tuning.treeShudderSecs, Tuning.treeShudderMag);
                float az = ShudderAngle(sh.t + 0.13f, Tuning.treeShudderSecs, Tuning.treeShudderMag);
                sh.tr.localRotation = sh.baseRot * Quaternion.Euler(ax * 57.29578f, 0f, az * 57.29578f);
            }
        }

        private void ClearChopFx()
        {
            foreach (var c in chips) if (c.go != null) Destroy(c.go);
            chips.Clear();
            if (chipRoot != null) { Destroy(chipRoot.gameObject); chipRoot = null; }
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
            var p = Player.EyeXZ();
            WolfUnit target = null;
            float targetD = float.MaxValue;
            foreach (var u in Wolves)
            {
                if (!u.brain.alive) continue;
                if (!Ahead(u.brain.pos.x, u.brain.pos.z, Tuning.axeReach)) continue;
                float d = Gdist(p.x, p.z, u.brain.pos.x, u.brain.pos.z);
                if (d < targetD) { targetD = d; target = u; }
            }
            if (target != null)
            {
                audio.PlayHit();
                audio.PlayYelp();
                Hud.FlashCross();
                hitStop = Tuning.hitStopSecs;
                Player.Kick(Tuning.hitShake);
                target.hp -= 1;
                target.fx.hitT = Tuning.wolfHitSecs;
                target.fx.flashT = Tuning.wolfFlashSecs;
                target.fx.flashed = true;
                float wdx = target.brain.pos.x - p.x;
                float wdz = target.brain.pos.z - p.z;
                float wd = Mathf.Sqrt(wdx * wdx + wdz * wdz);
                if (wd == 0f) wd = 1f;
                target.fx.hitDir = new Vec2(wdx / wd, wdz / wd);
                target.fx.knockT = Tuning.wolfHitSecs;
                target.fx.knockDir = new Vec2(wdx / wd, wdz / wd);
                wolfCooldown = Mathf.Max(wolfCooldown, Tuning.wolfStaggerSecs);
                if (target.hp <= 0)
                {
                    target.brain.alive = false;
                    Logic.UseAxe(W, "hit", Tuning);
                    RefreshHud();
                    Hud.SetPrompt(Lines.PromptWolfStill, 2.2f);
                    hitStop = Tuning.killStopSecs;
                    Player.Kick(Tuning.killShake);
                    audio.PlayKill();
                    Hud.FlashCross(Tuning.killFlashSecs);
                }
                else
                {
                    Logic.UseAxe(W, "chop", Tuning);
                    RefreshHud();
                    Hud.SetPrompt(Lines.PromptWolfSnarls, 1.6f);
                }
                return;
            }
            var ds = world.spots.dwarf;
            if (W.dwarfAlive && Ahead(ds.x, ds.z, Tuning.axeReach + 0.6f))
            {
                if (W.trust == Trust.Sharp)
                {
                    SayOne("Dwarf", Lines.DwarfWrist);
                    return;
                }
                if (W.trust == Trust.Ended)
                {
                    if (Logic.AttackDwarf(W))
                    {
                        audio.PlayHit();
                        RefreshHud();
                        Autosave(Lines.LabelGenPrefix + W.generation + Lines.LabelDwarfDead);
                        Hud.ShowModal(Lines.ModalForgeCold, Lines.DwarfDeadNote, null, null);
                    }
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
            var tree = NearestTree(Tuning.axeReach);
            if (tree.HasValue)
            {
                Logic.UseAxe(W, "chop", Tuning);
                audio.PlayThunk(W.axe.dullness / 100f);
                Hud.FlashCross();
                hitStop = Tuning.chopStopSecs;
                Player.Kick(Tuning.chopShake);
                BurstChips(tree.Value, p);
                ShakeTree(tree.Value);
                RefreshHud();
                Hud.SetPrompt(Lines.PromptChips, 1.6f);
                return;
            }
            audio.PlayThunk(0.05f);
        }

        private void Die()
        {
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

        public void Wake()
        {
            Hud.CloseDeath();
            Logic.Die(W, pendingDeathPos ?? new DropPos { x = 0f, z = 42f }, Tuning);
            pendingDeathPos = null;
            Player.Respawn(new Vector3(world.spots.spawn.x, 0f, world.spots.spawn.z));
            ResetPack();
            wolfCooldown = 0f;
            ClearChopFx();
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
                var wres = Logic.WolfStep(u.brain, p.x, p.z, Player.running, W.skill.noiseMul, dt, Tuning);
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
                        audio.PlayHurt();
                        Hud.HurtFlash();
                        hitStop = Tuning.hurtStopSecs;
                        Player.Kick(Tuning.hurtShake);
                        if (Logic.DamagePlayer(W, 1))
                        {
                            Die();
                            return;
                        }
                        RefreshHud();
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
                if (wres.hasMove)
                {
                    float sp = u.brain.state == "chase" ? Tuning.wolfChaseSpeed : 2.2f;
                    u.brain.pos.x += wres.move.x * sp * dt;
                    u.brain.pos.z += wres.move.z * sp * dt;
                }
            }

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

            foreach (var u in Wolves) u.view.Tick(dt, playT, Player.transform.position);
            npcView.Tick(dt, playT, Player.transform.position, W.dwarfAlive);
            TickChips(dt);
            TickTreeShakes(dt);
        }
    }
}
