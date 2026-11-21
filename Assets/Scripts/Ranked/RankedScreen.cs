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

    /// <summary>Construye la pantalla dentro de la zona de contenido del menú.</summary>
    public void Init(Transform content)
    {
        service = RankedServices.Current;
        Build(UiKit.ScrollArea(content, 14f));
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

            // Recompensas pendientes (subir de rango, fin de temporada).
            SeasonRewards.ClaimAndShow();

            // Que el rival vea la foto de perfil elegida en este dispositivo.
            if (profile.Avatar != ProfileAvatar.LocalId) ProfileAvatar.SyncToServer();
            RankedMatchController.myDisplayName = profile.Name;
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

        currentEmblem.sprite = UiKit.RankEmblem(tier);
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
            nextEmblem.sprite = UiKit.RankEmblem(tier);
            nextName.text = "—";
            return;
        }

        float fraction = Mathf.InverseLerp(tier.MinPoints, next.MinPoints, profile.Points);
        progressFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        progressText.text = $"{profile.Points} / {next.MinPoints}";
        toNextText.text = $"Faltan {next.MinPoints - profile.Points} puntos para subir a " +
                          $"<color=#{ColorUtility.ToHtmlStringRGB(UiKit.LeagueColor(next.LeagueIndex))}>{next.Name}</color>";
        nextEmblem.sprite = UiKit.RankEmblem(next);
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

    private void Build(Transform list)
    {
        GameObject column = list.gameObject;

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

        searchButton = UiKit.Button(column.transform, "BUSCAR PARTIDA", UiKit.ButtonStyle.Purple, OnSearch, 72f);
        UiKit.Size(searchButton, height: 170f);

        Image info = UiKit.Card(column.transform, "Info");
        UiKit.Size(info, height: 150f);
        var infoButton = info.gameObject.AddComponent<Button>();
        infoButton.targetGraphic = info;
        infoButton.onClick.AddListener(ShowSeasonEndRewards);
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

        Sprite coin = UiKit.LoadSprite("UI/coin");

        GameObject headerRow = UiKit.Node(card.transform, "Cabecera");
        UiKit.Horizontal(headerRow, 0, 10f).childForceExpandWidth = false;
        UiKit.Size(headerRow.transform, height: 44f);
        HeaderCell(headerRow.transform, "RANGO", 330f);
        HeaderCell(headerRow.transform, "PUNTOS", 200f);
        HeaderCell(headerRow.transform, $"AL LLEGAR (TEMPORADA {CurrentSeason})", -1f);

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

            // Lo que se gana al llegar a esta liga en la temporada actual: insignia exclusiva + monedas.
            RankTier firstTier = null;
            foreach (RankTier t in RankTiers.All)
            {
                if (t.LeagueIndex == i) { firstTier = t; break; }
            }
            SeasonReward reach = SeasonRules.RewardForTier(firstTier, CurrentSeason);

            if (!string.IsNullOrEmpty(reach.Badge))
            {
                GameObject badgeHolder = UiKit.Node(rowImage.transform, "Insignia");
                UiKit.Size(badgeHolder.transform, width: 70f);
                SeasonBadge.Create(badgeHolder.transform, CurrentSeason, i, 66f);
            }
            else
            {
                UiKit.Size(UiKit.Node(rowImage.transform, "SinInsignia").transform, width: 70f);
            }

            UiKit.Size(UiKit.Image(rowImage.transform, "Moneda", Color.white, coin), width: 50f);
            string coinsText = reach.Coins > 0 ? $"+{reach.Coins}" : "inicio";
            UiKit.Size(UiKit.Text(rowImage.transform, coinsText, 32f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left), flexibleWidth: 1f);
        }
    }

    private static void HeaderCell(Transform parent, string text, float width)
    {
        TMP_Text cell = UiKit.Text(parent, text, 26f, UiKit.Muted);
        if (width > 0f) UiKit.Size(cell, width: width);
        else UiKit.Size(cell, flexibleWidth: 1f);
    }

    private static int CurrentSeason => SeasonRules.CurrentSeason(GameClock.NowUtc);

    /// <summary>Temporadas de 60 días (las mismas para todos, las calcula SeasonRules).</summary>
    private static string SeasonText()
    {
        int season = CurrentSeason;
        TimeSpan left = TimeSpan.FromSeconds(Math.Max(0, SeasonRules.SeasonEndUtc(season) - GameClock.NowUtc));
        return $"Temporada {season}  ·  termina en {left.Days}d {left.Hours}h  <size=75%>(toca para ver premios)</size>";
    }

    private static void ShowSeasonEndRewards()
    {
        var sb = new StringBuilder();
        for (int i = RankTiers.Leagues.Count - 1; i >= 0; i--)
        {
            RankTiers.League league = RankTiers.Leagues[i];
            string color = ColorUtility.ToHtmlStringRGB(UiKit.LeagueColor(i));
            sb.AppendLine($"<color=#{color}>{league.Name}</color>: {league.SeasonGems} gemas · {league.SeasonCoins} monedas");
        }
        StoryPopup.Show($"Fin de la Temporada {CurrentSeason}",
            "Según tu rango final recibes:\n\n" + sb +
            $"\nLuego empiezas la siguiente con la mitad de tus puntos.\nCada temporada dura {SeasonRules.SeasonDays} días.",
            new StoryPopup.PopupButton("Entendido", null));
    }
}
