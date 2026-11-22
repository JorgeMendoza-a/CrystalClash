using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrystalClash.Board.Logic;
using CrystalClash.Ranked.Logic;
using UnityEngine;

/// <summary>
/// Clasificatoria de PRUEBA, sin servidor: el rival es un bot que juega el
/// mismo tablero (misma semilla) a ritmo humano. Sirve para probar toda la
/// pantalla y el flujo de partida mientras se conecta Unity Gaming Services.
/// El perfil y el historial se guardan en el dispositivo.
/// </summary>
public class TestRankedService : IRankedService
{
    private const string ProfileKey = "crystalclash.ranked.test.profile";
    private const string HistoryKey = "crystalclash.ranked.test.history";

    private static readonly string[] BotNames =
    {
        "MineroVeloz", "PicoDeOro", "Gemita99", "RocaDura", "ZafiroKing", "LaAmatista", "CarbonRey", "TopoMinero"
    };

    [Serializable]
    private class HistoryList
    {
        public List<MatchHistoryEntry> items = new List<MatchHistoryEntry>();
    }

    private class BotMatch
    {
        public BoardLogic Board;
        public System.Random Random;
        public int MovesLeft;
        public float StartTime;
        public float SecondsPerMove;
    }

    private readonly Dictionary<string, BotMatch> bots = new Dictionary<string, BotMatch>();
    private readonly List<SeasonReward> pendingRewards = new List<SeasonReward>();
    private RankedProfile profile;

    public string DisplayName => "Modo prueba (rival simulado)";
    public bool IsTestMode => true;

    public Task SignInAsync()
    {
        LoadProfile();
        return Task.CompletedTask;
    }

    public Task<RankedProfile> GetProfileAsync()
    {
        LoadProfile();
        return Task.FromResult(profile);
    }

    public Task<RankedProfile> SetNameAsync(string name)
    {
        string clean = RankedRules.NormalizeName(name);
        if (clean == null)
        {
            throw new ArgumentException($"Usa de {RankedRules.NameMinLength} a {RankedRules.NameMaxLength} letras, números o _.");
        }
        LoadProfile();
        profile.Name = clean;
        SaveProfile();
        return Task.FromResult(profile);
    }

    public Task SetCosmeticsAsync(string avatar, string banner)
    {
        LoadProfile();
        profile.Avatar = avatar;
        profile.Banner = banner ?? "";
        SaveProfile();
        return Task.CompletedTask;
    }

    public async Task<RankedMatchInfo> FindMatchAsync(CancellationToken cancel)
    {
        LoadProfile();
        await Task.Delay(UnityEngine.Random.Range(1500, 3500), cancel);

        int seed = UnityEngine.Random.Range(1, int.MaxValue);
        var match = new RankedMatchInfo
        {
            MatchId = Guid.NewGuid().ToString("N"),
            Seed = seed,
            OpponentName = BotNames[UnityEngine.Random.Range(0, BotNames.Length)],
            OpponentAvatar = UnityEngine.Random.Range(0, 3) == 0
                ? Cosmetics.Avatars[UnityEngine.Random.Range(0, Cosmetics.Avatars.Length)].Id
                : "a" + UnityEngine.Random.Range(0, 8),
            OpponentBanner = UnityEngine.Random.Range(0, 2) == 0
                ? Cosmetics.Banners[UnityEngine.Random.Range(0, Cosmetics.Banners.Length)].Id
                : "",
            OpponentPoints = Mathf.Max(0, profile.Points + UnityEngine.Random.Range(-150, 151))
        };

        bots[match.MatchId] = new BotMatch
        {
            Board = RankedRules.CreateBoard(seed),
            Random = new System.Random(seed ^ 0x5bd1e995),
            MovesLeft = RankedRules.Moves,
            StartTime = Time.realtimeSinceStartup,
            SecondsPerMove = UnityEngine.Random.Range(4f, 6.5f)
        };
        return match;
    }

    public Task ReportProgressAsync(string matchId, int score, int movesLeft)
    {
        return Task.CompletedTask;
    }

    public Task<OpponentProgress> GetOpponentProgressAsync(string matchId)
    {
        if (!bots.TryGetValue(matchId, out BotMatch bot)) return Task.FromResult(new OpponentProgress());

        // El bot hace las jugadas que "le tocan" según el tiempo que pasó.
        float elapsed = Time.realtimeSinceStartup - bot.StartTime;
        int shouldHaveMade = Mathf.Min(RankedRules.Moves, Mathf.FloorToInt(elapsed / bot.SecondsPerMove));
        while (RankedRules.Moves - bot.MovesLeft < shouldHaveMade) PlayBotMove(bot);

        return Task.FromResult(new OpponentProgress
        {
            Score = bot.Board.Score,
            MovesLeft = bot.MovesLeft,
            Finished = bot.MovesLeft == 0
        });
    }

