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
        public const float WolfHearWalk = 8f;
        public const float WolfHearRun = 25f;
        public const float WolfWarnSecs = 3f;
        public const float WolfLeash = 40f;
        public const int WolfHitsToKill = 3;
        public const float WolfChaseSpeed = 4.2f;
        public const float WolfAttackRange = 1.7f;
        public const float AxeReach = 2.6f;
        public const float WolfHitSecs = 0.35f;
        public const float WolfFlashSecs = 0.22f;
        public const float WolfKnockSpeed = 3.2f;
        public const float WolfStaggerSecs = 0.9f;
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
        public const string LibrarianRoute = "Three pines east. Split rock. Then follow the smoke. Take the sketch, the book stays with me.";
        public const string LibrarianReplay = "Three pines, split rock, smoke. The sketch is in your hands. Go carefully.";
        public const string BookSketch = "THREE PINES EAST out of the village.\nSPLIT ROCK where the path forks.\nSMOKE over the treeline marks the cave.\n\nOf the mountain smith, young and welcoming to all who climb with honest need. — S.";
        public const string DwarfCold = "State your business. Quickly.";
        public const string DwarfArrival = "...That edge. You walked here. Hm. Perhaps you are not entirely soft.";
        public const string DwarfCivil = "Ironwood. Past my cave to the east, where the pale trunks grow. Bring one log and we will see.";
        public const string DwarfSharpening = "Give it here. ... A working edge, earned. Keep it.";
        public const string DwarfWarnDraw = "Raise that again, little climber, and we are done.";
        public const string DwarfStranger = "You wear a dead climber’s boots. ...Speak.";
        public const string DwarfDeadNote = "The forge is cold. Whatever the dwarf knew about the axe is gone with him.";
        public static readonly string[] Blaze = new string[]
        {
            "Old axe-mark, gray with weather. Someone came this way before you, and wanted the next one to know.",
            "Fresh chips around an old mark. More than one climber has passed here. Few, perhaps, came back.",
            "The mark faces north, toward the smoke. Whoever left it knew the way.",
        };
        public const string GuideEast = "The mark faces east, not north. Three short strokes beneath it, cut with purpose. Whoever left it wanted the next climber to turn aside here.";
    }

    public class TuningSet
    {
        public float wolfHearWalk = Tuning.WolfHearWalk;
        public float wolfHearRun = Tuning.WolfHearRun;
        public float wolfWarnSecs = Tuning.WolfWarnSecs;
        public float wolfLeash = Tuning.WolfLeash;
        public float wolfAttackRange = Tuning.WolfAttackRange;
        public static TuningSet Default() { return new TuningSet(); }
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
        public static WorldState CreateWorld()
        {
            return new WorldState
            {
                version = Meta.SaveVersion,
                generation = 1,
                deaths = 0,
                dwarfAlive = true,
                trust = Trust.Cold,
                warnedDraw = false,
                axe = new AxeState { have = false, yard = true, dullness = 0f, sharpened = false, droppedAt = null },
                skill = new SkillState { chops = 0, kills = 0, swingMs = Tuning.SwingBaseMs, noiseMul = Tuning.NoiseBase },
                player = new PlayerState { hp = Tuning.WolfHitsToKill, alive = true },
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

        public static bool UseAxe(WorldState w, string kind)
        {
            if (!w.axe.have || !w.player.alive) return false;
            float hold = w.axe.sharpened ? Tuning.SharpenHoldMul : 1f;
            if (kind == "chop")
            {
                w.skill.chops += 1;
                w.axe.dullness = Math.Min(100f, w.axe.dullness + Tuning.DullPerChop * hold);
                if (w.skill.chops % Tuning.ChopsPerStep == 0)
                {
                    w.skill.swingMs = Math.Max(Tuning.SwingMinMs, w.skill.swingMs - Tuning.SwingStepMs);
                }
            }
            else if (kind == "hit")
            {
                w.skill.kills += 1;
                w.axe.dullness = Math.Min(100f, w.axe.dullness + Tuning.DullPerHit * hold);
                if (w.skill.kills % Tuning.KillsPerStep == 0)
                {
                    w.skill.noiseMul = Math.Max(Tuning.NoiseMin, w.skill.noiseMul - Tuning.NoiseStep);
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        public static TalkResult TalkDwarf(WorldState w)
        {
            if (!w.dwarfAlive) return new TalkResult { line = null, evt = "dead" };
            w.flags.metDwarf = true;
            if (w.trust == Trust.Ended) return new TalkResult { line = null, evt = "refused" };
            if (w.trust == Trust.Sharp) return new TalkResult { line = Lines.DwarfSharpening, evt = "idle-sharp" };
            if (w.trust == Trust.Civil) return new TalkResult { line = Lines.DwarfCivil, evt = "idle-civil" };
            if (w.axe.have && w.axe.dullness >= Tuning.DullThresholdCivil)
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

        public static bool TurnInIronwood(WorldState w)
        {
            if (!w.dwarfAlive) return false;
            if (w.trust != Trust.Civil) return false;
            if (w.ironwood.have < Tuning.IronwoodLogsNeeded) return false;
            if (!w.axe.have) return false;
            w.ironwood.have -= Tuning.IronwoodLogsNeeded;
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

        public static bool Die(WorldState w, DropPos pos)
        {
            if (w.player.alive) return false;
            w.deaths += 1;
            w.generation += 1;
            w.player.alive = true;
            w.player.hp = Tuning.WolfHitsToKill;
            w.trust = w.dwarfAlive ? Trust.Cold : Trust.Ended;
            w.warnedDraw = false;
            w.skill = new SkillState { chops = 0, kills = 0, swingMs = Tuning.SwingBaseMs, noiseMul = Tuning.NoiseBase };
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
                label = string.IsNullOrEmpty(label) ? "Gen " + w.generation : label,
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

        public static SaveResult ParseSave(string text)
        {
            JObject data;
            try
            {
                var tok = JToken.Parse(text);
                if (tok == null || tok.Type != JTokenType.Object) return Fail("That file is not a readable save.");
                data = (JObject)tok;
            }
            catch
            {
                return Fail("That file is not a readable save.");
            }
            if (data.Value<string>("app") != "axe-path") return Fail("That file belongs to another game.");
            var ver = data["version"];
            if (ver == null || ver.Type != JTokenType.Integer || ver.Value<int>() != Meta.SaveVersion)
            {
                return Fail("That save was written by version " + (ver == null ? "" : ver.ToString()) + ", this game reads version " + Meta.SaveVersion + ".");
            }
            var wtok = data["world"];
            if (wtok == null || wtok.Type != JTokenType.Object) return Fail("That save is missing its world.");
            var w = (JObject)wtok;
            if (!IsNum(w["generation"])) return Fail("That save has a broken generation.");
            if (!IsNum(w["deaths"])) return Fail("That save has a broken deaths.");
            if (w["dwarfAlive"] == null || w["dwarfAlive"].Type != JTokenType.Boolean) return Fail("That save has a broken dwarf.");
            var trust = w.Value<string>("trust");
            if (trust != Trust.Cold && trust != Trust.Civil && trust != Trust.Sharp && trust != Trust.Ended) return Fail("That save has broken trust.");
            var axe = w["axe"] as JObject;
            if (axe == null || axe["have"] == null || axe["have"].Type != JTokenType.Boolean || !IsNum(axe["dullness"])) return Fail("That save has a broken axe.");
            var skill = w["skill"] as JObject;
            if (skill == null || !IsNum(skill["swingMs"]) || !IsNum(skill["noiseMul"])) return Fail("That save has broken skill.");
            var player = w["player"] as JObject;
            if (player == null || player["alive"] == null || player["alive"].Type != JTokenType.Boolean || !IsNum(player["hp"])) return Fail("That save has a broken body.");
            var iron = w["ironwood"] as JObject;
            if (iron == null || !IsNum(iron["have"])) return Fail("That save has broken ironwood.");
            var book = w["book"] as JObject;
            if (book == null || book["have"] == null || book["have"].Type != JTokenType.Boolean) return Fail("That save has a broken book.");
            WorldState world;
            try
            {
                world = wtok.ToObject<WorldState>();
            }
            catch
            {
                return Fail("That file is not a readable save.");
            }
            return new SaveResult { ok = true, error = "", world = world, label = data.Value<string>("label") ?? "", savedAt = data.Value<string>("savedAt") ?? "" };
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

        public static WolfStepResult WolfStep(WolfState wolf, float px, float pz, bool running, float noiseMul, float dt, TuningSet t)
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
                    res.events.Add("attack");
                    return res;
                }
                res.hasMove = true;
                res.move = DirTo(wolf.pos, px, pz);
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
