using System.Collections.Generic;

namespace CrystalClashRanked
{
    // ------------------------------------------------------------------
    // Estado guardado en Cloud Save (solo lo escribe el servidor)
    // ------------------------------------------------------------------

    /// <summary>Perfil de clasificatoria (datos protegidos del jugador).</summary>
    public class RankedProfileState
    {
        /// <summary>ID público en orden de registro (100001, 100002...). 0 = aún sin asignar.</summary>
        public int ShortId { get; set; }

        public string Name { get; set; } = "";
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public List<HistoryEntryDto> History { get; set; } = new List<HistoryEntryDto>();
    }

    /// <summary>Contador global de IDs de jugador.</summary>
    public class CounterState
    {
        public const int FirstPlayerId = 100001;

        /// <summary>El próximo ID que se va a entregar.</summary>
        public int NextPlayerId { get; set; } = FirstPlayerId;
    }

    /// <summary>Cola de emparejamiento (dato global del juego).</summary>
    public class QueueState
    {
        public List<Ticket> Tickets { get; set; } = new List<Ticket>();
    }

    public class Ticket
    {
        public string Id { get; set; } = "";
        public string PlayerId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Points { get; set; }
        public long CreatedUtc { get; set; }
        public long LastPollUtc { get; set; }

        /// <summary>Se llena cuando otro jugador lo emparejó.</summary>
        public string MatchId { get; set; }
    }

    /// <summary>Una partida 1 vs 1 (dato global del juego).</summary>
    public class MatchState
    {
        public string MatchId { get; set; } = "";
        public int Seed { get; set; }
        public long CreatedUtc { get; set; }
        public List<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
        public bool Finalized { get; set; }
        public Dictionary<string, ResultDto> Results { get; set; } = new Dictionary<string, ResultDto>();
    }

    public class MatchPlayer
    {
        public string PlayerId { get; set; } = "";
        public string Name { get; set; } = "";
        public int PointsAtStart { get; set; }
        public bool IsBot { get; set; }

        // Progreso informado por el cliente (solo para mostrar, no se usa para decidir)
        public int ReportedScore { get; set; }
        public int MovesLeft { get; set; }

        // Resultado validado por el servidor
        public bool Submitted { get; set; }
        public int FinalScore { get; set; }
        public bool Invalid { get; set; }
    }

    // ------------------------------------------------------------------
    // Respuestas al juego
    // ------------------------------------------------------------------

    public class ProfileDto
    {
        public string PlayerId { get; set; } = "";
        public int ShortId { get; set; }
        public string Name { get; set; } = "";
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
    }

    public class MatchmakingDto
    {
        public string TicketId { get; set; } = "";
        public bool Matched { get; set; }
        public string MatchId { get; set; } = "";
        public int Seed { get; set; }
        public string OpponentName { get; set; } = "";
        public int OpponentPoints { get; set; }
        public bool OpponentIsBot { get; set; }
    }

    public class ProgressDto
    {
        public int Score { get; set; }
        public int MovesLeft { get; set; }
        public bool Finished { get; set; }
    }

    public class ResultDto
    {
        public bool Pending { get; set; }
        public int MyScore { get; set; }
        public int OpponentScore { get; set; }
        public int Outcome { get; set; }       // 0 = derrota, 1 = empate, 2 = victoria
        public int PointsChange { get; set; }
        public int NewPoints { get; set; }
        public string OpponentName { get; set; } = "";
        public bool OpponentIsBot { get; set; }
    }

    public class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public string Name { get; set; } = "";
        public int Points { get; set; }
        public bool IsMe { get; set; }
    }

    public class HistoryEntryDto
    {
        public string OpponentName { get; set; } = "";
        public int MyScore { get; set; }
        public int OpponentScore { get; set; }
        public int Outcome { get; set; }
        public int PointsChange { get; set; }
        public long TimestampUtc { get; set; }
    }
}
