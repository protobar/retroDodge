using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== POOLED OBJECT ====================
public class PooledObject : MonoBehaviour
{
    [Header("Pool Settings")]
    public float lifetime = 2f;
    public bool autoReturn = true;

    private ObjectPoolManager poolManager;
    private ObjectPoolManager.Pool parentPool;
    private Coroutine returnCoroutine;

    public void Initialize(ObjectPoolManager manager, ObjectPoolManager.Pool pool, float life)
    {
        poolManager = manager;
        parentPool = pool;
        lifetime = life;

        if (autoReturn && lifetime > 0)
        {
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
            }
            returnCoroutine = StartCoroutine(AutoReturn());
        }
    }

    IEnumerator AutoReturn()
    {
        yield return new WaitForSeconds(lifetime);
        ReturnToPool();
    }

    public void ReturnToPool()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        if (poolManager != null && parentPool != null)
        {
            poolManager.ReturnToPool(gameObject, parentPool);
        }
    }

    void OnDisable()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }
    }
}