    public async Task<RankedMatchResult> SubmitMatchAsync(RankedMatchInfo match, string encodedMoves, CancellationToken cancel)
    {
        // Igual que hará el servidor: no nos fiamos del puntaje, repetimos los movimientos.
        if (!RankedRules.TryReplay(match.Seed, encodedMoves, out int myScore, out string error))
        {
            throw new InvalidOperationException("Partida inválida: " + error);
        }

        // "Esperar al rival": el bot termina sus jugadas restantes.
        await Task.Delay(1200, cancel);
        int opponentScore = 0;
        if (bots.TryGetValue(match.MatchId, out BotMatch bot))
        {
            while (bot.MovesLeft > 0) PlayBotMove(bot);
            opponentScore = bot.Board.Score;
            bots.Remove(match.MatchId);
        }

        LoadProfile();
        MatchOutcome outcome = RankedRules.Compare(myScore, opponentScore);
        int change = RankedRules.PointsChange(profile.Points, match.OpponentPoints, outcome);

        profile.Points += change;
        foreach (SeasonReward reward in SeasonRules.RewardsForPeak(profile.SeasonPeakPoints, profile.Points, profile.Season))
        {
            pendingRewards.Add(reward);
            if (!string.IsNullOrEmpty(reward.Badge) && !profile.Badges.Contains(reward.Badge)) profile.Badges.Add(reward.Badge);
        }
        profile.SeasonPeakPoints = Mathf.Max(profile.SeasonPeakPoints, profile.Points);
        if (outcome == MatchOutcome.Win) profile.Wins++;
        else if (outcome == MatchOutcome.Loss) profile.Losses++;
        else profile.Draws++;
        SaveProfile();

        AddHistory(new MatchHistoryEntry
        {
            OpponentName = match.OpponentName,
            MyScore = myScore,
            OpponentScore = opponentScore,
            Outcome = (int)outcome,
            PointsChange = change,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        return new RankedMatchResult
        {
            MyScore = myScore,
            OpponentScore = opponentScore,
            Outcome = outcome,
            PointsChange = change,
            NewPoints = profile.Points,
            OpponentName = match.OpponentName
        };
    }

    public Task<List<LeaderboardEntry>> GetLeaderboardAsync(int count)
    {
        LoadProfile();
        var entries = new List<LeaderboardEntry>();
        var rnd = new System.Random(7);
        int points = 8600;
        foreach (string name in BotNames)
        {
            entries.Add(new LeaderboardEntry { PlayerId = "test_" + name, Name = name, Points = points });
            points -= rnd.Next(300, 1300);
        }
        entries.Add(new LeaderboardEntry { Name = profile.Name + " (tú)", Points = profile.Points, IsMe = true });
        entries.Sort((a, b) => b.Points.CompareTo(a.Points));
        for (int i = 0; i < entries.Count; i++) entries[i].Rank = i + 1;
        return Task.FromResult(entries.GetRange(0, Math.Min(count, entries.Count)));
    }

    public Task<List<SeasonReward>> ClaimRewardsAsync()
    {
        var rewards = new List<SeasonReward>(pendingRewards);
        pendingRewards.Clear();
        return Task.FromResult(rewards);
    }

    public Task<List<MatchHistoryEntry>> GetHistoryAsync()
    {
        return Task.FromResult(LoadHistory().items);
    }

    // ------------------------------------------------------------------

    // Amigos simulados (modo prueba): agregar un ID crea un amigo falso al instante.
    private static readonly FriendsList testFriends = new FriendsList();

    public Task<FriendsList> GetFriendsAsync()
    {
        testFriends.Result = "ok";
        return Task.FromResult(testFriends);
    }

    public Task<FriendsList> SendFriendRequestAsync(int shortId)
    {
        LoadProfile();
        if (shortId == profile.ShortId) testFriends.Result = "self";
        else if (testFriends.Friends.Exists(f => f.ShortId == shortId)) testFriends.Result = "already_friends";
        else
        {
            testFriends.Friends.Add(new FriendInfo
            {
                PlayerId = "test" + shortId,
                ShortId = shortId,
                Name = BotNames[UnityEngine.Random.Range(0, BotNames.Length)],
                Avatar = "a" + UnityEngine.Random.Range(0, 8),
                Points = UnityEngine.Random.Range(0, 2500)
            });
            testFriends.Result = "friends";
        }
        return Task.FromResult(testFriends);
    }

    public Task<FriendsList> RespondFriendRequestAsync(string playerId, bool accept)
    {
        testFriends.Result = "ok";
        return Task.FromResult(testFriends);
    }

    public Task<FriendsList> SendFriendRequestToPlayerAsync(string playerId)
    {
        testFriends.Result = "sent";
        return Task.FromResult(testFriends);
    }

    public Task<WeeklyBoard> GetWeeklyAsync(int count)
    {
        LoadProfile();
        int week = WeeklyRules.Week(GameClockNow());
        var board = new WeeklyBoard { Week = week, EndsUtc = WeeklyRules.WeekEndUtc(week) };
        string[] names = { BotNames[0], BotNames[1 % BotNames.Length], BotNames[2 % BotNames.Length] };
        int[] points = { 21, 15, 9 };
        for (int i = 0; i < names.Length; i++)
        {
            board.Entries.Add(new WeeklyRow { Rank = i + 1, PlayerId = "test_" + names[i], Name = names[i], Points = points[i], Wins = points[i] / 3 });
        }
        board.Entries.Add(new WeeklyRow { Rank = 4, PlayerId = profile.PlayerId, Name = profile.Name, Points = 6, Wins = 2, IsMe = true });
        board.MyRank = 4;
        board.MyPoints = 6;
        return Task.FromResult(board);
    }

    private static long GameClockNow() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public Task<PublicProfile> GetPublicProfileAsync(string playerId)
    {
        LoadProfile();
        FriendInfo friend = testFriends.Friends.Find(f => f.PlayerId == playerId);
        return Task.FromResult(new PublicProfile
        {
            PlayerId = playerId,
            ShortId = friend != null ? friend.ShortId : 0,
            Name = friend != null ? friend.Name : "Jugador",
            Avatar = friend != null ? friend.Avatar : "a1",
            Points = friend != null ? friend.Points : 300,
            Wins = 3,
            Losses = 2,
            FriendStatus = friend != null ? "friend" : "none"
        });
    }

    public Task<FriendsList> RemoveFriendAsync(string playerId)
    {
        testFriends.Friends.RemoveAll(f => f.PlayerId == playerId);
        testFriends.Result = "ok";
        return Task.FromResult(testFriends);
    }

    private static void PlayBotMove(BotMatch bot)
    {
        if (bot.MovesLeft <= 0) return;

        // Elige entre varias jugadas válidas al azar (juega "como persona", no perfecto).
        var options = new List<(GridPos, GridPos)>();
        BoardLogic board = bot.Board;
        for (int x = 0; x < board.Width && options.Count < 12; x++)
        {
            for (int y = 0; y < board.Height && options.Count < 12; y++)
            {
                var from = new GridPos(x, y);
                foreach (var to in new[] { new GridPos(x + 1, y), new GridPos(x, y + 1) })
                {
                    if (!board.IsInside(to)) continue;
                    if (board.GetMineral(from) == board.GetMineral(to) && board.GetSpecial(from) == SpecialKind.None) continue;
                    options.Add((from, to));
                }
            }
        }

        // Prueba candidatas al azar hasta encontrar una válida; si no, la primera que exista.
        for (int attempt = 0; attempt < options.Count; attempt++)
        {
            var pick = options[bot.Random.Next(options.Count)];
            if (board.TrySwap(pick.Item1, pick.Item2).IsValid)
            {
                bot.MovesLeft--;
                return;
            }
        }

        if (board.TryFindMove(out GridPos a, out GridPos b)) board.TrySwap(a, b);
        bot.MovesLeft--;
    }

    private void LoadProfile()
    {
        if (profile != null) return;
        string json = PlayerPrefs.GetString(ProfileKey, "");
        profile = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RankedProfile>(json);
        if (profile == null)
        {
            profile = new RankedProfile
            {
                PlayerId = "test-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ShortId = 100001,
                Name = "Minero" + UnityEngine.Random.Range(1000, 9999),
                Points = RankedRules.StartingPoints
            };
            SaveProfile();
        }

        // Temporadas (igual que el servidor): reinicio suave al cambiar.
        int current = SeasonRules.CurrentSeason(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (profile.Badges == null) profile.Badges = new List<string>();
        if (profile.Season == 0)
        {
            profile.Season = current;
            profile.SeasonPeakPoints = profile.Points;
            SaveProfile();
        }
        else if (profile.Season < current)
        {
            if (profile.Wins + profile.Losses + profile.Draws > 0) pendingRewards.Add(SeasonRules.SeasonEndReward(profile.Points, profile.Season));
            for (int s = profile.Season; s < current; s++) profile.Points = SeasonRules.SoftReset(profile.Points);
            profile.Season = current;
            profile.SeasonPeakPoints = profile.Points;
            SaveProfile();
        }
    }

    private void SaveProfile()
    {
        PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    private static HistoryList LoadHistory()
    {
        string json = PlayerPrefs.GetString(HistoryKey, "");
        HistoryList list = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<HistoryList>(json);
        return list ?? new HistoryList();
    }

    private static void AddHistory(MatchHistoryEntry entry)
    {
        HistoryList list = LoadHistory();
        list.items.Insert(0, entry);
        if (list.items.Count > 30) list.items.RemoveRange(30, list.items.Count - 30);
        PlayerPrefs.SetString(HistoryKey, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }
}
