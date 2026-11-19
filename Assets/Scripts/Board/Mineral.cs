using CrystalClash.Board.Logic;
using UnityEngine;

/// <summary>
/// Vista de un mineral. Solo muestra el color/especial y avisa al tablero
/// cuando se hace clic. El tipo lo decide BoardLogic, nunca el propio mineral.
///
/// Sprites: se cargan de Resources/Minerals/ (sapphire.png, emerald.png...,
/// prism.png y las marcas drill_h.png, drill_v.png, bomb.png). Para cambiar
/// el arte basta con reemplazar esos PNG por otros con el mismo nombre.
/// Si no están, se usa el sprite del prefab pintado de color.
///
/// Especiales: una marca encima del mineral (Taladro = flechas, Dinamita =
/// mecha); el Prisma tiene su propio sprite y va cambiando de color.
/// </summary>
public class Mineral : MonoBehaviour
{
    public int x;
    public int y;
    public MineralType mineralType;
    public SpecialKind special;

    // Nombres de archivo en Resources/Minerals, mismo orden que MineralType.
    private static readonly string[] gemFiles =
    {
        "sapphire", "emerald", "amethyst", "ruby", "gold", "carbon", "copper", "silver", "diamond"
    };

    /// <summary>Tamaño del sprite en unidades (el prefab lo escala a 0.8 de la celda).</summary>
    private const float GemSizeInUnits = 1.12f;

    private static bool spritesLoaded;
    private static Sprite[] gemSprites;
    private static Sprite prismSprite;
    private static Sprite drillHSprite;
    private static Sprite drillVSprite;
    private static Sprite bombSprite;

    /// <summary>Orden de dibujo de los minerales (encima del marco y las casillas del tablero).</summary>
    public const int SortingOrder = 30;

    private SpriteRenderer spriteRenderer;
    private Sprite fallbackSprite;
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
        LoadSprites();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = SortingOrder;
        fallbackSprite = spriteRenderer.sprite;
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
            board = FindAnyObjectByType<BoardManager>();
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
        }

        Sprite gem = GetGemSprite(type, newSpecial);
        if (gem != null)
        {
            spriteRenderer.sprite = gem;
            spriteRenderer.color = Color.white;
        }
        else
        {
            spriteRenderer.sprite = fallbackSprite;
            spriteRenderer.color = type >= 0 && type < colors.Length ? colors[type] : Color.white;
        }

        UpdateMarker();
    }

    static void LoadSprites()
    {
        if (spritesLoaded) return;
        spritesLoaded = true;

        gemSprites = new Sprite[gemFiles.Length];
        for (int i = 0; i < gemFiles.Length; i++)
        {
            gemSprites[i] = LoadSprite(gemFiles[i]);
        }

        prismSprite = LoadSprite("prism");
        drillHSprite = LoadSprite("drill_h");
        drillVSprite = LoadSprite("drill_v");
        bombSprite = LoadSprite("bomb");
    }

    /// <summary>
    /// Carga la textura (funciona la importes como Sprite o como Default) y
    /// crea el sprite con un tamaño fijo en unidades, sin depender de los
    /// Pixels Per Unit de la importación.
    /// </summary>
    static Sprite LoadSprite(string file)
    {
        Texture2D texture = Resources.Load<Texture2D>("Minerals/" + file);
        if (texture == null) return null;

        float pixelsPerUnit = texture.width / GemSizeInUnits;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);

        // Creado por código: que no se descargue al recargar la escena (cambio de nivel).
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    static Sprite GetGemSprite(int type, SpecialKind kind)
    {
        if (kind == SpecialKind.Prism) return prismSprite;
        return gemSprites != null && type >= 0 && type < gemSprites.Length ? gemSprites[type] : null;
    }

    static Sprite GetMarkerSprite(SpecialKind kind)
    {
        switch (kind)
        {
            case SpecialKind.LineH: return drillHSprite;
            case SpecialKind.LineV: return drillVSprite;
            case SpecialKind.Bomb: return bombSprite;
            default: return null;
        }
    }

    void UpdateMarker()
    {
        Sprite overlay = GetMarkerSprite(special);
        bool usePlaceholder = overlay == null && !(special == SpecialKind.Prism && prismSprite != null);

        if (special == SpecialKind.None || (overlay == null && !usePlaceholder))
        {
            if (marker != null) marker.enabled = false;
            return;
        }

        if (marker == null)
        {
            var child = new GameObject("SpecialMarker");
            child.transform.SetParent(transform, false);
            marker = child.AddComponent<SpriteRenderer>();
            marker.sortingLayerID = spriteRenderer.sortingLayerID;
            marker.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        marker.enabled = true;

        if (overlay != null)
        {
            marker.sprite = overlay;
            marker.color = Color.white;
            marker.transform.localScale = Vector3.one;
            return;
        }

        // Sin PNG de marcas: dibujamos formas simples con el sprite del prefab.
        marker.sprite = fallbackSprite;

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
