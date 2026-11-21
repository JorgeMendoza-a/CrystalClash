using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tienda: fotos de perfil y banners (con gemas o monedas) y potenciadores (con monedas).
/// Las vidas no se compran: se recuperan con el tiempo (o viendo anuncios).
/// </summary>
public static class ShopScreen
{
    private static readonly Color CardColor = new Color(0.13f, 0.11f, 0.24f, 0.97f);

    public static void Build(Transform root)
    {
        Transform list = UiKit.ScrollArea(root, 18f);

        UiKit.Size(UiKit.Title(list, "TIENDA", 100f), height: 130f);
        TMP_Text subtitle = UiKit.Text(list, "Personaliza tu perfil: tu rival lo verá en la pantalla de VS.", 32f, Color.white);
        UiKit.Outline(subtitle, 0.15f);
        UiKit.Size(subtitle, height: 46f);

        // Vidas gratis viendo anuncios
        int lives = SaveManager.Progress.GetLives(GameClock.NowUtc);
        string livesInfo = lives >= PlayerProgress.MaxLives
            ? "Tus vidas están llenas"
            : $"Mira un anuncio y gana 1 vida · quedan {LifeAds.Remaining} de {LifeAds.AdsPerWindow}";
        FreeLifeItem(list, livesInfo, lives < PlayerProgress.MaxLives && LifeAds.Remaining > 0);

        Section(list, "FOTOS DE PERFIL");
        BuildAvatarGrid(list);

        Section(list, "BANNERS");
        if (!string.IsNullOrEmpty(SaveManager.Progress.banner))
        {
            UiKit.Size(UiKit.Button(list, "Quitar banner", UiKit.ButtonStyle.Gray, Cosmetics.RemoveBanner, 36f), height: 100f);
        }
        foreach (Cosmetics.Item banner in Cosmetics.Banners) BannerCard(list, banner);

        Section(list, "POTENCIADORES");
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
    }

