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
            Assert.AreEqual(step.Cleared.Count - step.Created.Count, step.Spawns.Count);
        }
    }

    [Test]
    public void SameSeedAndMoves_ReplayToSameResult()
    {
        // Esto es lo que hará el servidor en el modo clasificatoria:
        // repetir los movimientos del jugador y comprobar los puntos.
        var player = new BoardLogic(Size, Size, Types, 2024);
        var moves = new List<(GridPos, GridPos)>();

        for (int i = 0; i < 100; i++)
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

    // ------------------------------------------------------------------
    // Especiales y combos
    // ------------------------------------------------------------------

    // Tableros de prueba 6x6: fondo sin líneas con (x + 2y) % 5 (tipos 0..4)
    // y el tipo 5 se usa para dibujar el escenario de cada test.
    private const int LayoutSize = 6;
    private const int LayoutTypes = 6;
    private const int C = 5;

    private static int[,] BackgroundTypes()
    {
        var types = new int[LayoutSize, LayoutSize];
        for (int x = 0; x < LayoutSize; x++)
        {
            for (int y = 0; y < LayoutSize; y++) types[x, y] = (x + 2 * y) % 5;
        }
        return types;
    }

    [Test]
    public void FourInLine_CreatesDrill_WhereThePlayerDropped()
    {
        int[,] types = BackgroundTypes();
        types[0, 0] = C; types[1, 0] = C; types[3, 0] = C;
        types[2, 1] = C;
        var board = BoardLogic.FromLayout(types, null, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(2, 1), new GridPos(2, 0));

        Assert.IsTrue(result.IsValid);
        CascadeStep first = result.Steps[0];
        Assert.AreEqual(1, first.Created.Count);
        Assert.AreEqual(SpecialKind.LineH, first.Created[0].Kind);
        Assert.AreEqual(new GridPos(2, 0), first.Created[0].Position);
        Assert.AreEqual(C, first.Created[0].Type);
    }

    [Test]
    public void FiveInLine_CreatesPrism()
    {
        int[,] types = BackgroundTypes();
        types[0, 0] = C; types[1, 0] = C; types[3, 0] = C; types[4, 0] = C;
        types[2, 1] = C;
        var board = BoardLogic.FromLayout(types, null, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(2, 1), new GridPos(2, 0));

        CascadeStep first = result.Steps[0];
        Assert.AreEqual(1, first.Created.Count);
        Assert.AreEqual(SpecialKind.Prism, first.Created[0].Kind);
        Assert.AreEqual(BoardLogic.Colorless, first.Created[0].Type);
    }

    [Test]
    public void LShape_CreatesBomb()
    {
        int[,] types = BackgroundTypes();
        types[0, 0] = C; types[1, 0] = C;
        types[2, 1] = C; types[2, 2] = C;
        types[3, 0] = C;
        var board = BoardLogic.FromLayout(types, null, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(3, 0), new GridPos(2, 0));

        CascadeStep first = result.Steps[0];
        Assert.AreEqual(1, first.Created.Count);
        Assert.AreEqual(SpecialKind.Bomb, first.Created[0].Kind);
        Assert.AreEqual(new GridPos(2, 0), first.Created[0].Position);
        Assert.AreEqual(5, first.Cleared.Count);
    }

    [Test]
    public void Drill_InMatch_ClearsWholeRow()
    {
        int[,] types = BackgroundTypes();
        types[0, 0] = C; types[1, 0] = C;
        types[2, 1] = C;
        var specials = new SpecialKind[LayoutSize, LayoutSize];
        specials[2, 1] = SpecialKind.LineH;
        var board = BoardLogic.FromLayout(types, specials, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(2, 1), new GridPos(2, 0));

        CascadeStep first = result.Steps[0];
        Assert.AreEqual(LayoutSize, first.Cleared.Count);
        Assert.AreEqual(1, first.Activations.Count);
        Assert.AreEqual(SpecialKind.LineH, first.Activations[0].Kind);
        foreach (GridPos pos in first.Cleared) Assert.AreEqual(0, pos.Y);
    }

    [Test]
    public void Prism_SwappedWithMineral_ClearsThatColor()
    {
        int[,] types = BackgroundTypes();
        var specials = new SpecialKind[LayoutSize, LayoutSize];
        specials[0, 0] = SpecialKind.Prism;
        var board = BoardLogic.FromLayout(types, specials, LayoutTypes, 1);

        int color = board.GetMineral(1, 0);
        int count = 0;
        for (int x = 0; x < LayoutSize; x++)
        {
            for (int y = 0; y < LayoutSize; y++)
            {
                if (board.GetMineral(x, y) == color) count++;
            }
        }

        // No hace falta que forme línea: el Prisma siempre es un movimiento válido.
        SwapResult result = board.TrySwap(new GridPos(0, 0), new GridPos(1, 0));

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.WasSpecialCombo);
        Assert.AreEqual(count + 1, result.Steps[0].Cleared.Count);
    }

    [Test]
    public void BombPlusBomb_Clears5x5_WithoutNeedingAMatch()
    {
        int[,] types = BackgroundTypes();
        var specials = new SpecialKind[LayoutSize, LayoutSize];
        specials[2, 2] = SpecialKind.Bomb;
        specials[3, 2] = SpecialKind.Bomb;
        var board = BoardLogic.FromLayout(types, specials, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(2, 2), new GridPos(3, 2));

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(25, result.Steps[0].Cleared.Count);
    }

    [Test]
    public void DrillPlusDrill_ClearsCross()
    {
        int[,] types = BackgroundTypes();
        var specials = new SpecialKind[LayoutSize, LayoutSize];
        specials[2, 2] = SpecialKind.LineH;
        specials[3, 2] = SpecialKind.LineV;
        var board = BoardLogic.FromLayout(types, specials, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(2, 2), new GridPos(3, 2));

        Assert.IsTrue(result.IsValid);
        // Fila 2 completa + columna 3 completa (la celda del cruce cuenta una vez).
        Assert.AreEqual(LayoutSize * 2 - 1, result.Steps[0].Cleared.Count);
    }

    [Test]
    public void PrismPlusPrism_ClearsWholeBoard()
    {
        int[,] types = BackgroundTypes();
        var specials = new SpecialKind[LayoutSize, LayoutSize];
        specials[2, 2] = SpecialKind.Prism;
        specials[2, 3] = SpecialKind.Prism;
        var board = BoardLogic.FromLayout(types, specials, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(2, 2), new GridPos(2, 3));

        Assert.AreEqual(LayoutSize * LayoutSize, result.Steps[0].Cleared.Count);
    }

    [Test]
    public void NormalMineralNextToDrill_WithoutMatch_IsInvalid()
    {
        int[,] types = BackgroundTypes();
        var specials = new SpecialKind[LayoutSize, LayoutSize];
        specials[2, 2] = SpecialKind.LineH;
        var board = BoardLogic.FromLayout(types, specials, LayoutTypes, 1);

        SwapResult result = board.TrySwap(new GridPos(2, 2), new GridPos(3, 2));

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(SpecialKind.LineH, board.GetSpecial(2, 2));
    }

    [Test]
    public void CascadePoints_AreMultipliedByLevel([Values(1, 2, 3, 42, 999)] int seed)
    {
        var board = new BoardLogic(Size, Size, Types, seed);

        for (int i = 0; i < 40; i++)
        {
            Assert.IsTrue(board.TryFindMove(out GridPos a, out GridPos b));
            SwapResult result = board.TrySwap(a, b);

            int total = 0;
            for (int s = 0; s < result.Steps.Count; s++)
            {
                CascadeStep step = result.Steps[s];
                Assert.AreEqual(s + 1, step.Level);
                Assert.AreEqual(step.Cleared.Count * BoardLogic.PointsPerMineral * step.Level, step.Points);
                total += step.Points;
            }
            Assert.AreEqual(total, result.PointsGained);
        }
    }

    private static void AssertBoardIsFullAndInRange(BoardLogic board)
    {
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                int type = board.GetMineral(x, y);
                if (board.GetSpecial(x, y) == SpecialKind.Prism)
                {
                    Assert.AreEqual(BoardLogic.Colorless, type);
                    continue;
                }
                Assert.GreaterOrEqual(type, 0);
                Assert.Less(type, board.MineralTypeCount);
            }
        }
    }

    private static string Snapshot(BoardLogic board)
    {
        var chars = new char[board.Width * board.Height * 2];
        int i = 0;
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int type = board.GetMineral(x, y);
                chars[i++] = type < 0 ? '*' : (char)('0' + type);
                chars[i++] = (char)('0' + (int)board.GetSpecial(x, y));
            }
        }
        return new string(chars);
    }
}
