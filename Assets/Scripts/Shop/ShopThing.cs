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

        // 2. 尝试添加到背包
        // 如果添加成功，再扣钱
        if (InventoryManager.instance.magicInventory.AddItem(item))
        {
            PlayerInventory.PlayerInstance.TrySpendGold(currentPrice);
            Destroy(this.gameObject);
        }
        else
        {
            Debug.Log("背包已满！");
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
}