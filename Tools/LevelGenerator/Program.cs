using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CrystalClash.Board.Logic;
using CrystalClash.Story.Logic;

/// <summary>
/// Genera y calibra los niveles del modo historia.
/// - Los niveles 1-10 conservan su diseño; solo se recalculan sus estrellas.
/// - Los niveles 11-100 se diseñan con plantillas y se ajusta su dificultad
///   (cuántos minerales/puntos piden) hasta que un bot que juega al azar gane
///   el porcentaje deseado de partidas. Una persona juega mejor que ese bot.
/// </summary>
public static class Program
{
    private static readonly string[] Minerals = { "Sapphire", "Emerald", "Amethyst", "Ruby", "Gold", "Carbon" };

    private sealed class World
    {
        public string Name;
        public int First, Last;
        public double StartWin, EndWin;
        public string[] Names;
    }

    private static readonly World[] Worlds =
    {
        new World { Name = "MINA DE CRISTALES", First = 1, Last = 15, StartWin = 0.55, EndWin = 0.40, Names = new[]
        {
            "Túnel de los ecos", "Vagonetas perdidas", "Pozo profundo", "Grieta brillante", "Guardián de la mina"
        }},
        new World { Name = "CAVERNAS DE ESMERALDA", First = 16, Last = 35, StartWin = 0.62, EndWin = 0.32, Names = new[]
        {
            "Entrada verde", "Gruta de musgo", "Lago escondido", "Raíces de cristal", "Guardián del musgo",
            "Pasaje susurrante", "Bosque subterráneo", "Cascada de jade", "Hongos brillantes", "Guardián de jade",
            "Puente de lianas", "Eco esmeralda", "Cámara de helechos", "Río oculto", "Guardián del río",
            "Nido de luciérnagas", "Laberinto verde", "Corazón del bosque", "Trono de jade", "Guardián esmeralda"
        }},
        new World { Name = "VOLCÁN DE RUBÍ", First = 36, Last = 60, StartWin = 0.58, EndWin = 0.28, Names = new[]
        {
            "Faldas del volcán", "Ríos de lava", "Roca ardiente", "Humo rojo", "Guardián de ceniza",
            "Forja antigua", "Puente de obsidiana", "Cráter dormido", "Brasas danzantes", "Guardián de la forja",
            "Grieta de fuego", "Vapor escarlata", "Mina de azufre", "Lago de magma", "Guardián del magma",
            "Columnas de basalto", "Cueva del dragón", "Tesoro carmesí", "Viento ardiente", "Guardián del dragón",
            "Cumbre humeante", "Erupción", "Corazón de rubí", "Trono de fuego", "Guardián del volcán"
        }},
        new World { Name = "CORAZÓN DE DIAMANTE", First = 61, Last = 100, StartWin = 0.55, EndWin = 0.22, Names = Enumerable.Range(0, 40)
            .Select(i => DiamondName(i)).ToArray() }
    };

    private static string DiamondName(int i)
    {
        string[] places = { "Galería", "Bóveda", "Salón", "Abismo", "Mirador", "Santuario", "Cripta", "Pasaje" };
        string[] traits = { "helada", "de cuarzo", "eterna", "prismática", "silenciosa", "de luz", "infinita", "estelar" };
        if ((i + 1) % 5 == 0) return i == 39 ? "Corazón de Diamante" : $"Guardián {RomanNumeral((i + 1) / 5)}";
        return $"{places[i % places.Length]} {traits[(i * 3 + i / places.Length) % traits.Length]}";
    }

    private static string RomanNumeral(int n)
    {
        string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        return n < r.Length ? r[n] : n.ToString();
    }