    private static void FreeLifeItem(Transform parent, string description, bool available)
    {
        Image card = UiKit.Card(parent, "Vidas gratis");
        UiKit.Size(card, height: 200f);
        HorizontalLayoutGroup row = UiKit.Horizontal(card.gameObject, 34, 18f);
        row.childForceExpandWidth = false;

        UiKit.Size(UiKit.Image(card.transform, "Icono", Color.white, UiKit.LoadSprite("UI/heart")), width: 130f);

        GameObject texts = UiKit.Node(card.transform, "Textos");
        UiKit.Vertical(texts, 0, 0f, TextAnchor.MiddleLeft);
        UiKit.Size(texts.transform, flexibleWidth: 1f);
        TMP_Text name = UiKit.Text(texts.transform, "Vida gratis", 50f, UiKit.Gold, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(name, 0.2f);
        UiKit.Size(name, height: 62f);
        TMP_Text desc = UiKit.Text(texts.transform, description, 30f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(desc, 0.12f);
        UiKit.Size(desc, height: 42f);

        Button watch = UiKit.Button(card.transform, "VER", UiKit.ButtonStyle.Green, () => LifeAds.WatchForLife(MenuScreens.Refresh), 46f);
        watch.interactable = available;
        UiKit.Size(watch, width: 250f);
    }

    private static void Section(Transform parent, string title)
    {
        TMP_Text text = UiKit.Title(parent, title, 60f);
        UiKit.Size(text, height: 90f);
    }

    // ------------------------------------------------------------------
    // Fotos de perfil
    // ------------------------------------------------------------------

    private static void BuildAvatarGrid(Transform parent)
    {
        const int columns = 3;
        var cell = new Vector2(320f, 500f);
        const float spacing = 18f;
        int rows = (Cosmetics.Avatars.Length + columns - 1) / columns;

        GameObject gridGO = UiKit.Node(parent, "Avatares");
        UiKit.Size(gridGO.transform, height: rows * cell.y + (rows - 1) * spacing);
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = cell;
        grid.spacing = new Vector2(spacing, spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;

        foreach (Cosmetics.Item item in Cosmetics.Avatars) AvatarCard(gridGO.transform, item);
    }

    private static void AvatarCard(Transform parent, Cosmetics.Item item)
    {
        Image card = CardBackground(parent, "Avatar " + item.Key, item.Rarity);
        UiKit.Vertical(card.gameObject, 16, 4f, TextAnchor.UpperCenter);

        Image image = UiKit.Image(card.transform, "Imagen", Color.white, Cosmetics.AvatarSprite(item.Id));
        image.preserveAspect = true;
        image.raycastTarget = false;
        UiKit.Size(image, height: 250f);

        TMP_Text name = UiKit.Text(card.transform, item.Name, 32f, Color.white);
        UiKit.Outline(name, 0.18f);
        UiKit.Size(name, height: 74f);

        TMP_Text rarity = UiKit.Text(card.transform, Cosmetics.RarityName(item.Rarity), 26f, Cosmetics.RarityColor(item.Rarity));
        UiKit.Outline(rarity, 0.15f);
        UiKit.Size(rarity, height: 34f);

        ActionButton(card.transform, item, 96f, -1f);
    }

    // ------------------------------------------------------------------
    // Banners
    // ------------------------------------------------------------------

    private static void BannerCard(Transform parent, Cosmetics.Item item)
    {
        Image card = CardBackground(parent, "Banner " + item.Key, item.Rarity);
        UiKit.Size(card, height: 480f);
        UiKit.Vertical(card.gameObject, 18, 10f, TextAnchor.UpperCenter);

        Image image = UiKit.Image(card.transform, "Imagen", Color.white, Cosmetics.BannerSprite(item.Id));
        image.preserveAspect = true;
        image.raycastTarget = false;
        UiKit.Size(image, height: 320f);

        GameObject row = UiKit.Node(card.transform, "Fila");
        UiKit.Size(row.transform, height: 100f);
        HorizontalLayoutGroup layout = UiKit.Horizontal(row, 0, 16f);
        layout.childForceExpandHeight = false;

        string color = ColorUtility.ToHtmlStringRGB(Cosmetics.RarityColor(item.Rarity));
        TMP_Text name = UiKit.Text(row.transform, $"<color=#{color}>{item.Name}  ·  {Cosmetics.RarityName(item.Rarity)}</color>", 36f,
            Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(name, 0.18f);
        UiKit.Size(name, flexibleWidth: 1f, height: 90f);

        ActionButton(row.transform, item, 96f, 300f);
    }

    // ------------------------------------------------------------------
    // Ayudas
    // ------------------------------------------------------------------

    /// <summary>Fondo oscuro con borde del color de la rareza.</summary>
    private static Image CardBackground(Transform parent, string name, Cosmetics.Rarity rarity)
    {
        Image card = UiKit.Image(parent, name, CardColor, UiKit.LoadSliced("UI/row", 24, 160f));
        card.type = Image.Type.Sliced;
        card.preserveAspect = false;
        var border = card.gameObject.AddComponent<Outline>();
        border.effectColor = Cosmetics.RarityColor(rarity);
        border.effectDistance = new Vector2(4f, -4f);
        return card;
    }

    /// <summary>Precio (si no lo tienes), EQUIPAR o EQUIPADO.</summary>
    private static void ActionButton(Transform parent, Cosmetics.Item item, float height, float width)
    {
        Button button;
        if (!SaveManager.Progress.OwnsCosmetic(item.Id))
        {
            Sprite currency = item.Currency == Cosmetics.Currency.Gems ? UiKit.LoadSprite("Minerals/amethyst") : UiKit.LoadSprite("UI/coin");
            button = PriceButton(parent, item.Price.ToString(), currency, UiKit.ButtonStyle.Green, () => Cosmetics.Buy(item, MenuScreens.Refresh), 40f);
        }
        else if (Cosmetics.IsEquipped(item))
        {
            button = UiKit.Button(parent, "EQUIPADO", UiKit.ButtonStyle.Gray, null, 34f);
            button.interactable = false;
        }
        else
        {
            button = UiKit.Button(parent, "EQUIPAR", UiKit.ButtonStyle.Blue, () => Cosmetics.Equip(item), 36f);
        }

        if (width > 0f) UiKit.Size(button, width: width, height: height);
        else UiKit.Size(button, height: height);
    }

    /// <summary>Botón con el ícono de la moneda/gema a la izquierda y el precio.</summary>
    private static Button PriceButton(Transform parent, string price, Sprite currency, UiKit.ButtonStyle style, System.Action onClick, float fontSize)
    {
        Button buy = UiKit.Button(parent, price, style, onClick, fontSize);
        TMP_Text label = buy.GetComponentInChildren<TMP_Text>();
        label.rectTransform.offsetMin = new Vector2(80f, 22f);
        Image icon = UiKit.Image(buy.transform, "Moneda", Color.white, currency);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(55f, 4f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        return buy;
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
}
