using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopThing : MonoBehaviour
{
    [Header("UI 组件")]
    public TextMeshProUGUI coinTextLabel;
    public TextMeshProUGUI itemDescription;
    public Image iconImage;

    [Header("数据")]
    public MagicItem item;
    public RelicData relic; // 新增遗物字段
    public int currentPrice;

    // 记录原本的颜色 (比如白色)
    private Color originalColor;

    // --- 1. 生命周期：开始监听 ---
    void OnEnable()
    {
        // 订阅金币变化事件
        if (PlayerInventory.PlayerInstance != null)
        {
            PlayerInventory.PlayerInstance.OnGoldChanged += CheckAffordability;

            // 刚出生时，先检查一次当前买不买得起
            CheckAffordability(PlayerInventory.PlayerInstance.currentGold);
        }
    }

    // --- 2. 生命周期：销毁时取消监听 ---
    // 【非常重要】商品被购买销毁时，必须取消订阅，否则会报错
    void OnDisable()
    {
        if (PlayerInventory.PlayerInstance != null)
        {
            PlayerInventory.PlayerInstance.OnGoldChanged -= CheckAffordability;
        }
    }

    // --- 3. 核心逻辑：检查是否买得起 ---
    // 这个方法会在金币变化时自动被调用
    void CheckAffordability(int currentGold)
    {
        if (coinTextLabel == null) return;

        if (currentGold >= currentPrice)
        {
            // 买得起 -> 变回原色
            coinTextLabel.color = Color.white; // 或者 originalColor
        }
        else
        {
            // 买不起 -> 变红
            coinTextLabel.color = Color.red;
        }
    }
    // --- 4. 点击购买 ---
    public void SetBuyButton()
    {
        if (PlayerInventory.PlayerInstance == null) return;

        // 1. 检查金币是否足够
        if (PlayerInventory.PlayerInstance.currentGold < currentPrice)
        {
            Debug.Log("金币不足！");
            return;
        }

        bool purchaseSuccess = false;

        // 2. 根据商品类型购买
        if (item != null)
        {
            // 买魔法
            if (InventoryManager.instance.magicInventory.AddItem(item))
            {
                purchaseSuccess = true;
            }
            else Debug.Log("法术背包已满！");
        }
        else if (relic != null)
        {
            // 买遗物 (遗物通常无限背包，直接 Add)
            if (InventoryManager.instance.relicInventory != null)
            {
                InventoryManager.instance.relicInventory.AddRelic(relic);
                purchaseSuccess = true;
            }
        }
        InventoryManager.RefreshItem();
        // 3. 扣钱并销毁
        if (purchaseSuccess)
        {
            PlayerInventory.PlayerInstance.TrySpendGold(currentPrice);
            Destroy(this.gameObject);
        }
    }

    // --- 5. 初始化 ---
    public void SetUpShop(MagicItem magicItem)
    {
        item = magicItem;
        currentPrice = magicItem.stats.rarity * 5;
        if (iconImage != null) iconImage.sprite = magicItem.itemImage;
        if (itemDescription != null) itemDescription.text = magicItem.itemDescription;

        if (coinTextLabel != null)
        {
            coinTextLabel.text = currentPrice.ToString();
            // 记录一下最开始的颜色 (比如白色)
            originalColor = coinTextLabel.color;
        }
        // 注意：SetUpShop 可能会在 OnEnable 之前运行，也可能在之后
        // 为了保险，我们在这里也手动检查一次
        if (PlayerInventory.PlayerInstance != null)
        {
            CheckAffordability(PlayerInventory.PlayerInstance.currentGold);
        }
    }

    // --- 6. 新增重载：用于初始化遗物 ---
    public void SetUpShop(RelicData relicData)
    {
        // 1. 设置商品类型为“遗物” (因为 currentPrice 和 item 字段是共享的，或者你需要新增字段)
        // 这里假设你要卖的是 Relic，但 ShopThing 之前只有 MagicItem item。
        // 你可能需要给 ShopThing 增加一个 RelicData relic; 字段，用来区分当前卖的是什么。

        relic = relicData;

        // 假设遗物的价格计算方式 (或者 RelicData 里有 price 字段)
        currentPrice = 15; // 比如固定15块钱，或者 relicData.price

        // 2. 更新显示
        if (iconImage != null) iconImage.sprite = relicData.icon;
        if (itemDescription != null) itemDescription.text = relicData.description;

        // 3. 设置价格文字
        if (coinTextLabel != null)
        {
            coinTextLabel.text = currentPrice.ToString();
            originalColor = coinTextLabel.color;
        }

        // 4. 检查是否买得起
        if (PlayerInventory.PlayerInstance != null)
        {
            CheckAffordability(PlayerInventory.PlayerInstance.currentGold);
        }
    }
}