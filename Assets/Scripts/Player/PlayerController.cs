using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        // 自动获取攻击脚本 (假设挂在同一个物体上)
        if (playerAttack == null) playerAttack = GetComponent<PlayerAttack>();
    }

    void Update()
    {
        // --- 1. 核心修改：如果商店开着，截断逻辑 ---
        // 增加判空，防止 ShopUI 被意外销毁导致报错
        if (ShopUI != null && ShopUI.activeSelf)
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
}