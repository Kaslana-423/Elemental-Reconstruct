using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("设置")]
    // 务必在 Inspector 拖入 3 个发射点 (Left, Center, Right)
    public Transform[] firePoints;
    public float fireInterval;     // 默认基础CD

    [Header("索敌设置")]
    public float autoAimRange = 15f;
    public LayerMask enemyLayer;

    // 并行射击计时器数组
    private float[] nextFireTimes;

    private PlayerController playerController;
    private Camera mainCamera;

    // 缓存搜索用的数组 (优化GC)
    private Collider2D[] enemyCache = new Collider2D[20];

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        mainCamera = Camera.main;

        // 【核心修改 1】游戏开始直接启动射击循环，不再等待按键
        StartCoroutine(FireManagementRoutine());
    }

    // Update里已经不需要写按键检测了，全交给协程处理
    void Update() { }

    IEnumerator FireManagementRoutine()
    {
        var allSlots = PlayerInventory.PlayerInstance.wands;

        // 初始化计时器
        if (nextFireTimes == null || nextFireTimes.Length != allSlots.Length)
        {
            nextFireTimes = new float[allSlots.Length];
        }

        while (true)
        {
            // 【核心修改 2】每一帧实时检测鼠标状态
            // isPressed 会在按住期间一直返回 true
            bool isManualAim = Mouse.current.leftButton.isPressed;

            // 遍历 3 个法术槽位 (并行逻辑)
            for (int i = 0; i < allSlots.Length; i++)
            {
                // 安全检查：发射点取模防止越界
                Transform currentOrigin = firePoints[i % firePoints.Length];
                WandStorage slotData = allSlots[i];

                // 检查 CD 是否转好
                if (Time.time >= nextFireTimes[i])
                {
                    // 只有槽位里有法术才处理
                    if (slotData != null &&
                        slotData.originalMagic != null &&
                        slotData.originalMagic.itemPrefab != null)
                    {
                        bool shouldFire = false;
                        Transform target = null;

                        // --- 分支逻辑 ---
                        if (isManualAim)
                        {
                            // A. 手动模式：按住鼠标 -> 强制开火，不管有没有怪
                            shouldFire = true;
                            // target 保持为 null，CalculateAndSpawn 会去读鼠标位置
                        }
                        else
                        {
                            // B. 自动模式：松开鼠标 -> 找最近的怪
                            target = FindNearestEnemy();

                            // 只有找到怪了才开火！(省子弹，也防止对着空气射)
                            if (target != null)
                            {
                                shouldFire = true;
                            }
                        }

                        // --- 执行发射 ---
                        if (shouldFire)
                        {
                            // 1. 通知动画/状态
                            if (playerController != null) playerController.NotifyAttackPerformed();

                            // 2. 发射 (传入 target，如果是 null 内部会自动处理成鼠标或前方)
                            CalculateAndSpawn(slotData, currentOrigin, isManualAim, target);

                            // 3. 进入冷却
                            float delay = slotData.GetFinalFireDelay(fireInterval);
                            nextFireTimes[i] = Time.time + delay;
                        }
                        else
                        {
                            // 如果自动模式没找到怪，就保持 "Ready" 状态，下一帧继续检测
                            // 不更新 nextFireTimes，这样怪一进范围就能秒射
                        }
                    }
                }
            }

            yield return null; // 等待下一帧
        }
    }

    // --- 修改后的发射计算方法 ---
    void CalculateAndSpawn(WandStorage data, Transform origin, bool isManual, Transform targetEnemy)
    {
        MagicItem baseItem = data.originalMagic;
        MagicItem mod1 = data.modifiedMagic1;
        MagicItem mod2 = data.modifiedMagic2;
        MagicItem trigger = data.triggerMagic;

        // 计算属性 (Damage, Count, etc.)
        SpellStats finalStats = baseItem.stats;
        if (mod1 != null) finalStats = finalStats + mod1.stats;
        if (mod2 != null) finalStats = finalStats + mod2.stats;

        int projectileCount = Mathf.Max(1, finalStats.count);
        float spreadAngle = finalStats.spread;
        float halfSpread = spreadAngle / 2f;

        // 只有环绕且多发时才计算步进角
        float orbitStepAngle = (finalStats.isOrbiting && projectileCount > 1) ? 360f / projectileCount : 0f;

        // ============ 【瞄准方向计算】 ============
        Quaternion baseAimRotation;

        if (isManual)
        {
            // 情况 1: 手动 -> 朝鼠标
            baseAimRotation = GetMouseRotation(origin);
        }
        else if (targetEnemy != null)
        {
            // 情况 2: 自动 -> 朝敌人
            Vector2 dir = targetEnemy.position - origin.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            baseAimRotation = Quaternion.Euler(0, 0, angle);
        }
        else
        {
            // 情况 3: 保底 (理论上 shouldFire控制了不会进这里，但为了安全)
            // 默认朝枪口前方
            baseAimRotation = origin.rotation;
        }
        // ========================================

        for (int j = 0; j < projectileCount; j++)
        {
            float randomSpreadOffset = UnityEngine.Random.Range(-halfSpread, halfSpread);
            Quaternion finalRotation = baseAimRotation * Quaternion.Euler(0, 0, randomSpreadOffset);

            GameObject spellObj = ObjectPoolManager.Instance.Spawn(baseItem.itemPrefab, origin.position, finalRotation);
            Projectile pScript = spellObj.GetComponent<Projectile>();

            if (pScript != null)
            {
                float currentOrbitOffset = 0f;
                if (finalStats.isOrbiting) currentOrbitOffset = j * orbitStepAngle;
                pScript.Initialize(finalStats, trigger, currentOrbitOffset, baseItem.itemPrefab);
            }
        }
    }

    // 获取鼠标方向
    Quaternion GetMouseRotation(Transform origin)
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(mainCamera.transform.position.z)));
        mouseWorldPos.z = origin.position.z;
        Vector3 aimDirection = (mouseWorldPos - origin.position).normalized;
        float baseAimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0, 0, baseAimAngle);
    }

    // 寻找最近敌人
    Transform FindNearestEnemy()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, autoAimRange, enemyCache, enemyLayer);
        Transform nearest = null;
        float minDistSq = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            if (enemyCache[i] == null) continue;
            if (enemyCache[i].CompareTag("Enemy"))
            {
                float distSq = (enemyCache[i].transform.position - transform.position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = enemyCache[i].transform;
                }
            }
        }
        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, autoAimRange);
    }
}