using UnityEngine;
using System;
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory PlayerInstance;
    [Header("经济系统")]
    [SerializeField] private int initialGold = 100; // 每局开始的初始金币
    public int currentGold { get; private set; }  // 只读属性，外部只能看，不能直接改
    public event Action<int> OnGoldChanged;
    // 这就是“法杖槽内的数据”
    // 数组长度固定为 3，代表法杖上的三个大槽位
    public WandStorage[] wands = new WandStorage[3];
    private const string GOLD_SAVE_KEY = "Player_Total_Gold";
    void Awake()
    {
        // --- 单例模式检查 ---
        if (PlayerInstance != null && PlayerInstance != this)
        {
            Debug.LogWarning("发现重复的 PlayerInventory，正在销毁多余的实例: " + gameObject.name);
            Destroy(this); // 改为只销毁脚本组件，防止误删 Canvas 或 Player
            return;
        }

        PlayerInstance = this;
        // 如果你希望切换场景时保留这个 Inventory，取消下面这行的注释
        // DontDestroyOnLoad(this.gameObject);

        // 初始化数据，防止空指针
        for (int i = 0; i < wands.Length; i++)
        {
            if (wands[i] == null) wands[i] = new WandStorage();
        }
        LoadGold();
        //test 后续应该删除
    }
    public void ResetGold()
    {
        currentGold = initialGold;
        SaveGold();
        // 通知 UI 更新
        OnGoldChanged?.Invoke(currentGold);
        Debug.Log("金币已重置");
    }
    private void LoadGold()
    {
        // PlayerPrefs.GetInt(key, defaultValue)
        // 意思是：尝试找 "Player_Total_Gold"，如果找不到（第一次玩），就返回 initialGold (100)
        currentGold = PlayerPrefs.GetInt(GOLD_SAVE_KEY, initialGold);
        OnGoldChanged?.Invoke(currentGold);
        // 告诉 UI 刷新显示
        // 注意：这里可能 UI 还没初始化完，所以 GoldUI 的 Start 里最好也读一遍
    }
    private void SaveGold()
    {
        // 把 currentGold 写入硬盘
        PlayerPrefs.SetInt(GOLD_SAVE_KEY, currentGold);
        PlayerPrefs.Save(); // 强制立即写入（防闪退）
    }
    public void AddGold(int amount)
    {
        if (amount < 0) return; // 防止加负数

        currentGold += amount;

        Debug.Log($"获得金币: {amount}, 当前总数: {currentGold}");
        SaveGold();
        OnGoldChanged?.Invoke(currentGold);
    }
    public bool TrySpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            Debug.Log($"花费金币: {amount}, 剩余: {currentGold}");
            SaveGold();
            OnGoldChanged?.Invoke(currentGold);
            return true; // 购买成功
        }
        else
        {
            Debug.Log("金币不足！");
            // 这里可以加一个 UI 提示 "金币不足" 的事件
            return false; // 购买失败
        }
    }
    // UI 拖拽保存数据时调用此方法
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