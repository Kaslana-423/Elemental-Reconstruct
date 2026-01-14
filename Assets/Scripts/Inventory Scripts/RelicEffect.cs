using UnityEngine;

// 这是一个抽象基类，所有具体的遗物效果都要继承它
public abstract class RelicEffect : ScriptableObject
{
    // 当遗物被获得时调用
    public abstract void OnEquip(Character player);
    public abstract void OnEquip(EnemyBase enemy);
}