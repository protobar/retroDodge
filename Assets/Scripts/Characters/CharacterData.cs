using UnityEngine;

/// <summary>
/// Simplified CharacterData with focused VFX system
/// Arrays only for throws and hits, specific effects for abilities
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "Retro Dodge/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Identity")]
    public string characterName = "Unknown Fighter";
    public string characterDescription = "A mysterious dodgeball warrior";
    public string characterTagline = "The Ultimate Fighter";
    public Sprite characterIcon;
    public GameObject characterPrefab;
    
    [Header("Character Lore")]
    [TextArea(3, 5)]
    public string characterLore = "A legendary warrior with unique abilities...";

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

    [Header("Character Color & Basic Effects")]
    public Color characterColor = Color.white;
    public GameObject dashEffect;

    [Header("VFX Position Offsets (Ultimate/Trick/Treat)")]
    [SerializeField] private Vector3 ultimateActivationOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 trickEffectOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 treatEffectOffset = new Vector3(0f, 1.5f, 0f);

    [Header("═══════════════════════════════════")]
    [Header("SIMPLIFIED VFX SYSTEM")]
    [Header("═══════════════════════════════════")]

    [Header("Throw VFX - Character Specific (Arrays for Variety)")]
    [SerializeField] private GameObject[] normalThrowVFX = new GameObject[3]; // Each character has unique throw effects
    [SerializeField] private GameObject[] jumpThrowVFX = new GameObject[2]; // Each character has unique jump throw effects

    [Header("Hit VFX - Shared (Arrays for Variety)")]
    [Tooltip("Same hit effects for all characters - just variety")]
    [SerializeField] private GameObject[] normalHitVFX = new GameObject[3]; // Shared: thump, pow, boom
    [SerializeField] private GameObject[] jumpThrowHitVFX = new GameObject[2]; // Shared: stronger impacts
    [SerializeField] private GameObject[] ultimateHitVFX = new GameObject[2]; // Shared: massive impacts

    [Header("═══════════════════════════════════")]
    [Header("ULTIMATE ABILITY")]
    [Header("═══════════════════════════════════")]

    [Header("Ultimate Settings")]
    public UltimateType ultimateType = UltimateType.PowerThrow;

    [Header("Ultimate VFX - Specific to Character")]
    [SerializeField] private GameObject ultimateActivationVFX; // Player activation effect (e.g., Fire Aura for Grudge)
    [SerializeField] private GameObject ultimateBallVFX; // Ball effect during flight (e.g., Fire Ball for Grudge)
    [SerializeField] private GameObject ultimateImpactVFX; // Impact effect when ult hits (e.g., Fire Explosion)

    [Header("Ultimate Audio")]
    [SerializeField] private AudioClip ultimateActivationSound; // "You are dead!" etc.
    [SerializeField] private AudioClip ultimateImpactSound; // Impact sound

    [Header("Ultimate: PowerThrow Settings")]
    [SerializeField] private int powerThrowDamage = 35;
    [SerializeField] private float powerThrowSpeed = 30f;
    [SerializeField] private float powerThrowKnockback = 12f;
    [SerializeField] private float powerThrowScreenShake = 1.2f;
    
    [Header("Ultimate Description")]
    [TextArea(2, 3)]
    public string ultimateDescription = "A powerful throw that deals massive damage and knockback.";

    [Header("Ultimate: MultiThrow Settings")]
    [SerializeField] private int multiThrowCount = 4;
    [SerializeField] private int multiThrowDamagePerBall = 12;
    [SerializeField] private float multiThrowSpeed = 22f;
    [SerializeField] private float multiThrowSpread = 20f;
    [SerializeField] private float multiThrowDelay = 0.15f;
    [SerializeField] private Vector3 multiThrowSpawnOffset = new Vector3(0.8f, 0.5f, 0f);

    [Header("Ultimate: Curveball Settings")]
    [SerializeField] private int curveballDamage = 25;
    [SerializeField] private float curveballSpeed = 20f;
    [SerializeField] private float curveballAmplitude = 3f;
    [SerializeField] private float curveballFrequency = 4f;
    [SerializeField] private float curveballDuration = 2f;

    [Header("Ultimate Charge")]
    [Range(50f, 200f)]
    public float ultimateChargeRequired = 100f;
    [Range(0.5f, 3f)]
    public float ultimateChargeRate = 1f;

    [Header("═══════════════════════════════════")]
    [Header("TRICK ABILITY (Opponent-Focused)")]
    [Header("═══════════════════════════════════")]

    [Header("Trick Settings")]
    public TrickType trickType = TrickType.SlowSpeed;

    [Header("Trick VFX - Spawns on Opponent Only")]
    [SerializeField] private GameObject trickEffectVFX; // Effect that spawns on opponent (damage, slow, freeze effect)

    [Header("Trick Audio")]
    [SerializeField] private AudioClip trickActivationSound; // Sound when trick is used
    
    [Header("Trick Description")]
    [TextArea(2, 3)]
    public string trickDescription = "A cunning ability that affects your opponent.";

    [Header("Trick: Slow Speed Settings")]
    [SerializeField] private float slowSpeedMultiplier = 0.3f;
    [SerializeField] private float slowSpeedDuration = 4f;

    [Header("Trick: Freeze Settings")]
    [SerializeField] private float freezeDuration = 2.5f;

    [Header("Trick: Instant Damage Settings")]
    [SerializeField] private int instantDamageAmount = 15;

    [Header("Trick Charge")]
    [Range(30f, 100f)]
    public float trickChargeRequired = 60f;
    [Range(0.5f, 3f)]
    public float trickChargeRate = 1f;

    [Header("═══════════════════════════════════")]
    [Header("TREAT ABILITY (Self-Focused)")]
    [Header("═══════════════════════════════════")]

    [Header("Treat Settings")]
    public TreatType treatType = TreatType.Shield;

    [Header("Treat VFX - Spawns on Self Only")]
    [SerializeField] private GameObject treatEffectVFX; // Effect that spawns on self (shield, speed boost, teleport effect)

    [Header("Treat Audio")]
    [SerializeField] private AudioClip treatActivationSound; // Sound when treat is used
    
    [Header("Treat Description")]
    [TextArea(2, 3)]
    public string treatDescription = "A beneficial ability that helps you in battle.";

    [Header("Treat: Shield Settings")]
    [SerializeField] private float shieldDuration = 4f;

    [Header("Treat: Teleport Settings")]
    [SerializeField] private float teleportRange = 8f;

    [Header("Treat: Speed Boost Settings")]
    [SerializeField] private float speedBoostMultiplier = 2.5f;
    [SerializeField] private float speedBoostDuration = 5f;

    [Header("Treat Charge")]
    [Range(30f, 100f)]
    public float treatChargeRequired = 60f;
    [Range(0.5f, 3f)]
    public float treatChargeRate = 1f;

    [Header("═══════════════════════════════════")]
    [Header("ENHANCED AUDIO SYSTEM")]
    [Header("═══════════════════════════════════")]

    [Header("Player Audio Arrays")]
    [Tooltip("Multiple jump sounds for variety")]
    public AudioClip[] jumpSounds;
    
    [Tooltip("Multiple dash sounds for variety")]
    public AudioClip[] dashSounds;
    
    [Tooltip("Multiple throw sounds for variety")]
    public AudioClip[] throwSounds;
    
    [Tooltip("Multiple footstep sounds for variety")]
    public AudioClip[] footstepSounds;
    
    [Tooltip("Multiple hurt sounds for variety")]
    public AudioClip[] hurtSounds;
    
    [Tooltip("Multiple death sounds for variety")]
    public AudioClip[] deathSounds;

    // ═══════════════════════════════════════════════════════════════
    // SIMPLIFIED VFX GETTER METHODS
    // ═══════════════════════════════════════════════════════════════

    #region Throw VFX (Character Specific)
    public GameObject GetRandomNormalThrowVFX()
    {
        return GetRandomFromArray(normalThrowVFX);
    }

    public GameObject GetRandomJumpThrowVFX()
    {
        return GetRandomFromArray(jumpThrowVFX);
    }
    #endregion

    #region Hit VFX (Shared Between Characters)
    public GameObject GetRandomNormalHitVFX()
    {
        return GetRandomFromArray(normalHitVFX);
    }

    public GameObject GetRandomJumpThrowHitVFX()
    {
        return GetRandomFromArray(jumpThrowHitVFX);
    }

    public GameObject GetRandomUltimateHitVFX()
    {
        return GetRandomFromArray(ultimateHitVFX);
    }
    #endregion

    #region Ultimate VFX (Character Specific)
    public GameObject GetUltimateActivationVFX()
    {
        return ultimateActivationVFX;
    }

    public GameObject GetUltimateBallVFX()
    {
        return ultimateBallVFX;
    }

    public GameObject GetUltimateImpactVFX()
    {
        return ultimateImpactVFX;
    }

    public AudioClip GetUltimateActivationSound()
    {
        return ultimateActivationSound;
    }

    public AudioClip GetUltimateImpactSound()
    {
        return ultimateImpactSound;
    }
    #endregion

    #region Trick VFX (Single Effect on Opponent)
    public GameObject GetTrickEffectVFX()
    {
        return trickEffectVFX;
    }

    public AudioClip GetTrickActivationSound()
    {
        return trickActivationSound;
    }
    #endregion

    #region Treat VFX (Single Effect on Self)
    public GameObject GetTreatEffectVFX()
    {
        return treatEffectVFX;
    }

    public AudioClip GetTreatActivationSound()
    {
        return treatActivationSound;
    }
    #endregion

    // ═══════════════════════════════════════════════════════════════
    // EXISTING PROPERTY GETTERS (Keep for compatibility)
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
                return GetRandomFromAudioArray(throwSounds);
            case CharacterAudioType.Jump:
                return GetRandomFromAudioArray(jumpSounds);
            case CharacterAudioType.Dash:
                return GetRandomFromAudioArray(dashSounds);
            case CharacterAudioType.Footstep:
                return GetRandomFromAudioArray(footstepSounds);
            case CharacterAudioType.Hurt:
                return GetRandomFromAudioArray(hurtSounds);
            case CharacterAudioType.Death:
                return GetRandomFromAudioArray(deathSounds);
            default:
                return null;
        }
    }

    /// <summary>
    /// Get random audio clip from array with null safety
    /// </summary>
    private AudioClip GetRandomFromAudioArray(AudioClip[] audioArray)
    {
        if (audioArray == null || audioArray.Length == 0) return null;
        
        // Filter out null entries
        var validClips = new System.Collections.Generic.List<AudioClip>();
        foreach (var clip in audioArray)
        {
            if (clip != null) validClips.Add(clip);
        }
        
        if (validClips.Count == 0) return null;
        
        return validClips[Random.Range(0, validClips.Count)];
    }
    #endregion

    #region Ultimate Properties
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

    #region Trick Properties
    // SlowSpeed specific
    public float GetSlowSpeedMultiplier() => slowSpeedMultiplier;
    public float GetSlowSpeedDuration() => slowSpeedDuration;

    // Freeze specific
    public float GetFreezeDuration() => freezeDuration;

    // InstantDamage specific
    public int GetInstantDamageAmount() => instantDamageAmount;
    #endregion

    #region Treat Properties
    // Shield specific
    public float GetShieldDuration() => shieldDuration;

    // Teleport specific
    public float GetTeleportRange() => teleportRange;

    // SpeedBoost specific
    public float GetSpeedBoostMultiplier() => speedBoostMultiplier;
    public float GetSpeedBoostDuration() => speedBoostDuration;

    //Abilities VFX Spawn Positions
    public Vector3 GetUltimateActivationOffset() => ultimateActivationOffset;
    public Vector3 GetTrickEffectOffset() => trickEffectOffset;
    public Vector3 GetTreatEffectOffset() => treatEffectOffset;
    #endregion

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get random element from array, with null safety
    /// </summary>
    private T GetRandomFromArray<T>(T[] array) where T : Object
    {
        if (array == null || array.Length == 0) return null;

        // Filter out null entries
        var validEntries = new System.Collections.Generic.List<T>();
        foreach (var item in array)
        {
            if (item != null) validEntries.Add(item);
        }

        if (validEntries.Count == 0) return null;

        return validEntries[Random.Range(0, validEntries.Count)];
    }
}

// ═══════════════════════════════════════════════════════════════
// ENUMS (Keep existing)
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
    Dash,
    Footstep,
    Hurt,
    Death
}