using System;
using System.Collections.Generic;
using CrystalClash.Ranked.Logic;
using NUnit.Framework;

public class WeeklyRulesTests
{
    [Test]
    public void Week_StartsOnMondayUtc_AndContainsNow()
    {
        long[] samples = { 1795264260L, 1795400000L, 1800000000L, 1767225600L };
        foreach (long now in samples)
        {
            int week = WeeklyRules.Week(now);
            long start = WeeklyRules.WeekStartUtc(week);
            Assert.IsTrue(start <= now && now < WeeklyRules.WeekEndUtc(week));
            Assert.AreEqual(DayOfWeek.Monday, DateTimeOffset.FromUnixTimeSeconds(start).DayOfWeek);
            Assert.AreEqual(7L * 86400L, WeeklyRules.WeekEndUtc(week) - start);
        }
    }

    [Test]
    public void Points_WinThreeDrawOneLossZero()
    {
        var e = new WeeklyEntry();
        WeeklyRules.Add(e, MatchOutcome.Win);
        WeeklyRules.Add(e, MatchOutcome.Draw);
        WeeklyRules.Add(e, MatchOutcome.Loss);
        Assert.AreEqual(4, e.Points);
        Assert.AreEqual(1, e.Wins);
        Assert.AreEqual(1, e.Draws);
        Assert.AreEqual(1, e.Losses);
    }

    [Test]
    public void Ranking_PointsThenWins_AndRewards()
    {
        var entries = new Dictionary<string, WeeklyEntry>
        {
            { "a", new WeeklyEntry { Points = 9, Wins = 3 } },
            { "b", new WeeklyEntry { Points = 9, Wins = 2, Draws = 3 } },
            { "c", new WeeklyEntry { Points = 12, Wins = 4 } },
        };
        Assert.AreEqual(1, WeeklyRules.RankOf(entries, "c"));
        Assert.AreEqual(2, WeeklyRules.RankOf(entries, "a"));   // empate a 9: más victorias
        Assert.AreEqual(3, WeeklyRules.RankOf(entries, "b"));
        Assert.AreEqual(0, WeeklyRules.RankOf(entries, "x"));   // no jugó

        Assert.IsNull(WeeklyRules.RewardFor(0, 10));
        Assert.AreEqual(100, WeeklyRules.RewardFor(1, 10).Gems);
        Assert.AreEqual(20, WeeklyRules.RewardFor(7, 10).Gems);
        Assert.AreEqual(5, WeeklyRules.RewardFor(50, 10).Gems);
        Assert.AreEqual((int)SeasonRewardKind.WeeklyEnd, WeeklyRules.RewardFor(2, 10).Kind);
    }
}
