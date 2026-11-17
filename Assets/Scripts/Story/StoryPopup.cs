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

        public PopupButton(string label, Action onClick, bool interactable = true)
        {
            Label = label;
            OnClick = onClick;
            Interactable = interactable;
        }
    }

    private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.15f, 0.97f);
    private static readonly Color TitleColor = new Color(1f, 0.8f, 0.25f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.42f, 0.85f);
    private static readonly Color DisabledButtonColor = new Color(0.25f, 0.25f, 0.3f);

    private static GameObject current;

    /// <summary>True mientras hay un popup abierto (el tablero ignora los clics).</summary>
    public static bool IsOpen => current != null;

    public static void Show(string title, string body, params PopupButton[] buttons)
    {
        Transform panel = CreatePanel(title, body);

        foreach (PopupButton button in buttons)
        {
            CreateButton(panel, button, 120f, 46f);
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
            CreateButton(gridGO.transform, cell, 0f, 34f);
        }

        foreach (PopupButton button in footer)
        {
            CreateButton(panel, button, 120f, 46f);
        }
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

        Image panel = CreateImage("Panel", canvasGO.transform, PanelColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(880f, 0f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(56, 56, 56, 56);
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText("Title", panel.transform, title, 66f, TitleColor, FontStyles.Bold);
        if (!string.IsNullOrEmpty(body))
        {
            CreateText("Body", panel.transform, body, 42f, Color.white, FontStyles.Normal);
        }

        return panel.transform;
    }

    private static void CreateButton(Transform parent, PopupButton data, float height, float fontSize)
    {
        Image image = CreateImage("Button", parent, data.Interactable ? ButtonColor : DisabledButtonColor);
        if (height > 0f)
        {
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        var button = image.gameObject.AddComponent<Button>();
        button.interactable = data.Interactable;
        button.targetGraphic = image;

        Action onClick = data.OnClick;
        button.onClick.AddListener(() =>
        {
            Close();
            onClick?.Invoke();
        });

        TMP_Text label = CreateText("Label", image.transform, data.Label, fontSize, Color.white, FontStyles.Bold);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 8f);
        labelRect.offsetMax = new Vector2(-8f, -8f);
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
