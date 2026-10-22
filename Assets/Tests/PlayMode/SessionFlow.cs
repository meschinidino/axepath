// Full-loop verification through the real session: title -> axe -> book ->
// chop -> wolf fight -> thaw -> ironwood -> sharpened -> death -> reclaim.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using AxePath;

public class SessionFlow
{
    [UnitySetUp]
    public IEnumerator WaitForBoot()
    {
        yield return TestBoot.WaitForReady();
    }

    private static float Face(float fx, float fz, float tx, float tz)
    {
        return Mathf.Atan2(-(tx - fx), -(tz - fz));
    }

    private static IEnumerator Shot(string name)
    {
        yield return null;
        yield return null;
        string rel = "Assets/Screenshots/" + name + ".png";
        string full = Path.Combine(Application.dataPath, "Screenshots/" + name + ".png");
        try { if (File.Exists(full)) File.Delete(full); } catch { }
        ScreenCapture.CaptureScreenshot(rel);
        // ScreenCapture writes on frame present, which never happens in a
        // batchmode test player — the file assert only holds in the editor.
        if (Application.isBatchMode) yield break;
        float t0 = Time.realtimeSinceStartup;
        while (!File.Exists(full) && Time.realtimeSinceStartup - t0 < 15f) yield return null;
        Assert.IsTrue(File.Exists(full), "screenshot missing: " + name);
    }

