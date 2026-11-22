using System;
using System.Collections.Generic;
using CrystalClash.Board.Logic;

namespace CrystalClash.Story.Logic
{
    /// <summary>Un premio: monedas, gemas y/o potenciadores.</summary>
    public struct EventReward
    {
        public int Coins;
        public int Gems;
        public int BoosterCount;
        public BoosterKind Booster;

        public void ApplyTo(PlayerProgress p)
        {
            p.coins += Coins;
            p.gems += Gems;
            if (BoosterCount > 0) p.AddBooster(Booster, BoosterCount);
        }
    }

    /// <summary>
    /// Recompensa diaria: ciclo de 7 días. Se reclama una vez por día (día
    /// calendario del jugador); si se salta un día, la racha vuelve al día 1.
    /// "today" es el número de día (días desde 1970 en la hora local del jugador).
    /// </summary>
    public static class DailyRewards
    {
        public static readonly EventReward[] Cycle =
        {
            new EventReward { Coins = 100 },
            new EventReward { BoosterCount = 1, Booster = BoosterKind.Hammer },
            new EventReward { Gems = 10 },
            new EventReward { Coins = 250 },
            new EventReward { BoosterCount = 2, Booster = BoosterKind.Bomb },
            new EventReward { Gems = 20 },
            new EventReward { Gems = 50, Coins = 500 },
        };

        public static bool CanClaim(PlayerProgress p, int today) => p.dailyLastDay != today;

        /// <summary>Índice (0..6) del premio que toca hoy.</summary>
        public static int NextIndex(PlayerProgress p, int today)
        {
            bool streakAlive = p.dailyLastDay == today - 1;
            return streakAlive ? p.dailyStreak % Cycle.Length : 0;
        }

        /// <summary>Cuántos días del ciclo actual ya están reclamados (para dibujarlos marcados).</summary>
        public static int ClaimedInCycle(PlayerProgress p, int today)
        {
            if (CanClaim(p, today)) return NextIndex(p, today);
            return (p.dailyStreak - 1) % Cycle.Length + 1;
        }

        /// <summary>Reclama el premio de hoy. Devuelve el índice del día reclamado, o -1 si ya se reclamó.</summary>
        public static int Claim(PlayerProgress p, int today)
        {
            if (!CanClaim(p, today)) return -1;
            int index = NextIndex(p, today);
            Cycle[index].ApplyTo(p);
            p.dailyStreak = index + 1;
            p.dailyLastDay = today;
            return index;
        }
    }

    public enum MissionType { PassLevels, EarnStars, CreateSpecials, UseBoosters, PlayRanked, WinRanked, ClearMinerals }

    public struct MissionDef
    {
        public MissionType Type;
        public int Target;
        public EventReward Reward;

        public MissionDef(MissionType type, int target, int coins, int gems)
        {
            Type = type;
            Target = target;
            Reward = new EventReward { Coins = coins, Gems = gems };
        }

        /// <summary>Las de clasificatoria cuentan como un solo grupo (máximo una por día).</summary>
        public int Group => Type == MissionType.WinRanked ? (int)MissionType.PlayRanked : (int)Type;
    }

    /// <summary>
    /// Misiones diarias: 3 por día (de tipos distintos), elegidas igual para el
    /// mismo día. Cada una da su premio y completar las 3 da un bonus.
    /// </summary>
    public static class DailyMissions
    {
        public const int Count = 3;
        public static readonly EventReward Bonus = new EventReward { Gems = 15 };

        public static readonly MissionDef[] Pool =
        {
            new MissionDef(MissionType.PassLevels, 2, 150, 0),
            new MissionDef(MissionType.PassLevels, 4, 0, 8),
            new MissionDef(MissionType.EarnStars, 5, 150, 0),
            new MissionDef(MissionType.EarnStars, 9, 0, 10),
            new MissionDef(MissionType.CreateSpecials, 8, 100, 0),
            new MissionDef(MissionType.CreateSpecials, 15, 0, 8),
            new MissionDef(MissionType.UseBoosters, 2, 120, 0),
            new MissionDef(MissionType.PlayRanked, 2, 150, 0),
            new MissionDef(MissionType.WinRanked, 1, 0, 10),
            new MissionDef(MissionType.WinRanked, 3, 0, 20),
            new MissionDef(MissionType.ClearMinerals, 150, 120, 0),
            new MissionDef(MissionType.ClearMinerals, 300, 0, 8),
        };

        /// <summary>Si cambió el día, elige las misiones nuevas y reinicia el progreso.</summary>
        public static void EnsureToday(PlayerProgress p, int today)
        {
            if (p.missionsDay == today && p.missionIds != null && p.missionIds.Length == Count) return;

            p.missionsDay = today;
            p.missionIds = Pick(today);
            p.missionProgress = new int[Count];
            p.missionClaimed = new bool[Count];
            p.missionBonusClaimed = false;
        }

        /// <summary>Misiones del día (mismo resultado para el mismo día).</summary>
        public static int[] Pick(int day)
        {
            var chosen = new List<int>();
            var groups = new HashSet<int>();
            uint state = (uint)day * 2654435761u + 12345u;
            int guard = 0;
            while (chosen.Count < Count && guard++ < 1000)
            {
                state = state * 1664525u + 1013904223u;
                int index = (int)((state >> 8) % (uint)Pool.Length);
                if (groups.Contains(Pool[index].Group)) continue;
                groups.Add(Pool[index].Group);
                chosen.Add(index);
            }
            return chosen.ToArray();
        }

        public static MissionDef Get(PlayerProgress p, int slot) => Pool[p.missionIds[slot]];

        /// <summary>Suma progreso a las misiones de hoy de ese tipo.</summary>
        public static void Add(PlayerProgress p, int today, MissionType type, int amount)
        {
            if (amount <= 0) return;
            EnsureToday(p, today);
            for (int i = 0; i < Count; i++)
            {
                MissionDef def = Get(p, i);
                if (def.Type != type) continue;
                p.missionProgress[i] = Math.Min(def.Target, p.missionProgress[i] + amount);
            }
        }

        public static bool IsComplete(PlayerProgress p, int slot) => p.missionProgress[slot] >= Get(p, slot).Target;

        public static bool CanClaim(PlayerProgress p, int slot) => IsComplete(p, slot) && !p.missionClaimed[slot];

        public static bool Claim(PlayerProgress p, int slot)
        {
            if (!CanClaim(p, slot)) return false;
            Get(p, slot).Reward.ApplyTo(p);
            p.missionClaimed[slot] = true;
            return true;
        }

        public static bool BonusReady(PlayerProgress p)
        {
            if (p.missionBonusClaimed) return false;
            for (int i = 0; i < Count; i++) if (!p.missionClaimed[i]) return false;
            return true;
        }

        public static bool ClaimBonus(PlayerProgress p)
        {
            if (!BonusReady(p)) return false;
            Bonus.ApplyTo(p);
            p.missionBonusClaimed = true;
            return true;
        }

        /// <summary>Premios listos para reclamar (para el aviso rojo del botón).</summary>
        public static int Claimable(PlayerProgress p, int today)
        {
            EnsureToday(p, today);
            int count = BonusReady(p) ? 1 : 0;
            for (int i = 0; i < Count; i++) if (CanClaim(p, i)) count++;
            return count;
        }
    }
}
