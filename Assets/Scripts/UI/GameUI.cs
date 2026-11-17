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

    /// <summary>
    /// Esconde el panel viejo de textos (movimientos/puntos/objetivos) porque
    /// ahora los muestra GameHud. Busca el "InfoPanel" que los contiene; si no
    /// lo encuentra, esconde la tarjeta de cada texto.
    /// </summary>
    public void HideLegacyPanel()
    {
        foreach (TMP_Text text in new[] { movesText, scoreText, objectivesText })
        {
            if (text == null) continue;

            Transform panel = text.transform;
            while (panel != null && panel.name != "InfoPanel") panel = panel.parent;

            if (panel != null) panel.gameObject.SetActive(false);
            else if (text.transform.parent != null) text.transform.parent.gameObject.SetActive(false);
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
