using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using NUnit.Framework;

public class DailyEventsTests
{
    [Test]
    public void DailyReward_OncePerDay_AndStreakAdvances()
    {
        var p = new PlayerProgress();
        int coins = p.coins;
        Assert.AreEqual(0, DailyRewards.Claim(p, 100));            // día 1
        Assert.AreEqual(coins + 100, p.coins);
        Assert.AreEqual(-1, DailyRewards.Claim(p, 100));           // ya reclamado hoy
        Assert.AreEqual(1, DailyRewards.Claim(p, 101));            // día 2 (martillo)
        Assert.AreEqual(PlayerProgress.StartingBoosters + 1, p.GetBoosters(BoosterKind.Hammer));
        Assert.AreEqual(2, DailyRewards.ClaimedInCycle(p, 101));
    }

    [Test]
    public void DailyReward_MissingADay_ResetsStreak()
    {
        var p = new PlayerProgress();
        DailyRewards.Claim(p, 100);
        DailyRewards.Claim(p, 101);
        Assert.AreEqual(0, DailyRewards.NextIndex(p, 103));        // se saltó el 102
    }

    [Test]
    public void DailyReward_AfterDaySeven_StartsAgain()
    {
        var p = new PlayerProgress();
        for (int d = 0; d < 7; d++) DailyRewards.Claim(p, 200 + d);
        Assert.AreEqual(7, DailyRewards.ClaimedInCycle(p, 206));
        Assert.AreEqual(0, DailyRewards.Claim(p, 207));            // vuelve al día 1
    }

    [Test]
    public void Missions_SameDaySamePick_DistinctGroups()
    {
        int[] a = DailyMissions.Pick(500);
        int[] b = DailyMissions.Pick(500);
        Assert.AreEqual(DailyMissions.Count, a.Length);
        for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i]);
        for (int day = 0; day < 200; day++)
        {
            int[] pick = DailyMissions.Pick(day);
            Assert.AreEqual(DailyMissions.Count, pick.Length);
            for (int i = 0; i < pick.Length; i++)
                for (int j = i + 1; j < pick.Length; j++)
                    Assert.AreNotEqual(DailyMissions.Pool[pick[i]].Group, DailyMissions.Pool[pick[j]].Group);
        }
    }

    [Test]
    public void Missions_ProgressClaimAndBonus()
    {
        var p = new PlayerProgress();
        DailyMissions.EnsureToday(p, 300);
        int gems = p.gems;
        int coins = p.coins;
        int expectedCoins = 0, expectedGems = DailyMissions.Bonus.Gems;
        for (int i = 0; i < DailyMissions.Count; i++)
        {
            MissionDef def = DailyMissions.Get(p, i);
            Assert.IsFalse(DailyMissions.CanClaim(p, i));
            DailyMissions.Add(p, 300, def.Type, def.Target + 5);   // no pasa del objetivo
            Assert.AreEqual(def.Target, p.missionProgress[i]);
            Assert.IsTrue(DailyMissions.Claim(p, i));
            Assert.IsFalse(DailyMissions.Claim(p, i));
            expectedCoins += def.Reward.Coins;
            expectedGems += def.Reward.Gems;
        }
        Assert.IsTrue(DailyMissions.ClaimBonus(p));
        Assert.IsFalse(DailyMissions.ClaimBonus(p));
        Assert.AreEqual(coins + expectedCoins, p.coins);
        Assert.AreEqual(gems + expectedGems, p.gems);
        Assert.AreEqual(0, DailyMissions.Claimable(p, 300));

        DailyMissions.EnsureToday(p, 301);                         // día nuevo: todo reinicia
        Assert.IsFalse(p.missionBonusClaimed);
        Assert.AreEqual(0, p.missionProgress[0]);
    }
}
