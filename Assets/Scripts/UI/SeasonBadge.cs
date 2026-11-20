using System.Collections.Generic;
using CrystalClash.Ranked.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Insignia exclusiva de temporada: emblema de la liga + brillo del color de
/// esa temporada + cinta con su número. Así el Campeón de la T1 y el de la T2
/// se parecen, pero no son iguales.
/// Arte especial opcional: Resources/UI/Seasons/s{temporada}/{liga}.png
/// (p. ej. Seasons/s2/campeon.png) reemplaza al emblema armado.
/// </summary>
public static class SeasonBadge
{
    private static readonly string[] LeagueFiles = { "bronce", "plata", "oro", "platino", "diamante", "maestro", "campeon" };

    // Un color por temporada (se repite cada 6 temporadas, con el número distinto en la cinta)
    private static readonly Color[] SeasonColors =
    {
        new Color(1f, 0.78f, 0.25f),   // T1 dorado
        new Color(0.3f, 0.9f, 0.85f),  // T2 turquesa
        new Color(1f, 0.38f, 0.38f),   // T3 carmesí
        new Color(0.72f, 0.45f, 1f),   // T4 violeta
        new Color(0.4f, 0.95f, 0.45f), // T5 esmeralda
        new Color(0.5f, 0.78f, 1f),    // T6 hielo
    };

    public static Color ColorFor(int season)
    {
        return SeasonColors[(Mathf.Max(1, season) - 1) % SeasonColors.Length];
    }

    public static string LeagueName(int leagueIndex)
    {
        int i = Mathf.Clamp(leagueIndex, 0, RankTiers.Leagues.Count - 1);
        return RankTiers.Leagues[i].Name;
    }

    /// <summary>Crea la insignia dentro de parent (cuadrada, del tamaño indicado).</summary>
    public static RectTransform Create(Transform parent, int season, int leagueIndex, float size)
    {
        GameObject root = UiKit.Node(parent, $"Insignia s{season}_{leagueIndex}");
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(size, size);
        root.AddComponent<LayoutElement>().preferredHeight = size;

        Color color = ColorFor(season);
        int league = Mathf.Clamp(leagueIndex, 0, LeagueFiles.Length - 1);

        Sprite custom = UiKit.LoadSprite($"UI/Seasons/s{season}/{LeagueFiles[league]}");

        Image glow = UiKit.Image(root.transform, "Brillo", new Color(color.r, color.g, color.b, 0.85f), UiKit.LoadSprite("UI/season_glow"));
        glow.raycastTarget = false;
        Place(glow.rectTransform, Vector2.zero, size * 1.15f);

        Image emblem = UiKit.Image(root.transform, "Emblema", Color.white, custom != null ? custom : UiKit.RankEmblem(league));
        emblem.raycastTarget = false;
        Place(emblem.rectTransform, new Vector2(0f, size * 0.04f), size * 0.86f);

        if (custom == null)
        {
            // Cinta con el número de temporada (tapa el número romano del emblema).
            Image ribbon = UiKit.Image(root.transform, "Cinta", color, UiKit.LoadSliced("UI/season_ribbon", 60, 100f * 320f / Mathf.Max(60f, size)));
            ribbon.type = Image.Type.Sliced;
            ribbon.preserveAspect = false;
            ribbon.raycastTarget = false;
            RectTransform r = ribbon.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, size * 0.02f);
            r.sizeDelta = new Vector2(size * 0.78f, size * 0.3f);

            TMP_Text label = UiKit.Text(ribbon.transform, "T" + season, size * 0.17f, Color.white);
            UiKit.Outline(label, 0.25f);
            UiKit.Stretch(label.rectTransform, 2f);
            label.rectTransform.offsetMax = new Vector2(-size * 0.1f, -size * 0.06f);
            label.rectTransform.offsetMin = new Vector2(size * 0.1f, 0f);
        }

        return rect;
    }

    private static void Place(RectTransform rect, Vector2 position, float size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
    }
}

/// <summary>Recoge las recompensas de temporada del servidor, suma monedas/gemas y las muestra.</summary>
public static class SeasonRewards
{
    private static bool claiming;

    public static async void ClaimAndShow()
    {
        if (claiming) return;
        claiming = true;
        try
        {
            List<SeasonReward> rewards = await RankedServices.Current.ClaimRewardsAsync();
            if (rewards == null || rewards.Count == 0) return;

            foreach (SeasonReward reward in rewards)
            {
                SaveManager.Progress.coins += reward.Coins;
                SaveManager.Progress.gems += reward.Gems;
            }
            SaveManager.Save();
            ShowNext(rewards, 0);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Recompensas de temporada: " + e.Message);
        }
        finally
        {
            claiming = false;
        }
    }

    private static void ShowNext(List<SeasonReward> rewards, int index)
    {
        if (index >= rewards.Count)
        {
            MenuScreens.Refresh(); // para que se vean las monedas, gemas e insignias nuevas
            return;
        }

        SeasonReward r = rewards[index];
        string prizes = $"<color=#FFD040>+{r.Coins} monedas</color>" + (r.Gems > 0 ? $"   <color=#C080FF>+{r.Gems} gemas</color>" : "");
        bool end = r.Kind == (int)SeasonRewardKind.SeasonEnd;
        string title = end ? $"¡Fin de la Temporada {r.Season}!" : "¡Nuevo rango!";
        string body = end
            ? $"Terminaste en <b>{r.TierName}</b>\n\n{prizes}\n\nEn la nueva temporada empiezas con <b>{r.NewSeasonPoints}</b> puntos " +
              $"({RankTiers.ForPoints(r.NewSeasonPoints).Name})."
            : $"Llegaste a <b>{r.TierName}</b>\n\n{prizes}" +
              (string.IsNullOrEmpty(r.Badge) ? "" :
                  $"\n\n¡Insignia y foto de perfil exclusivas <b>{SeasonBadge.LeagueName(r.LeagueIndex)} T{r.Season}</b>!\n<size=80%>Póntela en tu Perfil tocando el lápiz del avatar.</size>");

        bool showBadge = end || !string.IsNullOrEmpty(r.Badge);
        StoryPopup.ShowWithBadge(title, body, showBadge ? r.Season : 0, r.LeagueIndex,
            new StoryPopup.PopupButton(index + 1 < rewards.Count ? "Siguiente" : "¡Genial!", () => ShowNext(rewards, index + 1)));
    }
}
