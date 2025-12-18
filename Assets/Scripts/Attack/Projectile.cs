using UnityEngine;

// 确保物体有刚体和碰撞体，否则报错
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    // --- 内部数据 ---
    private SpellStats currentStats; // 保存接收到的最终属性
    private MagicItem triggerSpell;  // 保存触发槽里的法术

    private Rigidbody2D rb;
    private Transform targetEnemy;   // 追踪目标
    private Transform playerTransform; // 环绕中心（玩家）
    private float initialOrbitOffsetAngle;// 新增一个变量，存储这个子弹特有的初始角度偏移
    private bool hasInitialized = false;

    // 【新增】记录子弹出生时的全球时间戳
    private float spawnTime;

    // --- 初始化 (由 WandController 调用) ---
    public void Initialize(SpellStats finalStats, MagicItem triggerItem, float initialOrbitOffset = 0f)
    {
        this.currentStats = finalStats;
        this.triggerSpell = triggerItem;
        this.rb = GetComponent<Rigidbody2D>();
        // 【新增】保存初始偏移角
        this.initialOrbitOffsetAngle = initialOrbitOffset;
        this.spawnTime = Time.time;
        if (currentStats.isOrbiting)
        {
            // --- 环绕模式初始化 ---

            // A. 找到玩家 (中心点)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;

                // B. 计算初始角度 (防止子弹瞬移)
                // 计算当前生成点相对于玩家的角度，作为起始点
                // Vector3 dir = transform.position - playerTransform.position;
                // currentOrbitAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero; // 清空速度
            // 【新增】环绕物体通常不需要重力
            rb.gravityScale = 0f;
        }
        else
        {
            // 1. 应用基础物理属性
            rb.drag = currentStats.drag; // 阻力

            // 2. 初始速度 (应用偏移度 Deviation)
            // 如果有偏移度，随机旋转一下发射角度
            float randomAngle = 0f;
            if (currentStats.deviation > 0)
            {
                randomAngle = Random.Range(-currentStats.deviation, currentStats.deviation);
            }
            // 设置初始速度方向
            Vector2 finalDir = Quaternion.Euler(0, 0, randomAngle) * transform.right;
            rb.velocity = finalDir * currentStats.speed;
        }
        // 3. 应用大小 (Radius 用来控制大小)
        // 如果 radius 是 0，默认给个 1，防止看不见
        float scale = currentStats.radius > 0 ? currentStats.radius : 1f;

        // 4. 应用寿命
        Destroy(gameObject, currentStats.lifetime > 0 ? currentStats.lifetime : 5f);

        // 5. 如果是追踪模式，立刻找个目标
        if (currentStats.isHoming)
        {
            FindNearestEnemy();
        }

        hasInitialized = true;
    }

    void Update()
    {
        if (!hasInitialized) return;
        if (currentStats.isOrbiting && playerTransform != null)
        {
            OrbitMovement();
        }
        else
        {
            // --- 逻辑 1: 处理加速度 ---
            if (currentStats.acceleration != 0)
            {
                // 沿着当前飞行方向加速
                rb.velocity += (Vector2)transform.right * currentStats.acceleration * Time.deltaTime;
            }

            // --- 逻辑 2: 处理追踪 (Homing) ---
            if (currentStats.isHoming)
            {
                HandleHomingLogic();
            }

            // --- 逻辑 3: 始终让子弹头朝向飞行方向 ---
            // 这样看起来比较自然
            if (rb.velocity != Vector2.zero)
            {
                float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }


    }

    // --- 修改 OrbitMovement 方法 ---
    void OrbitMovement()
    {
        if (playerTransform == null) return;

        // 1. 计算子弹已经存活了多久
        float timeAlive = Time.time - spawnTime;

        // 2. 使用 "存活时间" 来计算基准角度，而不是全球时间
        float baseAngle = timeAlive * currentStats.angularSpeed;

        // 3. 加上这个子弹特有的队形偏移量，得到最终角度
        float finalAngle = baseAngle + initialOrbitOffsetAngle;

        // 4. 数学公式计算位置 (后面代码保持不变)
        float rad = finalAngle * Mathf.Deg2Rad;
        float r = currentStats.radius > 0 ? currentStats.radius : 2f;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * r;

        rb.MovePosition(playerTransform.position + offset);
        transform.rotation = Quaternion.Euler(0, 0, finalAngle + 90f);
    }
    // 追踪逻辑具体实现
    void HandleHomingLogic()
    {
        // 如果目标没了，重找
        if (targetEnemy == null)
        {
            FindNearestEnemy();
            // 如果还是没找到，就保持直线飞行
            return;
        }

        // 1. 获取当前速度方向的角度
        Vector2 currentVelocity = rb.velocity;
        // 防止速度为0时计算错误
        if (currentVelocity == Vector2.zero) currentVelocity = transform.right;

        float currentAngle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;

        // 2. 计算目标方向的角度
        Vector2 directionToTarget = (Vector2)targetEnemy.position - rb.position;
        float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;

        // 3. 使用 MoveTowardsAngle 平滑转向
        // angularSpeed 是每秒转动的度数，确保你的追踪修饰符里这个数值大于0 (例如 180 或 360)
        float turnSpeed = currentStats.angularSpeed > 0 ? currentStats.angularSpeed : 180f; // 默认给个值防止不动
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

        // 4. 将新角度转回速度向量
        Quaternion rotation = Quaternion.AngleAxis(newAngle, Vector3.forward);
        rb.velocity = rotation * Vector3.right * currentStats.speed;
    }

    // 碰撞检测
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasInitialized) return;

        if (other.CompareTag("Enemy"))
        {
            // 1. 造成伤害
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                // 计算暴击
                bool isCrit = Random.value < currentStats.critRate;
                float finalDamage = currentStats.damage * (isCrit ? currentStats.critMultiplier : 1f);

                // 应用击退 (Knockback)
                ApplyKnockback(other.transform);

                enemy.TakeDamage(finalDamage);
            }

            // 2. 触发逻辑 (如果有 Trigger 法术)
            if (triggerSpell != null && triggerSpell.itemPrefab != null)
            {
                SpawnTriggerSpell(transform.position);
            }

            // 3. 穿透逻辑
            currentStats.penetration--;
            if (currentStats.penetration < 0)
            {
                Destroy(gameObject); // 穿透次数用完，销毁
            }
        }
        // else if (other.CompareTag("Wall"))
        // {
        //     // 4. 弹射逻辑 (Bounce)
        //     if (currentStats.bounceCount > 0)
        //     {
        //         // 简单的反弹处理：在 OnTrigger 里做物理反弹比较麻烦
        //         // 这里简单处理为：碰到墙不销毁，而是减少一次弹射次数
        //         // 真正的反弹建议把 Collider 的 IsTrigger 去掉，或者用 Raycast 计算法线
        //         currentStats.bounceCount--;
        //     }
        //     else
        //     {
        //         Destroy(gameObject); // 撞墙销毁
        //     }
        // }
    }

    // 击退效果
    void ApplyKnockback(Transform target)
    {
        if (currentStats.knockback <= 0) return;

        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            Vector2 dir = (target.position - transform.position).normalized;
            // 瞬间施加一个力
            targetRb.AddForce(dir * currentStats.knockback, ForceMode2D.Impulse);
        }
    }

    // 生成触发法术
    void SpawnTriggerSpell(Vector3 pos)
    {
        // 生成
        GameObject newObj = Instantiate(triggerSpell.itemPrefab, pos, Quaternion.identity);
        Projectile newScript = newObj.GetComponent<Projectile>();

        if (newScript != null)
        {
            // 触发出来的法术，不再继承 Trigger，防止无限循环
            // 它的属性直接使用它自己的 stats
            newScript.Initialize(triggerSpell.stats, null);
        }
    }

    // 寻找最近的敌人
    void FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float minDistance = Mathf.Infinity;
        Transform nearest = null;

        foreach (GameObject go in enemies)
        {
            float dist = Vector2.Distance(transform.position, go.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = go.transform;
            }
        }
        targetEnemy = nearest;
    }
}