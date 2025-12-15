using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance;

    // 字典：Key是预制体的名字，Value是这个预制体对应的队列（池子）
    private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // --- 核心方法 1: 从池中获取对象 ---
    public GameObject GetObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;

        // 1. 如果字典里还没这个池子，先创建一个
        if (!poolDictionary.ContainsKey(key))
        {
            poolDictionary[key] = new Queue<GameObject>();
        }

        // 2. 尝试从队列里取出一个闲置对象
        if (poolDictionary[key].Count > 0)
        {
            GameObject obj = poolDictionary[key].Dequeue();

            // 如果这个对象在池子里的时候意外被删了(极少情况)，就递归再取一次
            if (obj == null) return GetObject(prefab, position, rotation);

            obj.SetActive(true); // 激活它！
            obj.transform.position = position;
            obj.transform.rotation = rotation; // 添加旋转设置
            return obj;
        }
        else
        {
            // 3. 如果池子空了，只能实例化一个新的（扩容）
            GameObject newObj = Instantiate(prefab, position, rotation); // 使用 rotation
            newObj.name = key; // 确保名字一致，方便回收
            return newObj;
        }
    }

    // --- 核心方法 2: 回收对象 ---
    public void ReturnObject(GameObject obj)
    {
        string key = obj.name; // 这里我们利用名字作为Key

        // 确保它被停用
        obj.SetActive(false);

        // 放入对应的队列
        if (!poolDictionary.ContainsKey(key))
        {
            poolDictionary[key] = new Queue<GameObject>();
        }
        poolDictionary[key].Enqueue(obj);
    }
}