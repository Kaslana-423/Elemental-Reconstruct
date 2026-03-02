using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("配置")]
    public List<GameObject> enemyPrefabs; // 怪物列表
    public GameObject warningPrefab;      // 【新】拖入刚才做的 Warning Prefab

    public Transform player;
    public float spawnInterval = 1f;
    public float minRadius = 10f;
    public float maxRadius = 15f;

    private float timer;
    [Header("生成区域")]
    public Collider2D spawnAreaCollider; // 【新】把场景里的 SpawnArea 拖进来
    void Update()
    {
        if (player == null) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnWarning(); // 改名：先生成预警
            timer = 0f;
        }
    }
    Vector3 FindValidPosition()
    {
        // 尝试找 15 次
        for (int i = 0; i < 15; i++)
        {
            // === 步骤 A: 依然先在玩家周围随机 ===
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(minRadius, maxRadius);
            Vector3 potentialPos = player.position + (Vector3)(randomDir * dist);

            // === 步骤 B: 检查这个点是否在 "生成区域" 内 ===
            // OverlapPoint: 检查一个点是否在 Collider 内部
            if (spawnAreaCollider.OverlapPoint(potentialPos))
            {
                // === 步骤 C: 依然要检查是不是撞到了墙壁障碍物 (比如地图中间的石柱) ===
                if (Physics2D.OverlapCircle(potentialPos, 0.4f, LayerMask.GetMask("Wall")) == null)
                {
                    return potentialPos; // 完美位置
                }
            }
        }

        // 如果随了15次都在墙外，作为保底，直接在玩家附近刷一个安全位置
        // 或者直接返回 zero (但这会导致刷怪卡顿)
        return player.position + Vector3.right * 5f;
    }
    void SpawnWarning()
    {
        if (enemyPrefabs.Count == 0) return;

        // 1. 选怪
        GameObject selectedEnemy = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];

        // 2. 找位置 (包含之前的防卡墙逻辑)
        Vector3 spawnPos = FindValidPosition();
        if (spawnPos == Vector3.zero) return; // 没找到位置就不刷了

        // 3. 【核心变化】生成预警圈，而不是怪物
        GameObject warningObj = ProjectTilePoolManager.Instance.Spawn(
            warningPrefab,
            spawnPos,
            Quaternion.identity
        );

        // 4. 告诉预警圈：等会你要变成哪种怪
        SpawnWarningController warningScript = warningObj.GetComponent<SpawnWarningController>();
        warningScript.Setup(selectedEnemy);
    }

}