    [UnityTest]
    public IEnumerator FullLoop()
    {
        yield return null;
        Assert.IsNotNull(Game.Session, "Boot did not run");
        var S = Game.Session;
        // Order-independent: earlier suites may have left a live session.
        S.ToTitle();
        Assert.AreEqual(GameSession.State.Title, S.CurrentState);
        yield return Shot("flow-title");

        S.NewGame(1);
        Assert.AreEqual(GameSession.State.Play, S.CurrentState);
        Assert.IsFalse(S.W.axe.have);

        // Axe.
        S.Player.Teleport(-30f, 178.5f, 0f);
        Assert.AreEqual("axe", S.InteractTarget().id);
        S.TryInteract();
        Assert.IsTrue(S.W.axe.have);
        Assert.IsTrue(S.Hud.IsDialogueOpen());
        S.AdvanceDialogue();
        Assert.IsTrue(S.Hud.IsDialogueOpen());
        S.AdvanceDialogue();
        Assert.IsFalse(S.Hud.IsDialogueOpen());
        yield return Shot("flow-hud");

        // Book.
        S.Player.Teleport(35f, 188.5f, 0f);
        Assert.AreEqual("bookstand", S.InteractTarget().id);
        S.TryInteract();
        Assert.IsTrue(S.W.book.have);
        Assert.IsTrue(S.Hud.IsBookOpen());
        S.CloseBookPublic();
        Assert.IsFalse(S.Hud.IsBookOpen());

        // Librarian: her talk hands over nothing new — the sketch is
        // already in hand and her books stay on the shelves.
        S.Player.Teleport(48f, 183.5f, 0f);
        Assert.AreEqual("librarian", S.InteractTarget().id);
        S.TryInteract();
        Assert.IsTrue(S.W.book.have);
        Assert.IsTrue(S.W.flags.metLibrarian);
        Assert.IsTrue(S.Hud.IsDialogueOpen());
        S.AdvanceDialogue();
        Assert.IsFalse(S.Hud.IsDialogueOpen());

        // Tree chop at the landmark pine.
        S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.AreEqual(1, S.W.skill.chops);
        Assert.AreEqual(9f, S.W.axe.dullness);
        Assert.AreEqual("Chips fly.", S.Hud.PromptText());
        yield return new WaitForSeconds(0.8f);

        // Wolf fight at the den (alpha of the pack).
        Assert.AreEqual(5, S.Wolves.Count);
        S.Wolves[0].brain.state = "chase";
        S.Wolves[0].brain.pos = new Vec2(-70f, 2f);
        S.Player.Teleport(-70f, 3.5f, Face(-70f, 3.5f, -70f, 2f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.AreEqual(1, S.Wolves[0].hp);
        Assert.AreEqual("The wolf snarls, wounded.", S.Hud.PromptText());
        yield return new WaitForSeconds(0.8f);
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.IsFalse(S.Wolves[0].brain.alive);
        Assert.AreEqual(1, S.W.skill.kills);
        Assert.AreEqual("The wolf is still. The forest feels larger.", S.Hud.PromptText());
        Assert.IsTrue(S.W.player.alive);
        Assert.IsTrue(S.W.player.hp >= 2);
        yield return new WaitForSeconds(0.8f);

        // Thaw with a dulled axe.
        S.W.axe.dullness = 45f;
        S.Player.Teleport(-4f, -310f, Face(-4f, -310f, -4f, -312f));
        Assert.AreEqual("dwarf", S.InteractTarget().id);
        S.TryInteract();
        Assert.AreEqual(Trust.Civil, S.W.trust);
        S.AdvanceDialogue();
        S.AdvanceDialogue();
        Assert.IsFalse(S.Hud.IsDialogueOpen());

        // Ironwood turn-in.
        S.Player.Teleport(72f, -337f, 0f);
        Assert.AreEqual("ironwood", S.InteractTarget().id);
        S.TryInteract();
        Assert.AreEqual(1, S.W.ironwood.have);
        S.AdvanceDialogue();
        S.Player.Teleport(-4f, -310f, Face(-4f, -310f, -4f, -312f));
        S.TryInteract();
        Assert.AreEqual(Trust.Sharp, S.W.trust);
        Assert.IsTrue(S.W.axe.sharpened);
        Assert.AreEqual(0f, S.W.axe.dullness);
        S.AdvanceDialogue();

        // Death and corpse run.
        S.Player.Teleport(0f, 100f, 0f);
        S.DebugDamage(99);
        Assert.IsTrue(S.Hud.IsDeathOpen());
        yield return Shot("flow-death");
        S.Wake();
        Assert.AreEqual(2, S.W.generation);
        Assert.AreEqual(Trust.Cold, S.W.trust);
        Assert.IsFalse(S.W.axe.have);
        Assert.IsNotNull(S.W.axe.droppedAt);
        S.Player.Teleport(S.W.axe.droppedAt.x, S.W.axe.droppedAt.z, 0f);
        Assert.AreEqual("cairn", S.InteractTarget().id);
        S.TryInteract();
        Assert.IsTrue(S.W.axe.have);
        S.AdvanceDialogue();

        // A save file exists on disk.
        string save = Path.Combine(Application.persistentDataPath, "axe-path-slot-1.json");
        Assert.IsTrue(File.Exists(save), "no save file at " + save);
    }

    [UnityTest]
    public IEnumerator TitleButtonsWork()
    {
        yield return null;
        var S = Game.Session;
        S.ToTitle();
        yield return null;
        Assert.AreEqual(GameSession.State.Title, S.CurrentState);
        Assert.IsNotNull(GameObject.Find("EventSystem"), "no EventSystem: UI clicks dead");
        var btn = GameObject.Find("HudCanvas/Title/Slots/Slot").GetComponent<UnityEngine.UI.Button>();
        Assert.IsNotNull(btn, "no slot button found");
        btn.onClick.Invoke();
        yield return null;
        Assert.AreEqual(GameSession.State.Play, S.CurrentState);
    }

    [UnityTest]
    public IEnumerator EscResumeRoundtrip()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(1);
        S.Player.Teleport(48f, 183.5f, 0f);
        var w = S.W;
        var before = S.Player.EyeXZ();
        S.ToTitle();
        yield return null;
        Assert.AreEqual(GameSession.State.Title, S.CurrentState);
        Assert.IsNotNull(GameObject.Find("HudCanvas/Title"), "title not shown");
        S.HandleTitleEscape();
        yield return null;
        Assert.AreEqual(GameSession.State.Play, S.CurrentState);
        Assert.IsNull(GameObject.Find("HudCanvas/Title"), "title still shown");
        Assert.AreSame(w, S.W, "resume replaced the session");
        var after = S.Player.EyeXZ();
        Assert.AreEqual(before.x, after.x, 0.01f, "resume respawned");
        Assert.AreEqual(before.z, after.z, 0.01f, "resume respawned");
    }

    [UnityTest]
    public IEnumerator EscDismissesTitleModal()
    {
        yield return null;
        var S = Game.Session;
        S.ToTitle();
        yield return null;
        S.Hud.ShowModal("t", "b", null, null);
        Assert.IsTrue(S.Hud.IsModalOpen());
        S.HandleTitleEscape();
        Assert.IsFalse(S.Hud.IsModalOpen(), "modal survived Esc");
        Assert.AreEqual(GameSession.State.Title, S.CurrentState);
    }

    [UnityTest]
    public IEnumerator PauseResumeRoundtrip()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(1);
        S.Player.Teleport(48f, 183.5f, 0f);
        var w = S.W;
        var before = S.Player.EyeXZ();
        S.HandlePlayEscape();
        yield return null;
        Assert.AreEqual(GameSession.State.Paused, S.CurrentState);
        Assert.IsTrue(S.Hud.IsPauseOpen());
        Assert.IsNull(GameObject.Find("HudCanvas/Title"), "pause showed the title");
        S.Resume();
        yield return null;
        Assert.AreEqual(GameSession.State.Play, S.CurrentState);
        Assert.IsFalse(S.Hud.IsPauseOpen());
        Assert.AreSame(w, S.W, "resume replaced the session");
        var after = S.Player.EyeXZ();
        Assert.AreEqual(before.x, after.x, 0.01f, "resume respawned");
        Assert.AreEqual(before.z, after.z, 0.01f, "resume respawned");
    }

