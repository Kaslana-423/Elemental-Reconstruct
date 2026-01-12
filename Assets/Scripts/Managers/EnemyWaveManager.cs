using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// --- 数据定义部分 ---

[System.Serializable]
public class SpawnRule
{
    [Header("怪物预制体")]
    public GameObject enemyPrefab;

    [Header("刷怪时间段 (相对于当前波次开始)")]
    public float startTimestamp = 0f; // 从波次开始第几秒开始刷
    public float endTimestamp = 60f;   // 到第几秒停止

    [Header("刷怪频率")]
    public float spawnInterval = 2f; // 每隔几秒刷一次

    [Header("数量与成群设置")]
    public int minCountPerSpawn = 1; // 每次刷几个（下限）
    public int maxCountPerSpawn = 1; // 每次刷几个（上限）
    public bool isGrouped = false;   // 是否成群生成（true=聚在一起，false=散开）
    [Header("地图边界设置")]

    // 内部计时器，不需要在面板配置
    [HideInInspector] public float nextSpawnTime = 0f;
}

[System.Serializable]
public class WaveData
{
    [Header("本波次持续时间 (秒)")]
    public float waveDuration = 60f;

    [Header("本波次包含的所有刷怪规则")]
    public List<SpawnRule> spawnRules;
}

// --- 逻辑管理器部分 ---

public class EnemyWaveManager : MonoBehaviour
{
    public static EnemyWaveManager Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    [Header("波次配置列表 (按顺序填入第1波到第20波)")]
    public List<WaveData> allWaves;

    [Header("生成设置")]
    public Transform playerTransform; // 玩家位置
    public float spawnRadius;   // 在玩家多少米外生成
    public float groupSpacing = 1f;   // 成群生成时，怪之间的间距
    public Vector2 mapMin; // 地图左下角坐标
    public Vector2 mapMax;   // 地图右上角坐标
    [Header("调试选项")]

    // 运行时状态
    public int currentWaveIndex = 0;
    public float currentWaveTime = 0f;
    private bool isWaveActive = false;

    void Start()
    {
        // 自动找玩家
        if (playerTransform == null && GameObject.FindWithTag("Player"))
        {
            playerTransform = GameObject.FindWithTag("Player").transform;
        }

        // 以前是自动开始，现在改为手动调用 StartCombat() 触发
        // StartWave(0); 
    }

    // --- 外部调用接口 ---
    // 在UI按钮事件里调用这个方法即可开始游戏
    public void StartCombat()
    {
        if (!isWaveActive)
        {
            StartWave(currentWaveIndex);
        }
    }

    void Update()
    {
        if (!isWaveActive || currentWaveIndex >= allWaves.Count) return;

        // 1. 更新波次时间
        currentWaveTime += Time.deltaTime;
        WaveData currentWave = allWaves[currentWaveIndex];

        // 2. 检查本波次是否结束
        if (currentWaveTime > currentWave.waveDuration)
        {
            // 防止重复调用
            StartCoroutine(NextWaveRoutine());
            isWaveActive = false;
            return;
        }

        // 3. 遍历当前波次的所有规则，看谁该刷怪了
        foreach (var rule in currentWave.spawnRules)
        {
            // 检查时间段：当前时间是否在 [开始, 结束] 范围内
            if (currentWaveTime >= rule.startTimestamp && currentWaveTime <= rule.endTimestamp)
            {
                // 检查冷却时间
                if (currentWaveTime >= rule.nextSpawnTime)
                {
                    SpawnEnemies(rule);
                    // 更新下一次刷怪时间 = 当前时间 + 间隔
                    rule.nextSpawnTime = currentWaveTime + rule.spawnInterval;
                }
            }
        }
    }

    void StartWave(int index)
    {
        currentWaveIndex = index;
        currentWaveTime = 0f;
        isWaveActive = true;

        // 重置该波次所有规则的计时器
        if (index < allWaves.Count)
        {
            foreach (var rule in allWaves[index].spawnRules)
            {
                // 第一只怪生成的时刻 = 规则定义的开始时间
                rule.nextSpawnTime = rule.startTimestamp;
            }
            Debug.Log($"第 {index + 1} 波开始！");
        }
    }

