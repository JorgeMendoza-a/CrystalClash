using System;
using System.Collections.Generic;

namespace CrystalClash.Board.Logic
{
    /// <summary>
    /// Toda la lógica del tablero match-3, en C# puro (sin Unity).
    ///
    /// - Determinista: misma semilla + mismos movimientos = mismo resultado.
    ///   Es la base del modo clasificatoria (los dos jugadores reciben el mismo
    ///   tablero) y de la validación en servidor (se repiten los movimientos).
    /// - Cada celda tiene un tipo (0..MineralTypeCount-1, igual que el enum
    ///   MineralType; Empty = vacía; Colorless = Prisma) y un SpecialKind.
    /// - (0,0) es la celda de abajo a la izquierda; las piezas caen hacia Y=0.
    ///
    /// Especiales:
    /// - 4 en línea          -> Taladro (LineH / LineV): limpia fila / columna.
    /// - L o T               -> Dinamita (Bomb): limpia 3x3.
    /// - 5 en línea          -> Prisma (Prism): al intercambiarlo limpia un color.
    /// - Especial + especial -> combos más grandes (ver ApplySwapCombo).
    /// Cada ola de la cascada multiplica sus puntos por su nivel (x1, x2, x3...).
    /// </summary>
    public sealed class BoardLogic
    {
        public const int Empty = -1;
        public const int Colorless = -2;
        public const int MinMineralTypes = 3;
        public const int MinSize = 3;
        public const int PointsPerMineral = 10;

        private const int MaxGenerationAttempts = 100;

        private readonly int[,] types;
        private readonly SpecialKind[,] specials;
        private readonly DeterministicRandom rng;

        public int Width { get; }
        public int Height { get; }
        public int MineralTypeCount { get; }
        public int Seed { get; }
        public int Score { get; private set; }
        public int MovesMade { get; private set; }

        public BoardLogic(int width, int height, int mineralTypeCount, int seed)
            : this(width, height, mineralTypeCount, seed, generate: true)
        {
        }

        private BoardLogic(int width, int height, int mineralTypeCount, int seed, bool generate)
        {
            if (width < MinSize) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < MinSize) throw new ArgumentOutOfRangeException(nameof(height));
            if (mineralTypeCount < MinMineralTypes) throw new ArgumentOutOfRangeException(nameof(mineralTypeCount));

            Width = width;
            Height = height;
            MineralTypeCount = mineralTypeCount;
            Seed = seed;

            types = new int[width, height];
            specials = new SpecialKind[width, height];
            rng = new DeterministicRandom(seed);

            if (generate) GenerateInitialBoard();
        }

