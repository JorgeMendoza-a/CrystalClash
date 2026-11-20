using System;
using System.Linq;
using CrystalClash.Ranked.Logic;
using NUnit.Framework;

public class SeasonRulesTests
{
    private static long Utc(int y, int m, int d) => new DateTimeOffset(y, m, d, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    [Test]
    public void Seasons_Last60Days()
    {
        Assert.AreEqual(1, SeasonRules.CurrentSeason(Utc(2026, 11, 1)));
        Assert.AreEqual(1, SeasonRules.CurrentSeason(Utc(2026, 12, 30)));   // día 59
        Assert.AreEqual(2, SeasonRules.CurrentSeason(Utc(2026, 12, 31)));   // día 60
        Assert.AreEqual(1, SeasonRules.CurrentSeason(Utc(2025, 1, 1)));     // antes del inicio
        Assert.AreEqual(SeasonRules.SeasonStartUtc(2), SeasonRules.SeasonEndUtc(1));
    }

    [Test]
    public void SoftReset_KeepsOrder_AndChampionsDontDropToBronze()
    {
        Assert.AreEqual(0, SeasonRules.SoftReset(0));
        Assert.AreEqual(4000, SeasonRules.SoftReset(8000));
        Assert.AreEqual("Diamante 1", RankTiers.ForPoints(SeasonRules.SoftReset(8000)).Name);
        Assert.AreEqual("Oro 1", RankTiers.ForPoints(SeasonRules.SoftReset(2000)).Name);

        int previous = -1;
        for (int p = 0; p <= 12000; p += 37)
        {
            int reset = SeasonRules.SoftReset(p);
            Assert.GreaterOrEqual(reset, previous);
            previous = reset;
        }
    }

    [Test]
    public void RankUp_GivesRewardPerNewDivision_AndBadgePerLeague()
    {
        var rewards = SeasonRules.RewardsForPeak(450, 620, 1);   // Bronce 5 → Plata 2
        Assert.AreEqual(2, rewards.Count);
        Assert.AreEqual("Plata 1", rewards[0].TierName);
        Assert.AreEqual("s1_plata", rewards[0].Badge);
        Assert.Greater(rewards[0].Coins, rewards[1].Coins);
        Assert.AreEqual("Plata 2", rewards[1].TierName);
        Assert.AreEqual("", rewards[1].Badge);

        Assert.AreEqual(0, SeasonRules.RewardsForPeak(620, 600, 1).Count);   // bajar no da nada
        var champion = SeasonRules.RewardsForPeak(7990, 8010, 3);
        Assert.AreEqual("s3_campeon", champion.Single().Badge);
    }

    [Test]
    public void SeasonEnd_RewardUsesFinalRank()
    {
        SeasonReward r = SeasonRules.SeasonEndReward(1500, 1);
        Assert.AreEqual((int)SeasonRewardKind.SeasonEnd, r.Kind);
        Assert.AreEqual(200, r.Gems);
        Assert.AreEqual(2000, r.Coins);
        Assert.AreEqual(750, r.NewSeasonPoints);
    }

    [Test]
    public void Badges_Parse()
    {
        Assert.IsTrue(SeasonRules.TryParseBadge("s12_campeon", out int season, out int league));
        Assert.AreEqual(12, season);
        Assert.AreEqual(6, league);
        Assert.IsFalse(SeasonRules.TryParseBadge("basura", out _, out _));
    }
}
