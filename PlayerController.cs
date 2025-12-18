using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("组件引用")]
    // 持有自己身上的 Character 组件引用
    [SerializeField] private Character myCharacterStats;

    [Header("数值设置")]
    public float moveSpeed = 6f;

    [Header("组件引用")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public GameObject ShopUI; // 确保在 Inspector 里拖入了商店面板

    // 【新增】引用攻击脚本，方便在打开商店时禁用它
    public PlayerAttack playerAttack;

    private Rigidbody2D rb;
    private Vector2 moveInput;


    [Header("玩家专属：双状态回复机制")]
    [Tooltip("战斗状态下的低速回蓝 (GDD: 战斗低回蓝)")]
    public float combatRegenRate = 4f;
    [Tooltip("脱战状态下的高速回蓝 (GDD: 脱战高回蓝)")]
    public float peaceRegenRate = 25f;
    [Tooltip("停止攻击/受击多少秒后，进入脱战状态")]
    public float peaceStateDelay = 2.0f; // GDD建议3秒，这里设2秒供测试，可调整

    // 记录上一次进行“战斗动作”的时间戳
    private float lastCombatActionTime;

    // 用于 UI 显示或其他逻辑判断
    public bool IsInCombatState => Time.time - lastCombatActionTime < peaceStateDelay;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        // 自动获取攻击脚本 (假设挂在同一个物体上)
        if (playerAttack == null) playerAttack = GetComponent<PlayerAttack>();

        // 如果没有在编辑器里拖拽，就自动获取
        if (myCharacterStats == null)
        {
            myCharacterStats = GetComponent<Character>();
        }

        if (myCharacterStats == null)
        {
            Debug.LogError("PlayerController: 找不到 Character 组件！回蓝逻辑将失效。");
            enabled = false; // 禁用自己以防报错
            return;
        }

        // 初始化时间戳，保证刚开始是脱战状态
        lastCombatActionTime = Time.time - peaceStateDelay - 1f;
    }

    void Update()
    {
        // --- 1. 核心修改：如果商店开着，截断逻辑 ---
        if (ShopUI.activeSelf)
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

        HandleManaRegenerationLogic();
    }

    void FixedUpdate()
    {
        // 商店开着的时候，Update 里已经 return 了，但为了双重保险
        // 或者因为 Update 里把 moveInput 设为了 zero，这里自然也就停了
        if (!ShopUI.activeSelf)
        {
            rb.velocity = moveInput * moveSpeed;
        }
    }

    // 单独提取开关商店逻辑
    void HandleShopInput()
    {
        if (ShopUI.activeSelf == true)
        {
            if (playerAttack != null)
            {
                playerAttack.enabled = false;
            }
        }
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            bool isActive = !ShopUI.activeSelf;
            ShopUI.SetActive(isActive);

            // 【进阶技巧】打开商店时，直接禁用攻击脚本，关商店时启用
            // 这样你都不用去改 PlayerAttack 的代码！
            if (playerAttack != null)
            {
                playerAttack.enabled = !isActive;
            }
        }
    }

    void UpdateVisuals()
    {
        if (moveInput.x < 0) spriteRenderer.flipX = true;
        else if (moveInput.x > 0) spriteRenderer.flipX = false;
    }

    private void HandleManaRegenerationLogic()
    {
        // 安全检查
        if (myCharacterStats == null) return;

        // 如果蓝满了，就别算了
        if (myCharacterStats.CurrentMP >= myCharacterStats.maxMP) return;

        // 判断回复速率
        float currentRegenRate = IsInCombatState ? combatRegenRate : peaceRegenRate;

        // 【关键修改】调用引用的组件的方法
        myCharacterStats.RestoreMP(currentRegenRate * Time.deltaTime);
    }

    // --- 公开方法：供外部调用以触发战斗状态 ---

    // 供 PlayerAttack 调用：当尝试攻击扣蓝成功时调用此方法
    public void NotifyAttackPerformed()
    {
        // 刷新战斗计时器
        lastCombatActionTime = Time.time;
        // Debug.Log("玩家发起攻击，刷新战斗状态");
    }

    // 供外部（如碰撞检测）调用：当玩家挨打时
    public void NotifyDamageTaken(float damage)
    {
        // 先让 Character 扣血
        if (myCharacterStats != null)
        {
            //TODO
            // myCharacterStats.TakeDamage(damage);
        }

        // 然后刷新战斗计时器
        lastCombatActionTime = Time.time;
        // Debug.Log("受到伤害，进入战斗状态！");
    }
}