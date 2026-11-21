using System;
using CrystalClash.Story.Logic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Artículos de la tienda para el perfil: avatares épicos y banners.
/// Las claves deben coincidir con ServerLogic.ShopAvatars / ShopBanners
/// (CloudCode/.../ServerLogic.cs). Imágenes en Resources/UI/Shop/.
/// </summary>
public static class Cosmetics
{
    public const string AvatarPrefix = "e_";
    public const string BannerPrefix = "b_";

    public enum Rarity { Raro, Epico, Legendario }
    public enum Currency { Gems, Coins }

    public class Item
    {
        public string Id;
        public string Key;
        public string Name;
        public Rarity Rarity;
        public int Price;
        public Currency Currency;
        public bool IsBanner;
    }

    public static readonly Item[] Avatars =
    {
        Avatar("golem_lava", "Gólem de Lava", Rarity.Legendario, 450),
        Avatar("rey_lich", "Rey Lich", Rarity.Legendario, 500),
        Avatar("caballero_obsidiana", "Caballero de Obsidiana", Rarity.Epico, 200),
        Avatar("demonio_mina", "Demonio de la Mina", Rarity.Legendario, 450),
        Avatar("hechicero_cristal", "Hechicero de Cristal", Rarity.Epico, 200),
        Avatar("ninja_minero", "Ninja Minero", Rarity.Epico, 180),
        Avatar("zombie_minero", "Zombi Minero", Rarity.Epico, 180),
        Avatar("golem_oro", "Gólem de Oro", Rarity.Legendario, 500),
        Avatar("yeti_hielo", "Yeti de Hielo", Rarity.Epico, 200),
        Avatar("dragon_fuego", "Dragón de Fuego", Rarity.Legendario, 500),
    };

    public static readonly Item[] Banners =
    {
        Banner("mina_cristales", "Mina de Cristales", Rarity.Epico, 250, Currency.Gems),
        Banner("volcan_ardiente", "Volcán Ardiente", Rarity.Legendario, 600, Currency.Gems),
        Banner("cueva_hielo", "Cueva de Hielo", Rarity.Epico, 250, Currency.Gems),
        Banner("tesoro_del_rey", "Tesoro del Rey", Rarity.Legendario, 650, Currency.Gems),
        Banner("entrada_mina", "Atardecer en la Mina", Rarity.Raro, 3000, Currency.Coins),
        Banner("noche_minera", "Noche Minera", Rarity.Raro, 3000, Currency.Coins),
        Banner("cripta_maldita", "Cripta Maldita", Rarity.Epico, 280, Currency.Gems),
        Banner("abismo_dragon", "Abismo del Dragón", Rarity.Legendario, 700, Currency.Gems),
    };

    private static Item Avatar(string key, string name, Rarity rarity, int gems)
    {
        return new Item { Id = AvatarPrefix + key, Key = key, Name = name, Rarity = rarity, Price = gems, Currency = Currency.Gems };
    }

    private static Item Banner(string key, string name, Rarity rarity, int price, Currency currency)
    {
        return new Item { Id = BannerPrefix + key, Key = key, Name = name, Rarity = rarity, Price = price, Currency = currency, IsBanner = true };
    }

    // ------------------------------------------------------------------

