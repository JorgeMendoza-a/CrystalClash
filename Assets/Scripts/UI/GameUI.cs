using System.Collections;
using CrystalClash.Story.Logic;
using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance;

    public TMP_Text scoreText;
    public TMP_Text movesText;

    [Tooltip("Texto con los objetivos del nivel (\"Zafiro 3/15\"...).")]
    public TMP_Text objectivesText;

    [Tooltip("Opcional: texto para mostrar \"¡Cascada x2!\". Si se deja vacío, no se muestra nada.")]
    public TMP_Text cascadeText;

    public float cascadeTextDuration = 0.8f;

    private Coroutine hideCascadeRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        UpdateScore(0);

        if (cascadeText != null)
        {
            cascadeText.gameObject.SetActive(false);
        }
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = "Puntos: " + score;
        }

        if (GameHud.Instance != null)
        {
            GameHud.Instance.SetScore(score);
        }
    }

    public void UpdateMoves(int moves)
    {
        if (movesText != null)
        {
            movesText.text = "Movimientos: " + moves;
        }
    }

    public void UpdateObjectives(LevelSession session)
    {
        if (objectivesText != null && session != null)
        {
            objectivesText.text = GameManager.DescribeObjectives(session, true);
        }
    }

    public void ShowCascade(int level)
    {
        if (cascadeText == null) return;

        cascadeText.text = "¡Cascada x" + level + "!";
        cascadeText.gameObject.SetActive(true);

        if (hideCascadeRoutine != null) StopCoroutine(hideCascadeRoutine);
        hideCascadeRoutine = StartCoroutine(HideCascadeAfterDelay());
    }

    private IEnumerator HideCascadeAfterDelay()
    {
        yield return new WaitForSeconds(cascadeTextDuration);
        cascadeText.gameObject.SetActive(false);
        hideCascadeRoutine = null;
    }
}
