using System;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Hora actual en segundos UTC (vidas, temporadas).</summary>
public static class GameClock
{
    public static long NowUtc => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

/// <summary>
/// Barra superior de los menús: vidas (con el tiempo para la siguiente),
/// monedas, gemas y botón de ajustes. Se refresca sola cada segundo.
/// </summary>
public class TopBarView : MonoBehaviour
{
    public const float Height = 130f;

    private TMP_Text livesText;
    private TMP_Text livesTimer;
    private TMP_Text coinsText;
    private TMP_Text gemsText;
    private float nextRefresh;

    public static TopBarView Create(Transform parent)
    {
        GameObject bar = UiKit.Node(parent, "BarraSuperior");
        var rect = (RectTransform)bar.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -14f);
        rect.sizeDelta = new Vector2(-30f, Height);

        HorizontalLayoutGroup row = UiKit.Horizontal(bar, 0, 12f);
        row.childForceExpandWidth = false;

        var view = bar.AddComponent<TopBarView>();
        view.Build(bar.transform);
        view.Refresh();
        return view;
    }

    private void Build(Transform bar)
    {
        Transform lives = Pill(bar, UiKit.LoadSprite("UI/heart"), 1.1f, out livesText);

        // Tocar las vidas: info y anuncio para ganar una.
        Image livesImage = lives.GetComponent<Image>();
        var livesButton = lives.gameObject.AddComponent<Button>();
        Sound.AddClick(livesButton);
        livesButton.targetGraphic = livesImage;
        livesButton.onClick.AddListener(ShowLivesPopup);
        livesTimer = UiKit.Text(lives, "", 26f, Color.white);
        UiKit.Outline(livesTimer, 0.15f);
        UiKit.Size(livesTimer, width: 100f);

        Pill(bar, UiKit.LoadSprite("UI/coin"), 1.2f, out coinsText);
        Pill(bar, UiKit.LoadSprite("Minerals/amethyst"), 1f, out gemsText);

        Button settings = UiKit.Button(bar, "", UiKit.ButtonStyle.Gray, MenuScreens.ShowSettings);
        UiKit.Size(settings, width: Height);
        Image gear = UiKit.Image(settings.transform, "Engranaje", Color.white, UiKit.LoadSprite("UI/gear"));
        gear.raycastTarget = false;
        UiKit.Stretch(gear.rectTransform, 26f);
    }

    private static void ShowLivesPopup()
    {
        PlayerProgress p = SaveManager.Progress;
        long now = GameClock.NowUtc;
        int lives = p.GetLives(now);
        if (lives >= PlayerProgress.MaxLives)
        {
            StoryPopup.Show("Vidas", $"Tienes todas tus vidas (<b>{lives}</b>).", new StoryPopup.PopupButton("Genial", null));
            return;
        }

        int seconds = p.SecondsToNextLife(now);
        StoryPopup.Show("Vidas",
            $"Tienes <b>{lives}</b> de {PlayerProgress.MaxLives} vidas.\nPróxima vida en <b>{seconds / 60}:{seconds % 60:00}</b>\n\n" +
            $"<size=80%>Anuncios disponibles: {LifeAds.Remaining} de {LifeAds.AdsPerWindow}</size>",
            new StoryPopup.PopupButton(LifeAds.ButtonLabel, () => LifeAds.WatchForLife(MenuScreens.Refresh), LifeAds.Remaining > 0),
            new StoryPopup.PopupButton("Cerrar", null));
    }

    private static Transform Pill(Transform parent, Sprite icon, float flexible, out TMP_Text value)
    {
        Image pill = UiKit.Card(parent, "Pastilla");
        UiKit.Size(pill, flexibleWidth: flexible);
        HorizontalLayoutGroup row = UiKit.Horizontal(pill.gameObject, 22, 8f);
        row.childForceExpandWidth = false;
        row.padding = new RectOffset(26, 22, 20, 20);

        UiKit.Size(UiKit.Image(pill.transform, "Icono", Color.white, icon), width: 80f);
        value = UiKit.Text(pill.transform, "0", 46f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(value, 0.2f);
        UiKit.Size(value, flexibleWidth: 1f);
        return pill.transform;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        Refresh();
    }

    public void Refresh()
    {
        PlayerProgress p = SaveManager.Progress;
        long now = GameClock.NowUtc;
        int lives = p.GetLives(now);
        livesText.text = lives.ToString();

        int seconds = p.SecondsToNextLife(now);
        livesTimer.text = lives >= PlayerProgress.MaxLives ? "LLENO" : $"{seconds / 60}:{seconds % 60:00}";
        coinsText.text = p.coins.ToString();
        gemsText.text = p.gems.ToString();
    }
}
