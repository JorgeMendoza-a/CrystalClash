using System.Collections.Generic;
using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using NUnit.Framework;

public class BoosterTests
{
    private const int Size = 6;

    // Fondo sin líneas: (x + 2y) % 5, igual que en BoardLogicTests.
    private static BoardLogic Board(SpecialKind[,] specials = null)
    {
        var types = new int[Size, Size];
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++) types[x, y] = (x + 2 * y) % 5;
        }
        return BoardLogic.FromLayout(types, specials, 6, 1);
    }

    [Test]
    public void Hammer_BreaksOneMineral_AndDoesNotCountAsMove()
    {
        BoardLogic board = Board();

        SwapResult result = board.UseBooster(BoosterKind.Hammer, new GridPos(2, 2));

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(1, result.Steps[0].Cleared.Count);
        Assert.AreEqual(new GridPos(2, 2), result.Steps[0].Cleared[0]);
        Assert.AreEqual(0, board.MovesMade);
        Assert.AreEqual(result.PointsGained, board.Score);
    }

    [Test]
    public void Bomb_Breaks3x3_OrLessInACorner()
    {
        Assert.AreEqual(9, Board().UseBooster(BoosterKind.Bomb, new GridPos(2, 2)).Steps[0].Cleared.Count);
        Assert.AreEqual(4, Board().UseBooster(BoosterKind.Bomb, new GridPos(0, 0)).Steps[0].Cleared.Count);
    }

    [Test]
    public void Lightning_BreaksRowAndColumn()
    {
        SwapResult result = Board().UseBooster(BoosterKind.Lightning, new GridPos(1, 4));

        Assert.AreEqual(Size * 2 - 1, result.Steps[0].Cleared.Count);
        foreach (GridPos pos in result.Steps[0].Cleared)
        {
            Assert.IsTrue(pos.X == 1 || pos.Y == 4);
        }
    }

    [Test]
    public void Hammer_OnSpecial_ActivatesIt()
    {
        var specials = new SpecialKind[Size, Size];
        specials[3, 3] = SpecialKind.LineV;

        SwapResult result = Board(specials).UseBooster(BoosterKind.Hammer, new GridPos(3, 3));

        Assert.AreEqual(Size, result.Steps[0].Cleared.Count);
        Assert.AreEqual(1, result.Steps[0].Activations.Count);
    }

    [Test]
    public void Shuffle_KeepsTheSameMinerals_AndLeavesAPlayableBoard()
    {
        var board = new BoardLogic(8, 8, 6, 9);
        int[] before = CountTypes(board);

        SwapResult result = board.UseBooster(BoosterKind.Shuffle, default(GridPos));

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.WasShuffled);
        Assert.AreEqual(0, result.PointsGained);
        CollectionAssertEqual(before, CountTypes(board));
        Assert.AreEqual(0, board.FindMatches().Count);
        Assert.IsTrue(board.HasAvailableMoves());
    }

    [Test]
    public void Booster_OutsideTheBoard_IsInvalid()
    {
        Assert.IsFalse(Board().UseBooster(BoosterKind.Bomb, new GridPos(-1, 0)).IsValid);
    }

    [Test]
    public void Boosters_AreDeterministic()
    {
        var a = new BoardLogic(8, 8, 6, 77);
        var b = new BoardLogic(8, 8, 6, 77);

        foreach (BoardLogic board in new[] { a, b })
        {
            board.UseBooster(BoosterKind.Bomb, new GridPos(3, 3));
            board.UseBooster(BoosterKind.Lightning, new GridPos(5, 1));
            board.UseBooster(BoosterKind.Shuffle, default(GridPos));
            board.UseBooster(BoosterKind.Hammer, new GridPos(0, 7));
        }

        Assert.AreEqual(a.Score, b.Score);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Assert.AreEqual(a.GetMineral(x, y), b.GetMineral(x, y));
                Assert.AreEqual(a.GetSpecial(x, y), b.GetSpecial(x, y));
            }
        }
    }

    [Test]
    public void Session_Booster_AddsScoreButKeepsMoves()
    {
        var level = new LevelDefinition { Number = 1, MaxMoves = 10, StarScores = new[] { 1, 2, 3 } };
        level.Objectives.Add(LevelObjective.Score(999999));
        var session = new LevelSession(level);
        var board = new BoardLogic(8, 8, 6, 4);

        SwapResult result = board.UseBooster(BoosterKind.Bomb, new GridPos(4, 4));
        session.ApplyBooster(result);

        Assert.AreEqual(10, session.MovesLeft);
        Assert.AreEqual(result.PointsGained, session.Score);
    }

    [Test]
    public void Progress_BoosterInventory()
    {
        var progress = new PlayerProgress();
        Assert.AreEqual(PlayerProgress.StartingBoosters, progress.GetBoosters(BoosterKind.Hammer));

        for (int i = 0; i < PlayerProgress.StartingBoosters; i++)
        {
            Assert.IsTrue(progress.TryUseBooster(BoosterKind.Hammer));
        }
        Assert.IsFalse(progress.TryUseBooster(BoosterKind.Hammer));
        Assert.AreEqual(0, progress.GetBoosters(BoosterKind.Hammer));

        progress.AddBooster(BoosterKind.Hammer, 2);
        Assert.AreEqual(2, progress.GetBoosters(BoosterKind.Hammer));

        // Guardados viejos sin el campo: se rellenan con la cantidad inicial.
        progress.boosters = new int[0];
        Assert.AreEqual(PlayerProgress.StartingBoosters, progress.GetBoosters(BoosterKind.Shuffle));
    }

    private static int[] CountTypes(BoardLogic board)
    {
        var counts = new int[board.MineralTypeCount + 2];
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++) counts[board.GetMineral(x, y) + 2]++;
        }
        return counts;
    }

    private static void CollectionAssertEqual(int[] expected, int[] actual)
    {
        Assert.AreEqual(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], actual[i]);
    }
}
