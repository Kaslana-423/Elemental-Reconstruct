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
            if (shopInstance.availableItems.Count == 0) break;

            Debug.Log("Generating Shop Item " + i);
            int randomIndex = Random.Range(0, shopInstance.availableItems.Count);

            GameObject newShopItem = Instantiate(shopInstance.emptyShopItem, shopInstance.slotGrid.transform);
            ShopThing newThingScript = newShopItem.GetComponent<ShopThing>();

            // 添加到列表
            shopInstance.shopItems.Add(newThingScript);

            // 直接使用新生成的脚本进行设置，而不是通过索引访问列表（更安全）
            newThingScript.SetUpShop(shopInstance.availableItems[randomIndex]);
        }
    }
}
