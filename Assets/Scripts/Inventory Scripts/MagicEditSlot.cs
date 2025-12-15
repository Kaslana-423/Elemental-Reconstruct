using UnityEngine;
using UnityEngine.UI;

public class MagicEditSlot : MonoBehaviour
{
    public int slotID;

    [Header("UI 组件")]
    public Image slotImage;       // 主槽图标
    public Image modifiedImage1;  // 修饰1图标
    public Image modifiedImage2;  // 修饰2图标
    public Image triggerImage;    // 触发图标

    [Header("当前槽位存储的数据 (只存引用)")]
    public MagicItem originalMagic;
    public MagicItem modifiedMagic1;
    public MagicItem modifiedMagic2;
    public MagicItem triggerMagic;

    // 点击事件 (可选)
    public void ItemOnClick()
    {
        Debug.Log($"Slot {slotID} Clicked. Main Spell: {(originalMagic != null ? originalMagic.itemName : "None")}");
    }

    // 更新槽位显示和数据 (核心方法)
    public void SetUpSlot(MagicItem org, MagicItem mod1, MagicItem mod2, MagicItem trig)
    {
        // 1. 存储数据引用 (供玩家读取)
        this.originalMagic = org;
        this.modifiedMagic1 = mod1;
        this.modifiedMagic2 = mod2;
        this.triggerMagic = trig;

        // 2. 更新主槽显示
        UpdateImageDisplay(slotImage, org);

        // 3. 更新修饰槽1显示
        UpdateImageDisplay(modifiedImage1, mod1);

        // 4. 更新修饰槽2显示
        UpdateImageDisplay(modifiedImage2, mod2);

        // 5. 更新触发槽显示
        UpdateImageDisplay(triggerImage, trig);
    }

    // 辅助方法：处理图片的显示与隐藏
    private void UpdateImageDisplay(Image img, MagicItem item)
    {
        if (item != null)
        {
            img.sprite = item.itemImage;
            img.enabled = true;  // 有物品就显示图片
        }
        else
        {
            img.sprite = null;
            img.enabled = false; // 没物品就隐藏图片，或者显示一个默认底图
        }
    }
}