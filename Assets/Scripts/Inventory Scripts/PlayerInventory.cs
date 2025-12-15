using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory PlayerInstance;

    // 这就是“法杖槽内的数据”
    // 数组长度固定为 3，代表法杖上的三个大槽位
    public WandStorage[] wands = new WandStorage[3];

    void Awake()
    {
        if (PlayerInstance == null) PlayerInstance = this;
        // 初始化数据，防止空指针
        for (int i = 0; i < wands.Length; i++)
        {
            if (wands[i] == null) wands[i] = new WandStorage();
        }
    }

    // UI 拖拽保存数据时调用此方法
    public static void UpdateWandStorage(int index, MagicItem org, MagicItem mod1, MagicItem mod2, MagicItem trig)
    {
        if (index >= 0 && index < PlayerInstance.wands.Length)
        {
            PlayerInstance.wands[index].originalMagic = org;
            PlayerInstance.wands[index].modifiedMagic1 = mod1;
            PlayerInstance.wands[index].modifiedMagic2 = mod2;
            PlayerInstance.wands[index].triggerMagic = trig;
        }
    }
}