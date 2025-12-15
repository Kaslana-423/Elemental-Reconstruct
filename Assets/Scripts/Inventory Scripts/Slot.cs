using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class Slot : MonoBehaviour, IPointerClickHandler
{
    public int slotID;
    public Image slotImage;
    public MagicItem magicItem;
    public string slotInfo;
    public GameObject itemInSlot;
    public void ItemOnClick()
    {
        Debug.Log("You clicked on slot " + slotID);
        Debug.Log("Item Info: " + slotInfo);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (magicItem == null) return; // 如果是空的，不反应

        // 检测右键点击
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log("右键点击了: " + magicItem.itemName);

            // 呼叫单例的 Tooltip 显示出来
            if (ItemInfoPanel.Instance != null)
            {
                ItemInfoPanel.Instance.ShowTooltip(magicItem, eventData.position);
            }
            else
            {
                Debug.LogError("ItemInfoPanel Instance is null! Make sure it is in the scene.");
            }
        }
        // 检测左键点击 (原本的逻辑)
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            ItemOnClick();
            // 左键点击时，顺便隐藏详情面板？看你需求
            ItemInfoPanel.Instance.HideTooltip();
        }
    }

    public void SetUpSlot(MagicItem item)
    {
        if (item == null)
        {
            itemInSlot.SetActive(false);
            return;
        }
        magicItem = item;
        itemInSlot = item.itemPrefab;
        slotImage.sprite = item.itemImage;
        slotInfo = item.itemDescription;
    }
}
