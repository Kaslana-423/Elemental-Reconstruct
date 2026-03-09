using UnityEngine;
using DG.Tweening;
using System.Collections;
using JetBrains.Annotations;
// 1. 这是一个抽象类 (abstract)，不能直接挂在物体上，必须被继承
public abstract class EnemyBase : MonoBehaviour
{
    [Header("基础属性")]
    public float maxHP = 100f;
    protected float currentHP;
    public float moveSpeed;
    private Tween hitTween;
    [Header("引用")]
    protected Transform playerTarget;
    private SpriteRenderer sr;
    protected Rigidbody2D rb;
    protected Animator anim;
    private Material originalMaterial; // 存原本的材质
    public Material whiteMaterial;
    public GameObject CoinPrefab;
    public float easyDamage = 1;
    public int CoinNum = 1;

    [Header("UI Effects")]
    public GameObject DamagePopupPrefab;
    // 【新增】打断抵抗阈值（韧性）。低于此击退力的攻击只会推开它，不会打断它的施法
    [Header("状态与抵抗")]
    public float interruptResist = 10f;
    protected bool isKnockedBack = false;

    // 2. 这里的 Awake 是 virtual 的，子类可以重写 (override)
    protected virtual void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        moveSpeed = Random.Range(moveSpeed * 0.8f, moveSpeed * 1.15f);
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        currentHP = maxHP;
        originalMaterial = sr.material;

        // 简单的获取玩家方式，后续可优化为单例获取
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            // 尝试找玩家身上的 "CenterPoint" 子物体
            Transform centerPoint = player.transform.Find("Center");

            if (centerPoint != null)
            {
                // 找到了！目标设为胸口
                playerTarget = centerPoint;
            }
            else
            {
                // 没找到（可能是忘了加），保底还是用玩家脚底
                playerTarget = player.transform;
            }
        }
    }
    void Update()
    {

    }

    IEnumerator FlashEffect()
    {
        // 1. 切换成纯白材质
        sr.material = whiteMaterial;

        // 2. 等待 0.1 秒
        yield return new WaitForSeconds(0.13f);

        // 3. 换回原本的材质
        sr.material = originalMaterial;
    }

    // 3. 受伤逻辑是通用的
    public virtual void TakeDamage(float amount, bool isCrit)
    {
        if (!gameObject.activeInHierarchy) return;

        currentHP -= amount * easyDamage;

        // --- 弹出伤害数字 ---
        if (DamagePopupPrefab != null && ObjectPoolManager.Instance != null)
        {
            // 在头顶稍微偏移一点的位置生成
            GameObject popupObj = ObjectPoolManager.Instance.Spawn(DamagePopupPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            DamagePopup popup = popupObj.GetComponent<DamagePopup>();

            if (popup != null)
            {
                popup.sourcePrefab = DamagePopupPrefab;
                popup.Setup(amount, isCrit);
            }
        }
        StartCoroutine(FlashEffect());
        if (currentHP <= 0) Die();
    }

    protected virtual void Die()
    {
        if (CoinPrefab != null)
        {
            for (int i = 0; i < CoinNum; i++)
            {
                // 1. 随机位置：在半径 0.5 范围内随机生成，避免完全重叠
                Vector3 spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * 0.5f);

                // 2. 生成金币
                GameObject coin = ObjectPoolManager.Instance.Spawn(CoinPrefab, spawnPos, Quaternion.identity);

                // 3. 物理炸开效果：如果有刚体，给一个向外的推力，让它们散开
                Rigidbody2D coinRb = coin.GetComponent<Rigidbody2D>();
                if (coinRb != null)
                {
                    // 计算从中心向外的方向
                    Vector2 dir = (spawnPos - transform.position).normalized;
                    // 防止重合导致方向为0
                    if (dir == Vector2.zero) dir = Random.insideUnitCircle.normalized;

                    // 施加随机力 (Impulse 模式适合瞬间爆发力)
                    float force = Random.Range(2f, 5f);
                    coinRb.AddForce(dir * force, ForceMode2D.Impulse);
                }
            }
        }

        // 掉落物品、播放死亡特效、销毁对象
        ObjectPoolManager.Instance.ReturnToPool(this.gameObject, this.gameObject);
    }

    // --- 修改 Move 方法 ---
    protected virtual void Move()
    {
        // 【核心拦截】：如果在击退状态，禁止代码控制速度，将控制权还给物理引擎
        if (isKnockedBack) return;

        if (playerTarget == null) return;

        float distance = Vector2.Distance(transform.position, playerTarget.position);

        if (distance < 0.1f)
        {
            rb.velocity = Vector2.zero;
            anim.SetFloat("velocity", 0f);
            return;
        }

        anim.SetFloat("velocity", moveSpeed);

        Vector2 dir = playerTarget.position - transform.position;
        rb.velocity = dir.normalized * moveSpeed;

        Vector3 currentScale = transform.localScale;
        if (dir.x > 0.2f)
            transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z);
        else if (dir.x < -0.2f)
            transform.localScale = new Vector3(-Mathf.Abs(currentScale.x), currentScale.y, currentScale.z);
    }
    // 【新增】统一的击退处理方法
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (!gameObject.activeInHierarchy || rb == null) return;

        rb.velocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        // 【核心修改】：只有当受到的击退力大于等于怪物的抵抗阈值时，才触发打断！
        if (force >= interruptResist)
        {
            InterruptAction();
        }

        StopCoroutine(nameof(KnockbackRoutine));
        StartCoroutine(KnockbackRoutine());
    }
    protected virtual void InterruptAction()
    {
        // 如果你的 Animator 里有受击动画 (Hit)，可以在这里统一触发：
        // if (anim != null) anim.SetTrigger("Hit"); 
    }

    private IEnumerator KnockbackRoutine()
    {
        isKnockedBack = true;
        // 击退硬直时间，0.15秒手感较佳，可自行微调
        yield return new WaitForSeconds(0.15f);
        isKnockedBack = false;
    }
}