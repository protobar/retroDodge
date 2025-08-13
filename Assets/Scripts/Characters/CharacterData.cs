using UnityEngine;

/// <summary>
/// Enhanced CharacterData with clean ability system
/// Full customization for Ultimate, Trick, and Treat abilities
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "Retro Dodge/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Identity")]
    public string characterName = "Unknown Fighter";
    public string characterDescription = "A mysterious dodgeball warrior";
    public Sprite characterIcon;
    public GameObject characterPrefab;

    [Header("Movement Stats")]
    [Range(1f, 10f)]
    public float moveSpeed = 5f;

    [Range(5f, 20f)]
    public float jumpHeight = 12f;

    public bool canDoubleJump = false;
    public bool canDash = false;

    [Header("Dash Settings (if enabled)")]
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private float dashDuration = 0.2f;

    [Header("Health Stats")]
    [Range(50, 200)]
    public int maxHealth = 100;

    [Range(0.5f, 2f)]
    public float damageResistance = 1f;

    [Header("Throw Damage")]
    [Range(5, 25)]
    public int normalThrowDamage = 10;

    [Range(8, 35)]
    public int jumpThrowDamage = 12;

    [Header("Throw Properties")]
    [Range(0.5f, 2f)]
    public float throwSpeedMultiplier = 1f;

    [Range(0f, 1f)]
    public float throwAccuracy = 1f;

    [Header("Visual Effects")]
    public Color characterColor = Color.white;
    public GameObject throwEffect;
    public GameObject dashEffect;

    [Header("Audio")]
    public AudioClip[] throwSounds;
    public AudioClip[] jumpSounds;
    public AudioClip dashSound;

    [Header("═══════════════════════════════════")]
    [Header("ABILITY SYSTEM")]
    [Header("═══════════════════════════════════")]

    [Header("Ultimate Ability")]
    public UltimateType ultimateType = UltimateType.PowerThrow;

    [Header("Ultimate: PowerThrow Settings")]
    [SerializeField] private int powerThrowDamage = 35;
    [SerializeField] private float powerThrowSpeed = 30f;
    [SerializeField] private float powerThrowKnockback = 12f;
    [SerializeField] private float powerThrowScreenShake = 1.2f;
    [SerializeField] private GameObject powerThrowEffect;
    [SerializeField] private AudioClip powerThrowSound;

    [Header("Ultimate: MultiThrow Settings")]
    [SerializeField] private int multiThrowCount = 4;
    [SerializeField] private int multiThrowDamagePerBall = 12;
    [SerializeField] private float multiThrowSpeed = 22f;
    [SerializeField] private float multiThrowSpread = 20f; // degrees
    [SerializeField] private float multiThrowDelay = 0.15f; // seconds between balls
    [SerializeField] private Vector3 multiThrowSpawnOffset = new Vector3(0.8f, 0.5f, 0f); // spawn position offset
    [SerializeField] private GameObject multiThrowEffect;
    [SerializeField] private AudioClip multiThrowSound;

    [Header("Ultimate: Curveball Settings")]
    [SerializeField] private int curveballDamage = 25;
    [SerializeField] private float curveballSpeed = 20f;
    [SerializeField] private float curveballAmplitude = 3f; // Y-axis curve height
    [SerializeField] private float curveballFrequency = 4f; // How fast it oscillates
    [SerializeField] private float curveballDuration = 2f; // How long curve lasts
    [SerializeField] private GameObject curveballEffect;
    [SerializeField] private AudioClip curveballSound;

    [Header("Ultimate Charge")]
    [Range(50f, 200f)]
    public float ultimateChargeRequired = 100f;
    [Range(0.5f, 3f)]
    public float ultimateChargeRate = 1f;

    [Header("Trick Ability (Opponent-Focused)")]
    public TrickType trickType = TrickType.SlowSpeed;

    [Header("Trick: Slow Speed Settings")]
    [SerializeField] private float slowSpeedMultiplier = 0.3f; // 30% of normal speed
    [SerializeField] private float slowSpeedDuration = 4f;
    [SerializeField] private GameObject slowSpeedEffect;
    [SerializeField] private AudioClip slowSpeedSound;

    [Header("Trick: Freeze Settings")]
    [SerializeField] private float freezeDuration = 2.5f;
    [SerializeField] private GameObject freezeEffect;
    [SerializeField] private AudioClip freezeSound;

    [Header("Trick: Instant Damage Settings")]
    [SerializeField] private int instantDamageAmount = 15;
    [SerializeField] private GameObject instantDamageEffect;
    [SerializeField] private AudioClip instantDamageSound;

    [Header("Trick Charge")]
    [Range(30f, 100f)]
    public float trickChargeRequired = 60f;
    [Range(0.5f, 3f)]
    public float trickChargeRate = 1f;

    [Header("Treat Ability (Self-Focused)")]
    public TreatType treatType = TreatType.Shield;

    [Header("Treat: Shield Settings")]
    [SerializeField] private float shieldDuration = 4f;
    [SerializeField] private GameObject shieldEffect;
    [SerializeField] private AudioClip shieldSound;

    [Header("Treat: Teleport Settings")]
    [SerializeField] private float teleportRange = 8f;
    [SerializeField] private GameObject teleportEffect;
    [SerializeField] private AudioClip teleportSound;

    [Header("Treat: Speed Boost Settings")]
    [SerializeField] private float speedBoostMultiplier = 2.5f;
    [SerializeField] private float speedBoostDuration = 5f;
    [SerializeField] private GameObject speedBoostEffect;
    [SerializeField] private AudioClip speedBoostSound;

    [Header("Treat Charge")]
    [Range(30f, 100f)]
    public float treatChargeRequired = 60f;
    [Range(0.5f, 3f)]
    public float treatChargeRate = 1f;

    // ═══════════════════════════════════════════════════════════════
    // PROPERTY GETTERS FOR CLEAN ACCESS
    // ═══════════════════════════════════════════════════════════════

    #region Basic Properties
    public float GetDashDistance() => dashDistance;
    public float GetDashCooldown() => dashCooldown;
    public float GetDashDuration() => dashDuration;

    public int GetThrowDamage(ThrowType throwType)
    {
        switch (throwType)
        {
            case ThrowType.Normal:
                return normalThrowDamage;
            case ThrowType.JumpThrow:
                return jumpThrowDamage;
            default:
                return normalThrowDamage;
        }
    }

    public float GetThrowSpeed(float baseSpeed)
    {
        return baseSpeed * throwSpeedMultiplier;
    }

    public Vector3 ApplyThrowAccuracy(Vector3 targetDirection)
    {
        if (throwAccuracy >= 1f) return targetDirection;

        float inaccuracy = 1f - throwAccuracy;
        Vector3 randomOffset = new Vector3(
            Random.Range(-inaccuracy, inaccuracy),
            Random.Range(-inaccuracy * 0.5f, inaccuracy * 0.5f),
            Random.Range(-inaccuracy, inaccuracy)
        );

        return (targetDirection + randomOffset * 0.2f).normalized;
    }

    public AudioClip GetRandomAudioClip(CharacterAudioType audioType)
    {
        switch (audioType)
        {
            case CharacterAudioType.Throw:
                return throwSounds?.Length > 0 ? throwSounds[Random.Range(0, throwSounds.Length)] : null;
            case CharacterAudioType.Jump:
                return jumpSounds?.Length > 0 ? jumpSounds[Random.Range(0, jumpSounds.Length)] : null;
            case CharacterAudioType.Dash:
                return dashSound;
            default:
                return null;
        }
    }
    #endregion

    #region Ultimate Ability Properties
    public int GetUltimateDamage()
    {
        switch (ultimateType)
        {
            case UltimateType.PowerThrow:
                return powerThrowDamage;
            case UltimateType.MultiThrow:
                return multiThrowDamagePerBall;
            case UltimateType.Curveball:
                return curveballDamage;
            default:
                return 30;
        }
    }

    public float GetUltimateSpeed()
    {
        switch (ultimateType)
        {
            case UltimateType.PowerThrow:
                return powerThrowSpeed;
            case UltimateType.MultiThrow:
                return multiThrowSpeed;
            case UltimateType.Curveball:
                return curveballSpeed;
            default:
                return 25f;
        }
    }

    public GameObject GetUltimateEffect()
    {
        switch (ultimateType)
        {
            case UltimateType.PowerThrow:
                return powerThrowEffect;
            case UltimateType.MultiThrow:
                return multiThrowEffect;
            case UltimateType.Curveball:
                return curveballEffect;
            default:
                return null;
        }
    }

    public AudioClip GetUltimateSound()
    {
        switch (ultimateType)
        {
            case UltimateType.PowerThrow:
                return powerThrowSound;
            case UltimateType.MultiThrow:
                return multiThrowSound;
            case UltimateType.Curveball:
                return curveballSound;
            default:
                return null;
        }
    }

    // PowerThrow specific
    public float GetPowerThrowKnockback() => powerThrowKnockback;
    public float GetPowerThrowScreenShake() => powerThrowScreenShake;

    // MultiThrow specific
    public int GetMultiThrowCount() => multiThrowCount;
    public float GetMultiThrowSpread() => multiThrowSpread;
    public float GetMultiThrowDelay() => multiThrowDelay;
    public Vector3 GetMultiThrowSpawnOffset() => multiThrowSpawnOffset;

    // Curveball specific
    public float GetCurveballAmplitude() => curveballAmplitude;
    public float GetCurveballFrequency() => curveballFrequency;
    public float GetCurveballDuration() => curveballDuration;
    #endregion

    #region Trick Ability Properties
    public GameObject GetTrickEffect()
    {
        switch (trickType)
        {
            case TrickType.SlowSpeed:
                return slowSpeedEffect;
            case TrickType.Freeze:
                return freezeEffect;
            case TrickType.InstantDamage:
                return instantDamageEffect;
            default:
                return null;
        }
    }

    public AudioClip GetTrickSound()
    {
        switch (trickType)
        {
            case TrickType.SlowSpeed:
                return slowSpeedSound;
            case TrickType.Freeze:
                return freezeSound;
            case TrickType.InstantDamage:
                return instantDamageSound;
            default:
                return null;
        }
    }

    // SlowSpeed specific
    public float GetSlowSpeedMultiplier() => slowSpeedMultiplier;
    public float GetSlowSpeedDuration() => slowSpeedDuration;

    // Freeze specific
    public float GetFreezeDuration() => freezeDuration;

    // InstantDamage specific
    public int GetInstantDamageAmount() => instantDamageAmount;
    #endregion

    #region Treat Ability Properties
    public GameObject GetTreatEffect()
    {
        switch (treatType)
        {
            case TreatType.Shield:
                return shieldEffect;
            case TreatType.Teleport:
                return teleportEffect;
            case TreatType.SpeedBoost:
                return speedBoostEffect;
            default:
                return null;
        }
    }

    public AudioClip GetTreatSound()
    {
        switch (treatType)
        {
            case TreatType.Shield:
                return shieldSound;
            case TreatType.Teleport:
                return teleportSound;
            case TreatType.SpeedBoost:
                return speedBoostSound;
            default:
                return null;
        }
    }

    // Shield specific
    public float GetShieldDuration() => shieldDuration;

    // Teleport specific
    public float GetTeleportRange() => teleportRange;

    // SpeedBoost specific
    public float GetSpeedBoostMultiplier() => speedBoostMultiplier;
    public float GetSpeedBoostDuration() => speedBoostDuration;
    #endregion
}

// ═══════════════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════════════

public enum ThrowType
{
    Normal,
    JumpThrow,
    Ultimate
}

public enum UltimateType
{
    PowerThrow,    // Heavy straight shot, high speed, knockback + screen shake
    MultiThrow,    // 3-5 rapid-fire balls in spread pattern
    Curveball      // Curves up/down on Y-axis unpredictably
}

public enum TrickType
{
    SlowSpeed,     // Reduce opponent movement speed
    Freeze,        // Temporarily immobilize opponent
    InstantDamage  // Quick unavoidable chip damage
}

public enum TreatType
{
    Shield,        // Temporary invulnerability
    Teleport,      // Strategic repositioning
    SpeedBoost     // Enhanced movement speed
}

public enum CharacterAudioType
{
    Throw,
    Jump,
    Dash
}