using UnityEngine;
using DG.Tweening;
using System.Collections;
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
    public virtual void TakeDamage(float amount)
    {
        currentHP -= amount;
        StartCoroutine(FlashEffect());
        // 播放受击动画、跳伤害数字...
        if (currentHP <= 0) Die();
    }

    protected virtual void Die()
    {
        // 掉落物品、播放死亡特效、销毁对象
        Destroy(gameObject);
    }

    // 4. 定义一个移动的方法，但不写死逻辑，因为 Boss 和 杂兵 移动方式不同
    protected virtual void Move()
    {
        if (playerTarget == null) return;

        // 计算与目标的距离
        float distance = Vector2.Distance(transform.position, playerTarget.position);

        // 【新增】防止抖动：如果距离非常近（比如小于0.2），就停止移动
        if (distance < 0.1f)
        {
            rb.velocity = Vector2.zero;
            anim.SetFloat("velocity", 0f); // 停止播放走路动画
            return;
        }

        anim.SetFloat("velocity", moveSpeed);

        // 默认逻辑：简单的朝向玩家移动
        Vector2 dir = playerTarget.position - transform.position;
        rb.velocity = dir.normalized * moveSpeed;

        // 处理翻转 (Flip)
        Vector3 currentScale = transform.localScale;

        // 增加阈值 0.1f，防止在垂直移动或与玩家重叠时频繁左右鬼畜翻转
        if (dir.x > 0.2f)
            transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z);
        else if (dir.x < -0.2f)
            transform.localScale = new Vector3(-Mathf.Abs(currentScale.x), currentScale.y, currentScale.z);
    }
}