using System;
using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Número de día del jugador (días desde 1970 en su hora local): cambia a medianoche.</summary>
public static class GameDays
{
    public static int Today
    {
        get
        {
            DateTime local = DateTimeOffset.FromUnixTimeSeconds(GameClock.NowUtc).ToLocalTime().DateTime.Date;
            return (int)(local - new DateTime(1970, 1, 1)).TotalDays;
        }
    }

    /// <summary>Tiempo hasta la medianoche local (cuando se renuevan premio y misiones).</summary>
    public static TimeSpan UntilTomorrow
    {
        get
        {
            DateTime now = DateTimeOffset.FromUnixTimeSeconds(GameClock.NowUtc).ToLocalTime().DateTime;
            return now.Date.AddDays(1) - now;
        }
    }

    public static string UntilTomorrowText
    {
        get
        {
            TimeSpan t = UntilTomorrow;
            return $"{(int)t.TotalHours}h {t.Minutes}m";
        }
    }
}

/// <summary>Popups de Recompensa diaria y Misiones diarias, y los botones del Inicio.</summary>
public static class DailyEventsPopup
{
    private static int autoShownDay = -1;

    // ------------------------------------------------------------------
    // Botones del Inicio
    // ------------------------------------------------------------------

    /// <summary>Fila con los botones DIARIO y MISIONES (con aviso rojo si hay algo para reclamar).</summary>
    public static void BuildHomeButtons(Transform parent)
    {
        PlayerProgress p = SaveManager.Progress;
        int today = GameDays.Today;

        GameObject row = UiKit.Node(parent, "Eventos");
        UiKit.Size(row.transform, height: 130f);
        HorizontalLayoutGroup layout = UiKit.Horizontal(row, 0, 24f);
        layout.padding = new RectOffset(110, 110, 0, 0);
        layout.childForceExpandWidth = true;

        HomeButton(row.transform, "DIARIO", UiKit.ButtonStyle.Green, ShowDaily, DailyRewards.CanClaim(p, today) ? 1 : 0);
        HomeButton(row.transform, "MISIONES", UiKit.ButtonStyle.Blue, ShowMissions, DailyMissions.Claimable(p, today));
    }

