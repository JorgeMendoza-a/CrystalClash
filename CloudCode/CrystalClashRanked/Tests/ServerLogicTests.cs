using System;
using System.Collections.Generic;
using CrystalClash.Board.Logic;
using CrystalClash.Ranked.Logic;
using CrystalClashRanked;

/// <summary>Tests simples (sin frameworks) de la lógica del servidor.</summary>
public static class ServerLogicTests
{
    private static int failures;

    public static int Main()
    {
        Run(nameof(Window_GrowsWithWaitingTime), Window_GrowsWithWaitingTime);
        Run(nameof(PickOpponent_ChoosesClosestValidTicket), PickOpponent_ChoosesClosestValidTicket);
        Run(nameof(CleanQueue_RemovesAbandonedTickets), CleanQueue_RemovesAbandonedTickets);
        Run(nameof(Bot_IsDeterministic_AndProgresses), Bot_IsDeterministic_AndProgresses);
        Run(nameof(Results_HumanVsHuman), Results_HumanVsHuman);
        Run(nameof(Results_Abandonment_Loses), Results_Abandonment_Loses);
        Run(nameof(Results_VsBot_HalfPoints), Results_VsBot_HalfPoints);
        Run(nameof(CanFinalize_WaitsForHumansOrTimeout), CanFinalize_WaitsForHumansOrTimeout);
        Run(nameof(Season_NewProfile_StartsInCurrentSeason), Season_NewProfile_StartsInCurrentSeason);
        Run(nameof(Season_Rollover_RewardsAndSoftReset), Season_Rollover_RewardsAndSoftReset);
        Run(nameof(Peak_GivesRewardsAndBadgesOnce), Peak_GivesRewardsAndBadgesOnce);
        Run(nameof(Avatar_OnlyOwnedBadges), Avatar_OnlyOwnedBadges);
        Console.WriteLine(failures == 0 ? "Todos los tests del servidor pasaron." : $"{failures} test(s) fallaron.");
        return failures;
    }

    private static void Window_GrowsWithWaitingTime()
    {
        Check(ServerLogic.PointsWindow(0) == 250, "ventana inicial");
        Check(ServerLogic.PointsWindow(10) > ServerLogic.PointsWindow(0), "crece");
        Check(ServerLogic.PointsWindow(100000) == 3000, "tope");
    }

    private static void PickOpponent_ChoosesClosestValidTicket()
    {
        var q = new QueueState();
        q.Tickets.Add(new Ticket { Id = "self", PlayerId = "me", Points = 1000, CreatedUtc = 0 });
        q.Tickets.Add(new Ticket { Id = "far", PlayerId = "a", Points = 2500, CreatedUtc = 0 });
        q.Tickets.Add(new Ticket { Id = "taken", PlayerId = "b", Points = 1000, CreatedUtc = 0, MatchId = "x" });
        q.Tickets.Add(new Ticket { Id = "near", PlayerId = "c", Points = 1100, CreatedUtc = 0 });
        q.Tickets.Add(new Ticket { Id = "nearer", PlayerId = "d", Points = 990, CreatedUtc = 0 });

        Check(ServerLogic.PickOpponent(q, "me", 1000, 0).Id == "nearer", "el más cercano");
        q.Tickets.RemoveAll(t => t.Id == "nearer" || t.Id == "near");
        Check(ServerLogic.PickOpponent(q, "me", 1000, 0) == null, "lejos y sin esperar: nadie");
        Check(ServerLogic.PickOpponent(q, "me", 1000, 60)?.Id == "far", "tras esperar, la ventana alcanza");
    }

    private static void CleanQueue_RemovesAbandonedTickets()
    {
        var q = new QueueState();
        q.Tickets.Add(new Ticket { Id = "old", LastPollUtc = 0 });
        q.Tickets.Add(new Ticket { Id = "fresh", LastPollUtc = 95 });
        ServerLogic.CleanQueue(q, 100);
        Check(q.Tickets.Count == 1 && q.Tickets[0].Id == "fresh", "limpieza");
    }

    private static void Bot_IsDeterministic_AndProgresses()
    {
        int a = ServerLogic.BotScoreAfter(1234, RankedRules.Moves);
        int b = ServerLogic.BotScoreAfter(1234, RankedRules.Moves);
        Check(a == b && a > 0, "determinista y con puntos");

        ProgressDto start = ServerLogic.BotProgress(1234, 1000, 1000);
        ProgressDto end = ServerLogic.BotProgress(1234, 1000, 1000 + 10000);
        Check(start.MovesLeft == RankedRules.Moves && start.Score == 0, "al empezar");
        Check(end.Finished && end.Score == a, "al terminar coincide con el puntaje final");
    }

    private static MatchState Match(bool aSubmitted, int aScore, bool bSubmitted, int bScore, bool bIsBot = false)
    {
        return new MatchState
        {
            MatchId = "m",
            Seed = 77,
            CreatedUtc = 0,
            Players = new List<MatchPlayer>
            {
                new MatchPlayer { PlayerId = "a", Name = "A", PointsAtStart = 200, Submitted = aSubmitted, FinalScore = aScore },
                new MatchPlayer { PlayerId = bIsBot ? "bot" : "b", Name = "B", PointsAtStart = 200, Submitted = bSubmitted, FinalScore = bScore, IsBot = bIsBot }
            }
        };
    }

