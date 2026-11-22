using System;
using System.Collections.Generic;
using System.Globalization;
using CrystalClash.Ranked.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tabla de posiciones de la clasificatoria (según el diseño): tu rango arriba,
/// pestañas, podio con los 3 primeros, lista del resto y "Tu posición" fija abajo.
/// </summary>
public class LeaderboardScreen : MonoBehaviour
{
    private const int Count = 50;

    private static readonly Color RowColor = new Color(0.16f, 0.14f, 0.36f, 0.95f);
    private static readonly Color MeColor = new Color(0.32f, 0.25f, 0.08f, 0.97f);

    private IRankedService service;
    private RankedProfile me;
    private Transform content;
    private Transform list;
    private Transform footer;
    private readonly List<Image> tabs = new List<Image>();
    private int tab;

    /// <summary>Reemplaza el contenido de la pantalla de clasificatoria por la tabla.</summary>
    public static void Open(Transform content, IRankedService service, RankedProfile me)
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        var screen = content.gameObject.AddComponent<LeaderboardScreen>();
        screen.service = service;
        screen.me = me;
        screen.content = content;
        screen.Build();
        screen.Load();
    }

    // ------------------------------------------------------------------

    private void Build()
    {
        GameObject column = UiKit.Node(content, "Posiciones");
        UiKit.Stretch((RectTransform)column.transform);
        UiKit.Vertical(column, 0, 16f);

        BuildHeader(column.transform);
        BuildMyRank(column.transform);
        BuildTabs(column.transform);

        // Zona con desplazamiento: ocupa todo el espacio que sobra.
        GameObject scrollHolder = UiKit.Node(column.transform, "Lista");
        LayoutElement holderSize = scrollHolder.AddComponent<LayoutElement>();
        holderSize.flexibleHeight = 1f;
        list = UiKit.ScrollArea(scrollHolder.transform, 14f);

        GameObject footerHolder = UiKit.Node(column.transform, "TuPosicion");
        UiKit.Size(footerHolder.transform, height: 150f);
        footer = footerHolder.transform;

        Message("Cargando...");
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = UiKit.Node(parent, "Encabezado");
        UiKit.Size(header.transform, height: 130f);
        HorizontalLayoutGroup row = UiKit.Horizontal(header, 0, 16f);
        row.childForceExpandHeight = false;

        UiKit.Size(UiKit.Button(header.transform, "<", UiKit.ButtonStyle.Gray, MenuScreens.ShowRanked, 60f), width: 120f, height: 120f);

        GameObject titles = UiKit.Node(header.transform, "Titulos");
        UiKit.Size(titles.transform, flexibleWidth: 1f, height: 130f);
        UiKit.Vertical(titles, 0, 0f, TextAnchor.MiddleCenter);
        UiKit.Size(UiKit.Title(titles.transform, "CLASIFICATORIA", 80f), height: 88f);
        TMP_Text season = UiKit.Text(titles.transform, $"Temporada {SeasonRules.CurrentSeason(GameClock.NowUtc)}", 34f, UiKit.Muted);
        UiKit.Outline(season, 0.15f);
        UiKit.Size(season, height: 40f);

        UiKit.Size(UiKit.Button(header.transform, "i", UiKit.ButtonStyle.Gray, RankedScreen.ShowSeasonEndRewards, 60f), width: 120f, height: 120f);
    }

    private void BuildMyRank(Transform parent)
    {
        Image card = UiKit.Card(parent, "TuRango");
        UiKit.Size(card, height: 250f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 30, 24f);
        row.childForceExpandHeight = false;

        int points = me != null ? me.Points : 0;
        RankTier tier = RankTiers.ForPoints(points);
        RankTier next = RankTiers.Next(tier);

        Image emblem = UiKit.Image(card.transform, "Emblema", Color.white, UiKit.RankEmblem(tier));
        emblem.preserveAspect = true;
        UiKit.Size(emblem, width: 190f, height: 190f);

        GameObject info = UiKit.Node(card.transform, "Info");
        UiKit.Size(info.transform, flexibleWidth: 1f, height: 190f);
        UiKit.Vertical(info, 0, 6f, TextAnchor.MiddleLeft);

        TMP_Text label = UiKit.Text(info.transform, "TU RANGO", 30f, UiKit.Muted, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Size(label, height: 36f);

        GameObject nameRow = UiKit.Node(info.transform, "Nombre");
        UiKit.Size(nameRow.transform, height: 64f);
        UiKit.Horizontal(nameRow, 0, 10f).childForceExpandHeight = true;
        TMP_Text name = UiKit.Text(nameRow.transform, TierLabel(tier), 58f, UiKit.LeagueColor(tier.LeagueIndex),
            FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(name, 0.2f);
        UiKit.Size(name, flexibleWidth: 1f);
        TMP_Text pts = UiKit.Text(nameRow.transform, $"{Format(points)} <size=55%>pts</size>", 56f, UiKit.Gold,
            FontStyles.Normal, TextAlignmentOptions.Right);
        UiKit.Outline(pts, 0.2f);
        UiKit.Size(pts, width: 260f);

        RectTransform fill = UiKit.ProgressBar(info.transform, 44f, out TMP_Text barText);
        TMP_Text toNext = UiKit.Text(info.transform, "", 28f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Size(toNext, height: 36f);

        if (next == null)
        {
            fill.anchorMax = Vector2.one;
            barText.text = Format(points);
            toNext.text = "¡Rango máximo!";
        }
        else
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(Mathf.InverseLerp(tier.MinPoints, next.MinPoints, points)), 1f);
            barText.text = $"{points} / {next.MinPoints}";
            string color = ColorUtility.ToHtmlStringRGB(UiKit.LeagueColor(next.LeagueIndex));
            toNext.text = $"Faltan <color=#FFD040>{next.MinPoints - points} pts</color> para <color=#{color}>{TierLabel(next, false)}</color>";
        }
    }

    private void BuildTabs(Transform parent)
    {
        Image bar = UiKit.Card(parent, "Pestanas");
        UiKit.Size(bar, height: 120f);
        HorizontalLayoutGroup row = UiKit.Horizontal(bar.gameObject, 14, 10f);
        row.childForceExpandWidth = true;

        string[] names = { "Global", "Amigos", "Semanal" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            Image tabImage = UiKit.Image(bar.transform, "Pestana " + names[i], Color.white, UiKit.ButtonSprite(UiKit.ButtonStyle.Purple));
            tabImage.type = Image.Type.Sliced;
            tabImage.preserveAspect = false;
            var button = tabImage.gameObject.AddComponent<Button>();
            Sound.AddClick(button);
            button.targetGraphic = tabImage;
            button.onClick.AddListener(() => SelectTab(index));
            TMP_Text text = UiKit.Text(tabImage.transform, names[i], 40f, Color.white);
            UiKit.Outline(text, 0.18f);
            UiKit.Stretch(text.rectTransform, 10f);
            tabs.Add(tabImage);
        }
        PaintTabs();
    }

    private void PaintTabs()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool selected = i == tab;
            tabs[i].color = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
            tabs[i].GetComponentInChildren<TMP_Text>().color = selected ? Color.white : UiKit.Muted;
        }
    }

    private void SelectTab(int index)
    {
        if (index == tab) return;
        tab = index;
        PaintTabs();
        if (tab == 0)
        {
            Load();
        }
        else if (tab == 1)
        {
            LoadFriends();
        }
        else
        {
            LoadWeekly();
        }
    }

    /// <summary>Pestaña Semanal: torneo de la semana (victoria 3, empate 1) con premios para los mejores.</summary>
    private async void LoadWeekly()
    {
        Clear();
        Message("Cargando...");
        try
        {
            WeeklyBoard board = await service.GetWeeklyAsync(Count);
            if (this == null || tab != 2) return;
            Clear();

            WeeklyInfo(board);

            var entries = new List<LeaderboardEntry>();
            foreach (WeeklyRow row in board.Entries)
            {
                entries.Add(new LeaderboardEntry { PlayerId = row.PlayerId, Rank = row.Rank, Name = row.Name, Points = row.Points, IsMe = row.IsMe });
            }
            var mine = new LeaderboardEntry
            {
                PlayerId = me != null ? me.PlayerId : "",
                Rank = board.MyRank,
                Name = me != null ? me.Name : "Tú",
                Points = board.MyPoints,
                IsMe = true
            };

            if (entries.Count == 0)
            {
                Message("Nadie ha jugado esta semana todavía.\n¡Gana una partida de clasificatoria y ponte primero!");
                BuildFooter(mine);
                return;
            }
            Fill(entries, mine);
        }
        catch (Exception e)
        {
            if (this == null || tab != 2) return;
            Debug.LogWarning("Torneo semanal: " + e.Message);
            Clear();
            Message("No se pudo cargar.\nRevisa tu internet.");
        }
    }

    /// <summary>Tarjeta con cuánto falta, cómo se suman puntos y botón de premios.</summary>
    private void WeeklyInfo(WeeklyBoard board)
    {
        Image card = UiKit.Card(list, "Torneo");
        UiKit.Size(card, height: 180f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 30, 16f);
        row.childForceExpandHeight = false;

        TimeSpan left = TimeSpan.FromSeconds(Math.Max(0, board.EndsUtc - GameClock.NowUtc));
        TMP_Text text = UiKit.Text(card.transform,
            $"<b>TORNEO SEMANAL</b>  ·  termina en <color=#FFD040>{left.Days}d {left.Hours}h</color>\n" +
            $"<size=80%>Victoria +{WeeklyRules.WinPoints} pts  ·  Empate +{WeeklyRules.DrawPoints} pt  ·  Los mejores ganan gemas</size>",
            34f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(text, 0.15f);
        UiKit.Size(text, flexibleWidth: 1f, height: 130f);

        UiKit.Size(UiKit.Button(card.transform, "PREMIOS", UiKit.ButtonStyle.Purple, ShowWeeklyRewards, 34f), width: 260f, height: 100f);
    }

    private static void ShowWeeklyRewards()
    {
        StoryPopup.Show("Premios del torneo",
            "Al terminar la semana (lunes), según tu puesto:\n\n" + string.Join("\n", WeeklyRules.RewardLines) +
            "\n\n<size=80%>El premio llega solo la próxima vez que entres a la clasificatoria.</size>",
            new StoryPopup.PopupButton("Entendido", null));
    }

    // ------------------------------------------------------------------
    // Datos
    // ------------------------------------------------------------------

    private async void Load()
    {
        Clear();
        Message("Cargando...");
        try
        {
            List<LeaderboardEntry> entries = await service.GetLeaderboardAsync(Count);
            if (this == null || tab != 0) return;
            Clear();
            Fill(entries ?? new List<LeaderboardEntry>());
        }
        catch (Exception e)
        {
            if (this == null || tab != 0) return;
            Debug.LogWarning("Tabla de posiciones: " + e.Message);
            Clear();
            Message("No se pudo cargar.\nRevisa tu internet.");
        }
    }

    /// <summary>Pestaña Amigos: tú y tus amigos ordenados por puntos.</summary>
    private async void LoadFriends()
    {
        Clear();
        Message("Cargando...");
        try
        {
            FriendsList friends = await service.GetFriendsAsync();
            if (this == null || tab != 1) return;
            Clear();

            var entries = new List<LeaderboardEntry>();
            foreach (FriendInfo f in friends.Friends) entries.Add(new LeaderboardEntry { PlayerId = f.PlayerId, Name = f.Name, Points = f.Points });
            if (me != null) entries.Add(new LeaderboardEntry { PlayerId = me.PlayerId, Name = me.Name, Points = me.Points, IsMe = true });
            entries.Sort((a, b) => b.Points.CompareTo(a.Points));
            for (int i = 0; i < entries.Count; i++) entries[i].Rank = i + 1;

            if (friends.Friends.Count == 0)
            {
                Message("Todavía no tienes amigos.\nAgrégalos desde tu <b>Perfil → VER AMIGOS</b> con su ID.");
                BuildFooter(entries.Find(e => e.IsMe));
                return;
            }
            Fill(entries);
        }
        catch (Exception e)
        {
            if (this == null || tab != 1) return;
            Debug.LogWarning("Tabla de amigos: " + e.Message);
            Clear();
            Message("No se pudo cargar.\nRevisa tu internet.");
        }
    }

    private void Fill(List<LeaderboardEntry> entries, LeaderboardEntry footerFallback = null)
    {
        if (entries.Count == 0)
        {
            Message("Todavía no hay jugadores esta temporada.\n¡Juega una partida y sé el primero!");
        }
        else
        {
            BuildPodium(entries);
            // Debajo del podio, la tabla completa (incluidos los 3 primeros).
            for (int i = 0; i < entries.Count; i++) BuildRow(list, entries[i], false);
        }

        LeaderboardEntry mine = entries.Find(e => e.IsMe) ?? footerFallback;
        BuildFooter(mine);
    }

    // ------------------------------------------------------------------
    // Podio y filas
    // ------------------------------------------------------------------

    private void BuildPodium(List<LeaderboardEntry> entries)
    {
        GameObject podium = UiKit.Node(list, "Podio");
        UiKit.Size(podium.transform, height: 560f);
        HorizontalLayoutGroup row = UiKit.Horizontal(podium, 0, 14f, TextAnchor.LowerCenter);
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;

        // Orden visual: 2º, 1º, 3º
        int[] order = { 1, 0, 2 };
        foreach (int i in order)
        {
            if (i < entries.Count) PodiumColumn(podium.transform, entries[i], i);
            else UiKit.Size(UiKit.Node(podium.transform, "Vacio").transform, height: 10f);
        }
    }

    private static void PodiumColumn(Transform parent, LeaderboardEntry entry, int place)
    {
        float[] blockHeights = { 160f, 120f, 95f };
        float[] emblemSizes = { 190f, 160f, 150f };
        Color[] blockTops =
        {
            new Color(1f, 0.8f, 0.2f),
            new Color(0.78f, 0.83f, 0.9f),
            new Color(0.9f, 0.52f, 0.25f)
        };
        Color[] blockBottoms =
        {
            new Color(0.82f, 0.55f, 0.05f),
            new Color(0.48f, 0.54f, 0.64f),
            new Color(0.6f, 0.3f, 0.12f)
        };

        RankTier tier = RankTiers.ForPoints(entry.Points);
        float crown = place == 0 ? 70f : 0f;
        float height = crown + emblemSizes[place] + 60f + 50f + blockHeights[place] + 30f;

        GameObject column = UiKit.Node(parent, "Puesto " + (place + 1));
        // Tocar el puesto abre el perfil del jugador.
        Image hit = column.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        MakeClickable(hit, entry);
        UiKit.Size(column.transform, height: height);
        UiKit.Vertical(column, 0, 6f, TextAnchor.LowerCenter);

        if (place == 0)
        {
            Image crownImage = UiKit.Image(column.transform, "Corona", Color.white, UiKit.LoadSprite("UI/ach_champion"));
            crownImage.preserveAspect = true;
            UiKit.Size(crownImage, height: crown);
        }

        Image emblem = UiKit.Image(column.transform, "Emblema", Color.white, UiKit.RankEmblem(tier));
        emblem.preserveAspect = true;
        UiKit.Size(emblem, height: emblemSizes[place]);

        TMP_Text name = UiKit.Text(column.transform, entry.Name, 40f, entry.IsMe ? UiKit.Gold : (place == 0 ? UiKit.Gold : Color.white));
        UiKit.Outline(name, 0.2f);
        UiKit.Size(name, height: 54f);

        TMP_Text pts = UiKit.Text(column.transform, $"{Format(entry.Points)} pts", 38f, place == 0 ? UiKit.Gold : Color.white);
        UiKit.Outline(pts, 0.2f);
        UiKit.Size(pts, height: 46f);

        // Bloque del podio con relieve: base oscura, mitad de arriba más clara y brillo en el borde.
        Color top = blockTops[place];
        Color bottom = blockBottoms[place];
        Image block = UiKit.Image(column.transform, "Bloque", bottom);
        UiKit.Size(block, height: blockHeights[place]);
        var edge = block.gameObject.AddComponent<UnityEngine.UI.Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.6f);
        edge.effectDistance = new Vector2(3f, -5f);

        Image upper = UiKit.Image(block.transform, "Claro", top);
        upper.raycastTarget = false;
        RectTransform upperRect = upper.rectTransform;
        upperRect.anchorMin = new Vector2(0f, 0.45f);
        upperRect.anchorMax = Vector2.one;
        upperRect.offsetMin = upperRect.offsetMax = Vector2.zero;

        Image shine = UiKit.Image(block.transform, "Brillo", new Color(1f, 1f, 1f, 0.55f));
        shine.raycastTarget = false;
        RectTransform shineRect = shine.rectTransform;
        shineRect.anchorMin = new Vector2(0f, 1f);
        shineRect.anchorMax = Vector2.one;
        shineRect.pivot = new Vector2(0.5f, 1f);
        shineRect.sizeDelta = new Vector2(0f, 8f);
        shineRect.anchoredPosition = Vector2.zero;

        TMP_Text number = UiKit.Text(block.transform, (place + 1).ToString(), 96f, Color.white);
        number.outlineColor = new Color32(30, 20, 10, 255);
        number.outlineWidth = 0.3f;
        UiKit.Stretch(number.rectTransform, 6f);
    }

    /// <summary>Al tocar la fila (o el puesto del podio) se abre el perfil de ese jugador.</summary>
    private static void MakeClickable(Image target, LeaderboardEntry entry)
    {
        if (string.IsNullOrEmpty(entry.PlayerId)) return;
        var button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        Sound.AddClick(button);
        string playerId = entry.PlayerId;
        button.onClick.AddListener(() => PlayerProfilePopup.Show(playerId));
    }

    private static void BuildRow(Transform parent, LeaderboardEntry entry, bool sticky)
    {
        RankTier tier = RankTiers.ForPoints(entry.Points);
        Image rowImage = UiKit.Image(parent, "Fila " + entry.Rank, entry.IsMe ? MeColor : RowColor, UiKit.LoadSliced("UI/row", 24, 160f));
        MakeClickable(rowImage, entry);
        rowImage.type = Image.Type.Sliced;
        rowImage.preserveAspect = false;
        if (!sticky) UiKit.Size(rowImage, height: 130f);
        else UiKit.Stretch(rowImage.rectTransform);

        if (entry.IsMe)
        {
            var outline = rowImage.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = UiKit.Gold;
            outline.effectDistance = new Vector2(4f, -4f);
        }

        HorizontalLayoutGroup row = UiKit.Horizontal(rowImage.gameObject, 18, 20f);
        row.padding = new RectOffset(36, 36, 12, 12);
        row.childForceExpandHeight = false;

        TMP_Text rank = UiKit.Text(rowImage.transform, entry.Rank > 0 ? entry.Rank.ToString() : "-", 52f, entry.IsMe ? UiKit.Gold : UiKit.Muted);
        UiKit.Outline(rank, 0.18f);
        UiKit.Size(rank, width: 80f, height: 80f);

        Image emblem = UiKit.Image(rowImage.transform, "Emblema", Color.white, UiKit.RankEmblem(tier));
        emblem.preserveAspect = true;
        UiKit.Size(emblem, width: 100f, height: 100f);

        GameObject names = UiKit.Node(rowImage.transform, "Nombre");
        UiKit.Size(names.transform, flexibleWidth: 1f, height: 100f);
        UiKit.Vertical(names, 0, 0f, TextAnchor.MiddleLeft);

        string topLine = sticky ? "TU POSICIÓN" : (entry.IsMe ? entry.Name + " (tú)" : entry.Name);
        TMP_Text first = UiKit.Text(names.transform, topLine, sticky ? 30f : 44f, sticky ? UiKit.Muted : Color.white,
            FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(first, 0.18f);
        UiKit.Size(first, height: 50f);

        string bottomLine = sticky ? entry.Name : TierLabel(tier, false);
        TMP_Text second = UiKit.Text(names.transform, bottomLine, sticky ? 44f : 34f,
            sticky ? Color.white : UiKit.LeagueColor(tier.LeagueIndex), FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(second, 0.18f);
        UiKit.Size(second, height: 46f);

        TMP_Text pts = UiKit.Text(rowImage.transform, $"{Format(entry.Points)} <size=55%>pts</size>", 52f, UiKit.Gold,
            FontStyles.Normal, TextAlignmentOptions.Right);
        UiKit.Outline(pts, 0.2f);
        UiKit.Size(pts, width: 250f, height: 80f);
    }

    private void BuildFooter(LeaderboardEntry mine)
    {
        for (int i = footer.childCount - 1; i >= 0; i--) Destroy(footer.GetChild(i).gameObject);
        if (mine == null)
        {
            // No estás en el top (o no has jugado esta temporada): mostramos tus puntos igual.
            mine = new LeaderboardEntry { Rank = 0, Name = me != null ? me.Name : "Tú", Points = me != null ? me.Points : 0, IsMe = true };
        }
        BuildRow(footer, mine, true);
    }

    // ------------------------------------------------------------------
    // Ayudas
    // ------------------------------------------------------------------

    private void Clear()
    {
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
    }

    private void Message(string text)
    {
        TMP_Text message = UiKit.Text(list, text, 38f, Color.white);
        UiKit.Outline(message, 0.15f);
        UiKit.Size(message, height: 200f);
    }

    private static readonly string[] Roman = { "", "I", "II", "III", "IV", "V" };

    /// <summary>"BRONCE II" (o "Bronce II" si upper = false).</summary>
    private static string TierLabel(RankTier tier, bool upper = true)
    {
        string league = upper ? tier.League.ToUpperInvariant() : tier.League;
        return tier.Division > 0 && tier.Division < Roman.Length ? $"{league} {Roman[tier.Division]}" : league;
    }

    /// <summary>1240 -> "1.240"</summary>
    private static string Format(int value)
    {
        return value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
