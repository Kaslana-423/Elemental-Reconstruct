using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Layer Settings")]
    public LayerMask enemyLayer;
    public LayerMask wallLayer;

    [Header("触发调整")]
    [Tooltip("回退距离：建议设为 1.0 左右，确保退回到上一帧的空地区域")]
    public Character character;
    public float triggerSpawnOffset = 1.0f;

    // --- 内部数据 ---
    private SpellStats currentStats;
    private MagicItem triggerSpell;
    private GameObject mySourcePrefab;
    private Rigidbody2D rb;
    private Transform targetEnemy;
    private Transform playerTransform;

    private float initialOrbitOffsetAngle;
    private bool hasInitialized = false;
    private float spawnTime;

    private Vector2 currentVelocityDir;
    private Collider2D[] enemySearchCache = new Collider2D[50];

    // 缓存射线
    private RaycastHit2D[] wallHitCache = new RaycastHit2D[5];

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (PlayerInventory.PlayerInstance != null)
        {
            playerTransform = PlayerInventory.PlayerInstance.transform;
            // 自动寻找 Character 组件
            if (character == null)
            {
                character = playerTransform.GetComponent<Character>();
            }
        }
    }

    public void Initialize(SpellStats finalStats, MagicItem triggerItem, float initialOrbitOffset, GameObject sourcePrefab)
    {
        this.mySourcePrefab = sourcePrefab;
        this.currentStats = finalStats;
        this.triggerSpell = triggerItem;
        this.initialOrbitOffsetAngle = initialOrbitOffset;
        this.spawnTime = Time.time;

        ResetState();

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) playerTransform = player.transform;
        }

        // 确保获取到玩家 Character 组件用于计算伤害
        if (character == null && playerTransform != null)
        {
            character = playerTransform.GetComponent<Character>();
        }

        if (currentStats.isOrbiting)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;

            // 【修复 1】：初始化时，虽然没有物理速度，但必须给它一个逻辑方向
            // 假设它初始朝向是右边，防止 (0,0)
            currentVelocityDir = transform.right;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.drag = 0f;
            rb.gravityScale = 0f;

            float randomAngle = 0f;
            if (currentStats.deviation > 0)
            {
                randomAngle = Random.Range(-currentStats.deviation, currentStats.deviation);
            }
            Vector2 finalDir = Quaternion.Euler(0, 0, randomAngle) * transform.right;

            currentVelocityDir = finalDir.normalized;
            rb.velocity = currentVelocityDir * currentStats.speed;
        }

        StartCoroutine(LifeTimeRoutine(currentStats.lifetime > 0 ? currentStats.lifetime : 5f));

        if (currentStats.isHoming) FindNearestEnemy();

        hasInitialized = true;
    }

    private void ResetState()
    {
        hasInitialized = false;
        targetEnemy = null;
        StopAllCoroutines();
        if (rb)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail) trail.Clear();
    }

    IEnumerator LifeTimeRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        HandleTriggerSpawn(currentVelocityDir, transform.position);
        ReturnToPool();
    }

    void Update()
    {
        if (!hasInitialized) return;

        if (currentStats.isOrbiting)
        {
            OrbitMovement();
        }
        else
        {
            if (rb.velocity.sqrMagnitude > 0.01f)
            {
                currentVelocityDir = rb.velocity.normalized;
                float angle = Mathf.Atan2(currentVelocityDir.y, currentVelocityDir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            if (currentStats.isHoming) HandleHomingLogic();

            if (currentStats.acceleration != 0)
                rb.velocity += (Vector2)transform.right * currentStats.acceleration * Time.deltaTime;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasInitialized) return;

        bool isEnemy = other.CompareTag("Enemy") || IsInLayer(other.gameObject.layer, enemyLayer);
        bool isWall = other.CompareTag("Wall") || IsInLayer(other.gameObject.layer, wallLayer);

        if (!isEnemy && !isWall) return;

        Vector2 incomingDir = currentVelocityDir;
        Vector2 reflectDir = incomingDir; // 默认方向

        // --- 1. 计算生成位置：简单粗暴的“轨迹倒退” ---
        // 不管墙在哪，直接往回退 offset 距离。因为子弹是从那里飞过来的，那里肯定是安全的。
        Vector2 safeSpawnPos = (Vector2)transform.position - (incomingDir * triggerSpawnOffset);

        // --- 2. 计算方向：为了镜面反射，我们只求法线 ---
        if (isWall)
        {
            Vector2 normal = GetImpactNormal(other, incomingDir);
            reflectDir = Vector2.Reflect(incomingDir, normal).normalized;

            // Debug: 绿色是回退后的安全生成点，洋红色是反射方向
            Debug.DrawLine(transform.position, safeSpawnPos, Color.green, 1.0f);
            Debug.DrawRay(safeSpawnPos, reflectDir, Color.magenta, 1.0f);

            // 撞墙触发生成
            HandleTriggerSpawn(reflectDir, safeSpawnPos); // 这里使用 safeSpawnPos
        }
        else if (isEnemy)
        {
            // 撞人伤害
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                bool isCrit = Random.value * 100 < currentStats.critRate + character.critRate;

                // 引入 character.allDamageMultiplier 来支持全局易伤/伤害遗物
                float finalDamage = (currentStats.damage + character.atk)
                    * (isCrit ? (currentStats.critMultiplier + character.critDamage) / 100 : 1f)
                    * character.allDamageMultiplier;
                ApplyKnockback(other.transform);
                enemy.TakeDamage(finalDamage, isCrit);
            }
            currentStats.penetration--;

            // 撞人触发生成（沿原方向穿透）
            HandleTriggerSpawn(incomingDir, transform.position);
        }

        // --- 3. 母体物理处理 ---
        if (currentStats.bounceCount > 0)
        {
            rb.velocity = reflectDir * currentStats.speed;
            currentVelocityDir = reflectDir;
            currentStats.bounceCount--;
        }
        else if (isWall || (isEnemy && currentStats.penetration < 0))
        {
            ReturnToPool();
        }
    }

    /// <summary>
    /// 只获取法线，不再纠结位置
    /// </summary>
    Vector2 GetImpactNormal(Collider2D wall, Vector2 incomingDir)
    {
        // 简单回溯射线，只为了拿 Normal
        float checkDist = 2.0f;
        Vector2 origin = (Vector2)transform.position - (incomingDir * checkDist);

        // 尝试 Raycast
        int count = Physics2D.RaycastNonAlloc(origin, incomingDir, wallHitCache, checkDist + 2.0f, wallLayer);
        for (int i = 0; i < count; i++)
        {
            if (wallHitCache[i].collider == wall)
            {
                return wallHitCache[i].normal;
            }
        }

        // 如果射线没扫到（比如穿模），用 BoxCollider 数学法线保底
        if (wall is BoxCollider2D box)
        {
            return GetNormalForBoxCollider(box, wall.ClosestPoint(transform.position));
        }

        // 最坏情况：直接反弹
        return -incomingDir;
    }

    Vector2 GetNormalForBoxCollider(BoxCollider2D box, Vector2 worldPoint)
    {
        Vector2 localPoint = box.transform.InverseTransformPoint(worldPoint);
        localPoint -= box.offset;

        float halfWidth = box.size.x * 0.5f;
        float halfHeight = box.size.y * 0.5f;

        // 简单的四方向判定
        float distLeft = Mathf.Abs(localPoint.x + halfWidth);
        float distRight = Mathf.Abs(localPoint.x - halfWidth);
        float distBottom = Mathf.Abs(localPoint.y + halfHeight);
        float distTop = Mathf.Abs(localPoint.y - halfHeight);

        float min = distLeft;
        Vector2 localNormal = Vector2.left;

        if (distRight < min) { min = distRight; localNormal = Vector2.right; }
        if (distBottom < min) { min = distBottom; localNormal = Vector2.down; }
        if (distTop < min) { min = distTop; localNormal = Vector2.up; }

        return box.transform.TransformDirection(localNormal).normalized;
    }

    void HandleTriggerSpawn(Vector2 fireDirection, Vector2 originPos)
    {
        if (triggerSpell == null || triggerSpell.itemPrefab == null) return;

        if (ObjectPoolManager.Instance != null)
        {
            int spawnCount = Mathf.Max(1, triggerSpell.stats.count);
            float spreadAngle = triggerSpell.stats.spread;
            float halfSpread = spreadAngle / 2f;

            for (int i = 0; i < spawnCount; i++)
            {
                float randomSpread = Random.Range(-halfSpread, halfSpread);
                float baseAngle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
                Quaternion finalRotation = Quaternion.Euler(0, 0, baseAngle + randomSpread);

                // 直接在 originPos 生成，不再做额外的前向偏移
                // 因为 originPos 已经被我们计算为“回退后的安全点”了
                GameObject newObj = ObjectPoolManager.Instance.Spawn(triggerSpell.itemPrefab, originPos, finalRotation);
                Projectile newScript = newObj.GetComponent<Projectile>();
                if (newScript != null)
                {
                    newScript.Initialize(triggerSpell.stats, null, 0f, triggerSpell.itemPrefab);
                }
            }
        }
    }

    bool IsInLayer(int layer, LayerMask mask) => (mask == (mask | (1 << layer)));

    void ReturnToPool()
    {
        if (ObjectPoolManager.Instance != null && mySourcePrefab != null)
            ObjectPoolManager.Instance.ReturnToPool(this.gameObject, mySourcePrefab);
        else
            Destroy(gameObject);
    }

    void OrbitMovement()
    {
        if (playerTransform == null) return;

        float timeAlive = Time.time - spawnTime;
        float baseAngle = timeAlive * currentStats.angularSpeed;
        float finalAngle = baseAngle + initialOrbitOffsetAngle;

        float rad = finalAngle * Mathf.Deg2Rad;
        float r = currentStats.radius > 0 ? currentStats.radius : 2f;

        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * r;

        rb.MovePosition(playerTransform.position + offset);

        // 设置旋转：让子弹头朝向切线方向 (圆周运动的方向)
        transform.rotation = Quaternion.Euler(0, 0, finalAngle + 90f);

        // 【修复 2】：手动更新 currentVelocityDir
        // 因为我们已经把物体旋转到了切线方向，所以 transform.right 就是当前的“飞行方向”
        currentVelocityDir = transform.right;
    }

    void HandleHomingLogic()
    {
        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
        {
            FindNearestEnemy(); return;
        }
        Vector2 currentVelocity = rb.velocity;
        Vector2 directionToTarget = (Vector2)targetEnemy.position - rb.position;
        float currentAngle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
        float turnSpeed = currentStats.angularSpeed > 0 ? currentStats.angularSpeed : 360f;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);
        Quaternion rotation = Quaternion.AngleAxis(newAngle, Vector3.forward);
        rb.velocity = rotation * Vector3.right * currentStats.speed;
    }

    void FindNearestEnemy()
    {
        float searchRadius = 20f;
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, searchRadius, enemySearchCache, enemyLayer);
        float minDistance = Mathf.Infinity;
        Transform nearest = null;
        for (int i = 0; i < count; i++)
        {
            if (IsInLayer(enemySearchCache[i].gameObject.layer, enemyLayer) || enemySearchCache[i].CompareTag("Enemy"))
            {
                float dist = (enemySearchCache[i].transform.position - transform.position).sqrMagnitude;
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = enemySearchCache[i].transform;
                }
            }
        }
        targetEnemy = nearest;
    }

    void ApplyKnockback(Transform target)
    {
        if (currentStats.knockback <= 0) return;
        if (target.TryGetComponent<Rigidbody2D>(out Rigidbody2D targetRb))
        {
            Vector2 dir = (target.position - transform.position).normalized;
            targetRb.AddForce(dir * currentStats.knockback, ForceMode2D.Impulse);
        }
    }
}