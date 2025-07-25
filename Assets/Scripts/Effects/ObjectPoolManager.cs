using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== OBJECT POOL MANAGER ====================
public class ObjectPoolManager : MonoBehaviour
{
    private static ObjectPoolManager instance;
    public static ObjectPoolManager Instance => instance;

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
        [HideInInspector]
        public Queue<GameObject> objects = new Queue<GameObject>();
    }

    [Header("Object Pools")]
    public List<Pool> pools = new List<Pool>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePools()
    {
        foreach (Pool pool in pools)
        {
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                pool.objects.Enqueue(obj);
            }
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        Pool targetPool = pools.Find(p => p.tag == tag);

        if (targetPool == null)
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            return null;
        }

        GameObject objectToSpawn;

        if (targetPool.objects.Count > 0)
        {
            objectToSpawn = targetPool.objects.Dequeue();
        }
        else
        {
            // Pool is empty, create new object
            objectToSpawn = Instantiate(targetPool.prefab);
        }

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        // Auto-return to pool after a delay if it has a PooledObject component
        PooledObject pooledObj = objectToSpawn.GetComponent<PooledObject>();
        if (pooledObj != null)
        {
            pooledObj.Initialize(this, targetPool, pooledObj.lifetime);
        }

        return objectToSpawn;
    }

    public void ReturnToPool(GameObject obj, Pool pool)
    {
        obj.SetActive(false);
        pool.objects.Enqueue(obj);
    }
}