using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    // --- 内部数据 ---
    private SpellStats currentStats;
    private MagicItem triggerSpell;
    private GameObject mySourcePrefab;
    private Rigidbody2D rb;
    private Transform targetEnemy;
    private Transform playerTransform; // 建议在 GameManager 或 PlayerController 里存单例
    private float initialOrbitOffsetAngle;
    private bool hasInitialized = false;
    private float spawnTime;

    // --- 优化：缓存碰撞检测数组，避免 GC ---
    private Collider2D[] enemySearchCache = new Collider2D[50];

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // 尝试获取玩家单例，避免重复查找
        if (PlayerInventory.PlayerInstance != null)
        {
            playerTransform = PlayerInventory.PlayerInstance.transform;
        }
    }

    // --- 初始化 ---
    public void Initialize(SpellStats finalStats, MagicItem triggerItem, float initialOrbitOffset, GameObject sourcePrefab)
    {
        this.mySourcePrefab = sourcePrefab;
        this.currentStats = finalStats;
        this.triggerSpell = triggerItem;
        this.initialOrbitOffsetAngle = initialOrbitOffset;
        this.spawnTime = Time.time;

        // 1. 彻底重置状态 (非常重要)
        ResetState();

        // 2. 确保玩家引用存在 (双重保险)
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) playerTransform = player.transform;
        }

        // 3. 模式分支
        if (currentStats.isOrbiting)
        {
            rb.bodyType = RigidbodyType2D.Kinematic; // 环绕不受到力
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic; // 普通子弹需要物理
            rb.drag = currentStats.drag;
            rb.gravityScale = 0f; // 这里的游戏应该不需要重力

            // 处理初始偏移 (Deviation)
            float randomAngle = 0f;
            if (currentStats.deviation > 0)
            {
                randomAngle = Random.Range(-currentStats.deviation, currentStats.deviation);
            }
            Vector2 finalDir = Quaternion.Euler(0, 0, randomAngle) * transform.right;
            rb.velocity = finalDir * currentStats.speed;
        }

        // 5. 【优化】启动寿命倒计时 (代替 Destroy)
        StartCoroutine(LifeTimeRoutine(currentStats.lifetime > 0 ? currentStats.lifetime : 5f));

        // 6. 追踪初始化
        if (currentStats.isHoming)
        {
            FindNearestEnemy();
        }

        hasInitialized = true;
    }

    private void ResetState()
    {
        hasInitialized = false;
        targetEnemy = null;
        StopAllCoroutines(); // 停止所有正在运行的逻辑（寿命、特效等）

        // 重置物理状态
        if (rb)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 重置 TrailRenderer (如果有)
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail) trail.Clear();
    }

    // 【新增】寿命协程：代替 Destroy(gameObject, time)
    IEnumerator LifeTimeRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        ReturnToPool(); // 时间到，回家
    }

    void Update()
    {
        if (!hasInitialized) return;

        // 环绕逻辑
        if (currentStats.isOrbiting)
        {
            OrbitMovement();
        }
        // 普通逻辑
        else
        {
            // 加速度
            if (currentStats.acceleration != 0)
            {
                rb.velocity += (Vector2)transform.right * currentStats.acceleration * Time.deltaTime;
            }

            // 追踪
            if (currentStats.isHoming)
            {
                HandleHomingLogic();
            }

            // 朝向修正
            if (rb.velocity.sqrMagnitude > 0.1f) // 速度太小就不转了，防止抖动
            {
                float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }
    }

    void OrbitMovement()
    {
        if (playerTransform == null) return;
        float timeAlive = Time.time - spawnTime;
        float baseAngle = timeAlive * currentStats.angularSpeed;
        float finalAngle = baseAngle + initialOrbitOffsetAngle;

        float rad = finalAngle * Mathf.Deg2Rad;
        float r = currentStats.radius > 0 ? currentStats.radius : 2f;

        // 优化：直接计算 offset
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * r;

        rb.MovePosition(playerTransform.position + offset);
        transform.rotation = Quaternion.Euler(0, 0, finalAngle + 90f);
    }

    void HandleHomingLogic()
    {
        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy) // 检查目标是否还活着
        {
            FindNearestEnemy();
            return;
        }

        // 向量计算优化
        Vector2 currentVelocity = rb.velocity;
        Vector2 directionToTarget = (Vector2)targetEnemy.position - rb.position;

        // 使用 Vector3.RotateTowards 或者 Lerp 插值角度可能比 Atan2 更快，但 Atan2 精度更高
        // 这里的逻辑保持不变，因为数学消耗还是小于 FindObject
        float currentAngle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;

        float turnSpeed = currentStats.angularSpeed > 0 ? currentStats.angularSpeed : 360f;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

        Quaternion rotation = Quaternion.AngleAxis(newAngle, Vector3.forward);
        rb.velocity = rotation * Vector3.right * currentStats.speed;
    }

    // 【优化】使用 Physics2D 查找，而不是遍历全图 tag
    void FindNearestEnemy()
    {
        // 设定一个合理的搜索半径，比如 20 米
        float searchRadius = 20f;

        // 使用 NonAlloc 版本避免产生 GC 垃圾内存
        // enemySearchCache 是我们在类开头定义的数组
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, searchRadius, enemySearchCache);

        float minDistance = Mathf.Infinity;
        Transform nearest = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = enemySearchCache[i];
            // 手动检查 Tag
            if (col.CompareTag("Enemy"))
            {
                float dist = (col.transform.position - transform.position).sqrMagnitude; // 用平方距离比较更快
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = col.transform;
                }
            }
        }
        targetEnemy = nearest;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasInitialized) return;

        if (other.CompareTag("Enemy"))
        {
            // 造成伤害逻辑.
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                bool isCrit = Random.value < currentStats.critRate;
                float finalDamage = currentStats.damage * (isCrit ? currentStats.critMultiplier : 1f);
                ApplyKnockback(other.transform);
                enemy.TakeDamage(finalDamage);
            }

            // 触发逻辑
            if (triggerSpell != null && triggerSpell.itemPrefab != null)
            {
                SpawnTriggerSpell(transform.position);
            }

            // 穿透逻辑
            currentStats.penetration--;
            if (currentStats.penetration < 0)
            {
                ReturnToPool(); // 回家
            }
        }
    }

    void SpawnTriggerSpell(Vector3 pos)
    {
        // 使用对象池生成
        if (ObjectPoolManager.Instance != null)
        {
            GameObject newObj = ObjectPoolManager.Instance.Spawn(triggerSpell.itemPrefab, pos, Quaternion.identity);
            Projectile newScript = newObj.GetComponent<Projectile>();
            if (newScript != null)
            {
                newScript.Initialize(triggerSpell.stats, null, 0f, triggerSpell.itemPrefab);
            }
        }
    }

    void ApplyKnockback(Transform target)
    {
        if (currentStats.knockback <= 0) return;

        if (target.TryGetComponent<Rigidbody2D>(out Rigidbody2D targetRb))
        {
            Vector2 dir;
            // 如果能找到玩家，就计算 玩家->敌人 的向量
            if (playerTransform != null)
            {
                dir = (target.position - playerTransform.position).normalized;
            }
            else
            {
                // 保底：还是按原来的写法
                dir = (target.position - transform.position).normalized;
            }

            targetRb.AddForce(dir * currentStats.knockback, ForceMode2D.Impulse);
        }
    }

    // 封装一个统一的回收方法
    void ReturnToPool()
    {
        if (ObjectPoolManager.Instance != null && mySourcePrefab != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(this.gameObject, mySourcePrefab);
        }
        else
        {
            Destroy(gameObject); // 保底措施
        }
    }
}