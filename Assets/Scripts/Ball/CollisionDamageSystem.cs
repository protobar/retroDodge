using UnityEngine;
using System.Collections;
using Photon.Pun;

/// <summary>
/// Enhanced CollisionDamageSystem with CLEANED UP PlayerCharacter-only support
/// Now uses VFXManager for all visual effects with proper variety
/// REMOVED: All legacy CharacterController support
/// </summary>
public class CollisionDamageSystem : MonoBehaviour
{
    [Header("Collision Settings - UPDATED")]
    [SerializeField] private bool useDynamicScaling = true;
    // Keep these as base values that will be scaled
    [SerializeField] private float baseCollisionRange = 1.0f;
    [SerializeField] private float baseDuckingHeightThreshold = 1.0f;
    [SerializeField] private float basePlayerCollisionHeight = 1.0f;
    [SerializeField] private float collisionRange = 1.0f;
    [SerializeField] private float duckingHeightThreshold = 1.0f;
    [SerializeField] private float playerCollisionHeight = 1.0f;
    [SerializeField] private float hitStopDuration = 0.1f;
    [SerializeField] private bool enablePredictiveCollision = true;
    [SerializeField] private float predictionDistance = 1.5f;


    private float scaledCollisionRange = 1.0f;
    private float scaledDuckingThreshold = 1.0f;
    private float scaledCollisionHeight = 1.0f;

    [Header("Damage Settings")]
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private int chargedDamageMultiplier = 15;
    [SerializeField] private int jumpThrowDamage = 12;
    [SerializeField] private int ultimateDamage = 25;

    [Header("Post-Impact Physics")]
    [SerializeField] private float bounceForce = 8f;
    [SerializeField] private float bounceRandomness = 2f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private bool enableBallBounce = true;

    [Header("Visual Effects - ENHANCED")]
    [SerializeField] private GameObject hitEffectPrefab; // Fallback only
    [SerializeField] private Color damageColor = Color.red;

    [Header("VFX Settings")]
    [SerializeField] private bool useVFXManager = true;
    [SerializeField] private bool enableHitVFXVariety = true;
    [SerializeField] private float vfxSpawnOffset = 0.2f; // Slight offset for better visibility

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip criticalHitSound;
    [SerializeField] private AudioClip deflectSound;
    [SerializeField] private AudioClip catchFailSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showCollisionGizmos = true;
    [SerializeField] private bool showDuckingVisualization = true;

    // Components
    private BallController ballController;
    private AudioSource audioSource;

    // Collision state
    private bool hasHitThisThrow = false;
    private PlayerCharacter lastThrower;
    private float lastCollisionTime = 0f;
    private float collisionCooldown = 0.2f;

    // Hit detection cache - CLEANED UP: Only PlayerCharacter
    private PlayerCharacter[] allPlayers;
    private float playerCacheTime = 0f;
    private float playerCacheInterval = 1f;

    // Track collision frame to prevent double hits
    private int lastCollisionFrame = -1;
    [SerializeField] private int maxRoofBounces = 1;
    private int usedRoofBounces = 0;

    public enum HitType
    {
        Basic,
        JumpThrow,
        Ultimate,
        Deflected
    }

    void Awake()
    {
        ballController = GetComponent<BallController>();

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        // NEW: Update scaled values from CharacterScaleManager
        UpdateScaledValues();

        RefreshPlayerCache();
    }

