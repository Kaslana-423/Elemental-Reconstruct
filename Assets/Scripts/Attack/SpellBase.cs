using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SpellBase : MonoBehaviour
{
    [Header("基础属性")]
    public float fireRate = 1.0f;  // 攻击间隔
    public float attackRange = 5.0f; // 攻击范围
    protected abstract void OwnFire();
}
