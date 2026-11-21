using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrystalClash.Ranked.Logic;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Clasificatoria EN LÍNEA con Unity Gaming Services. Todo pasa por el módulo
/// de Cloud Code "CrystalClashRanked" (carpeta CloudCode/ del repositorio):
/// emparejamiento, puntaje del rival, validación de la partida, ranking e
/// historial. El juego solo envía movimientos; el servidor decide los puntos.
/// </summary>
public class UgsRankedService : IRankedService
{
    public const string ModuleName = "CrystalClashRanked";

    private const string TestModeKey = "crystalclash.ranked.testmode";
    private const int PollMilliseconds = 2000;

    private static Task signInTask;
    private OpponentProgress lastOpponent;

    public string DisplayName => "Unity Gaming Services";
    public bool IsTestMode => false;

    /// <summary>Modo prueba (rival simulado sin servidor), se cambia en Ajustes.</summary>
    public static bool TestModeEnabled
    {
        get => PlayerPrefs.GetInt(TestModeKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(TestModeKey, value ? 1 : 0);
            PlayerPrefs.Save();
            RankedServices.Reset();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        RankedServices.OnlineFactory = () => TestModeEnabled ? null : new UgsRankedService();
    }

    // ------------------------------------------------------------------
    // IRankedService
    // ------------------------------------------------------------------

    public Task SignInAsync()
    {
        if (signInTask == null || signInTask.IsFaulted || signInTask.IsCanceled)
        {
            signInTask = SignInInternal();
        }
        return signInTask;
    }

    public async Task<RankedProfile> GetProfileAsync()
    {
        await SignInAsync();
        ProfileDto dto = await Call<ProfileDto>("GetProfile");
        return new RankedProfile
        {
            PlayerId = dto.PlayerId,
            ShortId = dto.ShortId,
            Avatar = string.IsNullOrEmpty(dto.Avatar) ? "a0" : dto.Avatar,
            Season = dto.Season,
            SeasonPeakPoints = dto.SeasonPeakPoints,
            Badges = dto.Badges ?? new List<string>(),
            Name = dto.Name,
            Points = dto.Points,
            Wins = dto.Wins,
            Losses = dto.Losses,
            Draws = dto.Draws
        };
    }

    public async Task<RankedProfile> SetNameAsync(string name)
    {
        await SignInAsync();
        ProfileDto dto = await Call<ProfileDto>("SetName", new Dictionary<string, object> { { "name", name } });
        return new RankedProfile
        {
            PlayerId = dto.PlayerId,
            ShortId = dto.ShortId,
            Avatar = string.IsNullOrEmpty(dto.Avatar) ? "a0" : dto.Avatar,
            Season = dto.Season,
            SeasonPeakPoints = dto.SeasonPeakPoints,
            Badges = dto.Badges ?? new List<string>(),
            Name = dto.Name,
            Points = dto.Points,
            Wins = dto.Wins,
            Losses = dto.Losses,
            Draws = dto.Draws
        };
    }

    public async Task SetAvatarAsync(string avatar)
    {
        await SignInAsync();
        await Call<ProfileDto>("SetAvatar", new Dictionary<string, object> { { "avatar", avatar } });
    }

    public async Task<RankedMatchInfo> FindMatchAsync(CancellationToken cancel)
    {
        await SignInAsync();
        MatchmakingDto status = await Call<MatchmakingDto>("FindMatch");

        try
        {
            while (!status.Matched)
            {
                await Task.Delay(PollMilliseconds, cancel);
                status = await Call<MatchmakingDto>("PollMatch", new Dictionary<string, object> { { "ticketId", status.TicketId } });
            }
        }
        catch (OperationCanceledException)
        {
            // Avisamos al servidor que ya no buscamos (sin esperar la respuesta).
            string ticket = status.TicketId;
            _ = Call<bool>("CancelMatchmaking", new Dictionary<string, object> { { "ticketId", ticket } });
            throw;
        }

        lastOpponent = null;
        return new RankedMatchInfo
        {
            MatchId = status.MatchId,
            Seed = status.Seed,
            OpponentName = status.OpponentName,
            OpponentAvatar = string.IsNullOrEmpty(status.OpponentAvatar) ? "a0" : status.OpponentAvatar,
            OpponentPoints = status.OpponentPoints,
            OpponentIsBot = status.OpponentIsBot
        };
    }

    public async Task ReportProgressAsync(string matchId, int score, int movesLeft)
    {
        ProgressDto dto = await Call<ProgressDto>("ReportProgress", new Dictionary<string, object>
        {
            { "matchId", matchId },
            { "score", score },
            { "movesLeft", movesLeft }
        });
        lastOpponent = new OpponentProgress { Score = dto.Score, MovesLeft = dto.MovesLeft, Finished = dto.Finished };
    }

    public Task<OpponentProgress> GetOpponentProgressAsync(string matchId)
    {
        return Task.FromResult(lastOpponent ?? new OpponentProgress { MovesLeft = RankedRules.Moves });
    }

    public async Task<RankedMatchResult> SubmitMatchAsync(RankedMatchInfo match, string encodedMoves, CancellationToken cancel)
    {
        ResultDto result = await Call<ResultDto>("SubmitMatch", new Dictionary<string, object>
        {
            { "matchId", match.MatchId },
            { "moves", encodedMoves }
        });

        // Esperamos a que el rival termine (el servidor decide cuando ambos enviaron o se acabó el tiempo).
        while (result.Pending)
        {
            await Task.Delay(PollMilliseconds, cancel);
            result = await Call<ResultDto>("GetMatchResult", new Dictionary<string, object> { { "matchId", match.MatchId } });
        }

        return new RankedMatchResult
        {
            MyScore = result.MyScore,
            OpponentScore = result.OpponentScore,
            Outcome = (MatchOutcome)result.Outcome,
            PointsChange = result.PointsChange,
            NewPoints = result.NewPoints,
            OpponentName = result.OpponentName
        };
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int count)
    {
        await SignInAsync();
        List<LeaderboardEntryDto> dtos = await Call<List<LeaderboardEntryDto>>("GetLeaderboard",
            new Dictionary<string, object> { { "limit", count } });

        var entries = new List<LeaderboardEntry>();
        foreach (LeaderboardEntryDto dto in dtos ?? new List<LeaderboardEntryDto>())
        {
            entries.Add(new LeaderboardEntry { Rank = dto.Rank, Name = dto.Name, Points = dto.Points, IsMe = dto.IsMe });
        }
        return entries;
    }

    public async Task<List<SeasonReward>> ClaimRewardsAsync()
    {
        await SignInAsync();
        return await Call<List<SeasonReward>>("ClaimRewards") ?? new List<SeasonReward>();
    }

    public async Task<List<MatchHistoryEntry>> GetHistoryAsync()
    {
        await SignInAsync();
        List<HistoryEntryDto> dtos = await Call<List<HistoryEntryDto>>("GetHistory");

        var history = new List<MatchHistoryEntry>();
        foreach (HistoryEntryDto dto in dtos ?? new List<HistoryEntryDto>())
        {
            history.Add(new MatchHistoryEntry
            {
                OpponentName = dto.OpponentName,
                MyScore = dto.MyScore,
                OpponentScore = dto.OpponentScore,
                Outcome = dto.Outcome,
                PointsChange = dto.PointsChange,
                TimestampUtc = dto.TimestampUtc
            });
        }
        return history;
    }

    // ------------------------------------------------------------------

    private static async Task SignInInternal()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        IAuthenticationService auth = AuthenticationService.Instance;
        if (auth.IsSignedIn) return;

#if UNITY_EDITOR
        // En el editor, cada copia del proyecto (p. ej. jugadores virtuales de
        // Multiplayer Play Mode) usa su propia cuenta, para poder jugar contra uno mismo.
        string profile = EditorProfileName();
        if (auth.Profile != profile) auth.SwitchProfile(profile);
#endif

        await auth.SignInAnonymouslyAsync();
    }

#if UNITY_EDITOR
    private static string EditorProfileName()
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in Application.dataPath) hash = hash * 31 + c;
            return "editor" + ((uint)hash).ToString("x8");
        }
    }
