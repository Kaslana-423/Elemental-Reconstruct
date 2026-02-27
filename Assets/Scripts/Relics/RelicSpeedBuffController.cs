using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelicSpeedBuffController : MonoBehaviour
{
    [System.Serializable]
    private struct SpeedBuffEntry
    {
        public float amount;
        public bool isMultiplier;
        public float duration;
    }

    private readonly List<SpeedBuffEntry> entries = new List<SpeedBuffEntry>();
    private Character character;
    private PlayerController controller;

    private float activeAdd = 0f;
    private float activeMultiplier = 1f;
    private bool isBound = false;

    public void RegisterEffect(Character target, float amount, bool isMultiplier, float duration)
    {
        if (target == null) return;

        character = target;
        if (controller == null) controller = target.GetComponent<PlayerController>();
        if (controller == null)
        {
            Debug.LogWarning("RelicSpeedBuffController: PlayerController not found.");
            return;
        }

        if (!isBound)
        {
            character.OnTakeDamage += HandleTakeDamage;
            isBound = true;
        }

        entries.Add(new SpeedBuffEntry
        {
            amount = amount,
            isMultiplier = isMultiplier,
            duration = duration
        });
    }

    private void OnDisable()
    {
        if (character != null && isBound)
        {
            character.OnTakeDamage -= HandleTakeDamage;
            isBound = false;
        }
    }

    private void HandleTakeDamage()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            StartCoroutine(ApplySpeedBuff(entries[i]));
        }
    }

    private IEnumerator ApplySpeedBuff(SpeedBuffEntry entry)
    {
        if (entry.isMultiplier)
        {
            ApplyMultiplier(entry.amount);
        }
        else
        {
            ApplyAdd(entry.amount);
        }

        if (entry.duration > 0f)
        {
            yield return new WaitForSeconds(entry.duration);
        }
        else
        {
            yield return null;
        }

        if (entry.isMultiplier)
        {
            RemoveMultiplier(entry.amount);
        }
        else
        {
            RemoveAdd(entry.amount);
        }
    }

    private float ComputeBaseSpeed()
    {
        if (controller == null) return 0f;
        float baseSpeed = controller.moveSpeed;
        if (Mathf.Abs(activeMultiplier) > 0.0001f)
        {
            baseSpeed /= activeMultiplier;
        }
        baseSpeed -= activeAdd;
        return baseSpeed;
    }

    private void UpdateSpeed(float baseSpeed)
    {
        if (controller == null) return;
        controller.moveSpeed = (baseSpeed + activeAdd) * activeMultiplier;
    }

    private void ApplyAdd(float amount)
    {
        float baseSpeed = ComputeBaseSpeed();
        activeAdd += amount;
        UpdateSpeed(baseSpeed);
    }

    private void RemoveAdd(float amount)
    {
        float baseSpeed = ComputeBaseSpeed();
        activeAdd -= amount;
        UpdateSpeed(baseSpeed);
    }

    private void ApplyMultiplier(float amount)
    {
        if (amount <= 0f) return;
        float baseSpeed = ComputeBaseSpeed();
        activeMultiplier *= amount;
        UpdateSpeed(baseSpeed);
    }

    private void RemoveMultiplier(float amount)
    {
        if (amount <= 0f) return;
        float baseSpeed = ComputeBaseSpeed();
        activeMultiplier /= amount;
        UpdateSpeed(baseSpeed);
    }
}