    [UnityTest]
    public IEnumerator PauseQuitsToTitle()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(1);
        S.HandlePlayEscape();
        yield return null;
        Assert.AreEqual(GameSession.State.Paused, S.CurrentState);
        var quit = GameObject.Find("HudCanvas/Pause/Quit").GetComponent<Button>();
        quit.onClick.Invoke();
        yield return null;
        Assert.AreEqual(GameSession.State.Title, S.CurrentState);
        Assert.IsFalse(S.Hud.IsPauseOpen());
        Assert.IsNotNull(GameObject.Find("HudCanvas/Title"), "title not shown");
    }

    [UnityTest]
    public IEnumerator LookSensSliderRoundtrips()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(1);
        S.HandlePlayEscape();
        yield return null;
        float orig = PlayerController.LookSens;
        var slider = GameObject.Find("HudCanvas/Pause/Sens").GetComponent<Slider>();
        var val = GameObject.Find("HudCanvas/Pause/SensVal").GetComponent<Text>();
        Assert.AreEqual(PlayerController.LookSensMin, slider.minValue);
        Assert.AreEqual(PlayerController.LookSensMax, slider.maxValue);
        slider.value = PlayerController.LookSensMax;
        Assert.AreEqual(PlayerController.LookSensMax, PlayerController.LookSens);
        Assert.AreEqual(PlayerController.LookSensMax, PlayerPrefs.GetFloat(GameSession.LookSensPrefKey));
        Assert.AreEqual("200%", val.text);
        slider.value = orig;
        Assert.AreEqual(orig, PlayerController.LookSens);
        Assert.AreEqual(orig, PlayerPrefs.GetFloat(GameSession.LookSensPrefKey));
        S.Resume();
        yield return null;
        Assert.AreEqual(GameSession.State.Play, S.CurrentState);
    }

    [UnityTest]
    public IEnumerator WaystoneRuneReads()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(1);
        S.Player.Teleport(-7f, -80f, 0f);
        var t = S.InteractTarget();
        Assert.IsNotNull(t, "no target at the waystone");
        Assert.AreEqual("waystone", t.id);
        S.TryInteract();
        Assert.IsTrue(S.Hud.IsDialogueOpen());
        S.AdvanceDialogue();
        Assert.IsFalse(S.Hud.IsDialogueOpen());
    }

    [UnityTest]
    public IEnumerator GuideReadsFromOutsideItsWall()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(1);
        // 4.5 south of the corner collider's center: genuinely walkable,
        // 3.9 from the mark — inside the extended reach only.
        S.Player.Teleport(13f, -272.5f, 0f);
        var t = S.InteractTarget();
        Assert.IsNotNull(t, "no target at the guide mark");
        Assert.AreEqual("guide", t.id);
        S.TryInteract();
        Assert.IsTrue(S.Hud.IsDialogueOpen());
        S.AdvanceDialogue();
        Assert.IsFalse(S.Hud.IsDialogueOpen());
    }

    [UnityTest]
    public IEnumerator RealClickPipeline()
    {
        yield return null;
        var S = Game.Session;
        S.ToTitle();
        yield return null;
        var btnGo = GameObject.Find("HudCanvas/Title/Slots/Slot");
        Assert.IsNotNull(btnGo, "no slot button found");
        var canvas = GameObject.Find("HudCanvas").GetComponent<Canvas>();
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        var rt = btnGo.GetComponent<RectTransform>();
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        var pointer = new PointerEventData(EventSystem.current);
        pointer.position = screen;
        var results = new System.Collections.Generic.List<RaycastResult>();
        raycaster.Raycast(pointer, results);
        bool hit = false;
        foreach (var r in results) if (r.gameObject == btnGo) hit = true;
        Assert.IsTrue(hit, "raycast missed the slot button");
        ExecuteEvents.Execute(btnGo, pointer, ExecuteEvents.submitHandler);
        yield return null;
        Assert.AreEqual(GameSession.State.Play, S.CurrentState);
        for (int i = 0; i < 30; i++) yield return null;
        Assert.AreEqual(GameSession.State.Play, S.CurrentState);
        Assert.IsTrue(S.W.player.alive);
    }

    [UnityTest]
    public IEnumerator WalkAwayCancelsDialogue()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(2);
        S.Player.Teleport(-30f, 178.5f, 0f);
        S.TryInteract();
        Assert.IsTrue(S.Hud.IsDialogueOpen());
        S.Player.Teleport(-30f, 100f, 0f);
        yield return null;
        yield return null;
        Assert.IsFalse(S.Hud.IsDialogueOpen());
    }

    [UnityTest]
    public IEnumerator HitStopAndKick()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(3);
        S.W.axe.have = true;
        int hp0 = S.W.player.hp;
        // Scripted bite: a chasing wolf inside attack range telegraphs
        // (windup) then lunges ~0.45s later — poll for the wound so the
        // kick is asserted fresh, on the bite frame itself.
        S.Wolves[0].brain.state = "chase";
        S.Wolves[0].brain.pos = new Vec2(-70f, 2.2f);
        S.Player.Teleport(-70f, 3.5f, Face(-70f, 3.5f, -70f, 2.2f));
        bool sawWindup = false;
        float t0 = Time.realtimeSinceStartup;
        while (S.W.player.hp == hp0 && Time.realtimeSinceStartup - t0 < 3f)
        {
            if (S.Wolves[0].brain.state == "windup") sawWindup = true;
            yield return null;
        }
        Assert.IsTrue(sawWindup, "wolf never telegraphed");
        Assert.AreEqual(hp0 - 1, S.W.player.hp);
        Assert.Greater(S.Player.Trauma, 0f);

        // Wound: the stop timer is set, the lens is kicked, and the wolf
        // holds its position for a frame — 0.06s of stop can never expire
        // inside one 0.05s-clamped frame.
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.AreEqual(1, S.Wolves[0].hp);
        float stop1 = S.HitStop;
        Assert.Greater(stop1, 0f);
        Assert.Greater(S.Player.Trauma, 0f);
        var frozen = S.Wolves[0].brain.pos;
        yield return null;
        Assert.AreEqual(frozen.x, S.Wolves[0].brain.pos.x);
        Assert.AreEqual(frozen.z, S.Wolves[0].brain.pos.z);
        Assert.Greater(S.HitStop, 0f);
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(0f, S.HitStop);

        // Kill: longer stop, wolf still, no stray bites interfered, and
        // the shake fully decays.
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.IsFalse(S.Wolves[0].brain.alive);
        Assert.Greater(S.HitStop, stop1);
        Assert.Greater(S.Player.Trauma, 0f);
        Assert.AreEqual(hp0 - 1, S.W.player.hp);
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(0f, S.Player.Trauma);
        Assert.IsTrue(S.W.player.alive);
    }

    [UnityTest]
    public IEnumerator ChopLandsWeight()
    {
        yield return null;
        var S = Game.Session;
        S.NewGame(4);
        S.W.axe.have = true;
        int c0 = S.W.skill.chops;
        S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.AreEqual(c0 + 1, S.W.skill.chops);
        Assert.Greater(S.HitStop, 0f);
        Assert.Greater(S.Player.Trauma, 0f);
        Assert.Greater(S.MotesOf(MoteKind.Chip), 0);
        Assert.Greater(S.TreeShakeCount, 0);
        yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(0, S.MoteCount);
        Assert.AreEqual(0, S.TreeShakeCount);
    }

    [UnityTest]
    public IEnumerator VolumeAndMuteWiring()
    {
        var S = Game.Session;
        S.SetVolume(0.5f);
        Assert.AreEqual(0.5f, S.Audio.MasterGain, 0.001f);
        Assert.AreEqual(0.5f, PlayerPrefs.GetFloat(GameSession.VolumePrefKey), 0.001f);
        S.SetVolume(2f);
        Assert.AreEqual(1f, S.Audio.MasterGain, 0.001f);
        bool before = S.Audio.Muted;
        S.ToggleMute();
        Assert.AreNotEqual(before, S.Audio.Muted);
        Assert.AreEqual(S.Audio.Muted ? 1 : 0, PlayerPrefs.GetInt(GameSession.MutePrefKey));
        S.ToggleMute();
        Assert.AreEqual(before, S.Audio.Muted);
        S.SetVolume(1f);
        yield return null;
    }

    [UnityTest]
    public IEnumerator LoadingHiddenAfterBoot()
    {
        var S = Game.Session;
        Assert.IsFalse(S.Hud.IsLoadingOpen(), "loading overlay stuck open after boot");
        Assert.IsFalse(GameSession.IsHandheld(), "editor is not a handheld");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ImpactWaitsForContact()
    {
        var S = Game.Session;
        S.NewGame(4);
        S.W.axe.have = true;
        int c0 = S.W.skill.chops;
        S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
        S.TryAttack();
        Assert.AreEqual(c0, S.W.skill.chops, "impact must not resolve on click");
        Assert.AreEqual(0f, S.HitStop);
        yield return TestBoot.SwingToContact(S);
        Assert.AreEqual(c0 + 1, S.W.skill.chops);
        Assert.Greater(S.MotesOf(MoteKind.Chip), 0);
    }

    [UnityTest]
    public IEnumerator JumpArc()
    {
        var S = Game.Session;
        S.NewGame(4);
        Assert.IsTrue(S.Player.TryJump());
        Assert.IsFalse(S.Player.TryJump(), "no double jump");
        float t0 = Time.realtimeSinceStartup;
        while (S.Player.AirY <= 0f && Time.realtimeSinceStartup - t0 < 2f) yield return null;
        Assert.Greater(S.Player.AirY, 0f, "never left the ground");
        t0 = Time.realtimeSinceStartup;
        while (S.Player.AirY > 0f && Time.realtimeSinceStartup - t0 < 3f) yield return null;
        Assert.AreEqual(0f, S.Player.AirY);
    }

    [UnityTest]
    public IEnumerator CrosshairHotInReach()
    {
        var S = Game.Session;
        S.NewGame(4);
        S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f));
        yield return null;
        Assert.IsTrue(S.Hud.IsCrossHot(), "pine in reach should heat the crosshair");
        S.Player.Teleport(0f, 152.5f, Face(0f, 152.5f, 0f, 150f) + Mathf.PI);
        yield return null;
        Assert.IsFalse(S.Hud.IsCrossHot(), "facing away should cool the crosshair");
    }

    [UnityTest]
    public IEnumerator WolfHitBurstsBlood()
    {
        var S = Game.Session;
        S.NewGame(4);
        S.W.axe.have = true;
        S.Wolves[0].brain.state = "chase";
        S.Wolves[0].brain.pos = new Vec2(-70f, 2f);
        S.Player.Teleport(-70f, 3.5f, Face(-70f, 3.5f, -70f, 2f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.AreEqual(1, S.Wolves[0].hp);
        Assert.Greater(S.MotesOf(MoteKind.Blood), 0);
        Assert.Greater(S.MotePosOf(MoteKind.Blood).z, 1.9f);
        Assert.AreEqual(0, S.MotesOf(MoteKind.Chip));
        Assert.AreEqual(0, S.MotesOf(MoteKind.Spark));
    }

    [UnityTest]
    public IEnumerator ForgeStrikeThrowsSparks()
    {
        var S = Game.Session;
        S.NewGame(4);
        S.W.axe.have = true;
        foreach (var w in S.Wolves) { w.brain.state = "idle"; w.brain.pos = new Vec2(-70f, 0f); }
        S.Player.Teleport(5f, -312.5f, Face(5f, -312.5f, 5f, -315f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.Greater(S.MotesOf(MoteKind.Spark), 0);
        Assert.Less(S.MotePosOf(MoteKind.Spark).z, -313.9f, "sparks erupt at the forge face, past the 313.6 rim");
        Assert.AreEqual(0, S.MotesOf(MoteKind.Chip));
        Assert.AreEqual(0, S.MotesOf(MoteKind.Blood));
        Assert.AreEqual("Sparks fly.", S.Hud.PromptText());
    }

    [UnityTest]
    public IEnumerator MillWallBurstsChipsWithoutShake()
    {
        var S = Game.Session;
        S.NewGame(4);
        S.W.axe.have = true;
        foreach (var w in S.Wolves) { w.brain.state = "idle"; w.brain.pos = new Vec2(-70f, 0f); }
        S.Player.Teleport(104f, 184f, Face(104f, 184f, 104f, 178f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.Greater(S.MotesOf(MoteKind.Chip), 0);
        Assert.Less(S.MotePosOf(MoteKind.Chip).z, 181.5f, "chips erupt at the mill face, past the 182.5 rim");
        Assert.AreEqual(0, S.TreeShakeCount, "walls don't shudder — chips must come from the wall, not a tree");
        Assert.AreEqual("Chips fly.", S.Hud.PromptText());
    }

    [UnityTest]
    public IEnumerator WolfCorpseLingers()
    {
        var S = Game.Session;
        S.NewGame(4);
        S.W.axe.have = true;
        for (int i = 1; i < S.Wolves.Count; i++) { S.Wolves[i].brain.state = "idle"; S.Wolves[i].brain.pos = new Vec2(-70f, 70f); }
        S.Wolves[0].brain.state = "chase";
        S.Wolves[0].brain.pos = new Vec2(-70f, 2f);
        S.Player.Teleport(-70f, 3.5f, Face(-70f, 3.5f, -70f, 2f));
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        yield return new WaitForSeconds(0.8f);
        S.TryAttack();
        yield return TestBoot.SwingToContact(S);
        Assert.IsFalse(S.Wolves[0].brain.alive);
        Assert.IsFalse(S.Wolves[0].fx.corpseGone, "fresh kill must still lie there");
        yield return new WaitForSeconds(3f);
        Assert.IsTrue(S.Wolves[0].fx.corpseGone, "corpse clears after a couple seconds");
    }

}
