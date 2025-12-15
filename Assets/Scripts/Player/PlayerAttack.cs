using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("设置")]
    public Transform firePoint; // 发射点 (法杖尖端的一个空物体)

    // 冷却时间 (防止一秒钟点1000次)
    public float globalCooldown = 0.2f;
    private float nextFireTime = 0f;

    void Update()
    {
        // 按下左键 && 冷却时间到了
        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            FireAllWands();
            nextFireTime = Time.time + globalCooldown;
        }
    }

    // --- 核心逻辑：齐射所有槽位 ---
    void FireAllWands()
    {
        // 1. 获取玩家身上的 3 个法杖槽数据
        // 注意：这里要根据你 PlayerInventory 的实际变量名来写，假设是 wands 数组
        // 如果你的变量名是 wandSlots，请自行修改
        var allSlots = PlayerInventory.PlayerInstance.wands;

        // 2. 遍历每一个槽位 (槽0, 槽1, 槽2)
        for (int i = 0; i < allSlots.Length; i++)
        {
            var slotData = allSlots[i];

            // 安全检查：只有当这个槽里装了“原件”且有预制体时，才发射
            if (slotData != null &&
                slotData.originalMagic != null &&
                slotData.originalMagic.itemPrefab != null)
            {
                // 发射这个槽位的法术
                CalculateAndSpawn(slotData);
            }
        }
    }

    // --- 处理单个槽位的计算与生成 ---
    void CalculateAndSpawn(WandStorage data) // 假设你的类名叫 WandStorage 或 WandSlotData
    {
        // A. 提取数据
        MagicItem baseItem = data.originalMagic;
        MagicItem mod1 = data.modifiedMagic1;
        MagicItem mod2 = data.modifiedMagic2;
        MagicItem trigger = data.triggerMagic;

        // B. 【关键】计算最终属性
        // 利用我们在 SpellStats 里写的 operator + 自动叠加属性
        SpellStats finalStats = baseItem.stats;

        if (mod1 != null) finalStats = finalStats + mod1.stats;
        if (mod2 != null) finalStats = finalStats + mod2.stats;

        // C. 处理多重施法 (Count)
        // 确保至少发射 1 个
        int projectileCount = Mathf.Max(1, finalStats.count);

        // 计算散射角度步长
        float spreadAngle = finalStats.spread;
        float angleStep = projectileCount > 1 ? spreadAngle / (projectileCount - 1) : 0;
        float startAngle = -spreadAngle / 2f;

        // D. 循环生成实体
        for (int j = 0; j < projectileCount; j++)
        {
            // 计算当前这颗子弹的角度
            float currentAngle = startAngle + (angleStep * j);
            Quaternion rotation = firePoint.rotation * Quaternion.Euler(0, 0, currentAngle);

            // 1. 生成子弹 (使用原件的 Prefab)
            GameObject spellObj = Instantiate(baseItem.itemPrefab, firePoint.position, rotation);

            // 2. 获取子弹脚本
            Projectile pScript = spellObj.GetComponent<Projectile>();

            // 3. 【注入灵魂】调用 Initialize
            if (pScript != null)
            {
                // 把算好的 finalStats 和 触发法术 塞给子弹
                pScript.Initialize(finalStats, trigger);
            }
        }
    }
}