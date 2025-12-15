using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemOnDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public Transform originalParent;
    public Inventory myBag;
    public MagicEditInventory myEditBag1;
    public MagicEditInventory myEditBag2;
    public MagicEditInventory myEditBag3;
    private int currentItemID;

    // 转发点击事件给父物体 (Slot)
    public void OnPointerClick(PointerEventData eventData)
    {
        GetComponentInParent<Slot>()?.OnPointerClick(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            // 如果不是左键，则阻止拖拽流程继续
            // Note: IBeginDragHandler 默认只响应 Primary Button (左键)，
            // 但如果你的 EventSystem 设置允许其他按钮，最好显式判断。
            return;
        }
        originalParent = transform.parent;
        currentItemID = originalParent.GetComponent<Slot>()?.slotID ?? -1;

        // 1. 寻找最顶层的 Canvas (或者你指定的 UI Root)
        // transform.root 通常是场景根物体，如果 Canvas 是根物体这没问题
        // 为了保险，我们通常找 Canvas 组件所在的物体
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            transform.SetParent(canvas.transform);
        }
        else
        {
            // 备用方案：一直往上找直到没有父物体
            transform.SetParent(transform.root);
        }

        // 2. 【关键】设为最后一个子物体，确保渲染在最上层
        transform.SetAsLastSibling();

        transform.position = eventData.position;
        GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        transform.position = eventData.position;
        Debug.Log(eventData.pointerCurrentRaycast.gameObject.name);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // 0. 防止拖到空区域报错
        if (eventData.pointerCurrentRaycast.gameObject == null)
        {
            ResetPosition();
            return;
        }

        GameObject targetObj = eventData.pointerCurrentRaycast.gameObject;
        string targetName = targetObj.name;

        // 获取目标槽位的 UI 脚本 (用于确定是第几个法杖: 0, 1, 2)
        var targetSlotUI = targetObj.GetComponentInParent<MagicEditSlot>();

        // 确定目标仓库 (Target Bag)
        MagicEditInventory targetBag = null;
        if (targetSlotUI != null)
        {
            if (targetSlotUI.slotID == 0) targetBag = myEditBag1;
            else if (targetSlotUI.slotID == 1) targetBag = myEditBag2;
            else if (targetSlotUI.slotID == 2) targetBag = myEditBag3;
        }

        // 获取来源信息 (Source)
        var sourceWandSlot = originalParent.GetComponentInParent<MagicEditSlot>(); // 来源是法杖?
        Slot sourceBagSlot = originalParent.GetComponent<Slot>(); // 来源是背包?

        // ==========================================================================================
        // 区域 1: 拖拽到【原件槽 (Original)】(支持 Backpack <-> Original, Trigger <-> Original)
        // ==========================================================================================
        if (targetName == "OriginalMagicImage" || targetName == "OriginalMagicSlot(Clone)") // 兼容点到槽或图片
        {
            if (targetBag == null) { ResetPosition(); return; }

            // 1.1 从背包拖来
            if (sourceBagSlot != null)
            {
                MagicItem itemInHand = myBag.itemList[currentItemID];
                // 规则：必须是 Projectile
                if (itemInHand != null && itemInHand.type != MagicType.Projectile) { ResetPosition(); return; }

                // 交换数据
                MagicItem temp = targetBag.OriginalMagicItem;
                targetBag.OriginalMagicItem = itemInHand;
                myBag.itemList[currentItemID] = temp;
            }
            // 1.2 从法杖拖来
            else if (sourceWandSlot != null)
            {
                // 获取来源仓库
                MagicEditInventory sourceBag = GetSourceBag(sourceWandSlot.slotID);

                // 规则：只能从 Original 或 Trigger 拖过来 (Modifier 不能进 Original)
                string sourceParentName = originalParent.name; // 来源槽的名字

                if (sourceParentName.Contains("Trigger"))
                {
                    // Trigger <-> Original
                    MagicItem temp = targetBag.OriginalMagicItem;
                    targetBag.OriginalMagicItem = sourceBag.TriggerMagicItem;
                    sourceBag.TriggerMagicItem = temp;
                }
                else if (sourceParentName.Contains("Original"))
                {
                    // Original <-> Original
                    MagicItem temp = targetBag.OriginalMagicItem;
                    targetBag.OriginalMagicItem = sourceBag.OriginalMagicItem;
                    sourceBag.OriginalMagicItem = temp;
                }
                else
                {
                    ResetPosition(); return; // 试图把修饰符拖进原件槽，禁止
                }
            }

            FinalizeSwap();
            return;
        }

        // ==========================================================================================
        // 区域 2: 拖拽到【修饰槽 1 (Modified 1)】(支持 Backpack <-> Mod, Mod <-> Mod)
        // ==========================================================================================
        if (targetName == "ModifyMagicSlot1" || targetName == "MdMagicImage1")
        {
            if (targetBag == null) { ResetPosition(); return; }

            // 2.1 从背包拖来
            if (sourceBagSlot != null)
            {
                MagicItem itemInHand = myBag.itemList[currentItemID];
                // 规则：必须是 Modifier
                if (itemInHand != null && itemInHand.type != MagicType.Modifier) { ResetPosition(); return; }

                MagicItem temp = targetBag.ModifiedMagicItem1;
                targetBag.ModifiedMagicItem1 = itemInHand;
                myBag.itemList[currentItemID] = temp;
            }
            // 2.2 从法杖拖来
            else if (sourceWandSlot != null)
            {
                MagicEditInventory sourceBag = GetSourceBag(sourceWandSlot.slotID);
                string sourceParentName = originalParent.name;

                // 规则：只能从其他修饰槽拖过来
                if (!sourceParentName.Contains("Modi")) { ResetPosition(); return; }

                MagicItem sourceItem = GetItemFromWandSlot(sourceBag, sourceParentName);

                // 交换
                SetItemToWandSlot(sourceBag, sourceParentName, targetBag.ModifiedMagicItem1); // 把我的给来源
                targetBag.ModifiedMagicItem1 = sourceItem; // 把来源给我
            }

            FinalizeSwap();
            return;
        }

        // ==========================================================================================
        // 区域 3: 拖拽到【修饰槽 2 (Modified 2)】(同上)
        // ==========================================================================================
        if (targetName == "ModifyMagicSlot2" || targetName == "MdMagicImage2")
        {
            if (targetBag == null) { ResetPosition(); return; }

            if (sourceBagSlot != null)
            {
                MagicItem itemInHand = myBag.itemList[currentItemID];
                if (itemInHand != null && itemInHand.type != MagicType.Modifier) { ResetPosition(); return; }

                MagicItem temp = targetBag.ModifiedMagicItem2;
                targetBag.ModifiedMagicItem2 = itemInHand;
                myBag.itemList[currentItemID] = temp;
            }
            else if (sourceWandSlot != null)
            {
                MagicEditInventory sourceBag = GetSourceBag(sourceWandSlot.slotID);
                string sourceParentName = originalParent.name;
                if (!sourceParentName.Contains("Modi")) { ResetPosition(); return; }

                MagicItem sourceItem = GetItemFromWandSlot(sourceBag, sourceParentName);
                SetItemToWandSlot(sourceBag, sourceParentName, targetBag.ModifiedMagicItem2);
                targetBag.ModifiedMagicItem2 = sourceItem;
            }

            FinalizeSwap();
            return;
        }

        // ==========================================================================================
        // 区域 4: 拖拽到【触发槽 (Trigger)】(支持 Backpack <-> Tri, Original <-> Tri)
        // ==========================================================================================
        if (targetName == "TriggerMagicSlot" || targetName == "TriMagicImage")
        {
            if (targetBag == null) { ResetPosition(); return; }

            if (sourceBagSlot != null)
            {
                MagicItem itemInHand = myBag.itemList[currentItemID];
                // 规则：必须是 Projectile
                if (itemInHand != null && itemInHand.type != MagicType.Projectile) { ResetPosition(); return; }

                MagicItem temp = targetBag.TriggerMagicItem;
                targetBag.TriggerMagicItem = itemInHand;
                myBag.itemList[currentItemID] = temp;
            }
            else if (sourceWandSlot != null)
            {
                MagicEditInventory sourceBag = GetSourceBag(sourceWandSlot.slotID);
                string sourceParentName = originalParent.name;

                // 规则：只能从 Original 或 Trigger 拖过来
                if (sourceParentName.Contains("Modified")) { ResetPosition(); return; }

                if (sourceParentName.Contains("Original"))
                {
                    // Original <-> Trigger
                    MagicItem temp = targetBag.TriggerMagicItem;
                    targetBag.TriggerMagicItem = sourceBag.OriginalMagicItem;
                    sourceBag.OriginalMagicItem = temp;
                }
                else if (sourceParentName.Contains("Trigger"))
                {
                    // Trigger <-> Trigger
                    MagicItem temp = targetBag.TriggerMagicItem;
                    targetBag.TriggerMagicItem = sourceBag.TriggerMagicItem;
                    sourceBag.TriggerMagicItem = temp;
                }
            }

            FinalizeSwap();
            return;
        }

        // ==========================================================================================
        // 区域 5: 拖拽回【背包】(MagicSlot)
        // ==========================================================================================
        // 修改：直接检测 Slot 组件，不再依赖名字判断，解决拖到图片上无法识别的问题
        Slot targetBackpackSlot = targetObj.GetComponent<Slot>();
        if (targetBackpackSlot == null) targetBackpackSlot = targetObj.GetComponentInParent<Slot>();

        if (targetBackpackSlot != null)
        {
            // 5.1 来源是背包 (背包内部整理)
            if (sourceBagSlot != null)
            {
                // 只有ID不同才交换
                if (targetBackpackSlot.slotID != currentItemID)
                {
                    var temp = myBag.itemList[currentItemID];
                    myBag.itemList[currentItemID] = myBag.itemList[targetBackpackSlot.slotID];
                    myBag.itemList[targetBackpackSlot.slotID] = temp;
                }
            }
            // 5.2 来源是法杖 (卸下装备)
            else if (sourceWandSlot != null)
            {
                MagicEditInventory sourceBag = GetSourceBag(sourceWandSlot.slotID);
                string sourceName = originalParent.name;

                // 获取法杖上的物品
                MagicItem wandItem = GetItemFromWandSlot(sourceBag, sourceName);
                MagicItem bagItem = myBag.itemList[targetBackpackSlot.slotID];

                // 规则检查：如果要交换（背包里有东西），得检查背包里的东西能不能放回法杖那个槽
                if (bagItem != null)
                {
                    if (sourceName.Contains("Modified") && bagItem.type != MagicType.Modifier) { ResetPosition(); return; }
                    if ((sourceName.Contains("Original") || sourceName.Contains("Trigger")) && bagItem.type != MagicType.Projectile) { ResetPosition(); return; }
                }

                // 执行交换
                myBag.itemList[targetBackpackSlot.slotID] = wandItem;
                SetItemToWandSlot(sourceBag, sourceName, bagItem);
            }

            FinalizeSwap();
            return;
        }

        // 默认归位
        ResetPosition();
    }

    // --- 辅助函数：保持代码整洁 ---

    void ResetPosition()
    {
        transform.SetParent(originalParent);
        transform.position = originalParent.position;
        GetComponent<CanvasGroup>().blocksRaycasts = true;
    }

    void FinalizeSwap()
    {
        // 既然数据变了，还原位置并强制刷新 UI
        transform.SetParent(originalParent);
        transform.position = originalParent.position;
        GetComponent<CanvasGroup>().blocksRaycasts = true;

        // 这一步非常重要：让 UI 根据新数据重新画一遍
        InventoryManager.EditMagicRefresh();
        InventoryManager.RefreshItem();
    }

    // 根据 slotID 获取对应的 ScriptableObject
    MagicEditInventory GetSourceBag(int id)
    {
        if (id == 0) return myEditBag1;
        if (id == 1) return myEditBag2;
        return myEditBag3;
    }

    // 根据 UI 名字获取仓库里的具体 Item
    MagicItem GetItemFromWandSlot(MagicEditInventory bag, string slotName)
    {
        if (slotName.Contains("Original")) return bag.OriginalMagicItem;
        if (slotName.Contains("Modi") && (slotName.Contains("1") || slotName.EndsWith("1"))) return bag.ModifiedMagicItem1;
        if (slotName.Contains("Modi") && (slotName.Contains("2") || slotName.EndsWith("2"))) return bag.ModifiedMagicItem2;
        if (slotName.Contains("Trigger")) return bag.TriggerMagicItem;
        return null;
    }

    // 根据 UI 名字写入 Item 到仓库
    void SetItemToWandSlot(MagicEditInventory bag, string slotName, MagicItem item)
    {
        if (slotName.Contains("Original")) bag.OriginalMagicItem = item;
        else if (slotName.Contains("Modi") && (slotName.Contains("1") || slotName.EndsWith("1"))) bag.ModifiedMagicItem1 = item;
        else if (slotName.Contains("Modi") && (slotName.Contains("2") || slotName.EndsWith("2"))) bag.ModifiedMagicItem2 = item;
        else if (slotName.Contains("Trigger")) bag.TriggerMagicItem = item;
    }
}
