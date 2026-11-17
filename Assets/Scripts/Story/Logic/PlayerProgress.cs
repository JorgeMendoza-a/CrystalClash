using System;
using System.Collections.Generic;
using CrystalClash.Board.Logic;

namespace CrystalClash.Story.Logic
{
    /// <summary>
    /// Progreso del modo historia. Campos públicos para que se pueda guardar
    /// como JSON (JsonUtility en Unity, o un servidor más adelante).
    /// Los niveles se numeran desde 1.
    /// </summary>
    [Serializable]
    public sealed class PlayerProgress
    {
        /// <summary>Nivel más alto desbloqueado.</summary>
        public int highestUnlocked = 1;

        /// <summary>Mejores estrellas por nivel: stars[0] es el nivel 1.</summary>
        public List<int> stars = new List<int>();

        public const int StartingBoosters = 3;

        /// <summary>Cantidad de cada potenciador, por índice de BoosterKind.</summary>
        public int[] boosters = { StartingBoosters, StartingBoosters, StartingBoosters, StartingBoosters };

        public int GetBoosters(BoosterKind kind)
        {
            EnsureBoosterSlots();
            return boosters[(int)kind];
        }

        /// <summary>Gasta uno si hay. Devuelve false si no quedaban.</summary>
        public bool TryUseBooster(BoosterKind kind)
        {
            EnsureBoosterSlots();
            if (boosters[(int)kind] <= 0) return false;
            boosters[(int)kind]--;
            return true;
        }

        public void AddBooster(BoosterKind kind, int amount)
        {
            EnsureBoosterSlots();
            boosters[(int)kind] = Math.Max(0, boosters[(int)kind] + amount);
        }

        /// <summary>Premio por pasar un nivel por primera vez (va rotando entre los 4).</summary>
        public static BoosterKind FirstWinReward(int levelNumber)
        {
            return (BoosterKind)((Math.Max(1, levelNumber) - 1) % 4);
        }

        private void EnsureBoosterSlots()
        {
            int count = Enum.GetValues(typeof(BoosterKind)).Length;
            if (boosters == null) boosters = new int[0];
            if (boosters.Length >= count) return;

            var resized = new int[count];
            for (int i = 0; i < count; i++) resized[i] = i < boosters.Length ? boosters[i] : StartingBoosters;
            boosters = resized;
        }

        public bool IsUnlocked(int levelNumber)
        {
            return levelNumber >= 1 && levelNumber <= highestUnlocked;
        }

        public int GetStars(int levelNumber)
        {
            int index = levelNumber - 1;
            return index >= 0 && index < stars.Count ? stars[index] : 0;
        }

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (int s in stars) total += s;
                return total;
            }
        }

        /// <summary>
        /// Guarda una victoria: conserva las mejores estrellas y desbloquea el
        /// siguiente nivel (sin pasar de totalLevels). Devuelve true si mejoró algo.
        /// </summary>
        public bool RecordWin(int levelNumber, int earnedStars, int totalLevels)
        {
            if (levelNumber < 1 || !IsUnlocked(levelNumber)) return false;

            earnedStars = Math.Max(1, Math.Min(3, earnedStars));
            bool improved = false;

            while (stars.Count < levelNumber) stars.Add(0);
            if (earnedStars > stars[levelNumber - 1])
            {
                stars[levelNumber - 1] = earnedStars;
                improved = true;
            }

            int next = Math.Min(levelNumber + 1, totalLevels);
            if (next > highestUnlocked)
            {
                highestUnlocked = next;
                improved = true;
            }

            return improved;
        }
    }
}
