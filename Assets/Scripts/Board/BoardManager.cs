using System;
using System.Collections;
using System.Collections.Generic;
using CrystalClash.Board.Logic;
using UnityEngine;

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
    // Entrada
    // ------------------------------------------------------------------

    public void SelectMineral(Mineral mineral)
    {
        if (IsBusy || mineral == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.CanPlay) return;
        if (StoryPopup.IsOpen) return;

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

        yield return AnimateSwap(first, second);

        if (!result.IsValid)
        {
            // Movimiento sin combinación: vuelven a su sitio.
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

        foreach (CascadeStep step in result.Steps)
        {
            if (step.Level > 1 && GameUI.Instance != null)
            {
                GameUI.Instance.ShowCascade(step.Level);
            }

            yield return AnimateClear(step);
            AddScore(step.Points);
            yield return AnimateCreateSpecials(step);
            yield return AnimateFallAndRefill(step);
        }

        if (result.WasShuffled)
        {
            Debug.Log("No había movimientos. Tablero reorganizado.");
            SyncAllTypesFromLogic();
        }

        IsBusy = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMoveFinished();
        }
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
