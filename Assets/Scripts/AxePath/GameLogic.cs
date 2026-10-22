// Axe Path — pure game logic. Zero Unity dependencies; covered by EditMode tests.
// Faithful C# port of the proven three.js slice logic. Public fields stay
// lowercase on purpose so save JSON keeps the exact slice shape.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AxePath
{
    public static class Meta
    {
        public const int SaveVersion = 1;
        public const int SaveSlots = 3;
    }

    public static class Tuning
    {
        public const float WolfHearWalk = 30f;
        public const float WolfHearRun = 90f;
        public const float WolfWarnSecs = 3f;
        public const float WolfLeash = 150f;
        public const int PlayerHp = 3;
        public const int WolfHp = 2;
        public const float WolfChaseSpeed = 4.2f;
        public const float WolfAttackRange = 1.7f;
        public const float WolfWindupSecs = 0.45f;
        public const float WolfLungeRange = 2.6f;
        public const float ChopStopSecs = 0.045f;
        public const float ChopShake = 0.35f;
        public const float TreeShudderSecs = 0.4f;
        public const float TreeShudderMag = 0.045f;
        public const float ChipGravity = 14f;
        public const float ChipLifeSecs = 1.1f;
        public const float AxeReach = 2.6f;
        public const float WolfHitSecs = 0.35f;
        public const float WolfFlashSecs = 0.22f;
        public const float WolfKnockSpeed = 3.2f;
        public const float WolfStaggerSecs = 0.9f;
        public const float HitStopSecs = 0.06f;
        public const float KillStopSecs = 0.12f;
        public const float HurtStopSecs = 0.05f;
        public const float HitShake = 0.55f;
        public const float KillShake = 0.85f;
        public const float HurtShake = 0.7f;
        public const float ShakeMaxRad = 0.05f;
        public const float TraumaDecaySecs = 0.4f;
        public const float KillFlashSecs = 0.3f;
        public const float DullPerChop = 9f;
        public const float DullPerHit = 6f;
        public const float DullThresholdCivil = 40f;
        public const int EdgeSteps = 3;
        public const float SwingBaseMs = 650f;
        public const float SwingStepMs = 60f;
        public const float SwingMinMs = 350f;
        public const float NoiseBase = 1f;
        public const float NoiseStep = 0.08f;
        public const float NoiseMin = 0.6f;
        public const int ChopsPerStep = 4;
        public const int KillsPerStep = 1;
        public const int IronwoodLogsNeeded = 1;
        public const float SharpenHoldMul = 0.2f;
    }

    public static class Trust
    {
        public const string Cold = "cold";
        public const string Civil = "civil";
        public const string Sharp = "sharpening";
        public const string Ended = "ended";
    }

    public static class Lines
    {
        public const string LibrarianRoute = "Three pines. Split rock. The waystone's rune. Then follow the smoke. Take the sketch, the book stays with me.";
        public const string LibrarianReplay = "Pines, rock, waystone, smoke. The sketch is in your hands. Go carefully.";
        public const string BookSketch = "THREE PINES where the village lets go.\nSPLIT ROCK astride the path — pass it close.\nTHE WAYSTONE'S RUNE, halfway up, gold in the dark.\nSMOKE over the treeline marks the cave.\n\nOf the mountain smith, young and welcoming to all who climb with honest need. — S.";
        public const string DwarfCold = "State your business. Quickly.";
        public const string DwarfArrival = "...That edge. You walked here. Hm. Perhaps you are not entirely soft.";
        public const string DwarfCivil = "Ironwood. Past my cave to the east, where the pale trunks grow. Bring one log and we will see.";
        public const string DwarfSharpening = "Give it here. ... A working edge, earned. Keep it.";
        public const string DwarfWarnDraw = "Raise that again, little climber, and we are done.";
        public const string DwarfStranger = "You wear a dead climber’s boots. ...Speak.";
        public const string DwarfDeadNote = "The forge is cold. Whatever the dwarf knew about the axe is gone with him.";
        public static readonly string[] Blaze = new string[]
        {
            "Axe-mark, gray with weather, cut chest-high facing the climb. Someone came this way before you and wanted the next one to know it.",
            "Fresh chips scattered under an old mark. More than one climber has passed here. Few, perhaps, came back.",
            "Axe-mark facing the walker, not the wood. Follow the marks north and the smoke will find you.",
        };
        public const string GuideEast = "The mark faces east, not north, with three short strokes cut beneath it. Whoever left it wanted the next climber to turn aside here.";
        public const string WaystoneRune = "A single rune cut deep and laid with gold, halfway up the stone. Beneath it, tally-marks count the climbers who came this way — most of them crossed through.";

        // HUD chrome (Hud.cs). Byte-identical centralization; no rewording.
        public const string HudHealth = "health";
        public const string HudNoAxe = "no axe";
        public const string HudEdge = "edge";
        public const string HudHandAxe = "axe";
        public const string HudHandBook = "sketch";
        public const string HudHandLog = "log";
        public const string TitleHead = "AXE PATH";
        public const string TitleSub = "You are no one. The forest does not care. Walk anyway.";
        public const string TitleExport = "Export current slot";
        public const string TitleControls = "WASD / arrows — move · Mouse — look · Shift — run · E — talk, take, read · Click — swing, advance";
        public const string BookClose = "Close (E)";
        public const string ModalOk = "OK";
        public const string ModalCancel = "Cancel";
        public const string DeathHead = "THE FOREST KEEPS YOU";
        public const string DeathWake = "Wake as the next nobody";
        public const string PauseHead = "PAUSED";
        public const string PauseResume = "Resume (Esc)";
        public const string PauseSensitivity = "Mouse sensitivity";
        public const string PauseQuit = "Save and quit to title";
        public const string LockHint = "Click to look around";
        public const string SlotPrefix = "Slot ";
        public const string SlotEmpty = " — empty, begin as a nobody";
        public const string SlotMid = " — ";
        public const string SlotDateSep = " · ";
        public const string SlotBroken = " — broken save, pick to overwrite";

        // Session copy (GameSession.cs). Near-duplicates stay separate consts.
        public const string PromptAxe = "Take the lumber axe (E)";
        public const string PromptBookTake = "Take the route sketch (E)";
        public const string PromptBookRead = "Read the sketch (E)";
        public const string PromptLibrarian = "Talk to the librarian (E)";
        public const string PromptDwarf = "Talk to the dwarf (E)";
        public const string PromptDwarfNote = "Look at the cold forge (E)";
        public const string PromptIronwood = "Take ironwood (E)";
        public const string PromptCairn = "Reclaim your axe (E)";
        public const string PromptBlaze = "Look at the mark (E)";
        public const string PromptGuide = "Look at the weathered mark (E)";
        public const string PromptWaystone = "Read the rune (E)";
        public const string YouAxeHeavy = "Heavier than it looks. The yard boss will miss it; the forest will test it.";
        public const string YouAxeYours = "Yours now, and already in your hand. Click to swing — the trees will teach your arms.";
        public const string BookTitle = "Route sketch";
        public const string DwarfRefused = "The dwarf turns his shoulder. The forge is closed to you.";
        public const string DwarfRefusedDone = "“Done.” The dwarf turns his shoulder. The forge is closed to you.";
        public const string ModalColdForge = "Cold forge";
        public const string ModalForgeCold = "The forge goes cold";
        public const string YouIronwood = "Pale as bone, and it hums faintly. One log is all he asked for.";
        public const string YouReclaim = "Still here. Still yours.";
        public const string PromptNoAxe = "No axe. The lumber yard has one.";
        public const string PromptWolfStill = "The wolf is still. The forest feels larger.";
        public const string PromptWolfSnarls = "The wolf snarls, wounded.";
        public const string PromptChips = "Chips fly.";
        public const string DwarfWrist = "The dwarf catches your wrist. “Not like this. Not ever.”";
        public const string DeathCopyPrefix = "Three wounds. The forest keeps you. Your axe lies where you fell, under a cairn of stones. Death ";
        public const string BannerNoSave = "Progress will NOT be saved — storage refused. This run counts for fun only.";
        public const string ModalBrokenSave = "Broken save";
        public const string ModalBrokenSuffix = " Starting fresh will overwrite it.";
        public const string ExportEmpty = "Slot is empty — nothing to export.";
        public const string ExportDonePrefix = "Save exported to ";
        public const string PromptStumbled = "The dark thickens... you stumble out at the village edge, empty-handed.";
        public const string PromptGrowl = "A growl in the dark. Walk, do not run.";
        public const string PromptHowl = "A howl rolls through the trees.";

        // Save slot labels. LabelGenPrefix is the one shared "Gen " const.
        public const string LabelGen1Nobody = "Gen 1, nobody";
        public const string LabelGenPrefix = "Gen ";
        public const string LabelAxeTaken = ", axe taken";
        public const string LabelCivil = ", civil";
        public const string LabelSharpened = ", sharpened";
        public const string LabelDwarfDead = ", dwarf dead";
        public const string LabelRefused = ", refused";
        public const string LabelNobody = ", nobody";
        public const string LabelYard = ", yard";
        public const string LabelCave = ", cave";
        public const string LabelStumbled = ", stumbled out";

        // Save parse errors. The a/broken near-dup cluster stays separate.
        public const string SaveErrUnreadable = "That file is not a readable save.";
        public const string SaveErrForeign = "That file belongs to another game.";
        public const string SaveErrVersionPrefix = "That save was written by version ";
        public const string SaveErrVersionMid = ", this game reads version ";
        public const string SaveErrNoWorld = "That save is missing its world.";
        public const string SaveErrGeneration = "That save has a broken generation.";
        public const string SaveErrDeaths = "That save has a broken deaths.";
        public const string SaveErrDwarf = "That save has a broken dwarf.";
        public const string SaveErrTrust = "That save has broken trust.";
        public const string SaveErrAxe = "That save has a broken axe.";
        public const string SaveErrSkill = "That save has broken skill.";
        public const string SaveErrBody = "That save has a broken body.";
        public const string SaveErrIronwood = "That save has broken ironwood.";
        public const string SaveErrBook = "That save has a broken book.";
    }

    // Every tunable as data: balance lives in tuning.json, not in code.
    // Field initializers are the canonical defaults (== Tuning consts);
    // FromJson merges a (possibly partial) JSON doc over those defaults,
    // so a corrupt or missing file degrades to default balance, never
    // to a crash or a zeroed-out world. Lowercase fields keep the JSON
    // shape stable, same convention as the save format.
    public class TuningSet
    {
        public float wolfHearWalk = Tuning.WolfHearWalk;
        public float wolfHearRun = Tuning.WolfHearRun;
        public float wolfWarnSecs = Tuning.WolfWarnSecs;
        public float wolfLeash = Tuning.WolfLeash;
        public int wolfHp = Tuning.WolfHp;
        public float wolfChaseSpeed = Tuning.WolfChaseSpeed;
        public float wolfAttackRange = Tuning.WolfAttackRange;
        public float wolfWindupSecs = Tuning.WolfWindupSecs;
        public float wolfLungeRange = Tuning.WolfLungeRange;
        public float wolfHitSecs = Tuning.WolfHitSecs;
        public float wolfFlashSecs = Tuning.WolfFlashSecs;
        public float wolfKnockSpeed = Tuning.WolfKnockSpeed;
        public float wolfStaggerSecs = Tuning.WolfStaggerSecs;
        public float chopStopSecs = Tuning.ChopStopSecs;
        public float chopShake = Tuning.ChopShake;
        public float treeShudderSecs = Tuning.TreeShudderSecs;
        public float treeShudderMag = Tuning.TreeShudderMag;
        public float chipGravity = Tuning.ChipGravity;
        public float chipLifeSecs = Tuning.ChipLifeSecs;
        public float axeReach = Tuning.AxeReach;
        public float hitStopSecs = Tuning.HitStopSecs;
        public float killStopSecs = Tuning.KillStopSecs;
        public float hurtStopSecs = Tuning.HurtStopSecs;
        public float hitShake = Tuning.HitShake;
        public float killShake = Tuning.KillShake;
        public float hurtShake = Tuning.HurtShake;
        public float shakeMaxRad = Tuning.ShakeMaxRad;
        public float traumaDecaySecs = Tuning.TraumaDecaySecs;
        public float killFlashSecs = Tuning.KillFlashSecs;
        public float dullPerChop = Tuning.DullPerChop;
        public float dullPerHit = Tuning.DullPerHit;
        public float dullThresholdCivil = Tuning.DullThresholdCivil;
        public float swingBaseMs = Tuning.SwingBaseMs;
        public float swingStepMs = Tuning.SwingStepMs;
        public float swingMinMs = Tuning.SwingMinMs;
        public float noiseBase = Tuning.NoiseBase;
        public float noiseStep = Tuning.NoiseStep;
        public float noiseMin = Tuning.NoiseMin;
        public int chopsPerStep = Tuning.ChopsPerStep;
        public int killsPerStep = Tuning.KillsPerStep;
        public int ironwoodLogsNeeded = Tuning.IronwoodLogsNeeded;
        public float sharpenHoldMul = Tuning.SharpenHoldMul;
        public int playerHp = Tuning.PlayerHp;
        public static TuningSet Default() { return new TuningSet(); }
        public string ToJson() { return JsonConvert.SerializeObject(this, Formatting.Indented); }
        public static TuningSet FromJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return TuningSet.Default();
            var t = TuningSet.Default();
            try { JsonConvert.PopulateObject(text, t); }
            catch { return TuningSet.Default(); }
            return t;
        }
    }

    [Serializable]
    public struct Vec2
    {
        public float x;
        public float z;
        public Vec2(float x, float z) { this.x = x; this.z = z; }
    }

    [Serializable]
    public class DropPos
    {
        public float x;
        public float z;
    }

    [Serializable]
    public class AxeState
    {
        public bool have;
        public bool yard;
        public float dullness;
        public bool sharpened;
        public DropPos droppedAt;
    }

    [Serializable]
    public class SkillState
    {
        public int chops;
        public int kills;
        public float swingMs;
        public float noiseMul;
    }

    [Serializable]
    public class PlayerState
    {
        public int hp;
        public bool alive;
    }

    [Serializable]
    public class IronwoodState
    {
        public int have;
        public bool turnedIn;
    }

    [Serializable]
    public class BookState
    {
        public bool have;
        public bool read;
    }

    [Serializable]
    public class FlagsState
    {
        public bool metDwarf;
        public bool metLibrarian;
    }

    [Serializable]
    public class WorldState
    {
        public int version;
        public int generation;
        public int deaths;
        public bool dwarfAlive;
        public string trust;
        public bool warnedDraw;
        public AxeState axe;
        public SkillState skill;
        public PlayerState player;
        public IronwoodState ironwood;
        public BookState book;
        public FlagsState flags;
    }

    public class TalkResult
    {
        public string line;
        public string evt;
    }

    public class SaveResult
    {
        public bool ok;
        public string error;
        public WorldState world;
        public string label;
        public string savedAt;
    }

    public class WriteResult
    {
        public string status;
        public string error;
    }

    public class SlotDesc
    {
        public string state;
        public string label;
        public string savedAt;
    }

    public interface ISaveBackend
    {
        string GetItem(string key);
        void SetItem(string key, string value);
    }

    public class MemoryBackend : ISaveBackend
    {
        private readonly Dictionary<string, string> map = new Dictionary<string, string>();
        public string GetItem(string key) { return map.ContainsKey(key) ? map[key] : null; }
        public void SetItem(string key, string value) { map[key] = value; }
    }

    public class Saver
    {
        public bool sessionOnly;
        public string lastError = "";
        private readonly ISaveBackend backend;
        public Saver(ISaveBackend backend) { this.backend = backend; }
        public string Key(int slot) { return "axe-path-slot-" + slot; }
        public WriteResult Save(int slot, string text)
        {
            try
            {
                backend.SetItem(Key(slot), text);
                if (backend.GetItem(Key(slot)) != text) throw new Exception("readback mismatch");
            }
            catch (Exception err)
            {
                sessionOnly = true;
                lastError = err != null && err.Message != null ? err.Message : "error";
                return new WriteResult { status = "session-only", error = lastError };
            }
            sessionOnly = false;
            lastError = "";
            return new WriteResult { status = "saved", error = "" };
        }
        public string Load(int slot)
        {
            try { return backend.GetItem(Key(slot)); }
            catch { return null; }
        }
        public SlotDesc Describe(int slot)
        {
            var raw = Load(slot);
            if (raw == null) return new SlotDesc { state = "empty", label = "", savedAt = "" };
            var parsed = Logic.ParseSave(raw);
            if (!parsed.ok) return new SlotDesc { state = "broken", label = "", savedAt = "" };
            return new SlotDesc { state = "filled", label = parsed.label, savedAt = parsed.savedAt };
        }
    }

    [Serializable]
    public class WolfState
    {
        public Vec2 pos;
        public Vec2 den;
        public string state;
        public float warnT;
        public float windupT;
        public bool alive;
    }

    public class WolfStepResult
    {
        public bool hasMove;
        public Vec2 move;
        public List<string> events = new List<string>();
    }

    public static class Logic
    {
        public static WorldState CreateWorld(TuningSet t = null)
        {
            if (t == null) t = TuningSet.Default();
            return new WorldState
            {
                version = Meta.SaveVersion,
                generation = 1,
                deaths = 0,
                dwarfAlive = true,
                trust = Trust.Cold,
                warnedDraw = false,
                axe = new AxeState { have = false, yard = true, dullness = 0f, sharpened = false, droppedAt = null },
                skill = new SkillState { chops = 0, kills = 0, swingMs = t.swingBaseMs, noiseMul = t.noiseBase },
                player = new PlayerState { hp = t.playerHp, alive = true },
                ironwood = new IronwoodState { have = 0, turnedIn = false },
                book = new BookState { have = false, read = false },
                flags = new FlagsState { metDwarf = false, metLibrarian = false },
            };
        }

        public static bool TakeYardAxe(WorldState w)
        {
            if (w.axe.have || !w.axe.yard) return false;
            w.axe.have = true;
            w.axe.yard = false;
            w.axe.dullness = 0f;
            return true;
        }

        public static bool TakeBook(WorldState w)
        {
            if (w.book.have) return false;
            w.book.have = true;
            w.book.read = true;
            w.flags.metLibrarian = true;
            return true;
        }

        public static int EdgeStep(WorldState w)
        {
            float d = w.axe.dullness;
            if (d < 34f) return 0;
            if (d < 67f) return 1;
            return 2;
        }

        public static bool UseAxe(WorldState w, string kind, TuningSet t = null)
        {
            if (!w.axe.have || !w.player.alive) return false;
            if (t == null) t = TuningSet.Default();
            float hold = w.axe.sharpened ? t.sharpenHoldMul : 1f;
            if (kind == "chop")
            {
                w.skill.chops += 1;
                w.axe.dullness = Math.Min(100f, w.axe.dullness + t.dullPerChop * hold);
                if (w.skill.chops % t.chopsPerStep == 0)
                {
                    w.skill.swingMs = Math.Max(t.swingMinMs, w.skill.swingMs - t.swingStepMs);
                }
            }
            else if (kind == "hit")
            {
                w.skill.kills += 1;
                w.axe.dullness = Math.Min(100f, w.axe.dullness + t.dullPerHit * hold);
                if (w.skill.kills % t.killsPerStep == 0)
                {
                    w.skill.noiseMul = Math.Max(t.noiseMin, w.skill.noiseMul - t.noiseStep);
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        public static TalkResult TalkDwarf(WorldState w, TuningSet t = null)
        {
            if (!w.dwarfAlive) return new TalkResult { line = null, evt = "dead" };
            w.flags.metDwarf = true;
            if (t == null) t = TuningSet.Default();
            if (w.trust == Trust.Ended) return new TalkResult { line = null, evt = "refused" };
            if (w.trust == Trust.Sharp) return new TalkResult { line = Lines.DwarfSharpening, evt = "idle-sharp" };
            if (w.trust == Trust.Civil) return new TalkResult { line = Lines.DwarfCivil, evt = "idle-civil" };
            if (w.axe.have && w.axe.dullness >= t.dullThresholdCivil)
            {
                w.trust = Trust.Civil;
                return new TalkResult { line = Lines.DwarfArrival, evt = "thaw" };
            }
            return new TalkResult { line = Lines.DwarfCold, evt = "cold" };
        }

        public static string DrawNearDwarf(WorldState w)
        {
            if (!w.dwarfAlive) return "dead";
            if (w.trust == Trust.Ended || w.trust == Trust.Sharp) return w.trust;
            if (!w.warnedDraw)
            {
                w.warnedDraw = true;
                return "warned";
            }
            w.trust = Trust.Ended;
            return Trust.Ended;
        }

        public static bool AttackDwarf(WorldState w)
        {
            if (!w.dwarfAlive) return false;
            w.dwarfAlive = false;
            w.trust = Trust.Ended;
            return true;
        }

        public static bool TurnInIronwood(WorldState w, TuningSet t = null)
        {
            if (!w.dwarfAlive) return false;
            if (w.trust != Trust.Civil) return false;
            if (t == null) t = TuningSet.Default();
            if (w.ironwood.have < t.ironwoodLogsNeeded) return false;
            if (!w.axe.have) return false;
            w.ironwood.have -= t.ironwoodLogsNeeded;
            w.ironwood.turnedIn = true;
            w.trust = Trust.Sharp;
            w.axe.sharpened = true;
            w.axe.dullness = 0f;
            return true;
        }

        public static bool DamagePlayer(WorldState w, int n)
        {
            if (!w.player.alive) return false;
            w.player.hp -= n;
            if (w.player.hp <= 0)
            {
                w.player.hp = 0;
                w.player.alive = false;
                return true;
            }
            return false;
        }

        public static bool Die(WorldState w, DropPos pos, TuningSet t = null)
        {
            if (w.player.alive) return false;
            if (t == null) t = TuningSet.Default();
            w.deaths += 1;
            w.generation += 1;
            w.player.alive = true;
            w.player.hp = t.playerHp;
            w.trust = w.dwarfAlive ? Trust.Cold : Trust.Ended;
            w.warnedDraw = false;
            w.skill = new SkillState { chops = 0, kills = 0, swingMs = t.swingBaseMs, noiseMul = t.noiseBase };
            if (w.axe.have)
            {
                w.axe.have = false;
                w.axe.sharpened = false;
                w.axe.droppedAt = pos;
            }
            w.ironwood.have = 0;
            return true;
        }

        public static bool ReclaimAxe(WorldState w)
        {
            if (w.axe.have || w.axe.droppedAt == null) return false;
            w.axe.have = true;
            w.axe.droppedAt = null;
            return true;
        }

        public static bool FreshYardAxeAvailable(WorldState w)
        {
            return !w.axe.have && w.axe.droppedAt == null;
        }

        public static string SerializeWorld(WorldState w, string label)
        {
            var payload = new
            {
                app = "axe-path",
                version = Meta.SaveVersion,
                savedAt = DateTime.UtcNow.ToString("o"),
                label = string.IsNullOrEmpty(label) ? Lines.LabelGenPrefix + w.generation : label,
                world = w,
            };
            return JsonConvert.SerializeObject(payload);
        }

        private static bool IsNum(JToken t)
        {
            return t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float);
        }

        private static SaveResult Fail(string error)
        {
            return new SaveResult { ok = false, error = error, world = null, label = "", savedAt = "" };
        }

        // Untrusted save strings never reach the UI unbounded. Legit values
        // (small version tokens, short labels) pass through byte-identical.
        private static string Cap(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            if (max > 0 && char.IsHighSurrogate(s[max - 1]) && char.IsLowSurrogate(s[max])) max--;
            return s.Substring(0, max);
        }

        // Reads a possibly-hostile string field: only plain values convert.
        // Anything else (objects, arrays, constructor syntax) becomes ""
        // instead of throwing, and long values are capped for UI sinks.
        private static string SafeField(JObject data, string key, int max)
        {
            var t = data[key];
            if (!(t is JValue)) return "";
            return Cap(t.Value<string>() ?? "", max);
        }

        public static SaveResult ParseSave(string text)
        {
            JObject data;
            try
            {
                var tok = JToken.Parse(text);
                if (tok == null || tok.Type != JTokenType.Object) return Fail(Lines.SaveErrUnreadable);
                data = (JObject)tok;
            }
            catch
            {
                return Fail(Lines.SaveErrUnreadable);
            }
            if (SafeField(data, "app", 64) != "axe-path") return Fail(Lines.SaveErrForeign);
            var ver = data["version"];
            int veri = 0;
            bool verOk = ver != null && ver.Type == JTokenType.Integer && int.TryParse(ver.ToString(), out veri) && veri == Meta.SaveVersion;
            if (!verOk)
            {
                return Fail(Lines.SaveErrVersionPrefix + Cap(ver == null ? "" : ver.ToString(), 64) + Lines.SaveErrVersionMid + Meta.SaveVersion + ".");
            }
            var wtok = data["world"];
            if (wtok == null || wtok.Type != JTokenType.Object) return Fail(Lines.SaveErrNoWorld);
            var w = (JObject)wtok;
            if (!IsNum(w["generation"])) return Fail(Lines.SaveErrGeneration);
            if (!IsNum(w["deaths"])) return Fail(Lines.SaveErrDeaths);
            if (w["dwarfAlive"] == null || w["dwarfAlive"].Type != JTokenType.Boolean) return Fail(Lines.SaveErrDwarf);
            var trust = SafeField(w, "trust", 32);
            if (trust != Trust.Cold && trust != Trust.Civil && trust != Trust.Sharp && trust != Trust.Ended) return Fail(Lines.SaveErrTrust);
            var axe = w["axe"] as JObject;
            if (axe == null || axe["have"] == null || axe["have"].Type != JTokenType.Boolean || !IsNum(axe["dullness"])) return Fail(Lines.SaveErrAxe);
            var skill = w["skill"] as JObject;
            if (skill == null || !IsNum(skill["swingMs"]) || !IsNum(skill["noiseMul"])) return Fail(Lines.SaveErrSkill);
            var player = w["player"] as JObject;
            if (player == null || player["alive"] == null || player["alive"].Type != JTokenType.Boolean || !IsNum(player["hp"])) return Fail(Lines.SaveErrBody);
            var iron = w["ironwood"] as JObject;
            if (iron == null || !IsNum(iron["have"])) return Fail(Lines.SaveErrIronwood);
            var book = w["book"] as JObject;
            if (book == null || book["have"] == null || book["have"].Type != JTokenType.Boolean) return Fail(Lines.SaveErrBook);
            WorldState world;
            try
            {
                world = wtok.ToObject<WorldState>();
            }
            catch
            {
                return Fail(Lines.SaveErrUnreadable);
            }
            return new SaveResult { ok = true, error = "", world = world, label = SafeField(data, "label", 120), savedAt = SafeField(data, "savedAt", 64) };
        }

        public static WolfState CreateWolf(float x, float z)
        {
            return new WolfState
            {
                pos = new Vec2(x, z),
                den = new Vec2(x, z),
                state = "idle",
                warnT = 0f,
                alive = true,
            };
        }

        private static float Dist(Vec2 a, float bx, float bz)
        {
            float dx = a.x - bx;
            float dz = a.z - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static WolfStepResult WolfStep(WolfState wolf, float px, float pz, bool running, float noiseMul, float dt, TuningSet t = null)
        {
            if (t == null) t = TuningSet.Default();
            var res = new WolfStepResult();
            if (!wolf.alive) return res;
            float hearR = (running ? t.wolfHearRun : t.wolfHearWalk) * noiseMul;
            float dPlayer = Dist(wolf.pos, px, pz);
            float dDen = Dist(wolf.pos, wolf.den.x, wolf.den.z);

            if (wolf.state == "idle")
            {
                if (dPlayer <= hearR)
                {
                    wolf.state = "warn";
                    wolf.warnT = t.wolfWarnSecs;
                    res.events.Add("growl");
                }
                return res;
            }
            if (wolf.state == "warn")
            {
                if (dPlayer > hearR * 1.2f)
                {
                    wolf.state = "idle";
                    wolf.warnT = 0f;
                    res.events.Add("calm");
                    return res;
                }
                wolf.warnT -= dt;
                if (wolf.warnT <= 0f)
                {
                    wolf.state = "chase";
                    res.events.Add("chase");
                }
                return res;
            }
            if (wolf.state == "chase")
            {
                if (dDen > t.wolfLeash)
                {
                    wolf.state = "return";
                    res.events.Add("leash");
                    res.hasMove = true;
                    res.move = DirTo(wolf.pos, wolf.den.x, wolf.den.z);
                    return res;
                }
                if (dPlayer <= t.wolfAttackRange)
                {
                    wolf.state = "windup";
                    wolf.windupT = t.wolfWindupSecs;
                    res.events.Add("windup");
                    return res;
                }
                res.hasMove = true;
                res.move = DirTo(wolf.pos, px, pz);
                return res;
            }
            if (wolf.state == "windup")
            {
                // Committed lunge: plants its feet, then leaps. Step past
                // lunge range before it lands and it snaps at empty air.
                wolf.windupT -= dt;
                if (wolf.windupT > 0f) return res;
                wolf.state = "chase";
                res.events.Add(dPlayer <= t.wolfLungeRange ? "attack" : "whiff");
                return res;
            }
            if (wolf.state == "return")
            {
                if (dDen < 1.5f)
                {
                    wolf.state = "idle";
                    return res;
                }
                if (dPlayer <= hearR * 0.6f)
                {
                    wolf.state = "chase";
                    res.events.Add("chase");
                    res.hasMove = true;
                    res.move = DirTo(wolf.pos, px, pz);
                    return res;
                }
                res.hasMove = true;
                res.move = DirTo(wolf.pos, wolf.den.x, wolf.den.z);
                return res;
            }
            return res;
        }

        private static Vec2 DirTo(Vec2 a, float bx, float bz)
        {
            float dx = bx - a.x;
            float dz = bz - a.z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d == 0f) d = 1f;
            return new Vec2(dx / d, dz / d);
        }
    }
}
