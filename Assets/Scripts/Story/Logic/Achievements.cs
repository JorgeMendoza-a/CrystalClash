using System.Collections.Generic;

namespace CrystalClash.Story.Logic
{
    public sealed class AchievementStatus
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Current { get; }
        public int Target { get; }
        public bool Unlocked => Current >= Target;

        public AchievementStatus(string id, string name, string description, int current, int target)
        {
            Id = id;
            Name = name;
            Description = description;
            Current = current < target ? current : target;
            Target = target;
        }
    }

    /// <summary>Logros que se muestran en el perfil (se calculan, no hace falta guardarlos).</summary>
    public static class Achievements
    {
        public const int CombosTarget = 100;
        public const int RankedWinsTarget = 10;
        public const int ChampionPoints = 8000;

        public static List<AchievementStatus> Evaluate(PlayerProgress progress, int totalLevels, int rankedWins, int rankedPoints)
        {
            int allStars = totalLevels * 3;
            return new List<AchievementStatus>
            {
                new AchievementStatus("combos", "Maestro\nde Combos", $"Crea {CombosTarget} minerales especiales",
                    progress.specialsCreated, CombosTarget),
                new AchievementStatus("battles", "Héroe\nde Batallas", $"Gana {RankedWinsTarget} partidas de clasificatoria",
                    rankedWins, RankedWinsTarget),
                new AchievementStatus("stars", "Leyenda\nde Cristales", "Consigue todas las estrellas del mundo",
                    progress.TotalStars, allStars > 0 ? allStars : 1),
                new AchievementStatus("champion", "Campeón de\nCrystal Clash", "Llega al rango Campeón",
                    rankedPoints, ChampionPoints),
            };
        }
    }
}
