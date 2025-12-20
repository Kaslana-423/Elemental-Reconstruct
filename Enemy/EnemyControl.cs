using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyControl : MonoBehaviour
{
    [Header("组件")]
    public Transform playerTrans;
    private Rigidbody2D rb;
    [Header("数值")]
    public float maxHP;
    public float runSpeed;
    public float currentHP;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTrans = playerObj.transform;
    }
    private void OnEnable()
    {
        currentHP = maxHP;
        GetComponent<SpriteRenderer>().color = Color.white;
    }
    private void FixedUpdate()
    {
        if (playerTrans != null)
        {
            // 简单的追踪逻辑：向玩家移动
            Vector2 direction = (playerTrans.position - transform.position).normalized;
            // 为了防止怪物重叠，这里依赖我们之前讨论的 Rigidbody2D 的物理碰撞
            // 所以我们使用 MovePosition 而不是直接改 transform
            rb.MovePosition(rb.position + direction * runSpeed * Time.fixedDeltaTime);
        }
    }
    public void TakeDamage(float damage)
    {
        // Debug.Log("!");
        currentHP -= damage;
        if (currentHP <= 0)
        {
            Die();
        }
    }
    private void Die()
    {
        ProjectTilePoolManager.Instance.Despawn(gameObject);
    }
}
