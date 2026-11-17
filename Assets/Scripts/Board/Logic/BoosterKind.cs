namespace CrystalClash.Board.Logic
{
    /// <summary>Potenciadores que el jugador puede usar sin gastar movimiento.</summary>
    public enum BoosterKind
    {
        /// <summary>Martillo: rompe un solo mineral.</summary>
        Hammer = 0,

        /// <summary>Bomba: rompe un área de 3x3.</summary>
        Bomb = 1,

        /// <summary>Rayo: rompe la fila y la columna completas.</summary>
        Lightning = 2,

        /// <summary>Mezclar: reorganiza el tablero.</summary>
        Shuffle = 3
    }
}
