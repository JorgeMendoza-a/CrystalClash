using System;
using System.Collections.Generic;

namespace CrystalClash.Ranked.Logic
{
    /// <summary>Un rango de la clasificatoria (p. ej. "Diamante II").</summary>
    public sealed class RankTier
    {
        /// <summary>Liga: Bronce, Plata, Oro, Platino, Diamante, Maestro, Campeón.</summary>
        public string League { get; }

        /// <summary>0 = sin división (Campeón); 1..5 = división (1 es la más baja).</summary>
        public int Division { get; }

        public int MinPoints { get; }

        /// <summary>int.MaxValue para el rango más alto.</summary>
        public int MaxPoints { get; }

        /// <summary>Índice de la liga (0 = Bronce ... 6 = Campeón). Sirve para elegir ícono y color.</summary>
        public int LeagueIndex { get; }

        public string Name => Division == 0 ? League : $"{League} {Division}";

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
    /// Tabla de rangos (la del diseño de la pantalla de Clasificatoria).
    /// Cada liga se divide en 5 divisiones iguales (Bronce 1 ... Bronce 5),
    /// salvo Campeón, que es la cima. En las ligas bajas se sube rápido (se
    /// gana mucho y se pierde poco) y en las altas cuesta más.
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
            public double WinMultiplier;   // multiplica lo que se gana al vencer
            public double LossMultiplier;  // multiplica lo que se pierde al caer
        }

        public const int DivisionsPerLeague = 5;

        public static readonly IReadOnlyList<League> Leagues = new[]
        {
            new League { Name = "Bronce",   MinPoints = 0,    MaxPoints = 499,          SeasonGems = 50,   SeasonCoins = 500,   WinMultiplier = 2.0, LossMultiplier = 0.5 },
            new League { Name = "Plata",    MinPoints = 500,  MaxPoints = 999,          SeasonGems = 100,  SeasonCoins = 1000,  WinMultiplier = 1.6, LossMultiplier = 0.6 },
            new League { Name = "Oro",      MinPoints = 1000, MaxPoints = 1999,         SeasonGems = 200,  SeasonCoins = 2000,  WinMultiplier = 1.3, LossMultiplier = 0.75 },
            new League { Name = "Platino",  MinPoints = 2000, MaxPoints = 3999,         SeasonGems = 300,  SeasonCoins = 3000,  WinMultiplier = 1.0, LossMultiplier = 0.9 },
            new League { Name = "Diamante", MinPoints = 4000, MaxPoints = 5999,         SeasonGems = 500,  SeasonCoins = 5000,  WinMultiplier = 0.8, LossMultiplier = 1.0 },
            new League { Name = "Maestro",  MinPoints = 6000, MaxPoints = 7999,         SeasonGems = 750,  SeasonCoins = 7500,  WinMultiplier = 0.6, LossMultiplier = 1.0 },
            new League { Name = "Campeón",  MinPoints = 8000, MaxPoints = int.MaxValue, SeasonGems = 1000, SeasonCoins = 10000, WinMultiplier = 0.5, LossMultiplier = 1.0 },
        };

        private static readonly List<RankTier> tiers = BuildTiers();

        public static IReadOnlyList<RankTier> All => tiers;

        public static League LeagueForPoints(int points)
        {
            return Leagues[ForPoints(points).LeagueIndex];
        }

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
                if (league.MaxPoints == int.MaxValue)
                {
                    // Campeón: la cima, sin divisiones.
                    list.Add(new RankTier(league.Name, i, 0, league.MinPoints, league.MaxPoints));
                    continue;
                }

                int size = (league.MaxPoints - league.MinPoints + 1) / DivisionsPerLeague;
                for (int d = 0; d < DivisionsPerLeague; d++)
                {
                    int min = league.MinPoints + d * size;
                    int max = d == DivisionsPerLeague - 1 ? league.MaxPoints : min + size - 1;
                    list.Add(new RankTier(league.Name, i, d + 1, min, max));
                }
            }
            return list;
        }
    }
}
