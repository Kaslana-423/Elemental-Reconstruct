using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "VoidSO", menuName = "Events/VoidSO")]
public class VoidSO : ScriptableObject
{
    public UnityAction OnFireEvent;

    public void RaiseEvent()
    {
        if (OnFireEvent != null)
        {
            OnFireEvent.Invoke();
        }
    }
}
