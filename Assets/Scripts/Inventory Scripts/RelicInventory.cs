using System.Collections.Generic;
using UnityEngine;

public class RelicInventory : MonoBehaviour
{
    // 当前拥有的遗物列表
    public List<RelicData> ownedRelics = new List<RelicData>();

    private Character player;

    void Awake()
    {
        player = GetComponent<Character>();
    }
    void Start()
    {
        RefreshRelics();
    }
    // --- 核心方法：获得遗物 ---
    public void AddRelic(RelicData relic)
    {
        ownedRelics.Add(relic);

        // 遍历这个遗物的所有效果，并立即执行
        foreach (var effect in relic.effects)
        {
            effect.OnEquip(player);
        }

        Debug.Log($"获得了遗物：{relic.relicName}");

        // 这里可以触发 UI 更新事件
    }
    public void RefreshRelics()
    {
        foreach (var relic in ownedRelics)
        {
            foreach (var effect in relic.effects)
            {
                effect.OnEquip(player);
            }
        }
    }
    // 移除遗物（如果需要的话）
    public void RemoveRelic(RelicData relic)
    {
        if (ownedRelics.Contains(relic))
        {
            ownedRelics.Remove(relic);
        }
    }
}