    private static void HomeButton(Transform parent, string label, UiKit.ButtonStyle style, Action onClick, int pending)
    {
        Button button = UiKit.Button(parent, label, style, onClick, 50f);
        if (pending <= 0) return;

        // Aviso rojo con la cantidad, en la esquina.
        Image dot = UiKit.Image(button.transform, "Aviso", new Color(0.9f, 0.15f, 0.15f), UiKit.LoadSliced("UI/row", 24, 160f));
        dot.type = Image.Type.Sliced;
        dot.raycastTarget = false;
        RectTransform rect = dot.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-14f, -14f);
        rect.sizeDelta = new Vector2(64f, 64f);
        TMP_Text count = UiKit.Text(dot.transform, pending.ToString(), 40f, Color.white);
        UiKit.Outline(count, 0.2f);
        UiKit.Stretch(count.rectTransform);
    }

    /// <summary>Al entrar al Inicio, la primera vez del día, se abre solo el premio diario.</summary>
    public static void AutoShowIfAvailable()
    {
        int today = GameDays.Today;
        if (autoShownDay == today || !DailyRewards.CanClaim(SaveManager.Progress, today)) return;
        autoShownDay = today;
        ShowDaily();
    }

    // ------------------------------------------------------------------
    // Recompensa diaria
    // ------------------------------------------------------------------

    public static void ShowDaily()
    {
        PlayerProgress p = SaveManager.Progress;
        int today = GameDays.Today;
        bool canClaim = DailyRewards.CanClaim(p, today);
        int claimed = DailyRewards.ClaimedInCycle(p, today);
        int todayIndex = canClaim ? DailyRewards.NextIndex(p, today) : claimed - 1;

        StoryPopup.ShowCustom("Recompensa diaria", panel =>
        {
            TMP_Text info = UiKit.Text(panel, canClaim
                ? "¡Entra cada día para premios mejores!\n<size=80%>Si te saltas un día, vuelves al día 1.</size>"
                : $"Vuelve mañana por el siguiente premio\n<size=80%>en {GameDays.UntilTomorrowText}</size>", 36f, Color.white);
            UiKit.Outline(info, 0.15f);
            UiKit.Size(info, height: 100f);

            const float cellW = 200f, cellH = 250f, gap = 14f;
            GameObject gridGO = UiKit.Node(panel, "Dias");
            UiKit.Size(gridGO.transform, height: cellH * 2 + gap);
            var grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(gap, gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < DailyRewards.Cycle.Length; i++)
            {
                DayCell(gridGO.transform, i, DailyRewards.Cycle[i], i < claimed, i == todayIndex && canClaim);
            }
        },
        new StoryPopup.PopupButton(canClaim ? "¡Reclamar!" : "Vuelve mañana", ClaimDaily, canClaim),
        new StoryPopup.PopupButton("Cerrar", MenuScreens.Refresh));
    }

    private static void DayCell(Transform parent, int index, EventReward reward, bool done, bool isToday)
    {
        Color background = done ? new Color(0.12f, 0.35f, 0.16f, 0.95f)
            : isToday ? new Color(0.35f, 0.26f, 0.08f, 0.97f)
            : new Color(0.13f, 0.12f, 0.27f, 0.95f);
        Image cell = UiKit.Image(parent, "Dia " + (index + 1), background, UiKit.LoadSliced("UI/row", 24, 160f));
        cell.type = Image.Type.Sliced;
        if (isToday || index == DailyRewards.Cycle.Length - 1)
        {
            var border = cell.gameObject.AddComponent<Outline>();
            border.effectColor = UiKit.Gold;
            border.effectDistance = new Vector2(4f, -4f);
        }
        UiKit.Vertical(cell.gameObject, 10, 4f, TextAnchor.MiddleCenter);

        TMP_Text day = UiKit.Text(cell.transform, $"Día {index + 1}", 32f, isToday ? UiKit.Gold : Color.white);
        UiKit.Outline(day, 0.18f);
        UiKit.Size(day, height: 42f);

        Image icon = UiKit.Image(cell.transform, "Icono", Color.white, RewardIcon(reward));
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        UiKit.Size(icon, height: 100f);

        TMP_Text amount = UiKit.Text(cell.transform, done ? "LISTO" : RewardShortText(reward), 28f, done ? new Color(0.5f, 1f, 0.55f) : Color.white);
        UiKit.Outline(amount, 0.18f);
        UiKit.Size(amount, height: 60f);
    }

    private static void ClaimDaily()
    {
        PlayerProgress p = SaveManager.Progress;
        int index = DailyRewards.Claim(p, GameDays.Today);
        if (index < 0) return;
        SaveManager.Save();
        Sound.Play("coin");
        EventReward reward = DailyRewards.Cycle[index];
        StoryPopup.Show($"¡Día {index + 1}!", "Ganaste:\n<b>" + RewardLongText(reward) + "</b>",
            new StoryPopup.PopupButton("Genial", MenuScreens.Refresh));
    }

    // ------------------------------------------------------------------
    // Misiones diarias
    // ------------------------------------------------------------------

    public static void ShowMissions()
    {
        PlayerProgress p = SaveManager.Progress;
        DailyMissions.EnsureToday(p, GameDays.Today);

        StoryPopup.ShowCustom("Misiones diarias", panel =>
        {
            TMP_Text info = UiKit.Text(panel, $"Se renuevan en <b>{GameDays.UntilTomorrowText}</b>", 32f, UiKit.Muted);
            UiKit.Outline(info, 0.15f);
            UiKit.Size(info, height: 50f);

            for (int i = 0; i < DailyMissions.Count; i++) MissionRow(panel, p, i);
            BonusRow(panel, p);
        },
        new StoryPopup.PopupButton("Cerrar", MenuScreens.Refresh));
    }

    private static void MissionRow(Transform parent, PlayerProgress p, int slot)
    {
        MissionDef def = DailyMissions.Get(p, slot);
        int progress = p.missionProgress[slot];
        bool claimed = p.missionClaimed[slot];

        Image row = UiKit.Image(parent, "Mision " + slot, claimed ? new Color(0.12f, 0.3f, 0.15f, 0.95f) : new Color(0.13f, 0.12f, 0.27f, 0.95f),
            UiKit.LoadSliced("UI/row", 24, 160f));
        row.type = Image.Type.Sliced;
        UiKit.Size(row, height: 190f);
        HorizontalLayoutGroup layout = UiKit.Horizontal(row.gameObject, 20, 16f);
        layout.childForceExpandHeight = false;

        GameObject texts = UiKit.Node(row.transform, "Textos");
        UiKit.Size(texts.transform, flexibleWidth: 1f, height: 160f);
        UiKit.Vertical(texts, 0, 6f, TextAnchor.MiddleLeft);
        TMP_Text title = UiKit.Text(texts.transform, Describe(def), 36f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(title, 0.18f);
        UiKit.Size(title, height: 48f);
        RectTransform fill = UiKit.ProgressBar(texts.transform, 40f, out TMP_Text barText);
        fill.anchorMax = new Vector2(Mathf.Clamp01(progress / (float)def.Target), 1f);
        barText.text = $"{progress} / {def.Target}";
        TMP_Text reward = UiKit.Text(texts.transform, "Premio: " + RewardLongText(def.Reward), 28f, UiKit.Gold, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(reward, 0.15f);
        UiKit.Size(reward, height: 38f);

        Button action;
        if (claimed)
        {
            action = UiKit.Button(row.transform, "LISTO", UiKit.ButtonStyle.Gray, null, 30f);
            action.interactable = false;
        }
        else if (DailyMissions.IsComplete(p, slot))
        {
            action = UiKit.Button(row.transform, "RECLAMAR", UiKit.ButtonStyle.Green, () => ClaimMission(slot), 30f);
        }
        else
        {
            action = UiKit.Button(row.transform, "EN CURSO", UiKit.ButtonStyle.Gray, null, 28f);
            action.interactable = false;
        }
        UiKit.Size(action, width: 230f, height: 100f);
    }

    private static void BonusRow(Transform parent, PlayerProgress p)
    {
        bool ready = DailyMissions.BonusReady(p);
        bool done = p.missionBonusClaimed;

        Image row = UiKit.Image(parent, "Bonus", new Color(0.3f, 0.2f, 0.45f, 0.95f), UiKit.LoadSliced("UI/row", 24, 160f));
        row.type = Image.Type.Sliced;
        UiKit.Size(row, height: 130f);
        HorizontalLayoutGroup layout = UiKit.Horizontal(row.gameObject, 20, 16f);
        layout.childForceExpandHeight = false;

        TMP_Text text = UiKit.Text(row.transform, $"<b>Bonus:</b> completa las 3 misiones\n<color=#C080FF>+{DailyMissions.Bonus.Gems} gemas</color>",
            32f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(text, 0.15f);
        UiKit.Size(text, flexibleWidth: 1f, height: 100f);

        Button action = done
            ? UiKit.Button(row.transform, "LISTO", UiKit.ButtonStyle.Gray, null, 30f)
            : UiKit.Button(row.transform, "RECLAMAR", ready ? UiKit.ButtonStyle.Purple : UiKit.ButtonStyle.Gray, ClaimBonus, 30f);
        action.interactable = ready;
        UiKit.Size(action, width: 230f, height: 96f);
    }

    private static void ClaimMission(int slot)
    {
        PlayerProgress p = SaveManager.Progress;
        if (!DailyMissions.Claim(p, slot)) return;
        SaveManager.Save();
        Sound.Play("coin");
        ShowMissions();
    }

    private static void ClaimBonus()
    {
        PlayerProgress p = SaveManager.Progress;
        if (!DailyMissions.ClaimBonus(p)) return;
        SaveManager.Save();
        Sound.Play("star");
        StoryPopup.Show("¡Misiones completas!", $"Ganaste <b>+{DailyMissions.Bonus.Gems} gemas</b>.\nMañana habrá misiones nuevas.",
            new StoryPopup.PopupButton("Genial", ShowMissions));
    }

    // ------------------------------------------------------------------
    // Textos e íconos
    // ------------------------------------------------------------------

    public static string Describe(MissionDef def)
    {
        switch (def.Type)
        {
            case MissionType.PassLevels: return $"Pasa {def.Target} niveles";
            case MissionType.EarnStars: return $"Consigue {def.Target} estrellas";
            case MissionType.CreateSpecials: return $"Crea {def.Target} especiales";
            case MissionType.UseBoosters: return $"Usa {def.Target} potenciadores";
            case MissionType.PlayRanked: return $"Juega {def.Target} partidas de clasificatoria";
            case MissionType.WinRanked: return def.Target == 1 ? "Gana 1 partida de clasificatoria" : $"Gana {def.Target} partidas de clasificatoria";
            case MissionType.ClearMinerals: return $"Rompe {def.Target} minerales";
            default: return "Misión";
        }
    }

    private static Sprite RewardIcon(EventReward r)
    {
        if (r.BoosterCount > 0)
        {
            string[] icons = { "UI/booster_hammer", "UI/booster_bomb", "UI/booster_lightning", "UI/booster_shuffle" };
            return UiKit.LoadSprite(icons[Mathf.Clamp((int)r.Booster, 0, icons.Length - 1)]);
        }
        return r.Gems > 0 ? UiKit.LoadSprite("Minerals/amethyst") : UiKit.LoadSprite("UI/coin");
    }

    private static string RewardShortText(EventReward r)
    {
        if (r.BoosterCount > 0) return $"x{r.BoosterCount}";
        if (r.Gems > 0 && r.Coins > 0) return $"{r.Gems} gemas\n+{r.Coins}";
        return r.Gems > 0 ? $"{r.Gems} gemas" : $"{r.Coins}";
    }

    private static string RewardLongText(EventReward r)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (r.Coins > 0) parts.Add($"{r.Coins} monedas");
        if (r.Gems > 0) parts.Add($"{r.Gems} gemas");
        if (r.BoosterCount > 0) parts.Add($"{r.BoosterCount} {GameManager.BoosterName(r.Booster)}");
        return string.Join(" + ", parts);
    }
}