    public static bool IsShopAvatar(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(AvatarPrefix);

    public static Sprite AvatarSprite(string id)
    {
        return IsShopAvatar(id) ? UiKit.LoadSprite("UI/Shop/Avatars/avatar_" + id.Substring(AvatarPrefix.Length)) : null;
    }

    public static Sprite BannerSprite(string id)
    {
        return !string.IsNullOrEmpty(id) && id.StartsWith(BannerPrefix)
            ? UiKit.LoadSprite("UI/Shop/Banners/banner_" + id.Substring(BannerPrefix.Length))
            : null;
    }

    public static string RarityName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendario: return "LEGENDARIO";
            case Rarity.Epico: return "ÉPICO";
            default: return "RARO";
        }
    }

    public static Color RarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendario: return new Color(1f, 0.8f, 0.3f);
            case Rarity.Epico: return new Color(0.75f, 0.5f, 1f);
            default: return new Color(0.45f, 0.7f, 1f);
        }
    }

    public static bool IsEquipped(Item item)
    {
        PlayerProgress p = SaveManager.Progress;
        return item.IsBanner ? p.banner == item.Id : p.avatarBadge == item.Id;
    }

    /// <summary>Compra con confirmación; si sale bien la equipa.</summary>
    public static void Buy(Item item, Action onDone)
    {
        string currency = item.Currency == Currency.Gems ? "gemas" : "monedas";
        StoryPopup.Show("¿Comprar?",
            $"<b>{item.Name}</b>\n<color=#{ColorUtility.ToHtmlStringRGB(RarityColor(item.Rarity))}>{RarityName(item.Rarity)}</color>\n\nPrecio: <b>{item.Price} {currency}</b>",
            new StoryPopup.PopupButton("Comprar", () =>
            {
                PlayerProgress p = SaveManager.Progress;
                bool paid = item.Currency == Currency.Gems ? p.TrySpendGems(item.Price) : p.TrySpendCoins(item.Price);
                if (!paid)
                {
                    StoryPopup.Show(item.Currency == Currency.Gems ? "Faltan gemas" : "Faltan monedas",
                        item.Currency == Currency.Gems
                            ? "Consigue gemas subiendo de rango en la clasificatoria."
                            : "Pasa niveles del modo historia para ganar monedas.",
                        new StoryPopup.PopupButton("Entendido", null));
                    return;
                }

                p.AddCosmetic(item.Id);
                Sound.Play("coin");
                Equip(item, false);
                StoryPopup.Show("¡Es tuyo!", $"<b>{item.Name}</b> ya está equipado en tu perfil.",
                    new StoryPopup.PopupButton("Genial", onDone));
            }),
            new StoryPopup.PopupButton("Cancelar", null));
    }

    /// <summary>Equipa un artículo comprado (o lo quita si ya estaba puesto y es un banner).</summary>
    public static void Equip(Item item, bool refresh = true)
    {
        PlayerProgress p = SaveManager.Progress;
        if (!p.OwnsCosmetic(item.Id)) return;
        if (item.IsBanner) p.banner = item.Id;
        else p.avatarBadge = item.Id;
        SaveManager.Save();
        ProfileAvatar.SyncToServer();
        if (refresh) MenuScreens.Refresh();
    }

    public static void RemoveBanner()
    {
        SaveManager.Progress.banner = "";
        SaveManager.Save();
        ProfileAvatar.SyncToServer();
        MenuScreens.Refresh();
    }

    /// <summary>
    /// Pone el banner de fondo dentro de parent (detrás de todo, sin afectar el
    /// layout), recortado para llenar el espacio sin deformarse. Devuelve null si no hay banner.
    /// </summary>
    public static RectTransform BannerBackground(Transform parent, string bannerId, float padding, float darken)
    {
        Sprite sprite = BannerSprite(bannerId);
        if (sprite == null) return null;

        GameObject holder = UiKit.Node(parent, "Banner");
        holder.transform.SetAsFirstSibling();
        holder.AddComponent<LayoutElement>().ignoreLayout = true;
        var holderRect = (RectTransform)holder.transform;
        UiKit.Stretch(holderRect, padding);
        holder.AddComponent<RectMask2D>();

        Image image = UiKit.Image(holder.transform, "Imagen", Color.white, sprite);
        image.raycastTarget = false;
        image.preserveAspect = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

        if (darken > 0f)
        {
            Image shade = UiKit.Image(holder.transform, "Sombra", new Color(0f, 0f, 0f, darken));
            shade.raycastTarget = false;
            UiKit.Stretch(shade.rectTransform);
        }
        return holderRect;
    }
}
