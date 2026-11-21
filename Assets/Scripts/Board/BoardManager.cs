using System;
using System.Collections;
using System.Collections.Generic;
using CrystalClash.Board.Logic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Vista del tablero: crea los GameObject de los minerales, recibe los clics
/// y anima lo que BoardLogic decide (incluidos los especiales y combos). Aquí NO hay reglas del juego; todas
/// viven en BoardLogic (C# puro y determinista).
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("Tablero")]
    public int width = 8;
    public int height = 8;

    [Tooltip("Cuántos tipos de mineral se usan (los primeros N del enum MineralType). 5-6 es lo ideal en 8x8.")]
    [Range(3, 9)]
    public int mineralTypeCount = 6;

    [Tooltip("Semilla del tablero. 0 = aleatoria en cada partida. Con la misma semilla sale el mismo tablero.")]
    public int seed = 0;

    public GameObject mineralPrefab;

    [Header("Toques")]
    [Tooltip("Cuánto hay que deslizar el dedo (en celdas) para cambiar un mineral con su vecino.")]
    public float swipeThreshold = 0.35f;

    [Header("Marco del tablero")]
    [Tooltip("Dibuja un marco y casillas detrás de los minerales (Resources/Board/*.png).")]
    public bool showBoardFrame = true;

    [Tooltip("Grosor del marco alrededor de las casillas, en celdas.")]
    public float frameThickness = 0.42f;

    [Header("Cámara")]
    [Tooltip("Ajusta la cámara para que el tablero entero quepa en pantalla (vertical u horizontal).")]
    public bool fitCameraToBoard = true;

    [Tooltip("Espacio extra alrededor del tablero, en celdas.")]
    public float cameraPadding = 0.3f;

    [Header("Animaciones (segundos)")]
    public float swapDuration = 0.15f;
    public float clearDuration = 0.15f;
    public float fallDuration = 0.25f;
    public float createDuration = 0.12f;

    public BoardLogic Logic { get; private set; }

    /// <summary>True mientras se anima un movimiento (se ignoran los clics).</summary>
    public bool IsBusy { get; private set; }

    private Mineral[,] views;
    private Mineral selectedMineral;

    void Start()
    {
        int maxTypes = Enum.GetValues(typeof(MineralType)).Length;
        int types = Mathf.Clamp(mineralTypeCount, BoardLogic.MinMineralTypes, maxTypes);
        int usedSeed = seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue);

        Logic = new BoardLogic(width, height, types, usedSeed);
        Debug.Log($"Tablero {width}x{height} con {types} minerales. Semilla: {usedSeed}");

        views = new Mineral[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                views[x, y] = CreateMineralView(x, y, Logic.GetMineral(x, y), Logic.GetSpecial(x, y), GetWorldPosition(x, y));
            }
        }

        if (GameUI.Instance != null)
        {
            GameUI.Instance.UpdateScore(Logic.Score);
        }

        if (showBoardFrame) BuildBoardBackground();
        FitCamera();
    }

    // Orden de dibujo: fondo de la UI (negativo) < marco < casillas < minerales (Mineral.SortingOrder).
    // Son positivos para que el tablero quede encima del fondo aunque el Canvas cambie.
    private const int FrameSortingOrder = 10;
    private const int CellSortingOrder = 20;
    private const int FrameBorderPixels = 48; // igual que FRAME_BORDER en Tools/gen_gems.py

    void BuildBoardBackground()
    {
        Texture2D frameTexture = Resources.Load<Texture2D>("Board/board_frame");
        Texture2D cellTexture = Resources.Load<Texture2D>("Board/board_cell");
        Texture2D cellAltTexture = Resources.Load<Texture2D>("Board/board_cell_alt");

        var root = new GameObject("BoardBackground");
        root.transform.SetParent(transform, false);

        if (frameTexture != null)
        {
            // 9-slice: las esquinas no se estiran, solo los lados y el centro.
            float pixelsPerUnit = FrameBorderPixels / frameThickness;
            var border = new Vector4(FrameBorderPixels, FrameBorderPixels, FrameBorderPixels, FrameBorderPixels);
            Sprite frameSprite = Sprite.Create(frameTexture, new Rect(0, 0, frameTexture.width, frameTexture.height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, border);

            var frameGO = new GameObject("Frame");
            frameGO.transform.SetParent(root.transform, false);
            frameGO.transform.position = GetWorldPosition((width - 1) / 2f, (height - 1) / 2f);
            var frame = frameGO.AddComponent<SpriteRenderer>();
            frame.sprite = frameSprite;
            frame.drawMode = SpriteDrawMode.Sliced;
            frame.size = new Vector2(width + frameThickness * 2f, height + frameThickness * 2f);
            frame.sortingOrder = FrameSortingOrder;
        }

        if (cellTexture != null)
        {
            Sprite cell = CreateCellSprite(cellTexture);
            Sprite cellAlt = cellAltTexture != null ? CreateCellSprite(cellAltTexture) : cell;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cellGO = new GameObject($"Cell {x},{y}");
                    cellGO.transform.SetParent(root.transform, false);
                    cellGO.transform.position = GetWorldPosition(x, y);
                    var renderer = cellGO.AddComponent<SpriteRenderer>();
                    renderer.sprite = (x + y) % 2 == 0 ? cell : cellAlt;
                    renderer.sortingOrder = CellSortingOrder;
                }
            }
        }
    }

    static Sprite CreateCellSprite(Texture2D texture)
    {
        // Una casilla mide exactamente 1 celda.
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }

    void FitCamera()
    {
        if (!fitCameraToBoard) return;

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float frame = showBoardFrame ? frameThickness : 0f;
        float neededHalfHeight = height / 2f + frame + cameraPadding;
        float neededHalfWidth = width / 2f + frame + cameraPadding;

        cam.orthographicSize = Mathf.Max(neededHalfHeight, neededHalfWidth / cam.aspect);
        cam.transform.position = new Vector3(0f, 0f, cam.transform.position.z);
    }

    // ------------------------------------------------------------------
    // Entrada (mouse y pantalla táctil, con el Input System)
    // ------------------------------------------------------------------

    private Mineral pressedMineral;
    private Vector2 pressWorld;
    private bool swipeDone;
    private bool tapToDeselect;

    void Update()
    {
        Pointer pointer = Pointer.current;
        Camera cam = Camera.main;
        if (pointer == null || cam == null || Logic == null) return;

        Vector2 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());

        if (pointer.press.wasPressedThisFrame)
        {
            OnPress(MineralAt(world), world);
        }
        else if (pointer.press.isPressed && pressedMineral != null && !swipeDone)
        {
            TrySwipe(world);
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            // Tocar el mineral ya seleccionado (sin deslizar) lo deselecciona.
            if (tapToDeselect && !swipeDone && pressedMineral != null && selectedMineral == pressedMineral)
            {
                SelectMineral(pressedMineral);
            }
            pressedMineral = null;
            tapToDeselect = false;
        }
    }

    void OnPress(Mineral mineral, Vector2 world)
    {
        pressedMineral = null;
        swipeDone = false;
        tapToDeselect = false;
        if (mineral == null) return;

        if (selectedMineral == mineral)
        {
            tapToDeselect = true;
        }
        else
        {
            SelectMineral(mineral);
        }

        // Si quedó seleccionado, se puede deslizar desde él.
        if (selectedMineral == mineral)
        {
            pressedMineral = mineral;
            pressWorld = world;
        }
    }

    void TrySwipe(Vector2 world)
    {
        Vector2 delta = world - pressWorld;
        if (delta.magnitude < swipeThreshold) return;

        swipeDone = true;
        int dx = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x > 0 ? 1 : -1) : 0;
        int dy = dx == 0 ? (delta.y > 0 ? 1 : -1) : 0;
        int nx = pressedMineral.x + dx;
        int ny = pressedMineral.y + dy;

        if (selectedMineral != pressedMineral || nx < 0 || nx >= width || ny < 0 || ny >= height) return;

        Mineral neighbor = views[nx, ny];
        if (neighbor != null) SelectMineral(neighbor);
    }

    static Mineral MineralAt(Vector2 world)
    {
        Collider2D hit = Physics2D.OverlapPoint(world);
        return hit != null ? hit.GetComponent<Mineral>() : null;
    }

    public void SelectMineral(Mineral mineral)
    {
        if (IsBusy || mineral == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.CanPlay) return;
        if (StoryPopup.IsOpen || MenuScreens.IsOpen) return;

        // Si hay un potenciador elegido (Martillo, Bomba, Rayo), este clic lo usa.
        if (GameManager.Instance != null && GameManager.Instance.TryConsumePendingBooster(new GridPos(mineral.x, mineral.y)))
        {
            if (selectedMineral != null)
            {
                selectedMineral.Deselect();
                selectedMineral = null;
            }
            return;
        }

        if (selectedMineral == null)
        {
            selectedMineral = mineral;
            selectedMineral.Select();
            return;
        }

        Mineral first = selectedMineral;
        first.Deselect();
        selectedMineral = null;

        if (first == mineral) return;

        var a = new GridPos(first.x, first.y);
        var b = new GridPos(mineral.x, mineral.y);

        if (a.IsAdjacentTo(b))
        {
            StartCoroutine(SwapRoutine(first, mineral));
        }
        else
        {
            // Clic en otro mineral lejano: lo seleccionamos a él.
            selectedMineral = mineral;
            selectedMineral.Select();
        }
    }

    // ------------------------------------------------------------------
    // Animación de un movimiento completo
    // ------------------------------------------------------------------

    IEnumerator SwapRoutine(Mineral first, Mineral second)
    {
        IsBusy = true;

        var a = new GridPos(first.x, first.y);
        var b = new GridPos(second.x, second.y);

        SwapResult result = Logic.TrySwap(a, b);

        Sound.Play("swap", 1f, 0.6f);
        yield return AnimateSwap(first, second);

        if (!result.IsValid)
        {
            // Movimiento sin combinación: vuelven a su sitio.
            Sound.Play("invalid", 1f, 0.7f);
            yield return AnimateSwap(first, second);
            IsBusy = false;
            yield break;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterMove(result);
        }

        views[a.X, a.Y] = second;
        views[b.X, b.Y] = first;
        first.SetPosition(b.X, b.Y);
        second.SetPosition(a.X, a.Y);

        yield return AnimateSteps(result);

        IsBusy = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMoveFinished();
        }
    }

    /// <summary>
    /// Final de nivel: convierte los movimientos sobrantes en bombas (una a
    /// una, bajando el contador) y las hace explotar. Al terminar avisa a
    /// GameManager.OnFinaleFinished. Devuelve false si no se pudo empezar.
    /// </summary>
    public bool PlayFinale(int moves)
    {
        if (IsBusy || Logic == null || moves <= 0) return false;

        if (selectedMineral != null)
        {
            selectedMineral.Deselect();
            selectedMineral = null;
        }

        StartCoroutine(FinaleRoutine(moves));
        return true;
    }

    IEnumerator FinaleRoutine(int moves)
    {
        IsBusy = true;
        SwapResult result = Logic.PlayFinale(moves);

        int left = moves;
        foreach (GridPos pos in result.FinaleBombs)
        {
            Mineral mineral = views[pos.X, pos.Y];
            if (mineral != null)
            {
                mineral.SetPiece((int)mineral.mineralType, SpecialKind.Bomb);
                StartCoroutine(Pop(mineral.transform));
                // Cada bomba nueva suena un poco más aguda.
                Sound.Play("star", Mathf.Min(2f, 0.9f + 0.06f * (moves - left)), 0.6f);
            }
            left--;
            if (GameHud.Instance != null) GameHud.Instance.SetMoves(left);
            yield return new WaitForSeconds(0.09f);
        }

        yield return new WaitForSeconds(0.35f);
        yield return AnimateSteps(result);

        IsBusy = false;
        if (GameManager.Instance != null) GameManager.Instance.OnFinaleFinished(result);
    }

    IEnumerator Pop(Transform target)
    {
        Vector3 baseScale = target.localScale;
        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
        {
            if (target == null) yield break;
            float k = 1f + Mathf.Sin(t / 0.18f * Mathf.PI) * 0.35f;
            target.localScale = baseScale * k;
            yield return null;
        }
        if (target != null) target.localScale = baseScale;
    }

    /// <summary>Usa un potenciador y lo anima. Devuelve false si no se pudo (tablero ocupado).</summary>
    public bool PlayBooster(BoosterKind kind, GridPos target)
    {
        if (IsBusy || Logic == null) return false;

        if (selectedMineral != null)
        {
            selectedMineral.Deselect();
            selectedMineral = null;
        }

        StartCoroutine(BoosterRoutine(kind, target));
        return true;
    }

    IEnumerator BoosterRoutine(BoosterKind kind, GridPos target)
    {
        IsBusy = true;

        SwapResult result = Logic.UseBooster(kind, target);
        if (!result.IsValid)
        {
            IsBusy = false;
            yield break;
        }

        Sound.Play("booster");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterBooster(kind, result);
        }

        yield return AnimateSteps(result);

        IsBusy = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMoveFinished();
        }
    }

    /// <summary>Anima las olas de una jugada o potenciador (romper, crear especiales, caer) y la reorganización.</summary>
    IEnumerator AnimateSteps(SwapResult result)
    {
        foreach (CascadeStep step in result.Steps)
        {
            if (step.Level > 1 && GameUI.Instance != null)
            {
                GameUI.Instance.ShowCascade(step.Level);
            }

            PlayStepSounds(step);
            yield return AnimateClear(step);
            AddScore(step.Points);
            if (step.Created.Count > 0) Sound.Play("special_create");
            yield return AnimateCreateSpecials(step);
            yield return AnimateFallAndRefill(step);
        }

        if (result.WasShuffled)
        {
            yield return AnimateShuffle();
        }
    }

    /// <summary>
    /// Sonidos de una ola: la campanita sube de tono con cada cascada, y cada
    /// especial que explota suena con lo suyo (taladro, dinamita, prisma).
    /// </summary>
    static void PlayStepSounds(CascadeStep step)
    {
        Sound.Play("match", Mathf.Min(1.8f, 1f + 0.1f * (step.Level - 1)), 0.8f);

        bool line = false, bomb = false, prism = false;
        foreach (SpecialActivation activation in step.Activations)
        {
            switch (activation.Kind)
            {
                case SpecialKind.LineH:
                case SpecialKind.LineV: line = true; break;
                case SpecialKind.Bomb: bomb = true; break;
                case SpecialKind.Prism: prism = true; break;
            }
        }
        if (line) Sound.Play("line", 1f, 0.8f);
        if (bomb) Sound.Play("bomb", 1f, 0.9f);
        if (prism) Sound.Play("prism");
    }

    IEnumerator AnimateShuffle()
    {
        // Se encogen, cambian y vuelven a crecer.
        float half = 0.12f;
        var scales = new Dictionary<Mineral, Vector3>();
        foreach (Mineral mineral in views)
        {
            if (mineral != null) scales[mineral] = mineral.transform.localScale;
        }

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float k = 1f - t / half;
            foreach (var pair in scales) pair.Key.transform.localScale = pair.Value * k;
            yield return null;
        }

        SyncAllTypesFromLogic();

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float k = t / half;
            foreach (var pair in scales) pair.Key.transform.localScale = pair.Value * k;
            yield return null;
        }

        foreach (var pair in scales) pair.Key.transform.localScale = pair.Value;
    }

    IEnumerator AnimateSwap(Mineral first, Mineral second)
    {
        var moves = new List<(Transform, Vector3)>
        {
            (first.transform, second.transform.position),
            (second.transform, first.transform.position)
        };
        yield return MoveAll(moves, swapDuration);
    }

    IEnumerator AnimateClear(CascadeStep step)
    {
        var cleared = new List<Mineral>(step.Cleared.Count);
        foreach (GridPos pos in step.Cleared)
        {
            Mineral mineral = views[pos.X, pos.Y];
            views[pos.X, pos.Y] = null;
            if (mineral != null) cleared.Add(mineral);
        }

        var startScales = new List<Vector3>(cleared.Count);
        foreach (Mineral mineral in cleared) startScales.Add(mineral.transform.localScale);

        for (float t = 0f; t < clearDuration; t += Time.deltaTime)
        {
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / clearDuration);
            for (int i = 0; i < cleared.Count; i++)
            {
                cleared[i].transform.localScale = startScales[i] * k;
            }
            yield return null;
        }

        foreach (Mineral mineral in cleared) Destroy(mineral.gameObject);
    }

    IEnumerator AnimateCreateSpecials(CascadeStep step)
    {
        if (step.Created.Count == 0) yield break;

        var created = new List<Mineral>(step.Created.Count);
        foreach (SpecialCreation creation in step.Created)
        {
            GridPos pos = creation.Position;
            Mineral mineral = CreateMineralView(pos.X, pos.Y, creation.Type, creation.Kind, GetWorldPosition(pos.X, pos.Y));
            views[pos.X, pos.Y] = mineral;
            created.Add(mineral);
        }

        var targetScales = new List<Vector3>(created.Count);
        foreach (Mineral mineral in created) targetScales.Add(mineral.transform.localScale);

        for (float t = 0f; t < createDuration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / createDuration);
            for (int i = 0; i < created.Count; i++)
            {
                created[i].transform.localScale = targetScales[i] * k;
            }
            yield return null;
        }

        for (int i = 0; i < created.Count; i++) created[i].transform.localScale = targetScales[i];
    }

    IEnumerator AnimateFallAndRefill(CascadeStep step)
    {
        var moves = new List<(Transform, Vector3)>();

        // Las caídas vienen ordenadas de abajo hacia arriba por columna,
        // así que la celda destino siempre está libre al moverla.
        foreach (MineralFall fall in step.Falls)
        {
            Mineral mineral = views[fall.From.X, fall.From.Y];
            views[fall.From.X, fall.From.Y] = null;
            views[fall.To.X, fall.To.Y] = mineral;
            mineral.SetPosition(fall.To.X, fall.To.Y);
            moves.Add((mineral.transform, GetWorldPosition(fall.To.X, fall.To.Y)));
        }

        foreach (MineralSpawn spawn in step.Spawns)
        {
            Vector3 start = GetWorldPosition(spawn.Position.X, spawn.StartY);
            Mineral mineral = CreateMineralView(spawn.Position.X, spawn.Position.Y, spawn.Type, SpecialKind.None, start);
            views[spawn.Position.X, spawn.Position.Y] = mineral;
            moves.Add((mineral.transform, GetWorldPosition(spawn.Position.X, spawn.Position.Y)));
        }

        yield return MoveAll(moves, fallDuration);
    }

    IEnumerator MoveAll(List<(Transform transform, Vector3 target)> moves, float duration)
    {
        var starts = new Vector3[moves.Count];
        for (int i = 0; i < moves.Count; i++) starts[i] = moves[i].transform.position;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            for (int i = 0; i < moves.Count; i++)
            {
                moves[i].transform.position = Vector3.LerpUnclamped(starts[i], moves[i].target, k);
            }
            yield return null;
        }

        for (int i = 0; i < moves.Count; i++) moves[i].transform.position = moves[i].target;
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    Mineral CreateMineralView(int x, int y, int type, SpecialKind special, Vector3 worldPosition)
    {
        GameObject obj = Instantiate(mineralPrefab, worldPosition, Quaternion.identity, transform);
        Mineral mineral = obj.GetComponent<Mineral>();
        mineral.SetPosition(x, y);
        mineral.SetPiece(type, special);
        return mineral;
    }

    void SyncAllTypesFromLogic()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                views[x, y].SetPiece(Logic.GetMineral(x, y), Logic.GetSpecial(x, y));
            }
        }
    }

    void AddScore(int points)
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(points);
        }
        else if (GameUI.Instance != null)
        {
            GameUI.Instance.UpdateScore(Logic.Score);
        }
    }

    Vector3 GetWorldPosition(float x, float y)
    {
        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        return new Vector3(x - offsetX, y - offsetY, 0f);
    }
}
