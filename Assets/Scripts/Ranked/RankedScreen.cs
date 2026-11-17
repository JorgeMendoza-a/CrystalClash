using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrystalClash.Ranked.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de Clasificatoria (según el diseño): rango actual, puntos y barra
/// hacia el siguiente rango, tabla de rangos con recompensas, temporada,
/// botón BUSCAR PARTIDA, tabla de posiciones e historial.
/// </summary>
public class RankedScreen : MonoBehaviour
{
    private IRankedService service;

    private TMP_Text status;
    private Image currentEmblem;
    private TMP_Text currentName;
    private TMP_Text pointsText;
    private RectTransform progressFill;
    private TMP_Text progressText;
    private TMP_Text toNextText;
    private Image nextEmblem;
    private TMP_Text nextName;
    private TMP_Text recordText;
    private Button searchButton;

    private CancellationTokenSource searchCancel;
    private bool searching;

    private void Awake()
    {
        service = RankedServices.Current;
        Build();
    }

    private async void Start()
    {
        await RefreshProfile();
    }

    private void OnDestroy()
    {
        if (searchCancel != null) searchCancel.Cancel();
    }

    // ------------------------------------------------------------------
    // Datos
    // ------------------------------------------------------------------

    private async Task RefreshProfile()
    {
        status.text = "Conectando...";
        try
        {
            await service.SignInAsync();
            RankedProfile profile = await service.GetProfileAsync();
            if (this == null) return;
            ShowProfile(profile);
            status.text = service.IsTestMode
                ? "<color=#FFB040>MODO PRUEBA: rival simulado, sin servidor</color>"
                : $"Conectado como <b>{profile.Name}</b>";
        }
        catch (Exception e)
        {
            if (this == null) return;
            status.text = "<color=#FF7070>No se pudo conectar. Revisa tu internet.</color>";
            Debug.LogWarning("Clasificatoria: " + e.Message);
        }
    }

    private void ShowProfile(RankedProfile profile)
    {
        RankTier tier = profile.Tier;
        RankTier next = RankTiers.Next(tier);

        currentEmblem.sprite = UiKit.RankEmblem(tier.LeagueIndex);
        currentName.text = tier.Name.ToUpperInvariant();
        currentName.color = UiKit.LeagueColor(tier.LeagueIndex);
        pointsText.text = profile.Points.ToString();
        recordText.text = $"Victorias {profile.Wins}  ·  Derrotas {profile.Losses}  ·  Empates {profile.Draws}";

        if (next == null)
        {
            progressFill.anchorMax = new Vector2(1f, 1f);
            progressText.text = profile.Points.ToString();
            toNextText.text = "¡Rango máximo!";
            nextEmblem.sprite = UiKit.RankEmblem(tier.LeagueIndex);
            nextName.text = "—";
            return;
        }

        float fraction = Mathf.InverseLerp(tier.MinPoints, next.MinPoints, profile.Points);
        progressFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        progressText.text = $"{profile.Points} / {next.MinPoints}";
        toNextText.text = $"Faltan {next.MinPoints - profile.Points} puntos para subir a " +
                          $"<color=#{ColorUtility.ToHtmlStringRGB(UiKit.LeagueColor(next.LeagueIndex))}>{next.Name}</color>";
        nextEmblem.sprite = UiKit.RankEmblem(next.LeagueIndex);
        nextName.text = next.Name.ToUpperInvariant();
        nextName.color = UiKit.LeagueColor(next.LeagueIndex);
    }

    // ------------------------------------------------------------------
    // Buscar partida
    // ------------------------------------------------------------------

