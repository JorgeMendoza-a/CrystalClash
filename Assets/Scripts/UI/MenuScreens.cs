using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;

/// <summary>
/// Pantallas de menú a pantalla completa (se construyen por código).
/// Solo hay una abierta a la vez; mientras hay una, el tablero no recibe clics.
/// </summary>
public static class MenuScreens
{
    private static GameObject current;

    public static bool IsOpen => current != null;

    public static void Close()
    {
        if (current != null) Object.Destroy(current);
        current = null;
    }

    public static void ShowHome()
    {
        Close();
        Canvas canvas = UiKit.CreateCanvas("HomeScreen", 400);
        current = canvas.gameObject;
        BuildHome(canvas.transform);
    }

    public static void ShowRanked()
    {
        Close();
        Canvas canvas = UiKit.CreateCanvas("RankedScreen", 400);
        current = canvas.gameObject;
        canvas.gameObject.AddComponent<RankedScreen>();
    }

    // ------------------------------------------------------------------

    private static void BuildHome(Transform root)
    {
        UnityEngine.UI.Image background = UiKit.Image(root, "Fondo", new Color(0.03f, 0.04f, 0.08f, 0.92f));
        UiKit.Stretch(background.rectTransform);

        GameObject column = UiKit.Node(root, "Columna");
        UiKit.Stretch((RectTransform)column.transform, 70f);
        UiKit.Vertical(column, 0, 36f, TextAnchor.MiddleCenter);

        UiKit.Size(UiKit.Image(column.transform, "Logo", Color.white, UiKit.LoadSprite("Minerals/prism")), height: 300f);
        UiKit.Size(UiKit.Text(column.transform, "CRYSTAL CLASH", 110f, UiKit.Gold, FontStyles.Bold), height: 140f);
        UiKit.Size(UiKit.Text(column.transform, "Combina minerales, supera la mina\ny desafía a otros mineros", 40f, UiKit.Muted), height: 110f);

        UiKit.Size(UiKit.Node(column.transform, "Espacio").transform, height: 40f);

        PlayerProgress progress = SaveManager.Progress;
        int levels = LevelCatalog.Count;
        string storyInfo = $"Sin internet · Nivel {Mathf.Min(progress.highestUnlocked, Mathf.Max(1, levels))} · Estrellas {progress.TotalStars}/{levels * 3}";

        ModeButton(column.transform, "MODO HISTORIA", storyInfo, UiKit.Blue, () =>
        {
            Close();
            if (GameManager.Instance != null) GameManager.Instance.StartStory();
        });

        ModeButton(column.transform, "CLASIFICATORIA", "1 vs 1 en línea · sube de rango", UiKit.Green, ShowRanked);
    }

    private static void ModeButton(Transform parent, string title, string subtitle, Color color, System.Action onClick)
    {
        UnityEngine.UI.Button button = UiKit.Button(parent, "", color, onClick);
        UiKit.Size(button, height: 210f);

        // Reemplazamos la etiqueta simple por título + subtítulo.
        Object.Destroy(button.GetComponentInChildren<TMP_Text>().gameObject);
        GameObject texts = UiKit.Node(button.transform, "Textos");
        UiKit.Stretch((RectTransform)texts.transform, 16f);
        UiKit.Vertical(texts, 0, 4f, TextAnchor.MiddleCenter);
        UiKit.Size(UiKit.Text(texts.transform, title, 64f, Color.white, FontStyles.Bold), height: 90f);
        UiKit.Size(UiKit.Text(texts.transform, subtitle, 34f, new Color(1f, 1f, 1f, 0.85f)), height: 50f);
    }
}
