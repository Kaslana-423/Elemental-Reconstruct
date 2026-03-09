using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(LineRenderer))]
public class Projectile : MonoBehaviour
{
    [Header("Layer Settings")]
    public LayerMask enemyLayer;
    public LayerMask wallLayer;

    [Header("触发调整")]
    public Character character;
    public float triggerSpawnOffset = 1.0f;

    // --- 内部数据 ---
    private SpellStats currentStats;
    private MagicItem triggerSpell;
    private GameObject mySourcePrefab;
    private Rigidbody2D rb;
    private Collider2D col;
    private LineRenderer lineRenderer;
    private Transform targetEnemy;
    private Transform playerTransform;

    private float initialOrbitOffsetAngle;
    private bool hasInitialized = false;
    private float spawnTime;
    private bool hasReversed;

    // 激光专用
    private float lastDamageTime;

    private Vector2 currentVelocityDir;
    private Collider2D[] enemySearchCache = new Collider2D[50];
    private RaycastHit2D[] wallHitCache = new RaycastHit2D[5];
    // 激光专用：独立计算每个敌人的受击冷却，绝不丢失扫射伤害
    private Dictionary<int, float> enemyDamageCooldowns = new Dictionary<int, float>();
    private float wallTriggerCooldown;
    private HashSet<int> hitEnemiesCache = new HashSet<int>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer)
        {
            lineRenderer.enabled = false;
            lineRenderer.useWorldSpace = true;
        }

        if (PlayerInventory.PlayerInstance != null)
        {
            playerTransform = PlayerInventory.PlayerInstance.transform;
            if (character == null) character = playerTransform.GetComponent<Character>();
        }
    }

    public void Initialize(SpellStats finalStats, MagicItem triggerItem, float initialOrbitOffset, GameObject sourcePrefab)
    {
        this.mySourcePrefab = sourcePrefab;
        this.currentStats = finalStats;
        this.triggerSpell = triggerItem;
        this.initialOrbitOffsetAngle = initialOrbitOffset;
        this.spawnTime = Time.time;
        this.lastDamageTime = -999f;

        ResetState();

        if (character == null && PlayerInventory.PlayerInstance != null)
            character = PlayerInventory.PlayerInstance.GetComponent<Character>();

        // 【新增核心：全自动贴图比例映射】
        // 让美术贴图的实际大小严格服从你在面板填写的 radius 数值
        if (currentStats.radius > 0 && !currentStats.isLaser)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                // 获取无缩放状态下，贴图物理尺寸的最长边 (Unity Unit)
                float maxDimension = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                if (maxDimension > 0.001f)
                {
                    // 核心算法：目标直径 (radius * 2) 除以 贴图最长边，得出绝对等比缩放率
                    float requiredScale = (currentStats.radius * 2f) / maxDimension;
                    transform.localScale = new Vector3(requiredScale, requiredScale, 1f);
                }
            }
        }

        // 【规则4】：爆炸与激光互斥。
        if (currentStats.isExplosion && currentStats.isLaser)
        {
            currentStats.isLaser = false;
        }

        // ==========================
        //         爆炸/领域 逻辑
        // ==========================
        if (currentStats.isExplosion)
        {
            currentStats.isHoming = false;

            if (currentStats.isOrbiting && currentStats.damageRate <= 0)
            {
                currentStats.isOrbiting = false;
            }

            if (currentStats.count > 1 && !currentStats.isOrbiting)
            {
                float scatterRange = (currentStats.radius > 0 ? currentStats.radius : 2f) * 0.8f;
                transform.position += (Vector3)(Random.insideUnitCircle * scatterRange);
            }

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            col.enabled = false;
            if (lineRenderer) lineRenderer.enabled = false;

            // （旧的强行形变缩放逻辑已被移除，由顶部自动适配接管）

            StartCoroutine(ExplosionRoutine());
        }
        // ==========================
        //         激光 逻辑
        // ==========================
        else if (currentStats.isLaser)
        {
            currentStats.isHoming = false;

            if (currentStats.isOrbiting && currentStats.damageRate <= 0)
            {
                currentStats.damageRate = 0.2f;
            }

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            col.enabled = false;
            if (lineRenderer)
            {
                lineRenderer.enabled = true;
                float visualWidth = currentStats.laserWidth > 0 ? currentStats.laserWidth : 0.2f;
                lineRenderer.startWidth = visualWidth;
                lineRenderer.endWidth = visualWidth;
            }

            float randomAngle = 0f;
            if (currentStats.deviation > 0) randomAngle = Random.Range(-currentStats.deviation, currentStats.deviation);
            currentVelocityDir = Quaternion.Euler(0, 0, randomAngle) * transform.right;

            StartCoroutine(LaserRoutine());
        }
        // ==========================
        //       普通子弹 逻辑
        // ==========================
        else
        {
            col.enabled = true;
            if (lineRenderer) lineRenderer.enabled = false;

            if (currentStats.isOrbiting)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero;
                currentVelocityDir = transform.right;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.drag = 0f;
                rb.gravityScale = 0f;

                float randomAngle = 0f;
                if (currentStats.deviation > 0) randomAngle = Random.Range(-currentStats.deviation, currentStats.deviation);
                Vector2 finalDir = Quaternion.Euler(0, 0, randomAngle) * transform.right;

                currentVelocityDir = finalDir.normalized;
                rb.velocity = currentVelocityDir * currentStats.speed;
            }

            if (currentStats.isHoming) FindNearestEnemy();
            StartCoroutine(LifeTimeRoutine(currentStats.lifetime > 0 ? currentStats.lifetime : 5f));
        }

        hasInitialized = true;
    }

    private void ResetState()
    {
        hasInitialized = false;
        targetEnemy = null;

        enemyDamageCooldowns.Clear(); // 必须清空字典防止干扰下一发子弹
        wallTriggerCooldown = -999f;

        hasReversed = false;

        StopAllCoroutines();
        if (rb)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        if (lineRenderer)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
    }

    IEnumerator LifeTimeRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        HandleTriggerSpawn(currentVelocityDir, transform.position);
        ReturnToPool();
    }

    // --- 【修改】激光逻辑协程 ---
    IEnumerator LaserRoutine()
    {
        float timer = 0f;
        float duration = currentStats.lifetime > 0 ? currentStats.lifetime : 0.1f;
        bool isContinuous = currentStats.damageRate > 0;
        bool hasDealtInstantDamage = false;

        if (!isContinuous)
        {
            // 1. 瞬发激光：只在发射瞬间计算一次伤害和路径
            UpdateLaserRaycast(false, ref hasDealtInstantDamage);

            // 然后纯粹等待时间结束（视觉残影保留，不再去重新扫描敌人，防止怪物死后射线穿透）
            while (timer < duration)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // 2. 持续激光：每一帧实时跟随并扫描
            while (timer < duration)
            {
                UpdateLaserRaycast(true, ref hasDealtInstantDamage);
                timer += Time.deltaTime;
                yield return null;
            }
        }

        ReturnToPool();
    }

    void UpdateLaserRaycast(bool isContinuous, ref bool instantDamageFlag)
    {
        if (!lineRenderer) return;

        Vector2 currentPos = transform.position;
        Vector2 currentDir = currentVelocityDir;

        float remainingLen = currentStats.maxDistance > 0 ? currentStats.maxDistance : (currentStats.speed > 0 ? currentStats.speed : 20f);
        int bounces = currentStats.bounceCount;
        int currentPenetration = currentStats.penetration;

        List<Vector3> points = new List<Vector3>();
        points.Add(currentPos);

        hitEnemiesCache.Clear();

        // 墙壁触发与瞬发标记
        bool dealInstantDamageThisFrame = false;
        if (!isContinuous)
        {
            if (!instantDamageFlag)
            {
                dealInstantDamageThisFrame = true;
                instantDamageFlag = true;
            }
        }

        bool canTriggerWall = false;
        if (isContinuous)
        {
            if (Time.time >= wallTriggerCooldown + currentStats.damageRate)
            {
                canTriggerWall = true;
                wallTriggerCooldown = Time.time;
            }
        }
        else
        {
            canTriggerWall = dealInstantDamageThisFrame;
        }

        // 彻底解绑 radius，防止环绕法术的轨道半径把激光判定框撑得过大
        float boxHeight = currentStats.laserWidth > 0 ? currentStats.laserWidth : 0.5f;

        for (int i = 0; i <= bounces; i++)
        {

            if (remainingLen <= 0) break;
            // 【核心修复】：将清空缓存移入循环内部。每一段折射光束都是独立的判定区，允许交叉光束对大型敌人造成复数伤害。
            hitEnemiesCache.Clear();

            // --- 第一步：无厚度射线找墙壁 ---
            float segmentLen = remainingLen;
            Vector2 hitNormal = Vector2.zero;
            bool hitWall = false;
            Vector2 wallHitPoint = Vector2.zero;

            RaycastHit2D[] thinHits = Physics2D.RaycastAll(currentPos, currentDir, remainingLen);
            System.Array.Sort(thinHits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in thinHits)
            {
                GameObject targetObj = hit.collider.gameObject;

                if (targetObj.transform.root == this.transform.root ||
                    (playerTransform != null && targetObj.transform.root == playerTransform.root) ||
                    targetObj.GetComponentInParent<Projectile>() != null)
                {
                    continue;
                }

                if (targetObj.CompareTag("Wall") || IsInLayer(targetObj.layer, wallLayer))
                {
                    segmentLen = hit.distance;
                    hitNormal = hit.normal;
                    hitWall = true;
                    wallHitPoint = hit.point;
                    break;
                }
            }

            Vector2 segmentEndPos = currentPos + currentDir * segmentLen;

            // --- 第二步：严丝合缝的矩形扫敌人 ---
            Vector2 boxCenter = currentPos + currentDir * (segmentLen / 2f);
            float angle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
            Vector2 boxSize = new Vector2(segmentLen, boxHeight);

            Collider2D[] hitColliders = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle);
            List<System.Tuple<float, Collider2D, EnemyBase>> validEnemies = new List<System.Tuple<float, Collider2D, EnemyBase>>();

            foreach (var col in hitColliders)
            {
                GameObject targetObj = col.gameObject;

                if (targetObj == this.gameObject ||
                    (playerTransform != null && (targetObj == playerTransform.gameObject || targetObj.transform.IsChildOf(playerTransform))) ||
                    col.GetComponentInParent<Projectile>() != null)
                {
                    continue;
                }

                if (targetObj.CompareTag("Enemy") || IsInLayer(targetObj.layer, enemyLayer))
                {
                    EnemyBase enemy = col.GetComponent<EnemyBase>();
                    if (enemy == null) enemy = col.GetComponentInParent<EnemyBase>();

                    Vector2 closestPoint = col.ClosestPoint(currentPos);
                    float dist = Vector2.Distance(currentPos, closestPoint);

                    Vector2 dirToPoint = (closestPoint - currentPos).normalized;
                    if (dist > 0.1f && Vector2.Dot(currentDir, dirToPoint) < -0.05f) continue;
                    if (dist > segmentLen) continue;

                    validEnemies.Add(new System.Tuple<float, Collider2D, EnemyBase>(dist, col, enemy));
                }
            }

            validEnemies.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            bool penetrationExhausted = false;

            foreach (var enemyData in validEnemies)
            {
                float dist = enemyData.Item1;
                Collider2D enemyCol = enemyData.Item2;
                EnemyBase enemy = enemyData.Item3;

                int targetID = enemy != null ? enemy.gameObject.GetInstanceID() : enemyCol.gameObject.GetInstanceID();

                if (!hitEnemiesCache.Contains(targetID))
                {
                    hitEnemiesCache.Add(targetID);

                    if (currentPenetration >= 0)
                    {
                        currentPenetration--;

                        // --- 独立计算每个敌人的伤害冷却 ---
                        bool canDealDamageToThisEnemy = false;
                        if (isContinuous)
                        {
                            float lastHitTime = -999f;
                            enemyDamageCooldowns.TryGetValue(targetID, out lastHitTime);
                            if (Time.time >= lastHitTime + currentStats.damageRate)
                            {
                                canDealDamageToThisEnemy = true;
                                enemyDamageCooldowns[targetID] = Time.time;
                            }
                        }
                        else
                        {
                            canDealDamageToThisEnemy = dealInstantDamageThisFrame;
                        }

                        if (canDealDamageToThisEnemy)
                        {
                            if (enemy != null) ApplyDamage(enemy, enemyCol.transform, currentDir);
                            HandleTriggerSpawn(currentDir, enemyCol.ClosestPoint(currentPos));
                        }

                        if (currentPenetration < 0)
                        {
                            segmentEndPos = currentPos + currentDir * dist;
                            segmentLen = dist;
                            penetrationExhausted = true;
                            hitWall = false;
                            break;
                        }
                    }
                }
            }

            points.Add(segmentEndPos);

            // --- 第三步：计算反弹逻辑 ---
            if (hitWall && !penetrationExhausted && i < bounces && segmentLen > 0)
            {
                if (canTriggerWall) HandleTriggerSpawn(currentDir, wallHitPoint);
                currentDir = Vector2.Reflect(currentDir, hitNormal).normalized;
                currentPos = segmentEndPos + currentDir * 0.05f;
                remainingLen -= segmentLen;
            }
            else
            {
                break;
            }
        }

        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
    }

    void Update()
    {
        if (!hasInitialized) return;

        if (currentStats.isOrbiting)
        {
            OrbitMovement();
            if (currentStats.isLaser || currentStats.isExplosion) return;
        }
        else
        {
            if (currentStats.isLaser || currentStats.isExplosion) return;

            // --- 1. 动态处理加速度，直接改变标量速度 ---
            if (currentStats.acceleration != 0)
            {
                currentStats.speed += currentStats.acceleration * Time.deltaTime;
            }

            // --- 2. 运动方向计算 ---
            if (currentStats.isHoming)
            {
                HandleHomingLogic();
            }
            else
            {
                // 【核心新增：回旋镖逻辑】
                // 当加速度为负，且速度已经被减到 0 以下时，触发自动向玩家飞回
                if (currentStats.acceleration < 0 && currentStats.speed < 0 && playerTransform != null)
                {
                    // 速度变为负数的瞬间，清空冷却记忆
                    if (!hasReversed)
                    {
                        hasReversed = true;
                        enemyDamageCooldowns.Clear();
                        hitEnemiesCache.Clear();
                    }

                    // 【修改】：触碰回收机制
                    // 设定一个稍微宽裕的判定半径，确保高速飞回时不会穿模漏判
                    float collectRadius = currentStats.radius > 0 ? currentStats.radius : 1.5f;
                    float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

                    // 当子弹飞回，距离玩家足够近时，直接回收
                    if (distToPlayer <= collectRadius + 0.5f)
                    {
                        ReturnToPool();
                        return; // 结束当前帧，对象已销毁
                    }

                    // 保持原有的平滑转向玩家的逻辑
                    Vector2 dirToPlayer = ((Vector2)playerTransform.position - rb.position).normalized;
                    Vector2 targetForward = -dirToPlayer;

                    float turnSpeed = currentStats.angularSpeed > 0 ? currentStats.angularSpeed : 270f;

                    float currentAngle = Mathf.Atan2(currentVelocityDir.y, currentVelocityDir.x) * Mathf.Rad2Deg;
                    float targetAngle = Mathf.Atan2(targetForward.y, targetForward.x) * Mathf.Rad2Deg;
                    float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

                    currentVelocityDir = (Quaternion.Euler(0, 0, newAngle) * Vector3.right).normalized;
                }
                // 物理速度 = 绝对前方向 * 当前标量速度（此时 speed 为负数，完美后退）
                rb.velocity = currentVelocityDir * currentStats.speed;
            }

            // --- 3. 视觉旋转同步 ---
            if (currentStats.spinSpeed != 0)
            {
                // 如果配置了自转速度，则无视飞行方向，持续自转（适用于锯刃、回旋镖、黑洞）
                transform.Rotate(0, 0, currentStats.spinSpeed * Time.deltaTime);
            }
            else if (rb.velocity.sqrMagnitude > 0.01f)
            {
                // 未配置自转，则保持原逻辑：让子弹头永远朝着运动方向（适用于飞弹、弓箭）
                float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    void ApplyDamage(EnemyBase enemy, Transform hitTransform, Vector2 hitDirection)
    {
        bool isCrit = Random.value * 100 < currentStats.critRate + character.critRate;
        float finalDamage = (currentStats.damage + character.atk)
            * (isCrit ? (currentStats.critMultiplier + character.critDamage) / 100 : 1f)
            * character.allDamageMultiplier;
        character.Heal(finalDamage * character.lifeStealPercent);

        // 传递方向
        ApplyKnockback(hitTransform, hitDirection);
        enemy.TakeDamage(finalDamage, isCrit);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasInitialized || currentStats.isLaser || currentStats.isExplosion) return;

        bool isEnemy = other.CompareTag("Enemy") || IsInLayer(other.gameObject.layer, enemyLayer);
        bool isWall = other.CompareTag("Wall") || IsInLayer(other.gameObject.layer, wallLayer);

        if (!isEnemy && !isWall) return;

        Vector2 incomingDir = currentVelocityDir;
        Vector2 reflectDir = incomingDir;
        Vector2 safeSpawnPos = (Vector2)transform.position - (incomingDir * triggerSpawnOffset);

        if (isWall)
        {
            Vector2 normal = GetImpactNormal(other, incomingDir);
            reflectDir = Vector2.Reflect(incomingDir, normal).normalized;
            HandleTriggerSpawn(reflectDir, safeSpawnPos);
        }
        else if (isEnemy)
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy == null) enemy = other.GetComponentInParent<EnemyBase>();

            int targetID = enemy != null ? enemy.gameObject.GetInstanceID() : other.gameObject.GetInstanceID();

            // 【核心修复】：将永久锁改为冷却判定。
            float hitCooldown = currentStats.damageRate > 0 ? currentStats.damageRate : 999f;
            if (enemyDamageCooldowns.TryGetValue(targetID, out float lastTime))
            {
                if (Time.time < lastTime + hitCooldown) return;
            }

            // 记录本次命中时间
            enemyDamageCooldowns[targetID] = Time.time;

            if (enemy != null) ApplyDamage(enemy, other.transform, incomingDir);

            currentStats.penetration--;
            HandleTriggerSpawn(incomingDir, transform.position);
        }

        if (currentStats.bounceCount > 0 && isWall)
        {
            rb.velocity = reflectDir * currentStats.speed;
            currentVelocityDir = reflectDir;
            currentStats.bounceCount--;

            // 【核心修复】：物理弹射后清空受击记忆
            enemyDamageCooldowns.Clear();
        }
        else if (isWall || (isEnemy && currentStats.penetration < 0))
        {
            ReturnToPool();
        }
    }
    Vector2 GetImpactNormal(Collider2D wall, Vector2 incomingDir)
    {
        float checkDist = 2.0f;
        Vector2 origin = (Vector2)transform.position - (incomingDir * checkDist);
        int count = Physics2D.RaycastNonAlloc(origin, incomingDir, wallHitCache, checkDist + 2.0f, wallLayer);
        for (int i = 0; i < count; i++)
        {
            if (wallHitCache[i].collider == wall) return wallHitCache[i].normal;
        }
        if (wall is BoxCollider2D box) return GetNormalForBoxCollider(box, wall.ClosestPoint(transform.position));
        return -incomingDir;
    }

    Vector2 GetNormalForBoxCollider(BoxCollider2D box, Vector2 worldPoint)
    {
        Vector2 localPoint = box.transform.InverseTransformPoint(worldPoint);
        localPoint -= box.offset;
        float halfWidth = box.size.x * 0.5f;
        float halfHeight = box.size.y * 0.5f;
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
                GameObject newObj = ObjectPoolManager.Instance.Spawn(triggerSpell.itemPrefab, originPos, finalRotation);
                Projectile newScript = newObj.GetComponent<Projectile>();
                if (newScript != null) newScript.Initialize(triggerSpell.stats, null, 0f, triggerSpell.itemPrefab);
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

        // 【修复1】：解绑 radius，独立计算轨道距离
        float r;
        if (currentStats.isExplosion)
        {
            // 领域法术：如果是单发（如黑洞），r=0 绝对贴身；如果被修饰符变成多发，则在外圈形成护城河
            r = currentStats.count > 1 ? Mathf.Max(2.5f, currentStats.radius + 1f) : 0f;
        }
        else
        {
            // 常规物理子弹：固定一个基础轨道距离(2.5)，并根据子弹自身大小略微外扩，防止穿模
            r = 2.5f + (currentStats.radius * 0.5f);
        }

        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * r;

        // 【修复2】：寻找玩家的真实中心点，取代脚底坐标
        Vector3 orbitCenter = playerTransform.position;
        Transform centerPoint = playerTransform.Find("Center"); // 尝试寻找名为 Center 的子物体

        if (centerPoint != null)
        {
            orbitCenter = centerPoint.position;
        }
        else
        {
            // 如果没建 Center 子物体，保底向上偏移 0.5 个单位（大概在胸口位置）
            orbitCenter += Vector3.up * 0.5f;
        }

        rb.MovePosition(orbitCenter + offset);
        transform.position = orbitCenter + offset;
        transform.rotation = Quaternion.Euler(0, 0, finalAngle + 90f);
        currentVelocityDir = transform.right;
    }

    void HandleHomingLogic()
    {
        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
        {
            FindNearestEnemy();
            // 找不到敌人时保底按直线飞行
            rb.velocity = currentVelocityDir * currentStats.speed;
            return;
        }

        // 核心修复：使用 currentVelocityDir 而不是 rb.velocity 计算当前角度，防止速度变负时翻转
        float currentAngle = Mathf.Atan2(currentVelocityDir.y, currentVelocityDir.x) * Mathf.Rad2Deg;

        Vector2 directionToTarget = (Vector2)targetEnemy.position - rb.position;

        // 【兼容负速度】：如果速度减到负数了，为了继续追击敌人，虚拟前方向应该背离敌人（倒车追击）
        Vector2 targetForward = currentStats.speed >= 0 ? directionToTarget : -directionToTarget;
        float targetAngle = Mathf.Atan2(targetForward.y, targetForward.x) * Mathf.Rad2Deg;

        float turnSpeed = currentStats.angularSpeed > 0 ? currentStats.angularSpeed : 360f;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

        currentVelocityDir = (Quaternion.Euler(0, 0, newAngle) * Vector3.right).normalized;
        rb.velocity = currentVelocityDir * currentStats.speed;
    }

    void FindNearestEnemy()
    {
        float searchRadius = 20f;

        // 【核心修复】：取消 physics 层的 enemyLayer 限制，全盘扫描后纯手工判定
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, searchRadius, enemySearchCache);
        float minDistance = Mathf.Infinity;
        Transform nearest = null;

        for (int i = 0; i < count; i++)
        {
            GameObject targetObj = enemySearchCache[i].gameObject;

            // 1. 过滤掉未激活的物体（防止锁定到上一秒刚被爆炸炸死回收的尸体）
            if (!targetObj.activeInHierarchy) continue;

            // 2. 绝对无视自身、玩家、其他子弹
            if (targetObj == this.gameObject ||
                (playerTransform != null && (targetObj == playerTransform.gameObject || targetObj.transform.IsChildOf(playerTransform))) ||
                targetObj.GetComponentInParent<Projectile>() != null)
            {
                continue;
            }

            // 3. 纯手工判定 Tag 或 Layer
            if (IsInLayer(targetObj.layer, enemyLayer) || targetObj.CompareTag("Enemy"))
            {
                float dist = (targetObj.transform.position - transform.position).sqrMagnitude;
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = targetObj.transform;
                }
            }
        }
        targetEnemy = nearest;
    }

    void ApplyKnockback(Transform target, Vector2 hitDirection)
    {
        if (currentStats.knockback <= 0) return;

        EnemyBase enemy = target.GetComponentInParent<EnemyBase>();
        if (enemy != null)
        {
            // 【新增】爆炸强制从中心向外击退
            if (currentStats.isExplosion)
            {
                hitDirection = ((Vector2)target.position - (Vector2)transform.position).normalized;
                if (hitDirection == Vector2.zero) hitDirection = Random.insideUnitCircle.normalized;
            }

            enemy.ApplyKnockback(hitDirection.normalized, currentStats.knockback);
        }
    }
    // --- 爆炸逻辑协程 ---
    IEnumerator ExplosionRoutine()
    {
        float timer = 0f;
        float duration = currentStats.lifetime > 0 ? currentStats.lifetime : 0.5f;
        bool isContinuous = currentStats.damageRate > 0;

        if (!isContinuous)
        {
            TriggerExplosionDamage();
            while (timer < duration)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            while (timer < duration)
            {
                if (Time.time >= lastDamageTime + currentStats.damageRate)
                {
                    TriggerExplosionDamage();
                    lastDamageTime = Time.time;
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }

        ReturnToPool();
    }

    // --- 触发爆炸伤害 ---
    void TriggerExplosionDamage()
    {
        float expRadius = currentStats.radius > 0 ? currentStats.radius : 2f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, expRadius);
        hitEnemiesCache.Clear();

        // 【修改1】：在结算伤害前，先全局锁定一次最近的敌人，作为所有衍生法术的集火目标
        FindNearestEnemy();

        foreach (var hit in hits)
        {
            GameObject targetObj = hit.gameObject;

            if (targetObj == this.gameObject ||
                (playerTransform != null && (targetObj == playerTransform.gameObject || targetObj.transform.IsChildOf(playerTransform))) ||
                targetObj.GetComponentInParent<Projectile>() != null)
            {
                continue;
            }

            if (targetObj.CompareTag("Enemy") || IsInLayer(targetObj.layer, enemyLayer))
            {
                EnemyBase enemy = targetObj.GetComponentInParent<EnemyBase>();
                int targetID = enemy != null ? enemy.gameObject.GetInstanceID() : targetObj.GetInstanceID();

                if (!hitEnemiesCache.Contains(targetID))
                {
                    hitEnemiesCache.Add(targetID);
                    if (enemy != null)
                    {
                        Vector2 dirToEnemy = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                        if (dirToEnemy == Vector2.zero) dirToEnemy = Random.insideUnitCircle.normalized;

                        ApplyDamage(enemy, hit.transform, dirToEnemy);

                        // 【修改2】：把触发逻辑移入循环内部！每炸到一个怪就生成一次
                        Vector2 spawnDir = Vector2.up;

                        if (targetEnemy != null)
                        {
                            // 计算从当前受击怪物，射向“全场最近敌人”的方向
                            spawnDir = ((Vector2)targetEnemy.position - (Vector2)hit.transform.position).normalized;

                            // 如果受击的怪恰好就是被锁定的“最近敌人”，为了防止方向归零报错，顺着爆炸冲击波向外射
                            if (spawnDir == Vector2.zero) spawnDir = dirToEnemy;
                        }

                        // 【修改3】：衍生法术的起点改为 hit.transform.position（受击怪物身上）
                        HandleTriggerSpawn(spawnDir, hit.transform.position);
                    }
                }
            }
        }
    }
}