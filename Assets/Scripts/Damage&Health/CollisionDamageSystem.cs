using UnityEngine;
using System.Collections;

/// <summary>
/// Handles ball collision, damage, and post-impact physics
/// Updated with ducking pass-through logic
/// </summary>
public class CollisionDamageSystem : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private float collisionRange = 1.0f;
    [SerializeField] private float duckingHeightThreshold = 1.0f; // Ball must be below this to hit ducking player
    [SerializeField] private float playerCollisionHeight = 1.0f; // Height offset for player collision center
    [SerializeField] private float hitStopDuration = 0.1f;
    [SerializeField] private bool enablePredictiveCollision = true;
    [SerializeField] private float predictionDistance = 1.5f;

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

    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float screenShakeIntensity = 0.3f;
    [SerializeField] private float screenShakeDuration = 0.2f;
    [SerializeField] private Color damageColor = Color.red;

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip criticalHitSound;
    [SerializeField] private AudioClip deflectSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showCollisionGizmos = true;
    [SerializeField] private bool showDuckingVisualization = true;

    // Components
    private BallController ballController;
    private AudioSource audioSource;

    // Collision state
    private bool hasHitThisThrow = false;
    private CharacterController lastThrower;
    private float lastCollisionTime = 0f;
    private float collisionCooldown = 0.2f;

    // Hit detection cache
    private CharacterController[] allPlayers;
    private float playerCacheTime = 0f;
    private float playerCacheInterval = 1f;

    // Track collision frame to prevent double hits
    private int lastCollisionFrame = -1;

    public enum HitType
    {
        Basic,
        Charged,
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

        RefreshPlayerCache();
    }

    void Update()
    {
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
        allPlayers = FindObjectsOfType<CharacterController>();
        playerCacheTime = Time.time;

        if (debugMode)
        {
            Debug.Log($"CollisionDamageSystem: Refreshed player cache - Found {allPlayers.Length} players");
        }
    }

    void CheckForCollisions()
    {
        // Multiple prevention methods for double hits
        if (hasHitThisThrow) return;
        if (Time.time - lastCollisionTime < collisionCooldown) return;
        if (Time.frameCount == lastCollisionFrame) return;

        Vector3 ballPosition = transform.position;
        Vector3 ballVelocity = ballController.velocity;

        // Early exit if ball is moving too slow (likely bouncing on ground)
        if (ballVelocity.magnitude < 5f) return;

        foreach (CharacterController player in allPlayers)
        {
            if (player == null) continue;

            // Skip the thrower
            if (player == ballController.GetThrower()) continue;

            // Check if player is valid target (not knocked out, etc.)
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null && playerHealth.GetCurrentHealth() <= 0) continue;

            // FIXED: Use ducking-aware collision detection
            bool shouldCollide = CheckPlayerCollisionWithDucking(player, ballPosition);

            if (shouldCollide)
            {
                // Determine hit type based on throw type and ball state
                HitType hitType = DetermineHitType();
                HandleCollision(player, hitType);
                return; // Only hit one player per frame
            }
        }
    }

    /// <summary>
    /// Check collision with ducking pass-through logic
    /// </summary>
    bool CheckPlayerCollisionWithDucking(CharacterController player, Vector3 ballPosition)
    {
        Vector3 playerCenter = player.transform.position + Vector3.up * playerCollisionHeight;
        float distanceToPlayer = Vector3.Distance(ballPosition, playerCenter);

        // First check: Is ball close enough to player?
        if (distanceToPlayer > collisionRange)
            return false;

        // Second check: If player is ducking, ball must be low enough to hit
        if (player.IsDucking())
        {
            if (ballPosition.y > duckingHeightThreshold)
            {
                // Ball is too high - passes over ducking player
                if (debugMode)
                {
                    Debug.Log($"Ball passed over ducking {player.name} (Ball Y: {ballPosition.y:F2}, Threshold: {duckingHeightThreshold})");
                }
                return false;
            }
        }

        // Ball should hit the player
        return true;
    }

    HitType DetermineHitType()
    {
        if (ballController == null) return HitType.Basic;

        BallController.ThrowType throwType = ballController.GetThrowType();

        switch (throwType)
        {
            case BallController.ThrowType.JumpThrow:
                return HitType.JumpThrow;
            case BallController.ThrowType.PowerShot:
                return HitType.Ultimate;
            default:
                return HitType.Basic;
        }
    }

    bool CheckPredictiveCollision(Vector3 ballPos, Vector3 ballVel, CharacterController player)
    {
        if (!enablePredictiveCollision) return false;
        if (ballVel.magnitude < 0.1f) return false;

        // Project ball position forward
        Vector3 predictedPosition = ballPos + ballVel.normalized * predictionDistance;

        // Use ducking-aware collision for prediction too
        return CheckPlayerCollisionWithDucking(player, predictedPosition);
    }

    void HandleCollision(CharacterController hitPlayer, HitType hitType)
    {
        if (debugMode)
        {
            Debug.Log($"=== COLLISION DAMAGE SYSTEM HIT ===");
            Debug.Log($"Hit Player: {hitPlayer.name}");
            Debug.Log($"Hit Type: {hitType}");
            Debug.Log($"Ball Velocity: {ballController.velocity.magnitude:F1}");
            Debug.Log($"Thrower: {ballController.GetThrower()?.name}");
            Debug.Log($"Player Ducking: {hitPlayer.IsDucking()}");
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
                        // Failed catch attempt
                        if (debugMode)
                        {
                            Debug.Log($"Catch attempt failed! Chance was {catchChance:F2}");
                        }
                    }
                }
            }
        }

        // If no successful catch, apply damage
        if (!successfulCatch)
        {
            ApplyDamage(hitPlayer, hitType, attemptedCatch);
            CreateImpactEffects(hitPlayer, hitType);
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
            case HitType.Charged:
                return 0.4f; // 40% chance for charged throws
            case HitType.Ultimate:
                return 0.1f; // 10% chance for ultimate throws
            default:
                return 0.7f;
        }
    }

    void HandleSuccessfulCatch(CharacterController catcher, CatchSystem catchSystem)
    {
        // Player successfully caught the ball
        ballController.OnCaught(catcher);

        // Play catch sound
        PlaySound(deflectSound);

        // Create catch effect
        CreateCatchEffect(catcher.transform.position);

        if (debugMode)
        {
            Debug.Log($"Successful catch by {catcher.name}!");
        }
    }

    void ApplyDamage(CharacterController hitPlayer, HitType hitType, bool attemptedCatch)
    {
        // Get player health component
        PlayerHealth playerHealth = hitPlayer.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            // Create health component if it doesn't exist
            playerHealth = hitPlayer.gameObject.AddComponent<PlayerHealth>();
        }

        // Calculate damage based on hit type
        int damage = CalculateDamage(hitType);

        // Better damage reduction for attempted catches
        if (attemptedCatch)
        {
            damage = Mathf.RoundToInt(damage * 0.5f); // 50% damage reduction for attempted catch
            if (debugMode)
            {
                Debug.Log($"Damage reduced for attempted catch: {damage}");
            }
        }

        // Apply damage
        playerHealth.TakeDamage(damage, ballController.GetThrower());

        // Visual feedback
        StartCoroutine(DamageFlash(hitPlayer));

        if (debugMode)
        {
            Debug.Log($"Applied {damage} damage to {hitPlayer.name} (Health: {playerHealth.GetCurrentHealth()})");
        }
    }

    int CalculateDamage(HitType hitType)
    {
        switch (hitType)
        {
            case HitType.Basic:
                return baseDamage;
            case HitType.Charged:
                return chargedDamageMultiplier;
            case HitType.JumpThrow:
                return jumpThrowDamage;
            case HitType.Ultimate:
                return ultimateDamage;
            case HitType.Deflected:
                return Mathf.RoundToInt(baseDamage * 0.5f);
            default:
                return baseDamage;
        }
    }

    void CreateImpactEffects(CharacterController hitPlayer, HitType hitType)
    {
        
            Vector3 impactPosition = hitPlayer.transform.position + Vector3.up * 1f;

            // Spawn hit effect
            if (hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, impactPosition, Quaternion.identity);
                Destroy(effect, 2f);
            }

            // Screen shake
            CameraController cameraController = FindObjectOfType<CameraController>();
            if (cameraController != null)
            {
                // Scale screen shake based on hit type
                float shakeIntensity = screenShakeIntensity;
                if (hitType == HitType.Ultimate) shakeIntensity *= 2f;
                else if (hitType == HitType.JumpThrow) shakeIntensity *= 1.5f;

                cameraController.ShakeCamera(shakeIntensity, screenShakeDuration);
            }

            // Play hit sound
            AudioClip soundToPlay = hitType == HitType.Ultimate ? criticalHitSound : hitSound;
            PlaySound(soundToPlay);

            if (debugMode)
            {
                Debug.Log($"Created impact effects for {hitType} hit");
            }
    }

        void HandlePostImpactPhysics(CharacterController hitPlayer, bool attemptedCatch)
        {
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
            StartCoroutine(ApplyKnockback(hitPlayer, -bounceDirection));

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

        IEnumerator ApplyKnockback(CharacterController player, Vector3 knockbackDirection)
        {
            // Brief hitstop
            yield return new WaitForSeconds(hitStopDuration);

            // More realistic knockback calculation
            Vector3 knockbackOffset = knockbackDirection.normalized * knockbackForce * 0.1f;
            player.transform.position += knockbackOffset;
        }

        IEnumerator DamageFlash(CharacterController player)
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
            // Simple catch effect indication
            if (debugMode)
            {
                Debug.Log($"Catch effect at {position}");
            }

            // TODO: Add particle effect or visual feedback for catches
        }

        void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

    // Public methods for ball controller integration
    public void OnBallThrown(CharacterController thrower)
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

    public CharacterController GetLastThrower() => lastThrower;

    // Add method to manually trigger collision (useful for testing)
    public void ForceCollisionCheck()
    {
        if (ballController != null && ballController.GetBallState() == BallController.BallState.Thrown)
        {
            CheckForCollisions();
        }
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!showCollisionGizmos) return;

        // Draw collision range
        Gizmos.color = hasHitThisThrow ? Color.gray : Color.red;
        Gizmos.DrawWireSphere(transform.position, collisionRange);

        // Draw prediction range when ball is moving
        if (ballController != null && ballController.GetBallState() == BallController.BallState.Thrown)
        {
            Vector3 ballVel = ballController.GetVelocity();
            if (ballVel.magnitude > 15f) // Only show for fast balls
            {
                Gizmos.color = Color.yellow;
                Vector3 predictedPos = transform.position + ballVel.normalized * predictionDistance;
                Gizmos.DrawLine(transform.position, predictedPos);
                Gizmos.DrawWireSphere(predictedPos, collisionRange);
            }
        }

        // Show ducking visualization
        if (showDuckingVisualization && allPlayers != null)
        {
            foreach (CharacterController player in allPlayers)
            {
                if (player == null || player == ballController?.GetThrower()) continue;

                Vector3 playerCenter = player.transform.position + Vector3.up * playerCollisionHeight;
                float distance = Vector3.Distance(transform.position, playerCenter);

                if (player.IsDucking())
                {
                    // Show ducking player in blue
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireSphere(playerCenter, collisionRange);

                    // Show ducking height threshold
                    Gizmos.color = Color.yellow;
                    Vector3 thresholdStart = player.transform.position + Vector3.up * duckingHeightThreshold + Vector3.left * 1.5f;
                    Vector3 thresholdEnd = player.transform.position + Vector3.up * duckingHeightThreshold + Vector3.right * 1.5f;
                    Gizmos.DrawLine(thresholdStart, thresholdEnd);

                    // Show if ball would pass through
                    bool ballPassesThrough = transform.position.y > duckingHeightThreshold;
                    Gizmos.color = ballPassesThrough ? Color.green : Color.red;
                    Gizmos.DrawLine(transform.position, playerCenter);

                    // Show ball height indicator
                    Gizmos.color = Color.cyan;
                    Vector3 ballHeightLine = new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z);
                    Gizmos.DrawLine(player.transform.position, ballHeightLine);
                    Gizmos.DrawWireCube(ballHeightLine, Vector3.one * 0.15f);
                }
                else
                {
                    // Show standing player in green
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(playerCenter, collisionRange);

                    // Show collision status
                    Gizmos.color = distance <= collisionRange ? Color.red : Color.white;
                    Gizmos.DrawLine(transform.position, playerCenter);
                }

                // Show player's actual collider bounds for reference
                Collider playerCollider = player.GetComponent<Collider>();
                if (playerCollider != null)
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.3f); // Semi-transparent white
                    Gizmos.DrawWireCube(playerCollider.bounds.center, playerCollider.bounds.size);
                }
            }
        }

        // Draw to all potential targets
        if (ballController != null && allPlayers != null)
        {
            Gizmos.color = Color.blue;
            foreach (CharacterController player in allPlayers)
            {
                if (player != null && player != ballController.GetThrower())
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance <= collisionRange * 2f) // Show nearby players
                    {
                        Gizmos.DrawLine(transform.position, player.transform.position);
                    }
                }
            }
        }
    }
}