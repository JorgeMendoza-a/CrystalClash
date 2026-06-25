using System.Collections.Generic;
using UnityEngine;
using System.Collections;


public class BoardManager : MonoBehaviour
{
    public int width = 8;
    public int height = 8;

    public GameObject candyPrefab;

    private Candy[,] board;
    private Candy selectedCandy;

    void Start()
    {
        GenerateBoard();
    }

    void GenerateBoard()
    {
        board = new Candy[width, height];

        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CreateCandy(x, y, offsetX, offsetY);
            }
        }
    }

    void CreateCandy(int x, int y, float offsetX, float offsetY)
    {
        Vector2 position = new Vector2(x - offsetX, y - offsetY);

        GameObject newCandy = Instantiate(
            candyPrefab,
            position,
            Quaternion.identity,
            transform
        );

        Candy candy = newCandy.GetComponent<Candy>();
        candy.SetPosition(x, y);

        board[x, y] = candy;
    }

    public void SelectCandy(Candy candy)
    {
        if (selectedCandy == null)
        {
            selectedCandy = candy;
            selectedCandy.Select();
        }
        else
        {
            selectedCandy.Deselect();

            if (IsNeighbor(selectedCandy, candy))
            {
                SwapCandies(selectedCandy, candy);

                List<Candy> matches = GetMatches();

              if (matches.Count > 0)
{
    StartCoroutine(ProcessBoard());
}
else
{
    SwapCandies(selectedCandy, candy);
}
            }

            selectedCandy = null;
        }
    }

    bool IsNeighbor(Candy a, Candy b)
    {
        int distanceX = Mathf.Abs(a.x - b.x);
        int distanceY = Mathf.Abs(a.y - b.y);

        return distanceX + distanceY == 1;
    }

    void SwapCandies(Candy a, Candy b)
    {
        board[a.x, a.y] = b;
        board[b.x, b.y] = a;

        int tempX = a.x;
        int tempY = a.y;

        a.SetPosition(b.x, b.y);
        b.SetPosition(tempX, tempY);

        Vector3 tempPos = a.transform.position;
        a.transform.position = b.transform.position;
        b.transform.position = tempPos;
    }

    List<Candy> GetMatches()
    {
        List<Candy> matches = new List<Candy>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                Candy c1 = board[x, y];
                Candy c2 = board[x + 1, y];
                Candy c3 = board[x + 2, y];

                if (c1 != null && c2 != null && c3 != null &&
                    c1.colorId == c2.colorId &&
                    c2.colorId == c3.colorId)
                {
                    AddMatch(matches, c1);
                    AddMatch(matches, c2);
                    AddMatch(matches, c3);
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                Candy c1 = board[x, y];
                Candy c2 = board[x, y + 1];
                Candy c3 = board[x, y + 2];

                if (c1 != null && c2 != null && c3 != null &&
                    c1.colorId == c2.colorId &&
                    c2.colorId == c3.colorId)
                {
                    AddMatch(matches, c1);
                    AddMatch(matches, c2);
                    AddMatch(matches, c3);
                }
            }
        }

        return matches;
    }

    void AddMatch(List<Candy> matches, Candy candy)
    {
        if (!matches.Contains(candy))
        {
            matches.Add(candy);
        }
    }

    void ClearMatches(List<Candy> matches)
    {
        foreach (Candy candy in matches)
        {
            board[candy.x, candy.y] = null;
            Destroy(candy.gameObject);
        }

        Debug.Log("Minerales eliminados: " + matches.Count);
    }

    void CollapseColumns()
    {
        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        for (int x = 0; x < width; x++)
        {
            int emptyY = -1;

            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == null && emptyY == -1)
                {
                    emptyY = y;
                }
                else if (board[x, y] != null && emptyY != -1)
                {
                    Candy candy = board[x, y];

                    board[x, emptyY] = candy;
                    board[x, y] = null;

                    candy.SetPosition(x, emptyY);
                    StartCoroutine(MoveCandy(
    candy,
    new Vector2(x - offsetX, emptyY - offsetY)
));

                    emptyY++;
                }
            }
        }

        Debug.Log("Columnas bajaron");
    }

    void RefillBoard()
{
    float offsetX = (width - 1) / 2f;
    float offsetY = (height - 1) / 2f;

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            if (board[x, y] == null)
            {
                Vector2 position = new Vector2(
                    x - offsetX,
                    y - offsetY
                );

                GameObject newCandy = Instantiate(
                    candyPrefab,
                    position,
                    Quaternion.identity,
                    transform
                );

                Candy candy = newCandy.GetComponent<Candy>();

                candy.SetPosition(x, y);

                board[x, y] = candy;
            }
        }
    }
}
IEnumerator MoveCandy(Candy candy, Vector2 targetPosition)// 
{
    float duration = 0.2f;
    float elapsed = 0f;

    Vector2 startPosition = candy.transform.position;

    while (elapsed < duration)
    {
        candy.transform.position = Vector2.Lerp(
            startPosition,
            targetPosition,
            elapsed / duration
        );

        elapsed += Time.deltaTime;
        yield return null;
    }

    candy.transform.position = targetPosition;
}

IEnumerator ProcessBoard()
{
    yield return new WaitForSeconds(0.2f);

    List<Candy> matches = GetMatches();

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
}
}