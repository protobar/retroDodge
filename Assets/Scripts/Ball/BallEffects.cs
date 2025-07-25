using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// ==================== BALL EFFECTS ====================
public class BallEffects : MonoBehaviour
{
    private static BallEffects instance;
    public static BallEffects Instance => instance;

    [Header("Effect Prefabs")]
    public GameObject corruptionEffectPrefab;
    public GameObject trailEffectPrefab;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayCorruptionEffect()
    {
        if (corruptionEffectPrefab != null)
        {
            GameObject effect = Instantiate(corruptionEffectPrefab);
            // Position effect appropriately
            Destroy(effect, 2f);
        }
    }
}