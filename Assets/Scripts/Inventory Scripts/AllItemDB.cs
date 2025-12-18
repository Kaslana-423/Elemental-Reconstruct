using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor; // 引用编辑器命名空间，用于自动填充列表
#endif

[CreateAssetMenu(fileName = "AllItemDB", menuName = "Inventory/AllItemDB")]
public class AllItemDB : ScriptableObject
{
    [Header("所有物品列表 (不用手动拖，用下面的右键菜单自动填)")]
    public List<MagicItem> allMagicItems = new List<MagicItem>();

    // --- 核心优化：字典 ---
    // 这个字典只在游戏运行时存在，用来加速查找
    private Dictionary<string, MagicItem> lookupTable;

    /// <summary>
    /// 初始化方法：把 List 转成 Dictionary
    /// </summary>
    public void Init()
    {
        // 如果字典已经有了，就不重复初始化了
        if (lookupTable != null && lookupTable.Count > 0) return;

        lookupTable = new Dictionary<string, MagicItem>();

        foreach (var item in allMagicItems)
        {
            // 1. 安全检查：防止列表里有空的
            if (item == null) continue;

            // 2. 安全检查：防止 ID 为空
            if (string.IsNullOrEmpty(item.itemID))
            {
                Debug.LogError($"[AllItemDB] 发现物品 {item.name} 没有设置 itemID！请检查！");
                continue;
            }

            // 3. 安全检查：防止 ID 重复 (这很重要！)
            if (lookupTable.ContainsKey(item.itemID))
            {
                Debug.LogError($"[AllItemDB] 重复的 ID: {item.itemID}。请检查 {item.name} 和 {lookupTable[item.itemID].name}");
            }
            else
            {
                // 加入字典
                lookupTable.Add(item.itemID, item);
            }
        }

        Debug.Log($"[AllItemDB] 数据库初始化完成，共索引 {lookupTable.Count} 个物品。");
    }

    /// <summary>
    /// 通过 ID 获取物品 (优化版)
    /// </summary>
    public MagicItem GetItemByID(string id)
    {
        // 自动初始化：如果还没初始化过，先初始化
        // 这样你就不用担心忘记在哪里调用 Init 了
        if (lookupTable == null)
        {
            Init();
        }

        if (string.IsNullOrEmpty(id)) return null;

        // 使用 TryGetValue 高效查找 (复杂度 O(1))
        if (lookupTable.TryGetValue(id, out MagicItem item))
        {
            return item;
        }

        Debug.LogWarning($"[AllItemDB] 找不到 ID 为 '{id}' 的物品！");
        return null;
    }

    // =========================================================
    //               【提升效率】编辑器自动加载功能
    // =========================================================
#if UNITY_EDITOR
    // 使用方法：在 Inspector 右键点击脚本标题 -> Load All Magic Items
    [ContextMenu("Load All Magic Items (自动加载所有法术)")]
    public void LoadAllItems()
    {
        allMagicItems.Clear();

        // 查找项目中所有类型为 MagicItem 的资源
        string[] guids = AssetDatabase.FindAssets("t:MagicItem");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MagicItem item = AssetDatabase.LoadAssetAtPath<MagicItem>(path);
            if (item != null)
            {
                allMagicItems.Add(item);
            }
        }

        Debug.Log($"[编辑器] 自动加载了 {allMagicItems.Count} 个物品。记得保存！");
        EditorUtility.SetDirty(this); // 标记脏数据，提示 Unity 保存
    }
#endif
}