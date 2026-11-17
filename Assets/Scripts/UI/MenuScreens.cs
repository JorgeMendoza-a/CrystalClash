using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantallas de menú a pantalla completa (se construyen por código).
/// Solo hay una abierta a la vez; mientras hay una, el tablero y el HUD de la
/// partida se esconden y no reciben clics.
/// </summary>
public static class MenuScreens
{
    private static GameObject current;

    public static bool IsOpen => current != null;

    public static void Close()
    {
        if (current != null) Object.Destroy(current);
        current = null;
        SetGameVisible(true);
    }

    public static void ShowHome()
    {
        Open("HomeScreen");
        BuildHome(current.transform);
    }

    public static void ShowRanked()
    {
        Open("RankedScreen");
        current.AddComponent<RankedScreen>();
    }

    private static void Open(string name)
    {
        if (current != null) Object.Destroy(current);
        current = UiKit.CreateCanvas(name, 400).gameObject;
        SetGameVisible(false);
    }

    /// <summary>Esconde/muestra el tablero y el HUD de la partida (para que no se vean detrás del menú).</summary>
    private static void SetGameVisible(bool visible)
    {
        BoardManager board = Object.FindFirstObjectByType<BoardManager>(FindObjectsInactive.Include);
        if (board != null && board.gameObject.activeSelf != visible) board.gameObject.SetActive(visible);

        if (GameHud.Instance != null)
        {
            Canvas canvas = GameHud.Instance.GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = visible;
        }
    }

    // ------------------------------------------------------------------

    private static void BuildHome(Transform root)
    {
        UiKit.FullscreenBackground(root, 0.35f);

        GameObject column = UiKit.Node(root, "Columna");
        UiKit.Stretch((RectTransform)column.transform, 70f);
        UiKit.Vertical(column, 0, 30f, TextAnchor.MiddleCenter);

        UiKit.Size(UiKit.Image(column.transform, "Logo", Color.white, UiKit.LoadSprite("Minerals/prism")), height: 280f);
        UiKit.Size(UiKit.Title(column.transform, "CRYSTAL CLASH", 130f), height: 160f);
        TMP_Text subtitle = UiKit.Text(column.transform, "Combina minerales, supera la mina\ny desafía a otros mineros", 42f, Color.white);
        UiKit.Outline(subtitle, 0.15f);
        UiKit.Size(subtitle, height: 120f);

        UiKit.Size(UiKit.Node(column.transform, "Espacio").transform, height: 60f);

        PlayerProgress progress = SaveManager.Progress;
        int levels = Mathf.Max(1, LevelCatalog.Count);
        string storyInfo = $"Sin internet · Nivel {Mathf.Min(progress.highestUnlocked, levels)} · Estrellas {progress.TotalStars}/{levels * 3}";

        ModeButton(column.transform, "MODO HISTORIA", storyInfo, UiKit.ButtonStyle.Blue, UiKit.LoadSprite("Minerals/emerald"), () =>
        {
            Close();
            if (GameManager.Instance != null) GameManager.Instance.StartStory();
        });

        ModeButton(column.transform, "CLASIFICATORIA", "1 vs 1 en línea · sube de rango", UiKit.ButtonStyle.Green,
            UiKit.RankEmblem(4), ShowRanked);
    }

    private static void ModeButton(Transform parent, string title, string subtitle, UiKit.ButtonStyle style, Sprite icon,
        System.Action onClick)
    {
        Button button = UiKit.Button(parent, "", style, onClick);
        UiKit.Size(button, height: 250f);
        Object.Destroy(button.GetComponentInChildren<TMP_Text>().gameObject);

        GameObject row = UiKit.Node(button.transform, "Contenido");
        UiKit.Stretch((RectTransform)row.transform, 34f);
        HorizontalLayoutGroup layout = UiKit.Horizontal(row, 0, 24f);
        layout.childForceExpandWidth = false;

        Image image = UiKit.Image(row.transform, "Icono", Color.white, icon);
        image.raycastTarget = false;
        UiKit.Size(image, width: 170f);

        GameObject texts = UiKit.Node(row.transform, "Textos");
        UiKit.Vertical(texts, 0, 0f, TextAnchor.MiddleLeft);
        UiKit.Size(texts.transform, flexibleWidth: 1f);

        TMP_Text titleText = UiKit.Text(texts.transform, title, 72f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(titleText, 0.2f);
        UiKit.Size(titleText, height: 96f);
        TMP_Text sub = UiKit.Text(texts.transform, subtitle, 34f, new Color(1f, 1f, 1f, 0.92f), FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Outline(sub, 0.15f);
        UiKit.Size(sub, height: 50f);
    }
}