    IEnumerator NextWaveRoutine()
    {
        // 1. 切换波次时，清理战场并吸附金币 (金币开始飞向玩家)
        CleanUpBattlefield();
        var pc = playerTransform.GetComponent<PlayerController>();
        Debug.Log("Waiting for coins...");
        // 2. 等待 2 秒，让金币有时间飞过来
        isWaveActive = false;
        SaveManager.Instance.SaveGame();
        if (currentWaveIndex + 1 < allWaves.Count)
        {
            // --- 暂停游戏逻辑 
            // 停止移动并打开商店

            if (pc != null)
            {
                pc.StopMoveAndAttack();
                yield return new WaitForSeconds(2.0f);
                currentWaveIndex++;
                pc.OpenShop();
            }
            // 尝试回血
            var charScript = playerTransform.GetComponent<Character>();
            if (charScript != null) charScript.HealFull();
        }
        else
        {
            Debug.Log("所有波次结束，胜利！");
            isWaveActive = false;
        }
    }

    /// <summary>
    /// 清理战场：回收所有敌人，吸附所有金币
    /// </summary>
    void CleanUpBattlefield()
    {
        // 1. 回收所有活着的敌人
        // 注意：这里使用 FindObjectsOfType 可能会比较耗时，但在波次切换时卡顿一帧通常可以接受
        // 如果想要高性能，可以让 ObjectPoolManager 维护一个 activeEnemies 列表
        EnemyBase[] activeEnemies = FindObjectsOfType<EnemyBase>();
        foreach (var enemy in activeEnemies)
        {
            if (enemy.gameObject.activeInHierarchy)
            {
                // 直接回收，不触发 EnemyBase.Die() 里的掉落逻辑
                if (ObjectPoolManager.Instance != null)
                {
                    ObjectPoolManager.Instance.ReturnToPool(enemy.gameObject, enemy.gameObject);
                }
                else
                {
                    enemy.gameObject.SetActive(false);
                }
            }
        }

        // 2. 让所有掉落的金币飞向玩家
        Coin[] activeCoins = FindObjectsOfType<Coin>();
        foreach (var coin in activeCoins)
        {
            if (coin.gameObject.activeInHierarchy)
            {
                // 调用 Coin 脚本里的吸附方法
                coin.StartMagnet(playerTransform);
            }
        }
    }

    // --- 核心生成逻辑 ---
    void SpawnEnemies(SpawnRule rule)
    {
        if (rule.enemyPrefab == null)
        {
            Debug.LogWarning($"[EnemyWaveManager] Wave {currentWaveIndex}: SpawnRule has missing Enemy Prefab!");
            return;
        }

        // 1. 确定生成数量
        int count = Random.Range(rule.minCountPerSpawn, rule.maxCountPerSpawn + 1);

        // 2. 确定生成中心点 (在玩家周围随机一个角度)
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        Vector3 centerPos = playerTransform.position + (Vector3)randomDir * spawnRadius;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos;

            if (rule.isGrouped)
            {
                // 成群：在中心点附近随机小范围偏移
                Vector2 offset = Random.insideUnitCircle * groupSpacing;
                spawnPos = centerPos + (Vector3)offset;
            }
            else
            {
                if (i > 0)
                {
                    randomDir = Random.insideUnitCircle.normalized;
                    spawnPos = playerTransform.position + (Vector3)randomDir * spawnRadius;
                }
                else
                {
                    spawnPos = centerPos;
                }
            }
            spawnPos.x = Mathf.Clamp(spawnPos.x, mapMin.x, mapMax.x);
            spawnPos.y = Mathf.Clamp(spawnPos.y, mapMin.y, mapMax.y);

            // 3. 生成
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Spawn(rule.enemyPrefab, spawnPos, Quaternion.identity);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (playerTransform != null)
            Gizmos.DrawWireSphere(playerTransform.position, spawnRadius);

        Gizmos.color = Color.red;
        // 画出地图边界
        Vector3 center = new Vector3((mapMin.x + mapMax.x) / 2, (mapMin.y + mapMax.y) / 2, 0);
        Vector3 size = new Vector3(mapMax.x - mapMin.x, mapMax.y - mapMin.y, 0);
        Gizmos.DrawWireCube(center, size);
    }
}