    public static int Main(string[] args)
    {
        string root = FindRepoRoot();
        string path = Path.Combine(root, "Assets", "Resources", "Levels", "levels.json");
        JsonNode existing = JsonNode.Parse(File.ReadAllText(path));
        JsonArray oldLevels = existing["levels"].AsArray();

        int total = Worlds[^1].Last;
        var levels = new JsonObject[total];

        Parallel.For(1, total + 1, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, n =>
        {
            LevelSpec spec = n <= 10 ? FromJson((JsonObject)oldLevels[n - 1], n) : Design(n);
            if (n > 10) Calibrate(spec, TargetWinRate(n));

            Stats stats = Simulate(spec, 240, n * 10007);
            spec.Stars = StarsFrom(spec, stats);
            levels[n - 1] = ToJson(spec);
            Console.WriteLine($"Nivel {n,3} {spec.Name,-28} gana bot {stats.WinRate:P0} (objetivo {(n > 10 ? TargetWinRate(n).ToString("P0") : "-")})  estrellas {string.Join("/", spec.Stars)}");
        });

        var worlds = new JsonArray();
        foreach (World w in Worlds) worlds.Add(new JsonObject { ["name"] = w.Name, ["levels"] = w.Last - w.First + 1 });

        var output = new JsonObject
        {
            ["worlds"] = worlds,
            ["levels"] = new JsonArray(levels.Select(l => (JsonNode)l).ToArray())
        };
        File.WriteAllText(path, output.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        Console.WriteLine($"Escrito {path} ({total} niveles)");
        return 0;
    }

    // ------------------------------------------------------------------
    // Diseño
    // ------------------------------------------------------------------

    private sealed class Objective
    {
        public string Type;    // collect / score
        public string Mineral; // para collect
        public double Base;    // cantidad antes de escalar
        public int Count;
    }

    private sealed class LevelSpec
    {
        public int Number;
        public string Name;
        public int Width = 8, Height = 8, Types = 6, Moves = 25;
        public List<Objective> Objectives = new List<Objective>();
        public int[] Stars = { 0, 0, 0 };
        public double Scale = 1.0;
    }

    private static World WorldOf(int n) => Worlds.First(w => n >= w.First && n <= w.Last);

    private static double TargetWinRate(int n)
    {
        World w = WorldOf(n);
        double t = (n - w.First) / (double)Math.Max(1, w.Last - w.First);
        double target = w.StartWin + (w.EndWin - w.StartWin) * t;
        if (n == w.First) target += 0.08;          // cada mundo empieza un poco más fácil
        if (n % 5 == 0) target -= 0.12;            // cada 5 niveles, un "guardián" más difícil
        return Math.Max(0.12, Math.Min(0.9, target));
    }

    private static LevelSpec Design(int n)
    {
        World w = WorldOf(n);
        // Los niveles 1-10 ya existían: los nombres nuevos del Mundo 1 empiezan en el 11.
        int nameIndex = n - Math.Max(w.First, 11);
        var spec = new LevelSpec { Number = n, Name = w.Names[Math.Max(0, Math.Min(w.Names.Length - 1, nameIndex))] };

        // Tamaño del tablero
        if (n % 6 == 4) spec.Width = 7;
        if (n >= 36 && n % 9 == 7) spec.Height = 9;
        if (n >= 61 && n % 11 == 3) { spec.Width = 7; spec.Height = 9; }

        // Movimientos
        spec.Moves = 20 + (n * 3) % 9;
        if (n % 5 == 0) spec.Moves += 3;

        // Objetivos: se van alternando plantillas
        double perMove = 0.95; // minerales de un tipo que rompe el bot por jugada (aprox.)
        double pointsPerMove = 85;
        int kind = n % 7 == 0 ? 5 : n % 5;
        string M(int j) => Minerals[(n * 7 + j * 2) % Minerals.Length];

        switch (kind)
        {
            case 1:
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(0), Base = spec.Moves * perMove });
                break;
            case 2:
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(0), Base = spec.Moves * perMove * 0.8 });
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(1), Base = spec.Moves * perMove * 0.8 });
                break;
            case 3:
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(0), Base = spec.Moves * perMove * 0.8 });
                spec.Objectives.Add(new Objective { Type = "score", Base = spec.Moves * pointsPerMove * 0.8 });
                break;
            case 4:
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(0), Base = spec.Moves * perMove * 0.7 });
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(1), Base = spec.Moves * perMove * 0.7 });
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(2), Base = spec.Moves * perMove * 0.7 });
                break;
            case 5:
                spec.Objectives.Add(new Objective { Type = "score", Base = spec.Moves * pointsPerMove });
                break;
            default: // 0: guardián
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(0), Base = spec.Moves * perMove * 0.75 });
                spec.Objectives.Add(new Objective { Type = "collect", Mineral = M(1), Base = spec.Moves * perMove * 0.75 });
                spec.Objectives.Add(new Objective { Type = "score", Base = spec.Moves * pointsPerMove * 0.8 });
                break;
        }

        ApplyScale(spec, 1.0);
        return spec;
    }

    private static void ApplyScale(LevelSpec spec, double scale)
    {
        spec.Scale = scale;
        foreach (Objective o in spec.Objectives)
        {
            if (o.Base <= 0) continue;
            double v = o.Base * scale;
            o.Count = o.Type == "score" ? Math.Max(300, (int)Math.Round(v / 50) * 50) : Math.Max(5, (int)Math.Round(v));
        }
    }

    /// <summary>Busca la escala de los objetivos para que el bot gane ~el porcentaje deseado.</summary>
    private static void Calibrate(LevelSpec spec, double target)
    {
        double lo = 0.2, hi = 4.0;
        for (int i = 0; i < 10; i++)
        {
            double mid = (lo + hi) / 2;
            ApplyScale(spec, mid);
            double win = Simulate(spec, i < 5 ? 80 : 160, spec.Number * 7919 + i).WinRate;
            if (win > target) lo = mid; else hi = mid;
        }
        ApplyScale(spec, (lo + hi) / 2);
    }

    // ------------------------------------------------------------------
    // Simulación
    // ------------------------------------------------------------------

    private sealed class Stats
    {
        public double WinRate;
        public List<int> WinningScores = new List<int>();
    }

    private static LevelDefinition ToDefinition(LevelSpec spec)
    {
        var def = new LevelDefinition
        {
            Number = spec.Number, Name = spec.Name, Width = spec.Width, Height = spec.Height,
            MineralTypeCount = spec.Types, MaxMoves = spec.Moves, StarScores = new[] { 0, 0, 0 }
        };
        foreach (Objective o in spec.Objectives)
        {
            def.Objectives.Add(o.Type == "score"
                ? LevelObjective.Score(o.Count)
                : LevelObjective.Collect(Array.IndexOf(Minerals, o.Mineral), o.Count));
        }
        return def;
    }

    private static Stats Simulate(LevelSpec spec, int runs, int seedBase)
    {
        LevelDefinition def = ToDefinition(spec);
        var stats = new Stats();
        int wins = 0;
        for (int i = 0; i < runs; i++)
        {
            var random = new Random(seedBase + i * 31);
            var board = new BoardLogic(def.Width, def.Height, def.MineralTypeCount, seedBase * 13 + i + 1);
            var session = new LevelSession(def);

            while (session.CanPlay)
            {
                if (!RandomMove(board, random, out GridPos a, out GridPos b)) break;
                session.ApplyMove(board.TrySwap(a, b));
            }

            if (session.State == SessionState.Won)
            {
                wins++;
                session.ApplyFinale(board.PlayFinale(session.MovesLeft));
                stats.WinningScores.Add(session.FinalScore);
            }
        }
        stats.WinRate = wins / (double)runs;
        stats.WinningScores.Sort();
        return stats;
    }

    /// <summary>Bot que juega "como persona distraída": una jugada válida al azar.</summary>
    private static bool RandomMove(BoardLogic board, Random random, out GridPos a, out GridPos b)
    {
        for (int k = 0; k < 80; k++)
        {
            int x = random.Next(board.Width), y = random.Next(board.Height);
            a = new GridPos(x, y);
            b = random.Next(2) == 0 ? new GridPos(x + 1, y) : new GridPos(x, y + 1);
            if (!board.IsInside(b)) continue;
            if (Copy(board).TrySwap(a, b).IsValid) return true;
        }
        return board.TryFindMove(out a, out b);
    }

    private static BoardLogic Copy(BoardLogic board)
    {
        var t = new int[board.Width, board.Height];
        var s = new SpecialKind[board.Width, board.Height];
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++) { t[x, y] = board.GetMineral(x, y); s[x, y] = board.GetSpecial(x, y); }
        return BoardLogic.FromLayout(t, s, board.MineralTypeCount, 1);
    }

    private static int[] StarsFrom(LevelSpec spec, Stats stats)
    {
        int scoreGoal = spec.Objectives.Where(o => o.Type == "score").Select(o => o.Count).DefaultIfEmpty(0).Max();
        if (stats.WinningScores.Count == 0) return new[] { scoreGoal, scoreGoal + 500, scoreGoal + 1000 };

        int P(double q) => stats.WinningScores[(int)Math.Min(stats.WinningScores.Count - 1, q * stats.WinningScores.Count)];
        int Round(int v) => (int)Math.Round(v / 50.0) * 50;

        int s1 = Math.Max(scoreGoal, Round(P(0.05)));
        int s2 = Math.Max(s1, Round(P(0.55)));
        int s3 = Math.Max(s2 + 50, Round(P(0.88)));
        return new[] { s1, s2, s3 };
    }

    // ------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------

    private static LevelSpec FromJson(JsonObject o, int n)
    {
        var spec = new LevelSpec
        {
            Number = n,
            Name = (string)o["name"],
            Width = (int)o["width"],
            Height = (int)o["height"],
            Types = (int)o["mineralTypes"],
            Moves = (int)o["moves"]
        };
        foreach (JsonNode node in o["objectives"].AsArray())
        {
            spec.Objectives.Add(new Objective
            {
                Type = (string)node["type"],
                Mineral = node["mineral"] != null ? (string)node["mineral"] : null,
                Count = (int)node["count"]
            });
        }
        return spec;
    }

    private static JsonObject ToJson(LevelSpec spec)
    {
        var objectives = new JsonArray();
        foreach (Objective o in spec.Objectives)
        {
            var node = new JsonObject { ["type"] = o.Type };
            if (o.Type == "collect") node["mineral"] = o.Mineral;
            node["count"] = o.Count;
            objectives.Add(node);
        }
        return new JsonObject
        {
            ["name"] = spec.Name,
            ["width"] = spec.Width,
            ["height"] = spec.Height,
            ["mineralTypes"] = spec.Types,
            ["seed"] = 0,
            ["moves"] = spec.Moves,
            ["objectives"] = objectives,
            ["stars"] = new JsonArray(spec.Stars.Select(s => (JsonNode)s).ToArray())
        };
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets"))) dir = Path.GetDirectoryName(dir);
        return dir ?? Directory.GetCurrentDirectory();
    }
}
