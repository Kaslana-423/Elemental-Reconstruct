using UnityEngine;

public class Coin : MonoBehaviour
{
    // 是否已经被吸附了
    private bool isMagnetized = false;
    private Transform targetPlayer;

    [Header("飞行速度")]
    public float flySpeed = 15f; // 飞向玩家的速度

    void Update()
    {
        // 如果进入被吸附状态，就持续飞向目标
        if (isMagnetized && targetPlayer != null)
        {
            // 优化：使用简单的 SqrMagnitude 距离判断来减少开销（可选）

            // 使用 MoveTowards 平滑移动
            // 为了防止所有金币完美同步导致卡顿，可以给速度加个微小的随机抖动
            float currentSpeed = flySpeed;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPlayer.position,
                currentSpeed * Time.deltaTime
            );

            // 如果距离极近，就在这里处理“吃到”的逻辑（或者靠 OnTriggerEnter 处理）
            if (Vector3.SqrMagnitude(transform.position - targetPlayer.position) < 0.01f) // 0.1 * 0.1 = 0.01
            {
                Collect();
            }
        }
    }

    // --- 公开方法：由玩家的拾取范围调用 ---
    public void StartMagnet(Transform playerTransform)
    {
        // 只有没被吸附的时候才启动，防止逻辑冲突
        if (!isMagnetized)
        {
            isMagnetized = true;
            targetPlayer = playerTransform;

            // 性能优化：吸附时关闭刚体和碰撞，减少物理开销
            var rb = GetComponent<Rigidbody2D>();
            if (rb) rb.simulated = false; // 或 rb.isKinematic = true;
            var col = GetComponent<Collider2D>();
            if (col) col.enabled = false;
        }
    }
    public void Collect()
    {
        // 这里写吃到金币的逻辑，比如增加玩家金币数
        PlayerInventory.PlayerInstance.AddGold(1);
        // 然后销毁或回收金币对象
        ObjectPoolManager.Instance.ReturnToPool(this.gameObject, this.gameObject);
    }
}