    private async void OnSearch()
    {
        if (searching) return;

        if (!service.IsTestMode && Application.internetReachability == NetworkReachability.NotReachable)
        {
            StoryPopup.Show("Sin internet", "La clasificatoria necesita conexión.\nEl modo historia sí funciona sin internet.",
                new StoryPopup.PopupButton("Entendido", null));
            return;
        }

        searching = true;
        searchCancel = new CancellationTokenSource();
        StoryPopup.Show("Buscando rival...", "Buscando un minero con puntos parecidos a los tuyos.",
            new StoryPopup.PopupButton("Cancelar", () => searchCancel.Cancel()));

        try
        {
            await service.SignInAsync();
            RankedMatchInfo match = await service.FindMatchAsync(searchCancel.Token);
            if (this == null) return;

            RankTier tier = RankTiers.ForPoints(match.OpponentPoints);
            StoryPopup.Show("¡Rival encontrado!", $"<b>{match.OpponentName}</b>\n{tier.Name} · {match.OpponentPoints} puntos");
            await Task.Delay(1500);

            StoryPopup.Close();
            RankedMatchState.Current = match;
            AppNavigation.PendingScreen = AppNavigation.Screen.None;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        catch (OperationCanceledException)
        {
            // El jugador canceló.
        }
        catch (Exception e)
        {
            Debug.LogWarning("Buscar partida: " + e.Message);
            StoryPopup.Show("No se pudo buscar partida", "Revisa tu conexión e inténtalo otra vez.",
                new StoryPopup.PopupButton("Cerrar", null));
        }
        finally
        {
            searching = false;
        }
    }

    private async void OnLeaderboard()
    {
        try
        {
            List<LeaderboardEntry> entries = await service.GetLeaderboardAsync(20);
            var sb = new StringBuilder();
            foreach (LeaderboardEntry e in entries)
            {
                string line = $"{e.Rank}. {e.Name} — {e.Points} ({RankTiers.ForPoints(e.Points).Name})";
                sb.AppendLine(e.IsMe ? $"<color=#FFD040><b>{line}</b></color>" : line);
            }
            StoryPopup.Show("Tabla de posiciones", sb.Length > 0 ? sb.ToString() : "Todavía no hay jugadores.",
                new StoryPopup.PopupButton("Cerrar", null));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Tabla de posiciones: " + e.Message);
            StoryPopup.Show("Tabla de posiciones", "No se pudo cargar.", new StoryPopup.PopupButton("Cerrar", null));
        }
    }

    private async void OnHistory()
    {
        try
        {
            List<MatchHistoryEntry> history = await service.GetHistoryAsync();
            var sb = new StringBuilder();
            int shown = 0;
            foreach (MatchHistoryEntry h in history)
            {
                if (shown++ >= 10) break;
                var outcome = (MatchOutcome)h.Outcome;
                string label = outcome == MatchOutcome.Win ? "<color=#66FF77>Victoria</color>"
                    : outcome == MatchOutcome.Loss ? "<color=#FF7070>Derrota</color>" : "Empate";
                string change = h.PointsChange >= 0 ? $"+{h.PointsChange}" : h.PointsChange.ToString();
                sb.AppendLine($"{label} vs {h.OpponentName}: {h.MyScore} - {h.OpponentScore} ({change})");
            }
            StoryPopup.Show("Historial de partidas", sb.Length > 0 ? sb.ToString() : "Aún no has jugado partidas.",
                new StoryPopup.PopupButton("Cerrar", null));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Historial: " + e.Message);
            StoryPopup.Show("Historial de partidas", "No se pudo cargar.", new StoryPopup.PopupButton("Cerrar", null));
        }
    }

    // ------------------------------------------------------------------
    // Construcción
    // ------------------------------------------------------------------

    private void Build()
    {
        Image background = UiKit.Image(transform, "Fondo", new Color(0.03f, 0.04f, 0.08f, 0.94f));
        UiKit.Stretch(background.rectTransform);

        GameObject column = UiKit.Node(transform, "Columna");
        UiKit.Stretch((RectTransform)column.transform, 36f);
        UiKit.Vertical(column, 0, 14f);

        UiKit.Size(UiKit.Text(column.transform, "CLASIFICATORIA", 88f, UiKit.Gold, FontStyles.Bold), height: 110f);
        UiKit.Size(UiKit.Text(column.transform, "Compite, sube de rango y demuestra que eres el mejor minero.", 34f, Color.white), height: 46f);
        status = UiKit.Text(column.transform, "", 30f, UiKit.Muted);
        UiKit.Size(status, height: 44f);

        BuildRankCard(column.transform);

        searchButton = UiKit.Button(column.transform, "BUSCAR PARTIDA", UiKit.Green, OnSearch, 60f);
        UiKit.Size(searchButton, height: 150f);
        recordText = UiKit.Text(column.transform, "", 30f, UiKit.Muted);
        UiKit.Size(recordText, height: 40f);

        UiKit.Size(UiKit.Text(column.transform, SeasonText(), 34f, UiKit.Gold), height: 48f);

        BuildTiersTable(column.transform);

        GameObject buttons = UiKit.Node(column.transform, "Botones");
        UiKit.Horizontal(buttons, 0, 16f).childForceExpandWidth = true;
        UiKit.Size(buttons.transform, height: 120f);
        UiKit.Button(buttons.transform, "Posiciones", UiKit.Blue, OnLeaderboard, 38f);
        UiKit.Button(buttons.transform, "Historial", UiKit.Blue, OnHistory, 38f);
        UiKit.Button(buttons.transform, "Volver", new Color(0.3f, 0.3f, 0.36f), MenuScreens.ShowHome, 38f);
    }

    private void BuildRankCard(Transform parent)
    {
        Image card = UiKit.Card(parent, "Rango");
        UiKit.Size(card, height: 380f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 34, 10f);
        row.childForceExpandWidth = true;

        GameObject left = UiKit.Node(card.transform, "Actual");
        UiKit.Vertical(left, 0, 4f, TextAnchor.MiddleCenter);
        UiKit.Size(UiKit.Text(left.transform, "TU RANGO", 30f, UiKit.LeagueColor(4), FontStyles.Bold), height: 40f);
        currentEmblem = UiKit.Image(left.transform, "Emblema", Color.white, UiKit.RankEmblem(0));
        UiKit.Size(currentEmblem, height: 190f);
        currentName = UiKit.Text(left.transform, "...", 40f, Color.white, FontStyles.Bold);
        UiKit.Size(currentName, height: 54f);

        GameObject middle = UiKit.Node(card.transform, "Puntos");
        UiKit.Vertical(middle, 0, 8f, TextAnchor.MiddleCenter);
        UiKit.Size(middle.transform, flexibleWidth: 1.3f);
        UiKit.Size(UiKit.Text(middle.transform, "PUNTOS", 32f, Color.white, FontStyles.Bold), height: 42f);
        pointsText = UiKit.Text(middle.transform, "-", 84f, UiKit.Gold, FontStyles.Bold);
        UiKit.Size(pointsText, height: 100f);

        Image bar = UiKit.Image(middle.transform, "Barra", new Color(0.05f, 0.07f, 0.12f));
        UiKit.Size(bar, height: 50f);
        Image fill = UiKit.Image(bar.transform, "Relleno", new Color(0.15f, 0.5f, 0.95f));
        progressFill = fill.rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        progressText = UiKit.Text(bar.transform, "", 30f, Color.white, FontStyles.Bold);
        UiKit.Stretch(progressText.rectTransform);

        toNextText = UiKit.Text(middle.transform, "", 30f, Color.white);
        UiKit.Size(toNextText, height: 80f);

        GameObject right = UiKit.Node(card.transform, "Siguiente");
        UiKit.Vertical(right, 0, 4f, TextAnchor.MiddleCenter);
        UiKit.Size(UiKit.Text(right.transform, "SIGUIENTE", 30f, UiKit.LeagueColor(4), FontStyles.Bold), height: 40f);
        nextEmblem = UiKit.Image(right.transform, "Emblema", Color.white, UiKit.RankEmblem(1));
        UiKit.Size(nextEmblem, height: 190f);
        nextName = UiKit.Text(right.transform, "...", 40f, Color.white, FontStyles.Bold);
        UiKit.Size(nextName, height: 54f);
    }

    private void BuildTiersTable(Transform parent)
    {
        Image card = UiKit.Card(parent, "Rangos");
        const float rowHeight = 78f;
        UiKit.Size(card, height: 70f + RankTiers.Leagues.Count * (rowHeight + 4f) + 60f);
        UiKit.Vertical(card.gameObject, 30, 4f);

        TableRow(card.transform, null, "RANGO", "PUNTOS", "RECOMPENSA DE TEMPORADA", Color.white, 56f, true);

        for (int i = RankTiers.Leagues.Count - 1; i >= 0; i--)
        {
            RankTiers.League league = RankTiers.Leagues[i];
            string range = league.MaxPoints == int.MaxValue ? $"{league.MinPoints}+" : $"{league.MinPoints} - {league.MaxPoints}";
            TableRow(card.transform, UiKit.RankEmblem(i), league.Name.ToUpperInvariant(), range,
                $"{league.SeasonGems} gemas · {league.SeasonCoins} monedas", UiKit.LeagueColor(i), rowHeight, false);
        }
    }

    private static void TableRow(Transform parent, Sprite emblem, string name, string range, string reward,
        Color nameColor, float height, bool header)
    {
        GameObject row = UiKit.Node(parent, "Fila " + name);
        UiKit.Horizontal(row, 0, 12f);
        UiKit.Size(row.transform, height: height);

        Image icon = UiKit.Image(row.transform, "Icono", Color.white, emblem);
        icon.enabled = emblem != null;
        UiKit.Size(icon, width: 70f);

        float size = header ? 26f : 34f;
        Color other = header ? UiKit.Muted : Color.white;
        UiKit.Size(UiKit.Text(row.transform, name, size, header ? UiKit.Muted : nameColor, FontStyles.Bold, TextAlignmentOptions.Left), width: 250f);
        UiKit.Size(UiKit.Text(row.transform, range, size, other, FontStyles.Normal), width: 220f);
        UiKit.Size(UiKit.Text(row.transform, reward, header ? 26f : 30f, other, FontStyles.Normal), flexibleWidth: 1f);
    }

    /// <summary>Las temporadas duran un mes (terminan el último día del mes, en UTC).</summary>
    private static string SeasonText()
    {
        DateTime now = DateTime.UtcNow;
        var end = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        TimeSpan left = end - now;
        int season = (now.Year - 2026) * 12 + now.Month;
        return $"Temporada {Mathf.Max(1, season)}  ·  termina en {left.Days}d {left.Hours}h";
    }
}
