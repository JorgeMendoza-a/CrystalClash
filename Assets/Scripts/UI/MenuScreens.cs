using System;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Pantallas de menú a pantalla completa, construidas por código. Todas
/// comparten el mismo armazón: fondo de la mina, barra superior (vidas,
/// monedas, gemas, ajustes) y barra inferior (Mapa, Inicio, Tienda,
/// Clasificatoria, Perfil). Mientras hay una abierta, el tablero y el HUD
/// de la partida se esconden y no reciben clics.
/// </summary>
public static class MenuScreens
{
    public enum Screen { Map, Home, Shop, Ranked, Profile }

    public const float BottomNavHeight = 210f;

    private static GameObject current;

    public static bool IsOpen => current != null;
    public static Screen CurrentScreen { get; private set; }

    public static void Close()
    {
        if (current != null) Object.Destroy(current);
        current = null;
        SetGameVisible(true);
    }

    public static void ShowHome() => Show(Screen.Home);
    public static void ShowRanked() => Show(Screen.Ranked);
    public static void ShowMap() => Show(Screen.Map);

    public static void Show(Screen screen)
    {
        if (current != null) Object.Destroy(current);
        StoryPopup.Close();

        current = UiKit.CreateCanvas("Menu " + screen, 400).gameObject;
        CurrentScreen = screen;
        SetGameVisible(false);

        Transform root = current.transform;
        UiKit.FullscreenBackground(root, screen == Screen.Home ? 0.25f : 0.5f);

        // Zona de contenido entre las dos barras.
        GameObject content = UiKit.Node(root, "Contenido");
        var rect = (RectTransform)content.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(30f, BottomNavHeight + 20f);
        rect.offsetMax = new Vector2(-30f, -(TopBarView.Height + 30f));

        switch (screen)
        {
            case Screen.Home: BuildHome(content.transform); break;
            case Screen.Map: LevelMapScreen.Build(content.transform); break;
            case Screen.Shop: ShopScreen.Build(content.transform); break;
            case Screen.Profile: ProfileScreen.Build(content.transform); break;
            case Screen.Ranked: current.AddComponent<RankedScreen>().Init(content.transform); break;
        }

        TopBarView.Create(root);
        BuildBottomNav(root, screen);
    }

    /// <summary>Vuelve a dibujar la pantalla actual (p. ej. después de comprar).</summary>
    public static void Refresh()
    {
        if (IsOpen) Show(CurrentScreen);
    }

    /// <summary>Esconde/muestra el tablero y el HUD de la partida (para que no se vean detrás del menú).</summary>
    private static void SetGameVisible(bool visible)
    {
        BoardManager board = Object.FindAnyObjectByType<BoardManager>(FindObjectsInactive.Include);
        if (board != null && board.gameObject.activeSelf != visible) board.gameObject.SetActive(visible);

        if (GameHud.Instance != null)
        {
            Canvas canvas = GameHud.Instance.GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = visible;
        }
    }

    // ------------------------------------------------------------------
    // Barra inferior
    // ------------------------------------------------------------------

    private static void BuildBottomNav(Transform root, Screen active)
    {
        Image bar = UiKit.Card(root, "BarraInferior");
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 10f);
        rect.sizeDelta = new Vector2(-20f, BottomNavHeight);

        HorizontalLayoutGroup row = UiKit.Horizontal(bar.gameObject, 26, 8f);
        row.childForceExpandWidth = true;
        row.padding = new RectOffset(30, 30, 24, 24);

