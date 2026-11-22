using System;
using System.Collections.Generic;

namespace CrystalClash.Ranked.Logic
{
    public enum SeasonRewardKind
    {
        /// <summary>Llegaste por primera vez a una división en esta temporada.</summary>
        RankUp = 0,

        /// <summary>Terminó la temporada: premio según tu rango final.</summary>
        SeasonEnd = 1,

        /// <summary>Terminó el torneo semanal: premio según tu puesto (Division = puesto, Season = semana).</summary>
        WeeklyEnd = 2
    }

    /// <summary>Una recompensa de temporada (la entrega el servidor; el juego la muestra y suma monedas/gemas).</summary>
    public class SeasonReward
    {
        public int Kind { get; set; }          // (int)SeasonRewardKind
        public int Season { get; set; }
        public int LeagueIndex { get; set; }
        public int Division { get; set; }
        public string TierName { get; set; } = "";
        public int Coins { get; set; }
        public int Gems { get; set; }

        /// <summary>Insignia exclusiva ganada (p. ej. "s1_oro"), o vacío.</summary>
        public string Badge { get; set; } = "";

        /// <summary>Solo en fin de temporada: puntos con los que empiezas la siguiente.</summary>
        public int NewSeasonPoints { get; set; }
    }

    /// <summary>
    /// Temporadas de la clasificatoria (las usan el juego y el servidor):
    /// duran 60 días, al terminar los puntos se reducen a la mitad (quien
    /// tenía más sigue más arriba) y se dan recompensas al subir de rango.
    /// </summary>
    public static class SeasonRules
    {
        public const int SeasonDays = 60;
        public const long SeasonSeconds = SeasonDays * 24L * 60L * 60L;

        /// <summary>Inicio de la Temporada 1: 1 de noviembre de 2026, 00:00 UTC.</summary>
        public static readonly long FirstSeasonStartUtc =
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        /// <summary>Qué fracción de los puntos se conserva al empezar una temporada nueva.</summary>
        public const double CarryOver = 0.5;

        // Monedas por llegar a una división nueva (por liga: Bronce ... Campeón)
        private static readonly int[] DivisionCoins = { 0, 40, 60, 80, 100, 130, 0 };

        // Bonus por llegar a una liga nueva (además de su insignia)
        private static readonly int[] LeagueCoins = { 0, 200, 400, 700, 1000, 1500, 2500 };

        private static readonly string[] LeagueFiles = { "bronce", "plata", "oro", "platino", "diamante", "maestro", "campeon" };

        public static int CurrentSeason(long nowUtc)
        {
            if (nowUtc < FirstSeasonStartUtc) return 1;
            return (int)((nowUtc - FirstSeasonStartUtc) / SeasonSeconds) + 1;
        }

        public static long SeasonStartUtc(int season)
        {
            return FirstSeasonStartUtc + (Math.Max(1, season) - 1) * SeasonSeconds;
        }

        public static long SeasonEndUtc(int season)
        {
            return SeasonStartUtc(season) + SeasonSeconds;
        }

        /// <summary>Puntos con los que se empieza la temporada siguiente.</summary>
        public static int SoftReset(int points)
        {
            return (int)Math.Floor(Math.Max(0, points) * CarryOver);
        }

        public static string BadgeId(int season, int leagueIndex)
        {
            int league = Math.Max(0, Math.Min(LeagueFiles.Length - 1, leagueIndex));
            return $"s{season}_{LeagueFiles[league]}";
        }

        /// <summary>Lee una insignia "s3_oro" → temporada 3, liga 2. False si no tiene ese formato.</summary>
        public static bool TryParseBadge(string badge, out int season, out int leagueIndex)
        {
            season = 0;
            leagueIndex = 0;
            if (string.IsNullOrEmpty(badge) || badge[0] != 's') return false;
            int sep = badge.IndexOf('_');
            if (sep < 2 || !int.TryParse(badge.Substring(1, sep - 1), out season)) return false;
            leagueIndex = Array.IndexOf(LeagueFiles, badge.Substring(sep + 1));
            return leagueIndex >= 0;
        }

        /// <summary>Recompensa por llegar a una división (y a una liga, si es su primera división).</summary>
        public static SeasonReward RewardForTier(RankTier tier, int season)
        {
            bool firstOfLeague = tier.Division <= 1;
            int coins = DivisionCoins[tier.LeagueIndex] + (firstOfLeague ? LeagueCoins[tier.LeagueIndex] : 0);
            bool hasBadge = firstOfLeague && tier.LeagueIndex > 0; // Bronce es donde todos empiezan

            return new SeasonReward
            {
                Kind = (int)SeasonRewardKind.RankUp,
                Season = season,
                LeagueIndex = tier.LeagueIndex,
                Division = tier.Division,
                TierName = tier.Name,
                Coins = coins,
                Badge = hasBadge ? BadgeId(season, tier.LeagueIndex) : ""
            };
        }

        /// <summary>
        /// Recompensas por subir el máximo de la temporada de oldPeak a newPeak:
        /// una por cada división alcanzada por primera vez.
        /// </summary>
        public static List<SeasonReward> RewardsForPeak(int oldPeak, int newPeak, int season)
        {
            var rewards = new List<SeasonReward>();
            if (newPeak <= oldPeak) return rewards;

            foreach (RankTier tier in RankTiers.All)
            {
                if (tier.MinPoints > oldPeak && tier.MinPoints <= newPeak && tier.MinPoints > 0)
                {
                    rewards.Add(RewardForTier(tier, season));
                }
            }
            return rewards;
        }

        /// <summary>Premio de fin de temporada según los puntos finales.</summary>
        public static SeasonReward SeasonEndReward(int finalPoints, int season)
        {
            RankTier tier = RankTiers.ForPoints(finalPoints);
            RankTiers.League league = RankTiers.Leagues[tier.LeagueIndex];
            return new SeasonReward
            {
                Kind = (int)SeasonRewardKind.SeasonEnd,
                Season = season,
                LeagueIndex = tier.LeagueIndex,
                Division = tier.Division,
                TierName = tier.Name,
                Coins = league.SeasonCoins,
                Gems = league.SeasonGems,
                NewSeasonPoints = SoftReset(finalPoints)
            };
        }
    }
}
