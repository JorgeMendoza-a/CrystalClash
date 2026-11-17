using System;
using System.Collections.Generic;
using CrystalClash.Story.Logic;
using UnityEngine;

/// <summary>
/// Carga los niveles del modo historia desde Resources/Levels/levels.json.
/// Para agregar o cambiar niveles solo hay que editar ese archivo.
/// </summary>
public static class LevelCatalog
{
    private const string ResourcePath = "Levels/levels";

    [Serializable]
    private class ObjectiveJson
    {
        public string type = "";     // "collect" o "score"
        public string mineral = "";  // nombre del enum MineralType (solo para "collect")
        public int count;
    }

    [Serializable]
    private class LevelJson
    {
        public string name = "";
        public int width = 8;
        public int height = 8;
        public int mineralTypes = 6;
        public int seed;
        public int moves = 25;
        public ObjectiveJson[] objectives = new ObjectiveJson[0];
        public int[] stars = new int[0];
    }

    [Serializable]
    private class CatalogJson
    {
        public LevelJson[] levels = new LevelJson[0];
    }

    private static List<LevelDefinition> levels;

    public static IReadOnlyList<LevelDefinition> Levels
    {
        get
        {
            if (levels == null) Load();
            return levels;
        }
    }

    public static int Count => Levels.Count;

    /// <summary>Nivel por número (desde 1). Null si no existe.</summary>
    public static LevelDefinition Get(int number)
    {
        return number >= 1 && number <= Count ? Levels[number - 1] : null;
    }

    private static void Load()
    {
        levels = new List<LevelDefinition>();

        TextAsset file = Resources.Load<TextAsset>(ResourcePath);
        if (file == null)
        {
            Debug.LogError($"No se encontró Resources/{ResourcePath}.json. El modo historia no tiene niveles.");
            return;
        }

        CatalogJson catalog = JsonUtility.FromJson<CatalogJson>(file.text);
        if (catalog == null || catalog.levels == null)
        {
            Debug.LogError("levels.json está vacío o mal escrito.");
            return;
        }

        for (int i = 0; i < catalog.levels.Length; i++)
        {
            int number = i + 1;
            try
            {
                LevelDefinition level = Convert(catalog.levels[i], number);
                level.Validate();
                levels.Add(level);
            }
            catch (Exception e)
            {
                // Paramos en el primer nivel roto para no desordenar la numeración.
                Debug.LogError($"levels.json, nivel {number}: {e.Message}");
                break;
            }
        }

        Debug.Log($"Modo historia: {levels.Count} niveles cargados.");
    }

    private static LevelDefinition Convert(LevelJson json, int number)
    {
        var level = new LevelDefinition
        {
            Number = number,
            Name = json.name,
            Width = json.width,
            Height = json.height,
            MineralTypeCount = json.mineralTypes,
            Seed = json.seed,
            MaxMoves = json.moves,
            StarScores = json.stars
        };

        foreach (ObjectiveJson objective in json.objectives)
        {
            switch (objective.type.Trim().ToLowerInvariant())
            {
                case "collect":
                    if (!Enum.TryParse(objective.mineral, true, out MineralType mineral))
                    {
                        throw new ArgumentException($"mineral desconocido '{objective.mineral}'");
                    }
                    level.Objectives.Add(LevelObjective.Collect((int)mineral, objective.count));
                    break;

                case "score":
                    level.Objectives.Add(LevelObjective.Score(objective.count));
                    break;

                default:
                    throw new ArgumentException($"tipo de objetivo desconocido '{objective.type}' (usa \"collect\" o \"score\")");
            }
        }

        return level;
    }
}
