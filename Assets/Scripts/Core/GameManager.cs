using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Mineral selectedMineral;

    [Header("Partida")]
    public int maxMoves = 25;

    public int CurrentMoves { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CurrentMoves = maxMoves;

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateMoves(CurrentMoves);
    }

    public void UseMove()
    {
        CurrentMoves--;

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateMoves(CurrentMoves);

        if (CurrentMoves <= 0)
        {
            Debug.Log("¡Sin movimientos!");
        }
    }
}