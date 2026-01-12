using UnityEngine;
using TMPro;
using System.Collections;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Color textColor;
    private float disappearTimer;
    private const float DISAPPEAR_TIMER_MAX = 0.5f; // 存在时间
    private Vector3 moveVector;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void Setup(float damageAmount)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();

        textMesh.text = damageAmount.ToString("0");

        // --- 强制设置显示参数 ---
        textMesh.fontSize = 6f; // 设置更合理的字体大小
        textMesh.alignment = TextAlignmentOptions.Center;

        textColor = textMesh.color;
        disappearTimer = DISAPPEAR_TIMER_MAX;

        // 2. 将 Z 轴设为 -5 (防止被挡)
        transform.position = new Vector3(transform.position.x, transform.position.y, -5f);

        // 随机一个向上的初始速度，带一点左右偏移
        moveVector = new Vector3(Random.Range(-1f, 1f), 3f) * 2f;

        // 重置文字透明度（因为对象池复用时可能是透明的）
        textColor.a = 1f;
        textMesh.color = textColor;

        // 确保如果是从非激活变成激活，重置缩放
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        // 1. 向上移动
        transform.position += moveVector * Time.deltaTime;
        moveVector -= moveVector * 8f * Time.deltaTime; // 模拟阻力/重力减速

        // 2. 倒计时消失
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            // 开始变透明
            float disappearSpeed = 3f;
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a < 0)
            {
                Finish();
            }
        }
    }

    // 用于对象池回收的引用
    [HideInInspector] public GameObject sourcePrefab;

    public void Finish()
    {
        if (ObjectPoolManager.Instance != null && sourcePrefab != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(gameObject, sourcePrefab);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}