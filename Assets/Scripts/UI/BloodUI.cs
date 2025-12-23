using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

public class BloodUI : MonoBehaviour
{
    public TextMeshProUGUI currentBlood; // 拖入你的 TMP 组件
    public TextMeshProUGUI maxBlood; // 拖入你的 TMP 组件
    public Image healthImage;

    [Header("目标角色")]
    public Character targetCharacter; // 需要引用具体的 Character 实例

    void Start()
    {
        if (targetCharacter == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                targetCharacter = player.GetComponent<Character>();
            }
        }
    }

    void OnEnable()
    {
        // 2. 订阅事件
        if (targetCharacter != null)
        {
            // 先移除防止重复订阅
            targetCharacter.OnHealthChanged -= UpdateBloodDisplay;
            targetCharacter.OnHealthChanged += UpdateBloodDisplay;
            // 初始刷新一次
            UpdateBloodDisplay(targetCharacter.CurrentHealth, targetCharacter.maxHealth);
        }
    }

    void OnDisable()
    {
        // 记得取消订阅，防止报错
        if (targetCharacter != null)
        {
            targetCharacter.OnHealthChanged -= UpdateBloodDisplay;
        }
    }

    // 3. 修正参数类型：必须是 float，因为 Character 中定义的是 Action<float, float>
    void UpdateBloodDisplay(float currentHealth, float maxHealth)
    {
        maxBlood.text = Mathf.CeilToInt(maxHealth).ToString();
        currentBlood.text = Mathf.CeilToInt(currentHealth).ToString();
        healthImage.fillAmount = currentHealth / maxHealth;
    }
}
