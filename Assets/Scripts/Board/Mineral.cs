using UnityEngine;

public class Mineral : MonoBehaviour
{
    public int x;
    public int y;
    public MineralType mineralType;

    private SpriteRenderer spriteRenderer;
    private Vector3 normalScale;
    private Vector3 selectedScale;

    private Color[] colors =
    {
        new Color(0.15f, 0.15f, 0.15f), // Carbon
        new Color(0.72f, 0.35f, 0.12f), // Copper
        new Color(0.75f, 0.75f, 0.75f), // Silver
        new Color(1f, 0.75f, 0.05f),    // Gold
        Color.green,                    // Emerald
        Color.blue,                     // Sapphire
        Color.red,                      // Ruby
        new Color(0.7f, 0f, 1f),         // Amethyst
        Color.cyan                      // Diamond
    };

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        normalScale = transform.localScale;
        selectedScale = normalScale * 1.2f;

        int randomIndex = Random.Range(0, colors.Length);
        mineralType = (MineralType)randomIndex;

        spriteRenderer.color = colors[randomIndex];
    }

    public void SetPosition(int newX, int newY)
    {
        x = newX;
        y = newY;
    }

    private void OnMouseDown()
    {
        BoardManager board = FindFirstObjectByType<BoardManager>();
        board.SelectMineral(this);
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