using CrystalClash.Story.Logic;
using NUnit.Framework;

public class WalletTests
{
    private const long T0 = 1_800_000_000;

    [Test]
    public void Lives_StartFull_AndAreLostOneByOne()
    {
        var p = new PlayerProgress();
        Assert.AreEqual(PlayerProgress.MaxLives, p.GetLives(T0));
        Assert.AreEqual(0, p.SecondsToNextLife(T0));

        Assert.IsTrue(p.LoseLife(T0));
        Assert.AreEqual(PlayerProgress.MaxLives - 1, p.GetLives(T0));
        Assert.AreEqual(PlayerProgress.LifeRegenSeconds, p.SecondsToNextLife(T0));
    }

    [Test]
    public void Lives_Regenerate_OverTime()
    {
        var p = new PlayerProgress();
        for (int i = 0; i < 5; i++) Assert.IsTrue(p.LoseLife(T0));
        Assert.IsFalse(p.LoseLife(T0));
        Assert.AreEqual(0, p.GetLives(T0));

        int r = PlayerProgress.LifeRegenSeconds;
        Assert.AreEqual(0, p.GetLives(T0 + r - 1));
        Assert.AreEqual(1, p.GetLives(T0 + r));
        Assert.AreEqual(1, p.SecondsToNextLife(T0 + 2 * r - 1));
        Assert.AreEqual(3, p.GetLives(T0 + 3 * r + 10));
        Assert.AreEqual(PlayerProgress.MaxLives, p.GetLives(T0 + 100 * r));
        Assert.AreEqual(0, p.SecondsToNextLife(T0 + 100 * r));
    }

    [Test]
    public void Lives_LosingAfterRegen_KeepsTheClockRunning()
    {
        var p = new PlayerProgress();
        int r = PlayerProgress.LifeRegenSeconds;
        p.LoseLife(T0);               // 4, reloj desde T0
        p.LoseLife(T0 + r / 2);       // 3, el reloj sigue desde T0
        Assert.AreEqual(4, p.GetLives(T0 + r));
        Assert.AreEqual(r, p.SecondsToNextLife(T0 + r));
    }

    [Test]
    public void Refill_FillsLives()
    {
        var p = new PlayerProgress();
        p.LoseLife(T0); p.LoseLife(T0);
        p.RefillLives(T0 + 5);
        Assert.AreEqual(PlayerProgress.MaxLives, p.GetLives(T0 + 5));
    }

    [Test]
    public void Coins_AndGems_CanBeSpentOnlyIfEnough()
    {
        var p = new PlayerProgress();
        Assert.AreEqual(PlayerProgress.StartingCoins, p.coins);
        Assert.IsTrue(p.TrySpendCoins(PlayerProgress.StartingCoins));
        Assert.IsFalse(p.TrySpendCoins(1));
        Assert.IsFalse(p.TrySpendGems(1));
        p.gems = 25;
        Assert.IsTrue(p.TrySpendGems(PlayerProgress.RefillLivesPrice));
        Assert.AreEqual(5, p.gems);
    }

    [Test]
    public void CoinsForWin_GrowWithStars()
    {
        Assert.AreEqual(75, PlayerProgress.CoinsForWin(1));
        Assert.AreEqual(125, PlayerProgress.CoinsForWin(3));
        Assert.AreEqual(125, PlayerProgress.CoinsForWin(9));
    }
}
