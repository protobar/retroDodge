using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Simplified VFXManager for Retro Dodge Rumble
/// Focused system with specific VFX flows for each ability type
/// </summary>
public class VFXManager : MonoBehaviour
{
    private static VFXManager instance;
    public static VFXManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("VFXManager");
                instance = go.AddComponent<VFXManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("VFX Settings")]
    [SerializeField] private int maxSimultaneousVFX = 30;
    [SerializeField] private float defaultVFXLifetime = 3f;
    [SerializeField] private bool debugMode = false;

    [Header("Default Fallback VFX")]
    [SerializeField] private GameObject defaultHitVFX;
    [SerializeField] private GameObject defaultThrowVFX;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource vfxAudioSource;
    [SerializeField] private float globalVFXVolume = 0.7f;

    // VFX tracking
    private Queue<GameObject> activeVFX = new Queue<GameObject>();

    // Performance counters
    private int currentHitVFX = 0;
    private int currentUltimateVFX = 0;
    private int currentAbilityVFX = 0;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeVFXManager();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void InitializeVFXManager()
    {
        // Setup audio source
        if (vfxAudioSource == null)
        {
            vfxAudioSource = gameObject.AddComponent<AudioSource>();
            vfxAudioSource.playOnAwake = false;
            vfxAudioSource.volume = globalVFXVolume;
        }

        if (debugMode)
        {
            Debug.Log("Simplified VFXManager initialized!");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // THROW VFX (Character-Specific Arrays)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawn character-specific throw VFX when player throws
    /// </summary>
    public void SpawnThrowVFX(Vector3 position, PlayerCharacter thrower, ThrowType throwType)
    {
        if (thrower == null) return;

        CharacterData characterData = thrower.GetCharacterData();
        if (characterData == null) return;

        GameObject vfxPrefab = null;

        switch (throwType)
        {
            case ThrowType.Normal:
                vfxPrefab = characterData.GetRandomNormalThrowVFX();
                break;
            case ThrowType.JumpThrow:
                vfxPrefab = characterData.GetRandomJumpThrowVFX();
                break;
            case ThrowType.Ultimate:
                // Ultimate throw handled separately
                return;
        }

        // Fallback
        if (vfxPrefab == null)
        {
            vfxPrefab = defaultThrowVFX;
        }

        if (vfxPrefab != null)
        {
            SpawnVFX(vfxPrefab, position, Quaternion.identity, 2f);
        }

        if (debugMode)
        {
            Debug.Log($"Spawned {characterData.characterName} {throwType} throw VFX");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HIT VFX (Shared Arrays for Variety)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawn shared hit VFX based on damage type - same for all characters
    /// </summary>
    public void SpawnHitVFX(Vector3 position, PlayerCharacter hitCharacter, ThrowType damageType)
    {
        if (hitCharacter == null) return;

        CharacterData characterData = hitCharacter.GetCharacterData();
        if (characterData == null) return;

        GameObject vfxPrefab = null;

        // Get shared hit VFX from any character (they're all the same)
        switch (damageType)
        {
            case ThrowType.Normal:
                vfxPrefab = characterData.GetRandomNormalHitVFX();
                break;
            case ThrowType.JumpThrow:
                vfxPrefab = characterData.GetRandomJumpThrowHitVFX();
                break;
            case ThrowType.Ultimate:
                vfxPrefab = characterData.GetRandomUltimateHitVFX();
                break;
        }

        // Fallback
        if (vfxPrefab == null)
        {
            vfxPrefab = defaultHitVFX;
        }

        if (vfxPrefab != null)
        {
            GameObject vfx = SpawnVFX(vfxPrefab, position, Quaternion.identity, defaultVFXLifetime);

            // Scale VFX based on damage type
            if (vfx != null)
            {
                float scale = damageType switch
                {
                    ThrowType.Ultimate => 1.5f,
                    ThrowType.JumpThrow => 1.2f,
                    _ => 1f
                };
                vfx.transform.localScale *= scale;
            }

            currentHitVFX++;
        }

        if (debugMode)
        {
            Debug.Log($"Spawned {damageType} hit VFX on {hitCharacter.name}");
        }
    }

    /// <summary>
    /// Overload for SpawnHitVFX with thrower parameter (for ultimate impacts)
    /// </summary>
    public void SpawnHitVFX(Vector3 position, PlayerCharacter hitCharacter, ThrowType damageType, PlayerCharacter thrower)
    {
        // For ultimate hits, also spawn ultimate impact VFX
        if (damageType == ThrowType.Ultimate && thrower != null)
        {
            SpawnUltimateImpactVFX(position, thrower, hitCharacter);
        }

        // Then spawn regular hit VFX
        SpawnHitVFX(position, hitCharacter, damageType);
    }

    // ═══════════════════════════════════════════════════════════════
    // ULTIMATE VFX SYSTEM (3-Stage Process)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Stage 1: Player activates ultimate - spawn activation VFX + sound
    /// </summary>
    public void SpawnUltimateActivationVFX(Vector3 basePosition, PlayerCharacter caster)
    {
        if (caster == null) return;

        CharacterData characterData = caster.GetCharacterData();
        if (characterData == null) return;

        // Apply custom offset for this character's ultimate
        Vector3 spawnPosition = basePosition + characterData.GetUltimateActivationOffset();

        // 1. Spawn activation VFX on player (e.g., Fire Aura for Grudge)
        GameObject activationVFX = characterData.GetUltimateActivationVFX();
        if (activationVFX != null)
        {
            GameObject vfx = SpawnVFX(activationVFX, spawnPosition, Quaternion.identity, 2f);
            if (vfx != null)
            {
                vfx.transform.localScale *= 1.3f; // Make it prominent
            }
        }

        // 2. Play activation sound ("You are dead!" etc.)
        AudioClip activationSound = characterData.GetUltimateActivationSound();
        if (activationSound != null)
        {
            PlayVFXSound(activationSound, 1.2f); // Louder for ultimate
        }

        currentUltimateVFX++;

        if (debugMode)
        {
            Debug.Log($"💥 {caster.name} ultimate activated: {characterData.ultimateType} at offset {characterData.GetUltimateActivationOffset()}");
        }
    }

    

    /// <summary>
    /// Stage 3: Ultimate impact when ball hits opponent
    /// </summary>
    public void SpawnUltimateImpactVFX(Vector3 position, PlayerCharacter caster, PlayerCharacter target)
    {
        if (caster == null) return;

        CharacterData casterData = caster.GetCharacterData();
        if (casterData == null) return;

        // 1. Spawn massive impact VFX
        GameObject impactVFX = casterData.GetUltimateImpactVFX();
        if (impactVFX != null)
        {
            GameObject vfx = SpawnVFX(impactVFX, position, Quaternion.identity, 4f);
            if (vfx != null)
            {
                vfx.transform.localScale *= 2f; // Massive impact effect
            }
        }

        // 2. Play impact sound
        AudioClip impactSound = casterData.GetUltimateImpactSound();
        if (impactSound != null)
        {
            PlayVFXSound(impactSound, 1.5f); // Very loud for impact
        }

        if (debugMode)
        {
            Debug.Log($"💥 {caster.name} ultimate IMPACT on {target?.name}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TRICK VFX (Simple: Sound + Effect on Opponent)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Trick activation: Play sound + spawn effect on opponent
    /// </summary>
    public void SpawnTrickVFX(Vector3 baseOpponentPosition, PlayerCharacter caster, PlayerCharacter target)
    {
        if (caster == null) return;

        CharacterData characterData = caster.GetCharacterData();
        if (characterData == null) return;

        // Apply custom offset for this character's trick
        Vector3 spawnPosition = baseOpponentPosition + characterData.GetTrickEffectOffset();

        // 1. Play trick sound
        AudioClip trickSound = characterData.GetTrickActivationSound();
        if (trickSound != null)
        {
            PlayVFXSound(trickSound);
        }

        // 2. Spawn effect on opponent (damage, slow, freeze effect)
        GameObject trickVFX = characterData.GetTrickEffectVFX();
        if (trickVFX != null)
        {
            float duration = GetTrickDuration(characterData.trickType, characterData);
            GameObject vfx = SpawnVFX(trickVFX, spawnPosition, Quaternion.identity, duration);

            // Make effect follow target for duration
            if (vfx != null && target != null)
            {
                StartCoroutine(AttachVFXToTarget(vfx, target.transform, duration, characterData.GetTrickEffectOffset()));
            }
        }

        currentAbilityVFX++;

        if (debugMode)
        {
            Debug.Log($"🎯 {caster.name} used {characterData.trickType} on {target?.name} at offset {characterData.GetTrickEffectOffset()}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TREAT VFX (Simple: Sound + Effect on Self)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Treat activation: Play sound + spawn effect on self
    /// </summary>
    public void SpawnTreatVFX(Vector3 baseSelfPosition, PlayerCharacter caster)
    {
        if (caster == null) return;

        CharacterData characterData = caster.GetCharacterData();
        if (characterData == null) return;

        // Apply custom offset for this character's treat
        Vector3 spawnPosition = baseSelfPosition + characterData.GetTreatEffectOffset();

        // 1. Play treat sound
        AudioClip treatSound = characterData.GetTreatActivationSound();
        if (treatSound != null)
        {
            PlayVFXSound(treatSound);
        }

        // 2. Spawn effect on self (shield, speed boost, teleport effect)
        GameObject treatVFX = characterData.GetTreatEffectVFX();
        if (treatVFX != null)
        {
            float duration = GetTreatDuration(characterData.treatType, characterData);
            GameObject vfx = SpawnVFX(treatVFX, spawnPosition, Quaternion.identity, duration);

            // Make effect follow caster for duration
            if (vfx != null)
            {
                StartCoroutine(AttachVFXToTarget(vfx, caster.transform, duration, characterData.GetTreatEffectOffset()));
            }
        }

        currentAbilityVFX++;

        if (debugMode)
        {
            Debug.Log($"✨ {caster.name} used {characterData.treatType} on self at offset {characterData.GetTreatEffectOffset()}");
        }
    }

    /// <summary>
    /// Special teleport VFX with departure and arrival effects
    /// </summary>
    public void SpawnTeleportVFX(Vector3 departurePos, Vector3 arrivalPos, PlayerCharacter caster)
    {
        if (caster == null) return;

        CharacterData characterData = caster.GetCharacterData();
        if (characterData == null) return;

        // Play teleport sound
        AudioClip teleportSound = characterData.GetTreatActivationSound();
        if (teleportSound != null)
        {
            PlayVFXSound(teleportSound);
        }

        // Spawn departure VFX
        GameObject teleportVFX = characterData.GetTreatEffectVFX();
        if (teleportVFX != null)
        {
            SpawnVFX(teleportVFX, departurePos, Quaternion.identity, 1.5f);

            // Spawn arrival VFX with delay
            StartCoroutine(DelayedTeleportArrivalVFX(arrivalPos, teleportVFX, 0.5f));
        }

        if (debugMode)
        {
            Debug.Log($"🌀 {caster.name} teleported from {departurePos} to {arrivalPos}");
        }
    }

    private IEnumerator DelayedTeleportArrivalVFX(Vector3 arrivalPos, GameObject vfxPrefab, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnVFX(vfxPrefab, arrivalPos, Quaternion.identity, 1.5f);
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get trick duration for VFX timing
    /// </summary>
    private float GetTrickDuration(TrickType trickType, CharacterData characterData)
    {
        return trickType switch
        {
            TrickType.SlowSpeed => characterData.GetSlowSpeedDuration(),
            TrickType.Freeze => characterData.GetFreezeDuration(),
            TrickType.InstantDamage => 2f, // Quick effect
            _ => 2f
        };
    }

    /// <summary>
    /// Get treat duration for VFX timing
    /// </summary>
    private float GetTreatDuration(TreatType treatType, CharacterData characterData)
    {
        return treatType switch
        {
            TreatType.Shield => characterData.GetShieldDuration(),
            TreatType.SpeedBoost => characterData.GetSpeedBoostDuration(),
            TreatType.Teleport => 1f, // Quick effect
            _ => 3f
        };
    }

    /// <summary>
    /// Attach VFX to follow a target transform
    /// </summary>
    private IEnumerator AttachVFXToTarget(GameObject vfx, Transform target, float duration, Vector3 offset)
    {
        if (vfx == null || target == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration && vfx != null && target != null)
        {
            vfx.transform.position = target.position + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // OVERLOAD FOR BACKWARDS COMPATIBILITY:
    private IEnumerator AttachVFXToTarget(GameObject vfx, Transform target, float duration)
    {
        return AttachVFXToTarget(vfx, target, duration, Vector3.up * 1.5f); // Default offset
    }

    /// <summary>
    /// Destroy VFX when parent is destroyed
    /// </summary>
    private IEnumerator DestroyWithParent(GameObject vfx, Transform parent)
    {
        while (vfx != null && parent != null)
        {
            yield return null;
        }

        if (vfx != null)
        {
            Destroy(vfx);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CORE VFX SPAWNING SYSTEM
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Core VFX spawning method
    /// </summary>
    public GameObject SpawnVFX(GameObject vfxPrefab, Vector3 position, Quaternion rotation, float lifetime = -1f)
    {
        if (vfxPrefab == null) return null;

        // Check VFX limit
        if (activeVFX.Count >= maxSimultaneousVFX)
        {
            CleanupOldestVFX();
        }

        GameObject vfx = Instantiate(vfxPrefab, position, rotation);

        if (vfx != null)
        {
            activeVFX.Enqueue(vfx);

            // Set lifetime
            float actualLifetime = lifetime > 0 ? lifetime : defaultVFXLifetime;
            StartCoroutine(DestroyVFXAfterTime(vfx, actualLifetime));

            // Auto-detect and play particle systems
            ParticleSystem[] particles = vfx.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                if (!ps.isPlaying)
                {
                    ps.Play();
                }
            }
        }

        return vfx;
    }

    /// <summary>
    /// Cleanup oldest VFX to make room for new ones
    /// </summary>
    private void CleanupOldestVFX()
    {
        if (activeVFX.Count > 0)
        {
            GameObject oldestVFX = activeVFX.Dequeue();
            if (oldestVFX != null)
            {
                Destroy(oldestVFX);
            }
        }
    }

    /// <summary>
    /// Destroy VFX after specified time
    /// </summary>
    private IEnumerator DestroyVFXAfterTime(GameObject vfx, float time)
    {
        yield return new WaitForSeconds(time);

        if (vfx != null)
        {
            Destroy(vfx);
        }
    }

    /// <summary>
    /// Play VFX sound effect
    /// </summary>
    public void PlayVFXSound(AudioClip clip, float volumeScale = 1f)
    {
        if (vfxAudioSource != null && clip != null)
        {
            vfxAudioSource.PlayOneShot(clip, globalVFXVolume * volumeScale);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Clear all active VFX (useful for scene transitions)
    /// </summary>
    public void ClearAllVFX()
    {
        while (activeVFX.Count > 0)
        {
            GameObject vfx = activeVFX.Dequeue();
            if (vfx != null)
            {
                Destroy(vfx);
            }
        }

        // Reset counters
        currentHitVFX = 0;
        currentUltimateVFX = 0;
        currentAbilityVFX = 0;

        if (debugMode)
        {
            Debug.Log("VFXManager: Cleared all VFX");
        }
    }

    /// <summary>
    /// Get VFX performance stats
    /// </summary>
    public void GetVFXStats(out int totalActive, out int hitVFX, out int ultimateVFX, out int abilityVFX)
    {
        totalActive = activeVFX.Count;
        hitVFX = currentHitVFX;
        ultimateVFX = currentUltimateVFX;
        abilityVFX = currentAbilityVFX;
    }

    /// <summary>
    /// Set VFX quality settings
    /// </summary>
    public void SetVFXQuality(int maxVFX)
    {
        maxSimultaneousVFX = maxVFX;

        if (debugMode)
        {
            Debug.Log($"VFX quality updated: Max VFX = {maxVFX}");
        }
    }

    // Debug display
    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 210, 120));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== SIMPLIFIED VFX MANAGER ===");
        GUILayout.Label($"Total Active: {activeVFX.Count}/{maxSimultaneousVFX}");
        GUILayout.Label($"Hit VFX: {currentHitVFX}");
        GUILayout.Label($"Ultimate VFX: {currentUltimateVFX}");
        GUILayout.Label($"Ability VFX: {currentAbilityVFX}");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}