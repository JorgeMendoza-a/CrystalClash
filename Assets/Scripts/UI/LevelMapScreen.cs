using System.Collections;
using System.Collections.Generic;
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
    private const float RowSpacing = 290f;
    private const float MapTopPadding = 130f;

    /// <summary>Mundo que se está mirando (null = el del nivel más alto desbloqueado).</summary>
    private static int? viewedWorld;

    public static void Build(Transform root)
    {
        PlayerProgress progress = SaveManager.Progress;
        IReadOnlyList<LevelCatalog.WorldInfo> worlds = LevelCatalog.Worlds;
        if (worlds.Count == 0) return;

        int current = LevelCatalog.WorldOf(Mathf.Min(progress.highestUnlocked, LevelCatalog.Count)).Number;
        int worldNumber = Mathf.Clamp(viewedWorld ?? current, 1, worlds.Count);
        LevelCatalog.WorldInfo world = worlds[worldNumber - 1];
        bool worldUnlocked = progress.IsUnlocked(world.FirstLevel);

        int worldStars = 0;
        for (int n = world.FirstLevel; n <= world.LastLevel; n++) worldStars += progress.GetStars(n);

        GameObject column = UiKit.Node(root, "Mapa");
        UiKit.Stretch((RectTransform)column.transform);
        VerticalLayoutGroup columnLayout = UiKit.Vertical(column, 0, 14f);
        columnLayout.childForceExpandHeight = false;

        // Encabezado: estrellas del mundo + título
        GameObject header = UiKit.Node(column.transform, "Encabezado");
        UiKit.Horizontal(header, 0, 16f).childForceExpandWidth = false;
        UiKit.Size(header.transform, height: 150f);

        Image starsBox = UiKit.Card(header.transform, "Estrellas");
        UiKit.Size(starsBox, width: 300f);
        UiKit.Horizontal(starsBox.gameObject, 26, 6f).childForceExpandWidth = false;
        UiKit.Size(UiKit.Image(starsBox.transform, "Estrella", Color.white, UiKit.LoadSprite("UI/star_full")), width: 80f);
        TMP_Text stars = UiKit.Text(starsBox.transform, $"{worldStars}<size=60%>/{world.LevelCount * 3}</size>", 52f, Color.white);
        UiKit.Outline(stars, 0.2f);
        UiKit.Size(stars, flexibleWidth: 1f);

        Image titleBox = UiKit.Card(header.transform, "Titulo");
        UiKit.Size(titleBox, flexibleWidth: 1f);
        UiKit.Vertical(titleBox.gameObject, 20, 0f, TextAnchor.MiddleCenter);
        UiKit.Size(UiKit.Title(titleBox.transform, "MAPA DE NIVELES", 64f), height: 90f);

        // Zona del mapa con desplazamiento (el Mundo 4 tiene 40 niveles)
        GameObject mapArea = UiKit.Node(column.transform, "Zona");
        LayoutElement mapElement = mapArea.AddComponent<LayoutElement>();
        mapElement.flexibleHeight = 1f;
        mapElement.minHeight = 600f;
        Transform scrollContent = UiKit.ScrollArea(mapArea.transform, 0f);

        int count = world.LevelCount;
        int rows = Mathf.CeilToInt(count / (float)NodesPerRow);
        float mapHeight = MapTopPadding + (rows - 1) * RowSpacing + 170f;

        GameObject mapHolder = UiKit.Node(scrollContent, "Niveles");
        UiKit.Size(mapHolder.transform, height: mapHeight);

        GameObject map = UiKit.Node(mapHolder.transform, "Centro");
        var mapRect = (RectTransform)map.transform;
        mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 1f);
        mapRect.pivot = new Vector2(0.5f, 1f);
        mapRect.anchoredPosition = Vector2.zero;
        mapRect.sizeDelta = new Vector2(1000f, mapHeight);

        for (int i = 1; i < count; i++)
        {
            Rail(map.transform, NodePosition(i), NodePosition(i + 1));
        }
        for (int i = 1; i <= count; i++)
        {
            Node(map.transform, world.FirstLevel + i - 1, NodePosition(i), progress);
        }

        // Abrir el mapa a la altura del nivel actual
        int currentLevel = Mathf.Clamp(progress.highestUnlocked, world.FirstLevel, world.LastLevel);
        int currentRow = (currentLevel - world.FirstLevel) / NodesPerRow;
        var contentRect = (RectTransform)scrollContent;
        contentRect.anchoredPosition = new Vector2(0f, Mathf.Max(0f, currentRow * RowSpacing - 300f));

        // Selector de mundo
        GameObject selector = UiKit.Node(column.transform, "Mundo");
        UiKit.Horizontal(selector, 0, 14f).childForceExpandWidth = false;
        UiKit.Size(selector.transform, height: 140f);

        Button prev = UiKit.Button(selector.transform, "<", UiKit.ButtonStyle.Gray, () => ShowWorld(worldNumber - 1), 60f);
        UiKit.Size(prev, width: 130f);
        prev.interactable = worldNumber > 1;

        Image worldBox = UiKit.Card(selector.transform, "Nombre");
        UiKit.Size(worldBox, flexibleWidth: 1f);
        UiKit.Vertical(worldBox.gameObject, 18, 0f, TextAnchor.MiddleCenter);
        TMP_Text worldTitle = UiKit.Text(worldBox.transform, $"MUNDO {world.Number}  <size=70%>· niveles {world.FirstLevel}-{world.LastLevel}</size>", 44f, Color.white);
        UiKit.Outline(worldTitle, 0.18f);
        UiKit.Size(worldTitle, height: 52f);
        string subtitle = worldUnlocked ? world.Name : $"BLOQUEADO · supera el nivel {world.FirstLevel - 1}";
        TMP_Text worldName = UiKit.Text(worldBox.transform, subtitle, 34f, worldUnlocked ? UiKit.Gold : UiKit.Muted);
        UiKit.Outline(worldName, 0.18f);
        UiKit.Size(worldName, height: 42f);

        Button next = UiKit.Button(selector.transform, ">", UiKit.ButtonStyle.Gray, () => ShowWorld(worldNumber + 1), 60f);
        UiKit.Size(next, width: 130f);
        next.interactable = worldNumber < worlds.Count;
    }

    private static void ShowWorld(int number)
    {
        viewedWorld = Mathf.Clamp(number, 1, LevelCatalog.Worlds.Count);
        MenuScreens.Refresh();
    }

    /// <summary>Al jugar un nivel, el mapa vuelve a abrirse en su mundo.</summary>
    public static void FocusLevel(int levelNumber)
    {
        viewedWorld = LevelCatalog.WorldOf(levelNumber).Number;
    }

    /// <summary>Posición del nivel i (1..N dentro del mundo): filas de 4 en serpiente, de arriba hacia abajo.</summary>
    private static Vector2 NodePosition(int indexInWorld)
    {
        int index = indexInWorld - 1;
        int row = index / NodesPerRow;
        int col = index % NodesPerRow;
        if (row % 2 == 1) col = NodesPerRow - 1 - col;

        float x = (col - (NodesPerRow - 1) / 2f) * ColumnSpacing;
        float y = -(MapTopPadding + row * RowSpacing);
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
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
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
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
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
            FocusLevel(n);
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
