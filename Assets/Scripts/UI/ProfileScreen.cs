using System;
using CrystalClash.Board.Logic;
using CrystalClash.Ranked.Logic;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Perfil: estadísticas del modo historia y de la clasificatoria.</summary>
public static class ProfileScreen
{
    public static void Build(Transform root)
    {
        Transform list = UiKit.ScrollArea(root, 16f);
        PlayerProgress p = SaveManager.Progress;
        int levels = LevelCatalog.Count;

        GameObject header = UiKit.Node(list, "Encabezado");
        UiKit.Horizontal(header, 0, 20f).childForceExpandWidth = false;
        UiKit.Size(header.transform, height: 200f);
        UiKit.Size(UiKit.Image(header.transform, "Avatar", Color.white, UiKit.LoadSprite("UI/nav_profile")), width: 200f);
        TMP_Text name = UiKit.Title(header.transform, "Minero", 80f);
        UiKit.Size(name, flexibleWidth: 1f);

        int completed = 0;
        for (int n = 1; n <= levels; n++) if (p.GetStars(n) > 0) completed++;

        Section(list, "MODO HISTORIA", new string[]
        {
            "Niveles superados", $"{completed} / {levels}",
            "Estrellas", $"{p.TotalStars} / {levels * 3}",
            "Monedas", p.coins.ToString(),
            "Gemas", p.gems.ToString(),
        });

        Section(list, "POTENCIADORES", new string[]
        {
            GameManager.BoosterName(BoosterKind.Hammer), p.GetBoosters(BoosterKind.Hammer).ToString(),
            GameManager.BoosterName(BoosterKind.Bomb), p.GetBoosters(BoosterKind.Bomb).ToString(),
            GameManager.BoosterName(BoosterKind.Lightning), p.GetBoosters(BoosterKind.Lightning).ToString(),
            GameManager.BoosterName(BoosterKind.Shuffle), p.GetBoosters(BoosterKind.Shuffle).ToString(),
        });

        TMP_Text[] ranked = Section(list, "CLASIFICATORIA", new string[]
        {
            "Rango", "...",
            "Puntos", "...",
            "Victorias / Derrotas", "...",
        });

        LoadRanked(name, ranked);
    }

    private static async void LoadRanked(TMP_Text name, TMP_Text[] values)
    {
        try
        {
            IRankedService service = RankedServices.Current;
            await service.SignInAsync();
            RankedProfile profile = await service.GetProfileAsync();
            if (name == null) return; // la pantalla ya se cerró

            name.text = profile.Name;
            RankTier tier = profile.Tier;
            values[0].text = $"<color=#{ColorUtility.ToHtmlStringRGB(UiKit.LeagueColor(tier.LeagueIndex))}>{tier.Name}</color>";
            values[1].text = profile.Points.ToString();
            values[2].text = $"{profile.Wins} / {profile.Losses}";
        }
        catch (Exception e)
        {
            Debug.LogWarning("Perfil: " + e.Message);
            if (values[0] != null) values[0].text = "Sin conexión";
        }
    }

    private static TMP_Text[] Section(Transform parent, string title, string[] pairs)
    {
        // pairs = { etiqueta1, valor1, etiqueta2, valor2, ... }
        int count = pairs.Length / 2;
        Image card = UiKit.Card(parent, title);
        UiKit.Size(card, height: 110f + count * 70f);
        UiKit.Vertical(card.gameObject, 34, 4f);

        TMP_Text header = UiKit.Text(card.transform, title, 42f, UiKit.Gold);
        UiKit.Outline(header, 0.18f);
        UiKit.Size(header, height: 56f);

        var values = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            string labelText = pairs[i * 2];
            string valueText = pairs[i * 2 + 1];
            Image row = UiKit.Row(card.transform, labelText);
            UiKit.Size(row, height: 64f);
            UiKit.Horizontal(row.gameObject, 24, 10f).childForceExpandWidth = true;
            TMP_Text label = UiKit.Text(row.transform, labelText, 34f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UiKit.Outline(label, 0.12f);
            values[i] = UiKit.Text(row.transform, valueText, 36f, UiKit.Gold, FontStyles.Normal, TextAlignmentOptions.Right);
            UiKit.Outline(values[i], 0.15f);
        }
        return values;
    }
}
