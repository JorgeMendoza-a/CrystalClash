using System;
using System.Collections.Generic;

namespace CrystalClash.Ranked.Logic
{
    /// <summary>Un rango de la clasificatoria (p. ej. "Diamante II").</summary>
    public sealed class RankTier
    {
        /// <summary>Liga: Bronce, Plata, Oro, Platino, Diamante, Maestro, Campeón.</summary>
        public string League { get; }

        /// <summary>0 = sin división; 1 = I; 2 = II.</summary>
        public int Division { get; }

        public int MinPoints { get; }

        /// <summary>int.MaxValue para el rango más alto.</summary>
        public int MaxPoints { get; }

        /// <summary>Índice de la liga (0 = Bronce ... 6 = Campeón). Sirve para elegir ícono y color.</summary>
        public int LeagueIndex { get; }

        public string Name => Division == 0 ? League : $"{League} {(Division == 1 ? "I" : "II")}";

        internal RankTier(string league, int leagueIndex, int division, int min, int max)
        {
            League = league;
            LeagueIndex = leagueIndex;
            Division = division;
            MinPoints = min;
            MaxPoints = max;
        }
    }

    /// <summary>
    /// Tabla de rangos (la misma del diseño de la pantalla de Clasificatoria).
    /// Las ligas de 1000 puntos o más se dividen en I y II.
    /// </summary>
    public static class RankTiers
    {
        public sealed class League
        {
            public string Name;
            public int MinPoints;
            public int MaxPoints;   // int.MaxValue = sin tope
            public int SeasonGems;  // recompensa de fin de temporada
            public int SeasonCoins;
        }

        public static readonly IReadOnlyList<League> Leagues = new[]
        {
            new League { Name = "Bronce",   MinPoints = 0,    MaxPoints = 499,          SeasonGems = 50,   SeasonCoins = 500 },
            new League { Name = "Plata",    MinPoints = 500,  MaxPoints = 999,          SeasonGems = 100,  SeasonCoins = 1000 },
            new League { Name = "Oro",      MinPoints = 1000, MaxPoints = 1999,         SeasonGems = 200,  SeasonCoins = 2000 },
            new League { Name = "Platino",  MinPoints = 2000, MaxPoints = 3999,         SeasonGems = 300,  SeasonCoins = 3000 },
            new League { Name = "Diamante", MinPoints = 4000, MaxPoints = 5999,         SeasonGems = 500,  SeasonCoins = 5000 },
            new League { Name = "Maestro",  MinPoints = 6000, MaxPoints = 7999,         SeasonGems = 750,  SeasonCoins = 7500 },
            new League { Name = "Campeón",  MinPoints = 8000, MaxPoints = int.MaxValue, SeasonGems = 1000, SeasonCoins = 10000 },
        };

        private static readonly List<RankTier> tiers = BuildTiers();

        public static IReadOnlyList<RankTier> All => tiers;

        public static RankTier ForPoints(int points)
        {
            points = Math.Max(0, points);
            for (int i = tiers.Count - 1; i >= 0; i--)
            {
                if (points >= tiers[i].MinPoints) return tiers[i];
            }
            return tiers[0];
        }

        /// <summary>El rango siguiente, o null si ya está en el más alto.</summary>
        public static RankTier Next(RankTier tier)
        {
            int index = tiers.IndexOf(tier);
            return index >= 0 && index < tiers.Count - 1 ? tiers[index + 1] : null;
        }

        private static List<RankTier> BuildTiers()
        {
            var list = new List<RankTier>();
            for (int i = 0; i < Leagues.Count; i++)
            {
                League league = Leagues[i];
                bool split = league.MaxPoints != int.MaxValue && league.MaxPoints - league.MinPoints + 1 >= 1000;

                if (!split)
                {
                    list.Add(new RankTier(league.Name, i, 0, league.MinPoints, league.MaxPoints));
                    continue;
                }

                int half = league.MinPoints + (league.MaxPoints - league.MinPoints + 1) / 2;
                list.Add(new RankTier(league.Name, i, 1, league.MinPoints, half - 1));
                list.Add(new RankTier(league.Name, i, 2, half, league.MaxPoints));
            }
            return list;
        }
    }
}
