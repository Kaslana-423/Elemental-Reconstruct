using UnityEngine;
using System;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory PlayerInstance;

    [Header("经济系统")]
    [SerializeField] private int initialGold = 100; // 每局开始的初始金币
    public int currentGold { get; private set; }    // 只读属性
    public event Action<int> OnGoldChanged;

    [Header("背包引用")]
    public Inventory myBag; // 引用你的背包 SO

    [Header("法杖槽位数据")]
    public WandStorage[] wands = new WandStorage[3];

    void Awake()
    {
        // --- 单例模式检查 ---
        if (PlayerInstance != null && PlayerInstance != this)
        {
            Destroy(this);
            return;
        }

        PlayerInstance = this;


        DontDestroyOnLoad(this.gameObject);

        // 初始化法杖数据，防止空指针
        for (int i = 0; i < wands.Length; i++)
        {
            if (wands[i] == null) wands[i] = new WandStorage();
        }
    }

    // --- 核心：被 SaveManager 调用的方法 ---
    // 当 SaveManager 读取到 JSON 里的金币数后，会调用这个方法来同步数据
    public void InitializeGoldFromSave(int gold)
    {
        currentGold = gold;
        // 只通知 UI 更新，不进行逻辑处理
        OnGoldChanged?.Invoke(currentGold);
    }

    // --- 重置金币 (新游戏时调用) ---
    public void ResetGold()
    {
        currentGold = initialGold;

        // 通知 UI 更新
        OnGoldChanged?.Invoke(currentGold);
        Debug.Log("金币已重置为初始值");
    }

    // --- 增加金币 ---
    public void AddGold(int amount)
    {
        if (amount < 0) return;

        currentGold += amount;

        Debug.Log($"获得金币: {amount}, 当前总数: {currentGold}");
        OnGoldChanged?.Invoke(currentGold);
        SaveManager.Instance.SaveGame();
    }

    // --- 消耗金币 ---
    public bool TrySpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            Debug.Log($"花费金币: {amount}, 剩余: {currentGold}");
            OnGoldChanged?.Invoke(currentGold);
            SaveManager.Instance.SaveGame();

            return true; // 购买成功
        }
        else
        {
            Debug.Log("金币不足！");
            return false; // 购买失败
        }
    }

    // --- 更新法杖数据 (UI拖拽时调用) ---
    public static void UpdateWandStorage(int index, MagicItem org, MagicItem mod1, MagicItem mod2, MagicItem trig)
    {
        if (PlayerInstance == null) return;

        if (index >= 0 && index < PlayerInstance.wands.Length)
        {
            PlayerInstance.wands[index].originalMagic = org;
            PlayerInstance.wands[index].modifiedMagic1 = mod1;
            PlayerInstance.wands[index].modifiedMagic2 = mod2;
            PlayerInstance.wands[index].triggerMagic = trig;
        }
    }
}