using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// 定义单个刷怪规则
[System.Serializable]
public class SpawnRule
{
    public GameObject enemyPrefab; // 对应的怪物预制体
    public float startTimestamp;   // 开始时间
    public float endTimestamp;     // 结束时间
    public float spawnInterval;    // 刷新间隔
    public int minCount;           // 最小数量
    public int maxCount;           // 最大数量
    public bool isGrouped;         // 是否成群生成 ('T'/'F')

    // 运行时用的计时器，不需要在面板显示
    [HideInInspector] public float nextSpawnTime;
}

// 定义整个波次的数据
[System.Serializable]
public class WaveData
{
    public int waveIndex;          // 波次序号
    public float waveDuration;     // 波次总时长
    public List<SpawnRule> rules = new List<SpawnRule>();
}

public class EnemyWaveManager : MonoBehaviour
{
    public static EnemyWaveManager Instance;

    [Header("配置")]
    [Tooltip("将导出的 CSV 文件拖到这里")]
    public TextAsset waveCsvFile;
    
    [Tooltip("怪物库：请将所有用到的怪物预制体拖进来，名字必须和Excel里的 'Prefab' 列一致")]
    public List<GameObject> enemyLibrary;

    [Header("生成设置")]
    public Transform playerTransform; 
    public float spawnRadius = 10f; // 刷怪距离玩家的半径
    public Vector2 mapMin = new Vector2(-50, -50);
    public Vector2 mapMax = new Vector2(50, 50);

