using System.Collections.Generic;
using CrystalClash.Board.Logic;
using CrystalClash.Ranked.Logic;
using NUnit.Framework;

public class RankedLogicTests
{
    [Test]
    public void Tiers_HaveFiveDivisionsPerLeague()
    {
        Assert.AreEqual("Bronce 1", RankTiers.ForPoints(0).Name);
        Assert.AreEqual("Bronce 1", RankTiers.ForPoints(99).Name);
        Assert.AreEqual("Bronce 2", RankTiers.ForPoints(100).Name);
        Assert.AreEqual("Bronce 5", RankTiers.ForPoints(499).Name);
        Assert.AreEqual("Plata 1", RankTiers.ForPoints(500).Name);
        Assert.AreEqual("Plata 5", RankTiers.ForPoints(999).Name);
        Assert.AreEqual("Oro 1", RankTiers.ForPoints(1000).Name);
        Assert.AreEqual("Oro 2", RankTiers.ForPoints(1200).Name);
        Assert.AreEqual("Platino 1", RankTiers.ForPoints(2000).Name);
        Assert.AreEqual("Platino 2", RankTiers.ForPoints(2400).Name);
        Assert.AreEqual("Diamante 5", RankTiers.ForPoints(5999).Name);
        Assert.AreEqual("Maestro 3", RankTiers.ForPoints(6800).Name);
        Assert.AreEqual("Campeón", RankTiers.ForPoints(8000).Name);
        Assert.AreEqual("Campeón", RankTiers.ForPoints(50000).Name);
        Assert.AreEqual("Bronce 1", RankTiers.ForPoints(-10).Name);

        // 6 ligas x 5 divisiones + Campeón
        Assert.AreEqual(31, RankTiers.All.Count);
    }

    [Test]
    public void Tiers_AreContiguous_AndNextWorks()
    {
        IReadOnlyList<RankTier> all = RankTiers.All;
        Assert.AreEqual(0, all[0].MinPoints);
        for (int i = 1; i < all.Count; i++)
        {
            Assert.AreEqual(all[i - 1].MaxPoints + 1, all[i].MinPoints);
            Assert.AreEqual(all[i], RankTiers.Next(all[i - 1]));
        }
        Assert.IsNull(RankTiers.Next(all[all.Count - 1]));
    }

    [Test]
    public void PointsChange_MatchesTheLeagueTable()
    {
        // Contra un rival de tus mismos puntos (base Elo: +20 / -20).
        Assert.AreEqual(60, RankedRules.PointsChange(200, 200, MatchOutcome.Win));     // Bronce x2
        Assert.AreEqual(-10, RankedRules.PointsChange(200, 200, MatchOutcome.Loss));   // Bronce x0.5
        Assert.AreEqual(48, RankedRules.PointsChange(700, 700, MatchOutcome.Win));     // Plata
        Assert.AreEqual(39, RankedRules.PointsChange(1500, 1500, MatchOutcome.Win));   // Oro
        Assert.AreEqual(-15, RankedRules.PointsChange(1500, 1500, MatchOutcome.Loss));
        Assert.AreEqual(30, RankedRules.PointsChange(3000, 3000, MatchOutcome.Win));   // Platino
        Assert.AreEqual(24, RankedRules.PointsChange(5000, 5000, MatchOutcome.Win));   // Diamante
        Assert.AreEqual(-20, RankedRules.PointsChange(5000, 5000, MatchOutcome.Loss));
        Assert.AreEqual(18, RankedRules.PointsChange(7000, 7000, MatchOutcome.Win));   // Maestro
        Assert.AreEqual(15, RankedRules.PointsChange(9000, 9000, MatchOutcome.Win));   // Campeón
        Assert.AreEqual(0, RankedRules.PointsChange(3000, 3000, MatchOutcome.Draw));
    }

    [Test]
    public void PointsChange_LowRanksClimbFaster()
    {
        int bronze = RankedRules.PointsChange(200, 200, MatchOutcome.Win);
        int gold = RankedRules.PointsChange(1500, 1500, MatchOutcome.Win);
        int diamond = RankedRules.PointsChange(5000, 5000, MatchOutcome.Win);
        Assert.Greater(bronze, gold);
        Assert.Greater(gold, diamond);

        int bronzeLoss = -RankedRules.PointsChange(200, 200, MatchOutcome.Loss);
        int diamondLoss = -RankedRules.PointsChange(5000, 5000, MatchOutcome.Loss);
        Assert.Less(bronzeLoss, diamondLoss);
    }

    [Test]
    public void PointsChange_BeatingStrongerPlayer_GivesMore()
    {
        int vsStronger = RankedRules.PointsChange(2000, 2600, MatchOutcome.Win);
        int vsEqual = RankedRules.PointsChange(2000, 2000, MatchOutcome.Win);
        int vsWeaker = RankedRules.PointsChange(2600, 2000, MatchOutcome.Win);

        Assert.Greater(vsStronger, vsEqual);
        Assert.Greater(vsEqual, vsWeaker);
        Assert.GreaterOrEqual(vsWeaker, 1);
    }

    [Test]
    public void PointsChange_NeverGoesBelowZero()
    {
        Assert.AreEqual(-5, RankedRules.PointsChange(5, 5, MatchOutcome.Loss));
        Assert.AreEqual(0, RankedRules.PointsChange(0, 0, MatchOutcome.Loss));
    }

    [Test]
    public void Moves_EncodeAndDecode()
    {
        var moves = new List<(GridPos, GridPos)> { (new GridPos(0, 1), new GridPos(1, 1)), (new GridPos(7, 6), new GridPos(7, 7)) };

        string text = RankedRules.EncodeMoves(moves);
        Assert.AreEqual("0,1,1,1;7,6,7,7", text);

        List<(GridPos a, GridPos b)> back = RankedRules.DecodeMoves(text);
        Assert.AreEqual(2, back.Count);
        Assert.AreEqual(new GridPos(7, 7), back[1].b);
    }

    [Test]
    public void Replay_ReproducesTheScore()
    {
        BoardLogic board = RankedRules.CreateBoard(555);
        var moves = new List<(GridPos, GridPos)>();
        for (int i = 0; i < RankedRules.Moves; i++)
        {
            Assert.IsTrue(board.TryFindMove(out GridPos a, out GridPos b));
            board.TrySwap(a, b);
            moves.Add((a, b));
        }

        Assert.IsTrue(RankedRules.TryReplay(555, RankedRules.EncodeMoves(moves), out int score, out string error), error);
        Assert.AreEqual(board.Score, score);
    }

    [Test]
    public void Replay_RejectsCheats()
    {
        Assert.IsFalse(RankedRules.TryReplay(1, "0,0,5,5", out _, out _));       // no adyacente
        Assert.IsFalse(RankedRules.TryReplay(1, "basura", out _, out _));         // mal formado

        var tooMany = new List<(GridPos, GridPos)>();
        BoardLogic board = RankedRules.CreateBoard(2);
        for (int i = 0; i < RankedRules.Moves + 1; i++)
        {
            board.TryFindMove(out GridPos a, out GridPos b);
            board.TrySwap(a, b);
            tooMany.Add((a, b));
        }
        Assert.IsFalse(RankedRules.TryReplay(2, RankedRules.EncodeMoves(tooMany), out _, out _));

        Assert.IsTrue(RankedRules.TryReplay(2, "", out int empty, out _));
        Assert.AreEqual(0, empty);
    }
}
