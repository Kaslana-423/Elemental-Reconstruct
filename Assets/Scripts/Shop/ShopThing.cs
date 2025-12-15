using System.Collections;

using System.Collections.Generic;

using TMPro;

using UnityEngine;

using UnityEngine.UI;



public class ShopThing : MonoBehaviour

{

    public MagicItem item; // 商店物品

    public TextMeshProUGUI coinTextLabel;

    public TextMeshProUGUI itemDescription;

    public Image iconImage; // 建议也直接引用 Image 组件，而不是 Sprite 变量

    public int rarity;

    public void SetBuyButton()
    {
        if (ShopManager.AdditemToBag(item))
        {
            Destroy(this.gameObject);
            InventoryManager.RefreshItem();
        }

    }

    public void SetUpShop(MagicItem magicItem)

    {


        // 如果你有 Image 组件引用
        if (iconImage != null)
        {
            iconImage.sprite = magicItem.itemImage;
        }

        rarity = magicItem.stats.rarity;

        item = magicItem;

        itemDescription.text = magicItem.itemDescription;
        if (coinTextLabel != null)
        {
            coinTextLabel.text = (magicItem.stats.rarity * 5).ToString();
        }

    }

}