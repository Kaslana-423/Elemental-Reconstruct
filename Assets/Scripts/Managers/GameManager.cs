using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    public List<GameObject> normalEnemies;
    public List<GameObject> attackEnemies;
    public List<GameObject> bossEnemies;
    public static GameManager Instance;
    public int gameProcess = 0; // 游戏进度

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


}
