using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class WaveUI : MonoBehaviour
{
    public TextMeshProUGUI waves; // 拖入你的 TMP 组件
    public TextMeshProUGUI counters;
    private Color defaultColor;

    void Start()
    {
        if (counters != null) defaultColor = counters.color;
    }

    // Update is called once per frame
    void Update()
    {
        if (EnemyWaveManager.Instance == null) return;

        // --- 修复检查 ---
        // 游戏通关或结束时，currentWaveIndex 可能会等于 allWaves.Count
        if (EnemyWaveManager.Instance.currentWaveIndex >= EnemyWaveManager.Instance.allWaves.Count)
        {
            counters.text = "0";
            return;
        }

        waves.text = ((int)EnemyWaveManager.Instance.currentWaveIndex + 1).ToString();

        // 计算剩余时间
        float totalDuration = EnemyWaveManager.Instance.allWaves[EnemyWaveManager.Instance.currentWaveIndex].waveDuration;
        float remainingTime = totalDuration - EnemyWaveManager.Instance.currentWaveTime;

        // 更新文本
        counters.text = ((int)remainingTime).ToString();

        // 倒计时少于5秒变红
        if (remainingTime <= 5f)
        {
            counters.color = Color.red;
        }
        else
        {
            counters.color = defaultColor;
        }
    }
}
