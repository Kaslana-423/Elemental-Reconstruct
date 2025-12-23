using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public float damage;
    public bool isBullet;
    public float speed = 5f;
    public float lifetime = 3f;

    // 这个变量用来记录“我是谁生成的”，也就是我的原始预制体
    // 不需要你在 Inspector 里拖，代码会自动赋值
    [HideInInspector] public GameObject sourcePrefab;

    private float currentLifetime;

    // 每次从对象池取出（SetActive(true)）时，都会调用 OnEnable
    void OnEnable()
    {
        // 重置寿命计时器
        currentLifetime = lifetime;
    }

    void Update()
    {
        if (isBullet)
        {
            currentLifetime -= Time.deltaTime;
            if (currentLifetime <= 0f)
            {
                ReturnSelf();
            }
        }
    }

    private void ReturnSelf()
    {
        // 如果有记录来源预制体，就回池
        if (sourcePrefab != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(this.gameObject, sourcePrefab);
        }
        else
        {
            // 如果没有记录（比如是直接拖在场景里的），就直接销毁
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 先尝试获取组件，存到一个临时变量里
        Character character = collision.GetComponent<Character>();

        // 2. 【安全检查】先看它是不是 null
        // 如果碰到的是墙壁、地板，character 就是 null，直接 return 跳过
        if (character == null) return;

        // 3. 既然不是 null，说明碰到的是活物，再检查是不是尸体
        // 逻辑锁：如果已经死了，就不再鞭尸
        if (character.CurrentHealth <= 0) return;

        // 4. 一切正常，造成伤害
        character.TakeDamage(this.damage);

        // 5. 如果是子弹，撞到人后应该消失
        if (isBullet)
        {
            ReturnSelf();
        }
    }
}
