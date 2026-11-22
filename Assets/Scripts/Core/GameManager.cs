using System.Collections.Generic;
using System.Text;
using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla una partida del modo historia: carga el nivel, configura el
/// tablero, cuenta movimientos y objetivos, y muestra victoria/derrota.
/// Las reglas están en LevelSession (C# puro); aquí solo se conectan con Unity.
/// Se ejecuta antes que BoardManager para configurar el tablero a tiempo.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Mineral selectedMineral;

    [Header("Partida")]
    [Tooltip("Movimientos si no hay niveles cargados (partida libre).")]
    public int maxMoves = 25;

    [Tooltip("Mostrar un popup con los objetivos al empezar cada nivel.")]
    public bool showLevelIntro = true;

    public LevelSession Session { get; private set; }

    /// <summary>Potenciador elegido que espera a que el jugador toque un mineral (null = ninguno).</summary>
    public BoosterKind? PendingBooster { get; private set; }

    private BoardManager board;
    private RankedMatchController ranked;
    private bool finalePlayed;

    /// <summary>True si esta partida es de clasificatoria (no de historia).</summary>
    public bool IsRanked => ranked != null;

    public int CurrentMoves => Session != null ? Session.MovesLeft : 0;
    public bool IsOutOfMoves => CurrentMoves <= 0;

    /// <summary>True si se puede hacer una jugada ahora mismo.</summary>
    public bool CanPlay => Session != null && Session.CanPlay && !StoryPopup.IsOpen && !MenuScreens.IsOpen &&
                           (ranked == null || ranked.AcceptingMoves);

    private void Awake()
    {
        Instance = this;

        // En celular: 60 FPS (Android viene a 30) y que la pantalla no se apague jugando.
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        LevelDefinition level = ChooseLevel();
        Session = new LevelSession(level);
        ConfigureBoard(level);
    }

    private void Start()
    {
        CloudSync.StartSync();

        if (GameUI.Instance != null) GameUI.Instance.HideLegacyPanel();

        // La barra superior de la escena era de muestra (números fijos): la reemplazan los menús.
        GameObject sceneTopBar = GameObject.Find("TopBar");
        if (sceneTopBar != null) sceneTopBar.SetActive(false);

        if (RankedMatchState.Active)
        {
            ranked = gameObject.AddComponent<RankedMatchController>();
            ranked.Begin(Session, board, RankedMatchState.Current);
            RefreshUI();
            return;
        }

        GameHud.Create(Session);
        RefreshUI();

        AppNavigation.Screen screen = AppNavigation.PendingScreen;
        AppNavigation.PendingScreen = AppNavigation.Screen.None;

        if (screen == AppNavigation.Screen.Home) MenuScreens.ShowHome();
        else if (screen == AppNavigation.Screen.Ranked) MenuScreens.ShowRanked();
        else if (screen == AppNavigation.Screen.Map) MenuScreens.ShowMap();
        else if (showLevelIntro) StartStory();
    }

    /// <summary>Muestra el inicio del nivel de historia actual.</summary>
    public void StartStory()
    {
        if (LevelCatalog.Count == 0) return;

        PlayerProgress progress = SaveManager.Progress;
        long now = GameClock.NowUtc;
        if (progress.GetLives(now) <= 0)
        {
            int seconds = progress.SecondsToNextLife(now);
            StoryPopup.Show("Sin vidas",
                $"Próxima vida en <b>{seconds / 60}:{seconds % 60:00}</b>\n\nMira un anuncio para ganar una vida al instante.",
                new StoryPopup.PopupButton(LifeAds.ButtonLabel, () => LifeAds.WatchForLife(StartStory), LifeAds.Remaining > 0),
                new StoryPopup.PopupButton("Mapa", MenuScreens.ShowMap));
            return;
        }

        ShowIntro();
    }

    /// <summary>Menú de pausa (botón "Menú" del HUD).</summary>
    public void ShowPause()
    {
        if (IsRanked || StoryPopup.IsOpen || MenuScreens.IsOpen) return;
        StoryPopup.Show("Pausa", $"Nivel {Session.Level.Number} · {Session.Level.Name}",
            new StoryPopup.PopupButton("Continuar", null),
            new StoryPopup.PopupButton("Reiniciar nivel", RetryLevel),
            new StoryPopup.PopupButton("Mapa", MenuScreens.ShowMap),
            new StoryPopup.PopupButton("Inicio", MenuScreens.ShowHome));
    }

    private void Update()
    {
        // Botón "atrás" de Android (o Escape en la PC).
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) OnBack();
    }

    private void OnBack()
    {
        if (StoryPopup.IsOpen) return; // los popups se cierran con sus botones

        if (MenuScreens.IsOpen)
        {
            if (MenuScreens.CurrentScreen != MenuScreens.Screen.Home) MenuScreens.ShowHome();
            return;
        }

        if (!IsRanked) ShowPause();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        StoryPopup.Close();
    }

    // ------------------------------------------------------------------
    // Llamado por BoardManager
    // ------------------------------------------------------------------

    /// <summary>Justo después de una jugada válida (antes de animarla).</summary>
    public void RegisterMove(SwapResult result)
    {
        Session.ApplyMove(result);
        if (ranked != null) ranked.RecordMove(result);
        CountSpecials(result);

        if (GameUI.Instance != null)
        {
            GameUI.Instance.UpdateMoves(Session.MovesLeft);
        }

        if (GameHud.Instance != null)
        {
            GameHud.Instance.SetMoves(Session.MovesLeft);
        }
    }

    // ------------------------------------------------------------------
    // Potenciadores
    // ------------------------------------------------------------------

    public static string BoosterName(BoosterKind kind)
    {
        switch (kind)
        {
            case BoosterKind.Hammer: return "Martillo";
            case BoosterKind.Bomb: return "Bomba";
            case BoosterKind.Lightning: return "Rayo";
            default: return "Mezclar";
        }
    }

    /// <summary>
    /// Botón de un potenciador. Mezclar se usa al instante; los demás quedan
    /// "armados" hasta que el jugador toque un mineral. Tocar de nuevo cancela.
    /// </summary>
    public void OnBoosterButton(BoosterKind kind)
    {
        if (!CanPlay || board == null || board.IsBusy || IsRanked) return;
        if (SaveManager.Progress.GetBoosters(kind) <= 0) return;

        if (kind == BoosterKind.Shuffle)
        {
            SetPendingBooster(null);
            board.PlayBooster(kind, default(GridPos));
            return;
        }

        SetPendingBooster(PendingBooster == kind ? (BoosterKind?)null : kind);
    }

    /// <summary>Lo llama BoardManager al tocar un mineral. True si había un potenciador armado y se usó.</summary>
    public bool TryConsumePendingBooster(GridPos target)
    {
        if (PendingBooster == null || board == null) return false;

        BoosterKind kind = PendingBooster.Value;
        SetPendingBooster(null);
        board.PlayBooster(kind, target);
        return true;
    }

    /// <summary>Justo después de usar un potenciador válido (antes de animarlo).</summary>
    public void RegisterBooster(BoosterKind kind, SwapResult result)
    {
        SaveManager.Progress.TryUseBooster(kind);
        SaveManager.Save();
        Session.ApplyBooster(result);
        CountSpecials(result);

        if (GameHud.Instance != null) GameHud.Instance.RefreshBoosters();
    }

    /// <summary>Suma los especiales creados (logro "Maestro de Combos"). Se guarda al terminar la partida.</summary>
    private static void CountSpecials(SwapResult result)
    {
        if (result == null || !result.IsValid) return;
        foreach (CascadeStep step in result.Steps) SaveManager.Progress.specialsCreated += step.Created.Count;
    }

    private void SetPendingBooster(BoosterKind? kind)
    {
        PendingBooster = kind;
        if (GameHud.Instance != null) GameHud.Instance.SetPendingBooster(kind);
    }

    /// <summary>Cuando termina de animarse la jugada (y sus cascadas).</summary>
    public void OnMoveFinished()
    {
        RefreshUI();

        if (ranked != null)
        {
            ranked.OnMoveFinished();
            return;
        }

        if (Session.State != SessionState.Playing) SetPendingBooster(null);

        if (Session.State == SessionState.Won)
        {
            // Movimientos sobrantes → bombas de bonus antes del resultado.
            if (!finalePlayed && Session.MovesLeft > 0 && board != null && board.PlayFinale(Session.MovesLeft))
            {
                finalePlayed = true;
                if (GameHud.Instance != null) GameHud.Instance.ShowBanner($"¡{Session.MovesLeft} bombas de bonus!", 2.2f);
                return;
            }
            OnWin();
        }
        else if (Session.State == SessionState.Lost)
        {
            OnLose();
        }
    }

    // ------------------------------------------------------------------
    // Navegación (se pueden conectar a botones de la UI)
    // ------------------------------------------------------------------

    public void PlayLevel(int number)
    {
        LevelSelection.CurrentLevel = number;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void RetryLevel()
    {
        PlayLevel(Session.Level.Number);
    }

    public void NextLevel()
    {
        int next = Session.Level.Number + 1;
        if (next <= LevelCatalog.Count) PlayLevel(next);
        else OpenLevelSelect();
    }

    public void OpenLevelSelect()
    {
        MenuScreens.ShowMap();
    }

    // ------------------------------------------------------------------

    private LevelDefinition ChooseLevel()
    {
        if (RankedMatchState.Active)
        {
            return RankedMatchController.CreateLevel(RankedMatchState.Current);
        }

        int count = LevelCatalog.Count;
        if (count == 0)
        {
            var free = new LevelDefinition
            {
                Number = 0,
                Name = "Partida libre",
                MaxMoves = Mathf.Max(1, maxMoves),
                StarScores = new[] { 1000, 2000, 3000 }
            };
            free.Objectives.Add(LevelObjective.Score(1000));
            return free;
        }

        int number = LevelSelection.CurrentLevel;
        if (number < 1 || number > count || !SaveManager.Progress.IsUnlocked(number))
        {
            number = Mathf.Clamp(SaveManager.Progress.highestUnlocked, 1, count);
        }

        LevelSelection.CurrentLevel = number;
        return LevelCatalog.Get(number);
    }

    private void ConfigureBoard(LevelDefinition level)
    {
        board = FindAnyObjectByType<BoardManager>();
        if (board == null)
        {
            Debug.LogError("GameManager: no hay BoardManager en la escena.");
            return;
        }

        board.width = level.Width;
        board.height = level.Height;
        board.mineralTypeCount = level.MineralTypeCount;
        board.seed = level.Seed;
    }

    private void RefreshUI()
    {
        if (GameHud.Instance != null)
        {
            GameHud.Instance.SetMoves(Session.MovesLeft);
            GameHud.Instance.SetObjectives(Session);
        }

        if (GameUI.Instance == null) return;

        GameUI.Instance.UpdateMoves(Session.MovesLeft);
        GameUI.Instance.UpdateObjectives(Session);
    }

    private void ShowIntro()
    {
        LevelDefinition level = Session.Level;
        StoryPopup.Show(
            $"Nivel {level.Number}",
            $"<b>{level.Name}</b>\n\n{DescribeObjectives(Session, false)}\n\n{level.MaxMoves} movimientos",
            new StoryPopup.PopupButton("¡Jugar!", null),
            new StoryPopup.PopupButton("Mapa", MenuScreens.ShowMap),
            new StoryPopup.PopupButton("Inicio", MenuScreens.ShowHome));
    }

    /// <summary>Lo llama BoardManager cuando terminan de explotar las bombas de bonus.</summary>
    public void OnFinaleFinished(SwapResult finale)
    {
        Session.ApplyFinale(finale);
        RefreshUI();
        OnWin();
    }

    // Premios del último nivel ganado (para poder duplicarlos con un anuncio).
    private int winCoins;
    private int winGems;
    private int winXp;
    private string winExtra = "";

    private void OnWin()
    {
        Sound.Play("win");
        int levelNumber = Session.Level.Number;
        winCoins = winGems = winXp = 0;
        winExtra = "";
        if (levelNumber > 0)
        {
            bool firstWin = SaveManager.Progress.GetStars(levelNumber) == 0;
            SaveManager.Progress.RecordWin(levelNumber, Session.Stars, LevelCatalog.Count);

            winCoins = PlayerProgress.CoinsForWin(Session.Stars);
            winGems = firstWin ? PlayerProgress.GemsPerLevelWin : 0; // gemas solo la primera vez que se pasa
            winXp = PlayerProgress.XpStoryWin + PlayerProgress.XpPerStar * Session.Stars;
            SaveManager.Progress.coins += winCoins;
            SaveManager.Progress.gems += winGems;
            SaveManager.Progress.AddXp(winXp);

            if (firstWin)
            {
                BoosterKind prize = PlayerProgress.FirstWinReward(levelNumber);
                SaveManager.Progress.AddBooster(prize, 1);
                winExtra = $"\n<color=#66FF77>¡Premio: +1 {BoosterName(prize)}!</color>";
            }

            SaveManager.Save();
        }

        ShowWinPopup(false);
    }

    private void ShowWinPopup(bool doubled)
    {
        int levelNumber = Session.Level.Number;
        string reward = "";
        if (levelNumber > 0)
        {
            int k = doubled ? 2 : 1;
            string gems = winGems > 0 ? $"  <color=#C080FF>+{winGems * k} gemas</color>" : "";
            reward = $"\n\n<color=#FFD040>+{winCoins * k} monedas</color>{gems}  <color=#80C8FF>+{winXp * k} XP</color>" +
                     (doubled ? "  <color=#66FF77><b>x2</b></color>" : "") + winExtra;
        }

        bool isLast = levelNumber >= LevelCatalog.Count;
        string body = $"Estrellas: <color=#FFD040><b>{Session.Stars}/3</b></color>\n\n" +
                      $"Puntos: {Session.Score}\n" +
                      (Session.FinaleBombs > 0 ? $"Bombas de bonus ({Session.FinaleBombs}): +{Session.MoveBonus}\n" : "") +
                      $"<b>Total: {Session.FinalScore}</b>" + reward;

        var next = new StoryPopup.PopupButton(isLast ? "Ver mapa" : "Siguiente nivel", NextLevel);
        var retry = new StoryPopup.PopupButton("Repetir", RetryLevel);
        var map = new StoryPopup.PopupButton("Mapa", MenuScreens.ShowMap);

        if (levelNumber > 0 && !doubled)
        {
            // Opcional: ver un anuncio para duplicar el premio. Si no, no pasa nada.
            StoryPopup.Show("¡Nivel completado!", body,
                next,
                StoryPopup.PopupButton.Styled("x2 Premio  (ver anuncio)", () =>
                    AdReward.Show(DoubleWinRewards, () => ShowWinPopup(false), "Mira el anuncio completo para duplicar el premio."),
                    UiKit.ButtonStyle.Purple),
                retry, map);
        }
        else
        {
            StoryPopup.Show(doubled ? "¡Premio duplicado!" : "¡Nivel completado!", body, next, retry, map);
        }
    }

    /// <summary>El anuncio se vio completo: se suman otra vez monedas, gemas y experiencia.</summary>
    private void DoubleWinRewards()
    {
        PlayerProgress p = SaveManager.Progress;
        p.coins += winCoins;
        p.gems += winGems;
        p.AddXp(winXp);
        SaveManager.Save();
        Sound.Play("coin");
        ShowWinPopup(true);
    }

    private void OnLose()
    {
        Sound.Play("lose");
        PlayerProgress progress = SaveManager.Progress;
        long now = GameClock.NowUtc;
        progress.LoseLife(now);
        progress.AddXp(PlayerProgress.XpStoryLoss);
        SaveManager.Save();
        int lives = progress.GetLives(now);

        string livesText = lives > 0
            ? $"Te quedan <color=#FF7070>{lives}</color> vidas"
            : "<color=#FF7070>¡Te quedaste sin vidas!</color>";

        string body = $"¡Casi!\n\n{DescribeObjectives(Session, true)}\n\n{livesText}";
        if (lives > 0)
        {
            StoryPopup.Show("Sin movimientos", body,
                new StoryPopup.PopupButton("Reintentar", RetryLevel),
                new StoryPopup.PopupButton("Mapa", MenuScreens.ShowMap));
        }
        else
        {
            StoryPopup.Show("Sin movimientos", body + "\nMira un anuncio para ganar una vida.",
                new StoryPopup.PopupButton(LifeAds.ButtonLabel, () => LifeAds.WatchForLife(RetryLevel), LifeAds.Remaining > 0),
                new StoryPopup.PopupButton("Mapa", MenuScreens.ShowMap));
        }
    }

    /// <summary>Texto con los objetivos, p. ej. "Zafiro 12/15". Lo usan los popups y GameUI.</summary>
    public static string DescribeObjectives(LevelSession session, bool showProgress)
    {
        var sb = new StringBuilder();
        foreach (ObjectiveProgress progress in session.Objectives)
        {
            if (sb.Length > 0) sb.Append('\n');

            LevelObjective objective = progress.Objective;
            string name = objective.Kind == ObjectiveKind.ReachScore
                ? "Puntos"
                : MineralNames.Get(objective.MineralType);

            if (!showProgress)
            {
                sb.Append(objective.Kind == ObjectiveKind.ReachScore
                    ? $"Consigue {objective.Target} puntos"
                    : $"Rompe {objective.Target} de {name}");
                continue;
            }

            string line = $"{name} {progress.Current}/{objective.Target}";
            sb.Append(progress.IsComplete ? $"<color=#66FF77>{line}</color>" : line);
        }
        return sb.ToString();
    }
}
