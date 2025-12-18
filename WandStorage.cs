using UnityEngine;

[System.Serializable] // 关键：加上这个，才能在 Inspector 里看到折叠菜单
public class WandStorage
{
    public string storageName = "Default Wand"; // 给仓库起个名，方便调试

    // 仓库里的具体内容
    public MagicItem originalMagic;
    public MagicItem modifiedMagic1;
    public MagicItem modifiedMagic2;
    public MagicItem triggerMagic;

    // 一个方便的方法：检查这个仓库是不是空的
    public bool IsEmpty()
    {
        return originalMagic == null;
    }

    // 清空仓库
    public void Clear()
    {
        originalMagic = null;
        modifiedMagic1 = null;
        modifiedMagic2 = null;
        triggerMagic = null;
    }
    public float GetFinalFireDelay(float baseInterval)
    {
        // 如果槽位为空，直接返回基础值
        if (originalMagic == null)
        {
            return baseInterval;
        }

        // 1. 获取原件的延迟修正
        float spellModifier = originalMagic.stats.delayMod;

        // 2. 累加所有修饰符的延迟修正
        float modifierSum = 0f;

        // 注意：MagicItem 的 operator+ 已经处理了属性叠加
        // 所以这里只需要分别加上每个修饰符的 stats.delayModifier 即可
        if (modifiedMagic1 != null) modifierSum += modifiedMagic1.stats.delayMod;
        if (modifiedMagic2 != null) modifierSum += modifiedMagic2.stats.delayMod;

        // 3. 计算最终结果
        // 最终延迟 = 基础延迟 + 原件修正 + 修饰符修正之和
        float finalDelay = baseInterval + spellModifier + modifierSum;

        // 4. 【安全底线】防止射速过快导致卡死或延迟变成负数
        // 设置最小间隔为 0.05秒 (即每秒最多20发)
        return Mathf.Max(0.05f, finalDelay);
    }
    public float GetFinalManaCost()
    {
        // 如果槽位为空，消耗为 0
        if (originalMagic == null)
        {
            return 0f;
        }

        // 1. 获取原件的基础消耗
        float baseCost = originalMagic.stats.mpCost;

        // 2. 累加所有修饰符的消耗修正
        float modifierSum = 0f;

        // MagicItem 的 operator+ 已经处理了 stats 里的 manaCost 叠加
        // CSV 里的修饰符如果是 "+5"，这里的 manaCost 就是 5；如果是 "*1.5"，需要你的 CSV 解析器处理好
        // 这里假设你的 CSV 解析器已经把修饰符的 manaCost 解析成了需要叠加的数值
        if (modifiedMagic1 != null) modifierSum += modifiedMagic1.stats.mpCost;
        if (modifiedMagic2 != null) modifierSum += modifiedMagic2.stats.mpCost;
        if (triggerMagic != null) modifierSum += triggerMagic.stats.mpCost;

        // 3. 计算最终结果
        float finalCost = baseCost + modifierSum;

        // 4. 【安全底线】防止消耗变成负数（除非你设计了回蓝法术）
        return Mathf.Max(0f, finalCost);
    }
}