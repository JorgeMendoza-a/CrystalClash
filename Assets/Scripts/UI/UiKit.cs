using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ayudas para construir pantallas por código con el estilo del juego
/// (tarjetas con marco dorado, textos TMP, botones). Pensado para una
/// pantalla vertical de referencia de 1080x1920.
/// </summary>
public static class UiKit
{
    public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
    public static readonly Color Green = new Color(0.25f, 0.62f, 0.2f);
    public static readonly Color Blue = new Color(0.2f, 0.42f, 0.85f);
    public static readonly Color Red = new Color(0.8f, 0.25f, 0.25f);
    public static readonly Color Muted = new Color(0.75f, 0.78f, 0.85f);
    public static readonly Color Dark = new Color(0.04f, 0.05f, 0.09f, 0.97f);
    public static readonly Color CardFallback = new Color(0.08f, 0.12f, 0.2f, 0.95f);

    private const int FrameBorderPixels = 48;
    private static Sprite cardSprite;

    /// <summary>Canvas a pantalla completa por encima de todo (orden mayor = más arriba).</summary>
    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static GameObject Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
    {
        GameObject go = Node(parent, name);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = sprite != null;
        return image;
    }

    /// <summary>Tarjeta con el marco dorado del tablero (9-slice).</summary>
    public static Image Card(Transform parent, string name)
    {
        GameObject go = Node(parent, name);
        var image = go.AddComponent<Image>();
        Sprite sprite = CardSprite();
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = CardFallback;
        }
        return image;
    }

    public static TMP_Text Text(Transform parent, string text, float size, Color color,
        FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        GameObject go = Node(parent, "Text");
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 16f;
        tmp.fontSizeMax = size;
        return tmp;
    }

    public static Button Button(Transform parent, string label, Color color, Action onClick, float fontSize = 46f)
    {
        Image image = Image(parent, "Button " + label, color);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        TMP_Text text = Text(image.transform, label, fontSize, Color.white, FontStyles.Bold);
        Stretch(text.rectTransform, 10f);
        return button;
    }

    public static VerticalLayoutGroup Vertical(GameObject go, int padding, float spacing,
        TextAnchor align = TextAnchor.UpperCenter)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childAlignment = align;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static HorizontalLayoutGroup Horizontal(GameObject go, int padding, float spacing,
        TextAnchor align = TextAnchor.MiddleCenter)
    {
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childAlignment = align;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return layout;
    }

    /// <summary>Fija el tamaño preferido dentro de un layout (-1 = no tocar).</summary>
    public static LayoutElement Size(Component component, float width = -1f, float height = -1f, float flexibleWidth = -1f)
    {
        LayoutElement element = component.GetComponent<LayoutElement>();
        if (element == null) element = component.gameObject.AddComponent<LayoutElement>();
        if (width >= 0f) element.preferredWidth = width;
        if (height >= 0f)
        {
            element.preferredHeight = height;
            element.minHeight = height;
        }
        if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
        return element;
    }

    public static Sprite LoadSprite(string path)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite CardSprite()
    {
        if (cardSprite != null) return cardSprite;

        Texture2D texture = Resources.Load<Texture2D>("Board/board_frame");
        if (texture == null) return null;

        var border = new Vector4(FrameBorderPixels, FrameBorderPixels, FrameBorderPixels, FrameBorderPixels);
        cardSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
            160f, 0, SpriteMeshType.FullRect, border);
        cardSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return cardSprite;
    }

    /// <summary>Emblema de liga por índice (0 = Bronce ... 6 = Campeón).</summary>
    public static Sprite RankEmblem(int leagueIndex)
    {
        string[] files = { "bronce", "plata", "oro", "platino", "diamante", "maestro", "campeon" };
        int i = Mathf.Clamp(leagueIndex, 0, files.Length - 1);
        return LoadSprite("UI/rank_" + files[i]);
    }

    /// <summary>Color del nombre de cada liga (como en el diseño).</summary>
    public static Color LeagueColor(int leagueIndex)
    {
        switch (leagueIndex)
        {
            case 0: return new Color(0.85f, 0.55f, 0.3f);
            case 1: return new Color(0.82f, 0.84f, 0.9f);
            case 2: return new Color(1f, 0.8f, 0.25f);
            case 3: return new Color(0.4f, 0.9f, 0.7f);
            case 4: return new Color(0.4f, 0.75f, 1f);
            case 5: return new Color(1f, 0.4f, 0.4f);
            default: return new Color(0.8f, 0.55f, 1f);
        }
    }
}
