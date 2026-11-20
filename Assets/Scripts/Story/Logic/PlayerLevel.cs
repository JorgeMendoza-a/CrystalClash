using System;

namespace CrystalClash.Story.Logic
{
    /// <summary>
    /// Nivel de jugador a partir de la experiencia total.
    /// Del nivel N al N+1 hacen falta 100 + 50·(N-1) puntos de experiencia.
    /// </summary>
    public static class PlayerLevel
    {
        public const int BaseXp = 100;
        public const int XpGrowth = 50;
        public const int MaxLevel = 999;

        public static int XpToNext(int level)
        {
            return BaseXp + XpGrowth * Math.Max(0, level - 1);
        }

        /// <summary>Nivel actual, experiencia dentro del nivel y la que falta para el siguiente.</summary>
        public static void FromXp(int totalXp, out int level, out int xpInLevel, out int xpForNext)
        {
            level = 1;
            int remaining = Math.Max(0, totalXp);
            while (level < MaxLevel && remaining >= XpToNext(level))
            {
                remaining -= XpToNext(level);
                level++;
            }
            xpInLevel = remaining;
            xpForNext = XpToNext(level);
        }
    }
}
