using UnityEngine;

[CreateAssetMenu(menuName = "Relic System/Effects/Life Steal")]
public class LifeStealEffect : RelicEffect
{
    [Range(0f, 1f)]
    public float lifeStealPercent = 0.05f; // 0.05 = 5%

    public override void OnEquip(Character player)
    {
        player.lifeStealPercent += lifeStealPercent;
    }

    public override void OnEquip(EnemyBase enemy)
    {
        // Not used for enemies.
    }
}
