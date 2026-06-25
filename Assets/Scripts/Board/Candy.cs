using UnityEngine;

public class Candy : MonoBehaviour
{
    public int x;
    public int y;
    public int colorId;

    private SpriteRenderer spriteRenderer;
    private Vector3 normalScale;
    private Vector3 selectedScale;

    private Color[] colors =
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        new Color(0.7f, 0f, 1f)
    };

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        normalScale = transform.localScale;
        selectedScale = normalScale * 1.2f;

        colorId = Random.Range(0, colors.Length);
        spriteRenderer.color = colors[colorId];
    }

    public void SetPosition(int newX, int newY)
    {
        x = newX;
        y = newY;
    }

    private void OnMouseDown()
    {
        BoardManager board = FindFirstObjectByType<BoardManager>();
        board.SelectCandy(this);
    }

    public void Select()
    {
        transform.localScale = selectedScale;
    }

    public void Deselect()
    {
        transform.localScale = normalScale;
    }
}