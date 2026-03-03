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
    private HashSet<int> hitEnemiesCache = new HashSet<int>();

    private Vector2 currentVelocityDir;
    private Collider2D[] enemySearchCache = new Collider2D[50];
    private RaycastHit2D[] wallHitCache = new RaycastHit2D[5];

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
        //         激光 逻辑
        // ==========================
        if (currentStats.isLaser)
        {
            // 【新增保险1】：直接没收追踪和环绕属性，不管面板上有没有挂，激光一律按 False 处理！
            currentStats.isHoming = false;
            currentStats.isOrbiting = false;

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
            col.enabled = false;

            if (lineRenderer)
            {
                lineRenderer.enabled = true;
                lineRenderer.startWidth = currentStats.radius > 0 ? currentStats.radius : 0.2f;
                lineRenderer.endWidth = lineRenderer.startWidth;
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

        bool shouldDealDamage = false;
        if (isContinuous)
        {
            if (Time.time >= lastDamageTime + currentStats.damageRate)
            {
                shouldDealDamage = true;
                lastDamageTime = Time.time;
            }
        }
        else
        {
            if (!instantDamageFlag)
            {
                shouldDealDamage = true;
                instantDamageFlag = true;
            }
        }

        float thickness = currentStats.radius > 0 ? currentStats.radius : 0.5f;

        for (int i = 0; i <= bounces; i++)
        {
            if (remainingLen <= 0) break;

            Vector2 segmentEndPos = currentPos + currentDir * remainingLen;
            Vector2 hitNormal = Vector2.zero;
            bool hitWall = false;

            RaycastHit2D[] allHits = Physics2D.CircleCastAll(currentPos, thickness, currentDir, remainingLen);
            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in allHits)
            {
                if (hit.collider.gameObject == this.gameObject ||
                   (playerTransform != null && hit.collider.gameObject == playerTransform.gameObject))
                    continue;

                GameObject targetObj = hit.collider.gameObject;

                bool isWall = targetObj.CompareTag("Wall") || IsInLayer(targetObj.layer, wallLayer);
                bool isEnemy = targetObj.CompareTag("Enemy") || IsInLayer(targetObj.layer, enemyLayer);

                if (isWall)
                {
                    // 【修复1】：强制焊死在中心直线上，绝对不向接触点偏移！
                    segmentEndPos = currentPos + currentDir * hit.distance;
                    hitNormal = hit.normal;
                    hitWall = true;

                    // 触发衍生子弹依然可以用 hit.point (生成在真实碰撞位置)
                    if (shouldDealDamage) HandleTriggerSpawn(currentDir, hit.point);

                    remainingLen -= hit.distance;
                    break;
                }
                else if (isEnemy)
                {
                    EnemyBase enemy = targetObj.GetComponentInParent<EnemyBase>();
                    int targetID = enemy != null ? enemy.gameObject.GetInstanceID() : targetObj.GetInstanceID();

                    if (!hitEnemiesCache.Contains(targetID))
                    {
                        hitEnemiesCache.Add(targetID);

                        if (currentPenetration >= 0)
                        {
                            // 【核心修改】：1. 先扣除穿透次数，评估是否截断
                            currentPenetration--;
                            bool isTruncatedHere = (currentPenetration < 0);

                            // 【核心修改】：2. 如果需要截断，立刻锁定截断终点
                            if (isTruncatedHere)
                            {
                                // 【修复2】：即使穿透耗尽停在敌人身上，光束末端也必须严格保持笔直！
                                segmentEndPos = currentPos + currentDir * hit.distance;
                                remainingLen = 0;
                            }

                            // 【核心修改】：3. 最后再去结算伤害（哪怕怪物在这一行代码被销毁，截断逻辑也已经完美生效了）
                            if (shouldDealDamage)
                            {
                                if (enemy != null) ApplyDamage(enemy, hit.collider.transform);
                                HandleTriggerSpawn(currentDir, hit.point);
                            }

                            // 4. 彻底退出射线扫描循环
                            if (isTruncatedHere)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            points.Add(segmentEndPos);

            if (hitWall && i < bounces && remainingLen > 0)
            {
                currentDir = Vector2.Reflect(currentDir, hitNormal).normalized;
                currentPos = segmentEndPos + currentDir * 0.05f;
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

        if (currentStats.isLaser) return;

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

    void ApplyDamage(EnemyBase enemy, Transform hitTransform)
    {
        bool isCrit = Random.value * 100 < currentStats.critRate + character.critRate;
        float finalDamage = (currentStats.damage + character.atk)
            * (isCrit ? (currentStats.critMultiplier + character.critDamage) / 100 : 1f)
            * character.allDamageMultiplier;
        character.Heal(finalDamage * character.lifeStealPercent);
        ApplyKnockback(hitTransform);
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
            if (enemy != null) ApplyDamage(enemy, other.transform);

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
        transform.rotation = Quaternion.Euler(0, 0, finalAngle + 90f);
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