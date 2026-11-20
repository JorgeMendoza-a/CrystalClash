using CrystalClash.Ranked.Logic;
using CrystalClash.Story.Logic;
using NUnit.Framework;

public class ProfileLogicTests
{
    [Test]
    public void Level_FromXp()
    {
        PlayerLevel.FromXp(0, out int level, out int inLevel, out int next);
        Assert.AreEqual(1, level);
        Assert.AreEqual(0, inLevel);
        Assert.AreEqual(100, next);

        PlayerLevel.FromXp(100, out level, out inLevel, out next);
        Assert.AreEqual(2, level);
        Assert.AreEqual(150, next);

        PlayerLevel.FromXp(100 + 150 + 20, out level, out inLevel, out next);
        Assert.AreEqual(3, level);
        Assert.AreEqual(20, inLevel);
        Assert.AreEqual(200, next);
    }

    [Test]
    public void Names_AreValidatedAndCleaned()
    {
        Assert.AreEqual("JorgeM145", RankedRules.NormalizeName("  JorgeM145 "));
        Assert.AreEqual("Pico de Oro", RankedRules.NormalizeName("Pico   de  Oro"));
        Assert.AreEqual("Ñandú_7", RankedRules.NormalizeName("Ñandú_7"));
        Assert.IsNull(RankedRules.NormalizeName("ab"));
        Assert.IsNull(RankedRules.NormalizeName("NombreDemasiadoLargo123"));
        Assert.IsNull(RankedRules.NormalizeName("<script>"));
        Assert.IsNull(RankedRules.NormalizeName(null));
    }

    [Test]
    public void Achievements_ShowProgress()
    {
        var progress = new PlayerProgress { specialsCreated = 150 };
        progress.RecordWin(1, 3, 10);

        var list = Achievements.Evaluate(progress, 10, 4, 8100);

        Assert.AreEqual(4, list.Count);
        Assert.IsTrue(list[0].Unlocked);                 // 150 >= 100 (y se muestra 100/100)
        Assert.AreEqual(100, list[0].Current);
        Assert.IsFalse(list[1].Unlocked);
        Assert.AreEqual(4, list[1].Current);
        Assert.AreEqual(3, list[2].Current);
        Assert.AreEqual(30, list[2].Target);
        Assert.IsTrue(list[3].Unlocked);
    }
}
