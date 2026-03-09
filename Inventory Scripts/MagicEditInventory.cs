using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New MagicEdit Inventory", menuName = "Inventory/MagicEdit Inventory")]
public class MagicEditInventory : ScriptableObject
{
    public MagicItem OriginalMagicItem;
    public MagicItem ModifiedMagicItem1;
    public MagicItem ModifiedMagicItem2;
    public MagicItem TriggerMagicItem;
}
