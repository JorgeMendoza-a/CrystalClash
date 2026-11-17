using System.Collections;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mapa de niveles: camino de rieles que serpentea entre los niveles.
/// Verde = superado, azul (latiendo) = el que toca, gris = bloqueado.
/// Debajo de cada nivel superado se ven sus estrellas.
/// </summary>
public static class LevelMapScreen
{
    private const int NodesPerRow = 4;
    private const float NodeSize = 160f;
    private const float ColumnSpacing = 235f;
    private const float RowSpacing = 300f;
    private const string WorldName = "MINA DE CRISTALES";

    public static void Build(Transform root)
    {
        PlayerProgress progress = SaveManager.Progress;
        int count = LevelCatalog.Count;

        GameObject column = UiKit.Node(root, "Mapa");
        UiKit.Stretch((RectTransform)column.transform);
        UiKit.Vertical(column, 0, 16f);

        // Encabezado: estrellas + título
        GameObject header = UiKit.Node(column.transform, "Encabezado");
        UiKit.Horizontal(header, 0, 16f).childForceExpandWidth = false;
        UiKit.Size(header.transform, height: 150f);

        Image starsBox = UiKit.Card(header.transform, "Estrellas");
        UiKit.Size(starsBox, width: 290f);
        UiKit.Horizontal(starsBox.gameObject, 26, 6f).childForceExpandWidth = false;
        UiKit.Size(UiKit.Image(starsBox.transform, "Estrella", Color.white, UiKit.LoadSprite("UI/star_full")), width: 80f);
        TMP_Text stars = UiKit.Text(starsBox.transform, $"{progress.TotalStars}<size=60%>/{count * 3}</size>", 52f, Color.white);
        UiKit.Outline(stars, 0.2f);
        UiKit.Size(stars, flexibleWidth: 1f);

        Image titleBox = UiKit.Card(header.transform, "Titulo");
        UiKit.Size(titleBox, flexibleWidth: 1f);
        UiKit.Vertical(titleBox.gameObject, 20, 0f, TextAnchor.MiddleCenter);
        UiKit.Size(UiKit.Title(titleBox.transform, "MAPA DE NIVELES", 64f), height: 90f);

        // Zona del mapa
        GameObject mapArea = UiKit.Node(column.transform, "Zona");
        UiKit.Size(mapArea.transform, height: 1000f);
        GameObject map = UiKit.Node(mapArea.transform, "Niveles");
        var mapRect = (RectTransform)map.transform;
        mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.sizeDelta = new Vector2(1000f, 1000f);

        int rows = Mathf.CeilToInt(count / (float)NodesPerRow);
        float top = (rows - 1) * RowSpacing / 2f;

        // Primero los rieles (para que queden debajo de los círculos)
        for (int n = 1; n < count; n++)
        {
            Rail(map.transform, NodePosition(n, top), NodePosition(n + 1, top));
        }

        for (int n = 1; n <= count; n++)
        {
            Node(map.transform, n, NodePosition(n, top), progress);
        }

        // Selector de mundo
        GameObject world = UiKit.Node(column.transform, "Mundo");
        UiKit.Horizontal(world, 0, 14f).childForceExpandWidth = false;
        UiKit.Size(world.transform, height: 140f);

        UiKit.Size(UiKit.Button(world.transform, "<", UiKit.ButtonStyle.Gray, ComingSoon, 60f), width: 130f);
        Image worldBox = UiKit.Card(world.transform, "Nombre");
        UiKit.Size(worldBox, flexibleWidth: 1f);
        UiKit.Vertical(worldBox.gameObject, 18, 0f, TextAnchor.MiddleCenter);
        TMP_Text worldTitle = UiKit.Text(worldBox.transform, "MUNDO 1", 44f, Color.white);
        UiKit.Outline(worldTitle, 0.18f);
        UiKit.Size(worldTitle, height: 52f);
        TMP_Text worldName = UiKit.Text(worldBox.transform, WorldName, 34f, UiKit.Gold);
        UiKit.Outline(worldName, 0.18f);
        UiKit.Size(worldName, height: 42f);
        UiKit.Size(UiKit.Button(world.transform, ">", UiKit.ButtonStyle.Gray, ComingSoon, 60f), width: 130f);
    }

