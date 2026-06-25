using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public int width = 8;
    public int height = 8;

    public GameObject mineralPrefab;

   private Cell[,] cells;
private Mineral selectedMineral;
private MoveFinder moveFinder;

    void Start()
{
    moveFinder = GetComponent<MoveFinder>();

    GenerateBoard();

    CheckAvailableMoves();
}

    void GenerateBoard()
    {
        cells = new Cell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y] = new Cell(x, y);
                CreateMineral(x, y);
            }
        }
    }

    void CreateMineral(int x, int y)
    {
        Vector2 position = GetWorldPosition(x, y);

        GameObject newMineral = Instantiate(
            mineralPrefab,
            position,
            Quaternion.identity,
            transform
        );

        Mineral mineral = newMineral.GetComponent<Mineral>();
        mineral.SetPosition(x, y);

        cells[x, y].mineral = mineral;
    }

    Vector2 GetWorldPosition(int x, int y)
    {
        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        return new Vector2(x - offsetX, y - offsetY);
    }

    public void SelectMineral(Mineral mineral)
    {
        if (selectedMineral == null)
        {
            selectedMineral = mineral;
            selectedMineral.Select();
        }
        else
        {
            selectedMineral.Deselect();

            if (IsNeighbor(selectedMineral, mineral))
            {
                SwapMinerals(selectedMineral, mineral);

                List<Mineral> matches = GetMatches();

                if (matches.Count > 0)
                {
                    StartCoroutine(ProcessBoard());
                }
                else
                {
                    SwapMinerals(selectedMineral, mineral);
                }
            }

            selectedMineral = null;
        }
    }

    bool IsNeighbor(Mineral a, Mineral b)
    {
        int distanceX = Mathf.Abs(a.x - b.x);
        int distanceY = Mathf.Abs(a.y - b.y);

        return distanceX + distanceY == 1;
    }

    void SwapMinerals(Mineral a, Mineral b)
    {
        cells[a.x, a.y].mineral = b;
        cells[b.x, b.y].mineral = a;

        int tempX = a.x;
        int tempY = a.y;

        a.SetPosition(b.x, b.y);
        b.SetPosition(tempX, tempY);

        Vector3 tempPos = a.transform.position;
        a.transform.position = b.transform.position;
        b.transform.position = tempPos;
    }

    List<Mineral> GetMatches()
    {
        List<Mineral> matches = new List<Mineral>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                Mineral m1 = cells[x, y].mineral;
                Mineral m2 = cells[x + 1, y].mineral;
                Mineral m3 = cells[x + 2, y].mineral;

                if (m1 != null && m2 != null && m3 != null &&
                    m1.mineralType == m2.mineralType &&
                    m2.mineralType == m3.mineralType)
                {
                    AddMatch(matches, m1);
                    AddMatch(matches, m2);
                    AddMatch(matches, m3);
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                Mineral m1 = cells[x, y].mineral;
                Mineral m2 = cells[x, y + 1].mineral;
                Mineral m3 = cells[x, y + 2].mineral;

                if (m1 != null && m2 != null && m3 != null &&
                    m1.mineralType == m2.mineralType &&
                    m2.mineralType == m3.mineralType)
                {
                    AddMatch(matches, m1);
                    AddMatch(matches, m2);
                    AddMatch(matches, m3);
                }
            }
        }

        return matches;
    }

    void AddMatch(List<Mineral> matches, Mineral mineral)
    {
        if (!matches.Contains(mineral))
        {
            matches.Add(mineral);
        }
    }

    IEnumerator ProcessBoard()
    {
        yield return new WaitForSeconds(0.2f);

        List<Mineral> matches = GetMatches();

        while (matches.Count > 0)
        {
            ClearMatches(matches);

            yield return new WaitForSeconds(0.2f);

            CollapseColumns();

            yield return new WaitForSeconds(0.3f);

            RefillBoard();

            yield return new WaitForSeconds(0.3f);

            matches = GetMatches();
        }

        Debug.Log("No hay más combinaciones");
CheckAvailableMoves();
    }

    void ClearMatches(List<Mineral> matches)
    {
        foreach (Mineral mineral in matches)
        {
            cells[mineral.x, mineral.y].mineral = null;
            Destroy(mineral.gameObject);
        }

        Debug.Log("Minerales eliminados: " + matches.Count);
    }

    void CollapseColumns()
    {
        for (int x = 0; x < width; x++)
        {
            int emptyY = -1;

            for (int y = 0; y < height; y++)
            {
                if (cells[x, y].mineral == null && emptyY == -1)
                {
                    emptyY = y;
                }
                else if (cells[x, y].mineral != null && emptyY != -1)
                {
                    Mineral mineral = cells[x, y].mineral;

                    cells[x, emptyY].mineral = mineral;
                    cells[x, y].mineral = null;

                    mineral.SetPosition(x, emptyY);
                    mineral.transform.position = GetWorldPosition(x, emptyY);

                    emptyY++;
                }
            }
        }
    }

    void RefillBoard()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (cells[x, y].mineral == null)
                {
                    CreateMineral(x, y);
                }
            }
        }
    }

void CheckAvailableMoves()
{
    bool hasMoves = moveFinder.HasAvailableMoves(cells, width, height);

    if (hasMoves)
    {
        Debug.Log("Sí hay movimientos disponibles");
    }
    else
    {
        Debug.LogWarning("No hay movimientos disponibles. Reorganizando tablero...");
        ShuffleBoard();
    }
}

void ShuffleBoard()
{
    List<MineralType> types = new List<MineralType>();

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (cells[x, y].mineral != null)
            {
                types.Add(cells[x, y].mineral.mineralType);
            }
        }
    }

    for (int i = 0; i < types.Count; i++)
    {
        int randomIndex = Random.Range(i, types.Count);

        MineralType temp = types[i];
        types[i] = types[randomIndex];
        types[randomIndex] = temp;
    }

    int index = 0;

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (cells[x, y].mineral != null)
            {
                cells[x, y].mineral.SetMineralType(types[index]);
                index++;
            }
        }
    }

    Debug.Log("Tablero reorganizado");

    if (!moveFinder.HasAvailableMoves(cells, width, height))
    {
        ShuffleBoard();
    }
}
}