using System.Collections.Generic;
using System.Text;
using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using UnityEngine;
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

    public int CurrentMoves => Session != null ? Session.MovesLeft : 0;
    public bool IsOutOfMoves => CurrentMoves <= 0;

    /// <summary>True si se puede hacer una jugada ahora mismo.</summary>
    public bool CanPlay => Session != null && Session.CanPlay && !StoryPopup.IsOpen;

    private void Awake()
    {
        Instance = this;

        LevelDefinition level = ChooseLevel();
        Session = new LevelSession(level);
        ConfigureBoard(level);
    }

    private void Start()
    {
        RefreshUI();

        if (showLevelIntro && LevelCatalog.Count > 0)
        {
            ShowIntro();
        }
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

        if (GameUI.Instance != null)
        {
            GameUI.Instance.UpdateMoves(Session.MovesLeft);
        }
    }

    /// <summary>Cuando termina de animarse la jugada (y sus cascadas).</summary>
    public void OnMoveFinished()
    {
        RefreshUI();

        if (Session.State == SessionState.Won)
        {
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
        PlayerProgress progress = SaveManager.Progress;
        var cells = new List<StoryPopup.PopupButton>();

        for (int number = 1; number <= LevelCatalog.Count; number++)
        {
            int n = number;
            bool unlocked = progress.IsUnlocked(n);
            string label = unlocked
                ? $"{n}\n<size=70%>{progress.GetStars(n)}/3</size>"
                : $"{n}\n<size=70%>cerrado</size>";
            cells.Add(new StoryPopup.PopupButton(label, () => PlayLevel(n), unlocked));
        }

        StoryPopup.ShowGrid(
            "Modo historia",
            $"Estrellas: {progress.TotalStars}/{LevelCatalog.Count * 3}",
            cells,
            5,
            new StoryPopup.PopupButton("Cerrar", null));
    }

    // ------------------------------------------------------------------

    private LevelDefinition ChooseLevel()
    {
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
        BoardManager board = FindFirstObjectByType<BoardManager>();
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
            new StoryPopup.PopupButton("Niveles", OpenLevelSelect));
    }

    private void OnWin()
    {
        int levelNumber = Session.Level.Number;
        if (levelNumber > 0)
        {
            SaveManager.Progress.RecordWin(levelNumber, Session.Stars, LevelCatalog.Count);
            SaveManager.Save();
        }

        if (Session.MoveBonus > 0 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(Session.MoveBonus);
        }

        bool isLast = levelNumber >= LevelCatalog.Count;
        StoryPopup.Show(
            "¡Nivel completado!",
            $"Estrellas: <color=#FFD040><b>{Session.Stars}/3</b></color>\n\n" +
            $"Puntos: {Session.Score}\n" +
            $"Bonus por {Session.MovesLeft} movimientos: +{Session.MoveBonus}\n" +
            $"<b>Total: {Session.FinalScore}</b>",
            new StoryPopup.PopupButton(isLast ? "Ver niveles" : "Siguiente nivel", NextLevel),
            new StoryPopup.PopupButton("Repetir", RetryLevel));
    }

    private void OnLose()
    {
        StoryPopup.Show(
            "Sin movimientos",
            $"¡Casi!\n\n{DescribeObjectives(Session, true)}",
            new StoryPopup.PopupButton("Reintentar", RetryLevel),
            new StoryPopup.PopupButton("Niveles", OpenLevelSelect));
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