    /// <summary>Posición de un nivel: filas de 4 que van y vuelven (serpiente).</summary>
    private static Vector2 NodePosition(int number, float top)
    {
        int index = number - 1;
        int row = index / NodesPerRow;
        int col = index % NodesPerRow;
        if (row % 2 == 1) col = NodesPerRow - 1 - col;

        float x = (col - (NodesPerRow - 1) / 2f) * ColumnSpacing;
        float y = top - row * RowSpacing;
        return new Vector2(x, y);
    }

    private static void Rail(Transform parent, Vector2 from, Vector2 to)
    {
        Sprite sprite = UiKit.LoadSliced("UI/rail", 24, 100f);
        Image rail = UiKit.Image(parent, "Riel", sprite != null ? Color.white : new Color(0.45f, 0.3f, 0.15f), sprite);
        rail.raycastTarget = false;
        if (sprite != null)
        {
            rail.type = Image.Type.Sliced;
            rail.preserveAspect = false;
        }

        Vector2 delta = to - from;
        RectTransform rect = rail.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (from + to) / 2f;
        rect.sizeDelta = new Vector2(delta.magnitude + 40f, 70f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private static void Node(Transform parent, int number, Vector2 position, PlayerProgress progress)
    {
        bool unlocked = progress.IsUnlocked(number);
        int earned = progress.GetStars(number);
        bool isCurrent = unlocked && earned == 0;

        string sprite = !unlocked ? "UI/node_gray" : isCurrent ? "UI/node_blue" : "UI/node_green";
        Image node = UiKit.Image(parent, "Nivel " + number, Color.white, UiKit.LoadSprite(sprite));
        RectTransform rect = node.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(NodeSize, NodeSize);

        var button = node.gameObject.AddComponent<Button>();
        button.targetGraphic = node;
        int n = number;
        button.onClick.AddListener(() =>
        {
            if (!SaveManager.Progress.IsUnlocked(n))
            {
                StoryPopup.Show($"Nivel {n}", "Supera el nivel anterior para desbloquearlo.", new StoryPopup.PopupButton("Entendido", null));
                return;
            }
            if (GameManager.Instance != null) GameManager.Instance.PlayLevel(n);
        });

        TMP_Text label = UiKit.Text(node.transform, number.ToString(), 70f, unlocked ? Color.white : new Color(0.75f, 0.75f, 0.8f));
        UiKit.Outline(label, 0.25f);
        UiKit.Stretch(label.rectTransform, 20f);

        // Estrellas debajo del círculo
        GameObject starsRow = UiKit.Node(node.transform, "Estrellas");
        var starsRect = (RectTransform)starsRow.transform;
        starsRect.anchorMin = starsRect.anchorMax = new Vector2(0.5f, 0f);
        starsRect.pivot = new Vector2(0.5f, 1f);
        starsRect.anchoredPosition = new Vector2(0f, 12f);
        starsRect.sizeDelta = new Vector2(180f, 56f);
        HorizontalLayoutGroup starsLayout = UiKit.Horizontal(starsRow, 0, 2f);
        starsLayout.childForceExpandWidth = true;

        Sprite full = UiKit.LoadSprite("UI/star_full");
        Sprite empty = UiKit.LoadSprite("UI/star_empty");
        for (int i = 0; i < 3; i++)
        {
            Image star = UiKit.Image(starsRow.transform, "Estrella", Color.white, i < earned ? full : empty);
            star.raycastTarget = false;
        }

        if (isCurrent) node.gameObject.AddComponent<Pulse>();
    }

    private static void ComingSoon()
    {
        StoryPopup.Show("Próximamente", "¡Nuevos mundos están en camino!", new StoryPopup.PopupButton("Genial", null));
    }

    /// <summary>Hace "latir" el nivel actual.</summary>
    private class Pulse : MonoBehaviour
    {
        private IEnumerator Start()
        {
            Vector3 baseScale = transform.localScale;
            while (true)
            {
                float k = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.06f;
                transform.localScale = baseScale * k;
                yield return null;
            }
        }
    }
}
