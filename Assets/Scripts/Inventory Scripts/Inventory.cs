using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MagicInventory", menuName = "Inventory/MagicInventory")]
public class Inventory : ScriptableObject
{
    public List<MagicItem> itemList = new List<MagicItem>();

    public bool AddItem(MagicItem item)
    {
        for (int i = 0; i < itemList.Count; i++)
        {
            if (itemList[i] == null)
            {
                itemList[i] = item;
                InventoryManager.RefreshItem();
                return true;
            }
        }
        return false;
    }

    internal void RemoveItem(MagicItem item, int magicId)
    {
        for (int i = 0; i < itemList.Count; i++)
        {
            if (itemList[i] == item && i == magicId)
            {
                itemList[i] = null;
                break;
            }
        }
        InventoryManager.RefreshItem();
    }
}