using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CrystalClash.Board.Logic;
using CrystalClash.Ranked.Logic;
using CrystalClash.Story.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Una partida clasificatoria en curso: pantalla de VS, tiempo límite,
/// puntaje del rival en vivo, envío de movimientos al servidor y resultado.
/// La crea GameManager cuando RankedMatchState.Active.
/// </summary>
public class RankedMatchController : MonoBehaviour
{
    private const float SyncIntervalSeconds = 2f;
    private const int GemsPerWin = 10;

    /// <summary>Mi nombre de clasificatoria (lo guarda RankedScreen al conectarse) para la pantalla de VS.</summary>
    public static string myDisplayName = "";

    private IRankedService service;
    private RankedMatchInfo match;
    private LevelSession session;
    private BoardManager board;

    private readonly List<(GridPos a, GridPos b)> moves = new List<(GridPos, GridPos)>();
    private float startTime;
    private bool started;
    private bool finished;
    private bool polling;
    private CancellationTokenSource cancel;

    /// <summary>True mientras el jugador puede hacer jugadas.</summary>
    public bool AcceptingMoves => started && !finished;

    public static LevelDefinition CreateLevel(RankedMatchInfo info)
    {
        var level = new LevelDefinition
        {
            Number = 0,
            Name = "Clasificatoria",
            Width = RankedRules.Width,
            Height = RankedRules.Height,
            MineralTypeCount = RankedRules.MineralTypes,
            Seed = info.Seed,
            MaxMoves = RankedRules.Moves,
            StarScores = new[] { 0, 0, 0 }
        };
        // Objetivo inalcanzable: la partida dura hasta gastar los movimientos (o el tiempo).
        level.Objectives.Add(LevelObjective.Score(int.MaxValue / 4));
        return level;
    }

    public void Begin(LevelSession levelSession, BoardManager boardManager, RankedMatchInfo info)
    {
        service = RankedServices.Current;
        session = levelSession;
        board = boardManager;
        match = info;
        cancel = new CancellationTokenSource();

        GameHud.Create(session, match);

        RankTier opponentTier = RankTiers.ForPoints(match.OpponentPoints);
        string myName = string.IsNullOrEmpty(myDisplayName) ? "Tú" : myDisplayName;
        Sound.Play("match_found");
        StoryPopup.ShowVersus(
            "¡Partida encontrada!",
            myName, ProfileAvatar.LocalId, ProfileAvatar.LocalBanner,
            match.OpponentName, match.OpponentAvatar, match.OpponentBanner,
            $"{opponentTier.Name} · {match.OpponentPoints}",
            $"Mismo tablero para los dos.\n{RankedRules.Moves} movimientos · {RankedRules.TimeLimitSeconds / 60}:{RankedRules.TimeLimitSeconds % 60:00} minutos\n" +
            "¡Gana quien haga más puntos!",
            new StoryPopup.PopupButton("¡Empezar!", StartPlaying));
    }

    private void OnDestroy()
    {
        if (cancel != null) cancel.Cancel();
    }

    private void StartPlaying()
    {
        started = true;
        startTime = Time.realtimeSinceStartup;
        StartCoroutine(SyncLoop());
    }

    private void Update()
    {
        if (!AcceptingMoves) return;

        float left = RankedRules.TimeLimitSeconds - (Time.realtimeSinceStartup - startTime);
        if (GameHud.Instance != null) GameHud.Instance.SetTimer(Mathf.Max(0f, left));

        // Se acabó el tiempo: cuenta lo que llevas (esperamos a que termine la animación).
        if (left <= 0f && (board == null || !board.IsBusy)) Finish();
    }

    // ------------------------------------------------------------------
    // Llamado por GameManager
    // ------------------------------------------------------------------

    public void RecordMove(SwapResult result)
    {
        if (result != null && result.IsValid) moves.Add((result.A, result.B));
    }

    public void OnMoveFinished()
    {
        if (!finished && session.MovesLeft <= 0) Finish();
    }

    // ------------------------------------------------------------------

