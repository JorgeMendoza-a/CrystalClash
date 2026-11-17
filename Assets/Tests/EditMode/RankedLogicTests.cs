using System.Collections.Generic;
using CrystalClash.Board.Logic;
using CrystalClash.Ranked.Logic;
using NUnit.Framework;

public class RankedLogicTests
{
    [Test]
    public void Tiers_MatchTheDesignTable()
    {
        Assert.AreEqual("Bronce", RankTiers.ForPoints(0).Name);
        Assert.AreEqual("Bronce", RankTiers.ForPoints(499).Name);
        Assert.AreEqual("Plata", RankTiers.ForPoints(500).Name);
        Assert.AreEqual("Oro I", RankTiers.ForPoints(1000).Name);
        Assert.AreEqual("Oro II", RankTiers.ForPoints(1500).Name);
        Assert.AreEqual("Platino I", RankTiers.ForPoints(2999).Name);
        Assert.AreEqual("Platino II", RankTiers.ForPoints(3000).Name);
        Assert.AreEqual("Diamante I", RankTiers.ForPoints(4000).Name);
        Assert.AreEqual("Diamante II", RankTiers.ForPoints(5999).Name);
        Assert.AreEqual("Maestro II", RankTiers.ForPoints(7999).Name);
        Assert.AreEqual("Campeón", RankTiers.ForPoints(8000).Name);
        Assert.AreEqual("Campeón", RankTiers.ForPoints(50000).Name);
        Assert.AreEqual("Bronce", RankTiers.ForPoints(-10).Name);
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
    public void PointsChange_EqualPlayers_IsSymmetric()
    {
        int win = RankedRules.PointsChange(1000, 1000, MatchOutcome.Win);
        int loss = RankedRules.PointsChange(1000, 1000, MatchOutcome.Loss);

        Assert.AreEqual(20, win);
        Assert.AreEqual(-20, loss);
        Assert.AreEqual(0, RankedRules.PointsChange(1000, 1000, MatchOutcome.Draw));
    }

    [Test]
    public void PointsChange_BeatingStrongerPlayer_GivesMore()
    {
        int vsStronger = RankedRules.PointsChange(1000, 1600, MatchOutcome.Win);
        int vsWeaker = RankedRules.PointsChange(1600, 1000, MatchOutcome.Win);

        Assert.Greater(vsStronger, 30);
        Assert.AreEqual(RankedRules.MinWinPoints, vsWeaker);
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