    void UpdateScaledValues()
    {
        if (useDynamicScaling && CharacterScaleManager.Instance != null)
        {
            scaledCollisionRange = CharacterScaleManager.Instance.GetCollisionRange();
            scaledDuckingThreshold = CharacterScaleManager.Instance.GetDuckingThreshold(transform);
            scaledCollisionHeight = CharacterScaleManager.Instance.GetCollisionHeight();

            if (debugMode)
            {
                Debug.Log($"CollisionDamageSystem: Updated scaled values");
                Debug.Log($"  Collision Range: {baseCollisionRange} → {scaledCollisionRange}");
                Debug.Log($"  Ducking Threshold: {baseDuckingHeightThreshold} → {scaledDuckingThreshold}");
                Debug.Log($"  Collision Height: {basePlayerCollisionHeight} → {scaledCollisionHeight}");
            }
        }
        else
        {
            // Fallback to base values
            scaledCollisionRange = baseCollisionRange;
            scaledDuckingThreshold = baseDuckingHeightThreshold;
            scaledCollisionHeight = basePlayerCollisionHeight;
        }
    }

    void Update()
    {

        // NEW: Refresh scaled values periodically (every second)
        if (Time.frameCount % 60 == 0)
        {
            UpdateScaledValues();
        }

        // Only check collisions when ball is thrown
        if (ballController != null && ballController.GetBallState() == BallController.BallState.Thrown)
        {
            CheckForCollisions();
        }

        // Refresh player cache periodically
        if (Time.time - playerCacheTime > playerCacheInterval)
        {
            RefreshPlayerCache();
        }
    }

    void RefreshPlayerCache()
    {
        allPlayers = FindObjectsOfType<PlayerCharacter>();
        playerCacheTime = Time.time;

        if (debugMode)
        {
            Debug.Log($"CollisionDamageSystem: Refreshed player cache - Found {allPlayers.Length} PlayerCharacters");
        }
    }

    void CheckForCollisions()
    {
        // Multiple prevention methods for double hits - ENHANCED
        if (hasHitThisThrow)
        {
            if (debugMode) Debug.Log("CheckForCollisions: Already hit this throw, skipping");
            return;
        }

        if (Time.time - lastCollisionTime < collisionCooldown)
        {
            if (debugMode) Debug.Log($"CheckForCollisions: Collision cooldown active ({Time.time - lastCollisionTime:F3}s)");
            return;
        }

        if (Time.frameCount == lastCollisionFrame)
        {
            if (debugMode) Debug.Log($"CheckForCollisions: Already processed this frame ({Time.frameCount})");
            return;
        }

        // CRITICAL: Only the ball authority should check for collisions in online; offline always check
        if (!PhotonNetwork.OfflineMode && !ballController.photonView.IsMine)
        {
            if (debugMode) Debug.Log("CheckForCollisions: Not ball owner, skipping collision check");
            return;
        }

        Vector3 ballPosition = transform.position;
        Vector3 ballVelocity = ballController.velocity;

        // Early exit if ball is moving too slow (likely bouncing on ground)
        if (ballVelocity.magnitude < 3f)
        {
            if (debugMode) Debug.Log($"CheckForCollisions: Ball too slow ({ballVelocity.magnitude:F1}), skipping");
            return;
        }

        if (debugMode)
        {
            Debug.Log($"CheckForCollisions: Active check - Ball authority={ballController.photonView.IsMine}, Speed={ballVelocity.magnitude:F1}, Players={allPlayers.Length}");
        }

        // Check PlayerCharacter system only
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player == null) continue;

            // Skip the thrower
            if (player == ballController.GetThrower()) continue;

