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

    [Test]
    public void Merge_KeepsEverythingBought()
    {
        var phone = new PlayerProgress { savedUtc = 10L };
        phone.AddCosmetic("e_dragon_fuego");
        var cloud = new PlayerProgress { savedUtc = 200L };
        cloud.AddCosmetic("b_volcan_ardiente");
        cloud.AddCosmetic("e_dragon_fuego");

        PlayerProgress merged = PlayerProgress.Merge(phone, cloud);
        Assert.IsTrue(merged.OwnsCosmetic("e_dragon_fuego"));
        Assert.IsTrue(merged.OwnsCosmetic("b_volcan_ardiente"));
        Assert.AreEqual(2, merged.ownedCosmetics.Count);
    }

    [Test]
    public void AddLife_UpToTheMaximum_KeepsTheTimer()
    {
        var p = new PlayerProgress();
        p.LoseLife(1000L);
        p.LoseLife(1000L);                                   // 3 vidas, cuenta desde 1000
        Assert.IsTrue(p.AddLife(1100L));
        Assert.AreEqual(4, p.GetLives(1100L));
        Assert.AreEqual(PlayerProgress.LifeRegenSeconds - 100, p.SecondsToNextLife(1100L)); // el reloj sigue
        Assert.IsTrue(p.AddLife(1200L));
        Assert.AreEqual(PlayerProgress.MaxLives, p.GetLives(1200L));
        Assert.IsFalse(p.AddLife(1300L));                    // ya estaban llenas
    }
}