    [Header("运行时状态 (只读)")]
    public List<WaveData> allWaves = new List<WaveData>();
    public int currentWaveIndex = 0;
    public float currentWaveTime = 0f; // Renamed from waveTimer
    public bool isWaveActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        LoadWavesFromCSV();
    }

    private void Start()
    {
        // 自动寻找玩家
        if (playerTransform == null)
            playerTransform = GameObject.FindWithTag("Player")?.transform;

        // 测试：游戏开始直接启动第1波
        // StartWave(0); // 注释掉这行，改为由玩家在商店界面点击“开始战斗”触发
    }

    // Restore StartCombat for external calls
    public void StartCombat()
    {
        if (!isWaveActive && currentWaveIndex < allWaves.Count)
        {
            StartWave(currentWaveIndex);
        }
    }

    private void Update()
    {
        if (!isWaveActive || currentWaveIndex >= allWaves.Count) return;

        WaveData currentWave = allWaves[currentWaveIndex];
        currentWaveTime += Time.deltaTime;

        // 1. 检查当前波次是否结束
        if (currentWaveTime >= currentWave.waveDuration)
        {
            EndWave();
            return;
        }

        // 2. 遍历当前波次的所有规则，进行刷怪
        foreach (var rule in currentWave.rules)
        {
            // 检查时间段：当前时间是否在 [Start, End] 之间
            if (currentWaveTime >= rule.startTimestamp && currentWaveTime <= rule.endTimestamp)
            {
                // 检查间隔：是否到了下一次生成时间
                if (currentWaveTime >= rule.nextSpawnTime)
                {
                    SpawnEnemies(rule);
                    rule.nextSpawnTime = currentWaveTime + rule.spawnInterval;
                }
            }
        }
    }

    // --- CSV 解析逻辑 (核心部分) ---
    void LoadWavesFromCSV()
    {
        if (waveCsvFile == null)
        {
            Debug.LogError("未设置 CSV 文件！");
            return;
        }

        allWaves.Clear();
        // 建立名字到预制体的映射字典，方便快速查找
        Dictionary<string, GameObject> prefabMap = new Dictionary<string, GameObject>();
        foreach (var p in enemyLibrary)
        {
            if (p != null && !prefabMap.ContainsKey(p.name)) prefabMap.Add(p.name, p);
        }

        // 按行分割
        string[] lines = waveCsvFile.text.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        // 临时字典用于合并同一波的数据
        Dictionary<int, WaveData> waveDict = new Dictionary<int, WaveData>();

        // 从第1行开始（跳过第0行标题）
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            string[] row = line.Split(',');

            // 防止空行或数据不全
            if (row.Length < 9) continue; 

            try 
            {
                // 解析各列数据 (对应Excel列 A-I)
                int waveId = int.Parse(row[0]);          // A: Wave
                float duration = float.Parse(row[1]);    // B: Duration
                string prefabName = row[2].Trim();       // C: Prefab
                float start = float.Parse(row[3]);       // D: Start
                float end = float.Parse(row[4]);         // E: End
                float interval = float.Parse(row[5]);    // F: Interval
                int min = int.Parse(row[6]);             // G: Min
                int max = int.Parse(row[7]);             // H: Max
                string groupedStr = row[8].Trim();       // I: Grouped (T/F)
                bool isGrouped = groupedStr.Equals("T", System.StringComparison.OrdinalIgnoreCase);

                // 1. 如果是新波次，先创建 WaveData
                if (!waveDict.ContainsKey(waveId))
                {
                    waveDict.Add(waveId, new WaveData { waveIndex = waveId, waveDuration = duration });
                }

                // 2. 查找预制体
                if (prefabMap.TryGetValue(prefabName, out GameObject prefab))
                {
                    SpawnRule rule = new SpawnRule
                    {
                        enemyPrefab = prefab,
                        startTimestamp = start,
                        endTimestamp = end,
                        spawnInterval = interval,
                        minCount = min,
                        maxCount = max,
                        isGrouped = isGrouped,
                        nextSpawnTime = start // 初始下次生成时间 = 开始时间
                    };
                    waveDict[waveId].rules.Add(rule);
                }
                else
                {
                    Debug.LogWarning($"CSV 第 {i+1} 行警告: 找不到名为 '{prefabName}' 的预制体，请检查 Enemy Library。");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"CSV 第 {i+1} 行解析错误: {ex.Message}");
            }
        }

        // 将字典转为 List 并排序 (防止Excel乱序)
        allWaves = waveDict.Values.OrderBy(w => w.waveIndex).ToList();
        Debug.Log($"解析完成，共加载 {allWaves.Count} 波数据。");
    }

    // --- 游戏逻辑 ---

    void StartWave(int index)
    {
        if (index >= allWaves.Count) return;

        currentWaveIndex = index;
        currentWaveTime = 0f;
        isWaveActive = true;

        // 重置所有规则的计时器
        foreach(var rule in allWaves[index].rules)
        {
            rule.nextSpawnTime = rule.startTimestamp;
        }

        Debug.Log($"第 {allWaves[index].waveIndex} 波开始！(持续 {allWaves[index].waveDuration}秒)");
    }

    void EndWave()
    {
        isWaveActive = false;
        Debug.Log($"第 {allWaves[currentWaveIndex].waveIndex} 波结束。");

        // 这里可以处理波次间隔逻辑，比如弹出商店
        StartCoroutine(WaveCompletedRoutine());
    }

    IEnumerator WaveCompletedRoutine()
    {
        // 1. 清理战场 (回收剩余敌人，吸附金币)
        CleanUpBattlefield();
        
        // 2. 玩家回血
        if (playerTransform != null)
        {
            var charScript = playerTransform.GetComponent<Character>();
            if (charScript != null) charScript.HealFull();
        }

        // 3. 存档 (如果有)
        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame();

        yield return new WaitForSeconds(2.0f);

        // 4. 判断是否还有下一波
        if (currentWaveIndex + 1 < allWaves.Count)
        {
            currentWaveIndex++; // 准备下一波的索引

            // 5. 打开商店
            if (playerTransform != null)
            {
                var pc = playerTransform.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.StopMoveAndAttack();
                    pc.OpenShop();
                }
            }
        }
        else
        {
            Debug.Log("所有波次已完成！胜利！");
            // 这里可以调用 GameManager 里的 Victory() 方法
        }
    }

    void CleanUpBattlefield()
    {
        // 回收所有敌人
        EnemyBase[] activeEnemies = FindObjectsOfType<EnemyBase>();
        foreach (var enemy in activeEnemies)
        {
            if (enemy.gameObject.activeInHierarchy)
            {
                if (ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.ReturnToPool(enemy.gameObject, enemy.gameObject);
                else
                    enemy.gameObject.SetActive(false);
            }
        }

        // 吸附所有金币
        Coin[] activeCoins = FindObjectsOfType<Coin>();
        foreach (var coin in activeCoins)
        {
            if (coin.gameObject.activeInHierarchy)
                coin.StartMagnet(playerTransform);
        }
    }
/*
    IEnumerator WaitAndStartNextWave(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentWaveIndex + 1 < allWaves.Count)
        {
            StartWave(currentWaveIndex + 1);
        }
        else
        {
            Debug.Log("所有波次已完成！胜利！");
        }
    }
*/
    void SpawnEnemies(SpawnRule rule)
    {
        if (rule.enemyPrefab == null || playerTransform == null) return;

        int count = Random.Range(rule.minCount, rule.maxCount + 1);
        
        // 生成中心点 (玩家周围随机方向)
        Vector2 spawnCenterDir = Random.insideUnitCircle.normalized;
        Vector3 spawnCenterPos = playerTransform.position + (Vector3)spawnCenterDir * spawnRadius;

        for (int i = 0; i < count; i++)
        {
            Vector3 finalPos;

            if (rule.isGrouped)
            {
                // 成群：都在中心点附近小范围随机
                Vector2 offset = Random.insideUnitCircle * 1.5f; 
                finalPos = spawnCenterPos + (Vector3)offset;
            }
            else
            {
                // 散开：如果是复数个，每一个都重新随机大方向（这里偷懒直接沿用中心点，或者你可以重新算）
                // 你的表格里 T 的通常是多数量，F 的通常是单数量，所以直接用 finalPos = spawnCenterPos 也没问题
                if (i == 0) finalPos = spawnCenterPos; 
                else 
                {
                    // 只有 F 且数量>1 时才会走到这，稍微散开一点
                     finalPos = spawnCenterPos + (Vector3)(Random.insideUnitCircle * 2f);
                }
            }

            // 限制在地图范围内
            finalPos.x = Mathf.Clamp(finalPos.x, mapMin.x, mapMax.x);
            finalPos.y = Mathf.Clamp(finalPos.y, mapMin.y, mapMax.y);

            Instantiate(rule.enemyPrefab, finalPos, Quaternion.identity);
        }
    }
}