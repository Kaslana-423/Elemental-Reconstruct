using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        Coin item = other.GetComponent<Coin>();

        if (item != null)
        {
            item.StartMagnet(transform.parent);
        }
    }
}