            // Check if player is valid target (not knocked out, etc.)
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null && playerHealth.GetCurrentHealth() <= 0) continue;

            // Use ducking-aware collision detection
            bool shouldCollide = CheckPlayerCollisionWithDucking(player, ballPosition);

            if (shouldCollide)
            {
                if (debugMode)
                {
                    Debug.Log($"COLLISION DETECTED: {player.name} at distance {Vector3.Distance(ballPosition, player.transform.position):F2}");
                }

                // Determine hit type based on throw type and ball state
                HitType hitType = DetermineHitType();
                HandleCollision(player, hitType);
                return; // Only hit one player per frame
            }
        }
    }

    /// <summary>
    /// Check collision with ducking pass-through logic for PlayerCharacter
    /// </summary>
    bool CheckPlayerCollisionWithDucking(PlayerCharacter player, Vector3 ballPosition)
    {
        // NEW: Use CharacterScaleManager for accurate collision detection
        if (useDynamicScaling && CharacterScaleManager.Instance != null)
        {
            // Check if ball is in collision range of character
            bool inRange = CharacterScaleManager.Instance.IsInCollisionRange(ballPosition, player.transform);

            if (!inRange) return false;

            // If player is ducking, check if ball is low enough to hit
            if (player.IsDucking())
            {
                float duckThreshold = CharacterScaleManager.Instance.GetDuckingThreshold(player.transform);
                if (ballPosition.y > duckThreshold)
                {
                    if (debugMode)
                    {
                        Debug.Log($"Ball passed over ducking {player.name} (Ball Y: {ballPosition.y:F2}, Threshold: {duckThreshold:F2})");
                    }
                    return false;
                }
            }

            return true;
        }
        else
        {
            // Fallback: Use scaled values directly
            Vector3 playerCenter = player.transform.position + Vector3.up * scaledCollisionHeight;
            float distanceToPlayer = Vector3.Distance(ballPosition, playerCenter);

            if (distanceToPlayer > scaledCollisionRange)
                return false;

            if (player.IsDucking())
            {
                if (ballPosition.y > player.transform.position.y + scaledDuckingThreshold)
                {
                    if (debugMode)
                    {
                        Debug.Log($"Ball passed over ducking {player.name} (Ball Y: {ballPosition.y:F2})");
                    }
                    return false;
                }
            }

            return true;
        }
    }

    HitType DetermineHitType()
    {
        if (ballController == null) return HitType.Basic;

        // Use the correct ThrowType enum values
        ThrowType throwType = ballController.GetThrowType();

        switch (throwType)
        {
            case ThrowType.JumpThrow:
                return HitType.JumpThrow;
            case ThrowType.Ultimate:
                return HitType.Ultimate;
            default:
                return HitType.Basic;
        }
    }

    void HandleCollision(PlayerCharacter hitPlayer, HitType hitType)
    {
        if (debugMode)
        {
            Debug.Log($"HIT: {hitPlayer.name} type:{hitType} v:{ballController.velocity.magnitude:F1} thrower:{ballController.GetThrower()?.name} duck:{hitPlayer.IsDucking()}");
        }

        // Check if player is trying to catch
        CatchSystem catchSystem = hitPlayer.GetComponent<CatchSystem>();
        bool attemptedCatch = false;
        bool successfulCatch = false;

        if (catchSystem != null)
        {
            if (catchSystem.IsBallInRange())
            {
                attemptedCatch = true;

                PlayerInputHandler inputHandler = hitPlayer.GetComponent<PlayerInputHandler>();
                if (inputHandler != null && inputHandler.GetCatchPressed())
                {
                    // Success chance based on timing and ball speed
                    float catchChance = CalculateCatchChance(hitType);
                    if (Random.Range(0f, 1f) <= catchChance)
                    {
                        successfulCatch = true;
                        HandleSuccessfulCatch(hitPlayer, catchSystem);
                        return;
                    }
                    else
                    {
                        // Failed catch attempt - spawn catch fail VFX
                        HandleFailedCatch(hitPlayer, attemptedCatch);
                    }
                }
            }
        }

        // If no successful catch, apply damage
        if (!successfulCatch)
        {
            ApplyDamage(hitPlayer, hitType, attemptedCatch);
            CreateEnhancedImpactEffects(hitPlayer, hitType, attemptedCatch);
            HandlePostImpactPhysics(hitPlayer, attemptedCatch);
        }

        // Mark collision properly
        hasHitThisThrow = true;
        lastCollisionTime = Time.time;
        lastCollisionFrame = Time.frameCount;
        lastThrower = ballController.GetThrower();
    }

    float CalculateCatchChance(HitType hitType)
    {
        switch (hitType)
        {
            case HitType.Basic:
                return 0.8f; // 80% chance for normal throws
            case HitType.JumpThrow:
                return 0.6f; // 60% chance for jump throws
            case HitType.Ultimate:
                return 0.1f; // 10% chance for ultimate throws
            default:
                return 0.7f;
        }
    }

    void HandleSuccessfulCatch(PlayerCharacter catcher, CatchSystem catchSystem)
    {
        // Player successfully caught the ball
        ballController.OnCaught(catcher);

        // Play catch sound
        PlaySound(deflectSound);

        // Create catch effect - VFXManager doesn't have SpawnCatchSuccessVFX, use fallback
        CreateCatchEffect(catcher.transform.position);

        if (debugMode)
        {
            Debug.Log($"Successful catch by {catcher.name}!");
        }
    }

    void HandleFailedCatch(PlayerCharacter player, bool attemptedCatch)
    {
        // VFXManager doesn't have SpawnCatchFailVFX, use fallback
        PlaySound(catchFailSound);

        if (debugMode)
        {
            Debug.Log($"Failed catch attempt by {player.name}!");
        }
    }

    void ApplyDamage(PlayerCharacter hitPlayer, HitType hitType, bool attemptedCatch)
    {
        // CRITICAL: Only ball authority should apply damage (offline mode or ball owner)
        if (!PhotonNetwork.OfflineMode && !ballController.photonView.IsMine)
        {
            if (debugMode)
            {
                Debug.Log($"ApplyDamage: Not ball owner, skipping damage application");
            }
            return;
        }

        // Get player health component
        PlayerHealth playerHealth = hitPlayer.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            // Create health component if it doesn't exist
            playerHealth = hitPlayer.gameObject.AddComponent<PlayerHealth>();
        }

        // Get damage from ball controller (which gets it from character data)
        int damage = ballController.GetCurrentDamage();

        // Apply character damage resistance if available
        CharacterData characterData = hitPlayer.GetCharacterData();
        if (characterData != null)
        {
            damage = Mathf.RoundToInt(damage * characterData.damageResistance);
        }

        // Damage reduction for attempted catches
        if (attemptedCatch)
        {
            damage = Mathf.RoundToInt(damage * 0.7f); // 30% reduction for attempted catch (tunable)
            if (debugMode)
            {
                Debug.Log($"Damage reduced for attempted catch: {damage}");
            }
        }

        if (PhotonNetwork.OfflineMode)
        {
            // Offline: apply damage locally with proper attacker reference
            PlayerCharacter thrower = ballController.GetThrower();
            playerHealth.TakeDamage(damage, thrower);
            
            // Track damage dealt for progression
            if (thrower != null)
            {
                var throwerHealth = thrower.GetComponent<PlayerHealth>();
                if (throwerHealth != null)
                {
                    throwerHealth.damageDealt += damage;
                }
            }
            
            if (debugMode)
            {
                Debug.Log($"Applied {damage} damage OFFLINE to {hitPlayer.name} from {thrower?.name ?? "unknown"}");
            }
        }
        else
        {
            // Use RPC to tell the hit player's client to damage themselves
            PhotonView hitPlayerView = hitPlayer.GetComponent<PhotonView>();
            if (hitPlayerView != null)
            {
                // Get the thrower's PhotonView for attacker reference
                PlayerCharacter thrower = ballController.GetThrower();
                int attackerViewID = -1;
                if (thrower != null)
                {
                    PhotonView throwerView = thrower.GetComponent<PhotonView>();
                    if (throwerView != null)
                    {
                        attackerViewID = throwerView.ViewID;
                    }
                    
                    // Track damage dealt for progression
                    var throwerHealth = thrower.GetComponent<PlayerHealth>();
                    if (throwerHealth != null)
                    {
                        throwerHealth.damageDealt += damage;
                    }
                }

                if (debugMode)
                {
                    Debug.Log($"DAMAGE RPC: Sending {damage} damage to {hitPlayer.name} (ViewID: {hitPlayerView.ViewID}) from thrower ViewID: {attackerViewID}");
                    Debug.Log($"  - Ball Owner: {ballController.photonView.Owner?.NickName}");
                    Debug.Log($"  - Hit Type: {hitType}");
                    Debug.Log($"  - Attempted Catch: {attemptedCatch}");
                }

                // Send RPC ONLY to the hit player's client (not RpcTarget.All)
                hitPlayerView.RPC("TakeDamageFromBall", hitPlayerView.Owner, damage, attackerViewID);
            }
            else
            {
                // Fallback for local damage (shouldn't happen in multiplayer)
                Debug.LogWarning($"No PhotonView found on {hitPlayer.name}, applying local damage");
                playerHealth.TakeDamage(damage, null);
            }
        }

        // Visual feedback (this can run on all clients, but let's limit it to the ball owner to prevent duplication)
        StartCoroutine(DamageFlash(hitPlayer));

        if (debugMode) Debug.Log($"Applied {damage} damage to {hitPlayer.name}");
    }

    /// <summary>
    /// ENHANCED: Create impact effects using VFXManager system with variety
    /// </summary>
    void CreateEnhancedImpactEffects(PlayerCharacter hitPlayer, HitType hitType, bool attemptedCatch)
    {
        Vector3 impactPosition = hitPlayer.transform.position + Vector3.up * 1f + Vector3.forward * vfxSpawnOffset;

        // NEW: Use VFXManager for varied hit effects
        if (useVFXManager && VFXManager.Instance != null)
        {
            // Convert HitType to ThrowType for VFXManager
            ThrowType throwType = hitType switch
            {
                HitType.JumpThrow => ThrowType.JumpThrow,
                HitType.Ultimate => ThrowType.Ultimate,
                _ => ThrowType.Normal
            };

            // Get the thrower for ultimate impact effects
            PlayerCharacter thrower = ballController.GetThrower();

            // FIXED: Always spawn hit VFX, and ultimate impact VFX for ultimates
            if (hitType == HitType.Ultimate && thrower != null)
            {
                // Spawn both regular hit VFX and ultimate impact VFX
                VFXManager.Instance.SpawnHitVFX(impactPosition, hitPlayer, throwType, thrower);
            }
            else
            {
                // Just spawn regular hit VFX
                VFXManager.Instance.SpawnHitVFX(impactPosition, hitPlayer, throwType);
            }
        }
        else
        {
            // Fallback to old system
            if (hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, impactPosition, Quaternion.identity);
                Destroy(effect, 2f);
            }
        }

        // Screen shake (centralized through CameraShakeManager)
        // Base shake values (more intense than before)
        float shakeIntensity = 0.8f;
        float shakeDuration = 0.4f;

        switch (hitType)
        {
            case HitType.Ultimate:
                shakeIntensity = 2.0f; // Massive shake for ultimate
                shakeDuration = 0.8f;
                break;
            case HitType.JumpThrow:
                shakeIntensity = 1.2f;
                shakeDuration = 0.5f;
                break;
            case HitType.Basic:
                shakeIntensity = 0.8f;
                shakeDuration = 0.4f;
                break;
        }

        // Reduce shake if attempted catch
        if (attemptedCatch)
        {
            shakeIntensity *= 0.6f;
            shakeDuration *= 0.7f;
        }

        CameraShakeManager.Instance.TriggerShake(shakeIntensity, shakeDuration, $"Damage_{hitType}_{hitPlayer?.name ?? "Unknown"}");

        // Play hit sound (VFXManager handles audio automatically through SpawnHitVFX)
        if (!useVFXManager || VFXManager.Instance == null)
        {
            // Fallback audio
            AudioClip soundToPlay = hitType == HitType.Ultimate ? criticalHitSound : hitSound;
            PlaySound(soundToPlay);
        }

        if (debugMode)
        {
            Debug.Log($"🎆 Created ENHANCED impact effects for {hitType} hit on {hitPlayer.name}");
        }
    }

    void HandlePostImpactPhysics(PlayerCharacter hitPlayer, bool attemptedCatch)
    {
        if (ballController != null)
        {
            ballController.RemoveUltimateBallVFX();
        }

        // Authentic Super Dodge Ball bounce physics
        Vector3 ballPos = transform.position;
        Vector3 playerPos = hitPlayer.transform.position;

        // Calculate bounce direction (away from player)
        Vector3 bounceDirection = (ballPos - playerPos).normalized;

        // Add appropriate bounce height based on hit type
        HitType currentHitType = DetermineHitType();
        switch (currentHitType)
        {
            case HitType.JumpThrow:
                bounceDirection.y = 0.4f; // Higher bounce for jump throws
                break;
            case HitType.Ultimate:
                bounceDirection.y = 0.6f; // Highest bounce for ultimate
                break;
            default:
                bounceDirection.y = 0.2f; // Normal bounce
                break;
        }

        bounceDirection = bounceDirection.normalized;

        // Reduced randomness for more predictable physics
        Vector3 randomOffset = new Vector3(
            Random.Range(-bounceRandomness * 0.1f, bounceRandomness * 0.1f),
            0f, // No random Y offset
            Random.Range(-bounceRandomness * 0.1f, bounceRandomness * 0.1f)
        );

        // Calculate final bounce velocity
        Vector3 bounceVelocity = (bounceDirection + randomOffset) * bounceForce;

        // Reduce bounce if player attempted to catch (more realistic)
        if (attemptedCatch)
        {
            bounceVelocity *= 0.7f;
        }

        // Apply knockback to player
        StartCoroutine(ApplyKnockback(hitPlayer.transform, -bounceDirection));

        // Set ball physics
        if (enableBallBounce)
        {
            ballController.SetVelocity(bounceVelocity);
            // Ball becomes free immediately for faster gameplay
            ballController.SetBallState(BallController.BallState.Free);
        }
        else
        {
            ballController.ResetBall();
        }

        if (debugMode)
        {
            Debug.Log($"Applied post-impact physics - Bounce: {bounceVelocity.magnitude:F1}, Direction: {bounceDirection}");
        }
    }

    IEnumerator ApplyKnockback(Transform player, Vector3 knockbackDirection)
    {
        // Brief hitstop
        yield return new WaitForSeconds(hitStopDuration);

        // More realistic knockback calculation
        Vector3 knockbackOffset = knockbackDirection.normalized * knockbackForce * 0.1f;
        player.position += knockbackOffset;
    }

    IEnumerator DamageFlash(PlayerCharacter player)
    {
        Renderer playerRenderer = player.GetComponentInChildren<Renderer>();
        if (playerRenderer == null) yield break;

        Material originalMaterial = playerRenderer.material;
        Color originalColor = originalMaterial.color;

        // Flash red
        playerRenderer.material.color = damageColor;
        yield return new WaitForSeconds(0.1f);

        // Back to normal
        if (playerRenderer != null && playerRenderer.material != null)
        {
            playerRenderer.material.color = originalColor;
        }
    }

    void CreateCatchEffect(Vector3 position)
    {
        // Simple catch effect indication - could be enhanced with particles
        if (debugMode)
        {
            Debug.Log($"Catch effect at {position}");
        }

        // Basic visual feedback for successful catch
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position + Vector3.up * 0.5f, Quaternion.identity);
            effect.transform.localScale *= 0.5f; // Smaller effect for catches

            // Change color to indicate success (if possible)
            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                var main = particles.main;
                main.startColor = Color.green; // Green for successful catch
            }

            Destroy(effect, 1f);
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Public methods for ball controller integration - CLEANED UP
    public void OnBallThrown(PlayerCharacter thrower)
    {
        hasHitThisThrow = false;
        lastThrower = thrower;
        lastCollisionFrame = -1;

        if (debugMode)
        {
            Debug.Log($"CollisionDamageSystem: Ball thrown by {thrower.name}");
        }
    }

    public void OnBallReset()
    {
        hasHitThisThrow = false;
        lastThrower = null;
        lastCollisionTime = 0f;
        lastCollisionFrame = -1;

        if (debugMode)
        {
            Debug.Log("CollisionDamageSystem: Ball reset");
        }
    }

    public bool HasHitThisThrow() => hasHitThisThrow;
    public PlayerCharacter GetLastThrower() => lastThrower;

    // Add method to manually trigger collision (useful for testing)
    public void ForceCollisionCheck()
    {
        if (ballController != null && ballController.GetBallState() == BallController.BallState.Thrown)
        {
            CheckForCollisions();
        }
    }

    // Enhanced VFX Integration Methods
    public void SetVFXManagerEnabled(bool enabled)
    {
        useVFXManager = enabled;
        if (debugMode)
        {
            Debug.Log($"VFX Manager usage set to: {enabled}");
        }
    }

    public bool IsVFXManagerEnabled()
    {
        return useVFXManager && VFXManager.Instance != null;
    }

    // Get VFX spawn position with offset
    public Vector3 GetVFXPosition(Transform target)
    {
        return target.position + Vector3.up * 1f + Vector3.forward * vfxSpawnOffset;
    }

    // Enhanced collision detection with predictive collision
    private bool PredictiveCollisionCheck(PlayerCharacter player, Vector3 ballPosition, Vector3 ballVelocity)
    {
        if (!enablePredictiveCollision) return false;

        // Predict where the ball will be in the next frame
        Vector3 predictedBallPos = ballPosition + (ballVelocity * Time.fixedDeltaTime * predictionDistance);

        // Check if predicted position would collide
        Vector3 playerCenter = player.transform.position + Vector3.up * playerCollisionHeight;
        float predictedDistance = Vector3.Distance(predictedBallPos, playerCenter);

        return predictedDistance <= collisionRange;
    }

    // Method to handle special VFX for different character types (if needed in future)
    private void HandleCharacterSpecificVFX(PlayerCharacter hitPlayer, HitType hitType, Vector3 impactPosition)
    {
        if (!useVFXManager || VFXManager.Instance == null) return;

        // Future feature: Add character-specific VFX based on character data
        // For now, all character-specific effects are handled in the main VFX calls

        if (debugMode)
        {
            Debug.Log($"Character-specific VFX handling for {hitPlayer.name} (placeholder)");
        }
    }

    // Health integration helper
    private bool IsPlayerValidTarget(PlayerCharacter player)
    {
        if (player == null) return false;

        // Check if player is the thrower
        if (player == ballController.GetThrower()) return false;

        // Check player health
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.GetCurrentHealth() <= 0) return false;

        return true;
    }

    // Method to handle combo hits (if ball hits multiple players in sequence)
    private void HandleComboHit(PlayerCharacter hitPlayer, HitType hitType)
    {
        // Future feature: Track consecutive hits for combo multipliers
        if (useVFXManager && VFXManager.Instance != null)
        {
            // Could spawn special combo VFX here
            Debug.Log($"Combo hit potential detected on {hitPlayer.name}");
        }
    }

    // Debug visualization with enhanced VFX system information
    void OnDrawGizmosSelected()
    {
        if (!showCollisionGizmos) return;

        // Update scaled values for editor visualization
        if (Application.isPlaying)
        {
            UpdateScaledValues();
        }

        // Draw collision range (use scaled value)
        Gizmos.color = hasHitThisThrow ? Color.gray : Color.red;
        Gizmos.DrawWireSphere(transform.position, scaledCollisionRange);

        // Show enhanced VFX system status
        if (useVFXManager)
        {
            Gizmos.color = VFXManager.Instance != null ? Color.green : Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.forward * vfxSpawnOffset, 0.1f);
        }

        // Show ducking visualization (use scaled value)
        if (showDuckingVisualization)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * scaledDuckingThreshold,
                              new Vector3(scaledCollisionRange * 2f, 0.1f, scaledCollisionRange * 2f));
        }

        // Show predictive collision range
        if (enablePredictiveCollision && ballController != null)
        {
            Vector3 predictedPos = transform.position + (ballController.velocity * Time.fixedDeltaTime * predictionDistance);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(predictedPos, scaledCollisionRange * 0.5f);
            Gizmos.DrawLine(transform.position, predictedPos);
        }

        // Draw player collision heights with scaled values
        PlayerCharacter[] players = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in players)
        {
            if (player == null) continue;

            Vector3 playerCenter;

            // NEW: Use CharacterScaleManager if available
            if (useDynamicScaling && CharacterScaleManager.Instance != null)
            {
                playerCenter = CharacterScaleManager.Instance.GetCollisionCheckPosition(player.transform);
            }
            else
            {
                playerCenter = player.transform.position + Vector3.up * scaledCollisionHeight;
            }

            // Color based on player state
            if (player.IsDucking())
            {
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.white;
            }

            Gizmos.DrawWireSphere(playerCenter, scaledCollisionRange);

            // Draw target position (where ball should aim)
            if (useDynamicScaling && CharacterScaleManager.Instance != null)
            {
                Gizmos.color = Color.magenta;
                Vector3 targetPos = CharacterScaleManager.Instance.GetCharacterTargetPosition(player.transform);
                Gizmos.DrawWireSphere(targetPos, 0.3f);
                Gizmos.DrawLine(player.transform.position, targetPos);
            }
        }

        // Show last collision info
        if (lastCollisionTime > 0f && Time.time - lastCollisionTime < 2f)
        {
            Gizmos.color = Color.magenta;
            Vector3 textPos = transform.position + Vector3.up * 3f;
            Gizmos.DrawWireCube(textPos, Vector3.one * 0.5f);
        }
    }

    // Editor helper methods
