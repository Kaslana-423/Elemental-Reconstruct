using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem; // 【1. 必须引入这个命名空间】

public class ItemInfoPanel : MonoBehaviour
{
    public static ItemInfoPanel Instance;
    public Inventory myBag;
    [Header("UI 组件引用")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public Image iconImage;
    public GameObject panelObj; // 自身的面板物体
    public MagicItem item;
    public int MagicId;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
        // 初始化完成后，立即隐藏面板，确保单例已赋值
        HideTooltip();
    }

    public void ShowTooltip(MagicItem item, Vector2 position, int magicId)
    {
        // 1. 更新数据
        titleText.text = item.itemName;
        descText.text = item.itemDescription;
        this.item = item;
        if (iconImage != null) iconImage.sprite = item.itemImage;
        MagicId = magicId;
        // 2. 移动位置 (稍微偏移一点，别挡住鼠标)
        transform.position = position;
        // 3. 显示
        panelObj.SetActive(true);
    }
    public void DestroyThisMagic()
    {
        myBag.RemoveItem(item, MagicId);
        HideTooltip();
    }


    // --- 隐藏面板 ---
    public void HideTooltip()
    {
        panelObj.SetActive(false);
    }
}