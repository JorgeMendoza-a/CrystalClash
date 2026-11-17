using CrystalClash.Board.Logic;
using UnityEngine;

/// <summary>
/// Vista de un mineral. Solo muestra el color/especial y avisa al tablero
/// cuando se hace clic. El tipo lo decide BoardLogic, nunca el propio mineral.
///
/// Los especiales se dibujan con una marca encima (un hijo con el mismo sprite)
/// hasta que tengamos arte propio:
/// - Taladro horizontal: barra blanca horizontal.
/// - Taladro vertical:   barra blanca vertical.
/// - Dinamita:           cuadro oscuro en el centro.
/// - Prisma:             blanco con colores que van cambiando.
/// </summary>
public class Mineral : MonoBehaviour
{
    public int x;
    public int y;
    public MineralType mineralType;
    public SpecialKind special;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer marker;
    private BoardManager board;
    private Vector3 normalScale;
    private Vector3 selectedScale;

    // Mismo orden que el enum MineralType.
    private static readonly Color[] colors =
    {
        new Color(0.15f, 0.45f, 1f),    // Sapphire
        new Color(0.15f, 0.8f, 0.25f),  // Emerald
        new Color(0.65f, 0.2f, 0.95f),  // Amethyst
        new Color(0.9f, 0.1f, 0.15f),   // Ruby
        new Color(1f, 0.78f, 0.1f),     // Gold
        new Color(0.4f, 0.4f, 0.45f),   // Carbon
        new Color(0.72f, 0.35f, 0.12f), // Copper
        new Color(0.85f, 0.85f, 0.85f), // Silver
        Color.cyan                      // Diamond
    };

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        board = GetComponentInParent<BoardManager>();
        normalScale = transform.localScale;
        selectedScale = normalScale * 1.2f;
    }

    void Update()
    {
        if (special == SpecialKind.Prism)
        {
            float hue = Mathf.Repeat(Time.time * 0.5f + (x + y) * 0.1f, 1f);
            spriteRenderer.color = Color.HSVToRGB(hue, 0.35f, 1f);
        }
    }

    public void SetPosition(int newX, int newY)
    {
        x = newX;
        y = newY;
    }

    private void OnMouseDown()
    {
        if (board == null)
        {
            board = FindFirstObjectByType<BoardManager>();
        }

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

    public void SetMineralType(MineralType newType)
    {
        SetPiece((int)newType, special);
    }

    /// <summary>Pone el tipo (o BoardLogic.Colorless para el Prisma) y el especial.</summary>
    public void SetPiece(int type, SpecialKind newSpecial)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        special = newSpecial;

        if (type >= 0 && type < colors.Length)
        {
            mineralType = (MineralType)type;
            spriteRenderer.color = colors[type];
        }
        else
        {
            spriteRenderer.color = Color.white;
        }

        UpdateMarker();
    }

    void UpdateMarker()
    {
        if (special == SpecialKind.None)
        {
            if (marker != null) marker.enabled = false;
            return;
        }

        if (marker == null)
        {
            var child = new GameObject("SpecialMarker");
            child.transform.SetParent(transform, false);
            marker = child.AddComponent<SpriteRenderer>();
            marker.sprite = spriteRenderer.sprite;
            marker.sortingLayerID = spriteRenderer.sortingLayerID;
            marker.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        marker.enabled = true;

        switch (special)
        {
            case SpecialKind.LineH:
                marker.transform.localScale = new Vector3(0.9f, 0.22f, 1f);
                marker.color = new Color(1f, 1f, 1f, 0.9f);
                break;

            case SpecialKind.LineV:
                marker.transform.localScale = new Vector3(0.22f, 0.9f, 1f);
                marker.color = new Color(1f, 1f, 1f, 0.9f);
                break;

            case SpecialKind.Bomb:
                marker.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                marker.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                break;

            case SpecialKind.Prism:
                marker.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                marker.color = Color.white;
                break;
        }
    }
}
