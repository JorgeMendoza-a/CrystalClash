using System.Collections.Generic;

namespace CrystalClash.Board.Logic
{
    /// <summary>Un mineral que cae de una celda a otra durante el colapso (con su especial, si tiene).</summary>
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
    /// Un mineral nuevo (siempre normal) que entra por arriba. StartY es la fila
    /// (por encima del tablero) desde la que debería empezar a caer en la animación.
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

    /// <summary>Un especial que se activó (para efectos visuales y sonido).</summary>
    public readonly struct SpecialActivation
    {
        public readonly GridPos Position;
        public readonly SpecialKind Kind;

        public SpecialActivation(GridPos position, SpecialKind kind)
        {
            Position = position;
            Kind = kind;
        }
    }

    /// <summary>
    /// Un especial nuevo creado por una combinación grande. Se coloca DESPUÉS
    /// de limpiar las celdas de Cleared (su celda también está en Cleared).
    /// Type es BoardLogic.Colorless para el Prisma.
    /// </summary>
    public readonly struct SpecialCreation
    {
        public readonly GridPos Position;
        public readonly int Type;
        public readonly SpecialKind Kind;

        public SpecialCreation(GridPos position, int type, SpecialKind kind)
        {
            Position = position;
            Type = type;
            Kind = kind;
        }
    }

    /// <summary>
    /// Una "ola" de la cascada. Orden para animar:
    /// 1) Cleared (y Activations como efecto), 2) Created, 3) Falls, 4) Spawns.
    /// La lógica ya está aplicada cuando la vista lo recibe.
    /// </summary>
    public sealed class CascadeStep
    {
        /// <summary>1 = la jugada del jugador, 2+ = cascadas. Multiplica los puntos.</summary>
        public int Level { get; }

        public List<GridPos> Cleared { get; } = new List<GridPos>();

        /// <summary>
        /// Tipo que tenía cada celda de Cleared (mismo índice). Sirve para los
        /// objetivos del modo historia ("rompe 15 zafiros"). Colorless = Prisma.
        /// </summary>
        public List<int> ClearedTypes { get; } = new List<int>();
        public List<SpecialActivation> Activations { get; } = new List<SpecialActivation>();
        public List<SpecialCreation> Created { get; } = new List<SpecialCreation>();
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

        /// <summary>True si fue un intercambio de especiales (Prisma o dos especiales juntos).</summary>
        public bool WasSpecialCombo { get; internal set; }

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
