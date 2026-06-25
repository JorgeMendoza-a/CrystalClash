using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Mineral selectedMineral;

    private void Awake()
    {
        Instance = this;
    }
}