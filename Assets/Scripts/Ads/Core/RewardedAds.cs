using System;

/// <summary>
/// Claves de LevelPlay (dashboard de Unity → LevelPlay → Apps / Ad Units).
/// No son secretas: van dentro del juego. Vacías = sin anuncios reales.
/// </summary>
public static class AdsConfig
{
    public const string AndroidAppKey = "";
    public const string AndroidRewardedAdUnitId = "790fg51xj8xvr999";
}

/// <summary>Quien muestra anuncios con recompensa (LevelPlay, si el paquete está instalado).</summary>
public interface IRewardedAdProvider
{
    /// <summary>Hay un anuncio cargado listo para mostrar.</summary>
    bool IsReady { get; }

    /// <summary>Muestra el anuncio. done(true) si el jugador lo vio completo y gana el premio.</summary>
    void Show(Action<bool> done);
}

/// <summary>
/// Punto de encuentro: el módulo de LevelPlay se registra aquí solo cuando el
/// paquete está instalado; el juego pregunta aquí sin depender del SDK.
/// </summary>
public static class RewardedAds
{
    public static IRewardedAdProvider Provider { get; set; }
}
