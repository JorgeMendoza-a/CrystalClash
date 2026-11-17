using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance;
    public TMP_Text movesText;

    public TMP_Text scoreText;

    private void Awake()
    {
        Instance = this;
    }

public void UpdateMoves(int moves)
{
    movesText.text = "Movimientos: " + moves;
}
    private void Start()
    {
        UpdateScore(0);
    }

    public void UpdateScore(int score)
    {
        scoreText.text = "Puntos: " + score;
    }
}