#if UNITY_EDITOR
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnValidate()
    {
        // Clamp values to reasonable ranges
        collisionRange = Mathf.Clamp(collisionRange, 0.1f, 5f);
        duckingHeightThreshold = Mathf.Clamp(duckingHeightThreshold, 0.5f, 3f);
        playerCollisionHeight = Mathf.Clamp(playerCollisionHeight, 0.5f, 10f);
        vfxSpawnOffset = Mathf.Clamp(vfxSpawnOffset, 0f, 1f);

        // Ensure collision cooldown is reasonable
        collisionCooldown = Mathf.Clamp(collisionCooldown, 0.05f, 1f);

        // Validate VFX settings
        if (useVFXManager && Application.isPlaying)
        {
            if (VFXManager.Instance == null)
            {
                Debug.LogWarning("CollisionDamageSystem: VFX Manager is enabled but no VFXManager instance found in scene!");
            }
        }
    }
#endif

    // Performance optimization: Object pooling for frequent effects
    private void InitializeEffectPools()
    {
        // This could be expanded to pre-instantiate effect objects for better performance
        if (debugMode)
        {
            Debug.Log("CollisionDamageSystem: Effect pools initialized");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // LEGACY SUPPORT REMOVED
    // All CharacterController legacy methods have been removed to clean up the code
    // This includes:
    // - allLegacyPlayers cache
    // - CheckLegacyPlayerCollisionWithDucking()
    // - HandleLegacyCollision()
    // - HandleLegacySuccessfulCatch()
    // - HandleLegacyFailedCatch()
    // - ApplyLegacyDamage()
    // - CreateLegacyEnhancedImpactEffects()
    // - HandleLegacyPostImpactPhysics()
    // - DamageFlashLegacy()
    // - OnBallThrownLegacy()
    // - GetLastLegacyThrower()
    // - IsLegacyPlayerValidTarget()
    // ═══════════════════════════════════════════════════════════════
}