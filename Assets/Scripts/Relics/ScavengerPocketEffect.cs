using UnityEngine;

[CreateAssetMenu(menuName = "Relic System/Effects/Scavenger Pocket")]
public class ScavengerPocketEffect : RelicEffect
{
    public float goldMultiplier = 1.3f;
    public float pickupRangeMultiplier = 1.5f;

    public override void OnEquip(Character player)
    {
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            inventory = PlayerInventory.PlayerInstance;
        }

        if (inventory == null)
        {
            Debug.LogWarning("ScavengerPocketEffect: PlayerInventory not found.");
            return;
        }

        if (goldMultiplier > 0f)
        {
            inventory.goldGainMultiplier *= goldMultiplier;
        }

        if (pickupRangeMultiplier > 0f)
        {
            inventory.pickupRangeMultiplier *= pickupRangeMultiplier;
        }

        PickUp[] pickUps = player.GetComponentsInChildren<PickUp>(true);
        for (int i = 0; i < pickUps.Length; i++)
        {
            pickUps[i].RefreshRangeFromRelics();
        }
    }

    public override void OnEquip(EnemyBase enemy)
    {
        // Not used for enemies.
    }
}
