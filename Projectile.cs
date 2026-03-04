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
        // ==========================
        //         爆炸/领域 逻辑
        // ==========================
        if (currentStats.isExplosion)
        {
            currentStats.isHoming = false;
            currentStats.isOrbiting = false;

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            col.enabled = false;
            if (lineRenderer) lineRenderer.enabled = false;

            // 【新增】：根据 radius 自动缩放判定领域
            float expRadius = currentStats.radius > 0 ? currentStats.radius : 2f;
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                // Unity 原生 Sprite 默认为 1x1，将其 scale 设为直径即可完美贴合判定范围
                transform.localScale = new Vector3(expRadius * 2f, expRadius * 2f, 1f);
            }

            StartCoroutine(ExplosionRoutine());
        }
        // ==========================
        //         激光 逻辑
        // ==========================
        else if (currentStats.isLaser)
        {
            // 仅没收追踪，放行环绕
            currentStats.isHoming = false;

            // 【核心兼容】：如果拥有环绕属性且为瞬发，强制赋予 0.2 秒的扫射伤害间隔
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

                // 【核心修改】：使用专属的 laserWidth 来控制视觉宽度。如果没填(0)，则给个默认值 0.2f
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
            if (currentStats.isLaser || currentStats.isExplosion) return; // 【修改】拦截爆炸
        }
        else
        {
            if (currentStats.isLaser || currentStats.isExplosion) return; // 【修改】拦截爆炸
            // --- 普通子弹直线与追踪逻辑 ---
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
        if (!hasInitialized || currentStats.isLaser) return;

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
            // 传入物理碰撞的入射方向 incomingDir
            if (enemy != null) ApplyDamage(enemy, other.transform, incomingDir);

            currentStats.penetration--;
            HandleTriggerSpawn(incomingDir, transform.position);
        }

        if (currentStats.bounceCount > 0 && isWall)
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
        float r = currentStats.radius > 0 ? currentStats.radius : 2f;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * r;

        rb.MovePosition(playerTransform.position + offset);

        // 【新增】：强制同步视觉坐标，消除激光扫描时的拖影脱节
        transform.position = playerTransform.position + offset;

        transform.rotation = Quaternion.Euler(0, 0, finalAngle + 90f);

        // 实时刷新绝对方向供激光射线读取
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