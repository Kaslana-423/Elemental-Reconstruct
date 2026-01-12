using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    static ShopManager shopInstance;
    public GameObject slotGrid;
    public Inventory myBag;
    public List<ShopThing> shopItems = new List<ShopThing>();
    public List<MagicItem> availableItems = new List<MagicItem>();

    // 假设你也像 availableItems 一样，通过编辑器把所有可能的遗物拖进了这个列表
    public List<RelicData> availableRelics = new List<RelicData>();

    public GameObject emptyShopItem;

    void Awake()
    {
        if (shopInstance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            shopInstance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
    void Start()
    {
        RefreshShop();
    }
    public static bool AdditemToBag(MagicItem item)
    {
        for (int i = 0; i < shopInstance.myBag.itemList.Count; i++)
        {
            if (shopInstance.myBag.itemList[i] == null)
            {
                shopInstance.myBag.itemList[i] = item;
                InventoryManager.RefreshItem();
                return true;
            }
        }
        return false;
    }
    public static void RefreshShop()
    {
        // 1. 【关键修复】清理列表，防止访问到上次已被销毁的物体
        shopInstance.shopItems.Clear();

        // 2. 销毁旧的 UI 物体
        // 使用 foreach 遍历 transform 是最安全且简单的清空子物体方式
        foreach (Transform child in shopInstance.slotGrid.transform)
        {
            Destroy(child.gameObject);
        }

        // 3. 生成新商品
        for (int i = 0; i < 4; i++)
        {
            // 安全检查：防止没有商品数据时报错
            if (shopInstance.availableItems.Count == 0 && shopInstance.availableRelics.Count == 0) break;

            Debug.Log("Generating Shop Item " + i);

            // 决定这次生成道具还是遗物 (如果有遗物列表的话)
            // 简单逻辑：如果两个都有，50%概率。如果只有一种，就只生那种。
            bool spawnRelic = false;

            if (shopInstance.availableRelics.Count > 0 && shopInstance.availableItems.Count > 0)
            {
                // 50% 概率
                spawnRelic = Random.value > 0.5f;
            }
            else if (shopInstance.availableRelics.Count > 0)
            {
                // 只有遗物
                spawnRelic = true;
            }
            else
            {
                // 只有道具
                spawnRelic = false;
            }

            GameObject newShopItem = Instantiate(shopInstance.emptyShopItem, shopInstance.slotGrid.transform);
            ShopThing newThingScript = newShopItem.GetComponent<ShopThing>();

            // 添加到列表
            shopInstance.shopItems.Add(newThingScript);

            if (spawnRelic)
            {
                int relicIndex = Random.Range(0, shopInstance.availableRelics.Count);
                newThingScript.SetUpShop(shopInstance.availableRelics[relicIndex]);
            }
            else
            {
                int itemIndex = Random.Range(0, shopInstance.availableItems.Count);
                newThingScript.SetUpShop(shopInstance.availableItems[itemIndex]);
            }
        }
    }
}
