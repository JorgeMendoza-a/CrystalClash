using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalClash.Ranked.Logic
{
    /// <summary>Un jugador en la tabla del torneo semanal.</summary>
    public class WeeklyEntry
    {
        public string Name { get; set; } = "";
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
    }

    /// <summary>
    /// Torneo semanal: cada partida de clasificatoria suma puntos de torneo
    /// (victoria 3, empate 1, derrota 0). Empieza el lunes a las 00:00 UTC y dura
    /// 7 días. Al terminar, los mejores ganan gemas y monedas.
    /// </summary>
    public static class WeeklyRules
    {
        public const int WinPoints = 3;
        public const int DrawPoints = 1;
        public const int SecondsPerDay = 86400;

        /// <summary>Número de semana (el 1 de enero de 1970 fue jueves: +3 hace que empiecen en lunes).</summary>
        public static int Week(long nowUtc) => (int)Math.Floor((nowUtc / (double)SecondsPerDay + 3) / 7);

        public static long WeekStartUtc(int week) => ((long)week * 7 - 3) * SecondsPerDay;
        public static long WeekEndUtc(int week) => WeekStartUtc(week + 1);

        public static int PointsFor(MatchOutcome outcome)
        {
            return outcome == MatchOutcome.Win ? WinPoints : outcome == MatchOutcome.Draw ? DrawPoints : 0;
        }

        public static void Add(WeeklyEntry entry, MatchOutcome outcome)
        {
            entry.Points += PointsFor(outcome);
            if (outcome == MatchOutcome.Win) entry.Wins++;
            else if (outcome == MatchOutcome.Draw) entry.Draws++;
            else entry.Losses++;
        }

        /// <summary>Orden de la tabla: más puntos, luego más victorias, luego menos partidas.</summary>
        public static List<KeyValuePair<string, WeeklyEntry>> Ranking(Dictionary<string, WeeklyEntry> entries)
        {
            return entries
                .OrderByDescending(e => e.Value.Points)
                .ThenByDescending(e => e.Value.Wins)
                .ThenBy(e => e.Value.Wins + e.Value.Draws + e.Value.Losses)
                .ThenBy(e => e.Key, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Puesto (1 = primero) o 0 si no jugó esa semana.</summary>
        public static int RankOf(Dictionary<string, WeeklyEntry> entries, string playerId)
        {
            List<KeyValuePair<string, WeeklyEntry>> ranking = Ranking(entries);
            for (int i = 0; i < ranking.Count; i++)
            {
                if (ranking[i].Key == playerId) return i + 1;
            }
            return 0;
        }

        /// <summary>Premio según el puesto final (0 = no jugó: nada).</summary>
        public static SeasonReward RewardFor(int rank, int week)
        {
            int gems, coins;
            if (rank <= 0) return null;
            if (rank == 1) { gems = 100; coins = 1000; }
            else if (rank == 2) { gems = 60; coins = 600; }
            else if (rank == 3) { gems = 40; coins = 400; }
            else if (rank <= 10) { gems = 20; coins = 200; }
            else { gems = 5; coins = 100; }   // por participar

            return new SeasonReward
            {
                Kind = (int)SeasonRewardKind.WeeklyEnd,
                Season = week,
                Division = rank,
                TierName = "Puesto " + rank,
                Gems = gems,
                Coins = coins
            };
        }

        /// <summary>Texto de la tabla de premios (para el botón de información).</summary>
        public static readonly string[] RewardLines =
        {
            "1°: 100 gemas + 1000 monedas",
            "2°: 60 gemas + 600 monedas",
            "3°: 40 gemas + 400 monedas",
            "4° al 10°: 20 gemas + 200 monedas",
            "Por participar: 5 gemas + 100 monedas"
        };
    }
}
