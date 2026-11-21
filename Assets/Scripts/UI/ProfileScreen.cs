using System;
using System.Collections.Generic;
using CrystalClash.Ranked.Logic;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Perfil del jugador: avatar, nombre (editable), ID, fecha de inicio,
/// emblema de rango, nivel con experiencia, estadísticas y logros.
/// </summary>
public static class ProfileScreen
{
    // Avatares: si existen Resources/UI/Avatars/avatar_1..N se usan esos;
    // si no, estos sprites provisionales.
    private static readonly string[] FallbackAvatars =
    {
        "UI/Nav/icono_perfil", "Minerals/sapphire", "Minerals/emerald", "Minerals/amethyst",
        "Minerals/ruby", "Minerals/gold", "Minerals/diamond", "Minerals/prism"
    };

    private const int MaxCustomAvatars = 24;
    private const string Quote = "“¡Sigue jugando y alcanza la cima!”";

    // Elementos que se completan cuando llega el perfil de clasificatoria
    private class View
    {
        public TMP_Text Name;
        public TMP_Text Id;
        public Image RankEmblem;
        public TMP_Text RankName;
        public TMP_Text Played;
        public TMP_Text Wins;
        public TMP_Text WinRate;
        public Transform AchievementsRow;
        public Transform BadgesRow;
        public TMP_Text BadgesEmpty;
        public RankedProfile Profile;
    }

    public static void Build(Transform root)
    {
        Transform list = UiKit.ScrollArea(root, 16f);
        PlayerProgress p = SaveManager.Progress;
        var view = new View();

        BuildHeader(list, p, view);
        BuildLevel(list, p);
        BuildAccount(list);
        BuildStats(list, p, view);
        BuildAchievements(list, view);
        BuildBadges(list, view);

        Image quoteCard = UiKit.Card(list, "Frase");
        UiKit.Size(quoteCard, height: 120f);
        UiKit.Vertical(quoteCard.gameObject, 30, 0f, TextAnchor.MiddleCenter);
        TMP_Text quote = UiKit.Text(quoteCard.transform, Quote, 40f, Color.white, FontStyles.Italic);
        UiKit.Outline(quote, 0.15f);
        UiKit.Size(quote, height: 60f);

        FillAchievements(view, 0, 0);
        LoadRanked(view);
    }

    // ------------------------------------------------------------------
    // Cabecera
    // ------------------------------------------------------------------

