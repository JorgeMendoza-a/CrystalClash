using System.Collections.Generic;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel superior de la partida, construido por código:
/// [ MOVIMIENTOS ]  [ OBJETIVOS (ícono + 7/20) ]  [ PUNTUACIÓN + 3 estrellas ]
/// Usa el marco del tablero (Resources/Board/board_frame.png) como fondo de las
/// tarjetas, los PNG de los minerales como íconos y Resources/UI/star_*.png.
/// </summary>
public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }

    private static readonly Color LabelColor = new Color(1f, 0.82f, 0.35f);
    private static readonly Color DoneColor = new Color(0.45f, 1f, 0.5f);
    private static readonly Color LowMovesColor = new Color(1f, 0.45f, 0.4f);

    // Posición del panel (en píxeles de una pantalla de referencia 1080x1920).
    private const float TopOffset = 140f;
    private const float PanelHeight = 240f;
    private const int FrameBorderPixels = 48;

    private TMP_Text movesValue;
    private TMP_Text scoreValue;
    private readonly List<Image> stars = new List<Image>();
    private readonly List<TMP_Text> objectiveCounts = new List<TMP_Text>();

    private Sprite cardSprite;
    private Sprite starFull;
    private Sprite starEmpty;
    private LevelSession session;

    /// <summary>Crea el panel para esta partida.</summary>
    public static GameHud Create(LevelSession session)
    {
        var canvasGO = new GameObject("GameHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // debajo de los popups (500)

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // ajustar por ancho (pantalla vertical)

        var hud = canvasGO.AddComponent<GameHud>();
        hud.Build(session);
        return hud;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // Actualizar
    // ------------------------------------------------------------------

    public void SetMoves(int moves)
    {
        if (movesValue == null) return;
        movesValue.text = moves.ToString();
        movesValue.color = moves <= 5 ? LowMovesColor : Color.white;
    }

    public void SetScore(int score)
    {
        if (scoreValue == null) return;
        scoreValue.text = score.ToString();

        if (session == null) return;
        int[] thresholds = session.Level.StarScores;
        for (int i = 0; i < stars.Count && i < thresholds.Length; i++)
        {
            bool earned = score >= thresholds[i];
            stars[i].sprite = earned ? starFull : starEmpty;
            stars[i].enabled = stars[i].sprite != null;
        }
    }

    public void SetObjectives(LevelSession current)
    {
        for (int i = 0; i < objectiveCounts.Count && i < current.Objectives.Count; i++)
        {
            ObjectiveProgress progress = current.Objectives[i];
            TMP_Text text = objectiveCounts[i];

            if (progress.Objective.Kind == ObjectiveKind.ReachScore)
            {
                text.text = progress.IsComplete ? "¡Listo!" : $"{progress.Current}\n<size=70%>/{progress.Objective.Target}</size>";
            }
            else
            {
                int left = progress.Objective.Target - progress.Current;
                text.text = progress.IsComplete ? "¡Listo!" : left.ToString();
            }
            text.color = progress.IsComplete ? DoneColor : Color.white;
        }
    }

    // ------------------------------------------------------------------
    // Construcción
    // ------------------------------------------------------------------

    private void Build(LevelSession current)
    {
        session = current;
        cardSprite = LoadSliced("Board/board_frame");
        starFull = LoadSprite("UI/star_full");
        starEmpty = LoadSprite("UI/star_empty");

        var rowGO = new GameObject("Panel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGO.transform.SetParent(transform, false);
        var row = (RectTransform)rowGO.transform;
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -TopOffset);
        row.sizeDelta = new Vector2(-40f, PanelHeight);

        var layout = rowGO.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        BuildMovesCard(row);
        BuildObjectivesCard(row, current);
        BuildScoreCard(row);

        SetMoves(current.MovesLeft);
        SetObjectives(current);
        SetScore(current.Score);
    }

    private void BuildMovesCard(Transform parent)
    {
        Transform card = CreateCard(parent, "MOVIMIENTOS", 250f, 1f);
        movesValue = CreateText(card, "0", 100f, Color.white, FontStyles.Bold, 120f);
    }

    private void BuildObjectivesCard(Transform parent, LevelSession current)
    {
        Transform card = CreateCard(parent, "OBJETIVOS", 0f, 2f);

        var slotsGO = new GameObject("Objetivos", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        slotsGO.transform.SetParent(card, false);
        slotsGO.GetComponent<LayoutElement>().preferredHeight = 150f;
        var slots = slotsGO.GetComponent<HorizontalLayoutGroup>();
        slots.spacing = 10f;
        slots.childAlignment = TextAnchor.MiddleCenter;
        slots.childControlWidth = true;
        slots.childControlHeight = true;
        slots.childForceExpandWidth = true;
        slots.childForceExpandHeight = true;

        foreach (ObjectiveProgress progress in current.Objectives)
        {
            var slotGO = new GameObject("Objetivo", typeof(RectTransform), typeof(VerticalLayoutGroup));
            slotGO.transform.SetParent(slotsGO.transform, false);
            var slot = slotGO.GetComponent<VerticalLayoutGroup>();
            slot.childAlignment = TextAnchor.MiddleCenter;
            slot.childControlWidth = true;
            slot.childControlHeight = true;
            slot.childForceExpandWidth = true;
            slot.childForceExpandHeight = false;
            slot.spacing = 2f;

            Sprite icon = progress.Objective.Kind == ObjectiveKind.ReachScore
                ? starFull
                : LoadSprite("Minerals/" + MineralFile(progress.Objective.MineralType));
            CreateIcon(slotGO.transform, icon, 84f);

            objectiveCounts.Add(CreateText(slotGO.transform, "", 40f, Color.white, FontStyles.Bold, 60f));
        }
    }

    private void BuildScoreCard(Transform parent)
    {
        Transform card = CreateCard(parent, "PUNTUACIÓN", 250f, 1f);
        scoreValue = CreateText(card, "0", 56f, LabelColor, FontStyles.Bold, 70f);

        var starsGO = new GameObject("Estrellas", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        starsGO.transform.SetParent(card, false);
        starsGO.GetComponent<LayoutElement>().preferredHeight = 64f;
        var row = starsGO.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 6f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;

        for (int i = 0; i < 3; i++)
        {
            Image star = CreateIcon(starsGO.transform, starEmpty, 60f);
            ((RectTransform)star.transform).sizeDelta = new Vector2(60f, 60f);
            stars.Add(star);
        }
    }

    private Transform CreateCard(Transform parent, string title, float width, float flexible)
    {
        var go = new GameObject("Card " + title, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        if (cardSprite != null)
        {
            image.sprite = cardSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.08f, 0.12f, 0.2f, 0.95f);
        }

        var element = go.GetComponent<LayoutElement>();
        if (width > 0f) element.preferredWidth = width;
        element.flexibleWidth = width > 0f ? 0f : flexible;

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 22, 18);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(go.transform, title, 30f, LabelColor, FontStyles.Bold, 40f);
        return go.transform;
    }

    private static TMP_Text CreateText(Transform parent, string text, float size, Color color, FontStyles style, float height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = size;
        return tmp;
    }

    private static Image CreateIcon(Transform parent, Sprite sprite, float height)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    private static string MineralFile(int type)
    {
        string[] files = { "sapphire", "emerald", "amethyst", "ruby", "gold", "carbon", "copper", "silver", "diamond" };
        return type >= 0 && type < files.Length ? files[type] : "";
    }

    private static Sprite LoadSprite(string path)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite LoadSliced(string path)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;
        var border = new Vector4(FrameBorderPixels, FrameBorderPixels, FrameBorderPixels, FrameBorderPixels);
        // 160 px por unidad => el borde de 48 px se ve de ~30 px en el canvas (marco fino).
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
            160f, 0, SpriteMeshType.FullRect, border);
    }
}
