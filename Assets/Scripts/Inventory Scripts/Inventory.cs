using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MagicInventory", menuName = "Inventory/MagicInventory")]
public class Inventory : ScriptableObject
{
    public List<MagicItem> itemList = new List<MagicItem>();

    internal void RemoveItem(MagicItem item)
    {
        for (int i = 0; i < itemList.Count; i++)
        {
            if (itemList[i] == item)
            {
                itemList[i] = null;
                break;
            }
        }
        InventoryManager.RefreshItem();
    }
}