        /// <summary>
        /// Crea un tablero con un diseño concreto (tests, niveles hechos a mano).
        /// layoutTypes[x, y]; layoutSpecials puede ser null. No se valida que
        /// tenga movimientos: eso es responsabilidad de quien diseña el nivel.
        /// </summary>
        public static BoardLogic FromLayout(int[,] layoutTypes, SpecialKind[,] layoutSpecials, int mineralTypeCount, int seed)
        {
            int width = layoutTypes.GetLength(0);
            int height = layoutTypes.GetLength(1);
            var board = new BoardLogic(width, height, mineralTypeCount, seed, generate: false);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    SpecialKind special = layoutSpecials != null ? layoutSpecials[x, y] : SpecialKind.None;
                    int type = special == SpecialKind.Prism ? Colorless : layoutTypes[x, y];

                    if (special != SpecialKind.Prism && (type < 0 || type >= mineralTypeCount))
                    {
                        throw new ArgumentException($"Tipo inválido {type} en ({x},{y})");
                    }

                    board.types[x, y] = type;
                    board.specials[x, y] = special;
                }
            }

            return board;
        }

        // ------------------------------------------------------------------
        // Consulta
        // ------------------------------------------------------------------

        public int GetMineral(int x, int y) => types[x, y];

        public int GetMineral(GridPos pos) => types[pos.X, pos.Y];

        public SpecialKind GetSpecial(int x, int y) => specials[x, y];

        public SpecialKind GetSpecial(GridPos pos) => specials[pos.X, pos.Y];

        public bool IsInside(GridPos pos)
        {
            return pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;
        }

        /// <summary>Todas las celdas que forman parte de una línea de 3 o más.</summary>
        public List<GridPos> FindMatches()
        {
            bool[,] marked = new bool[Width, Height];
            foreach (Run run in FindRuns())
            {
                foreach (GridPos pos in run.Cells) marked[pos.X, pos.Y] = true;
            }

            var matches = new List<GridPos>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (marked[x, y]) matches.Add(new GridPos(x, y));
                }
            }
            return matches;
        }

        public bool HasAvailableMoves()
        {
            return TryFindMove(out _, out _);
        }

        /// <summary>Busca un intercambio válido (útil también para pistas).</summary>
        public bool TryFindMove(out GridPos a, out GridPos b)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var from = new GridPos(x, y);

                    var right = new GridPos(x + 1, y);
                    if (IsInside(right) && IsValidSwap(from, right))
                    {
                        a = from;
                        b = right;
                        return true;
                    }

                    var up = new GridPos(x, y + 1);
                    if (IsInside(up) && IsValidSwap(from, up))
                    {
                        a = from;
                        b = up;
                        return true;
                    }
                }
            }

            a = default;
            b = default;
            return false;
        }

        // ------------------------------------------------------------------
        // Acciones
        // ------------------------------------------------------------------

        /// <summary>
        /// Intenta intercambiar A con B (A es el mineral que el jugador mueve
        /// hacia B). Si no es válido, el tablero queda igual y IsValid es false.
        /// Si es válido, resuelve toda la cascada y la devuelve paso a paso.
        /// </summary>
        public SwapResult TrySwap(GridPos a, GridPos b)
        {
            var result = new SwapResult(a, b);

            if (!IsInside(a) || !IsInside(b) || !a.IsAdjacentTo(b)) return result;
            if (types[a.X, a.Y] == Empty || types[b.X, b.Y] == Empty) return result;

            bool isCombo = IsSpecialCombo(a, b);
            Swap(a, b);

            List<Run> runs = FindRuns();
            if (!isCombo && runs.Count == 0)
            {
                Swap(a, b);
                return result;
            }

            result.IsValid = true;
            result.WasSpecialCombo = isCombo;
            MovesMade++;

            int level = 1;
            CascadeStep step = isCombo
                ? ExecuteComboStep(level, a, b)
                : ExecuteMatchStep(level, runs, a, b);
            AddStep(result, step);

            runs = FindRuns();
            while (runs.Count > 0)
            {
                level++;
                AddStep(result, ExecuteMatchStep(level, runs, null, null));
                runs = FindRuns();
            }

            if (!HasAvailableMoves())
            {
                Shuffle();
                result.WasShuffled = true;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Resolución de una ola
        // ------------------------------------------------------------------

        private sealed class Run
        {
            public readonly List<GridPos> Cells = new List<GridPos>();
            public bool Horizontal;
            public int Type;
        }

        /// <summary>Estado temporal mientras se resuelve una ola.</summary>
        private sealed class StepContext
        {
            public readonly CascadeStep Step;
            public readonly bool[,] Marked;
            public readonly bool[,] Consumed;
            public readonly Queue<GridPos> Queue = new Queue<GridPos>();

            public StepContext(CascadeStep step, int width, int height)
            {
                Step = step;
                Marked = new bool[width, height];
                Consumed = new bool[width, height];
            }
        }

        private void AddStep(SwapResult result, CascadeStep step)
        {
            Collapse(step);
            Refill(step);
            result.Steps.Add(step);
            result.PointsGained += step.Points;
        }

        /// <summary>Ola normal: limpia las líneas, activa especiales en cadena y crea especiales nuevos.</summary>
        private CascadeStep ExecuteMatchStep(int level, List<Run> runs, GridPos? swapA, GridPos? swapB)
        {
            var ctx = new StepContext(new CascadeStep(level), Width, Height);

            List<SpecialCreation> creations = PlanCreations(runs, swapA, swapB);

            foreach (Run run in runs)
            {
                foreach (GridPos pos in run.Cells) Mark(ctx, pos);
            }

            FinishStep(ctx, creations);
            return ctx.Step;
        }

        /// <summary>Ola de un intercambio de especiales (Prisma o dos especiales).</summary>
        private CascadeStep ExecuteComboStep(int level, GridPos a, GridPos b)
        {
            var ctx = new StepContext(new CascadeStep(level), Width, Height);
            ApplySwapCombo(ctx, a, b);
            FinishStep(ctx, new List<SpecialCreation>());
            return ctx.Step;
        }

        private void FinishStep(StepContext ctx, List<SpecialCreation> creations)
        {
            ProcessQueue(ctx);

            foreach (GridPos pos in ctx.Step.Cleared)
            {
                ctx.Step.ClearedTypes.Add(types[pos.X, pos.Y]);
                types[pos.X, pos.Y] = Empty;
                specials[pos.X, pos.Y] = SpecialKind.None;
            }

            foreach (SpecialCreation creation in creations)
            {
                types[creation.Position.X, creation.Position.Y] = creation.Type;
                specials[creation.Position.X, creation.Position.Y] = creation.Kind;
                ctx.Step.Created.Add(creation);
            }

            ctx.Step.Points = ctx.Step.Cleared.Count * PointsPerMineral * ctx.Step.Level;
            Score += ctx.Step.Points;
        }

        private void Mark(StepContext ctx, GridPos pos)
        {
            if (!IsInside(pos) || ctx.Marked[pos.X, pos.Y] || types[pos.X, pos.Y] == Empty) return;

            ctx.Marked[pos.X, pos.Y] = true;
            ctx.Queue.Enqueue(pos);
        }

        /// <summary>Limpia todo lo marcado; cada especial alcanzado se activa y marca más celdas.</summary>
        private void ProcessQueue(StepContext ctx)
        {
            while (ctx.Queue.Count > 0)
            {
                GridPos pos = ctx.Queue.Dequeue();
                ctx.Step.Cleared.Add(pos);

                SpecialKind special = specials[pos.X, pos.Y];
                if (special != SpecialKind.None && !ctx.Consumed[pos.X, pos.Y])
                {
                    ctx.Consumed[pos.X, pos.Y] = true;
                    ctx.Step.Activations.Add(new SpecialActivation(pos, special));
                    ActivateSpecial(ctx, pos, special);
                }
            }
        }

        private void ActivateSpecial(StepContext ctx, GridPos pos, SpecialKind special)
        {
            switch (special)
            {
                case SpecialKind.LineH:
                    MarkRow(ctx, pos.Y);
                    break;

                case SpecialKind.LineV:
                    MarkColumn(ctx, pos.X);
                    break;

                case SpecialKind.Bomb:
                    MarkArea(ctx, pos, 1);
                    break;

                case SpecialKind.Prism:
                    // Activado en cadena (no por intercambio): elige un color al azar
                    // entre los que quedan en el tablero.
                    int color = PickRemainingColor(ctx);
                    if (color >= 0) MarkColor(ctx, color);
                    break;
            }
        }

        /// <summary>
        /// Intercambio especial. Tras el Swap, en B está el mineral que movió el
        /// jugador; B es el centro de los efectos.
        /// </summary>
        private void ApplySwapCombo(StepContext ctx, GridPos a, GridPos b)
        {
            SpecialKind sa = specials[a.X, a.Y];
            SpecialKind sb = specials[b.X, b.Y];

            ctx.Consumed[a.X, a.Y] = true;
            ctx.Consumed[b.X, b.Y] = true;
            Mark(ctx, a);
            Mark(ctx, b);

            if (sa == SpecialKind.Prism && sb == SpecialKind.Prism)
            {
                ctx.Step.Activations.Add(new SpecialActivation(b, SpecialKind.Prism));
                ctx.Step.Activations.Add(new SpecialActivation(a, SpecialKind.Prism));
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++) Mark(ctx, new GridPos(x, y));
                }
                return;
            }

            if (sa == SpecialKind.Prism || sb == SpecialKind.Prism)
            {
                GridPos prism = sa == SpecialKind.Prism ? a : b;
                GridPos other = sa == SpecialKind.Prism ? b : a;
                SpecialKind otherKind = specials[other.X, other.Y];
                int color = types[other.X, other.Y];

                ctx.Step.Activations.Add(new SpecialActivation(prism, SpecialKind.Prism));

                if (otherKind == SpecialKind.None)
                {
                    MarkColor(ctx, color);
                    return;
                }

                // Prisma + especial: todo ese color se convierte en ese especial y se activa.
                ctx.Consumed[other.X, other.Y] = false;
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        if (types[x, y] != color) continue;

                        if (specials[x, y] == SpecialKind.None)
                        {
                            specials[x, y] = IsLine(otherKind)
                                ? (rng.Next(2) == 0 ? SpecialKind.LineH : SpecialKind.LineV)
                                : otherKind;
                        }
                        Mark(ctx, new GridPos(x, y));
                    }
                }
                return;
            }

            // Dos especiales (sin Prisma).
            ctx.Step.Activations.Add(new SpecialActivation(b, specials[b.X, b.Y]));
            ctx.Step.Activations.Add(new SpecialActivation(a, specials[a.X, a.Y]));

            bool lineA = IsLine(sa);
            bool lineB = IsLine(sb);

            if (lineA && lineB)
            {
                MarkRow(ctx, b.Y);
                MarkColumn(ctx, b.X);
            }
            else if (sa == SpecialKind.Bomb && sb == SpecialKind.Bomb)
            {
                MarkArea(ctx, b, 2);
            }
            else
            {
                // Taladro + Dinamita: 3 filas y 3 columnas.
                for (int d = -1; d <= 1; d++)
                {
                    if (b.Y + d >= 0 && b.Y + d < Height) MarkRow(ctx, b.Y + d);
                    if (b.X + d >= 0 && b.X + d < Width) MarkColumn(ctx, b.X + d);
                }
            }
        }

        /// <summary>
        /// Decide qué especiales nacen de las líneas de esta ola y dónde.
        /// Las líneas que comparten celdas forman un grupo (así se detectan L y T).
        /// </summary>
        private List<SpecialCreation> PlanCreations(List<Run> runs, GridPos? swapA, GridPos? swapB)
        {
            var creations = new List<SpecialCreation>();
            if (runs.Count == 0) return creations;

            // Union-find de líneas que comparten alguna celda.
            int[] parent = new int[runs.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            var owner = new int[Width, Height];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++) owner[x, y] = -1;
            }

            for (int r = 0; r < runs.Count; r++)
            {
                foreach (GridPos pos in runs[r].Cells)
                {
                    int existing = owner[pos.X, pos.Y];
                    if (existing == -1) owner[pos.X, pos.Y] = r;
                    else parent[FindRoot(parent, existing)] = FindRoot(parent, r);
                }
            }

            for (int root = 0; root < runs.Count; root++)
            {
                if (FindRoot(parent, root) != root) continue;

                var groupRuns = new List<Run>();
                for (int r = 0; r < runs.Count; r++)
                {
                    if (FindRoot(parent, r) == root) groupRuns.Add(runs[r]);
                }

                Run longest = groupRuns[0];
                bool hasH = false;
                bool hasV = false;
                foreach (Run run in groupRuns)
                {
                    if (run.Cells.Count > longest.Cells.Count) longest = run;
                    if (run.Horizontal) hasH = true;
                    else hasV = true;
                }

                SpecialKind kind;
                if (longest.Cells.Count >= 5) kind = SpecialKind.Prism;
                else if (hasH && hasV) kind = SpecialKind.Bomb;
                else if (longest.Cells.Count == 4) kind = longest.Horizontal ? SpecialKind.LineH : SpecialKind.LineV;
                else continue;

                GridPos position = ChooseCreationCell(groupRuns, longest, kind, swapA, swapB);
                int type = kind == SpecialKind.Prism ? Colorless : longest.Type;
                creations.Add(new SpecialCreation(position, type, kind));
            }

            return creations;
        }

        private static int FindRoot(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static GridPos ChooseCreationCell(List<Run> groupRuns, Run longest, SpecialKind kind, GridPos? swapA, GridPos? swapB)
        {
            // 1) Donde el jugador soltó el mineral (B), o de donde lo tomó (A).
            if (swapB.HasValue && GroupContains(groupRuns, swapB.Value)) return swapB.Value;
            if (swapA.HasValue && GroupContains(groupRuns, swapA.Value)) return swapA.Value;

            // 2) Dinamita en cascada: en el cruce de la L / T.
            if (kind == SpecialKind.Bomb)
            {
                foreach (Run h in groupRuns)
                {
                    if (!h.Horizontal) continue;
                    foreach (Run v in groupRuns)
                    {
                        if (v.Horizontal) continue;
                        foreach (GridPos pos in h.Cells)
                        {
                            if (v.Cells.Contains(pos)) return pos;
                        }
                    }
                }
            }

            // 3) El centro de la línea más larga.
            return longest.Cells[longest.Cells.Count / 2];
        }

        private static bool GroupContains(List<Run> groupRuns, GridPos pos)
        {
            foreach (Run run in groupRuns)
            {
                if (run.Cells.Contains(pos)) return true;
            }
            return false;
        }

        private void MarkRow(StepContext ctx, int y)
        {
            for (int x = 0; x < Width; x++) Mark(ctx, new GridPos(x, y));
        }

        private void MarkColumn(StepContext ctx, int x)
        {
            for (int y = 0; y < Height; y++) Mark(ctx, new GridPos(x, y));
        }

        private void MarkArea(StepContext ctx, GridPos center, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    Mark(ctx, new GridPos(center.X + dx, center.Y + dy));
                }
            }
        }

        private void MarkColor(StepContext ctx, int color)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (types[x, y] == color) Mark(ctx, new GridPos(x, y));
                }
            }
        }

        private int PickRemainingColor(StepContext ctx)
        {
            var present = new List<int>();
            bool[] seen = new bool[MineralTypeCount];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int type = types[x, y];
                    if (type >= 0 && !ctx.Marked[x, y] && !seen[type])
                    {
                        seen[type] = true;
                        present.Add(type);
                    }
                }
            }

            present.Sort();
            return present.Count == 0 ? -1 : present[rng.Next(present.Count)];
        }

        private static bool IsLine(SpecialKind kind) => kind == SpecialKind.LineH || kind == SpecialKind.LineV;

        // ------------------------------------------------------------------
        // Detección
        // ------------------------------------------------------------------

        /// <summary>Líneas de 3+ minerales del mismo color (el Prisma no tiene color y no cuenta).</summary>
        private List<Run> FindRuns()
        {
            var runs = new List<Run>();

            for (int y = 0; y < Height; y++)
            {
                int start = 0;
                for (int x = 1; x <= Width; x++)
                {
                    if (x < Width && types[x, y] >= 0 && types[x, y] == types[start, y]) continue;

                    if (types[start, y] >= 0 && x - start >= 3)
                    {
                        var run = new Run { Horizontal = true, Type = types[start, y] };
                        for (int i = start; i < x; i++) run.Cells.Add(new GridPos(i, y));
                        runs.Add(run);
                    }
                    start = x;
                }
            }

            for (int x = 0; x < Width; x++)
            {
                int start = 0;
                for (int y = 1; y <= Height; y++)
                {
                    if (y < Height && types[x, y] >= 0 && types[x, y] == types[x, start]) continue;

                    if (types[x, start] >= 0 && y - start >= 3)
                    {
                        var run = new Run { Horizontal = false, Type = types[x, start] };
                        for (int i = start; i < y; i++) run.Cells.Add(new GridPos(x, i));
                        runs.Add(run);
                    }
                    start = y;
                }
            }

            return runs;
        }

        private bool IsSpecialCombo(GridPos a, GridPos b)
        {
            SpecialKind sa = specials[a.X, a.Y];
            SpecialKind sb = specials[b.X, b.Y];
            return sa == SpecialKind.Prism || sb == SpecialKind.Prism ||
                   (sa != SpecialKind.None && sb != SpecialKind.None);
        }

        private bool IsValidSwap(GridPos a, GridPos b)
        {
            if (types[a.X, a.Y] == Empty || types[b.X, b.Y] == Empty) return false;
            return IsSpecialCombo(a, b) || SwapCreatesMatch(a, b);
        }

        private bool SwapCreatesMatch(GridPos a, GridPos b)
        {
            if (types[a.X, a.Y] == types[b.X, b.Y]) return false;

            Swap(a, b);
            bool hasMatch = HasMatchAt(a) || HasMatchAt(b);
            Swap(a, b);
            return hasMatch;
        }

        private bool HasMatchAt(GridPos pos)
        {
            int type = types[pos.X, pos.Y];
            if (type < 0) return false;

            int horizontal = 1 + CountDirection(pos, -1, 0, type) + CountDirection(pos, 1, 0, type);
            if (horizontal >= 3) return true;

            int vertical = 1 + CountDirection(pos, 0, -1, type) + CountDirection(pos, 0, 1, type);
            return vertical >= 3;
        }

        private int CountDirection(GridPos start, int dirX, int dirY, int type)
        {
            int count = 0;
            int x = start.X + dirX;
            int y = start.Y + dirY;

            while (x >= 0 && x < Width && y >= 0 && y < Height && types[x, y] == type)
            {
                count++;
                x += dirX;
                y += dirY;
            }
            return count;
        }

        // ------------------------------------------------------------------
        // Movimiento de piezas
        // ------------------------------------------------------------------

        private void Swap(GridPos a, GridPos b)
        {
            int tempType = types[a.X, a.Y];
            types[a.X, a.Y] = types[b.X, b.Y];
            types[b.X, b.Y] = tempType;

            SpecialKind tempSpecial = specials[a.X, a.Y];
            specials[a.X, a.Y] = specials[b.X, b.Y];
            specials[b.X, b.Y] = tempSpecial;
        }

        /// <summary>Hace caer los minerales hacia abajo (Y=0) para tapar los huecos.</summary>
        private void Collapse(CascadeStep step)
        {
            for (int x = 0; x < Width; x++)
            {
                int writeY = 0;
                for (int y = 0; y < Height; y++)
                {
                    if (types[x, y] == Empty) continue;

                    if (y != writeY)
                    {
                        types[x, writeY] = types[x, y];
                        specials[x, writeY] = specials[x, y];
                        types[x, y] = Empty;
                        specials[x, y] = SpecialKind.None;
                        step.Falls.Add(new MineralFall(new GridPos(x, y), new GridPos(x, writeY)));
                    }
                    writeY++;
                }
            }
        }

        /// <summary>Rellena los huecos de arriba con minerales nuevos (pueden crear cascadas).</summary>
        private void Refill(CascadeStep step)
        {
            for (int x = 0; x < Width; x++)
            {
                int spawnIndex = 0;
                for (int y = 0; y < Height; y++)
                {
                    if (types[x, y] != Empty) continue;

                    int type = rng.Next(MineralTypeCount);
                    types[x, y] = type;
                    specials[x, y] = SpecialKind.None;
                    step.Spawns.Add(new MineralSpawn(new GridPos(x, y), type, Height + spawnIndex));
                    spawnIndex++;
                }
            }
        }

        // ------------------------------------------------------------------
        // Generación y reorganización
        // ------------------------------------------------------------------

        private void GenerateInitialBoard()
        {
            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                FillWithoutMatches();
                if (HasAvailableMoves()) return;
            }

            // Prácticamente imposible con 3+ tipos en un tablero razonable,
            // pero por si acaso garantizamos un tablero jugable.
            Shuffle();
        }

        /// <summary>Rellena todo el tablero (sin especiales) evitando líneas de 3 desde el inicio.</summary>
        private void FillWithoutMatches()
        {
            var candidates = new List<int>(MineralTypeCount);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    candidates.Clear();
                    for (int type = 0; type < MineralTypeCount; type++)
                    {
                        bool makesHorizontal = x >= 2 && types[x - 1, y] == type && types[x - 2, y] == type;
                        bool makesVertical = y >= 2 && types[x, y - 1] == type && types[x, y - 2] == type;
                        if (!makesHorizontal && !makesVertical) candidates.Add(type);
                    }

                    // Como mucho se excluyen 2 tipos y hay 3 o más, así que nunca está vacía.
                    types[x, y] = candidates[rng.Next(candidates.Count)];
                    specials[x, y] = SpecialKind.None;
                }
            }
        }

        /// <summary>Reorganiza las piezas (con sus especiales) hasta que no haya líneas y sí haya movimientos.</summary>
        private void Shuffle()
        {
            var pieces = new List<(int type, SpecialKind special)>(Width * Height);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (types[x, y] != Empty) pieces.Add((types[x, y], specials[x, y]));
                }
            }

            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                for (int i = pieces.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var temp = pieces[i];
                    pieces[i] = pieces[j];
                    pieces[j] = temp;
                }

                int index = 0;
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        if (types[x, y] == Empty) continue;
                        types[x, y] = pieces[index].type;
                        specials[x, y] = pieces[index].special;
                        index++;
                    }
                }

                if (FindRuns().Count == 0 && HasAvailableMoves()) return;
            }

            // Si mezclar no funciona (muy improbable), generamos un tablero nuevo.
            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                FillWithoutMatches();
                if (HasAvailableMoves()) return;
            }
        }
    }
}
