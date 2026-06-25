using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Candy selectedCandy;

    private void Awake()
    {
        Instance = this;
    }
}