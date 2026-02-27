using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelicDamageBuffController : MonoBehaviour
{
    [System.Serializable]
    private struct DamageBuffEntry
    {
        public float amount;
        public bool isMultiplier;
        public float duration;
    }

    private readonly List<DamageBuffEntry> entries = new List<DamageBuffEntry>();
    private Character character;

    private float activeAdd = 0f;
    private float activeMultiplier = 1f;
    private bool isBound = false;

    public void RegisterEffect(Character target, float amount, bool isMultiplier, float duration)
    {
        if (target == null) return;

        character = target;

        if (!isBound)
        {
            character.OnTakeDamage += HandleTakeDamage;
            isBound = true;
        }

        entries.Add(new DamageBuffEntry
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
            StartCoroutine(ApplyDamageBuff(entries[i]));
        }
    }

    private IEnumerator ApplyDamageBuff(DamageBuffEntry entry)
    {
        if (character == null) yield break;

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

    private float ComputeBaseMultiplier()
    {
        if (character == null) return 1f;

        float value = character.allDamageMultiplier - activeAdd;
        if (Mathf.Abs(activeMultiplier) > 0.0001f)
        {
            value /= activeMultiplier;
        }

        return value;
    }

    private void UpdateMultiplier(float baseValue)
    {
        if (character == null) return;
        character.allDamageMultiplier = (baseValue * activeMultiplier) + activeAdd;
    }

    private void ApplyAdd(float amount)
    {
        float baseValue = ComputeBaseMultiplier();
        activeAdd += amount;
        UpdateMultiplier(baseValue);
    }

    private void RemoveAdd(float amount)
    {
        float baseValue = ComputeBaseMultiplier();
        activeAdd -= amount;
        UpdateMultiplier(baseValue);
    }

    private void ApplyMultiplier(float amount)
    {
        if (amount <= 0f) return;

        float baseValue = ComputeBaseMultiplier();
        activeMultiplier *= amount;
        UpdateMultiplier(baseValue);
    }

    private void RemoveMultiplier(float amount)
    {
        if (amount <= 0f) return;

        float baseValue = ComputeBaseMultiplier();
        activeMultiplier /= amount;
        UpdateMultiplier(baseValue);
    }
}
