using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tienda: potenciadores con monedas y relleno de vidas con gemas.</summary>
public static class ShopScreen
{
    public static void Build(Transform root)
    {
        Transform list = UiKit.ScrollArea(root, 16f);

        UiKit.Size(UiKit.Title(list, "TIENDA", 100f), height: 130f);
        TMP_Text subtitle = UiKit.Text(list, "Gana monedas pasando niveles y gemas en la clasificatoria.", 32f, Color.white);
        UiKit.Outline(subtitle, 0.15f);
        UiKit.Size(subtitle, height: 46f);

        string[] icons = { "UI/booster_hammer", "UI/booster_bomb", "UI/booster_lightning", "UI/booster_shuffle" };
        string[] descriptions =
        {
            "Rompe un mineral",
            "Explota un área de 3x3",
            "Rompe una fila y una columna",
            "Reorganiza el tablero"
        };

        for (int i = 0; i < icons.Length; i++)
        {
            var kind = (BoosterKind)i;
            int owned = SaveManager.Progress.GetBoosters(kind);
            Item(list, UiKit.LoadSprite(icons[i]), GameManager.BoosterName(kind), $"{descriptions[i]} · tienes {owned}",
                $"{PlayerProgress.BoosterPrice}", UiKit.LoadSprite("UI/coin"), UiKit.ButtonStyle.Green, () => BuyBooster(kind));
        }

        Item(list, UiKit.LoadSprite("UI/heart"), "Llenar vidas", $"Recupera las {PlayerProgress.MaxLives} vidas al instante",
            $"{PlayerProgress.RefillLivesPrice}", UiKit.LoadSprite("Minerals/amethyst"), UiKit.ButtonStyle.Purple, BuyLives);
    }

    private static void Item(Transform parent, Sprite icon, string title, string description, string price, Sprite currency,
        UiKit.ButtonStyle style, System.Action onBuy)
    {
        Image card = UiKit.Card(parent, "Producto " + title);
        UiKit.Size(card, height: 200f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 34, 18f);
        row.childForceExpandWidth = false;

        UiKit.Size(UiKit.Image(card.transform, "Icono", Color.white, icon), width: 130f);

        GameObject texts = UiKit.Node(card.transform, "Textos");
        UiKit.Vertical(texts, 0, 0f, TextAnchor.MiddleLeft);
        UiKit.Size(texts.transform, flexibleWidth: 1f);
        TMP_Text name = UiKit.Text(texts.transform, title, 50f, UiKit.Gold, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(name, 0.2f);
        UiKit.Size(name, height: 62f);
        TMP_Text desc = UiKit.Text(texts.transform, description, 30f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(desc, 0.12f);
        UiKit.Size(desc, height: 42f);

        Button buy = UiKit.Button(card.transform, price, style, onBuy, 46f);
        UiKit.Size(buy, width: 250f);
        TMP_Text label = buy.GetComponentInChildren<TMP_Text>();
        label.rectTransform.offsetMin = new Vector2(90f, 22f);
        Image coin = UiKit.Image(buy.transform, "Moneda", Color.white, currency);
        coin.raycastTarget = false;
        RectTransform coinRect = coin.rectTransform;
        coinRect.anchorMin = coinRect.anchorMax = new Vector2(0f, 0.5f);
        coinRect.anchoredPosition = new Vector2(65f, 4f);
        coinRect.sizeDelta = new Vector2(70f, 70f);
    }

    private static void BuyBooster(BoosterKind kind)
    {
        if (!SaveManager.Progress.TrySpendCoins(PlayerProgress.BoosterPrice))
        {
            StoryPopup.Show("Faltan monedas", "Pasa niveles para ganar más monedas.", new StoryPopup.PopupButton("Entendido", null));
            return;
        }
        SaveManager.Progress.AddBooster(kind, 1);
        SaveManager.Save();
        MenuScreens.Refresh();
    }

    private static void BuyLives()
    {
        PlayerProgress p = SaveManager.Progress;
        if (p.GetLives(GameClock.NowUtc) >= PlayerProgress.MaxLives)
        {
            StoryPopup.Show("Vidas llenas", "Ya tienes todas tus vidas.", new StoryPopup.PopupButton("Genial", null));
            return;
        }
        if (!p.TrySpendGems(PlayerProgress.RefillLivesPrice))
        {
            StoryPopup.Show("Faltan gemas", "Gana partidas de clasificatoria para conseguir gemas.", new StoryPopup.PopupButton("Entendido", null));
            return;
        }
        p.RefillLives(GameClock.NowUtc);
        SaveManager.Save();
        MenuScreens.Refresh();
    }
}
