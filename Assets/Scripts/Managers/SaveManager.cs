using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO; // 必须引用：用于文件读写

public class SaveManager : MonoBehaviour
{
    // 单例模式：方便在任何地方调用 SaveManager.Instance.SaveGame()
    public static SaveManager Instance;

    [Header("必须引用的组件")]
    public AllItemDB itemDB;    // 你的物品数据库
    public Inventory myBackpack;   // 你的背包 SO
    [Header("法术编辑槽 SO引用 (直接覆盖它们)")]
    public MagicEditInventory editBag1; // 对应第1根法杖
    public MagicEditInventory editBag2; // 对应第2根法杖
    public MagicEditInventory editBag3; // 对应第3根法杖
    // 存档文件路径
    private string savePath;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 设置存档路径 (跨平台兼容)
        savePath = Path.Combine(Application.persistentDataPath, "game_save.json");
    }

    private void Start()
    {
        // 游戏启动时自动读档
        LoadGame();
    }

    // ==========================================
    // 1. 保存逻辑 (Game Data -> JSON -> File)
    // ==========================================
    public void SaveGame()
    {
        GameSaveData data = new GameSaveData();

        // --- A. 保存金币 ---
        if (PlayerInventory.PlayerInstance != null)
        {
            data.gold = PlayerInventory.PlayerInstance.currentGold;
        }
        if (GameManager.Instance != null)
        {
            data.gameProcess = GameManager.Instance.gameProcess;
        }
        // --- B. 保存背包 (按位置记录) ---
        foreach (var item in myBackpack.itemList)
        {
            if (item != null)
                data.backpackIDs.Add(item.itemID); // 存 ID
            else
                data.backpackIDs.Add("");          // 存空位占位符
        }

        // --- C. 保存法杖 ---
        if (PlayerInventory.PlayerInstance != null)
        {
            foreach (var wand in PlayerInventory.PlayerInstance.wands)
            {
                WandSaveData wandData = new WandSaveData();
                wandData.originalID = wand.originalMagic != null ? wand.originalMagic.itemID : "";
                wandData.mod1ID = wand.modifiedMagic1 != null ? wand.modifiedMagic1.itemID : "";
                wandData.mod2ID = wand.modifiedMagic2 != null ? wand.modifiedMagic2.itemID : "";
                wandData.triggerID = wand.triggerMagic != null ? wand.triggerMagic.itemID : "";

                data.wands.Add(wandData);
            }
        }

        // 写入硬盘
        string json = JsonUtility.ToJson(data, true);
        Debug.Log("保存的 JSON 数据:\n" + json);
        File.WriteAllText(savePath, json);
        Debug.Log($"存档成功！路径: {savePath}");
    }

    // ==========================================
    // 2. 加载逻辑 (File -> JSON -> ID -> Game Data)
    // ==========================================
    public void LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("没有找到存档文件，开始新游戏。");
            return;
        }
        if (itemDB != null) itemDB.Init();
        // 读取 JSON
        string json = File.ReadAllText(savePath);
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

        // --- A. 恢复金币 ---
        // 注意：不要调用 AddGold，那样会触发保存循环。直接赋值或者写一个 SetGold 方法。
        // 这里假设你在 PlayerInventory 加了一个 InitializeGold 方法
        if (PlayerInventory.PlayerInstance != null)
        {
            PlayerInventory.PlayerInstance.InitializeGoldFromSave(data.gold);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameProcess = data.gameProcess;
        }

        var bagList = myBackpack.itemList;
        // 清空旧数据或者覆盖
        for (int i = 0; i < bagList.Count; i++)
        {
            if (i < data.backpackIDs.Count)
            {
                string id = data.backpackIDs[i];
                if (!string.IsNullOrEmpty(id))
                    bagList[i] = itemDB.GetItemByID(id);
                else
                    bagList[i] = null;
            }
        }
        MagicEditInventory[] editBags = { editBag1, editBag2, editBag3 };
        // --- C. 恢复法杖 ---
        for (int i = 0; i < editBags.Length; i++)
        {
            // 如果存档里有这根法杖的数据
            if (i < data.wands.Count)
            {
                WandSaveData wData = data.wands[i];
                MagicEditInventory targetSO = editBags[i];

                if (targetSO != null)
                {
                    // A. 直接把数据填入 SO
                    targetSO.OriginalMagicItem = itemDB.GetItemByID(wData.originalID);
                    targetSO.ModifiedMagicItem1 = itemDB.GetItemByID(wData.mod1ID);
                    targetSO.ModifiedMagicItem2 = itemDB.GetItemByID(wData.mod2ID);
                    targetSO.TriggerMagicItem = itemDB.GetItemByID(wData.triggerID);

                    // B. 【重要同步】同时也更新 PlayerInventory 内存里的数据
                    // 这样当你保存游戏时，PlayerInventory 里的数据也是新的
                    if (PlayerInventory.PlayerInstance != null)
                    {
                        PlayerInventory.UpdateWandStorage(i,
                            targetSO.OriginalMagicItem,
                            targetSO.ModifiedMagicItem1,
                            targetSO.ModifiedMagicItem2,
                            targetSO.TriggerMagicItem);
                    }
                }
            }
        }

        // --- D. 刷新所有 UI ---
        InventoryManager.RefreshItem(); // 刷新背包 UI
        InventoryManager.EditMagicRefresh(); // 刷新法杖 UI
        // 如果有金币UI刷新事件，记得在这里触发一下
    }

    // ==========================================
    // 3. 自动保存 (可选)
    // ==========================================
    private void OnApplicationQuit()
    {
        SaveGame(); // 退出游戏时自动保存
    }
    [System.Serializable]
    public class GameSaveData
    {
        public int gold;
        public int gameProcess;
        public List<string> backpackIDs = new List<string>();
        public List<WandSaveData> wands = new List<WandSaveData>();
    }

    [System.Serializable]
    public class WandSaveData
    {
        public string originalID;
        public string mod1ID;
        public string mod2ID;
        public string triggerID;
    }
}