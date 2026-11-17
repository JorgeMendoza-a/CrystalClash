using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrystalClash.Ranked.Logic;

/// <summary>Perfil de clasificatoria del jugador (lo guarda el servidor).</summary>
[Serializable]
public class RankedProfile
{
    public string PlayerId = "";
    public string Name = "";
    public int Points;
    public int Wins;
    public int Losses;
    public int Draws;

    public RankTier Tier => RankTiers.ForPoints(Points);
}

/// <summary>Partida encontrada: los dos jugadores reciben la misma semilla.</summary>
public class RankedMatchInfo
{
    public string MatchId = "";
    public int Seed;
    public string OpponentName = "";
    public int OpponentPoints;
}

public class OpponentProgress
{
    public int Score;
    public int MovesLeft;
    public bool Finished;
}

public class RankedMatchResult
{
    public int MyScore;
    public int OpponentScore;
    public MatchOutcome Outcome;
    public int PointsChange;
    public int NewPoints;
    public string OpponentName = "";
}

[Serializable]
public class LeaderboardEntry
{
    public int Rank;
    public string Name = "";
    public int Points;
    public bool IsMe;
}

[Serializable]
public class MatchHistoryEntry
{
    public string OpponentName = "";
    public int MyScore;
    public int OpponentScore;
    public int Outcome;        // (int)MatchOutcome, para poder guardarlo como JSON
    public int PointsChange;
    public long TimestampUtc;  // segundos desde 1970
}

/// <summary>
/// Todo lo que la clasificatoria necesita del servidor. Hay dos implementaciones:
/// - TestRankedService: rival simulado, sin internet (para probar).
/// - La de Unity Gaming Services (cuando se conecte el proyecto).
/// </summary>
public interface IRankedService
{
    /// <summary>Nombre para mostrar ("Unity Gaming Services", "Modo prueba"...).</summary>
    string DisplayName { get; }

    /// <summary>True si es la versión de prueba (rival simulado).</summary>
    bool IsTestMode { get; }

    Task SignInAsync();
    Task<RankedProfile> GetProfileAsync();

    /// <summary>Busca rival. Termina cuando hay partida o se cancela (OperationCanceledException).</summary>
    Task<RankedMatchInfo> FindMatchAsync(CancellationToken cancel);

    /// <summary>Envía mi puntaje parcial (para que el rival lo vea en vivo).</summary>
    Task ReportProgressAsync(string matchId, int score, int movesLeft);

    Task<OpponentProgress> GetOpponentProgressAsync(string matchId);

    /// <summary>
    /// Envía mis movimientos (no mi puntaje: el servidor lo recalcula) y espera
    /// a que el rival termine para devolver el resultado.
    /// </summary>
    Task<RankedMatchResult> SubmitMatchAsync(RankedMatchInfo match, string encodedMoves, CancellationToken cancel);

    Task<List<LeaderboardEntry>> GetLeaderboardAsync(int count);
    Task<List<MatchHistoryEntry>> GetHistoryAsync();
}

/// <summary>Elige la implementación del servicio de clasificatoria.</summary>
public static class RankedServices
{
    private static IRankedService current;

    /// <summary>
    /// Fábrica opcional que registra la implementación en línea (Unity Gaming
    /// Services) cuando está instalada. Si no hay, se usa el modo prueba.
    /// </summary>
    public static Func<IRankedService> OnlineFactory;

    public static IRankedService Current
    {
        get
        {
            if (current == null)
            {
                current = OnlineFactory != null ? OnlineFactory() : new TestRankedService();
            }
            return current;
        }
    }
}

/// <summary>Partida en curso (sobrevive a la recarga de escena).</summary>
public static class RankedMatchState
{
    public static RankedMatchInfo Current { get; set; }
    public static bool Active => Current != null;
}

/// <summary>Qué pantalla abrir al cargar la escena.</summary>
public static class AppNavigation
{
    public enum Screen { None, Home, Ranked }

    /// <summary>La primera vez que se abre el juego se muestra el inicio.</summary>
    public static Screen PendingScreen = Screen.Home;
}
