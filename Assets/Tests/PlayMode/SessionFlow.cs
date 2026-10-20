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
        Assert.AreEqual(1, S.Wolves[0].hp);
        Assert.AreEqual("The wolf snarls, wounded.", S.Hud.PromptText());
        yield return new WaitForSeconds(0.8f);
        S.TryAttack();
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

}
