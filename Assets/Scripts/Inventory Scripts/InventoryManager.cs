using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    static InventoryManager instance;
    public Inventory magicInventory;
    public MagicEditInventory MagicEditInventory1;
    public MagicEditInventory MagicEditInventory2;
    public MagicEditInventory MagicEditInventory3;
    public GameObject MagicSlotGrid;
    public GameObject RelicSlotGrid;
    public GameObject emptySlot;
    public GameObject EditEmptySlot;
    public GameObject EditSlotGrid;
    public List<GameObject> MagicSlots = new List<GameObject>();
    public List<GameObject> EditSlots = new List<GameObject>();
    public int EditLevel;
    void Awake()
    {
        if (instance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
    void OnEnable()
    {
        RefreshItem();
        EditMagicRefresh();
    }
    public static void RefreshAllUI()
    {
        RefreshItem();
        EditMagicRefresh();
    }

    public static void RefreshItem()
    {
        instance.MagicSlots.Clear();
        for (int i = 0; i < instance.MagicSlotGrid.transform.childCount; i++)
        {
            if (instance.MagicSlotGrid.transform.childCount == 0) break;
            Destroy(instance.MagicSlotGrid.transform.GetChild(i).gameObject);
        }
        for (int i = 0; i < instance.magicInventory.itemList.Count; i++)
        {
            GameObject newSlot = Instantiate(instance.emptySlot, instance.MagicSlotGrid.transform);
            instance.MagicSlots.Add(newSlot);
            instance.MagicSlots[i].GetComponent<Slot>().slotID = i;
            instance.MagicSlots[i].GetComponent<Slot>().SetUpSlot(instance.magicInventory.itemList[i]);
        }
    }
    public static void EditMagicRefresh()
    {
        instance.EditSlots.Clear();
        for (int i = 0; i < instance.EditSlotGrid.transform.childCount; i++)
        {
            if (instance.EditSlotGrid.transform.childCount == 0) break;
            Destroy(instance.EditSlotGrid.transform.GetChild(i).gameObject);
        }
        // 假设有3个编辑槽，对应 MagicEditInventory1,2,3
        // 假设每个 MagicEditInventory 有 public MagicItem originalMagic; public MagicItem modified1; public MagicItem modified2; public MagicItem trigger;
        for (int i = 0; i < instance.EditLevel; i++)
        {
            GameObject newSlot = Instantiate(instance.EditEmptySlot, instance.EditSlotGrid.transform);
            instance.EditSlots.Add(newSlot);
            MagicEditSlot slot = newSlot.GetComponent<MagicEditSlot>();
            slot.slotID = i;
            // 获取对应的库存
            MagicEditInventory editInv = null;
            if (i == 0) editInv = instance.MagicEditInventory1;
            else if (i == 1) editInv = instance.MagicEditInventory2;
            else if (i == 2) editInv = instance.MagicEditInventory3;
            // 调用 SetUpSlot
            PlayerInventory.UpdateWandStorage(i, editInv.OriginalMagicItem, editInv.ModifiedMagicItem1, editInv.ModifiedMagicItem2, editInv.TriggerMagicItem);
            slot.SetUpSlot(editInv.OriginalMagicItem, editInv.ModifiedMagicItem1, editInv.ModifiedMagicItem2, editInv.TriggerMagicItem);
        }
    }
}
