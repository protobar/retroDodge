using UnityEngine;

/// <summary>
/// ScriptableObject that defines all character stats and abilities
/// Create new characters by making new instances of this asset
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "Retro Dodge/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Identity")]
    public string characterName = "Unknown Fighter";
    public string characterDescription = "A mysterious dodgeball warrior";
    public Sprite characterIcon;
    public GameObject characterPrefab; // Visual representation

    [Header("Movement Stats")]
    [Range(1f, 10f)]
    public float moveSpeed = 5f;

    [Range(5f, 20f)]
    public float jumpHeight = 12f;

    public bool canDoubleJump = false;
    public bool canDash = false;

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private float dashDuration = 0.2f;

    [Header("Health Stats")]
    [Range(50, 200)]
    public int maxHealth = 100;

    [Range(0.5f, 2f)]
    public float damageResistance = 1f; // 1 = normal, 0.5 = takes half damage, 2 = takes double

    [Header("Throw Damage")]
    [Range(5, 25)]
    public int normalThrowDamage = 10;

    [Range(8, 35)]
    public int jumpThrowDamage = 12;

    [Range(15, 50)]
    public int ultimateThrowDamage = 25;

    [Header("Throw Properties")]
    [Range(0.5f, 2f)]
    public float throwSpeedMultiplier = 1f;

    [Range(0f, 1f)]
    public float throwAccuracy = 1f; // 1 = perfect accuracy, 0 = very inaccurate

    public ThrowType specialThrowType = ThrowType.Normal;

    [Header("Ultimate Ability")]
    public UltimateAbilityType ultimateType = UltimateAbilityType.PowerThrow;

    [Range(50f, 200f)]
    public float ultimateChargeRequired = 100f;

    [Range(0.5f, 3f)]
    public float ultimateChargeRate = 1f; // Multiplier for gaining ultimate charge

    [Header("Special Abilities")]
    public bool hasWallJump = false;
    public bool hasAirDash = false;
    public bool hasQuickThrow = false; // Throws faster than normal

    [Header("Visual Effects")]
    public Color characterColor = Color.white;
    public GameObject ultimateEffect;
    public GameObject throwEffect;
    public GameObject dashEffect;

    [Header("Audio")]
    public AudioClip[] throwSounds;
    public AudioClip[] jumpSounds;
    public AudioClip ultimateSound;
    public AudioClip dashSound;

    // Properties for easy access
    public float GetDashDistance() => dashDistance;
    public float GetDashCooldown() => dashCooldown;
    public float GetDashDuration() => dashDuration;

    /// <summary>
    /// Get damage value based on throw type
    /// </summary>
    public int GetThrowDamage(ThrowType throwType)
    {
        switch (throwType)
        {
            case ThrowType.Normal:
                return normalThrowDamage;
            case ThrowType.JumpThrow:
                return jumpThrowDamage;
            case ThrowType.Ultimate:
                return ultimateThrowDamage;
            default:
                return normalThrowDamage;
        }
    }

    /// <summary>
    /// Calculate final damage after applying character multipliers
    /// </summary>
    public int GetModifiedDamage(ThrowType throwType, float powerMultiplier = 1f)
    {
        int baseDamage = GetThrowDamage(throwType);
        float finalDamage = baseDamage * powerMultiplier;

        // Apply special throw modifications
        if (specialThrowType == ThrowType.PowerThrow && throwType == ThrowType.Normal)
        {
            finalDamage *= 1.2f; // 20% bonus for power throw characters
        }

        return Mathf.RoundToInt(finalDamage);
    }

    /// <summary>
    /// Get throw speed based on character stats
    /// </summary>
    public float GetThrowSpeed(float baseSpeed)
    {
        return baseSpeed * throwSpeedMultiplier;
    }

    /// <summary>
    /// Apply accuracy modifier to throw direction
    /// </summary>
    public Vector3 ApplyThrowAccuracy(Vector3 targetDirection)
    {
        if (throwAccuracy >= 1f) return targetDirection;

        // Add random offset based on accuracy
        float inaccuracy = 1f - throwAccuracy;
        Vector3 randomOffset = new Vector3(
            Random.Range(-inaccuracy, inaccuracy),
            Random.Range(-inaccuracy * 0.5f, inaccuracy * 0.5f), // Less Y variation
            Random.Range(-inaccuracy, inaccuracy)
        );

        return (targetDirection + randomOffset * 0.2f).normalized;
    }

    /// <summary>
    /// Check if character can perform a specific ability
    /// </summary>
    public bool CanPerformAbility(CharacterAbility ability)
    {
        switch (ability)
        {
            case CharacterAbility.DoubleJump:
                return canDoubleJump;
            case CharacterAbility.Dash:
                return canDash;
            case CharacterAbility.WallJump:
                return hasWallJump;
            case CharacterAbility.AirDash:
                return hasAirDash;
            case CharacterAbility.QuickThrow:
                return hasQuickThrow;
            default:
                return false;
        }
    }

    /// <summary>
    /// Get random audio clip for specific action
    /// </summary>
    public AudioClip GetRandomAudioClip(CharacterAudioType audioType)
    {
        switch (audioType)
        {
            case CharacterAudioType.Throw:
                return throwSounds?.Length > 0 ? throwSounds[Random.Range(0, throwSounds.Length)] : null;
            case CharacterAudioType.Jump:
                return jumpSounds?.Length > 0 ? jumpSounds[Random.Range(0, jumpSounds.Length)] : null;
            case CharacterAudioType.Ultimate:
                return ultimateSound;
            case CharacterAudioType.Dash:
                return dashSound;
            default:
                return null;
        }
    }
}

/// <summary>
/// Types of throws available
/// </summary>
public enum ThrowType
{
    Normal,
    JumpThrow,
    Ultimate,
    PowerThrow,
    CurveThrow,
    MultiThrow
}

/// <summary>
/// Types of ultimate abilities
/// </summary>
public enum UltimateAbilityType
{
    PowerThrow,     // Massive damage single throw
    MultiThrow,     // Throw multiple balls
    GravitySlam,    // Ball curves downward dramatically  
    HomingBall,     // Ball tracks target
    ExplosiveBall,  // Ball explodes on impact
    TimeFreeze,     // Freeze opponent briefly
    Shield,         // Temporary invincibility
    SpeedBoost,     // Temporary speed increase
    Teleport,       // Instant movement
    Curveball       // Ball curves around obstacles
}

/// <summary>
/// Character abilities for capability checking
/// </summary>
public enum CharacterAbility
{
    DoubleJump,
    Dash,
    WallJump,
    AirDash,
    QuickThrow,
    UltimateThrow
}

/// <summary>
/// Audio types for character sounds
/// </summary>
public enum CharacterAudioType
{
    Throw,
    Jump,
    Ultimate,
    Dash
}