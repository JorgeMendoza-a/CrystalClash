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
    /// - Los minerales se guardan como enteros 0..MineralTypeCount-1, que
    ///   coinciden con el enum MineralType. Empty (-1) = celda vacía.
    /// - (0,0) es la celda de abajo a la izquierda; las piezas caen hacia Y=0.
    /// </summary>
    public sealed class BoardLogic
    {
        public const int Empty = -1;
        public const int MinMineralTypes = 3;
        public const int MinSize = 3;
        public const int PointsPerMineral = 10;

        private const int MaxGenerationAttempts = 100;

        private readonly int[,] grid;
        private readonly DeterministicRandom rng;

        public int Width { get; }
        public int Height { get; }
        public int MineralTypeCount { get; }
        public int Seed { get; }
        public int Score { get; private set; }
        public int MovesMade { get; private set; }

        public BoardLogic(int width, int height, int mineralTypeCount, int seed)
        {
            if (width < MinSize) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < MinSize) throw new ArgumentOutOfRangeException(nameof(height));
            if (mineralTypeCount < MinMineralTypes) throw new ArgumentOutOfRangeException(nameof(mineralTypeCount));

            Width = width;
            Height = height;
            MineralTypeCount = mineralTypeCount;
            Seed = seed;

            grid = new int[width, height];
            rng = new DeterministicRandom(seed);

            GenerateInitialBoard();
        }

        // ------------------------------------------------------------------
        // Consulta
        // ------------------------------------------------------------------

        public int GetMineral(int x, int y) => grid[x, y];

        public int GetMineral(GridPos pos) => grid[pos.X, pos.Y];

        public bool IsInside(GridPos pos)
        {
            return pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;
        }

        /// <summary>Todas las celdas que forman parte de una línea de 3 o más.</summary>
        public List<GridPos> FindMatches()
        {
            bool[,] marked = new bool[Width, Height];

            // Horizontales
            for (int y = 0; y < Height; y++)
            {
                int runStart = 0;
                for (int x = 1; x <= Width; x++)
                {
                    bool continues = x < Width && grid[x, y] != Empty && grid[x, y] == grid[runStart, y];
                    if (continues) continue;

                    if (grid[runStart, y] != Empty && x - runStart >= 3)
                    {
                        for (int i = runStart; i < x; i++) marked[i, y] = true;
                    }
                    runStart = x;
                }
            }

            // Verticales
            for (int x = 0; x < Width; x++)
            {
                int runStart = 0;
                for (int y = 1; y <= Height; y++)
                {
                    bool continues = y < Height && grid[x, y] != Empty && grid[x, y] == grid[x, runStart];
                    if (continues) continue;

                    if (grid[x, runStart] != Empty && y - runStart >= 3)
                    {
                        for (int i = runStart; i < y; i++) marked[x, i] = true;
                    }
                    runStart = y;
                }
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

        /// <summary>Busca un intercambio que produzca combinación (útil también para pistas).</summary>
        public bool TryFindMove(out GridPos a, out GridPos b)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var from = new GridPos(x, y);

                    var right = new GridPos(x + 1, y);
                    if (IsInside(right) && SwapCreatesMatch(from, right))
                    {
                        a = from;
                        b = right;
                        return true;
                    }

                    var up = new GridPos(x, y + 1);
                    if (IsInside(up) && SwapCreatesMatch(from, up))
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
        /// Intenta intercambiar dos celdas. Si no genera combinación, el tablero
        /// queda igual y IsValid es false. Si es válido, resuelve toda la
        /// cascada (romper, caer, rellenar, repetir) y la devuelve paso a paso.
        /// </summary>
        public SwapResult TrySwap(GridPos a, GridPos b)
        {
            var result = new SwapResult(a, b);

            if (!IsInside(a) || !IsInside(b) || !a.IsAdjacentTo(b)) return result;
            if (grid[a.X, a.Y] == Empty || grid[b.X, b.Y] == Empty) return result;

            Swap(a, b);

            List<GridPos> matches = FindMatches();
            if (matches.Count == 0)
            {
                Swap(a, b);
                return result;
            }

            result.IsValid = true;
            MovesMade++;

            int level = 1;
            while (matches.Count > 0)
            {
                var step = new CascadeStep(level);

                foreach (GridPos pos in matches)
                {
                    grid[pos.X, pos.Y] = Empty;
                    step.Cleared.Add(pos);
                }

                step.Points = matches.Count * PointsPerMineral;
                Score += step.Points;
                result.PointsGained += step.Points;

                Collapse(step);
                Refill(step);

                result.Steps.Add(step);
                level++;
                matches = FindMatches();
            }

            if (!HasAvailableMoves())
            {
                Shuffle();
                result.WasShuffled = true;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Internos
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

        /// <summary>Rellena todo el tablero evitando crear líneas de 3 desde el inicio.</summary>
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
                        bool makesHorizontal = x >= 2 && grid[x - 1, y] == type && grid[x - 2, y] == type;
                        bool makesVertical = y >= 2 && grid[x, y - 1] == type && grid[x, y - 2] == type;
                        if (!makesHorizontal && !makesVertical) candidates.Add(type);
                    }

                    // Como mucho se excluyen 2 tipos y hay 3 o más, así que nunca está vacía.
                    grid[x, y] = candidates[rng.Next(candidates.Count)];
                }
            }
        }

        private void Swap(GridPos a, GridPos b)
        {
            int temp = grid[a.X, a.Y];
            grid[a.X, a.Y] = grid[b.X, b.Y];
            grid[b.X, b.Y] = temp;
        }

        private bool SwapCreatesMatch(GridPos a, GridPos b)
        {
            if (grid[a.X, a.Y] == Empty || grid[b.X, b.Y] == Empty) return false;
            if (grid[a.X, a.Y] == grid[b.X, b.Y]) return false;

            Swap(a, b);
            bool hasMatch = HasMatchAt(a) || HasMatchAt(b);
            Swap(a, b);
            return hasMatch;
        }

        private bool HasMatchAt(GridPos pos)
        {
            int type = grid[pos.X, pos.Y];
            if (type == Empty) return false;

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

            while (x >= 0 && x < Width && y >= 0 && y < Height && grid[x, y] == type)
            {
                count++;
                x += dirX;
                y += dirY;
            }
            return count;
        }

        /// <summary>Hace caer los minerales hacia abajo (Y=0) para tapar los huecos.</summary>
        private void Collapse(CascadeStep step)
        {
            for (int x = 0; x < Width; x++)
            {
                int writeY = 0;
                for (int y = 0; y < Height; y++)
                {
                    if (grid[x, y] == Empty) continue;

                    if (y != writeY)
                    {
                        grid[x, writeY] = grid[x, y];
                        grid[x, y] = Empty;
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
                    if (grid[x, y] != Empty) continue;

                    int type = rng.Next(MineralTypeCount);
                    grid[x, y] = type;
                    step.Spawns.Add(new MineralSpawn(new GridPos(x, y), type, Height + spawnIndex));
                    spawnIndex++;
                }
            }
        }

        /// <summary>Reorganiza los minerales hasta que no haya líneas hechas y sí haya movimientos.</summary>
        private void Shuffle()
        {
            var types = new List<int>(Width * Height);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (grid[x, y] != Empty) types.Add(grid[x, y]);
                }
            }

            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                for (int i = types.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int temp = types[i];
                    types[i] = types[j];
                    types[j] = temp;
                }

                int index = 0;
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        if (grid[x, y] != Empty) grid[x, y] = types[index++];
                    }
                }

                if (FindMatches().Count == 0 && HasAvailableMoves()) return;
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
