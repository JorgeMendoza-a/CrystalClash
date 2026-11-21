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

        // ------------------------------------------------------------------
        // Monedas, gemas y vidas
        // ------------------------------------------------------------------

        public const int StartingCoins = 500;
        public const int MaxLives = 5;
        public const int LifeRegenSeconds = 20 * 60;

        public const int CoinsPerWin = 50;
        public const int CoinsPerStar = 25;
        public const int BoosterPrice = 250;     // monedas
        public const int RefillLivesPrice = 20;  // gemas

        public int coins = StartingCoins;
        public int gems;

        /// <summary>Vidas guardadas en el momento livesTimestampUtc (la regeneración se calcula al leer).</summary>
        public int lives = MaxLives;
        public long livesTimestampUtc;

        /// <summary>Vidas actuales, contando las que se recuperaron con el tiempo.</summary>
        public int GetLives(long nowUtc)
        {
            ApplyRegen(nowUtc);
            return lives;
        }

        /// <summary>Segundos para la próxima vida (0 si están llenas).</summary>
        public int SecondsToNextLife(long nowUtc)
        {
            ApplyRegen(nowUtc);
            if (lives >= MaxLives) return 0;
            return (int)Math.Max(0, LifeRegenSeconds - (nowUtc - livesTimestampUtc));
        }

        /// <summary>Quita una vida (al perder un nivel). Devuelve false si no quedaban.</summary>
        public bool LoseLife(long nowUtc)
        {
            ApplyRegen(nowUtc);
            if (lives <= 0) return false;
            if (lives == MaxLives) livesTimestampUtc = nowUtc; // empieza a contar la regeneración
            lives--;
            return true;
        }

        public void RefillLives(long nowUtc)
        {
            lives = MaxLives;
            livesTimestampUtc = nowUtc;
        }

        private void ApplyRegen(long nowUtc)
        {
            if (lives >= MaxLives)
            {
                lives = MaxLives;
                return;
            }
            if (nowUtc <= livesTimestampUtc) return;

            long elapsed = nowUtc - livesTimestampUtc;
            int recovered = (int)(elapsed / LifeRegenSeconds);
            if (recovered <= 0) return;

            lives = Math.Min(MaxLives, lives + recovered);
            livesTimestampUtc += (long)recovered * LifeRegenSeconds;
            if (lives >= MaxLives) livesTimestampUtc = nowUtc;
        }

        /// <summary>Monedas que da ganar un nivel (siempre, no solo la primera vez).</summary>
        public static int CoinsForWin(int stars)
        {
            return CoinsPerWin + CoinsPerStar * Math.Max(0, Math.Min(3, stars));
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount < 0 || coins < amount) return false;
            coins -= amount;
            return true;
        }

        public bool TrySpendGems(int amount)
        {
            if (amount < 0 || gems < amount) return false;
            gems -= amount;
            return true;
        }

        // ------------------------------------------------------------------
        // Perfil: avatar, experiencia, fecha de inicio y estadísticas
        // ------------------------------------------------------------------

        /// <summary>Avatar elegido (índice en ProfileAvatars del juego).</summary>
        public int avatar;

        /// <summary>Insignia de temporada usada como foto de perfil ("s1_oro"); vacío = usar el avatar normal.</summary>
        public string avatarBadge = "";

        /// <summary>Experiencia total (sube de nivel de jugador).</summary>
        public int xp;

        /// <summary>Primera vez que se abrió el juego (segundos UTC). 0 = aún no se sabe.</summary>
        public long createdUtc;

        /// <summary>Especiales creados en total (para el logro "Maestro de Combos").</summary>
        public int specialsCreated;

        /// <summary>Última vez que se guardó (segundos UTC). Sirve para juntar con la copia de la nube.</summary>
        public long savedUtc;

        /// <summary>Copia independiente (listas nuevas).</summary>
        public PlayerProgress Clone()
        {
            var copy = (PlayerProgress)MemberwiseClone();
            copy.stars = new List<int>(stars ?? new List<int>());
            copy.boosters = boosters != null ? (int[])boosters.Clone() : null;
            return copy;
        }

        /// <summary>
        /// Junta dos copias del progreso (este dispositivo y la nube):
        /// lo que solo sube (niveles, estrellas, experiencia, logros) se toma
        /// el máximo; lo que se gasta (monedas, gemas, potenciadores, vidas) y
        /// las preferencias se toman de la copia guardada más recientemente.
        /// </summary>
        public static PlayerProgress Merge(PlayerProgress a, PlayerProgress b)
        {
            if (a == null) return b?.Clone();
            if (b == null) return a.Clone();

            PlayerProgress newer = b.savedUtc > a.savedUtc ? b : a;
            PlayerProgress older = ReferenceEquals(newer, a) ? b : a;
            PlayerProgress result = newer.Clone();

            result.highestUnlocked = Math.Max(a.highestUnlocked, b.highestUnlocked);
            result.xp = Math.Max(a.xp, b.xp);
            result.specialsCreated = Math.Max(a.specialsCreated, b.specialsCreated);
            result.savedUtc = Math.Max(a.savedUtc, b.savedUtc);

            if (older.createdUtc > 0 && (result.createdUtc == 0 || older.createdUtc < result.createdUtc))
            {
                result.createdUtc = older.createdUtc;
            }

            int levels = Math.Max(a.stars?.Count ?? 0, b.stars?.Count ?? 0);
            result.stars = new List<int>(levels);
            for (int i = 0; i < levels; i++)
            {
                result.stars.Add(Math.Max(a.GetStars(i + 1), b.GetStars(i + 1)));
            }
            return result;
        }

        public const int XpStoryWin = 30;
        public const int XpPerStar = 10;
        public const int XpStoryLoss = 5;
        public const int XpRankedWin = 40;
        public const int XpRankedDraw = 25;
        public const int XpRankedLoss = 15;

        public void AddXp(int amount)
        {
            if (amount > 0) xp += amount;
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
