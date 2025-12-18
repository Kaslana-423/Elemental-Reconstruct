using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("设置")]
    public Transform firePoint; // 发射点 (现在它不需要旋转了，固定在角色前方即可)
    [Header("法杖设置")]
    public float fireInterval;

    private float nextFireTime = 0f;
    private int currentSlotIndex = 0;
    private Coroutine firingCoroutine;

    // 缓存摄像机引用，提升性能
    private Camera mainCamera;

    // 需要增加两个组件引用
    private PlayerController playerController;
    private Character playerCharacter;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerCharacter = GetComponent<Character>();

        if (playerController == null || playerCharacter == null)
        {
            Debug.LogError("PlayerAttack: 缺少必要的组件 (PlayerController 或 Character)！");
        }

        mainCamera = Camera.main;
    }

    void Update()
    {
        // ... (这部分按键检测逻辑保持不变) ...
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (firingCoroutine == null)
            {
                firingCoroutine = StartCoroutine(FireManagementRoutine());
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (firingCoroutine != null)
            {
                StopCoroutine(firingCoroutine);
                firingCoroutine = null;
            }
        }
    }

    IEnumerator FireManagementRoutine()
    {
        // 假设 PlayerInventory 是单例或能全局访问
        var allSlots = PlayerInventory.PlayerInstance.wands;

        // 获取法杖基础间隔 (如果没找到组件就用默认值 0.5s)
        float baseInterval = fireInterval;

        while (true)
        {
            if (Time.time >= nextFireTime)
            {
                var slotData = allSlots[currentSlotIndex];
                // ============ 【核心修改区域 开始】 ============

                float manaCost = slotData.GetFinalManaCost();
                bool castSuccess = false;

                // 【关键修改】直接操作 Character 组件进行扣蓝
                if (playerCharacter != null && playerCharacter.ConsumeMP(manaCost))
                {
                    // --- A. 扣款成功 ---

                    // 【关键新增】通知 PlayerController 刷新战斗状态
                    if (playerController != null)
                    {
                        playerController.NotifyAttackPerformed();
                    }

                    // 执行发射
                    if (slotData != null &&
                    slotData.originalMagic != null &&
                    slotData.originalMagic.itemPrefab != null)
                    {
                        CalculateAndSpawn(slotData);
                        castSuccess = true;
                    }
                }
                else
                {
                    // --- B. 余额不足 ---
                    // 缺蓝处理...
                    castSuccess = false;
                }
                // =========== 【核心修改区域】 ===========

                // 1. 调用新方法计算当前槽位的实际延迟
                // 传入法杖的基础间隔
                float currentSlotDelay = slotData.GetFinalFireDelay(baseInterval);

                // 2. (可选) 调试日志，验证计算结果
                // Debug.Log($"槽位[{currentSlotIndex}] 发射。基础:{baseInterval:F2}, 最终延迟:{currentSlotDelay:F2}");

                // 3. 设置下一次允许开火的时间
                if (castSuccess) nextFireTime = Time.time + currentSlotDelay;

                // ========================================

                currentSlotIndex++;
                if (currentSlotIndex >= allSlots.Length) currentSlotIndex = 0;
            }
            yield return null;
        }
    }



    void CalculateAndSpawn(WandStorage data)
    {
        // A. 提取数据 (不变)
        MagicItem baseItem = data.originalMagic;
        MagicItem mod1 = data.modifiedMagic1;
        MagicItem mod2 = data.modifiedMagic2;
        MagicItem trigger = data.triggerMagic;

        // B. 计算最终属性 (不变)
        SpellStats finalStats = baseItem.stats;
        if (mod1 != null) finalStats = finalStats + mod1.stats;
        if (mod2 != null) finalStats = finalStats + mod2.stats;

        // C. 处理多重施法和散射角度 (不变)
        int projectileCount = Mathf.Max(1, finalStats.count);
        float spreadAngle = finalStats.spread;
        float halfSpread = spreadAngle / 2f;
        float orbitStepAngle = 0f;
        // 如果是环绕模式，计算每个子弹之间的角度间隔
        if (finalStats.isOrbiting && projectileCount > 1)
        {
            orbitStepAngle = 360f / projectileCount;
        }

        // Let's Aim! 
        // --- 新增：计算基准瞄准方向 ---

        // 1. 获取鼠标屏幕坐标
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        // 2. 转世界坐标。确保 Z 轴距离足够让摄像机看到
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(mainCamera.transform.position.z)));
        // 重要：把鼠标的 Z 轴拉平和发射点一致，确保是 2D 平面计算
        mouseWorldPos.z = firePoint.position.z;

        // 3. 计算向量方向 (目标点 - 起始点)
        Vector3 aimDirection = (mouseWorldPos - firePoint.position).normalized;

        // 4. 计算基准角度 (Atan2 返回弧度，转为角度)
        float baseAimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        // 5. 生成基准旋转四元数
        Quaternion baseAimRotation = Quaternion.Euler(0, 0, baseAimAngle);

        // ---------------------------

        // D. 循环生成实体
        for (int j = 0; j < projectileCount; j++)
        {
            // 【修改点 3：计算随机偏移量】

            // 以前是：固定的步长
            // float currentSpreadOffset = startAngle + (angleStep * j);

            // 现在是：在 [-half, +half] 之间随机取一个值
            // 使用 UnityEngine.Random.Range(min, max)
            float randomSpreadOffset = UnityEngine.Random.Range(-halfSpread, halfSpread);

            // 基于计算出来的鼠标方向进行随机偏移
            // 注意这里用的是 randomSpreadOffset
            Quaternion finalRotation = baseAimRotation * Quaternion.Euler(0, 0, randomSpreadOffset);

            // 生成子弹
            GameObject spellObj = Instantiate(baseItem.itemPrefab, firePoint.position, finalRotation);
            Projectile pScript = spellObj.GetComponent<Projectile>();

            // 初始化子弹
            if (pScript != null)
            {
                // --- 计算当前子弹的环绕偏移角 ---
                float currentOrbitOffset = 0f;
                if (finalStats.isOrbiting)
                {
                    // 第0个偏移0度，第1个偏移 step 度，第2个偏移 2*step 度...
                    currentOrbitOffset = j * orbitStepAngle;

                    // (可选高级功能) 如果你希望每次发射的环绕物初始位置随机一点，
                    // 可以加上一个随机的起始角：
                    // currentOrbitOffset += Random.Range(0f, 360f);
                }

                // 【关键修改】调用新的 Initialize，传入计算好的偏移角
                pScript.Initialize(finalStats, trigger, currentOrbitOffset);
            }
        }
    }
}