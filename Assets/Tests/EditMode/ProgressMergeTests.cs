using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using NUnit.Framework;

public class ProgressMergeTests
{
    [Test]
    public void Merge_KeepsBestProgress_AndNewestWallet()
    {
        var phone = new PlayerProgress { highestUnlocked = 12, xp = 900, coins = 300, gems = 5, savedUtc = 100, createdUtc = 50 };
        phone.RecordWin(1, 3, 100);
        phone.stars[0] = 3;

        var cloud = new PlayerProgress { highestUnlocked = 20, xp = 700, coins = 800, gems = 40, savedUtc = 200, createdUtc = 10 };
        cloud.stars.AddRange(new[] { 1, 2, 3 });
        cloud.AddBooster(BoosterKind.Bomb, 4);

        PlayerProgress merged = PlayerProgress.Merge(phone, cloud);

        Assert.AreEqual(20, merged.highestUnlocked);           // máximo
        Assert.AreEqual(900, merged.xp);                        // máximo
        Assert.AreEqual(3, merged.GetStars(1));                 // máximo por nivel
        Assert.AreEqual(2, merged.GetStars(2));
        Assert.AreEqual(3, merged.GetStars(3));
        Assert.AreEqual(800, merged.coins);                     // de la copia más nueva (nube)
        Assert.AreEqual(40, merged.gems);
        Assert.AreEqual(PlayerProgress.StartingBoosters + 4, merged.GetBoosters(BoosterKind.Bomb));
        Assert.AreEqual(10L, merged.createdUtc);                // la fecha más antigua
        Assert.AreEqual(200L, merged.savedUtc);
    }

    [Test]
    public void Merge_WithNothing_ReturnsCopy()
    {
        var p = new PlayerProgress { highestUnlocked = 5 };
        PlayerProgress merged = PlayerProgress.Merge(p, null);
        Assert.AreEqual(5, merged.highestUnlocked);
        merged.highestUnlocked = 9;
        Assert.AreEqual(5, p.highestUnlocked);                  // es una copia
    }
}
