using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ==================== EFFECTS POOL ====================
public class EffectsPool : MonoBehaviour
{
    private static EffectsPool instance;
    public static EffectsPool Instance => instance;

    [System.Serializable]
    public class EffectPool
    {
        public string effectName;
        public GameObject effectPrefab;
        public int poolSize = 5;
        [HideInInspector]
        public Queue<GameObject> pool = new Queue<GameObject>();
    }

    [Header("Effect Pools")]
    public EffectPool[] effectPools;

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
        foreach (var effectPool in effectPools)
        {
            for (int i = 0; i < effectPool.poolSize; i++)
            {
                GameObject effect = Instantiate(effectPool.effectPrefab);
                effect.SetActive(false);
                effectPool.pool.Enqueue(effect);
            }
        }
    }

    public GameObject GetEffect(string effectName)
    {
        foreach (var effectPool in effectPools)
        {
            if (effectPool.effectName == effectName)
            {
                if (effectPool.pool.Count > 0)
                {
                    GameObject effect = effectPool.pool.Dequeue();
                    effect.SetActive(true);

                    // Auto-return to pool after particle system finishes
                    ParticleSystem particles = effect.GetComponent<ParticleSystem>();
                    if (particles != null)
                    {
                        StartCoroutine(ReturnToPoolAfterDelay(effect, effectPool, particles.main.duration + particles.main.startLifetime.constantMax));
                    }
                    else
                    {
                        StartCoroutine(ReturnToPoolAfterDelay(effect, effectPool, 2f)); // Default 2 seconds
                    }

                    return effect;
                }
                else
                {
                    // Pool empty, create new instance
                    GameObject effect = Instantiate(effectPool.effectPrefab);
                    ParticleSystem particles = effect.GetComponent<ParticleSystem>();
                    if (particles != null)
                    {
                        StartCoroutine(ReturnToPoolAfterDelay(effect, effectPool, particles.main.duration));
                    }
                    else
                    {
                        StartCoroutine(ReturnToPoolAfterDelay(effect, effectPool, 2f));
                    }
                    return effect;
                }
            }
        }

        Debug.LogWarning($"Effect '{effectName}' not found in pool!");
        return null;
    }

    IEnumerator ReturnToPoolAfterDelay(GameObject effect, EffectPool pool, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (effect != null)
        {
            effect.SetActive(false);
            pool.pool.Enqueue(effect);
        }
    }
}