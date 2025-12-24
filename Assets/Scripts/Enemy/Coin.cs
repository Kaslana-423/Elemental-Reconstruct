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
            // 使用 MoveTowards 平滑移动
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPlayer.position,
                flySpeed * Time.deltaTime
            );

            // 如果距离极近，就在这里处理“吃到”的逻辑（或者靠 OnTriggerEnter 处理）
            if (Vector3.Distance(transform.position, targetPlayer.position) < 0.1f)
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