    private static void BuildHeader(Transform parent, PlayerProgress p, View view)
    {
        Image card = UiKit.Card(parent, "Cabecera");
        UiKit.Size(card, height: 430f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 36, 20f);
        row.childForceExpandWidth = false;

        // Avatar con marco y lápiz
        GameObject avatarHolder = UiKit.Node(card.transform, "Avatar");
        UiKit.Size(avatarHolder.transform, width: 250f);
        Image frame = UiKit.Image(avatarHolder.transform, "Marco", Color.white, UiKit.PanelSprite());
        frame.type = Image.Type.Sliced;
        frame.preserveAspect = false;
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(230f, 230f);
        if (SeasonRules.TryParseBadge(p.avatarBadge, out int badgeSeason, out int badgeLeague))
        {
            // Foto de perfil: una insignia de temporada ganada.
            RectTransform badge = SeasonBadge.Create(frame.transform, badgeSeason, badgeLeague, 190f);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = Vector2.zero;
        }
        else
        {
            Image avatar = UiKit.Image(frame.transform, "Imagen", Color.white, AvatarSprite(p.avatar));
            avatar.raycastTarget = false;
            UiKit.Stretch(avatar.rectTransform, 38f);
        }
        SmallIconButton(frame.transform, "UI/icon_pencil", new Vector2(1f, 0f), new Vector2(-10f, 10f), 76f, () => ChooseAvatar(view));

        // Centro: nombre, ID, miembro desde
        GameObject middle = UiKit.Node(card.transform, "Datos");
        UiKit.Vertical(middle, 0, 10f, TextAnchor.MiddleLeft);
        UiKit.Size(middle.transform, flexibleWidth: 1f);

        GameObject nameRow = UiKit.Node(middle.transform, "Nombre");
        HorizontalLayoutGroup nameLayout = UiKit.Horizontal(nameRow, 0, 12f, TextAnchor.MiddleLeft);
        nameLayout.childForceExpandWidth = false;
        UiKit.Size(nameRow.transform, height: 96f);
        view.Name = UiKit.Text(nameRow.transform, "...", 62f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(view.Name, 0.2f);
        UiKit.Size(view.Name, flexibleWidth: 1f);
        UiKit.Size(SmallIconButton(nameRow.transform, "UI/icon_pencil", Vector2.zero, Vector2.zero, 0f, () => EditName(view)), width: 86f);

        GameObject idRow = UiKit.Node(middle.transform, "Id");
        HorizontalLayoutGroup idLayout = UiKit.Horizontal(idRow, 0, 12f, TextAnchor.MiddleLeft);
        idLayout.childForceExpandWidth = false;
        UiKit.Size(idRow.transform, height: 64f);
        view.Id = UiKit.Text(idRow.transform, "ID: ...", 40f, UiKit.Muted, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(view.Id, 0.15f);
        UiKit.Size(view.Id, flexibleWidth: 1f);
        UiKit.Size(SmallIconButton(idRow.transform, "UI/icon_copy", Vector2.zero, Vector2.zero, 0f, () => CopyId(view)), width: 70f);

        Image member = UiKit.Row(middle.transform, "Miembro");
        UiKit.Size(member, height: 120f);
        HorizontalLayoutGroup memberLayout = UiKit.Horizontal(member.gameObject, 16, 14f, TextAnchor.MiddleLeft);
        memberLayout.childForceExpandWidth = false;
        UiKit.Size(UiKit.Image(member.transform, "Corona", Color.white, UiKit.LoadSprite("UI/ach_champion")), width: 90f);
        string since = p.createdUtc > 0
            ? DateTimeOffset.FromUnixTimeSeconds(p.createdUtc).ToLocalTime().ToString("dd / MM / yyyy")
            : DateTime.Now.ToString("dd / MM / yyyy");
        TMP_Text memberText = UiKit.Text(member.transform, $"Miembro desde:\n<color=#FFFFFF>{since}</color>", 34f, UiKit.Muted,
            FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(memberText, 0.12f);
        UiKit.Size(memberText, flexibleWidth: 1f);

        // Derecha: emblema de rango con cinta
        GameObject rank = UiKit.Node(card.transform, "Rango");
        UiKit.Vertical(rank, 0, -10f, TextAnchor.MiddleCenter);
        UiKit.Size(rank.transform, width: 290f);
        view.RankEmblem = UiKit.Image(rank.transform, "Emblema", Color.white, UiKit.RankEmblem(RankTiers.ForPoints(0)));
        UiKit.Size(view.RankEmblem, height: 260f);

        Image ribbon = UiKit.Image(rank.transform, "Cinta", Color.white, UiKit.ButtonSprite(UiKit.ButtonStyle.Blue));
        ribbon.type = Image.Type.Sliced;
        ribbon.preserveAspect = false;
        UiKit.Size(ribbon, height: 92f);
        view.RankName = UiKit.Text(ribbon.transform, "...", 42f, Color.white);
        UiKit.Outline(view.RankName, 0.2f);
        UiKit.Stretch(view.RankName.rectTransform, 16f);
    }

    private static Button SmallIconButton(Transform parent, string icon, Vector2 anchor, Vector2 position, float size, Action onClick)
    {
        Button button = UiKit.Button(parent, "", UiKit.ButtonStyle.Blue, onClick);
        UnityEngine.Object.Destroy(button.GetComponentInChildren<TMP_Text>().gameObject);
        Image image = UiKit.Image(button.transform, "Icono", Color.white, UiKit.LoadSprite(icon));
        image.raycastTarget = false;
        UiKit.Stretch(image.rectTransform, 20f);

        if (size > 0f)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
        }
        return button;
    }

    // ------------------------------------------------------------------
    // Nivel y estadísticas
    // ------------------------------------------------------------------

    /// <summary>Guardado en la nube: vincular con Google para no perder el progreso.</summary>
    private static void BuildAccount(Transform parent)
    {
        bool linked = AccountLink.IsLinked;

        Image card = UiKit.Card(parent, "Cuenta");
        UiKit.Size(card, height: 150f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 30, 18f);
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        row.childAlignment = TextAnchor.MiddleLeft;

        TMP_Text info = UiKit.Text(card.transform,
            linked ? "<b>Cuenta vinculada</b>\n<size=75%>Tu progreso se guarda en la nube</size>"
                   : "<b>Cuenta de invitado</b>\n<size=75%>Vincula para no perder tu progreso</size>",
            38f, linked ? UiKit.Gold : Color.white);
        info.alignment = TextAlignmentOptions.Left;
        UiKit.Outline(info, 0.15f);
        UiKit.Size(info, flexibleWidth: 1f, height: 100f);

        if (!linked)
        {
            UiKit.Size(UiKit.Button(card.transform, "VINCULAR CON GOOGLE", UiKit.ButtonStyle.Green, AccountLink.StartLink, 34f),
                width: 400f, height: 96f);
        }
    }

    private static void BuildLevel(Transform parent, PlayerProgress p)
    {
        PlayerLevel.FromXp(p.xp, out int level, out int inLevel, out int next);

        Image card = UiKit.Card(parent, "Nivel");
        UiKit.Size(card, height: 160f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 36, 18f);
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        UiKit.Size(UiKit.Image(card.transform, "Estrella", Color.white, UiKit.LoadSprite("UI/star_full")), width: 100f, height: 100f);
        TMP_Text levelText = UiKit.Title(card.transform, $"<size=70%>Nivel</size> {level}", 64f);
        UiKit.Size(levelText, width: 250f, height: 90f);

        GameObject barHolder = UiKit.Node(card.transform, "Barra");
        UiKit.Size(barHolder.transform, flexibleWidth: 1f, height: 60f);
        UiKit.Vertical(barHolder, 0, 0f, TextAnchor.MiddleCenter);
        RectTransform fill = UiKit.ProgressBar(barHolder.transform, 56f, out TMP_Text barLabel);
        fill.anchorMax = new Vector2(Mathf.Clamp01(inLevel / (float)next), 1f);
        barLabel.text = "";

        TMP_Text xp = UiKit.Text(card.transform, $"{inLevel} / {next}", 40f, Color.white);
        UiKit.Outline(xp, 0.18f);
        UiKit.Size(xp, width: 210f, height: 60f);
    }

    private static void BuildStats(Transform parent, PlayerProgress p, View view)
    {
        Image card = UiKit.Card(parent, "Estadisticas");
        UiKit.Size(card, height: 270f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 30, 6f);
        row.childForceExpandWidth = true;

        view.Played = Stat(card.transform, "UI/icon_swords", "Partidas jugadas", "...");
        view.Wins = Stat(card.transform, "UI/Nav/icono_clasificatoria", "Victorias", "...");
        view.WinRate = Stat(card.transform, "UI/icon_target", "% de victorias", "...");
        Stat(card.transform, "Minerals/amethyst", "Cristales", p.gems.ToString("N0"));
    }

    private static TMP_Text Stat(Transform parent, string icon, string label, string value)
    {
        GameObject column = UiKit.Node(parent, label);
        UiKit.Vertical(column, 0, 2f, TextAnchor.MiddleCenter);
        UiKit.Size(UiKit.Image(column.transform, "Icono", Color.white, UiKit.LoadSprite(icon)), height: 96f);
        TMP_Text labelText = UiKit.Text(column.transform, label, 30f, Color.white);
        UiKit.Outline(labelText, 0.12f);
        UiKit.Size(labelText, height: 40f);
        TMP_Text valueText = UiKit.Text(column.transform, value, 56f, Color.white);
        UiKit.Outline(valueText, 0.2f);
        UiKit.Size(valueText, height: 66f);
        return valueText;
    }

    // ------------------------------------------------------------------
    // Logros
    // ------------------------------------------------------------------

    private static void BuildAchievements(Transform parent, View view)
    {
        Image card = UiKit.Card(parent, "Logros");
        UiKit.Size(card, height: 430f);
        UiKit.Vertical(card.gameObject, 30, 6f);

        GameObject title = UiKit.Node(card.transform, "Titulo");
        HorizontalLayoutGroup titleRow = UiKit.Horizontal(title, 0, 14f);
        titleRow.childForceExpandWidth = false;
        UiKit.Size(title.transform, height: 76f);
        UiKit.Size(UiKit.Image(title.transform, "Corona", Color.white, UiKit.LoadSprite("UI/coin")), width: 60f);
        UiKit.Size(UiKit.Title(title.transform, "Logros destacados", 54f), width: 520f);

        GameObject row = UiKit.Node(card.transform, "Fila");
        UiKit.Horizontal(row, 0, 10f).childForceExpandWidth = true;
        UiKit.Size(row.transform, height: 290f);
        view.AchievementsRow = row.transform;
    }

    private static void FillAchievements(View view, int rankedWins, int rankedPoints)
    {
        if (view.AchievementsRow == null) return;
        foreach (Transform child in view.AchievementsRow) UnityEngine.Object.Destroy(child.gameObject);

        int firstWorld = LevelCatalog.Worlds.Count > 0 ? LevelCatalog.Worlds[0].LevelCount : LevelCatalog.Count;
        List<AchievementStatus> list = Achievements.Evaluate(SaveManager.Progress, firstWorld, rankedWins, rankedPoints);
        foreach (AchievementStatus a in list)
        {
            AchievementStatus achievement = a;
            Image cell = UiKit.Image(view.AchievementsRow, achievement.Id, new Color(0f, 0f, 0f, 0f));
            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;
            button.onClick.AddListener(() => StoryPopup.Show(achievement.Name.Replace("\n", " "),
                $"{achievement.Description}\n\nProgreso: <b>{achievement.Current} / {achievement.Target}</b>",
                new StoryPopup.PopupButton("Cerrar", null)));

            UiKit.Vertical(cell.gameObject, 0, 2f, TextAnchor.UpperCenter);
            Image icon = UiKit.Image(cell.transform, "Escudo", achievement.Unlocked ? Color.white : new Color(0.35f, 0.35f, 0.4f, 0.8f),
                UiKit.LoadSprite("UI/ach_" + achievement.Id));
            icon.raycastTarget = false;
            UiKit.Size(icon, height: 160f);

            TMP_Text name = UiKit.Text(cell.transform, achievement.Name, 30f, achievement.Unlocked ? Color.white : UiKit.Muted);
            UiKit.Outline(name, 0.15f);
            UiKit.Size(name, height: 76f);

            string state = achievement.Unlocked ? "<color=#66FF77>¡Logrado!</color>" : $"{achievement.Current} / {achievement.Target}";
            TMP_Text progress = UiKit.Text(cell.transform, state, 28f, UiKit.Gold);
            UiKit.Outline(progress, 0.15f);
            UiKit.Size(progress, height: 40f);
        }
    }

    // ------------------------------------------------------------------
    // Insignias de temporada
    // ------------------------------------------------------------------

    private static void BuildBadges(Transform parent, View view)
    {
        Image card = UiKit.Card(parent, "Insignias");
        UiKit.Size(card, height: 330f);
        UiKit.Vertical(card.gameObject, 30, 6f);
        UiKit.Size(UiKit.Title(card.transform, "Insignias de temporada", 50f), height: 70f);

        GameObject row = UiKit.Node(card.transform, "Fila");
        HorizontalLayoutGroup layout = UiKit.Horizontal(row, 0, 12f, TextAnchor.MiddleLeft);
        layout.childForceExpandWidth = false;
        UiKit.Size(row.transform, height: 190f);
        view.BadgesRow = row.transform;

        view.BadgesEmpty = UiKit.Text(row.transform,
            "Sube de liga en la clasificatoria para ganar insignias exclusivas de cada temporada.", 32f, UiKit.Muted);
        UiKit.Size(view.BadgesEmpty, flexibleWidth: 1f);
    }

    private static void FillBadges(View view, List<string> badges)
    {
        if (view.BadgesRow == null || badges == null || badges.Count == 0) return;
        view.BadgesEmpty.gameObject.SetActive(false);

        // Las más nuevas y altas primero; caben 5 a lo ancho.
        var parsed = new List<(int season, int league)>();
        foreach (string badge in badges)
        {
            if (SeasonRules.TryParseBadge(badge, out int season, out int league)) parsed.Add((season, league));
        }
        parsed.Sort((a, b) => a.season != b.season ? b.season.CompareTo(a.season) : b.league.CompareTo(a.league));

        int shown = 0;
        foreach (var (season, league) in parsed)
        {
            if (shown++ >= 5) break;
            GameObject holder = UiKit.Node(view.BadgesRow, "Insignia");
            UiKit.Size(holder.transform, width: 180f);
            RectTransform badge = SeasonBadge.Create(holder.transform, season, league, 175f);
            var button = badge.gameObject.AddComponent<Button>();
            Image hit = badge.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            button.targetGraphic = hit;
            int s = season, l = league;
            button.onClick.AddListener(() => StoryPopup.ShowWithBadge($"{SeasonBadge.LeagueName(l)} · Temporada {s}",
                "Insignia exclusiva por llegar a esta liga en esa temporada.", s, l, new StoryPopup.PopupButton("Cerrar", null)));
        }
    }

    // ------------------------------------------------------------------
    // Datos de clasificatoria (servidor)
    // ------------------------------------------------------------------

    private static async void LoadRanked(View view)
    {
        try
        {
            IRankedService service = RankedServices.Current;
            await service.SignInAsync();
            RankedProfile profile = await service.GetProfileAsync();
            if (view.Name == null) return; // la pantalla ya se cerró

            view.Profile = profile;
            view.Name.text = profile.Name;
            view.Id.text = "ID: #" + DisplayId(profile);

            RankTier tier = profile.Tier;
            view.RankEmblem.sprite = UiKit.RankEmblem(tier);
            view.RankName.text = tier.Name.ToUpperInvariant();

            int played = profile.Wins + profile.Losses + profile.Draws;
            view.Played.text = played.ToString("N0");
            view.Wins.text = profile.Wins.ToString("N0");
            view.WinRate.text = played > 0 ? $"{100f * profile.Wins / played:0.#}%" : "-";

            FillAchievements(view, profile.Wins, profile.Points);
            FillBadges(view, profile.Badges);
            SeasonRewards.ClaimAndShow();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Perfil: " + e.Message);
            if (view.Name == null) return;
            view.Name.text = "Sin conexión";
            view.Played.text = view.Wins.text = view.WinRate.text = "-";
        }
    }

    /// <summary>ID que se muestra: el número en orden de registro (100001...) que da el servidor.</summary>
    public static string DisplayId(RankedProfile profile)
    {
        return profile.ShortId > 0 ? profile.ShortId.ToString() : ShortId(profile.PlayerId);
    }

    /// <summary>Respaldo si el servidor todavía no dio un número: 6 cifras a partir del ID del jugador.</summary>
    public static string ShortId(string playerId)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in playerId ?? "") hash = hash * 31 + c;
            return (100000 + (uint)hash % 900000).ToString();
        }
    }

    private static void CopyId(View view)
    {
        if (view.Profile == null) return;
        string id = "#" + DisplayId(view.Profile);
        GUIUtility.systemCopyBuffer = id;
        StoryPopup.Show("ID copiado", id, new StoryPopup.PopupButton("Genial", null));
    }

    private static void EditName(View view)
    {
        string current = view.Profile != null ? view.Profile.Name : "";
        StoryPopup.ShowInput("Tu nombre",
            $"De {RankedRules.NameMinLength} a {RankedRules.NameMaxLength} letras, números o _",
            current, RankedRules.NameMaxLength, "Guardar", SaveName);
    }

    private static async void SaveName(string name)
    {
        if (RankedRules.NormalizeName(name) == null)
        {
            StoryPopup.Show("Nombre inválido",
                $"Usa de {RankedRules.NameMinLength} a {RankedRules.NameMaxLength} letras, números o _ (sin símbolos).",
                new StoryPopup.PopupButton("Entendido", null));
            return;
        }

        try
        {
            await RankedServices.Current.SetNameAsync(name);
            MenuScreens.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Cambiar nombre: " + e.Message);
            StoryPopup.Show("No se pudo cambiar el nombre", "Revisa tu conexión e inténtalo otra vez.",
                new StoryPopup.PopupButton("Cerrar", null));
        }
    }

    // ------------------------------------------------------------------
    // Avatares
    // ------------------------------------------------------------------

    public static int AvatarCount
    {
        get
        {
            int custom = 0;
            while (custom < MaxCustomAvatars && Resources.Load<Texture2D>($"UI/Avatars/avatar_{custom + 1}") != null) custom++;
            return custom > 0 ? custom : FallbackAvatars.Length;
        }
    }

    public static Sprite AvatarSprite(int index)
    {
        Sprite custom = UiKit.LoadSprite($"UI/Avatars/avatar_{index + 1}");
        if (custom != null) return custom;
        int i = Mathf.Clamp(index, 0, FallbackAvatars.Length - 1);
        return UiKit.LoadSprite(FallbackAvatars[i]);
    }

    private static void ChooseAvatar(View view)
    {
        int count = AvatarCount;
        var cells = new List<StoryPopup.PopupButton>();
        for (int i = 0; i < count; i++)
        {
            int index = i;
            cells.Add(new StoryPopup.PopupButton(AvatarSprite(i), () =>
            {
                SaveManager.Progress.avatar = index;
                SaveManager.Progress.avatarBadge = "";
                SaveManager.Save();
                ProfileAvatar.SyncToServer();
                MenuScreens.Refresh();
            }));
        }

        // Insignias de temporada ganadas: también sirven como foto de perfil.
        List<string> badges = view.Profile != null ? view.Profile.Badges : null;
        int badgeCount = 0;
        if (badges != null)
        {
            foreach (string badge in badges)
            {
                if (!SeasonRules.TryParseBadge(badge, out int season, out int league)) continue;
                string id = badge;
                cells.Add(new StoryPopup.PopupButton(t =>
                {
                    RectTransform b = SeasonBadge.Create(t, season, league, 120f);
                    b.anchorMin = b.anchorMax = new Vector2(0.5f, 0.5f);
                    b.anchoredPosition = Vector2.zero;
                }, () =>
                {
                    SaveManager.Progress.avatarBadge = id;
                    SaveManager.Save();
                    ProfileAvatar.SyncToServer();
                    MenuScreens.Refresh();
                }));
                badgeCount++;
            }
        }

        string body = badgeCount > 0
            ? "Avatares e insignias de temporada que ganaste"
            : "Gana insignias en la clasificatoria para usarlas como foto de perfil";
        StoryPopup.ShowGrid("Foto de perfil", body, cells, 4, new StoryPopup.PopupButton("Cerrar", null));
    }
}