    private IEnumerator SyncLoop()
    {
        var wait = new WaitForSecondsRealtime(SyncIntervalSeconds);
        while (!finished)
        {
            Poll();
            yield return wait;
        }
    }

    private async void Poll()
    {
        if (polling) return;
        polling = true;
        try
        {
            await service.ReportProgressAsync(match.MatchId, session.Score, session.MovesLeft);
            OpponentProgress progress = await service.GetOpponentProgressAsync(match.MatchId);
            if (this != null && GameHud.Instance != null)
            {
                GameHud.Instance.SetOpponent(progress.Score, progress.MovesLeft, progress.Finished);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Clasificatoria (sincronizar): " + e.Message);
        }
        finally
        {
            polling = false;
        }
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        if (GameHud.Instance != null) GameHud.Instance.SetTimer(0f);
        Submit();
    }

    private async void Submit()
    {
        StoryPopup.Show("¡Terminaste!", $"Tus puntos: <b>{session.Score}</b>\n\nEsperando a que {match.OpponentName} termine...");

        try
        {
            string encoded = RankedRules.EncodeMoves(moves);
            RankedMatchResult result = await service.SubmitMatchAsync(match, encoded, cancel.Token);
            if (this == null) return;
            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogWarning("Clasificatoria (enviar): " + e.Message);
            if (this == null) return;
            StoryPopup.Show("No se pudo enviar la partida", "Revisa tu conexión.",
                new StoryPopup.PopupButton("Reintentar", Submit),
                new StoryPopup.PopupButton("Salir", () => Leave(AppNavigation.Screen.Ranked)));
        }
    }

    private void ShowResult(RankedMatchResult result)
    {
        Sound.Play(result.Outcome == MatchOutcome.Win ? "win" : result.Outcome == MatchOutcome.Loss ? "lose" : "star");

        string title;
        switch (result.Outcome)
        {
            case MatchOutcome.Win: title = "¡VICTORIA!"; break;
            case MatchOutcome.Loss: title = "Derrota"; break;
            default: title = "Empate"; break;
        }

        // Premios locales: gemas por ganar y experiencia por jugar.
        string gemsLine = "";
        if (result.Outcome == MatchOutcome.Win)
        {
            SaveManager.Progress.gems += GemsPerWin;
            gemsLine = $"\n<color=#C080FF>+{GemsPerWin} gemas</color>";
        }
        int xp = result.Outcome == MatchOutcome.Win ? PlayerProgress.XpRankedWin
            : result.Outcome == MatchOutcome.Draw ? PlayerProgress.XpRankedDraw : PlayerProgress.XpRankedLoss;
        SaveManager.Progress.AddXp(xp);
        DailyMissions.Add(SaveManager.Progress, GameDays.Today, MissionType.PlayRanked, 1);
        if (result.Outcome == MatchOutcome.Win) DailyMissions.Add(SaveManager.Progress, GameDays.Today, MissionType.WinRanked, 1);
        SaveManager.Save();
        gemsLine += $"  <color=#80C8FF>+{xp} XP</color>";

        string change = result.PointsChange >= 0 ? $"<color=#66FF77>+{result.PointsChange}</color>" : $"<color=#FF7070>{result.PointsChange}</color>";
        RankTier tier = RankTiers.ForPoints(result.NewPoints);

        StoryPopup.Show(
            title,
            $"Tú: <b>{result.MyScore}</b>   —   {result.OpponentName}: <b>{result.OpponentScore}</b>\n\n" +
            $"Puntos de clasificatoria: {change}\n" +
            $"Ahora tienes <b>{result.NewPoints}</b> · <color=#{ColorUtility.ToHtmlStringRGB(UiKit.LeagueColor(tier.LeagueIndex))}>{tier.Name}</color>" + gemsLine,
            new StoryPopup.PopupButton("Jugar otra", () => Leave(AppNavigation.Screen.Ranked)),
            new StoryPopup.PopupButton("Salir", () => Leave(AppNavigation.Screen.Home)));
    }

    private static void Leave(AppNavigation.Screen screen)
    {
        RankedMatchState.Current = null;
        AppNavigation.PendingScreen = screen;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
