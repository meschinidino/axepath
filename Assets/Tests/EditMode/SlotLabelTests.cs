// Axe Path — save-slot button copy. Pins every slot state plus the
// import-hardening rules (40-char cut, surrogate safety, no dangling date).
using NUnit.Framework;
using AxePath;

[TestFixture]
public class SlotLabelTests
{
    [Test]
    public void EmptySlotInvitesANobody()
    {
        var s = Hud.FormatSlotLabel(2, new SlotDesc { state = "empty", label = "", savedAt = "" });
        Assert.AreEqual("Slot 2 — empty, begin as a nobody", s);
    }

    [Test]
    public void FilledSlotShowsLabelAndDate()
    {
        var s = Hud.FormatSlotLabel(1, new SlotDesc { state = "filled", label = "Gen 1, nobody", savedAt = "2026-10-21T15:04:00" });
        Assert.AreEqual("Slot 1 — Gen 1, nobody · 2026-10-21", s);
    }

    [Test]
    public void FilledSlotWithoutDateHasNoDanglingSeparator()
    {
        var s = Hud.FormatSlotLabel(1, new SlotDesc { state = "filled", label = "Gen 2, civil", savedAt = "" });
        Assert.AreEqual("Slot 1 — Gen 2, civil", s);
        Assert.IsFalse(s.Contains("·"));
    }

    [Test]
    public void BrokenSlotOffersOverwrite()
    {
        var s = Hud.FormatSlotLabel(3, new SlotDesc { state = "broken", label = "", savedAt = "" });
        Assert.AreEqual("Slot 3 — broken save, pick to overwrite", s);
    }

    [Test]
    public void LongImportLabelTruncatesAt40Chars()
    {
        var s = Hud.FormatSlotLabel(1, new SlotDesc { state = "filled", label = new string('x', 120), savedAt = "2026-10-21" });
        Assert.AreEqual("Slot 1 — " + new string('x', 40) + "... · 2026-10-21", s);
    }

    [Test]
    public void NullLabelBehavesAsEmpty()
    {
        var s = Hud.FormatSlotLabel(1, new SlotDesc { state = "filled", label = null, savedAt = "" });
        Assert.AreEqual("Slot 1 — ", s);
    }
}
