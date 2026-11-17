using System.Collections.Generic;
using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel superior de la partida, construido por código:
/// arriba:  [ MOVIMIENTOS ]  [ OBJETIVOS (ícono + cuántos faltan) ]  [ PUNTUACIÓN + 3 estrellas ]
/// abajo:   [ 🔨 ] [ 💣 ] [ ⚡ ] [ 🔄 ]  potenciadores con su cantidad
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

    private const float BoosterBarBottom = 40f;
    private const float BoosterButtonSize = 190f;

    private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.35f);
    private static readonly Color DisabledColor = new Color(1f, 1f, 1f, 0.35f);

    private class BoosterButton
    {
        public BoosterKind Kind;
        public Button Button;
        public Image Background;
        public Image Icon;
        public TMP_Text Count;
    }

    private readonly List<BoosterButton> boosterButtons = new List<BoosterButton>();
    private TMP_Text boosterHint;
    private BoosterKind? pendingBooster;

    private Sprite cardSprite;
    private Sprite starFull;
    private Sprite starEmpty;
    private LevelSession session;

    private RankedMatchInfo rankedMatch;
    private TMP_Text timerText;
    private TMP_Text opponentScore;
    private TMP_Text opponentMoves;

    /// <summary>Crea el panel para esta partida (match != null = clasificatoria).</summary>
    public static GameHud Create(LevelSession session, RankedMatchInfo match = null)
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
        hud.rankedMatch = match;
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

    /// <summary>Resalta el potenciador armado y muestra la ayuda (null = ninguno).</summary>
    public void SetPendingBooster(BoosterKind? kind)
    {
        pendingBooster = kind;
        RefreshBoosters();

        if (boosterHint == null) return;
        boosterHint.gameObject.SetActive(kind != null);
        if (kind != null)
        {
            boosterHint.text = $"Toca un mineral para usar el <b>{GameManager.BoosterName(kind.Value)}</b>\n<size=75%>(toca el botón otra vez para cancelar)</size>";
        }
    }

    /// <summary>Actualiza cantidades y estado de los botones de potenciadores.</summary>
    public void RefreshBoosters()
    {
        foreach (BoosterButton b in boosterButtons)
        {
            int count = SaveManager.Progress.GetBoosters(b.Kind);
            bool selected = pendingBooster == b.Kind;

            b.Count.text = count.ToString();
            b.Button.interactable = count > 0;
            b.Icon.color = count > 0 ? Color.white : DisabledColor;
            b.Background.color = selected ? SelectedColor : Color.white;
            b.Button.transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }
    }

    // ------------------------------------------------------------------
    // Construcción
    // ------------------------------------------------------------------

    private void BuildBoosterBar()
    {
        var barGO = new GameObject("Potenciadores", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        barGO.transform.SetParent(transform, false);
        var bar = (RectTransform)barGO.transform;
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, BoosterBarBottom);
        bar.sizeDelta = new Vector2(-60f, BoosterButtonSize);

        var layout = barGO.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Sprite badge = LoadSprite("UI/badge");
        string[] icons = { "UI/booster_hammer", "UI/booster_bomb", "UI/booster_lightning", "UI/booster_shuffle" };

        for (int i = 0; i < icons.Length; i++)
        {
            var kind = (BoosterKind)i;
            boosterButtons.Add(CreateBoosterButton(barGO.transform, kind, LoadSprite(icons[i]), badge));
        }

        // Texto de ayuda encima de la barra, visible solo con un potenciador armado.
        boosterHint = CreateText(transform, "", 40f, Color.white, FontStyles.Normal, 110f);
        var hintRect = boosterHint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, BoosterBarBottom + BoosterButtonSize + 20f);
        hintRect.sizeDelta = new Vector2(-80f, 110f);
        boosterHint.gameObject.SetActive(false);

        RefreshBoosters();
    }

    private BoosterButton CreateBoosterButton(Transform parent, BoosterKind kind, Sprite icon, Sprite badge)
    {
        var go = new GameObject("Boton " + kind, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(BoosterButtonSize, BoosterButtonSize);

        var background = go.GetComponent<Image>();
        if (cardSprite != null)
        {
            background.sprite = cardSprite;
            background.type = Image.Type.Sliced;
        }
        else
        {
            background.color = new Color(0.08f, 0.12f, 0.2f, 0.95f);
        }

        var button = go.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.OnBoosterButton(kind);
        });

        Image iconImage = CreateIcon(go.transform, icon, 0f);
        var iconRect = (RectTransform)iconImage.transform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(30f, 30f);
        iconRect.offsetMax = new Vector2(-30f, -30f);

        Image badgeImage = CreateIcon(go.transform, badge, 0f);
        var badgeRect = (RectTransform)badgeImage.transform;
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(-28f, 28f);
        badgeRect.sizeDelta = new Vector2(76f, 76f);
        badgeImage.enabled = true;
        if (badge == null) badgeImage.color = new Color(0.15f, 0.6f, 0.25f);

        TMP_Text count = CreateText(badgeImage.transform, "0", 40f, Color.white, FontStyles.Normal, 0f);
        var countRect = count.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;

        return new BoosterButton { Kind = kind, Button = button, Background = background, Icon = iconImage, Count = count };
    }

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
        if (rankedMatch != null)
        {
            BuildOpponentCard(row);
        }
        else
        {
            BuildObjectivesCard(row, current);
        }
        BuildScoreCard(row);

        // En clasificatoria no hay potenciadores (las dos partidas deben ser iguales).
        if (rankedMatch == null) BuildBoosterBar();

        SetMoves(current.MovesLeft);
        SetObjectives(current);
        SetScore(current.Score);
    }

    private void BuildMovesCard(Transform parent)
    {
        Transform card = CreateCard(parent, "MOVIMIENTOS", 250f, 1f);
        if (rankedMatch == null)
        {
            movesValue = CreateText(card, "0", 100f, Color.white, FontStyles.Normal, 120f);
            return;
        }

        movesValue = CreateText(card, "0", 80f, Color.white, FontStyles.Normal, 96f);
        timerText = CreateText(card, "", 34f, LabelColor, FontStyles.Normal, 44f);
    }

    private void BuildOpponentCard(Transform parent)
    {
        Transform card = CreateCard(parent, "RIVAL", 0f, 2f);
        CreateText(card, rankedMatch.OpponentName, 38f, Color.white, FontStyles.Normal, 48f);
        opponentScore = CreateText(card, "0", 60f, new Color(1f, 0.55f, 0.5f), FontStyles.Normal, 66f);
        opponentMoves = CreateText(card, $"Movimientos: {CrystalClash.Ranked.Logic.RankedRules.Moves}", 28f, Color.white, FontStyles.Normal, 36f);
    }

    /// <summary>Clasificatoria: puntaje del rival en vivo.</summary>
    public void SetOpponent(int score, int movesLeft, bool finished)
    {
        if (opponentScore == null) return;
        opponentScore.text = score.ToString();
        opponentMoves.text = finished ? "¡Terminó!" : $"Movimientos: {movesLeft}";
    }

    /// <summary>Clasificatoria: tiempo restante.</summary>
    public void SetTimer(float secondsLeft)
    {
        if (timerText == null) return;
        int total = Mathf.CeilToInt(secondsLeft);
        timerText.text = $"{total / 60}:{total % 60:00}";
        timerText.color = total <= 20 ? LowMovesColor : LabelColor;
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

            objectiveCounts.Add(CreateText(slotGO.transform, "", 40f, Color.white, FontStyles.Normal, 60f));
        }
    }

    private void BuildScoreCard(Transform parent)
    {
        Transform card = CreateCard(parent, "PUNTUACIÓN", 250f, 1f);
        scoreValue = CreateText(card, "0", 56f, LabelColor, FontStyles.Normal, 70f);

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

        CreateText(go.transform, title, 30f, LabelColor, FontStyles.Normal, 40f);
        return go.transform;
    }

    private static TMP_Text CreateText(Transform parent, string text, float size, Color color, FontStyles style, float height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        UiKit.ApplyFont(tmp);
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
        UiKit.Outline(tmp, 0.15f);
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
