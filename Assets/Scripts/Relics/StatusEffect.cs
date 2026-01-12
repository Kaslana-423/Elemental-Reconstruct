using UnityEngine;

[CreateAssetMenu(menuName = "Relic System/Effects/Stat Boost")]
public class StatBoostEffect : RelicEffect
{
    public enum StatType { MaxHealth, DamageMultiplier, MoveSpeed, RegenRate }

    public StatType targetStat;
    public float amount; // 增加的数值
    public bool isMultiplier; // 是乘法(%)还是加法(+)

    public override void OnEquip(Character player)
    {
        // 假设你的 Character 脚本里有 ModifyStat 方法
        // player.Stats.ModifyStat(targetStat, amount, isMultiplier);
        Debug.Log($"应用效果：{targetStat} 增加 {amount}");

        // 简单示例逻辑：
        if (targetStat == StatType.MaxHealth)
            player.maxHealth += amount;
        // 记得刷新UI
    }
}