        NavTile(bar.transform, "MAPA", "UI/nav_map", Screen.Map, active);
        NavTile(bar.transform, "INICIO", "UI/nav_home", Screen.Home, active);
        NavTile(bar.transform, "TIENDA", "UI/nav_shop", Screen.Shop, active);
        NavTile(bar.transform, "CLASIFICATORIA", "UI/nav_trophy", Screen.Ranked, active);
        NavTile(bar.transform, "PERFIL", "UI/nav_profile", Screen.Profile, active);
    }

    private static void NavTile(Transform parent, string label, string icon, Screen target, Screen active)
    {
        bool selected = target == active;
        Image tile = UiKit.Image(parent, "Nav " + label,
            selected ? new Color(0.25f, 0.18f, 0.45f, 0.95f) : new Color(0.06f, 0.08f, 0.14f, 0.85f));
        tile.sprite = UiKit.LoadSliced("UI/row", 24, 160f);
        tile.type = Image.Type.Sliced;
        tile.preserveAspect = false;

        var button = tile.gameObject.AddComponent<Button>();
        button.targetGraphic = tile;
        button.onClick.AddListener(() => Show(target));

        UiKit.Vertical(tile.gameObject, 6, 0f, TextAnchor.MiddleCenter).padding = new RectOffset(4, 4, 10, 8);
        Image image = UiKit.Image(tile.transform, "Icono", Color.white, UiKit.LoadSprite(icon));
        image.raycastTarget = false;
        UiKit.Size(image, height: selected ? 104f : 92f);

        TMP_Text text = UiKit.Text(tile.transform, label, 26f, selected ? UiKit.Gold : Color.white);
        UiKit.Outline(text, 0.2f);
        UiKit.Size(text, height: 36f);
    }

    // ------------------------------------------------------------------
    // Inicio
    // ------------------------------------------------------------------

    private static void BuildHome(Transform root)
    {
        GameObject column = UiKit.Node(root, "Columna");
        UiKit.Stretch((RectTransform)column.transform);
        UiKit.Vertical(column, 0, 22f, TextAnchor.MiddleCenter);

        BuildCrystals(column.transform);

        TMP_Text crystal = UiKit.Text(column.transform, "CRYSTAL", 150f, Color.white);
        crystal.enableVertexGradient = true;
        crystal.colorGradient = new VertexGradient(
            new Color(0.55f, 0.85f, 1f), new Color(0.55f, 0.85f, 1f),
            new Color(0.1f, 0.4f, 0.95f), new Color(0.1f, 0.4f, 0.95f));
        UiKit.Outline(crystal, 0.24f);
        UiKit.Size(crystal, height: 165f);
        UiKit.Size(UiKit.Title(column.transform, "CLASH", 150f), height: 165f);

        UiKit.Size(UiKit.Node(column.transform, "Espacio").transform, height: 20f);

        MainButton(column.transform, "JUGAR", UiKit.ButtonStyle.Green, 180f, 84f, PlayCurrentLevel);
        MainButton(column.transform, "NIVELES", UiKit.ButtonStyle.Blue, 140f, 60f, ShowMap);
        MainButton(column.transform, "CLASIFICATORIA", UiKit.ButtonStyle.Purple, 140f, 60f, ShowRanked);
        MainButton(column.transform, "TIENDA", UiKit.ButtonStyle.Blue, 140f, 60f, () => Show(Screen.Shop));
    }

    /// <summary>Racimo de cristales sobre el logo (con los sprites de los minerales).</summary>
    private static void BuildCrystals(Transform parent)
    {
        GameObject cluster = UiKit.Node(parent, "Cristales");
        UiKit.Size(cluster.transform, height: 260f);

        Place(cluster.transform, "Minerals/sapphire", -190f, -40f, 150f, 20f);
        Place(cluster.transform, "Minerals/sapphire", 190f, -40f, 150f, -20f);
        Place(cluster.transform, "Minerals/amethyst", -95f, 0f, 190f, 10f);
        Place(cluster.transform, "Minerals/amethyst", 95f, 0f, 190f, -10f);
        Place(cluster.transform, "Minerals/prism", 0f, 30f, 250f, 0f);
    }

    private static void Place(Transform parent, string sprite, float x, float y, float size, float angle)
    {
        Image image = UiKit.Image(parent, "Cristal", Color.white, UiKit.LoadSprite(sprite));
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(size, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static void MainButton(Transform parent, string label, UiKit.ButtonStyle style, float height, float fontSize, Action onClick)
    {
        GameObject holder = UiKit.Node(parent, "Fila " + label);
        UiKit.Size(holder.transform, height: height);
        UiKit.Horizontal(holder, 0, 0f).padding = new RectOffset(110, 110, 0, 0);
        UiKit.Size(UiKit.Button(holder.transform, label, style, onClick, fontSize), flexibleWidth: 1f);
    }

    /// <summary>JUGAR: el nivel más alto desbloqueado.</summary>
    public static void PlayCurrentLevel()
    {
        GameManager game = GameManager.Instance;
        if (game == null) return;

        int level = Mathf.Clamp(SaveManager.Progress.highestUnlocked, 1, Mathf.Max(1, LevelCatalog.Count));
        if (game.Session != null && game.Session.Level.Number == level && !game.IsRanked)
        {
            Close();
            game.StartStory();
        }
        else
        {
            game.PlayLevel(level);
        }
    }

    // ------------------------------------------------------------------
    // Ajustes
    // ------------------------------------------------------------------

    public static void ShowSettings()
    {
        bool testMode = UgsRankedService.TestModeEnabled;
        StoryPopup.Show("Ajustes",
            $"Clasificatoria: <b>{(testMode ? "modo prueba (rival simulado)" : "en línea")}</b>\n\n" +
            "Crystal Clash\n<size=75%>Fuente: Lilita One (SIL Open Font License)</size>",
            new StoryPopup.PopupButton("Cerrar", null),
            new StoryPopup.PopupButton(testMode ? "Usar clasificatoria en línea" : "Usar modo prueba", () =>
            {
                UgsRankedService.TestModeEnabled = !testMode;
                Refresh();
            }),
            new StoryPopup.PopupButton("Borrar progreso", ConfirmReset));
    }

    private static void ConfirmReset()
    {
        StoryPopup.Show("¿Borrar progreso?",
            "Se borran tus niveles, estrellas, monedas, gemas y potenciadores de este dispositivo.\nNo se puede deshacer.",
            new StoryPopup.PopupButton("Cancelar", null),
            new StoryPopup.PopupButton("Sí, borrar", () =>
            {
                SaveManager.ResetProgress();
                Refresh();
            }));
    }
}
