using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup sencillo que se construye por código (no hace falta armarlo en la escena).
/// Se usa para: inicio de nivel, victoria, derrota y selección de nivel.
/// Cuando tengamos el arte final se puede reemplazar por prefabs bonitos.
/// </summary>
public static class StoryPopup
{
    public struct PopupButton
    {
        public string Label;
        public Action OnClick;
        public bool Interactable;

        /// <summary>Imagen opcional dentro del botón (p. ej. para elegir avatar).</summary>
        public Sprite Icon;

        /// <summary>Contenido armado por código dentro del botón (p. ej. una insignia de temporada).</summary>
        public Action<Transform> BuildContent;

        public PopupButton(string label, Action onClick, bool interactable = true)
        {
            Label = label;
            OnClick = onClick;
            Interactable = interactable;
            Icon = null;
            BuildContent = null;
        }

        public PopupButton(Sprite icon, Action onClick, bool interactable = true)
        {
            Label = "";
            OnClick = onClick;
            Interactable = interactable;
            Icon = icon;
            BuildContent = null;
        }

        public PopupButton(Action<Transform> buildContent, Action onClick)
        {
            Label = "";
            OnClick = onClick;
            Interactable = true;
            Icon = null;
            BuildContent = buildContent;
        }
    }

    private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.15f, 0.97f);
    private static readonly Color TitleColor = new Color(1f, 0.8f, 0.25f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.42f, 0.85f);
    private static readonly Color DisabledButtonColor = new Color(0.25f, 0.25f, 0.3f);

    private static GameObject current;

    /// <summary>True mientras hay un popup abierto (el tablero ignora los clics).</summary>
    public static bool IsOpen => current != null;

    /// <summary>Pantalla de VS: tu foto y nombre contra los del rival.</summary>
    public static void ShowVersus(string title, string myName, string myAvatar, string myBanner,
        string rivalName, string rivalAvatar, string rivalBanner,
        string rivalInfo, string body, params PopupButton[] buttons)
    {
        Transform panel = CreatePanel(title, null);

        var rowGO = new GameObject("VS", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGO.transform.SetParent(panel, false);
        rowGO.GetComponent<LayoutElement>().preferredHeight = 330f;
        var row = rowGO.GetComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 10f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        VersusSide(rowGO.transform, myName, "Tú", myAvatar, myBanner);

        TMP_Text vs = CreateText("VS", rowGO.transform, "VS", 96f, TitleColor, FontStyles.Normal);
        UiKit.Outline(vs, 0.25f);
        vs.gameObject.AddComponent<LayoutElement>().preferredWidth = 140f;

        VersusSide(rowGO.transform, rivalName, rivalInfo, rivalAvatar, rivalBanner);

        if (!string.IsNullOrEmpty(body))
        {
            TMP_Text bodyText = CreateText("Body", panel, body, 40f, Color.white, FontStyles.Normal);
            UiKit.Outline(bodyText, 0.12f);
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            CreateButton(panel, buttons[i], 130f, 50f, i == 0 ? UiKit.ButtonStyle.Green : UiKit.ButtonStyle.Blue);
        }
    }

    private static void VersusSide(Transform parent, string name, string info, string avatar, string banner)
    {
        var side = new GameObject("Lado", typeof(RectTransform), typeof(VerticalLayoutGroup));
        side.transform.SetParent(parent, false);
        var layout = side.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 4f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Banner de fondo (comprado en la tienda), oscurecido para que se lea el nombre.
        Cosmetics.BannerBackground(side.transform, banner, 0f, 0.35f);

        ProfileAvatar.Create(side.transform, avatar, 200f);

        TMP_Text nameText = CreateText("Nombre", side.transform, name, 40f, Color.white, FontStyles.Normal);
        UiKit.Outline(nameText, 0.2f);
        nameText.rectTransform.sizeDelta = new Vector2(300f, 54f);
        nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

        TMP_Text infoText = CreateText("Info", side.transform, info, 30f, UiKit.Muted, FontStyles.Normal);
        UiKit.Outline(infoText, 0.15f);
        infoText.rectTransform.sizeDelta = new Vector2(300f, 42f);
        infoText.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
    }

    /// <summary>Como Show, con una insignia de temporada debajo del título (season 0 = sin insignia).</summary>
    public static void ShowWithBadge(string title, string body, int season, int leagueIndex, params PopupButton[] buttons)
    {
        Transform panel = CreatePanel(title, body);
        if (season > 0)
        {
            RectTransform badge = SeasonBadge.Create(panel, season, leagueIndex, 260f);
            badge.SetSiblingIndex(1); // entre el título y el texto
        }
        for (int i = 0; i < buttons.Length; i++)
        {
            CreateButton(panel, buttons[i], 130f, 50f, i == 0 ? UiKit.ButtonStyle.Green : UiKit.ButtonStyle.Blue);
        }
    }

    public static void Show(string title, string body, params PopupButton[] buttons)
    {
        Transform panel = CreatePanel(title, body);

        for (int i = 0; i < buttons.Length; i++)
        {
            // El primer botón es la acción principal (verde); el resto, azules.
            CreateButton(panel, buttons[i], 130f, 50f, i == 0 ? UiKit.ButtonStyle.Green : UiKit.ButtonStyle.Blue);
        }
    }

    /// <summary>Cuadrícula de botones (para elegir nivel).</summary>
    public static void ShowGrid(string title, string body, IList<PopupButton> cells, int columns, params PopupButton[] footer)
    {
        Transform panel = CreatePanel(title, body);

        var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        gridGO.transform.SetParent(panel, false);
        var grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(140f, 140f);
        grid.spacing = new Vector2(16f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        int rows = Mathf.CeilToInt(cells.Count / (float)columns);
        gridGO.GetComponent<LayoutElement>().preferredHeight = rows * 140f + (rows - 1) * 16f;

        foreach (PopupButton cell in cells)
        {
            CreateButton(gridGO.transform, cell, 0f, 36f, UiKit.ButtonStyle.Blue);
        }

        foreach (PopupButton button in footer)
        {
            CreateButton(panel, button, 120f, 46f, UiKit.ButtonStyle.Gray);
        }
    }

    /// <summary>Popup con un campo de texto (p. ej. para cambiar el nombre).</summary>
    public static void ShowInput(string title, string body, string initialText, int maxLength, string confirmLabel, Action<string> onConfirm)
    {
        Transform panel = CreatePanel(title, body);

        Image box = CreateImage("Campo", panel, new Color(0.03f, 0.05f, 0.1f, 1f));
        box.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;

        // Se arma desactivado para que el campo no se inicialice sin sus partes.
        box.gameObject.SetActive(false);

        var area = new GameObject("Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(box.transform, false);
        var areaRect = (RectTransform)area.transform;
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(26f, 14f);
        areaRect.offsetMax = new Vector2(-26f, -14f);

        TMP_Text placeholder = CreateText("Placeholder", area.transform, "Escribe aquí...", 48f, new Color(1f, 1f, 1f, 0.35f), FontStyles.Normal);
        TMP_Text text = CreateText("Texto", area.transform, "", 48f, Color.white, FontStyles.Normal);
        foreach (TMP_Text t in new[] { placeholder, text })
        {
            t.enableAutoSizing = false;
            RectTransform r = t.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        var field = box.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = areaRect;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.characterLimit = maxLength;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.fontAsset = UiKit.GameFont != null ? UiKit.GameFont : text.font;
        field.text = initialText ?? "";
        box.gameObject.SetActive(true);

        CreateButton(panel, new PopupButton(confirmLabel, () => onConfirm?.Invoke(field.text)), 130f, 50f, UiKit.ButtonStyle.Green);
        CreateButton(panel, new PopupButton("Cancelar", null), 120f, 46f, UiKit.ButtonStyle.Gray);
    }

    public static void Close()
    {
        if (current != null) UnityEngine.Object.Destroy(current);
        current = null;
    }

    // ------------------------------------------------------------------

    private static Transform CreatePanel(string title, string body)
    {
        Close();

        var canvasGO = new GameObject("StoryPopup", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        current = canvasGO;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        // Fondo oscuro que tapa todo (y bloquea los clics a la UI de atrás).
        Image dim = CreateImage("Dim", canvasGO.transform, new Color(0f, 0f, 0f, 0.7f));
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Sprite panelSprite = UiKit.PanelSprite();
        Image panel = CreateImage("Panel", canvasGO.transform, panelSprite != null ? Color.white : PanelColor);
        if (panelSprite != null)
        {
            panel.sprite = panelSprite;
            panel.type = Image.Type.Sliced;
        }
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(880f, 0f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(70, 70, 70, 70);
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text titleText = CreateText("Title", panel.transform, title, 76f, TitleColor, FontStyles.Normal);
        titleText.enableVertexGradient = true;
        titleText.colorGradient = new VertexGradient(
            new Color(1f, 0.93f, 0.55f), new Color(1f, 0.93f, 0.55f),
            new Color(0.95f, 0.6f, 0.12f), new Color(0.95f, 0.6f, 0.12f));
        UiKit.Outline(titleText, 0.22f);

        if (!string.IsNullOrEmpty(body))
        {
            TMP_Text bodyText = CreateText("Body", panel.transform, body, 44f, Color.white, FontStyles.Normal);
            UiKit.Outline(bodyText, 0.12f);
        }

        return panel.transform;
    }

    private static void CreateButton(Transform parent, PopupButton data, float height, float fontSize, UiKit.ButtonStyle style)
    {
        Sprite sprite = UiKit.ButtonSprite(data.Interactable ? style : UiKit.ButtonStyle.Gray);
        Image image = CreateImage("Button", parent, sprite != null ? Color.white : (data.Interactable ? ButtonColor : DisabledButtonColor));
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
        if (height > 0f)
        {
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        var button = image.gameObject.AddComponent<Button>();
        Sound.AddClick(button);
        button.interactable = data.Interactable;
        button.targetGraphic = image;

        Action onClick = data.OnClick;
        button.onClick.AddListener(() =>
        {
            Close();
            onClick?.Invoke();
        });

        if (data.BuildContent != null)
        {
            data.BuildContent(image.transform);
            return;
        }

        if (data.Icon != null)
        {
            Image icon = CreateImage("Icono", image.transform, Color.white);
            icon.sprite = data.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(16f, 18f);
            iconRect.offsetMax = new Vector2(-16f, -12f);
            if (string.IsNullOrEmpty(data.Label)) return;
        }

        TMP_Text label = CreateText("Label", image.transform, data.Label, fontSize, Color.white, FontStyles.Normal);
        UiKit.Outline(label, 0.2f);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 20f);
        labelRect.offsetMax = new Vector2(-18f, -12f);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        UiKit.ApplyFont(tmp);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }
}
