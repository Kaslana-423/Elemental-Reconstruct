using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MagicType
{
    Projectile,
    Modifier
}

[System.Serializable]
public struct SpellStats
{
    [Header("基础战斗属性")]
    public float damage;
    public int count;
    public int penetration;
    public float mpCost;

    [Header("行为属性")]
    public int bounceCount;       // 弹射次数 (激光反射次数)
    public float lifetime;        // 寿命 (瞬发激光为视觉残留时间，持续激光为照射时间)
    public float damageRate;      // 伤害频率 (0=瞬发一次性; >0=每隔多少秒造成一次伤害)
    public float radius;          // 判定半径 (爆炸范围 / 激光粗细)
    public float spread;

    [Header("特殊行为")]
    public bool isOrbiting;
    public bool isLaser;          // 【新增】是否启用激光逻辑
    public float maxDistance;     // 【新增】激光最大射程 (代替 speed 控制长度)
    public float laserWidth;      // 【新增】控制激光的视觉宽度

    [Header("物理运动属性")]
    public float speed;
    public float drag;
    public float acceleration;
    public float angularSpeed;
    public float deviation;
    public bool isHoming;

    [Header("高级属性")]
    public int rarity;
    public float critRate;
    public float critMultiplier;
    public float knockback;

    [Header("修正属性")]
    public float delayMod;
    public float chargeMod;

    // 运算符重载：叠加属性
    public static SpellStats operator +(SpellStats a, SpellStats b)
    {
        a.damage += b.damage;
        a.count += b.count;
        a.penetration += b.penetration;
        a.mpCost += b.mpCost;
        a.bounceCount += b.bounceCount;
        a.lifetime += b.lifetime;
        a.damageRate += b.damageRate; // 频率叠加
        a.radius += b.radius;
        a.spread += b.spread;
        a.speed += b.speed;
        a.drag += b.drag;
        a.acceleration += b.acceleration;
        a.angularSpeed += b.angularSpeed;
        a.deviation += b.deviation;
        a.isHoming = a.isHoming || b.isHoming;
        a.critRate += b.critRate;
        a.critMultiplier += b.critMultiplier;
        a.knockback += b.knockback;
        a.delayMod += b.delayMod;
        a.chargeMod += b.chargeMod;
        a.isOrbiting = a.isOrbiting || b.isOrbiting;

        // 【新增】激光属性叠加
        a.isLaser = a.isLaser || b.isLaser;
        a.maxDistance += b.maxDistance;
        a.laserWidth += b.laserWidth; // 【新增】视觉宽度也能被修饰符叠加

        return a;
    }
}

[CreateAssetMenu(fileName = "New Magic Item", menuName = "Inventory/MagicItem")]
public class MagicItem : ScriptableObject
{
    public string itemID;
    [Header("基本信息")]
    public string itemName;
    public Sprite itemImage;
    [TextArea] public string itemDescription;

    [Header("核心配置")]
    public MagicType type;
    public GameObject itemPrefab;

    [Header("数值配置")]
    public SpellStats stats;
}