using UnityEngine;

[CreateAssetMenu(menuName = "Relic System/Effects/Shop Discount")]
public class ShopDiscountEffect : RelicEffect
{
    [Tooltip("商店价格倍率，0.9 = 9折")]
    public float shopPriceMultiplier = 0.9f;

    public override void OnEquip(Character player)
    {
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            inventory = PlayerInventory.PlayerInstance;
        }

        if (inventory == null)
        {
            Debug.LogWarning("ShopDiscountEffect: PlayerInventory not found.");
            return;
        }

        if (shopPriceMultiplier > 0f)
        {
            inventory.shopPriceMultiplier *= shopPriceMultiplier;
        }

        if (ShopManager.HasInstance)
        {
            ShopManager.RecalculateCurrentShopPrices();
        }
    }

    public override void OnEquip(EnemyBase enemy)
    {
        // Not used for enemies.
    }
}
