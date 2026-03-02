using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Character : MonoBehaviour
{
    [Header("MP 基础属性")]
    public float maxMP = 100f;
    // 使用 protected 保护数据，SerializeField 方便在编辑器看
    [SerializeField] protected float _currentMP;

    // 公开的只读属性
    public float CurrentMP => _currentMP;

    // MP 变化事件，UI可以监听这个
    public event Action<float, float> OnMPChanged;

    protected virtual void Start()
    {
        // 初始化 MP
        _currentMP = maxMP;
        // 初始触发一次事件，让UI更新到满状态
        OnMPChanged?.Invoke(_currentMP, maxMP);

        // ... (其他初始化) ...
    }

    // --- 核心接口方法 ---

    // 1. 尝试消耗 MP (通用接口)
    // 返回 true 表示钱够了，扣款成功
    public virtual bool ConsumeMP(float amount)
    {
        if (_currentMP >= amount)
        {
            _currentMP -= amount;
            OnMPChanged?.Invoke(_currentMP, maxMP); // 通知 UI 更新
            return true;
        }
        else
        {
            // Debug.Log($"{gameObject.name} MP 不足！");
            // 这里可以触发一个“缺蓝”事件，比如让角色头顶冒一个图标，或播放音效
            return false;
        }
    }

    // 2. 回复/吸收 MP (通用接口)
    public virtual void RestoreMP(float amount)
    {
        _currentMP += amount;
        // 确保不超过上限，也不低于 0
        _currentMP = Mathf.Clamp(_currentMP, 0f, maxMP);
        OnMPChanged?.Invoke(_currentMP, maxMP); // 通知 UI 更新
    }
}
