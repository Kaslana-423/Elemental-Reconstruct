using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MagicType
{
    Projectile, // 原件/触发 (火球, 水球)
    Modifier    // 修饰 (多重, 加速, 追踪)
}

[System.Serializable]
public struct SpellStats
{
    [Header("基础战斗属性")]
    public float damage;          // 伤害
    public int count;             // 数量 (多重施法)
    public int penetration;       // 穿透数
    public float mpCost;          // MP消耗

    [Header("行为属性")]
    public int bounceCount;       // 弹射次数
    public float lifetime;        // 寿命
    public float damageRate;      // 伤害频率 (光环类用)
    public float radius;          // 判定半径 (爆炸/光环)
    public float spread;          // 散射度
    [Header("特殊行为")]
    public bool isOrbiting;       // 是否环绕模式

    [Header("物理运动属性")]
    public float speed;           // 速度
    public float drag;            // 阻力
    public float acceleration;    // 加速度
    public float angularSpeed;    // 角速度 (旋转/追踪用)
    public float deviation;       // 偏移度
    public bool isHoming;         // 是否追踪

    [Header("高级属性")]
    public int rarity;        // 稀有度
    public float critRate;        // 暴击率
    public float critMultiplier;  // 暴击倍率
    public float knockback;       // 击退

    [Header("修正属性")]
    public float delayMod;        // 延迟修正
    public float chargeMod;       // 充能修正

    // 方便的加法运算：将修饰符的属性加到基础属性上
    public static SpellStats operator +(SpellStats a, SpellStats b)
    {
        a.damage += b.damage;
        a.count += b.count;
        a.penetration += b.penetration;
        a.mpCost += b.mpCost;
        a.bounceCount += b.bounceCount;
        a.lifetime += b.lifetime;
        a.damageRate += b.damageRate;
        a.radius += b.radius;
        a.spread += b.spread;
        a.speed += b.speed;
        a.drag += b.drag;
        a.acceleration += b.acceleration;
        a.angularSpeed += b.angularSpeed;
        a.deviation += b.deviation;
        a.isHoming = a.isHoming || b.isHoming; // 布尔值通常取或
        a.critRate += b.critRate;
        a.critMultiplier += b.critMultiplier;
        a.knockback += b.knockback;
        a.delayMod += b.delayMod;
        a.chargeMod += b.chargeMod;
        a.isOrbiting = a.isOrbiting || b.isOrbiting;
        return a;
    }
}

[CreateAssetMenu(fileName = "New Magic Item", menuName = "Inventory/MagicItem")]
public class MagicItem : ScriptableObject
{
    public string itemName;
    public Sprite itemImage;
    [TextArea] public string itemDescription;

    [Header("核心配置")]
    public MagicType type;          // 关键：决定了它能放在哪个槽
    public GameObject itemPrefab;  // 只有 Projectile 类型需要这个

    [Header("数值配置")]
    public SpellStats stats;        // 所有黄色属性都在这里
}
