/// <summary>
/// Tipos de mineral. El número de cada uno es el que usa BoardLogic.
/// Un nivel con N tipos usa los N primeros, así que los 6 de arriba son
/// los principales (bien distintos entre sí).
/// </summary>
public enum MineralType
{
    Sapphire,   // Zafiro
    Emerald,    // Esmeralda
    Amethyst,   // Amatista
    Ruby,       // Rubí
    Gold,       // Oro
    Carbon,     // Carbón
    Copper,     // Cobre
    Silver,     // Plata
    Diamond     // Diamante
}

public static class MineralNames
{
    private static readonly string[] names =
    {
        "Zafiro", "Esmeralda", "Amatista", "Rubí", "Oro", "Carbón", "Cobre", "Plata", "Diamante"
    };

    public static string Get(int type)
    {
        return type >= 0 && type < names.Length ? names[type] : "?";
    }
}
