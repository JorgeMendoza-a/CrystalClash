using UnityEngine;

public class MoveFinder : MonoBehaviour
{
    public bool HasAvailableMoves(Cell[,] cells, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (CanCreateMatch(cells, x, y, x + 1, y, width, height))
{
    Debug.Log($"Movimiento encontrado: ({x},{y}) ↔ ({x + 1},{y})");
    return true;
}

if (CanCreateMatch(cells, x, y, x, y + 1, width, height))
{
    Debug.Log($"Movimiento encontrado: ({x},{y}) ↔ ({x},{y + 1})");
    return true;
}
            }
        }

        return false;
    }

    bool CanCreateMatch(Cell[,] cells, int x1, int y1, int x2, int y2, int width, int height)
    {
        if (x2 >= width || y2 >= height)
            return false;

        Mineral a = cells[x1, y1].mineral;
        Mineral b = cells[x2, y2].mineral;

        if (a == null || b == null)
            return false;

        SwapTypes(a, b);

        bool hasMatch =
            HasMatchAt(cells, x1, y1, width, height) ||
            HasMatchAt(cells, x2, y2, width, height);

        SwapTypes(a, b);

        return hasMatch;
    }

    void SwapTypes(Mineral a, Mineral b)
    {
        MineralType temp = a.mineralType;
        a.mineralType = b.mineralType;
        b.mineralType = temp;
    }

    bool HasMatchAt(Cell[,] cells, int x, int y, int width, int height)
    {
        Mineral mineral = cells[x, y].mineral;

        if (mineral == null)
            return false;

        MineralType type = mineral.mineralType;

        int horizontal = 1;
        horizontal += CountDirection(cells, x, y, -1, 0, type, width, height);
        horizontal += CountDirection(cells, x, y, 1, 0, type, width, height);

        if (horizontal >= 3)
            return true;

        int vertical = 1;
        vertical += CountDirection(cells, x, y, 0, -1, type, width, height);
        vertical += CountDirection(cells, x, y, 0, 1, type, width, height);

        return vertical >= 3;
    }

    int CountDirection(Cell[,] cells, int startX, int startY, int dirX, int dirY, MineralType type, int width, int height)
    {
        int count = 0;

        int x = startX + dirX;
        int y = startY + dirY;

        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            Mineral mineral = cells[x, y].mineral;

            if (mineral == null || mineral.mineralType != type)
                break;

            count++;

            x += dirX;
            y += dirY;
        }

        return count;
    }
}