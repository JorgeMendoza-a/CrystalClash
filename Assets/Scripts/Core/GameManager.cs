using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Mineral selectedMineral;

    [Header("Partida")]
    public int maxMoves = 25;

    public int CurrentMoves { get; private set; }

    /// <summary>True cuando ya no quedan movimientos (el tablero deja de aceptar jugadas).</summary>
    public bool IsOutOfMoves => CurrentMoves <= 0;

    private void Awake()
    {
        Instance = this;
        CurrentMoves = maxMoves;
    }

    private void Start()
    {
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateMoves(CurrentMoves);
    }

    public void UseMove()
    {
        if (CurrentMoves > 0) CurrentMoves--;

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateMoves(CurrentMoves);

        if (CurrentMoves <= 0)
        {
            Debug.Log("¡Sin movimientos!");
        }
    }
}
