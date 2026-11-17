using System.Collections.Generic;

namespace CrystalClash.Board.Logic
{
    /// <summary>Un mineral que cae de una celda a otra durante el colapso.</summary>
    public readonly struct MineralFall
    {
        public readonly GridPos From;
        public readonly GridPos To;

        public MineralFall(GridPos from, GridPos to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// Un mineral nuevo que entra por arriba. StartY es la fila (por encima del
    /// tablero) desde la que debería empezar a caer en la animación.
    /// </summary>
    public readonly struct MineralSpawn
    {
        public readonly GridPos Position;
        public readonly int Type;
        public readonly int StartY;

        public MineralSpawn(GridPos position, int type, int startY)
        {
            Position = position;
            Type = type;
            StartY = startY;
        }
    }

    /// <summary>
    /// Una "ola" de la cascada: qué se rompió, qué cayó y qué apareció.
    /// La vista la usa para animar; la lógica ya está aplicada.
    /// </summary>
    public sealed class CascadeStep
    {
        public int Level { get; }
        public List<GridPos> Cleared { get; } = new List<GridPos>();
        public List<MineralFall> Falls { get; } = new List<MineralFall>();
        public List<MineralSpawn> Spawns { get; } = new List<MineralSpawn>();
        public int Points { get; internal set; }

        public CascadeStep(int level)
        {
            Level = level;
        }
    }

    /// <summary>Resultado completo de intentar un intercambio.</summary>
    public sealed class SwapResult
    {
        public GridPos A { get; }
        public GridPos B { get; }
        public bool IsValid { get; internal set; }
        public List<CascadeStep> Steps { get; } = new List<CascadeStep>();
        public int PointsGained { get; internal set; }

        /// <summary>True si al final no quedaban movimientos y el tablero se reorganizó.</summary>
        public bool WasShuffled { get; internal set; }

        public SwapResult(GridPos a, GridPos b)
        {
            A = a;
            B = b;
        }
    }
}
