using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    private CircleCollider2D triggerCollider;
    private float baseRadius;

    private void Awake()
    {
        triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider != null)
        {
            baseRadius = triggerCollider.radius;
        }
    }

    private void Start()
    {
        RefreshRangeFromRelics();
    }

    public void RefreshRangeFromRelics()
    {
        if (triggerCollider == null) return;

        float multiplier = 1f;
        if (PlayerInventory.PlayerInstance != null)
        {
            multiplier = Mathf.Max(0f, PlayerInventory.PlayerInstance.pickupRangeMultiplier);
        }

        triggerCollider.radius = baseRadius * multiplier;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Coin item = other.GetComponent<Coin>();

        if (item != null)
        {
            item.StartMagnet(transform.parent);
        }
    }
}