    private static void Results_HumanVsHuman()
    {
        var r = ServerLogic.ComputeResults(Match(true, 900, true, 700), id => 200);
        Check(r["a"].Outcome == (int)MatchOutcome.Win && r["a"].PointsChange == 60, "gana +60 en Bronce");
        Check(r["b"].Outcome == (int)MatchOutcome.Loss && r["b"].PointsChange == -10, "pierde -10 en Bronce");
        Check(r["a"].MyScore == 900 && r["a"].OpponentScore == 700 && r["a"].NewPoints == 260, "puntajes");
    }

    private static void Results_Abandonment_Loses()
    {
        var r = ServerLogic.ComputeResults(Match(true, 0, false, 5000), id => 200);
        Check(r["a"].Outcome == (int)MatchOutcome.Win, "el que envió gana aunque haya hecho 0 si el otro abandona");
        Check(r["b"].Outcome == (int)MatchOutcome.Loss, "el que abandona pierde");
    }

    private static void Results_VsBot_HalfPoints()
    {
        int botScore = ServerLogic.BotScoreAfter(77, RankedRules.Moves);
        var r = ServerLogic.ComputeResults(Match(true, botScore + 100, false, 0, bIsBot: true), id => 200);
        Check(r.Count == 1, "solo hay resultado para el humano");
        Check(r["a"].Outcome == (int)MatchOutcome.Win && r["a"].PointsChange == 60, "contra bot da lo mismo: +60");
        Check(r["a"].OpponentIsBot && r["a"].OpponentScore == botScore, "contra bot");
    }

    private static void CanFinalize_WaitsForHumansOrTimeout()
    {
        Check(!ServerLogic.CanFinalize(Match(true, 1, false, 1), 10), "espera al rival");
        Check(ServerLogic.CanFinalize(Match(true, 1, true, 1), 10), "ambos enviaron");
        Check(ServerLogic.CanFinalize(Match(true, 1, false, 0, bIsBot: true), 10), "contra bot basta conmigo");
        Check(ServerLogic.CanFinalize(Match(true, 1, false, 1), ServerLogic.MatchGraceSeconds + 1), "se acabó el margen");
    }

    private static long SeasonStart(int season) => SeasonRules.SeasonStartUtc(season);

    private static void Season_NewProfile_StartsInCurrentSeason()
    {
        var p = new RankedProfileState { Points = 0 };
        Check(ServerLogic.ApplySeason(p, SeasonStart(1) + 10), "cambia");
        Check(p.Season == 1 && p.PendingRewards.Count == 0, "temporada 1 sin premio");
        Check(!ServerLogic.ApplySeason(p, SeasonStart(1) + 100), "no vuelve a cambiar");
    }

    private static void Season_Rollover_RewardsAndSoftReset()
    {
        var p = new RankedProfileState { Season = 1, Points = 8400, Wins = 3, SeasonPeakPoints = 8400 };
        Check(ServerLogic.ApplySeason(p, SeasonStart(2) + 5), "nueva temporada");
        Check(p.Season == 2 && p.Points == 4200 && p.SeasonPeakPoints == 4200, "mitad de puntos");
        Check(p.PendingRewards.Count == 1 && p.PendingRewards[0].Kind == (int)SeasonRewardKind.SeasonEnd, "premio de fin");
        Check(p.PendingRewards[0].Gems == 1000 && p.PendingRewards[0].Season == 1, "premio de Campeón T1");

        var idle = new RankedProfileState { Season = 1, Points = 8000 };
        ServerLogic.ApplySeason(idle, SeasonStart(3) + 5);
        Check(idle.Points == 2000 && idle.PendingRewards.Count == 0, "dos temporadas sin jugar: dos reinicios, sin premio");
    }

    private static void Peak_GivesRewardsAndBadgesOnce()
    {
        var p = new RankedProfileState { Season = 1, Points = 520, SeasonPeakPoints = 480 };
        ServerLogic.ApplyPeak(p);
        Check(p.Badges.Contains("s1_plata") && p.PendingRewards.Count == 1, "Plata 1 con insignia");

        p.Points = 470; ServerLogic.ApplyPeak(p);
        p.Points = 530; ServerLogic.ApplyPeak(p);
        Check(p.PendingRewards.Count == 1 && p.Badges.Count == 1, "bajar y volver a subir no repite premios");
    }

    private static void Avatar_OnlyOwnedBadges()
    {
        var p = new RankedProfileState();
        p.Badges.Add("s1_oro");
        Check(ServerLogic.IsValidAvatar(p, "a0") && ServerLogic.IsValidAvatar(p, "a7"), "avatares normales");
        Check(ServerLogic.IsValidAvatar(p, "s1_oro"), "insignia ganada");
        Check(!ServerLogic.IsValidAvatar(p, "s1_campeon"), "insignia no ganada");
        Check(!ServerLogic.IsValidAvatar(p, "a999") && !ServerLogic.IsValidAvatar(p, "") && !ServerLogic.IsValidAvatar(p, "x"), "basura");
    }

    private static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }
}
