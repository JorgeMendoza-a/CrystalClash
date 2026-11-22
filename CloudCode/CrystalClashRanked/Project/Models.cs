using System.Collections.Generic;
using CrystalClash.Ranked.Logic;

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

        /// <summary>Foto de perfil: "a3" (avatar normal 3), "e_dragon_fuego" (de la tienda) o una insignia ganada ("s1_oro").</summary>
        public string Avatar { get; set; } = "a0";

        /// <summary>Banner de la tienda ("b_volcan_ardiente"); vacío = sin banner.</summary>
        public string Banner { get; set; } = "";
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public List<HistoryEntryDto> History { get; set; } = new List<HistoryEntryDto>();

        // Temporadas
        public int Season { get; set; }
        public int SeasonPeakPoints { get; set; }

        /// <summary>Insignias exclusivas ganadas ("s1_oro", "s2_campeon"...).</summary>
        public List<string> Badges { get; set; } = new List<string>();

        /// <summary>Recompensas que el juego todavía no recogió (monedas/gemas y aviso).</summary>
        public List<SeasonReward> PendingRewards { get; set; } = new List<SeasonReward>();

        /// <summary>True cuando su ID corto ya está en el índice (para buscarlo al agregar amigos).</summary>
        public bool Indexed { get; set; }

        /// <summary>Última semana en la que ya se revisó el premio del torneo semanal anterior.</summary>
        public int WeeklyCheckedWeek { get; set; }
    }

    /// <summary>Tabla del torneo de una semana (dato global): PlayerId -> puntos.</summary>
    public class WeeklyState
    {
        public Dictionary<string, WeeklyEntry> Entries { get; set; } = new Dictionary<string, WeeklyEntry>();
    }

    /// <summary>Amigos y solicitudes de un jugador (datos protegidos del jugador). Guarda PlayerIds.</summary>
    public class FriendsState
    {
        public List<string> Friends { get; set; } = new List<string>();

        /// <summary>Solicitudes que me mandaron (esperan mi respuesta).</summary>
        public List<string> Incoming { get; set; } = new List<string>();

        /// <summary>Solicitudes que yo mandé.</summary>
        public List<string> Outgoing { get; set; } = new List<string>();
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
        public string Avatar { get; set; } = "a0";
        public string Banner { get; set; } = "";
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
        public string Avatar { get; set; } = "a0";
        public string Banner { get; set; } = "";
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

    /// <summary>Progreso del modo historia guardado en la nube (el JSON de PlayerProgress del juego).</summary>
    public class StoryProgressDto
    {
        public bool Exists { get; set; }
        public string Json { get; set; } = "";
        public long SavedUtc { get; set; }
    }

    public class ProfileDto
    {
        public string PlayerId { get; set; } = "";
        public int ShortId { get; set; }
        public string Avatar { get; set; } = "a0";
        public string Banner { get; set; } = "";
        public string Name { get; set; } = "";
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int Season { get; set; }
        public int SeasonPeakPoints { get; set; }
        public List<string> Badges { get; set; } = new List<string>();
    }

    /// <summary>Un amigo (o solicitud) como lo ve el juego.</summary>
    public class FriendDto
    {
        public string PlayerId { get; set; } = "";
        public int ShortId { get; set; }
        public string Name { get; set; } = "";
        public string Avatar { get; set; } = "a0";
        public string Banner { get; set; } = "";
        public int Points { get; set; }
    }

    public class FriendsDto
    {
        /// <summary>Resultado de la última acción: "ok", "sent", "friends", "already_friends", "already_sent", "self", "not_found", "full".</summary>
        public string Result { get; set; } = "ok";
        public List<FriendDto> Friends { get; set; } = new List<FriendDto>();
        public List<FriendDto> Incoming { get; set; } = new List<FriendDto>();
        public List<FriendDto> Outgoing { get; set; } = new List<FriendDto>();
    }

    public class MatchmakingDto
    {
        public string TicketId { get; set; } = "";
        public bool Matched { get; set; }
        public string MatchId { get; set; } = "";
        public int Seed { get; set; }
        public string OpponentName { get; set; } = "";
        public string OpponentAvatar { get; set; } = "a0";
        public string OpponentBanner { get; set; } = "";
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

    /// <summary>Perfil de otro jugador (al tocarlo en la tabla o en amigos).</summary>
    public class PublicProfileDto
    {
        public string PlayerId { get; set; } = "";
        public int ShortId { get; set; }
        public string Name { get; set; } = "";
        public string Avatar { get; set; } = "a0";
        public string Banner { get; set; } = "";
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public List<string> Badges { get; set; } = new List<string>();

        /// <summary>"self", "friend", "sent" (le mandé solicitud), "incoming" (me la mandó) o "none".</summary>
        public string FriendStatus { get; set; } = "none";
    }

    public class WeeklyRowDto
    {
        public int Rank { get; set; }
        public string PlayerId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Points { get; set; }
        public int Wins { get; set; }
        public bool IsMe { get; set; }
    }

    public class WeeklyDto
    {
        public int Week { get; set; }
        public long EndsUtc { get; set; }
        public List<WeeklyRowDto> Entries { get; set; } = new List<WeeklyRowDto>();
        public int MyRank { get; set; }
        public int MyPoints { get; set; }
    }

    public class LeaderboardEntryDto
    {
        public string PlayerId { get; set; } = "";
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
