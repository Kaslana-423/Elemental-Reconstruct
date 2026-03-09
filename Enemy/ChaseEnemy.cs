
using UnityEngine;

public class ChaserEnemy : EnemyBase
{
    public float contactDamage = 10f;
    public float damageInterval = 0.5f; // 伤害间隔设置为 0.2 秒
    private float lastDamageTime;

    void Update()
    {
        // 直接调用基类的移动
        Move();
    }

    // 碰撞伤害逻辑
    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // 检查冷却时间
            if (Time.time >= lastDamageTime + damageInterval)
            {
                // 假设玩家脚本叫 PlayerHealth
                var character = collision.gameObject.GetComponent<Character>();
                if (character != null)
                {
                    character.TakeDamage(contactDamage);
                    lastDamageTime = Time.time; // 更新上次造成伤害的时间
                }
            }
        }
    }
}