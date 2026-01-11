using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("组件引用")]
    // 持有自己身上的 Character 组件引用 (这里是基类，实际是 PlayerStats 或类似的子类)
    [SerializeField] private Character myCharacterStats;

    [Header("数值设置")]
    public float moveSpeed = 6f;

    [Header("组件引用")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public GameObject ShopUI; // 确保在 Inspector 里拖入了商店面板
    public GameObject StateBar;

    // 引用攻击脚本，方便在打开商店时禁用它
    public PlayerAttack playerAttack;

    public Rigidbody2D rb;
    private Vector2 moveInput;

    [Header("战斗状态监测")]
    [Tooltip("停止攻击/受击多少秒后，视为脱战")]
    public float peaceStateDelay = 2.0f;

    // 记录上一次进行“战斗动作”的时间戳
    private float lastCombatActionTime;

    // 控制是否允许输入移动
    private bool isInputEnabled = true;

    // 用于 UI 显示或其他逻辑判断 (比如脱战回血)
    public bool IsInCombatState => Time.time - lastCombatActionTime < peaceStateDelay;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        // 自动获取攻击脚本
        if (playerAttack == null) playerAttack = GetComponent<PlayerAttack>();

        // 自动获取 Character 组件
        if (myCharacterStats == null)
        {
            myCharacterStats = GetComponent<Character>();
        }

        if (myCharacterStats == null)
        {
            Debug.LogError("PlayerController: 找不到 Character 组件！受伤逻辑将失效。");
        }

        // 初始化时间戳，保证刚开始是脱战状态
        lastCombatActionTime = Time.time - peaceStateDelay - 1f;
    }

    void Update()
    {
        // --- 1. 核心修改：如果商店开着，或者输入被禁用，截断逻辑 ---
        if ((ShopUI != null && ShopUI.activeSelf) || !isInputEnabled)
        {
            HandleShopInput(); // 只处理商店开关

            // 强制停止移动 (防止滑行)
            moveInput = Vector2.zero;
            rb.velocity = Vector2.zero;

            return; // 【关键】直接结束 Update，不再执行下面的移动和翻转逻辑
        }

        // --- 下面是正常的移动逻辑 (只有商店关着才会执行) ---

        // 检测商店开启
        HandleShopInput();

        // 读取移动输入
        moveInput = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
        }

        if (Gamepad.current != null && moveInput == Vector2.zero)
        {
            moveInput = Gamepad.current.leftStick.ReadValue();
        }

        moveInput = moveInput.normalized;

        UpdateVisuals();

        // 如果你需要做脱战回血，可以在这里写
        // HandleHealthRegeneration(); 
    }

    void FixedUpdate()
    {
        // 商店开着的时候，Update 里已经 return 了，但为了双重保险
        if (ShopUI == null || !ShopUI.activeSelf)
        {
            rb.velocity = moveInput * moveSpeed;
        }
    }
    public void StopMoveAndAttack()
    {
        isInputEnabled = false; // 禁用输入        moveInput = Vector2.zero;
        rb.velocity = Vector2.zero;

        // 如果有攻击脚本，也让它停止攻击
        if (playerAttack != null)
        {
            playerAttack.enabled = false;
        }
    }
    public void OpenShop()
    {
        if (ShopUI != null)
        {
            ShopUI.SetActive(true);
            if (StateBar != null) StateBar.SetActive(false);

            // 打开商店时禁用攻击
            if (playerAttack != null)
            {
                playerAttack.enabled = false;
            }
        }
    }
    // 单独提取开关商店逻辑
    void HandleShopInput()
    {
        if (ShopUI == null) return;

        // 确保商店开着的时候攻击脚本是被禁用的 (双重保险)
        if (ShopUI.activeSelf == true)
        {
            if (playerAttack != null) playerAttack.enabled = false;
        }

        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            bool isActive = !ShopUI.activeSelf;
            ShopUI.SetActive(isActive);
            StateBar.SetActive(!isActive);
            // 打开商店时禁用攻击，关商店时启用
            if (playerAttack != null)
            {
                playerAttack.enabled = !isActive;
            }
        }
    }

    // --- 新增：供UI按钮调用 ---
    public void FinishShoppingAndStart()
    {
        if (ShopUI != null)
        {
            // 1. 关闭商店
            ShopUI.SetActive(false);

            // 2. 显示UI条
            if (StateBar != null) StateBar.SetActive(true);

            // 3. 启用攻击
            if (playerAttack != null)
            {
                playerAttack.enabled = true;
            }
        }

        isInputEnabled = true;

        // 4. 通知刷怪管理器开始
        if (EnemyWaveManager.Instance != null)
        {
            EnemyWaveManager.Instance.StartCombat();
        }
    }

    void UpdateVisuals()
    {
        if (moveInput.x < 0) spriteRenderer.flipX = true;
        else if (moveInput.x > 0) spriteRenderer.flipX = false;
    }

    // --- 公开方法：供外部调用以触发战斗状态 ---

    // 供 PlayerAttack 调用：当发起攻击时
    public void NotifyAttackPerformed()
    {
        // 刷新战斗计时器
        lastCombatActionTime = Time.time;
    }

    // 供外部（如碰撞检测）调用：当玩家挨打时
    public void NotifyDamageTaken(float damage)
    {
        // 1. 让 Character 扣血
        if (myCharacterStats != null)
        {
            myCharacterStats.TakeDamage(damage);
        }

        // 2. 刷新战斗计时器 (挨打也算进战斗状态)
        lastCombatActionTime = Time.time;

        // Debug.Log($"玩家受到 {damage} 点伤害，进入战斗状态！剩余血量: {myCharacterStats.CurrentHealth}");
    }
}