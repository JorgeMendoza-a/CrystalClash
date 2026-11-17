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
    private TMP_Text perMatchText;
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
        RankedRules.TypicalChange(profile.Points, out int win, out int loss);
        perMatchText.text = $"En {tier.League}: ganar <color=#66FF77>+{win}</color>  ·  perder <color=#FF7070>{loss}</color> (aprox.)";

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
        UiKit.FullscreenBackground(transform, 0.62f);

        GameObject column = UiKit.Node(transform, "Columna");
        UiKit.Stretch((RectTransform)column.transform, 30f);
        UiKit.Vertical(column, 0, 14f);

        // Encabezado: emblema + título dorado + emblema
        GameObject header = UiKit.Node(column.transform, "Encabezado");
        HorizontalLayoutGroup headerRow = UiKit.Horizontal(header, 0, 10f);
        headerRow.childForceExpandWidth = false;
        UiKit.Size(header.transform, height: 130f);
        UiKit.Size(UiKit.Image(header.transform, "Emblema", Color.white, UiKit.RankEmblem(4)), width: 130f);
        UiKit.Size(UiKit.Title(header.transform, "CLASIFICATORIA", 100f), flexibleWidth: 1f);
        UiKit.Size(UiKit.Image(header.transform, "Emblema", Color.white, UiKit.RankEmblem(6)), width: 130f);

        TMP_Text subtitle = UiKit.Text(column.transform, "Compite, sube de rango y demuestra que eres el mejor minero.", 34f, Color.white);
        UiKit.Outline(subtitle, 0.15f);
        UiKit.Size(subtitle, height: 46f);

        BuildRankCard(column.transform);

        searchButton = UiKit.Button(column.transform, "BUSCAR PARTIDA", UiKit.ButtonStyle.Green, OnSearch, 72f);
        UiKit.Size(searchButton, height: 170f);

        Image info = UiKit.Card(column.transform, "Info");
        UiKit.Size(info, height: 150f);
        UiKit.Vertical(info.gameObject, 24, 0f, TextAnchor.MiddleCenter);
        TMP_Text season = UiKit.Text(info.transform, SeasonText(), 34f, UiKit.Gold);
        UiKit.Size(season, height: 38f);
        perMatchText = UiKit.Text(info.transform, "", 30f, Color.white);
        UiKit.Size(perMatchText, height: 34f);
        recordText = UiKit.Text(info.transform, "", 28f, UiKit.Muted);
        UiKit.Size(recordText, height: 32f);

        BuildTiersTable(column.transform);

        GameObject buttons = UiKit.Node(column.transform, "Botones");
        UiKit.Horizontal(buttons, 0, 16f).childForceExpandWidth = true;
        UiKit.Size(buttons.transform, height: 120f);
        UiKit.Button(buttons.transform, "Posiciones", UiKit.ButtonStyle.Blue, OnLeaderboard, 42f);
        UiKit.Button(buttons.transform, "Historial", UiKit.ButtonStyle.Blue, OnHistory, 42f);
        UiKit.Button(buttons.transform, "Volver", UiKit.ButtonStyle.Gray, MenuScreens.ShowHome, 42f);

        status = UiKit.Text(column.transform, "", 28f, UiKit.Muted);
        UiKit.Outline(status, 0.15f);
        UiKit.Size(status, height: 38f);
    }

    private void BuildRankCard(Transform parent)
    {
        Image card = UiKit.Card(parent, "Rango");
        UiKit.Size(card, height: 380f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 40, 10f);
        row.childForceExpandWidth = true;

        GameObject left = UiKit.Node(card.transform, "Actual");
        UiKit.Vertical(left, 0, 2f, TextAnchor.MiddleCenter);
        TMP_Text leftLabel = UiKit.Text(left.transform, "TU RANGO", 32f, UiKit.LeagueColor(4));
        UiKit.Outline(leftLabel, 0.15f);
        UiKit.Size(leftLabel, height: 42f);
        currentEmblem = UiKit.Image(left.transform, "Emblema", Color.white, UiKit.RankEmblem(0));
        UiKit.Size(currentEmblem, height: 180f);
        currentName = UiKit.Text(left.transform, "...", 44f, Color.white);
        UiKit.Outline(currentName, 0.2f);
        UiKit.Size(currentName, height: 56f);

        GameObject middle = UiKit.Node(card.transform, "Puntos");
        UiKit.Vertical(middle, 0, 8f, TextAnchor.MiddleCenter);
        UiKit.Size(middle.transform, flexibleWidth: 1.3f);
        TMP_Text pointsLabel = UiKit.Text(middle.transform, "PUNTOS", 34f, Color.white);
        UiKit.Outline(pointsLabel, 0.15f);
        UiKit.Size(pointsLabel, height: 44f);

        GameObject pointsRow = UiKit.Node(middle.transform, "Valor");
        UiKit.Horizontal(pointsRow, 0, 12f).childForceExpandWidth = false;
        UiKit.Size(pointsRow.transform, height: 110f);
        UiKit.Size(UiKit.Image(pointsRow.transform, "Estrella", Color.white, UiKit.LoadSprite("UI/star_full")), width: 80f);
        pointsText = UiKit.Title(pointsRow.transform, "-", 96f);
        UiKit.Size(pointsText, width: 220f);

        progressFill = UiKit.ProgressBar(middle.transform, 56f, out progressText);

        toNextText = UiKit.Text(middle.transform, "", 30f, Color.white);
        UiKit.Outline(toNextText, 0.12f);
        UiKit.Size(toNextText, height: 80f);

        GameObject right = UiKit.Node(card.transform, "Siguiente");
        UiKit.Vertical(right, 0, 2f, TextAnchor.MiddleCenter);
        TMP_Text rightLabel = UiKit.Text(right.transform, "SIGUIENTE", 32f, UiKit.LeagueColor(4));
        UiKit.Outline(rightLabel, 0.15f);
        UiKit.Size(rightLabel, height: 42f);
        nextEmblem = UiKit.Image(right.transform, "Emblema", Color.white, UiKit.RankEmblem(1));
        UiKit.Size(nextEmblem, height: 180f);
        nextName = UiKit.Text(right.transform, "...", 44f, Color.white);
        UiKit.Outline(nextName, 0.2f);
        UiKit.Size(nextName, height: 56f);
    }

    private void BuildTiersTable(Transform parent)
    {
        Image card = UiKit.Card(parent, "Rangos");
        const float rowHeight = 66f;
        UiKit.Size(card, height: 64f + 48f + RankTiers.Leagues.Count * (rowHeight + 6f) + 70f);
        UiKit.Vertical(card.gameObject, 34, 6f);

        TMP_Text title = UiKit.Text(card.transform, "RANGOS  <size=60%>(5 divisiones cada uno)</size>", 42f, Color.white);
        UiKit.Outline(title, 0.18f);
        UiKit.Size(title, height: 58f);

        Sprite gem = UiKit.LoadSprite("Minerals/amethyst");
        Sprite coin = UiKit.LoadSprite("UI/coin");

        GameObject headerRow = UiKit.Node(card.transform, "Cabecera");
        UiKit.Horizontal(headerRow, 0, 10f).childForceExpandWidth = false;
        UiKit.Size(headerRow.transform, height: 44f);
        HeaderCell(headerRow.transform, "RANGO", 330f);
        HeaderCell(headerRow.transform, "PUNTOS", 200f);
        HeaderCell(headerRow.transform, "RECOMPENSA DE TEMPORADA", -1f);

        for (int i = RankTiers.Leagues.Count - 1; i >= 0; i--)
        {
            RankTiers.League league = RankTiers.Leagues[i];
            string range = league.MaxPoints == int.MaxValue ? $"{league.MinPoints}+" : $"{league.MinPoints} - {league.MaxPoints}";

            Image rowImage = UiKit.Row(card.transform, "Fila " + league.Name);
            UiKit.Size(rowImage, height: rowHeight);
            HorizontalLayoutGroup row = UiKit.Horizontal(rowImage.gameObject, 8, 10f);
            row.childForceExpandWidth = false;

            UiKit.Size(UiKit.Image(rowImage.transform, "Emblema", Color.white, UiKit.RankEmblem(i)), width: 70f);
            TMP_Text name = UiKit.Text(rowImage.transform, league.Name.ToUpperInvariant(), 38f, UiKit.LeagueColor(i),
                FontStyles.Normal, TextAlignmentOptions.Left);
            UiKit.Outline(name, 0.18f);
            UiKit.Size(name, width: 250f);
            UiKit.Size(UiKit.Text(rowImage.transform, range, 34f, Color.white), width: 200f);

            UiKit.Size(UiKit.Image(rowImage.transform, "Gema", Color.white, gem), width: 50f);
            UiKit.Size(UiKit.Text(rowImage.transform, league.SeasonGems.ToString(), 32f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left), width: 90f);
            UiKit.Size(UiKit.Image(rowImage.transform, "Moneda", Color.white, coin), width: 50f);
            UiKit.Size(UiKit.Text(rowImage.transform, league.SeasonCoins.ToString(), 32f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left), flexibleWidth: 1f);
        }
    }

    private static void HeaderCell(Transform parent, string text, float width)
    {
        TMP_Text cell = UiKit.Text(parent, text, 26f, UiKit.Muted);
        if (width > 0f) UiKit.Size(cell, width: width);
        else UiKit.Size(cell, flexibleWidth: 1f);
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
