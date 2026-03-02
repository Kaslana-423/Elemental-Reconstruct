using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnWarningController : MonoBehaviour
{
    [Header("配置")]
    public float warningDuration = 1.5f; // 预警时间

    // 动画曲线：让缩放看起来更有弹性 (0 -> 1)
    public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private GameObject enemyPrefabToSpawn; // 记录要生成哪种怪
    private float timer;

    // 【核心】初始化：Spawner 告诉我要生什么怪
    public void Setup(GameObject enemyPrefab)
    {
        this.enemyPrefabToSpawn = enemyPrefab;
    }

    void OnEnable()
    {
        timer = 0f;
        transform.localScale = Vector3.zero; // 一开始看不见
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 1. 处理视觉动画 (从小变大)
        float progress = timer / warningDuration;
        float currentScale = scaleCurve.Evaluate(progress);
        transform.localScale = new Vector3(currentScale, currentScale, 1f);

        // 2. 时间到了，生成真正的怪物
        if (timer >= warningDuration)
        {
            SpawnRealEnemy();
        }
    }

    void SpawnRealEnemy()
    {
        if (enemyPrefabToSpawn != null)
        {
            // 强制把 Z 设置为 0
            Vector3 spawnPos = transform.position;
            spawnPos.z = 0f;

            ProjectTilePoolManager.Instance.Spawn(
                enemyPrefabToSpawn,
                spawnPos, // 使用修正后的坐标
                Quaternion.identity
            );
        }
        ProjectTilePoolManager.Instance.Despawn(gameObject);
    }
}
