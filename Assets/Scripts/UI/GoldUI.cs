using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class GoldUI : MonoBehaviour
{
    public TextMeshProUGUI goldText; // 拖入你的 TMP 组件

    void Start()
    {
        // Start 运行时，所有脚本的 Awake 都已执行完毕，PlayerInstance 肯定不为空了
        if (PlayerInventory.PlayerInstance != null)
        {
            // 为了防止 OnEnable 没订阅上（因为那时候 PlayerInstance 可能还是 null），
            // 我们在这里重新订阅一次。
            // 先减后加，确保不会重复订阅
            PlayerInventory.PlayerInstance.OnGoldChanged -= UpdateGoldDisplay;
            PlayerInventory.PlayerInstance.OnGoldChanged += UpdateGoldDisplay;

            // 初始化显示
            UpdateGoldDisplay(PlayerInventory.PlayerInstance.currentGold);
        }
    }

    public void OnEnable()
    {
        // 尝试订阅（如果 PlayerInventory 还没醒来，这里会失败，但在 Start 里会补救）
        if (PlayerInventory.PlayerInstance != null)
        {
            PlayerInventory.PlayerInstance.OnGoldChanged += UpdateGoldDisplay;
            UpdateGoldDisplay(PlayerInventory.PlayerInstance.currentGold);
        }
    }

    public void OnDisable()
    {
        if (PlayerInventory.PlayerInstance != null)
        {
            PlayerInventory.PlayerInstance.OnGoldChanged -= UpdateGoldDisplay;
        }
    }
    void UpdateGoldDisplay(int newAmount)
    {
        goldText.text = newAmount.ToString();
    }
}