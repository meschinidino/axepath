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

    public class GameSession : MonoBehaviour
    {
        public enum State { Title, Play }

        public State CurrentState { get; private set; }
        public WorldState W { get; private set; }
        public WolfState Wolf { get; private set; }
        public WolfFx WolfFx { get; private set; }
        public int WolfHp { get; private set; }
        public PlayerController Player { get; private set; }
        public Hud Hud { get; private set; }

        private GameWorld world;
        private ActorRefs actors;
        private AudioSynth audio;
        private WolfView wolfView;
        private NpcView npcView;
        private Saver saver;
        private float wolfCooldown;
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

        public void StartSession(GameWorld world, ActorRefs actors, PlayerController p, Hud h, AudioSynth audio, WolfView wolfView, NpcView npcView)
        {
            this.world = world;
            this.actors = actors;
            Player = p;
            Hud = h;
            this.audio = audio;
            this.wolfView = wolfView;
            this.npcView = npcView;
            saver = new Saver(new FileBackend());
            Wolf = Logic.CreateWolf(world.spots.den.x, world.spots.den.z);
            WolfFx = new WolfFx();
            WolfHp = 2;
            wolfView.Bind(actors.wolf, actors.wolfEyes, actors.wolfFurMats, Wolf, WolfFx);
            npcView.Bind(actors.dwarf, actors.librarian);
            Player.OnStep += () => audio.PlayStep(Player.running);
            Hud.OnWake += () => { audio.PlayClick(); Wake(); };
            Hud.OnBookClose += () => { audio.PlayClick(); bookOpen = false; Hud.CloseBook(); };
            Hud.OnExport += () => Export();
            Hud.OnFirstLock += () => { audio.PlayClick(); Hud.ShowFirstLock(false); LockCursor(); };
            CurrentState = State.Title;
            Hud.RenderSlots((s) => saver.Describe(s), (s, info) => PickSlotWith(s, info));
            Hud.ShowTitle(true);
        }

        private void PickSlotWith(int s, SlotDesc info)
        {
            audio.PlayClick();
            if (info.state == "filled")
            {
                var parsed = Logic.ParseSave(saver.Load(s));
                if (!parsed.ok)
                {
                    Hud.ShowModal("Broken save", parsed.error + " Starting fresh will overwrite it.", () => NewGame(s), null);
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
            saver.Save(s, Logic.SerializeWorld(W, "Gen 1, nobody"));
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
            Wolf = Logic.CreateWolf(world.spots.den.x, world.spots.den.z);
            WolfFx = new WolfFx();
            wolfView.Rebind(Wolf, WolfFx);
            WolfHp = 2;
            wolfCooldown = 0f;
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

        private bool IsLocked() { return Cursor.lockState == CursorLockMode.Locked; }

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
            Hud.SetBanner(saver.sessionOnly ? "Progress will NOT be saved — storage refused. This run counts for fun only." : "");
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
            actors.wolf.SetActive(Wolf.alive);
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
                cands.Add(new InteractTarget { id = "axe", x = refs.axeStandPos.x, z = refs.axeStandPos.z, label = "Take the lumber axe (E)" });
            }
            if (!W.book.have)
            {
                cands.Add(new InteractTarget { id = "bookstand", x = refs.bookStandPos.x, z = refs.bookStandPos.z, label = "Take the route sketch (E)" });
            }
            else
            {
                cands.Add(new InteractTarget { id = "readbook", x = refs.bookStandPos.x, z = refs.bookStandPos.z, label = "Read the sketch (E)" });
            }
            cands.Add(new InteractTarget { id = "librarian", x = spots.librarian.x, z = spots.librarian.z, label = "Talk to the librarian (E)" });
            if (W.dwarfAlive)
            {
                cands.Add(new InteractTarget { id = "dwarf", x = spots.dwarf.x, z = spots.dwarf.z, label = "Talk to the dwarf (E)" });
            }
            else
            {
                cands.Add(new InteractTarget { id = "dwarfnote", x = spots.dwarf.x, z = spots.dwarf.z, label = "Look at the cold forge (E)" });
            }
            if (!W.ironwood.turnedIn && W.ironwood.have < 1)
            {
                cands.Add(new InteractTarget { id = "ironwood", x = refs.ironLogPos.x, z = refs.ironLogPos.z, label = "Take ironwood (E)", reach = 5f });
            }
            if (W.axe.droppedAt != null)
            {
                cands.Add(new InteractTarget { id = "cairn", x = W.axe.droppedAt.x, z = W.axe.droppedAt.z, label = "Reclaim your axe (E)" });
            }
            for (int i = 0; i < refs.blazePos.Count; i++)
            {
                cands.Add(new InteractTarget { id = "blaze", bi = i, x = refs.blazePos[i].x, z = refs.blazePos[i].z, label = "Look at the mark (E)" });
            }
            cands.Add(new InteractTarget { id = "guide", x = refs.guidePos.x, z = refs.guidePos.z, label = "Look at the weathered mark (E)" });
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
                    Autosave("Gen " + W.generation + ", axe taken");
                    Say("You", new List<string> {
                        "Heavier than it looks. The yard boss will miss it; the forest will test it.",
                        "Yours now, and already in your hand. Click to swing — the trees will teach your arms.",
                    }, null);
                }
                return;
            }
            if (t.id == "bookstand" || t.id == "readbook")
            {
                Logic.TakeBook(W);
                RefreshHud();
                bookOpen = true;
                Hud.ShowBook("Route sketch", Lines.BookSketch);
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
                    Autosave("Gen " + W.generation + ", civil");
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
                            Autosave("Gen " + W.generation + ", sharpened");
                            SayOne("Dwarf", Lines.DwarfSharpening);
                        }
                    }
                    else SayOne("Dwarf", Lines.DwarfCivil);
                }
                else if (res.evt == "idle-sharp") SayOne("Dwarf", Lines.DwarfSharpening);
                else if (res.evt == "refused") SayOne("Dwarf", "The dwarf turns his shoulder. The forge is closed to you.");
                RefreshHud();
                return;
            }
            if (t.id == "dwarfnote")
            {
                Hud.ShowModal("Cold forge", Lines.DwarfDeadNote, null, null);
                return;
            }
            if (t.id == "ironwood")
            {
                W.ironwood.have += 1;
                RefreshHud();
                SayOne("You", "Pale as bone, and it hums faintly. One log is all he asked for.");
                return;
            }
            if (t.id == "cairn")
            {
                if (Logic.ReclaimAxe(W))
                {
                    RefreshHud();
                    SayOne("You", "Still here. Still yours.");
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
                Hud.SetPrompt("No axe. The lumber yard has one.", 1.6f);
                return;
            }
            if (!Player.TrySwing(W.skill.swingMs * 0.8f)) return;
            var p = Player.EyeXZ();
            if (Wolf.alive && Ahead(Wolf.pos.x, Wolf.pos.z, Tuning.AxeReach))
            {
                audio.PlayHit();
                audio.PlayYelp();
                Hud.FlashCross();
                WolfHp -= 1;
                WolfFx.hitT = Tuning.WolfHitSecs;
                WolfFx.flashT = Tuning.WolfFlashSecs;
                WolfFx.flashed = true;
                float wdx = Wolf.pos.x - p.x;
                float wdz = Wolf.pos.z - p.z;
                float wd = Mathf.Sqrt(wdx * wdx + wdz * wdz);
                if (wd == 0f) wd = 1f;
                WolfFx.hitDir = new Vec2(wdx / wd, wdz / wd);
                WolfFx.knockT = Tuning.WolfHitSecs;
                WolfFx.knockDir = new Vec2(wdx / wd, wdz / wd);
                wolfCooldown = Mathf.Max(wolfCooldown, Tuning.WolfStaggerSecs);
                if (WolfHp <= 0)
                {
                    Wolf.alive = false;
                    Logic.UseAxe(W, "hit");
                    RefreshHud();
                    Hud.SetPrompt("The wolf is still. The forest feels larger.", 2.2f);
                }
                else
                {
                    Logic.UseAxe(W, "chop");
                    RefreshHud();
                    Hud.SetPrompt("The wolf snarls, wounded.", 1.6f);
                }
                return;
            }
            var ds = world.spots.dwarf;
            if (W.dwarfAlive && Ahead(ds.x, ds.z, Tuning.AxeReach + 0.6f))
            {
                if (W.trust == Trust.Sharp)
                {
                    SayOne("Dwarf", "The dwarf catches your wrist. “Not like this. Not ever.”");
                    return;
                }
                if (W.trust == Trust.Ended)
                {
                    if (Logic.AttackDwarf(W))
                    {
                        audio.PlayHit();
                        RefreshHud();
                        Autosave("Gen " + W.generation + ", dwarf dead");
                        Hud.ShowModal("The forge goes cold", Lines.DwarfDeadNote, null, null);
                    }
                    return;
                }
                var res = Logic.DrawNearDwarf(W);
                if (res == "warned") SayOne("Dwarf", Lines.DwarfWarnDraw);
                else if (res == Trust.Ended)
                {
                    SayOne("Dwarf", "“Done.” The dwarf turns his shoulder. The forge is closed to you.");
                    Autosave("Gen " + W.generation + ", refused");
                }
                RefreshHud();
                return;
            }
            if (!W.dwarfAlive) return;
            if (NearestTree(Tuning.AxeReach).HasValue)
            {
                Logic.UseAxe(W, "chop");
                audio.PlayThunk(W.axe.dullness / 100f);
                Hud.FlashCross();
                RefreshHud();
                Hud.SetPrompt("Chips fly.", 1.6f);
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
                "Three wounds. The forest keeps you. Your axe lies where you fell, under a cairn of stones. Death " + (W.deaths + 1) + ".",
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
            if (W != null) Autosave("Gen " + W.generation);
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

        public void Wake()
        {
            Hud.CloseDeath();
            Logic.Die(W, pendingDeathPos ?? new DropPos { x = 0f, z = 42f });
            pendingDeathPos = null;
            Player.Respawn(new Vector3(world.spots.spawn.x, 0f, world.spots.spawn.z));
            Wolf = Logic.CreateWolf(world.spots.den.x, world.spots.den.z);
            WolfFx = new WolfFx();
            wolfView.Rebind(Wolf, WolfFx);
            WolfHp = 2;
            wolfCooldown = 0f;
            lostT = 0f;
            howlT = 12f;
            visitedYard = false;
            visitedMouth = false;
            Autosave("Gen " + W.generation + ", nobody");
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
                Hud.SetTitleMessage("Slot is empty — nothing to export.");
                return;
            }
            string path = Path.Combine(Application.persistentDataPath, "axe-path-slot-" + slot + "-export.json");
            File.WriteAllText(path, raw);
            Hud.SetTitleMessage("Save exported to " + path);
        }

        void Update()
        {
            if (CurrentState != State.Play) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (bookOpen) { bookOpen = false; Hud.CloseBook(); }
                else if (dialogue == null && !Hud.IsModalOpen() && !Hud.IsDeathOpen()) ToTitle();
            }
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
            var cf = Player.Facing(); Hud.ShowCompass(cf.x, cf.z);
            var p = Player.EyeXZ();
            var spots = world.spots;
            if (!visitedYard && Gdist(p.x, p.z, spots.yard.x, spots.yard.z) < 6f)
            {
                visitedYard = true;
                Autosave("Gen " + W.generation + ", yard");
            }
            if (!visitedMouth && Gdist(p.x, p.z, spots.caveMouth.x, spots.caveMouth.z) < 6f)
            {
                visitedMouth = true;
                Autosave("Gen " + W.generation + ", cave");
            }
            bool inside = p.x > -30f && p.x < 30f && p.z > -70f && p.z < 45f;
            if (!inside)
            {
                lostT += dt;
                if (lostT > 150f)
                {
                    lostT = 0f;
                    Player.Teleport(0f, 42f, 0f);
                    if (W.axe.have)
                    {
                        W.axe.have = false;
                        W.axe.droppedAt = new DropPos { x = 1.5f, z = 42f };
                    }
                    Autosave("Gen " + W.generation + ", stumbled out");
                    RefreshHud();
                    Hud.SetPrompt("The dark thickens... you stumble out at the village edge, empty-handed.", 3.0f);
                }
            }
            else lostT = 0f;

            wolfCooldown = Mathf.Max(0f, wolfCooldown - dt);
            var wres = Logic.WolfStep(Wolf, p.x, p.z, Player.running, W.skill.noiseMul, dt, null);
            foreach (var ev in wres.events)
            {
                if (ev == "growl")
                {
                    audio.PlayGrowl();
                    Hud.SetPrompt("A growl in the dark. Walk, do not run.", 2.5f);
                }
                else if (ev == "chase") audio.PlayChase();
                else if (ev == "attack" && wolfCooldown <= 0f)
                {
                    wolfCooldown = 1.2f;
                    audio.PlayHit();
                    Hud.HurtFlash();
                    if (Logic.DamagePlayer(W, 1))
                    {
                        Die();
                        return;
                    }
                    RefreshHud();
                }
            }
            if (wres.hasMove)
            {
                float sp = Wolf.state == "chase" ? Tuning.WolfChaseSpeed : 2.2f;
                Wolf.pos.x += wres.move.x * sp * dt;
                Wolf.pos.z += wres.move.z * sp * dt;
            }

            howlT -= dt;
            if (howlT <= 0f)
            {
                howlT = UnityEngine.Random.Range(18f, 30f);
                if (Wolf.alive && dialogue == null)
                {
                    float dDen = Gdist(p.x, p.z, spots.den.x, spots.den.z);
                    if (dDen > 15f && dDen < 60f)
                    {
                        audio.PlayHowl(1f - (dDen - 15f) / 45f);
                        Hud.SetPrompt("A howl rolls through the trees.", 2.5f);
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

            wolfView.Tick(dt, playT, Player.transform.position);
            npcView.Tick(dt, playT, Player.transform.position, W.dwarfAlive);
        }
    }
}
