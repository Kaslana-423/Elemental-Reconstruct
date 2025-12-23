using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance;

    // 字典：Key是预制体(Prefab)，Value是这个预制体对应的队列
    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();

    // 为了保持Hierarchy面板整洁，把生成的子弹都放在这个父物体下
    private Transform poolParent;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        poolParent = new GameObject("ProjectilePool_Parent").transform;
    }

    /// <summary>
    /// 从池中获取对象 (替代 Instantiate)
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // 1. 如果字典里还没这个预制体的记录，先创建一条记录
        if (!poolDictionary.ContainsKey(prefab))
        {
            poolDictionary.Add(prefab, new Queue<GameObject>());
        }

        // 2. 检查队列里有没有闲置的子弹
        if (poolDictionary[prefab].Count > 0)
        {
            // 有闲置的：取出来
            GameObject obj = poolDictionary[prefab].Dequeue();

            // 【保险措施】防止取出的对象在外部被意外销毁了
            if (obj == null)
            {
                return Spawn(prefab, position, rotation);
            }

            obj.SetActive(true);
            obj.transform.position = position;
            obj.transform.rotation = rotation;

            // 【关键修改】即使是复用的对象，也要确保它的 sourcePrefab 是正确的
            // 虽然理论上不会变，但为了保险起见还是赋值一下
            var attackScript = obj.GetComponent<Attack>();
            if (attackScript != null)
            {
                attackScript.sourcePrefab = prefab;
            }

            return obj;
        }
        else
        {
            // 没闲置的：创建一个新的 (Instantiate)
            GameObject newObj = Instantiate(prefab, position, rotation);
            newObj.transform.SetParent(poolParent);

            // 【关键修改】尝试获取 Attack 脚本，并记录它的“生父”预制体
            // 这样子弹自己就知道该回到哪个池子里了
            var attackScript = newObj.GetComponent<Attack>();
            if (attackScript != null)
            {
                attackScript.sourcePrefab = prefab;
            }

            return newObj;
        }
    }

    /// <summary>
    /// 把对象放回池里 (替代 Destroy)
    /// </summary>
    public void ReturnToPool(GameObject obj, GameObject originalPrefab)
    {
        obj.SetActive(false); // 隐藏

        // 这里的 originalPrefab 必须是你生成它时用的那个预制体
        // 如果你不方便传 prefab，可以在 Spawn 时给 obj 挂个脚本记录它的来源
        // 这里为了简单，我们假设调用方知道它是谁生成的

        if (!poolDictionary.ContainsKey(originalPrefab))
        {
            poolDictionary.Add(originalPrefab, new Queue<GameObject>());
        }

        poolDictionary[originalPrefab].Enqueue(obj);
    }
}