using System.Collections.Generic;
using CrystalClash.Board.Logic;
using NUnit.Framework;

public class BoardLogicTests
{
    private const int Size = 8;
    private const int Types = 6;

    [Test]
    public void SameSeed_ProducesSameBoard()
    {
        var a = new BoardLogic(Size, Size, Types, 12345);
        var b = new BoardLogic(Size, Size, Types, 12345);

        Assert.AreEqual(Snapshot(a), Snapshot(b));
    }

    [Test]
    public void DifferentSeeds_ProduceDifferentBoards()
    {
        var a = new BoardLogic(Size, Size, Types, 1);
        var b = new BoardLogic(Size, Size, Types, 2);

        Assert.AreNotEqual(Snapshot(a), Snapshot(b));
    }

    [Test]
    public void InitialBoard_HasNoMatches_AndHasMoves([Values(1, 2, 3, 42, 999, -7)] int seed)
    {
        var board = new BoardLogic(Size, Size, Types, seed);

        Assert.AreEqual(0, board.FindMatches().Count);
        Assert.IsTrue(board.HasAvailableMoves());
        AssertBoardIsFullAndInRange(board);
    }

    [Test]
    public void InitialBoard_WorksWithMinimumTypes()
    {
        var board = new BoardLogic(Size, Size, BoardLogic.MinMineralTypes, 77);

        Assert.AreEqual(0, board.FindMatches().Count);
        Assert.IsTrue(board.HasAvailableMoves());
    }

    [Test]
    public void NonAdjacentSwap_IsInvalid_AndDoesNotChangeBoard()
    {
        var board = new BoardLogic(Size, Size, Types, 5);
        string before = Snapshot(board);

        SwapResult result = board.TrySwap(new GridPos(0, 0), new GridPos(2, 0));

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(before, Snapshot(board));
        Assert.AreEqual(0, board.MovesMade);
        Assert.AreEqual(0, board.Score);
    }

    [Test]
    public void SwapWithoutMatch_IsInvalid_AndDoesNotChangeBoard()
    {
        var board = new BoardLogic(Size, Size, Types, 5);
        string before = Snapshot(board);

        bool found = false;
        for (int x = 0; x < Size - 1 && !found; x++)
        {
            for (int y = 0; y < Size && !found; y++)
            {
                var a = new GridPos(x, y);
                var b = new GridPos(x + 1, y);
                if (board.GetMineral(a) == board.GetMineral(b)) continue;

                // Probamos en una copia con la misma semilla para no tocar 'board'.
                var probe = new BoardLogic(Size, Size, Types, 5);
                if (probe.TrySwap(a, b).IsValid) continue;

                found = true;
                SwapResult result = board.TrySwap(a, b);
                Assert.IsFalse(result.IsValid);
            }
        }

        Assert.IsTrue(found, "No se encontró un intercambio inválido para probar");
        Assert.AreEqual(before, Snapshot(board));
    }

    [Test]
    public void ValidSwap_ClearsScoresAndLeavesStableFullBoard([Values(1, 2, 3, 42, 999)] int seed)
    {
        var board = new BoardLogic(Size, Size, Types, seed);
        Assert.IsTrue(board.TryFindMove(out GridPos a, out GridPos b));

        SwapResult result = board.TrySwap(a, b);

        Assert.IsTrue(result.IsValid);
        Assert.Greater(result.Steps.Count, 0);
        Assert.Greater(result.PointsGained, 0);
        Assert.AreEqual(result.PointsGained, board.Score);
        Assert.AreEqual(1, board.MovesMade);
        Assert.AreEqual(0, board.FindMatches().Count);
        Assert.IsTrue(board.HasAvailableMoves());
        AssertBoardIsFullAndInRange(board);

        foreach (CascadeStep step in result.Steps)
        {
            Assert.GreaterOrEqual(step.Cleared.Count, 3);
            Assert.AreEqual(step.Cleared.Count, step.Spawns.Count);
            Assert.AreEqual(step.Cleared.Count * BoardLogic.PointsPerMineral, step.Points);
        }
    }

    [Test]
    public void SameSeedAndMoves_ReplayToSameResult()
    {
        // Esto es lo que hará el servidor en el modo clasificatoria:
        // repetir los movimientos del jugador y comprobar los puntos.
        var player = new BoardLogic(Size, Size, Types, 2024);
        var moves = new List<(GridPos, GridPos)>();

        for (int i = 0; i < 30; i++)
        {
            Assert.IsTrue(player.TryFindMove(out GridPos a, out GridPos b));
            Assert.IsTrue(player.TrySwap(a, b).IsValid);
            moves.Add((a, b));
        }

        var server = new BoardLogic(Size, Size, Types, 2024);
        foreach (var move in moves)
        {
            Assert.IsTrue(server.TrySwap(move.Item1, move.Item2).IsValid);
        }

        Assert.AreEqual(player.Score, server.Score);
        Assert.AreEqual(player.MovesMade, server.MovesMade);
        Assert.AreEqual(Snapshot(player), Snapshot(server));
    }

    [Test]
    public void DeterministicRandom_IsStable()
    {
        // Valores fijos: si alguien cambia el algoritmo, este test avisa,
        // porque romperia la compatibilidad con partidas/servidor existentes.
        var rng = new DeterministicRandom(42);
        uint[] expected = { 2581720956u, 1925393290u, 3661312704u };

        foreach (uint value in expected)
        {
            Assert.AreEqual(value, rng.NextUInt());
        }
    }

    private static void AssertBoardIsFullAndInRange(BoardLogic board)
    {
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                int type = board.GetMineral(x, y);
                Assert.GreaterOrEqual(type, 0);
                Assert.Less(type, board.MineralTypeCount);
            }
        }
    }

    private static string Snapshot(BoardLogic board)
    {
        var chars = new char[board.Width * board.Height];
        int i = 0;
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int type = board.GetMineral(x, y);
                chars[i++] = type == BoardLogic.Empty ? '.' : (char)('0' + type);
            }
        }
        return new string(chars);
    }
}
