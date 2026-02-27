using UnityEngine;

[CreateAssetMenu(menuName = "Relic System/Effects/On Hit Damage Boost")]
public class OnHitDamageBoostEffect : RelicEffect
{
    public float amount = 1.2f;
    public bool isMultiplier = true; // true=乘法, false=加法
    public float duration = 2f;

    public override void OnEquip(Character player)
    {
        var controller = player.GetComponent<RelicDamageBuffController>();
        if (controller == null)
        {
            controller = player.gameObject.AddComponent<RelicDamageBuffController>();
        }

        controller.RegisterEffect(player, amount, isMultiplier, duration);
    }

    public override void OnEquip(EnemyBase enemy)
    {
        // Not used for enemies.
    }
}
