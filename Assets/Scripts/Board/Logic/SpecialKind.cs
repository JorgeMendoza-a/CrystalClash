namespace CrystalClash.Board.Logic
{
    /// <summary>Minerales especiales que se crean al hacer combinaciones grandes.</summary>
    public enum SpecialKind
    {
        None = 0,

        /// <summary>Taladro horizontal (4 en línea horizontal): limpia toda su fila.</summary>
        LineH = 1,

        /// <summary>Taladro vertical (4 en línea vertical): limpia toda su columna.</summary>
        LineV = 2,

        /// <summary>Dinamita (combinación en L o T): explota un área de 3x3.</summary>
        Bomb = 3,

        /// <summary>Prisma (5 en línea): sin color; al intercambiarlo limpia todo un color.</summary>
        Prism = 4
    }
}
