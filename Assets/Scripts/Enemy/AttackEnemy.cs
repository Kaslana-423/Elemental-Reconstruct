using UnityEngine;
using System.Collections;

public class AttackEnemy : EnemyBase
{
    [Header("攻击设置")]
    public float attackRange = 1.5f;   // 攻击范围
    public float attackCooldown = 0.5f;  // 攻击冷却时间
    public float contactDamage = 10f;

    [Header("远程攻击设置")]
    public bool isHaveBullet = false; // 是否有子弹攻击
    public GameObject bulletPrefab;   // 子弹预制体 (原 bullet)
    public Transform firePoint;       // 发射点 (如果不填则默认用自身位置)
    public float bulletSpeed = 5f;    // 子弹速度
    public float fireDelay = 0.2f;    // 动画播放多久后发射 (用于卡点)

    private float lastAttackTime;
    private bool isAttacking = false; // 是否正在攻击中

    void Update()
    {
        anim.SetBool("isAttack", isAttacking);
        if (playerTarget == null) return;

        // 1. 如果正在攻击，完全交由协程控制，Update 不再干涉移动
        if (isAttacking)
        {

            rb.velocity = Vector2.zero; // 确保物理上停住
            return;
        }

        // 计算与玩家的距离
        float distance = Vector2.Distance(transform.position, playerTarget.position);

        // 如果在攻击范围内
        if (distance <= attackRange)
        {
            // 停止移动
            rb.velocity = Vector2.zero;

            // 检查冷却并攻击
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(AttackRoutine());
            }
            else
            {
                // 冷却中，虽然不攻击，但也保持面向玩家
                FaceTarget();
            }
        }
        else
        {
            // 不在范围内，执行基类的追逐逻辑
            base.Move();
        }
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;

        // 攻击前最后一次校准朝向
        FaceTarget();

        anim.SetTrigger("Attack");
        lastAttackTime = Time.time;

        // --- 发射逻辑 ---
        if (isHaveBullet && bulletPrefab != null)
        {
            // 等待一小会儿，让动画播到“出手”的那一帧
            yield return new WaitForSeconds(fireDelay);
            FireBullet();
        }

        // --- 等待动画开始 ---
        // 刚 SetTrigger 时，Animator 可能还没切换状态。
        // 我们等待一小段时间，直到检测到状态名为 "Attack" 或 "attack"
        // 或者超时（防止状态名不对导致卡死）
        float waitTimer = 0f;
        while (waitTimer < 0.5f)
        {
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Attack") || info.IsName("attack"))
            {
                break; // 进入状态了，跳出等待
            }
            waitTimer += Time.deltaTime;
            yield return null;
        }

        // --- 等待动画结束 ---
        while (true)
        {
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);

            // 退出条件：
            // 1. 状态已经不是 Attack 了 (说明播完切走了)
            // 2. 状态是 Attack，但进度 >= 1 (播完了)
            bool isAttackState = info.IsName("Attack") || info.IsName("attack");
            if (!isAttackState || info.normalizedTime >= 1.0f)
            {
                break;
            }
            yield return null;
        }

        // 攻击结束，恢复行动
        isAttacking = false;
    }

    private void FireBullet()
    {
        // 确定发射位置
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        // 1. 从对象池获取子弹
        // 注意：ObjectPoolManager 必须在场景中存在
        GameObject obj = ObjectPoolManager.Instance.Spawn(bulletPrefab, spawnPos, Quaternion.identity);

        if (obj != null && playerTarget != null)
        {
            // 2. 计算方向
            Vector2 dir = (playerTarget.position - spawnPos).normalized;
            // 4. 设置速度 (假设子弹有 Rigidbody2D)
            Rigidbody2D bulletRb = obj.GetComponent<Rigidbody2D>();
            if (bulletRb != null)
            {
                bulletRb.velocity = dir * bulletSpeed;
            }
        }
    }

    // 简单的朝向逻辑 (从基类 Move 中提取的简化版)
    private void FaceTarget()
    {
        Vector2 dir = playerTarget.position - transform.position;
        Vector3 currentScale = transform.localScale;

        if (dir.x > 0.1f)
            transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z);
        else if (dir.x < -0.1f)
            transform.localScale = new Vector3(-Mathf.Abs(currentScale.x), currentScale.y, currentScale.z);
    }

    // 在编辑器里画出攻击范围，方便调试
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    // 碰撞伤害逻辑
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // 假设玩家脚本叫 PlayerHealth
            collision.gameObject.GetComponent<Character>()?.TakeDamage(contactDamage);
        }
    }
}