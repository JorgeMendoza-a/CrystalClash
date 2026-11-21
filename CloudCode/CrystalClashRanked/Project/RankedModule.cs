using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CrystalClash.Ranked.Logic;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;

namespace CrystalClashRanked
{
    /// <summary>Registra el cliente de las APIs de Unity Gaming Services.</summary>
    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.AddGameApiClient();
        }
    }

    /// <summary>
    /// Servidor de la clasificatoria de Crystal Clash (módulo de Cloud Code).
    /// El juego solo envía MOVIMIENTOS; el servidor repite la partida con la
    /// misma lógica del tablero y decide los puntos. Los perfiles se guardan
    /// como datos protegidos (el jugador no los puede modificar).
    /// </summary>
    public class RankedModule
    {
        public const string LeaderboardId = "ranked_points";

        private const string ProfileKey = "ranked_profile";
        private const string StoryProgressKey = "story_progress";
        private const int MaxStoryProgressLength = 200_000;
        private const string QueueCustomId = "matchmaking";
        private const string CountersCustomId = "counters";
        private const string CountersKey = "player_ids";
        private const string QueueKey = "queue";
        private const string MatchKey = "state";
        private const int MaxRetries = 5;
        private const int HistorySize = 20;

        private static readonly Random random = new Random();

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ==================================================================
        // Funciones que llama el juego
        // ==================================================================

        [CloudCodeFunction("GetProfile")]
        public async Task<ProfileDto> GetProfile(IExecutionContext context, IGameApiClient api)
        {
            var (profile, _) = await LoadProfile(context, api, context.PlayerId);
            return ToDto(context.PlayerId, profile);
        }

        /// <summary>Cambia el nombre de clasificatoria (3-16 letras, números, _ y espacios).</summary>
        [CloudCodeFunction("SetName")]
        public async Task<ProfileDto> SetName(IExecutionContext context, IGameApiClient api, string name)
        {
            string clean = RankedRules.NormalizeName(name);
            if (clean == null)
            {
                throw new Exception($"Nombre inválido: usa de {RankedRules.NameMinLength} a {RankedRules.NameMaxLength} letras, números o _.");
            }

            string me = context.PlayerId;
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (profile, writeLock) = await LoadProfile(context, api, me);
                profile.Name = clean;
                if (!await TrySaveProfile(context, api, me, profile, writeLock)) continue;

                // Si ya está en la tabla de posiciones, que salga con el nombre nuevo.
                if (profile.Wins + profile.Losses + profile.Draws > 0) await UpdateLeaderboard(context, api, me, profile);
                return ToDto(me, profile);
            }

            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        /// <summary>Guarda el progreso del modo historia de este jugador (respaldo en la nube).</summary>
        [CloudCodeFunction("SaveProgress")]
        public async Task<bool> SaveProgress(IExecutionContext context, IGameApiClient api, string json, long savedUtc)
        {
            if (string.IsNullOrEmpty(json) || json.Length > MaxStoryProgressLength) throw new Exception("Progreso inválido.");
            JToken.Parse(json); // que sea JSON válido

            var dto = new StoryProgressDto { Exists = true, Json = json, SavedUtc = savedUtc };
            await api.CloudSaveData.SetProtectedItemAsync(
                context, context.ServiceToken, context.ProjectId, context.PlayerId,
                new SetItemBody(StoryProgressKey, JsonConvert.SerializeObject(dto)), default);
            return true;
        }

        /// <summary>Devuelve el progreso del modo historia guardado (Exists = false si no hay).</summary>
        [CloudCodeFunction("LoadProgress")]
        public async Task<StoryProgressDto> LoadProgress(IExecutionContext context, IGameApiClient api)
        {
            var response = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, context.PlayerId, new List<string> { StoryProgressKey }, null, default);
            Item item = response.Data.Results.FirstOrDefault(i => i.Key == StoryProgressKey);
            return item == null ? new StoryProgressDto { Exists = false } : Deserialize<StoryProgressDto>(item.Value);
        }

        /// <summary>Cambia la foto de perfil (la ve el rival en la pantalla de VS).</summary>
        [CloudCodeFunction("SetAvatar")]
        public async Task<ProfileDto> SetAvatar(IExecutionContext context, IGameApiClient api, string avatar)
        {
            string me = context.PlayerId;
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (profile, writeLock) = await LoadProfile(context, api, me);
                if (!ServerLogic.IsValidAvatar(profile, avatar)) throw new Exception("Foto de perfil no disponible.");
                if (profile.Avatar == avatar) return ToDto(me, profile);

                profile.Avatar = avatar;
                if (await TrySaveProfile(context, api, me, profile, writeLock)) return ToDto(me, profile);
            }
            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        [CloudCodeFunction("FindMatch")]
        public async Task<MatchmakingDto> FindMatch(IExecutionContext context, IGameApiClient api)
        {
            string me = context.PlayerId;
            var (profile, _) = await LoadProfile(context, api, me);

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (queue, writeLock) = await LoadCustom<QueueState>(context, api, QueueCustomId, QueueKey);
                long now = Now;
                ServerLogic.CleanQueue(queue, now);

                Ticket mine = queue.Tickets.FirstOrDefault(t => t.PlayerId == me);
                if (mine != null)
                {
                    // Ya estaba buscando: seguimos con ese ticket.
                    mine.LastPollUtc = now;
                    if (await TrySaveCustom(context, api, QueueCustomId, QueueKey, queue, writeLock))
                    {
                        return await TicketStatus(context, api, mine);
                    }
                    continue;
                }

                Ticket opponent = ServerLogic.PickOpponent(queue, me, profile.Points, now);
                if (opponent != null)
                {
                    MatchState match = NewMatch(
                        new MatchPlayer { PlayerId = opponent.PlayerId, Name = opponent.Name, Avatar = opponent.Avatar, PointsAtStart = opponent.Points },
                        new MatchPlayer { PlayerId = me, Name = profile.Name, Avatar = profile.Avatar, PointsAtStart = profile.Points });

                    opponent.MatchId = match.MatchId;
                    opponent.LastPollUtc = now;
                    if (!await TrySaveCustom(context, api, QueueCustomId, QueueKey, queue, writeLock)) continue;

                    await SaveCustom(context, api, MatchCustomId(match.MatchId), MatchKey, match, null);
                    return MatchFor(match, me, "");
                }

                var ticket = new Ticket
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PlayerId = me,
                    Name = profile.Name,
                    Avatar = profile.Avatar,
                    Points = profile.Points,
                    CreatedUtc = now,
                    LastPollUtc = now
                };
                queue.Tickets.Add(ticket);
                if (await TrySaveCustom(context, api, QueueCustomId, QueueKey, queue, writeLock))
                {
                    return new MatchmakingDto { TicketId = ticket.Id };
                }
            }

            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        [CloudCodeFunction("PollMatch")]
        public async Task<MatchmakingDto> PollMatch(IExecutionContext context, IGameApiClient api, string ticketId)
        {
            string me = context.PlayerId;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (queue, writeLock) = await LoadCustom<QueueState>(context, api, QueueCustomId, QueueKey);
                long now = Now;
                Ticket ticket = queue.Tickets.FirstOrDefault(t => t.Id == ticketId && t.PlayerId == me);
                if (ticket == null)
                {
                    // El ticket se perdió (p. ej. por inactividad): empezar de nuevo.
                    return await FindMatch(context, api);
                }

                ticket.LastPollUtc = now;

                if (ticket.MatchId != null)
                {
                    queue.Tickets.Remove(ticket);
                    if (!await TrySaveCustom(context, api, QueueCustomId, QueueKey, queue, writeLock)) continue;
                    return await TicketStatus(context, api, ticket);
                }

                if (now - ticket.CreatedUtc >= ServerLogic.BotAfterSeconds)
                {
                    // Nadie disponible: partida contra un bot.
                    queue.Tickets.Remove(ticket);
                    if (!await TrySaveCustom(context, api, QueueCustomId, QueueKey, queue, writeLock)) continue;

                    MatchState match = NewMatch(
                        new MatchPlayer { PlayerId = me, Name = ticket.Name, Avatar = ticket.Avatar, PointsAtStart = ticket.Points },
                        new MatchPlayer
                        {
                            PlayerId = "bot",
                            Name = ServerLogic.BotNamePrefix + "Minero" + random.Next(100, 999),
                            Avatar = "a" + random.Next(0, 8),
                            PointsAtStart = ticket.Points,
                            IsBot = true
                        });
                    await SaveCustom(context, api, MatchCustomId(match.MatchId), MatchKey, match, null);
                    return MatchFor(match, me, ticket.Id);
                }

                if (await TrySaveCustom(context, api, QueueCustomId, QueueKey, queue, writeLock))
                {
                    return new MatchmakingDto { TicketId = ticket.Id };
                }
            }

            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        [CloudCodeFunction("CancelMatchmaking")]
        public async Task<bool> CancelMatchmaking(IExecutionContext context, IGameApiClient api, string ticketId)
        {
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (queue, writeLock) = await LoadCustom<QueueState>(context, api, QueueCustomId, QueueKey);
                int removed = queue.Tickets.RemoveAll(t => t.Id == ticketId && t.PlayerId == context.PlayerId && t.MatchId == null);
                if (removed == 0) return false;
                if (await TrySaveCustom(context, api, QueueCustomId, QueueKey, queue, writeLock)) return true;
            }
            return false;
        }

        /// <summary>Guarda mi progreso (solo para mostrarlo) y devuelve el del rival.</summary>
        [CloudCodeFunction("ReportProgress")]
        public async Task<ProgressDto> ReportProgress(IExecutionContext context, IGameApiClient api, string matchId, int score, int movesLeft)
        {
            string me = context.PlayerId;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (match, writeLock) = await LoadMatch(context, api, matchId);
                MatchPlayer mine = Player(match, me);
                MatchPlayer other = match.Players.First(p => p.PlayerId != me);

                if (!mine.Submitted)
                {
                    mine.ReportedScore = Math.Max(0, score);
                    mine.MovesLeft = Math.Max(0, Math.Min(RankedRules.Moves, movesLeft));
                    if (!await TrySaveCustom(context, api, MatchCustomId(matchId), MatchKey, match, writeLock)) continue;
                }

                if (other.IsBot) return ServerLogic.BotProgress(match.Seed, match.CreatedUtc, Now);
                return new ProgressDto
                {
                    Score = other.Submitted ? other.FinalScore : other.ReportedScore,
                    MovesLeft = other.Submitted ? 0 : other.MovesLeft,
                    Finished = other.Submitted
                };
            }

            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        /// <summary>Recibe mis movimientos, los valida repitiendo la partida y, si se puede, decide el resultado.</summary>
        [CloudCodeFunction("SubmitMatch")]
        public async Task<ResultDto> SubmitMatch(IExecutionContext context, IGameApiClient api, string matchId, string moves)
        {
            string me = context.PlayerId;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (match, writeLock) = await LoadMatch(context, api, matchId);
                MatchPlayer mine = Player(match, me);

                if (!mine.Submitted)
                {
                    bool valid = RankedRules.TryReplay(match.Seed, moves ?? "", out int score, out _);
                    mine.Submitted = true;
                    mine.Invalid = !valid;
                    mine.FinalScore = valid ? score : 0;
                    mine.MovesLeft = 0;
                    if (!await TrySaveCustom(context, api, MatchCustomId(matchId), MatchKey, match, writeLock)) continue;
                }

                return await GetMatchResult(context, api, matchId);
            }

            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        [CloudCodeFunction("GetMatchResult")]
        public async Task<ResultDto> GetMatchResult(IExecutionContext context, IGameApiClient api, string matchId)
        {
            string me = context.PlayerId;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (match, writeLock) = await LoadMatch(context, api, matchId);
                Player(match, me);

                if (match.Finalized)
                {
                    return match.Results.TryGetValue(me, out ResultDto done) ? done : new ResultDto { Pending = true };
                }

                if (!ServerLogic.CanFinalize(match, Now)) return new ResultDto { Pending = true };

                // Puntos actuales de cada jugador humano (para no bajar de 0).
                var profiles = new Dictionary<string, RankedProfileState>();
                foreach (MatchPlayer p in match.Players.Where(p => !p.IsBot))
                {
                    profiles[p.PlayerId] = (await LoadProfile(context, api, p.PlayerId)).profile;
                }

                match.Results = ServerLogic.ComputeResults(match, id => profiles[id].Points);
                match.Finalized = true;

                // Quien logre guardar "Finalized" es quien aplica los puntos (una sola vez).
                if (!await TrySaveCustom(context, api, MatchCustomId(matchId), MatchKey, match, writeLock)) continue;

                foreach (var pair in match.Results)
                {
                    await ApplyResult(context, api, pair.Key, pair.Value);
                }

                return match.Results.TryGetValue(me, out ResultDto result) ? result : new ResultDto { Pending = true };
            }

            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        [CloudCodeFunction("GetLeaderboard")]
        public async Task<List<LeaderboardEntryDto>> GetLeaderboard(IExecutionContext context, IGameApiClient api, int limit)
        {
            limit = Math.Max(1, Math.Min(50, limit));
            var entries = new List<LeaderboardEntryDto>();
            int season = SeasonRules.CurrentSeason(Now);

            try
            {
                // Pedimos de más y nos quedamos con los de esta temporada (los que no
                // jugaron desde el reinicio aún tienen sus puntos viejos en la tabla).
                var response = await api.Leaderboards.GetLeaderboardScoresAsync(
                    context, context.ServiceToken, Guid.Parse(context.ProjectId), LeaderboardId, true, 0, Math.Min(200, limit * 4));

                foreach (LeaderboardEntry entry in response.Data.Results)
                {
                    if (SeasonFromMetadata(entry.Metadata) != season) continue;
                    entries.Add(new LeaderboardEntryDto
                    {
                        Rank = entries.Count + 1,
                        Name = NameFromMetadata(entry.Metadata) ?? entry.PlayerName,
                        Points = (int)entry.Score,
                        IsMe = entry.PlayerId == context.PlayerId
                    });
                    if (entries.Count >= limit) break;
                }
            }
            catch (ApiException e) when (e.Response != null && e.Response.StatusCode == HttpStatusCode.NotFound)
            {
                // Tabla vacía todavía.
            }

            return entries;
        }

        [CloudCodeFunction("GetHistory")]
        public async Task<List<HistoryEntryDto>> GetHistory(IExecutionContext context, IGameApiClient api)
        {
            var (profile, _) = await LoadProfile(context, api, context.PlayerId);
            return profile.History;
        }

        // ==================================================================
        // Ayudas
        // ==================================================================

        private static MatchState NewMatch(MatchPlayer first, MatchPlayer second)
        {
            first.MovesLeft = RankedRules.Moves;
            second.MovesLeft = RankedRules.Moves;
            return new MatchState
            {
                MatchId = Guid.NewGuid().ToString("N"),
                Seed = ServerLogic.NewSeed(random),
                CreatedUtc = Now,
                Players = new List<MatchPlayer> { first, second }
            };
        }

        private async Task<MatchmakingDto> TicketStatus(IExecutionContext context, IGameApiClient api, Ticket ticket)
        {
            if (ticket.MatchId == null) return new MatchmakingDto { TicketId = ticket.Id };
            var (match, _) = await LoadMatch(context, api, ticket.MatchId);
            return MatchFor(match, ticket.PlayerId, ticket.Id);
        }

        private static MatchmakingDto MatchFor(MatchState match, string me, string ticketId)
        {
            MatchPlayer other = match.Players.First(p => p.PlayerId != me);
            return new MatchmakingDto
            {
                TicketId = ticketId,
                Matched = true,
                MatchId = match.MatchId,
                Seed = match.Seed,
                OpponentName = other.Name,
                OpponentAvatar = other.Avatar,
                OpponentPoints = other.PointsAtStart,
                OpponentIsBot = other.IsBot
            };
        }

        private static MatchPlayer Player(MatchState match, string playerId)
        {
            MatchPlayer player = match.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new Exception("No participas en esta partida.");
            return player;
        }

        private async Task ApplyResult(IExecutionContext context, IGameApiClient api, string playerId, ResultDto result)
        {
            RankedProfileState profile = null;
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (loaded, writeLock) = await LoadProfile(context, api, playerId);
                profile = loaded;
                profile.Points = Math.Max(0, profile.Points + result.PointsChange);
                if (result.Outcome == (int)MatchOutcome.Win) profile.Wins++;
                else if (result.Outcome == (int)MatchOutcome.Loss) profile.Losses++;
                else profile.Draws++;

                profile.History.Insert(0, new HistoryEntryDto
                {
                    OpponentName = result.OpponentName,
                    MyScore = result.MyScore,
                    OpponentScore = result.OpponentScore,
                    Outcome = result.Outcome,
                    PointsChange = result.PointsChange,
                    TimestampUtc = Now
                });
                if (profile.History.Count > HistorySize) profile.History.RemoveRange(HistorySize, profile.History.Count - HistorySize);

                ServerLogic.ApplyPeak(profile);

                if (await TrySaveProfile(context, api, playerId, profile, writeLock)) break;
            }

            if (profile == null) return;
            await UpdateLeaderboard(context, api, playerId, profile);
        }

        /// <summary>Pone los puntos del jugador en la tabla (con su nombre y temporada).</summary>
        private async Task UpdateLeaderboard(IExecutionContext context, IGameApiClient api, string playerId, RankedProfileState profile)
        {
            try
            {
                await api.Leaderboards.AddLeaderboardPlayerScoreAsync(
                    context, context.ServiceToken, Guid.Parse(context.ProjectId), LeaderboardId, playerId,
                    new AddLeaderboardScore(profile.Points, new Dictionary<string, object>
                    {
                        { "name", profile.Name },
                        { "season", profile.Season }
                    }));
            }
            catch (ApiException)
            {
                // Si falla la tabla, el perfil ya quedó guardado; se corregirá en la próxima partida.
            }
        }

        /// <summary>Entrega (y borra) las recompensas pendientes: subir de rango, fin de temporada.</summary>
        [CloudCodeFunction("ClaimRewards")]
        public async Task<List<SeasonReward>> ClaimRewards(IExecutionContext context, IGameApiClient api)
        {
            string me = context.PlayerId;
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (profile, writeLock) = await LoadProfile(context, api, me);
                if (profile.PendingRewards.Count == 0) return new List<SeasonReward>();

                var rewards = new List<SeasonReward>(profile.PendingRewards);
                profile.PendingRewards.Clear();
                if (await TrySaveProfile(context, api, me, profile, writeLock)) return rewards;
            }
            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        private static int SeasonFromMetadata(object metadata)
        {
            try
            {
                if (metadata == null) return 0;
                JToken token = metadata is string s ? JToken.Parse(s) : JToken.FromObject(metadata);
                return token.Type == JTokenType.Object && token["season"] != null ? (int)token["season"] : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string NameFromMetadata(object metadata)
        {
            try
            {
                if (metadata == null) return null;
                JToken token = metadata is string s ? JToken.Parse(s) : JToken.FromObject(metadata);
                return token.Type == JTokenType.Object ? (string)token["name"] : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static ProfileDto ToDto(string playerId, RankedProfileState profile)
        {
            return new ProfileDto
            {
                PlayerId = playerId,
                ShortId = profile.ShortId,
                Avatar = profile.Avatar,
                Season = profile.Season,
                SeasonPeakPoints = profile.SeasonPeakPoints,
                Badges = profile.Badges,
                Name = profile.Name,
                Points = profile.Points,
                Wins = profile.Wins,
                Losses = profile.Losses,
                Draws = profile.Draws
            };
        }

        private static string MatchCustomId(string matchId) => "match_" + matchId;

        // ------------------------------------------------------------------
        // Cloud Save
        // ------------------------------------------------------------------

        private async Task<(RankedProfileState profile, string writeLock)> LoadProfile(IExecutionContext context, IGameApiClient api, string playerId)
        {
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var (profile, writeLock) = await ReadProfile(context, api, playerId);

                bool changed = false;
                if (profile == null)
                {
                    // Primera vez: perfil nuevo con su ID en orden y un nombre de minero.
                    int id = await NextPlayerId(context, api);
                    profile = new RankedProfileState { ShortId = id, Name = "Minero" + id, Points = RankedRules.StartingPoints };
                    changed = true;
                }
                else if (profile.ShortId == 0)
                {
                    // Cuentas creadas antes de que existieran los IDs: se les asigna ahora.
                    profile.ShortId = await NextPlayerId(context, api);
                    changed = true;
                }

                int seasonBefore = profile.Season;
                if (ServerLogic.ApplySeason(profile, Now)) changed = true;

                if (!changed) return (profile, writeLock);

                if (await TrySaveProfile(context, api, playerId, profile, writeLock))
                {
                    // Temporada nueva: la tabla de posiciones muestra los puntos reiniciados.
                    if (seasonBefore != 0 && seasonBefore != profile.Season) await UpdateLeaderboard(context, api, playerId, profile);

                    var (saved, savedLock) = await ReadProfile(context, api, playerId);
                    return (saved ?? profile, savedLock);
                }
                // Otro pedido guardó el perfil al mismo tiempo: lo leemos de nuevo (y el ID
                // que sacamos queda sin usar, que no es problema).
            }

            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        private async Task<(RankedProfileState profile, string writeLock)> ReadProfile(IExecutionContext context, IGameApiClient api, string playerId)
        {
            var response = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId, new List<string> { ProfileKey }, null, default);
            Item item = response.Data.Results.FirstOrDefault(i => i.Key == ProfileKey);
            return item == null ? (null, null) : (Deserialize<RankedProfileState>(item.Value), item.WriteLock);
        }

        /// <summary>Entrega el siguiente ID (100001, 100002...). Con bloqueo para que nunca se repita.</summary>
        private async Task<int> NextPlayerId(IExecutionContext context, IGameApiClient api)
        {
            for (int attempt = 0; attempt < MaxRetries * 2; attempt++)
            {
                var (counter, writeLock) = await LoadCustom<CounterState>(context, api, CountersCustomId, CountersKey);
                int id = Math.Max(counter.NextPlayerId, CounterState.FirstPlayerId);
                counter.NextPlayerId = id + 1;
                if (await TrySaveCustom(context, api, CountersCustomId, CountersKey, counter, writeLock)) return id;
            }
            throw new Exception("El servidor está ocupado, inténtalo de nuevo.");
        }

        private async Task<bool> TrySaveProfile(IExecutionContext context, IGameApiClient api, string playerId, RankedProfileState profile, string writeLock)
        {
            try
            {
                await api.CloudSaveData.SetProtectedItemAsync(
                    context, context.ServiceToken, context.ProjectId, playerId,
                    new SetItemBody(ProfileKey, JsonConvert.SerializeObject(profile), writeLock), default);
                return true;
            }
            catch (ApiException e) when (IsConflict(e))
            {
                return false;
            }
        }

        private async Task<(MatchState match, string writeLock)> LoadMatch(IExecutionContext context, IGameApiClient api, string matchId)
        {
            if (string.IsNullOrEmpty(matchId) || matchId.Length > 40) throw new Exception("Partida inválida.");
            var (match, writeLock) = await LoadCustom<MatchState>(context, api, MatchCustomId(matchId), MatchKey);
            if (string.IsNullOrEmpty(match.MatchId)) throw new Exception("La partida no existe.");
            return (match, writeLock);
        }

        private async Task<(T value, string writeLock)> LoadCustom<T>(IExecutionContext context, IGameApiClient api, string customId, string key)
            where T : new()
        {
            var response = await api.CloudSaveData.GetCustomItemsAsync(
                context, context.ServiceToken, context.ProjectId, customId, new List<string> { key }, null, default);
            Item item = response.Data.Results.FirstOrDefault(i => i.Key == key);
            return item == null ? (new T(), null) : (Deserialize<T>(item.Value), item.WriteLock);
        }

        private async Task SaveCustom<T>(IExecutionContext context, IGameApiClient api, string customId, string key, T value, string writeLock)
        {
            await api.CloudSaveData.SetCustomItemAsync(
                context, context.ServiceToken, context.ProjectId, customId,
                new SetItemBody(key, JsonConvert.SerializeObject(value), writeLock), default);
        }

        /// <summary>Guarda con bloqueo optimista: false si otro lo cambió antes (hay que reintentar).</summary>
        private async Task<bool> TrySaveCustom<T>(IExecutionContext context, IGameApiClient api, string customId, string key, T value, string writeLock)
        {
            try
            {
                await SaveCustom(context, api, customId, key, value, writeLock);
                return true;
            }
            catch (ApiException e) when (IsConflict(e))
            {
                return false;
            }
        }

        private static bool IsConflict(ApiException e)
        {
            return e.Response != null && e.Response.StatusCode == HttpStatusCode.Conflict;
        }

        private static T Deserialize<T>(object value) where T : new()
        {
            if (value == null) return new T();
            string json = value is string s ? s : (value is JToken token && token.Type == JTokenType.String ? (string)token : JsonConvert.SerializeObject(value));
            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }
    }
}
