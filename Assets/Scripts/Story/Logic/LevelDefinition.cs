using System;
using System.Collections.Generic;
using CrystalClash.Board.Logic;

namespace CrystalClash.Story.Logic
{
    public enum ObjectiveKind
    {
        /// <summary>Romper N minerales de un tipo (los especiales de ese color también cuentan).</summary>
        CollectMineral,

        /// <summary>Llegar a N puntos.</summary>
        ReachScore
    }

    public sealed class LevelObjective
    {
        public ObjectiveKind Kind { get; }

        /// <summary>Tipo de mineral (solo para CollectMineral). Coincide con el enum MineralType.</summary>
        public int MineralType { get; }

        public int Target { get; }

        private LevelObjective(ObjectiveKind kind, int mineralType, int target)
        {
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
            Kind = kind;
            MineralType = mineralType;
            Target = target;
        }

        public static LevelObjective Collect(int mineralType, int count) =>
            new LevelObjective(ObjectiveKind.CollectMineral, mineralType, count);

        public static LevelObjective Score(int points) =>
            new LevelObjective(ObjectiveKind.ReachScore, -1, points);
    }

    /// <summary>Datos de un nivel del modo historia (sin Unity: se puede validar en tests o servidor).</summary>
    public sealed class LevelDefinition
    {
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public int Width { get; set; } = 8;
        public int Height { get; set; } = 8;
        public int MineralTypeCount { get; set; } = 6;

        /// <summary>0 = tablero distinto en cada intento.</summary>
        public int Seed { get; set; }

        public int MaxMoves { get; set; } = 25;
        public List<LevelObjective> Objectives { get; } = new List<LevelObjective>();

        /// <summary>Puntos (finales, con bonus) para 1, 2 y 3 estrellas.</summary>
        public int[] StarScores { get; set; } = { 0, 0, 0 };

        /// <summary>Lanza ArgumentException con un mensaje claro si el nivel está mal hecho.</summary>
        public void Validate()
        {
            string where = $"Nivel {Number}";

            if (Width < BoardLogic.MinSize || Height < BoardLogic.MinSize)
                throw new ArgumentException($"{where}: el tablero debe ser al menos {BoardLogic.MinSize}x{BoardLogic.MinSize}");
            if (MineralTypeCount < BoardLogic.MinMineralTypes)
                throw new ArgumentException($"{where}: necesita al menos {BoardLogic.MinMineralTypes} tipos de mineral");
            if (MaxMoves <= 0)
                throw new ArgumentException($"{where}: los movimientos deben ser mayores que 0");
            if (Objectives.Count == 0)
                throw new ArgumentException($"{where}: necesita al menos un objetivo");
            if (StarScores == null || StarScores.Length != 3)
                throw new ArgumentException($"{where}: 'stars' debe tener exactamente 3 valores");
            if (StarScores[0] > StarScores[1] || StarScores[1] > StarScores[2])
                throw new ArgumentException($"{where}: los puntos de las estrellas deben ir de menor a mayor");

            foreach (LevelObjective objective in Objectives)
            {
                if (objective.Kind == ObjectiveKind.CollectMineral &&
                    (objective.MineralType < 0 || objective.MineralType >= MineralTypeCount))
                {
                    throw new ArgumentException(
                        $"{where}: el objetivo pide el mineral {objective.MineralType}, pero el nivel solo usa los primeros {MineralTypeCount}");
                }
            }
        }
    }
}
