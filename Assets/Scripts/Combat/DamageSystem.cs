using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ==================== DAMAGE SYSTEM ====================
public class DamageSystem : MonoBehaviour
{
    [Header("Damage Settings")]
    public float invincibilityDuration = 0.5f;

    private static Dictionary<DamageType, DamageInfo> damageTable =
        new Dictionary<DamageType, DamageInfo>
        {
            { DamageType.BallHit, new DamageInfo { baseDamage = 10f, stunDuration = 0.3f } },
            { DamageType.HoldPenalty, new DamageInfo { baseDamage = 2f, stunDuration = 0f } },
            { DamageType.Ultimate, new DamageInfo { baseDamage = 25f, stunDuration = 0.8f } }
        };

    public static float CalculateDamage(DamageType type, float multiplier = 1f)
    {
        if (damageTable.ContainsKey(type))
        {
            return damageTable[type].baseDamage * multiplier;
        }
        return 0f;
    }

    public static float GetStunDuration(DamageType type)
    {
        if (damageTable.ContainsKey(type))
        {
            return damageTable[type].stunDuration;
        }
        return 0f;
    }

    public static void ApplyDamage(CharacterBase target, float damage, DamageType type, Vector3 hitPosition)
    {
        if (target == null) return;

        // Apply damage
        target.TakeDamage(damage, type);

        // Create damage number UI
        CreateDamageNumber(hitPosition, damage, type);

        // Screen shake based on damage type
        float shakeIntensity = type == DamageType.Ultimate ? 0.5f : 0.2f;
        CameraShake.Instance?.Shake(shakeIntensity, 0.3f);
    }

    static void CreateDamageNumber(Vector3 worldPos, float damage, DamageType type)
    {
        // Create floating damage number
        if (DamageNumberUI.Instance != null)
        {
            Color damageColor = type == DamageType.Ultimate ? Color.red :
                               type == DamageType.HoldPenalty ? Color.magenta : Color.white;

            DamageNumberUI.Instance.ShowDamageNumber(worldPos, damage, damageColor);
        }
    }
}

[System.Serializable]
public class DamageInfo
{
    public float baseDamage;
    public float stunDuration;
}