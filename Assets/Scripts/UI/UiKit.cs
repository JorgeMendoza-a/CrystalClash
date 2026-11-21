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

    public enum ButtonStyle { Green, Blue, Gray, Red, Purple }

    private const int FrameBorderPixels = 48;
    private static Sprite cardSprite;
    private static TMP_FontAsset gameFont;
    private static bool fontLoaded;
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> slicedCache =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    /// <summary>Fuente del juego (Lilita One, Resources/Fonts). Null si no está: se usa la de TMP.</summary>
    public static TMP_FontAsset GameFont
    {
        get
        {
            if (fontLoaded) return gameFont;
            fontLoaded = true;

            Font font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            if (font == null) return null;

            gameFont = TMP_FontAsset.CreateFontAsset(font);
            if (gameFont != null)
            {
                gameFont.hideFlags = HideFlags.DontUnloadUnusedAsset;
                // Si falta algún carácter, usar la fuente por defecto de TMP.
                if (TMP_Settings.defaultFontAsset != null)
                {
                    gameFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
                }
            }
            return gameFont;
        }
    }

    public static void ApplyFont(TMP_Text text)
    {
        TMP_FontAsset font = GameFont;
        if (font != null) text.font = font;
    }

    /// <summary>Contorno oscuro (para títulos y textos de botones).</summary>
    public static void Outline(TMP_Text text, float width = 0.18f)
    {
        text.outlineWidth = width;
        text.outlineColor = new Color32(30, 18, 5, 255);
    }

    /// <summary>Título dorado con degradado y contorno, como en el diseño.</summary>
    public static TMP_Text Title(Transform parent, string text, float size)
    {
        TMP_Text tmp = Text(parent, text, size, Color.white, FontStyles.Normal);
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new VertexGradient(
            new Color(1f, 0.93f, 0.55f), new Color(1f, 0.93f, 0.55f),
            new Color(0.95f, 0.6f, 0.12f), new Color(0.95f, 0.6f, 0.12f));
        Outline(tmp, 0.22f);
        return tmp;
    }

    /// <summary>Sprite 9-slice desde Resources (borde en píxeles de la textura).</summary>
    public static Sprite LoadSliced(string path, int borderPixels, float pixelsPerUnit)
    {
        string key = path + "|" + borderPixels + "|" + pixelsPerUnit;
        if (slicedCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;

        var border = new Vector4(borderPixels, borderPixels, borderPixels, borderPixels);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
            pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        slicedCache[key] = sprite;
        return sprite;
    }

    /// <summary>Panel ornamentado (Resources/UI/panel.png); si no está, el marco del tablero.</summary>
    public static Sprite PanelSprite()
    {
        return LoadSliced("UI/panel", 80, 160f) ?? CardSprite();
    }

    public static Sprite ButtonSprite(ButtonStyle style)
    {
        string file;
        switch (style)
        {
            case ButtonStyle.Green: file = "UI/btn_green"; break;
            case ButtonStyle.Blue: file = "UI/btn_blue"; break;
            case ButtonStyle.Red: file = "UI/btn_red"; break;
            case ButtonStyle.Purple: file = "UI/btn_purple"; break;
            default: file = "UI/btn_gray"; break;
        }
        return LoadSliced(file, 56, 110f);
    }

    /// <summary>El fondo de la mina de la escena (Image llamada "Background"), para los menús.</summary>
    public static Sprite SceneBackground()
    {
        GameObject background = GameObject.Find("Background");
        Image image = background != null ? background.GetComponent<Image>() : null;
        return image != null ? image.sprite : null;
    }

    /// <summary>Fondo a pantalla completa: la mina de la escena oscurecida (o un color si no hay).</summary>
    public static void FullscreenBackground(Transform parent, float darkness = 0.55f)
    {
        Image solid = Image(parent, "FondoSolido", new Color(0.03f, 0.04f, 0.08f, 1f));
        Stretch(solid.rectTransform);

        Sprite art = SceneBackground();
        if (art != null)
        {
            Image picture = Image(parent, "FondoMina", Color.white, art);
            picture.preserveAspect = false;
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = art.rect.width / art.rect.height;
        }

        Image shade = Image(parent, "Oscurecer", new Color(0.02f, 0.02f, 0.05f, darkness));
        Stretch(shade.rectTransform);
    }

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

    /// <summary>Contenedor ajustado al área segura (para no quedar bajo el notch).</summary>
    public static Transform SafeRoot(Transform canvas)
    {
        GameObject root = Node(canvas, "AreaSegura");
        Stretch((RectTransform)root.transform);
        root.AddComponent<SafeAreaFitter>();
        return root.transform;
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

    /// <summary>Tarjeta con marco dorado ornamentado (9-slice).</summary>
    public static Image Card(Transform parent, string name)
    {
        GameObject go = Node(parent, name);
        var image = go.AddComponent<Image>();
        Sprite sprite = PanelSprite();
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
        ApplyFont(tmp);
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

    public static Button Button(Transform parent, string label, ButtonStyle style, Action onClick, float fontSize = 46f)
    {
        Sprite sprite = ButtonSprite(style);
        Color fallback = style == ButtonStyle.Green ? Green : style == ButtonStyle.Blue ? Blue : style == ButtonStyle.Red ? Red : new Color(0.3f, 0.3f, 0.36f);
        Image image = Image(parent, "Button " + label, sprite != null ? Color.white : fallback, sprite);
        if (sprite != null)
        {
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.preserveAspect = false;
        }

        var button = image.gameObject.AddComponent<Button>();
        Sound.AddClick(button);
        button.targetGraphic = image;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        TMP_Text text = Text(image.transform, label, fontSize, Color.white, FontStyles.Normal);
        Outline(text);
        Stretch(text.rectTransform, 18f);
        text.rectTransform.offsetMin = new Vector2(24f, 22f);
        text.rectTransform.offsetMax = new Vector2(-24f, -14f);
        return button;
    }

    /// <summary>Barra de progreso con relieve. Devuelve el RectTransform del relleno (anchorMax.x = fracción).</summary>
    public static RectTransform ProgressBar(Transform parent, float height, out TMP_Text label)
    {
        Sprite bgSprite = LoadSliced("UI/bar_bg", 40, 160f);
        Sprite fillSprite = LoadSliced("UI/bar_fill", 40, 160f);

        Image bar = Image(parent, "Barra", bgSprite != null ? Color.white : new Color(0.05f, 0.07f, 0.12f), bgSprite);
        if (bgSprite != null) { bar.type = UnityEngine.UI.Image.Type.Sliced; bar.preserveAspect = false; }
        Size(bar, height: height);

        Image fill = Image(bar.transform, "Relleno", fillSprite != null ? Color.white : new Color(0.15f, 0.5f, 0.95f), fillSprite);
        if (fillSprite != null) { fill.type = UnityEngine.UI.Image.Type.Sliced; fill.preserveAspect = false; }
        RectTransform rect = fill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        label = Text(bar.transform, "", height * 0.6f, Color.white);
        Outline(label, 0.2f);
        Stretch(label.rectTransform);
        return rect;
    }

    /// <summary>
    /// Zona con desplazamiento vertical. Devuelve el contenido (con VerticalLayoutGroup)
    /// donde se agregan los elementos; su altura crece sola.
    /// </summary>
    public static Transform ScrollArea(Transform parent, float spacing = 14f)
    {
        GameObject view = Node(parent, "Scroll");
        Stretch((RectTransform)view.transform);
        view.AddComponent<RectMask2D>();
        var scroll = view.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        GameObject content = Node(view.transform, "Contenido");
        var rect = (RectTransform)content.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(0f, 0f);
        Vertical(content, 0, spacing);
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = (RectTransform)view.transform;
        scroll.content = rect;
        return content.transform;
    }

    /// <summary>Fondo de fila de tabla.</summary>
    public static Image Row(Transform parent, string name)
    {
        Sprite sprite = LoadSliced("UI/row", 24, 160f);
        Image image = Image(parent, name, sprite != null ? Color.white : new Color(0.12f, 0.16f, 0.26f, 0.9f), sprite);
        if (sprite != null) { image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false; }
        return image;
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
        if (width >= 0f)
        {
            element.preferredWidth = width;
            // Un ancho fijo no crece (si no, un layout hijo con "expand" lo estira).
            if (flexibleWidth < 0f) element.flexibleWidth = 0f;
        }
        if (height >= 0f)
        {
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;
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

    private static readonly string[] LeagueFiles = { "bronce", "plata", "oro", "platino", "diamante", "maestro", "campeon" };

    /// <summary>
    /// Emblema de una división concreta (Resources/UI/Ranks/rango_plata_3.png,
    /// rango_campeon.png). Si falta, usa el emblema provisional de la liga.
    /// </summary>
    public static Sprite RankEmblem(CrystalClash.Ranked.Logic.RankTier tier)
    {
        int league = Mathf.Clamp(tier.LeagueIndex, 0, LeagueFiles.Length - 1);
        string file = tier.Division == 0
            ? "UI/Ranks/rango_" + LeagueFiles[league]
            : "UI/Ranks/rango_" + LeagueFiles[league] + "_" + tier.Division;
        return LoadSprite(file) ?? LoadSprite("UI/rank_" + LeagueFiles[league]);
    }

    /// <summary>Emblema representativo de una liga (0 = Bronce ... 6 = Campeón): su división 1.</summary>
    public static Sprite RankEmblem(int leagueIndex)
    {
        int league = Mathf.Clamp(leagueIndex, 0, LeagueFiles.Length - 1);
        string file = league == LeagueFiles.Length - 1
            ? "UI/Ranks/rango_campeon"
            : "UI/Ranks/rango_" + LeagueFiles[league] + "_1";
        return LoadSprite(file) ?? LoadSprite("UI/rank_" + LeagueFiles[league]);
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
