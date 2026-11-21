using System;
using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using NUnit.Framework;

public class StoryLogicTests
{
    private static LevelDefinition Level(int moves, params LevelObjective[] objectives)
    {
        var level = new LevelDefinition
        {
            Number = 1,
            Name = "Test",
            MaxMoves = moves,
            MineralTypeCount = 6,
            StarScores = new[] { 100, 1000, 5000 }
        };
        level.Objectives.AddRange(objectives);
        return level;
    }

    /// <summary>Juega con el primer movimiento válido hasta que termine la sesión.</summary>
    private static void PlayUntilEnd(LevelSession session, BoardLogic board)
    {
        int guard = 0;
        while (session.CanPlay && guard++ < 1000)
        {
            Assert.IsTrue(board.TryFindMove(out GridPos a, out GridPos b));
            session.ApplyMove(board.TrySwap(a, b));
        }
    }

    [Test]
    public void ValidMove_SpendsOneMove_AndAddsScore()
    {
        var board = new BoardLogic(8, 8, 6, 3);
        var session = new LevelSession(Level(10, LevelObjective.Score(999999)));

        Assert.IsTrue(board.TryFindMove(out GridPos a, out GridPos b));
        SwapResult result = board.TrySwap(a, b);
        session.ApplyMove(result);

        Assert.AreEqual(9, session.MovesLeft);
        Assert.AreEqual(result.PointsGained, session.Score);
        Assert.AreEqual(SessionState.Playing, session.State);
    }

    [Test]
    public void InvalidMove_DoesNotSpendMoves()
    {
        var board = new BoardLogic(8, 8, 6, 3);
        var session = new LevelSession(Level(10, LevelObjective.Score(999999)));

        session.ApplyMove(board.TrySwap(new GridPos(0, 0), new GridPos(5, 5)));

        Assert.AreEqual(10, session.MovesLeft);
    }

    [Test]
    public void RunningOutOfMoves_WithoutObjectives_Loses()
    {
        var board = new BoardLogic(8, 8, 6, 3);
        var session = new LevelSession(Level(3, LevelObjective.Score(999999)));

        PlayUntilEnd(session, board);

        Assert.AreEqual(SessionState.Lost, session.State);
        Assert.AreEqual(0, session.MovesLeft);
        Assert.AreEqual(0, session.Stars);
        Assert.IsFalse(session.CanPlay);
    }

    [Test]
    public void ReachingScore_Wins_WithBonusForRemainingMoves()
    {
        var board = new BoardLogic(8, 8, 6, 3);
        var session = new LevelSession(Level(50, LevelObjective.Score(1)));

        PlayUntilEnd(session, board);

        Assert.AreEqual(SessionState.Won, session.State);
        Assert.AreEqual(49, session.MovesLeft);
        Assert.AreEqual(0, session.MoveBonus);

        // Movimientos sobrantes → bombas de bonus
        SwapResult finale = board.PlayFinale(session.MovesLeft);
        session.ApplyFinale(finale);
        Assert.AreEqual(49, session.FinaleBombs);
        Assert.Greater(session.MoveBonus, 0);
        Assert.AreEqual(finale.PointsGained, session.MoveBonus);
        Assert.AreEqual(session.Score + session.MoveBonus, session.FinalScore);
        Assert.GreaterOrEqual(session.Stars, 1);
    }

    [Test]
    public void CollectObjective_CountsOnlyThatMineral()
    {
        var board = new BoardLogic(8, 8, 6, 11);
        var session = new LevelSession(Level(40, LevelObjective.Collect(2, 5000)));

        int expected = 0;
        for (int i = 0; i < 15; i++)
        {
            Assert.IsTrue(board.TryFindMove(out GridPos a, out GridPos b));
            SwapResult result = board.TrySwap(a, b);
            foreach (CascadeStep step in result.Steps)
            {
                foreach (int type in step.ClearedTypes)
                {
                    if (type == 2) expected++;
                }
            }
            session.ApplyMove(result);
        }

        Assert.AreEqual(expected, session.Objectives[0].Current);
    }

    [Test]
    public void ObjectiveProgress_IsCappedAtTarget()
    {
        var board = new BoardLogic(8, 8, 3, 5);
        var session = new LevelSession(Level(40, LevelObjective.Collect(0, 3), LevelObjective.Score(999999)));

        PlayUntilEnd(session, board);

        Assert.AreEqual(3, session.Objectives[0].Current);
        Assert.IsTrue(session.Objectives[0].IsComplete);
    }

    [Test]
    public void Stars_FollowFinalScoreThresholds()
    {
        var board = new BoardLogic(8, 8, 6, 3);
        var level = Level(30, LevelObjective.Score(1));
        level.StarScores = new[] { 1, 1, 1 };
        var session = new LevelSession(level);

        PlayUntilEnd(session, board);

        Assert.AreEqual(3, session.Stars);
    }

    [Test]
    public void Finale_TurnsMovesIntoBombs_AndLeavesStableBoard()
    {
        var board = new BoardLogic(8, 8, 6, 21);
        SwapResult finale = board.PlayFinale(7);

        Assert.AreEqual(7, finale.FinaleBombs.Count);
        Assert.Greater(finale.Steps.Count, 0);
        Assert.Greater(finale.PointsGained, 0);
        Assert.AreEqual(finale.PointsGained, board.Score);
        Assert.AreEqual(0, board.MovesMade);
        Assert.AreEqual(0, board.FindMatches().Count);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Assert.AreEqual(SpecialKind.None, board.GetSpecial(x, y));
            }
        }

        // Determinista
        var again = new BoardLogic(8, 8, 6, 21);
        Assert.AreEqual(finale.PointsGained, again.PlayFinale(7).PointsGained);
        Assert.AreEqual(0, new BoardLogic(8, 8, 6, 21).PlayFinale(0).FinaleBombs.Count);
    }

    [Test]
    public void InvalidLevels_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => Level(10).Validate());
        Assert.Throws<ArgumentException>(() => Level(0, LevelObjective.Score(10)).Validate());
        Assert.Throws<ArgumentException>(() => Level(10, LevelObjective.Collect(8, 10)).Validate());

        var badStars = Level(10, LevelObjective.Score(10));
        badStars.StarScores = new[] { 300, 200, 100 };
        Assert.Throws<ArgumentException>(() => badStars.Validate());
    }

    [Test]
    public void Progress_UnlocksNextLevel_AndKeepsBestStars()
    {
        var progress = new PlayerProgress();
        Assert.IsTrue(progress.IsUnlocked(1));
        Assert.IsFalse(progress.IsUnlocked(2));

        Assert.IsTrue(progress.RecordWin(1, 2, 10));
        Assert.IsTrue(progress.IsUnlocked(2));
        Assert.AreEqual(2, progress.GetStars(1));

        Assert.IsFalse(progress.RecordWin(1, 1, 10));
        Assert.AreEqual(2, progress.GetStars(1));

        Assert.IsTrue(progress.RecordWin(1, 3, 10));
        Assert.AreEqual(3, progress.GetStars(1));
        Assert.AreEqual(3, progress.TotalStars);
    }

    [Test]
    public void Progress_IgnoresLockedLevels_AndDoesNotUnlockPastTheEnd()
    {
        var progress = new PlayerProgress();

        Assert.IsFalse(progress.RecordWin(5, 3, 10));
        Assert.AreEqual(1, progress.highestUnlocked);

        progress.RecordWin(1, 1, 2);
        progress.RecordWin(2, 1, 2);
        Assert.AreEqual(2, progress.highestUnlocked);
    }
}
