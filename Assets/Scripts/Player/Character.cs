using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Character : MonoBehaviour
{
    [Header("HP 基础属性")]
    public float maxHealth = 100f;

    [Header("战斗属性")]
    public float atk = 10;
    public float allDamageMultiplier = 1f; // 全局伤害倍率（模拟易伤效果）
    protected float lastDamageTime = -999f; // 上次受伤时间

    // 使用 protected 保护数据，SerializeField 方便在编辑器调试看数值
    [SerializeField] protected float _currentHealth;

    // 公开的只读属性，外部只能看，不能直接改
    public float CurrentHealth => _currentHealth;

    // 状态标记
    public bool IsDead { get; protected set; }

    // --- 事件系统 ---
    // 1. 血量变化事件：UI 监听这个来更新血条
    // 参数: currentHealth, maxHealth
    public event Action<float, float> OnHealthChanged;

    // 2. 受伤事件：用于播放受击音效、闪白特效、屏幕抖动等
    public event Action OnTakeDamage;

    // 3. 死亡事件：用于播放死亡动画、掉落物品、游戏结束等
    public event Action OnDeath;
    // 定义一个委托
    public delegate void DamageModifier(ref float damage, string spellTag);
    // 定义事件
    public event DamageModifier OnCalculateDamage;

    protected virtual void Start()
    {
        // 初始化 HP
        _currentHealth = maxHealth;
        IsDead = false;

        // 初始触发一次事件，确保 UI 显示满血
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // --- 核心接口方法 ---

    /// <summary>
    /// 受伤逻辑 (通用接口)
    /// </summary>
    /// <param name="damage">受到的伤害数值</param>
    public virtual void TakeDamage(float damage)
    {
        // 如果已经死了，就不要鞭尸了
        if (IsDead) return;

        // --- 全局无敌时间检查 ---
        // 如果距离上次受伤的时间小于无敌时间，则忽略这次伤害
        if (Time.time < lastDamageTime) return;

        // 更新受伤时间
        lastDamageTime = Time.time;

        // 扣血
        _currentHealth -= damage;

        // 触发受击事件 (给音效或特效用)
        OnTakeDamage?.Invoke();

        // 确保血量不低于 0
        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            Die(); // 触发死亡逻辑
        }

        // 通知 UI 更新血条
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// 治疗逻辑 (通用接口)
    /// </summary>
    /// <param name="amount">回复量</param>
    public virtual void Heal(float amount)
    {
        if (IsDead) return; // 尸体通常不能回血

        _currentHealth += amount;

        // 确保不超过上限
        if (_currentHealth > maxHealth)
        {
            _currentHealth = maxHealth;
        }

        // 通知 UI 更新血条
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// 死亡逻辑 (子类可以重写)
    /// </summary>
    public void HealFull()
    {
        if (IsDead) return; // 尸体通常不能回血

        _currentHealth = maxHealth;

        // 通知 UI 更新血条
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }
    protected virtual void Die()
    {
        IsDead = true;

        // 触发死亡事件，让外部（比如 GameMode 或 掉落系统）知道这个角色挂了
        OnDeath?.Invoke();

        Debug.Log($"{gameObject.name} 已死亡");

        // 注意：这里不要直接 Destroy，因为子类可能需要播放几秒钟的死亡动画
        // 建议在子类重写的 Die 里处理 Destroy
    }
}