#endif

    private static Task<T> Call<T>(string function, Dictionary<string, object> args = null)
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<T>(ModuleName, function, args ?? new Dictionary<string, object>());
    }

    // ------------------------------------------------------------------
    // Respuestas del servidor (mismos nombres que en CloudCode/.../Models.cs)
    // ------------------------------------------------------------------

    [Serializable]
    public class ProfileDto
    {
        public string PlayerId;
        public int ShortId;
        public string Avatar;
        public int Season;
        public int SeasonPeakPoints;
        public List<string> Badges;
        public string Name;
        public int Points;
        public int Wins;
        public int Losses;
        public int Draws;
    }

    [Serializable]
    public class MatchmakingDto
    {
        public string TicketId;
        public bool Matched;
        public string MatchId;
        public int Seed;
        public string OpponentName;
        public string OpponentAvatar;
        public int OpponentPoints;
        public bool OpponentIsBot;
    }

    [Serializable]
    public class ProgressDto
    {
        public int Score;
        public int MovesLeft;
        public bool Finished;
    }

    [Serializable]
    public class ResultDto
    {
        public bool Pending;
        public int MyScore;
        public int OpponentScore;
        public int Outcome;
        public int PointsChange;
        public int NewPoints;
        public string OpponentName;
        public bool OpponentIsBot;
    }

    [Serializable]
    public class LeaderboardEntryDto
    {
        public int Rank;
        public string Name;
        public int Points;
        public bool IsMe;
    }

    [Serializable]
    public class HistoryEntryDto
    {
        public string OpponentName;
        public int MyScore;
        public int OpponentScore;
        public int Outcome;
        public int PointsChange;
        public long TimestampUtc;
    }
}
