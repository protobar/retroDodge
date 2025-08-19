using UnityEngine;
using System.Collections;

/// <summary>
/// Enhanced BallController with Complete Ball Hold Timer System
/// Includes Warning Phase, Danger Phase, and Penalty Damage
/// FIXED: Ball no longer gives damage immediately on pickup
/// </summary>
public class BallController : MonoBehaviour
{
    [Header("Ball Settings")]
    [SerializeField] private float baseSpeed = 25f;
    [SerializeField] private float gravity = 15f;
    [SerializeField] private float pickupRange = 1.2f;
    [SerializeField] private float bounceMultiplier = 0.6f;

    [Header("Ball Hold Timer System")]
    [SerializeField] private float maxHoldTime = 5f;
    [SerializeField] private float warningStartTime = 3f;
    [SerializeField] private float dangerStartTime = 4f;
    [SerializeField] private float holdPenaltyDamage = 10f;
    [SerializeField] private bool enableHoldTimer = true;
    [SerializeField] private bool resetBallOnPenalty = true;
    [SerializeField] private float ballDropDelay = 0.5f; // Delay before ball drops/resets

    [Header("Hold Timer Visual Effects")]
    [SerializeField] private GameObject warningEffect;
    [SerializeField] private GameObject dangerEffect;
    [SerializeField] private GameObject corruptionEffect;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;
    [SerializeField] private Color corruptionColor = Color.magenta;

    [Header("Hold Timer Audio")]
    [SerializeField] private AudioClip warningSound;
    [SerializeField] private AudioClip dangerSound;
    [SerializeField] private AudioClip corruptionSound;
    [SerializeField] private AudioClip tickSound;

    [Header("NEO GEO 1996 Authentic Physics")]
    [SerializeField] private float normalThrowSpeed = 18f;
    [SerializeField] private float jumpThrowSpeed = 22f;
    [SerializeField] private bool useNeoGeoPhysics = true;

    [Header("Ball Hold Position")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0.5f, 1.5f, 0f);
    [SerializeField] private bool useRelativeToPlayer = true;
    [SerializeField] private float holdFollowSpeed = 10f;
    [SerializeField] private bool smoothHoldMovement = true;

    [Header("Wall & Bounds Collision")]
    [SerializeField] private bool enableWallCollision = true;
    [SerializeField] private float arenaLeftBound = -12f;
    [SerializeField] private float arenaRightBound = 12f;
    [SerializeField] private float arenaTopBound = 8f;
    [SerializeField] private float arenaBottomBound = -2f;
    [SerializeField] private float wallBounceMultiplier = 0.75f;
    [SerializeField] private float energyLossOnBounce = 0.1f;
    [SerializeField] private int maxWallBounces = 3;

    [Header("Wall Collision Effects")]
    [SerializeField] private GameObject wallBounceEffect;
    [SerializeField] private AudioClip wallBounceSound;
    [SerializeField] private bool enableWallShake = true;

    [Header("Ultimate Ball VFX")]
    private GameObject ultimateBallVFXInstance; // Track the VFX instance
    private bool hasUltimateBallVFX = false;

    // Wall collision state
    private int currentWallBounces = 0;
    private bool hasHitWallThisFrame = false;

    [Header("Visual Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color heldColor = Color.yellow;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Ball state
    public enum BallState { Free, Held, Thrown }

    [SerializeField] private BallState currentState = BallState.Free;
    private ThrowType currentThrowType = ThrowType.Normal;
    private int currentDamage = 10;
    private float currentThrowSpeed = 18f;

    // Ball Hold Timer State - FIXED VARIABLES
    private float ballHoldStartTime = 0f;
    private bool isShowingWarning = false;
    private bool isInDangerPhase = false;
    private bool hasAppliedPenalty = false; // Changed: Only apply penalty once
    private Coroutine ballDropCoroutine;

    // Physics
    public Vector3 velocity;
    private bool isGrounded = false;
    private bool hasHitTarget = false;
    private bool homingEnabled = false;

    // References
    private Transform ballTransform;
    private Renderer ballRenderer;
    private CollisionDamageSystem collisionSystem;
    private AudioSource audioSource;

    // Character system integration
    private PlayerCharacter holder;
    private CharacterController legacyHolder;
    private PlayerCharacter thrower;
    private CharacterController legacyThrower;
    private Transform targetOpponent;
    private bool isJumpThrow = false;

    // Ground detection
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float groundCheckDistance = 0.6f;

    // Visual effects for hold timer
    private GameObject activeWarningEffect;
    private GameObject activeDangerEffect;
    private GameObject activeCorruptionEffect;
    private Color originalBallColor;

    void Awake()
    {
        ballTransform = transform;
        ballRenderer = GetComponent<Renderer>();
        audioSource = GetComponent<AudioSource>();

        // Setup audio source
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        // Setup collision damage system
        collisionSystem = GetComponent<CollisionDamageSystem>();
        if (collisionSystem == null)
        {
            collisionSystem = gameObject.AddComponent<CollisionDamageSystem>();
        }

        // Ensure ball has a collider
        if (GetComponent<Collider>() == null)
        {
            SphereCollider ballCollider = gameObject.AddComponent<SphereCollider>();
            ballCollider.radius = 0.5f;
            ballCollider.isTrigger = false;
        }

        // Store original ball color
        if (ballRenderer != null)
        {
            originalBallColor = ballRenderer.material.color;
        }

        SetBallState(BallState.Free);
    }

    void Update()
    {
        switch (currentState)
        {
            case BallState.Free:
                HandleFreeBall();
                break;
            case BallState.Held:
                HandleHeldBall();
                if (enableHoldTimer)
                {
                    UpdateBallHoldTimer();
                }
                break;
            case BallState.Thrown:
                HandleThrownBall();
                break;
        }

        CheckPickupRange();
        UpdateVisuals();
    }

    void HandleFreeBall()
    {
        // Apply gravity when ball is FREE
        if (!isGrounded)
        {
            velocity.y -= gravity * Time.deltaTime;
        }
        else
        {
            // Ball bounces or comes to rest
            if (velocity.y < -1f)
            {
                velocity.y = -velocity.y * bounceMultiplier;
                if (velocity.y < 2f)
                {
                    velocity.y = 0f;
                }
            }
            else
            {
                velocity.y = 0f;
            }

            // Apply friction on ground
            velocity.x *= 0.95f;
            velocity.z *= 0.95f;
        }

        // Move the ball
        ballTransform.Translate(velocity * Time.deltaTime, Space.World);
        CheckGrounded();
    }

    void HandleHeldBall()
    {
        // Support both new PlayerCharacter and legacy CharacterController
        Transform holderTransform = null;
        if (holder != null)
        {
            holderTransform = holder.transform;
        }
        else if (legacyHolder != null)
        {
            holderTransform = legacyHolder.transform;
        }

        if (holderTransform != null)
        {
            Vector3 holdPosition = CalculateHoldPosition(holderTransform);

            if (smoothHoldMovement)
            {
                ballTransform.position = Vector3.Lerp(ballTransform.position, holdPosition, holdFollowSpeed * Time.deltaTime);
            }
            else
            {
                ballTransform.position = holdPosition;
            }

            velocity = Vector3.zero;
        }
        else
        {
            SetBallState(BallState.Free);
        }
    }

    Vector3 CalculateHoldPosition(Transform holderTransform)
    {
        Vector3 basePosition = holderTransform.position;

        if (useRelativeToPlayer)
        {
            Vector3 holdPosition = basePosition;
            holdPosition += holderTransform.right * holdOffset.x;
            holdPosition += Vector3.up * holdOffset.y;
            holdPosition += holderTransform.forward * holdOffset.z;
            return holdPosition;
        }
        else
        {
            return basePosition + holdOffset;
        }
    }

    void HandleThrownBall()
    {
        // Existing Neo Geo physics
        if (useNeoGeoPhysics)
        {
            HandleNeoGeoPhysics();
        }

        // Existing homing behavior
        if (homingEnabled)
        {
            ApplyHomingBehavior();
        }

        // NEW: Check for wall collisions
        CheckWallCollisions();

        // Move the ball (existing logic)
        ballTransform.Translate(velocity * Time.deltaTime, Space.World);

        // Existing ground collision
        CheckGrounded();
        if (isGrounded && velocity.y <= 0)
        {
            velocity.y = -velocity.y * bounceMultiplier;
            if (velocity.magnitude < 5f)
            {
                SetBallState(BallState.Free);
            }
        }

        // Enhanced out-of-bounds check
        if (ballTransform.position.y < arenaBottomBound - 3f)
        {
            ResetBall();
        }
    }

    void CheckWallCollisions()
    {
        if (!enableWallCollision || currentState != BallState.Thrown) return;
        if (velocity.magnitude < 0.1f || hasHitWallThisFrame) return;

        Vector3 currentPos = ballTransform.position;
        Vector3 nextPos = currentPos + velocity * Time.deltaTime;

        // Left wall
        if (currentPos.x > arenaLeftBound && nextPos.x <= arenaLeftBound)
        {
            HandleWallBounce("Left");
        }
        // Right wall
        else if (currentPos.x < arenaRightBound && nextPos.x >= arenaRightBound)
        {
            HandleWallBounce("Right");
        }
        // Top wall
        else if (currentPos.y < arenaTopBound && nextPos.y >= arenaTopBound)
        {
            HandleWallBounce("Top");
        }
        // Bottom wall
        else if (currentPos.y > arenaBottomBound && nextPos.y <= arenaBottomBound)
        {
            HandleWallBounce("Bottom");
        }

        hasHitWallThisFrame = false;
    }

    void HandleWallBounce(string wallType)
    {
        hasHitWallThisFrame = true;
        currentWallBounces++;

        switch (wallType)
        {
            case "Left":
                ballTransform.position = new Vector3(arenaLeftBound, ballTransform.position.y, ballTransform.position.z);
                velocity.x = -velocity.x * wallBounceMultiplier;
                break;
            case "Right":
                ballTransform.position = new Vector3(arenaRightBound, ballTransform.position.y, ballTransform.position.z);
                velocity.x = -velocity.x * wallBounceMultiplier;
                break;
            case "Top":
                ballTransform.position = new Vector3(ballTransform.position.x, arenaTopBound, ballTransform.position.z);
                velocity.y = -velocity.y * wallBounceMultiplier;
                break;
            case "Bottom":
                ballTransform.position = new Vector3(ballTransform.position.x, arenaBottomBound, ballTransform.position.z);
                velocity.y = -velocity.y * wallBounceMultiplier * 0.8f;
                break;
        }

        // Apply energy loss
        velocity *= (1f - energyLossOnBounce);

        // Add slight randomness
        velocity += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);

        // Effects
        PlayWallBounceEffects(wallType);

        // Stop bouncing if too many bounces or too slow
        if (currentWallBounces >= maxWallBounces || velocity.magnitude < 2f)
        {
            SetBallState(BallState.Free);
            currentWallBounces = 0;
        }

        if (debugMode)
        {
            Debug.Log($"⚡ Wall Bounce: {wallType} | Velocity: {velocity.magnitude:F1} | Bounces: {currentWallBounces}");
        }
    }

    void PlayWallBounceEffects(string wallType)
    {
        // Play sound
        if (wallBounceSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(wallBounceSound, 0.5f);
        }

        // Screen shake
        if (enableWallShake)
        {
            CameraController camera = FindObjectOfType<CameraController>();
            if (camera != null)
            {
                camera.ShakeCamera(0.2f, 0.15f);
            }
        }

        // Spawn effect
        if (wallBounceEffect != null)
        {
            GameObject effect = Instantiate(wallBounceEffect, ballTransform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // BALL HOLD TIMER SYSTEM - FIXED IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Main ball hold timer update - called every frame when ball is held
    /// FIXED: Now properly checks hold duration and prevents immediate damage
    /// </summary>
    void UpdateBallHoldTimer()
    {
        if (currentState != BallState.Held || ballHoldStartTime == 0f) return;

        float holdDuration = GetHoldDuration();

        // Warning Phase (3-4 seconds) - only trigger once
        if (holdDuration >= warningStartTime && !isShowingWarning && !isInDangerPhase && !hasAppliedPenalty)
        {
            StartWarningPhase();
        }

        // Danger Phase (4-5 seconds) - only trigger once
        if (holdDuration >= dangerStartTime && !isInDangerPhase && !hasAppliedPenalty)
        {
            StartDangerPhase();
        }

        // Penalty Phase (5+ seconds) - only trigger once
        if (holdDuration >= maxHoldTime && !hasAppliedPenalty)
        {
            StartPenaltyPhase();
        }

        // Update visual effects
        UpdateHoldTimerVisuals(holdDuration);

        if (debugMode && Time.frameCount % 60 == 0) // Log once per second
        {
            Debug.Log($"Ball Hold Timer: {holdDuration:F1}s | Warning: {isShowingWarning} | Danger: {isInDangerPhase} | Penalty: {hasAppliedPenalty}");
        }
    }

    /// <summary>
    /// Start the warning phase (3-4 seconds)
    /// </summary>
    void StartWarningPhase()
    {
        isShowingWarning = true;

        // Play warning sound
        PlayHoldTimerSound(warningSound);

        // Create warning visual effect
        if (warningEffect != null)
        {
            activeWarningEffect = Instantiate(warningEffect, ballTransform.position, Quaternion.identity);
            activeWarningEffect.transform.SetParent(ballTransform);
        }

        // Change ball color to warning
        if (ballRenderer != null)
        {
            ballRenderer.material.color = warningColor;
        }

        // Notify UI systems
        BallHoldTimerUI.Instance?.ShowWarning(this);

        if (debugMode)
        {
            Debug.Log("🟡 Ball Hold Timer: WARNING PHASE STARTED");
        }
    }

    /// <summary>
    /// Start the danger phase (4-5 seconds)
    /// </summary>
    void StartDangerPhase()
    {
        isInDangerPhase = true;

        // Stop warning effects
        if (activeWarningEffect != null)
        {
            Destroy(activeWarningEffect);
        }

        // Play danger sound
        PlayHoldTimerSound(dangerSound);

        // Create danger visual effect
        if (dangerEffect != null)
        {
            activeDangerEffect = Instantiate(dangerEffect, ballTransform.position, Quaternion.identity);
            activeDangerEffect.transform.SetParent(ballTransform);
        }

        // Change ball color to danger
        if (ballRenderer != null)
        {
            ballRenderer.material.color = dangerColor;
        }

        // Notify UI systems
        BallHoldTimerUI.Instance?.ShowDanger(this);

        // Screen shake warning
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ShakeCamera(0.2f, 0.3f);
        }

        if (debugMode)
        {
            Debug.Log("🔶 Ball Hold Timer: DANGER PHASE STARTED");
        }
    }

    /// <summary>
    /// Start the penalty phase (5+ seconds) - one-time damage and ball drop
    /// </summary>
    void StartPenaltyPhase()
    {
        hasAppliedPenalty = true;

        // Stop danger effects
        if (activeDangerEffect != null)
        {
            Destroy(activeDangerEffect);
        }

        // Play corruption sound
        PlayHoldTimerSound(corruptionSound);

        // Create corruption visual effect
        if (corruptionEffect != null)
        {
            activeCorruptionEffect = Instantiate(corruptionEffect, ballTransform.position, Quaternion.identity);
            activeCorruptionEffect.transform.SetParent(ballTransform);
        }

        // Change ball color to corruption
        if (ballRenderer != null)
        {
            ballRenderer.material.color = corruptionColor;
        }

        // Apply ONE-TIME penalty damage
        ApplyOneTimePenalty();

        // Notify UI systems
        BallHoldTimerUI.Instance?.ShowPenalty(this);

        // Strong screen shake
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ShakeCamera(0.8f, 0.8f);
        }

        if (debugMode)
        {
            Debug.Log("🔴 Ball Hold Timer: PENALTY PHASE - One-time damage applied!");
        }

        // Start ball drop/reset after delay
        if (ballDropCoroutine != null)
        {
            StopCoroutine(ballDropCoroutine);
        }
        ballDropCoroutine = StartCoroutine(DropBallAfterDelay());
    }

    /// <summary>
    /// Apply one-time penalty damage and prepare to drop ball
    /// </summary>
    void ApplyOneTimePenalty()
    {
        // Apply damage to ball holder
        if (holder != null)
        {
            PlayerHealth holderHealth = holder.GetComponent<PlayerHealth>();
            if (holderHealth != null)
            {
                int damage = Mathf.RoundToInt(holdPenaltyDamage);
                holderHealth.TakeDamage(damage, null);

                if (debugMode)
                {
                    Debug.Log($"💀 Ball Hold Penalty: {damage} damage to {holder.name} - Ball will be dropped!");
                }
            }
        }
        else if (legacyHolder != null)
        {
            PlayerHealth holderHealth = legacyHolder.GetComponent<PlayerHealth>();
            if (holderHealth != null)
            {
                int damage = Mathf.RoundToInt(holdPenaltyDamage);
                holderHealth.TakeDamage(damage, legacyHolder);

                if (debugMode)
                {
                    Debug.Log($"💀 Ball Hold Penalty: {damage} damage to {legacyHolder.name} - Ball will be dropped!");
                }
            }
        }

        // Extra screen shake for impact
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ShakeCamera(0.5f, 0.3f);
        }
    }

    /// <summary>
    /// Coroutine to drop/reset ball after penalty delay
    /// </summary>
    System.Collections.IEnumerator DropBallAfterDelay()
    {
        yield return new WaitForSeconds(ballDropDelay);

        if (debugMode)
        {
            Debug.Log("⚡ Ball Hold Penalty: Dropping ball due to excessive hold time!");
        }

        // Show effect before dropping
        if (activeCorruptionEffect != null)
        {
            // Make corruption effect more intense before ball drops
            activeCorruptionEffect.transform.localScale *= 2f;
        }

        yield return new WaitForSeconds(0.2f);

        if (resetBallOnPenalty)
        {
            // Reset ball to center
            ResetBall();
        }
        else
        {
            // Just drop the ball where the player is
            DropBall();
        }
    }

    /// <summary>
    /// Drop the ball at current location (alternative to reset)
    /// </summary>
    void DropBall()
    {
        // Release from holder
        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }
        else if (legacyHolder != null)
        {
            legacyHolder.SetHasBall(false);
            legacyHolder = null;
        }

        // Add some random velocity so ball doesn't just sit there
        velocity = new Vector3(
            Random.Range(-3f, 3f),
            Random.Range(2f, 4f),
            Random.Range(-3f, 3f)
        );

        SetBallState(BallState.Free);

        if (debugMode)
        {
            Debug.Log("Ball dropped due to hold penalty!");
        }
    }

    /// <summary>
    /// Update visual effects based on hold duration
    /// </summary>
    void UpdateHoldTimerVisuals(float holdDuration)
    {
        if (ballRenderer == null) return;

        // Pulsing effect during warning/danger phases
        if (isShowingWarning && !isInDangerPhase)
        {
            // Yellow warning pulse
            float pulse = Mathf.Sin(Time.time * 4f) * 0.3f + 0.7f;
            ballRenderer.material.color = Color.Lerp(heldColor, warningColor, pulse);
        }
        else if (isInDangerPhase && !hasAppliedPenalty)
        {
            // Red danger pulse (faster)
            float pulse = Mathf.Sin(Time.time * 8f) * 0.4f + 0.6f;
            ballRenderer.material.color = Color.Lerp(warningColor, dangerColor, pulse);
        }
        else if (hasAppliedPenalty)
        {
            // Corruption pulse (very fast) - more intense
            float pulse = Mathf.Sin(Time.time * 15f) * 0.6f + 0.4f;
            ballRenderer.material.color = Color.Lerp(dangerColor, corruptionColor, pulse);
        }
    }

    /// <summary>
    /// Reset all hold timer states - FIXED VERSION
    /// </summary>
    void ResetHoldTimer()
    {
        // FIXED: Don't reset ballHoldStartTime here, only reset state flags
        isShowingWarning = false;
        isInDangerPhase = false;
        hasAppliedPenalty = false;

        // Stop any running ball drop coroutine
        if (ballDropCoroutine != null)
        {
            StopCoroutine(ballDropCoroutine);
            ballDropCoroutine = null;
        }

        // Clean up visual effects
        if (activeWarningEffect != null)
        {
            Destroy(activeWarningEffect);
            activeWarningEffect = null;
        }

        if (activeDangerEffect != null)
        {
            Destroy(activeDangerEffect);
            activeDangerEffect = null;
        }

        if (activeCorruptionEffect != null)
        {
            Destroy(activeCorruptionEffect);
            activeCorruptionEffect = null;
        }

        // Reset ball color
        if (ballRenderer != null)
        {
            ballRenderer.material.color = originalBallColor;
        }

        // Notify UI systems
        BallHoldTimerUI.Instance?.HideTimer();

        if (debugMode)
        {
            Debug.Log("⚪ Ball Hold Timer: RESET");
        }
    }

    /// <summary>
    /// Completely stop and reset the hold timer - FIXED VERSION
    /// </summary>
    void StopHoldTimer()
    {
        ballHoldStartTime = 0f;
        ResetHoldTimer();

        if (debugMode)
        {
            Debug.Log("⏹️ Ball Hold Timer: STOPPED");
        }
    }

    /// <summary>
    /// Get current hold duration - FIXED VERSION
    /// </summary>
    public float GetHoldDuration()
    {
        if (currentState != BallState.Held || ballHoldStartTime == 0f) return 0f;
        return Time.time - ballHoldStartTime;
    }

    /// <summary>
    /// Get hold timer progress (0-1)
    /// </summary>
    public float GetHoldProgress()
    {
        if (currentState != BallState.Held || ballHoldStartTime == 0f) return 0f;
        return Mathf.Clamp01(GetHoldDuration() / maxHoldTime);
    }

    /// <summary>
    /// Check which phase we're in
    /// </summary>
    public string GetCurrentHoldPhase()
    {
        if (currentState != BallState.Held) return "None";
        if (hasAppliedPenalty) return "Penalty";
        if (isInDangerPhase) return "Danger";
        if (isShowingWarning) return "Warning";
        return "Normal";
    }

    /// <summary>
    /// Play hold timer sound effect
    /// </summary>
    void PlayHoldTimerSound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // EXISTING BALL CONTROLLER METHODS (FIXED for Hold Timer)
    // ═══════════════════════════════════════════════════════════════

    public bool TryPickup(PlayerCharacter character)
    {
        if (currentState != BallState.Free) return false;

        float distance = Vector3.Distance(ballTransform.position, character.transform.position);
        if (distance <= pickupRange)
        {
            holder = character;
            legacyHolder = null;
            SetBallState(BallState.Held);
            character.SetHasBall(true);

            // FIXED: START HOLD TIMER properly
            ballHoldStartTime = Time.time;
            ResetHoldTimer(); // This now only resets flags, not the timer

            if (debugMode)
            {
                string characterName = character.GetCharacterData()?.characterName ?? character.name;
                Debug.Log($"{characterName} picked up the ball! Hold timer started at {ballHoldStartTime}");
            }
            return true;
        }
        return false;
    }

    public bool TryPickupLegacy(CharacterController character)
    {
        if (currentState != BallState.Free) return false;

        float distance = Vector3.Distance(ballTransform.position, character.transform.position);
        if (distance <= pickupRange)
        {
            legacyHolder = character;
            holder = null;
            SetBallState(BallState.Held);
            character.SetHasBall(true);

            // FIXED: START HOLD TIMER properly
            ballHoldStartTime = Time.time;
            ResetHoldTimer(); // This now only resets flags, not the timer

            if (debugMode)
            {
                Debug.Log($"{character.name} picked up the ball! Hold timer started at {ballHoldStartTime}");
            }
            return true;
        }
        return false;
    }

    public void OnCaught(PlayerCharacter catcher)
    {
        if (catcher == null)
        {
            Debug.LogError("BallController.OnCaught: PlayerCharacter catcher is null!");
            return;
        }

        // REMOVE ULTIMATE BALL VFX WHEN CAUGHT
        RemoveUltimateBallVFX();

        holder = catcher;
        legacyHolder = null;
        SetBallState(BallState.Held);
        catcher.SetHasBall(true);
        velocity = Vector3.zero;
        thrower = null;
        legacyThrower = null;
        targetOpponent = null;
        hasHitTarget = false;
        homingEnabled = false;

        // START HOLD TIMER FOR CAUGHT BALL
        ballHoldStartTime = Time.time;
        ResetHoldTimer();

        if (debugMode)
        {
            string characterName = catcher.GetCharacterData()?.characterName ?? catcher.name;
            Debug.Log($"{characterName} caught the ball! Hold timer started at {ballHoldStartTime}");
        }
    }

    public void OnCaught(CharacterController catcher)
    {
        if (catcher == null)
        {
            Debug.LogError("BallController.OnCaught: CharacterController catcher is null!");
            return;
        }

        // REMOVE ULTIMATE BALL VFX WHEN CAUGHT
        RemoveUltimateBallVFX();

        legacyHolder = catcher;
        holder = null;
        SetBallState(BallState.Held);
        catcher.SetHasBall(true);
        velocity = Vector3.zero;
        thrower = null;
        legacyThrower = null;
        targetOpponent = null;
        hasHitTarget = false;
        homingEnabled = false;

        // START HOLD TIMER FOR CAUGHT BALL
        ballHoldStartTime = Time.time;
        ResetHoldTimer();

        if (debugMode)
        {
            Debug.Log($"{catcher.name} caught the ball! Hold timer started at {ballHoldStartTime}");
        }
    }

    public void ThrowBall(Vector3 direction, float power)
    {
        if (currentState != BallState.Held) return;

        // FIXED: STOP HOLD TIMER WHEN BALL IS THROWN
        StopHoldTimer();

        // Set thrower reference (support both systems)
        if (holder != null)
        {
            thrower = holder;
            legacyThrower = null;
        }
        else if (legacyHolder != null)
        {
            legacyThrower = legacyHolder;
            thrower = null;
        }

        hasHitTarget = false;

        // Detect if this is a jump throw
        bool isInAir = false;
        if (thrower != null)
        {
            isInAir = !thrower.IsGrounded();
        }
        else if (legacyThrower != null)
        {
            isInAir = !legacyThrower.IsGrounded();
        }

        isJumpThrow = isInAir;

        // Update throw type if not already set
        if (currentThrowType == ThrowType.Normal && isJumpThrow)
        {
            currentThrowType = ThrowType.JumpThrow;
        }

        // Notify collision system
        if (collisionSystem != null)
        {
            if (thrower != null)
            {
                collisionSystem.OnBallThrown(thrower);
            }
            else if (legacyThrower != null)
            {
                collisionSystem.OnBallThrownLegacy(legacyThrower);
            }
        }

        // Find target opponent
        targetOpponent = FindOpponentForThrower();

        Vector3 throwDirection;
        float throwSpeed = currentThrowSpeed;

        if (targetOpponent != null)
        {
            Vector3 throwPos = ballTransform.position;
            Vector3 targetPos = targetOpponent.position;

            if (isJumpThrow)
            {
                throwDirection = (targetPos - throwPos).normalized;
                currentThrowType = ThrowType.JumpThrow;
            }
            else
            {
                Vector3 horizontalDirection = new Vector3(
                    targetPos.x - throwPos.x,
                    0f,
                    targetPos.z - throwPos.z
                );
                throwDirection = horizontalDirection.normalized;
            }

            if (debugMode)
            {
                Debug.Log($"Throwing {currentThrowType} at {targetOpponent.name}: {currentDamage} damage");
            }
        }
        else
        {
            throwDirection = isJumpThrow ?
                new Vector3(1f, -0.5f, 0f).normalized :
                Vector3.right;
            throwSpeed = currentThrowSpeed;
        }

        // Set velocity
        velocity = throwDirection * throwSpeed * power;

        // Release from holder
        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }
        else if (legacyHolder != null)
        {
            legacyHolder.SetHasBall(false);
            legacyHolder = null;
        }

        SetBallState(BallState.Thrown);
        ApplyUltimateBallVFX();

        if (debugMode)
        {
            Debug.Log($"Ball thrown: {currentThrowType}, Damage: {currentDamage}, Speed: {throwSpeed}");
        }
    }

    /// <summary>
    /// Apply ultimate ball VFX when ball is thrown as ultimate
    /// </summary>
    public void ApplyUltimateBallVFX()
    {
        if (currentThrowType != ThrowType.Ultimate) return;

        PlayerCharacter ultimateThrower = thrower;
        if (ultimateThrower == null) return;

        // Don't apply if already has VFX
        if (hasUltimateBallVFX && ultimateBallVFXInstance != null) return;

        CharacterData characterData = ultimateThrower.GetCharacterData();
        if (characterData == null) return;

        // Get and spawn ultimate ball VFX
        GameObject ballVFXPrefab = characterData.GetUltimateBallVFX();
        if (ballVFXPrefab != null)
        {
            ultimateBallVFXInstance = Instantiate(ballVFXPrefab, ballTransform.position, Quaternion.identity);
            ultimateBallVFXInstance.transform.SetParent(ballTransform);
            ultimateBallVFXInstance.transform.localPosition = Vector3.zero;
            hasUltimateBallVFX = true;

            if (debugMode)
            {
                Debug.Log($"Applied ultimate ball VFX for {ultimateThrower.name}");
            }
        }
    }

    /// <summary>
    /// Remove ultimate ball VFX (called when ball hits or is destroyed)
    /// </summary>
    public void RemoveUltimateBallVFX()
    {
        if (hasUltimateBallVFX && ultimateBallVFXInstance != null)
        {
            Destroy(ultimateBallVFXInstance);
            ultimateBallVFXInstance = null;
            hasUltimateBallVFX = false;

            if (debugMode)
            {
                Debug.Log("Ultimate ball VFX removed");
            }
        }
    }

    public void ResetBall()
    {
        Vector3 spawnPosition = new Vector3(0, 2f, 0);
        ballTransform.position = spawnPosition;
        velocity = Vector3.zero;
        hasHitTarget = false;

        // REMOVE ULTIMATE BALL VFX WHEN BALL RESETS
        RemoveUltimateBallVFX();

        // Reset wall collision state
        currentWallBounces = 0;
        hasHitWallThisFrame = false;

        // RESET HOLD TIMER completely
        StopHoldTimer();

        // Clear holders
        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }
        if (legacyHolder != null)
        {
            legacyHolder.SetHasBall(false);
            legacyHolder = null;
        }

        thrower = null;
        legacyThrower = null;
        targetOpponent = null;
        isJumpThrow = false;
        homingEnabled = false;

        // Reset throw data
        currentThrowType = ThrowType.Normal;
        currentDamage = 10;
        currentThrowSpeed = normalThrowSpeed;

        if (collisionSystem != null)
        {
            collisionSystem.OnBallReset();
        }

        SetBallState(BallState.Free);

        if (debugMode)
        {
            Debug.Log("Ball reset to center!");
        }
    }

    public void SetBallState(BallState newState)
    {
        currentState = newState;
        switch (newState)
        {
            case BallState.Free:
                // REMOVE ULTIMATE BALL VFX WHEN BALL BECOMES FREE
                RemoveUltimateBallVFX();

                thrower = null;
                legacyThrower = null;
                targetOpponent = null;
                hasHitTarget = false;
                isJumpThrow = false;
                homingEnabled = false;
                StopHoldTimer();
                if (collisionSystem != null)
                {
                    collisionSystem.OnBallReset();
                }
                break;
            case BallState.Held:
                // REMOVE ULTIMATE BALL VFX WHEN BALL IS HELD
                RemoveUltimateBallVFX();

                velocity = Vector3.zero;
                // Hold timer is started in pickup/catch methods
                break;
            case BallState.Thrown:
                StopHoldTimer();
                break;
        }
    }

    // Continue with the rest of the existing methods...
    // (HandleNeoGeoPhysics, ApplyHomingBehavior, CheckPickupRange, etc.)

    void HandleNeoGeoPhysics()
    {
        if (debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Neo Geo Physics: {currentThrowType}, Speed: {velocity.magnitude:F1}, Damage: {currentDamage}");
        }
    }

    void ApplyHomingBehavior()
    {
        if (targetOpponent != null)
        {
            Vector3 directionToTarget = (targetOpponent.position - ballTransform.position).normalized;
            velocity = Vector3.Slerp(velocity, directionToTarget * velocity.magnitude, 2f * Time.deltaTime);
        }
    }

    void CheckPickupRange()
    {
        if (currentState != BallState.Free) return;

        PlayerCharacter[] players = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in players)
        {
            float distance = Vector3.Distance(ballTransform.position, player.transform.position);
            if (distance <= pickupRange)
            {
                ShowPickupIndicator(true);
                return;
            }
        }

        GameObject legacyPlayer = GameObject.FindGameObjectWithTag("Player");
        if (legacyPlayer != null)
        {
            float distance = Vector3.Distance(ballTransform.position, legacyPlayer.transform.position);
            if (distance <= pickupRange)
            {
                ShowPickupIndicator(true);
                return;
            }
        }

        ShowPickupIndicator(false);
    }

    void CheckGrounded()
    {
        Vector3 rayStart = ballTransform.position;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayer);
        Debug.DrawRay(rayStart, Vector3.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
    }

    void UpdateVisuals()
    {
        if (currentState == BallState.Thrown || velocity.magnitude > 0.1f)
        {
            ballTransform.Rotate(Vector3.right * rotationSpeed * Time.deltaTime, Space.Self);
        }

        // Don't override ball color if hold timer is active
        if (ballRenderer != null && !isShowingWarning && !isInDangerPhase && !hasAppliedPenalty)
        {
            Color ballColor = currentState switch
            {
                BallState.Free => availableColor,
                BallState.Held => heldColor,
                BallState.Thrown => GetThrowTypeColor(),
                _ => availableColor
            };
            ballRenderer.material.color = ballColor;
        }
    }

    Color GetThrowTypeColor()
    {
        return currentThrowType switch
        {
            ThrowType.Ultimate => Color.red,
            ThrowType.JumpThrow => Color.green,
            _ => Color.red
        };
    }

    void ShowPickupIndicator(bool show)
    {
        if (show && currentState == BallState.Free)
        {
            float scale = 1f + Mathf.Sin(Time.time * 8f) * 0.1f;
            ballTransform.localScale = Vector3.one * scale;
        }
        else
        {
            ballTransform.localScale = Vector3.one;
        }
    }

    Transform FindOpponentForThrower()
    {
        Transform throwerTransform = null;
        if (thrower != null)
        {
            throwerTransform = thrower.transform;
        }
        else if (legacyThrower != null)
        {
            throwerTransform = legacyThrower.transform;
        }

        if (throwerTransform == null) return null;

        // Find all characters and return first valid opponent
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player.transform == throwerTransform) continue;
            return player.transform;
        }

        // Legacy support
        CharacterController[] allLegacyPlayers = FindObjectsOfType<CharacterController>();
        foreach (CharacterController player in allLegacyPlayers)
        {
            if (player.transform == throwerTransform) continue;
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null) continue;
            return player.transform;
        }

        // Fallback: Look for dummy opponent
        GameObject dummy = GameObject.Find("DummyOpponent");
        return dummy?.transform;
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC GETTERS AND SETTERS
    // ═══════════════════════════════════════════════════════════════

    public void SetThrowData(ThrowType throwType, int damage, float throwSpeed)
    {
        currentThrowType = throwType;
        currentDamage = damage;
        currentThrowSpeed = throwSpeed;

        if (debugMode)
        {
            Debug.Log($"Ball throw data set: {throwType}, {damage} damage, {throwSpeed} speed");
        }
    }

    /// <summary>
    /// Set the thrower for this ball (needed for multithrow balls)
    /// </summary>
    public void SetThrower(PlayerCharacter newThrower)
    {
        thrower = newThrower;
        legacyThrower = null;
    }
    public void OnCatchFailed()
    {
        velocity *= 0.8f;
        velocity.x += Random.Range(-2f, 2f);
        velocity.y += Random.Range(1f, 3f);

        if (debugMode)
        {
            Debug.Log("Ball catch failed - ball deflected!");
        }
    }

    public void EnableHoming(bool enable)
    {
        homingEnabled = enable;
        if (debugMode)
        {
            Debug.Log($"Ball homing {(enable ? "enabled" : "disabled")}");
        }
    }

    // Ball Hold Timer Getters
    public bool IsInWarningPhase() => isShowingWarning;
    public bool IsInDangerPhase() => isInDangerPhase;
    public bool IsInPenaltyPhase() => hasAppliedPenalty;
    public float GetMaxHoldTime() => maxHoldTime;
    public float GetTimeUntilWarning() => Mathf.Max(0f, warningStartTime - GetHoldDuration());
    public float GetTimeUntilDanger() => Mathf.Max(0f, dangerStartTime - GetHoldDuration());
    public float GetTimeUntilPenalty() => Mathf.Max(0f, maxHoldTime - GetHoldDuration());

    // Legacy compatibility methods
    public BallState GetBallState() => currentState;
    public bool IsHeld() => currentState == BallState.Held;
    public bool IsFree() => currentState == BallState.Free;
    public Transform GetCurrentTarget() => targetOpponent;
    public Vector3 GetVelocity() => velocity;
    public void SetVelocity(Vector3 newVelocity) => velocity = newVelocity;
    public void SetBallStatePublic(BallState newState) => SetBallState(newState);
    public int GetCurrentDamage() => currentDamage;
    public ThrowType GetThrowType() => currentThrowType;
    public PlayerCharacter GetHolder() => holder;
    public CharacterController GetHolderLegacy() => legacyHolder;
    public PlayerCharacter GetThrower() => thrower;
    public CharacterController GetThrowerLegacy() => legacyThrower;

    // ═══════════════════════════════════════════════════════════════
    // DEBUG VISUALIZATION
    // ═══════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        // Draw ground check
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);

        // Draw trajectory line
        if (currentState == BallState.Thrown && useNeoGeoPhysics)
        {
            Gizmos.color = isJumpThrow ? Color.green : Color.cyan;
            Vector3 trajectoryEnd = transform.position + velocity.normalized * 8f;
            Gizmos.DrawLine(transform.position, trajectoryEnd);

            if (isJumpThrow)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one * 0.5f);
            }
        }

        // Draw target opponent
        if (targetOpponent != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, targetOpponent.position);
            Gizmos.DrawWireSphere(targetOpponent.position, 0.5f);
        }

        // Draw hold timer info when ball is held
        if (currentState == BallState.Held)
        {
            Transform holderTransform = holder?.transform ?? legacyHolder?.transform;
            if (holderTransform != null)
            {
                Vector3 holdPos = CalculateHoldPosition(holderTransform);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(holdPos, 0.3f);
                Gizmos.DrawLine(holderTransform.position, holdPos);

                // Show hold timer phase with color
                if (hasAppliedPenalty)
                {
                    Gizmos.color = corruptionColor;
                }
                else if (isInDangerPhase)
                {
                    Gizmos.color = dangerColor;
                }
                else if (isShowingWarning)
                {
                    Gizmos.color = warningColor;
                }
                else
                {
                    Gizmos.color = Color.green;
                }

                // Draw timer progress circle
                float progress = GetHoldProgress();
                Vector3 timerPos = transform.position + Vector3.up * 1.5f;
                Gizmos.DrawWireSphere(timerPos, 0.5f * progress);
            }
        }

        // Draw character info if held by a character
        if (holder != null && holder.GetCharacterData() != null)
        {
            CharacterData data = holder.GetCharacterData();
            Gizmos.color = data.characterColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.4f);
        }

        // NEW: Draw arena bounds
        Gizmos.color = Color.cyan;

        // Left wall
        Vector3 leftTop = new Vector3(arenaLeftBound, arenaTopBound, 0);
        Vector3 leftBottom = new Vector3(arenaLeftBound, arenaBottomBound, 0);
        Gizmos.DrawLine(leftTop, leftBottom);

        // Right wall
        Vector3 rightTop = new Vector3(arenaRightBound, arenaTopBound, 0);
        Vector3 rightBottom = new Vector3(arenaRightBound, arenaBottomBound, 0);
        Gizmos.DrawLine(rightTop, rightBottom);

        // Top wall
        Gizmos.DrawLine(leftTop, rightTop);

        // Bottom wall
        Gizmos.DrawLine(leftBottom, rightBottom);
    }

    void OnGUI()
    {
        if (!debugMode) return;

        // Hold Timer Debug Info
        if (currentState == BallState.Held)
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label("=== BALL HOLD TIMER DEBUG ===");
            GUILayout.Label($"Hold Duration: {GetHoldDuration():F2}s / {maxHoldTime}s");
            GUILayout.Label($"Progress: {GetHoldProgress():P0}");
            GUILayout.Label($"Current Phase: {GetCurrentHoldPhase()}");
            GUILayout.Label($"Timer Start: {ballHoldStartTime:F2}");
            GUILayout.Label($"Current Time: {Time.time:F2}");

            if (isShowingWarning)
            {
                GUILayout.Label($"⚠️ WARNING: {GetTimeUntilDanger():F1}s until danger");
            }

            if (isInDangerPhase)
            {
                GUILayout.Label($"🔶 DANGER: {GetTimeUntilPenalty():F1}s until penalty");
            }

            if (hasAppliedPenalty)
            {
                GUILayout.Label($"💀 PENALTY: Ball will be dropped/reset!");
                if (ballDropCoroutine != null)
                {
                    GUILayout.Label($"⏱️ Ball drops in: {ballDropDelay:F1}s");
                }
            }

            string holderName = holder?.name ?? legacyHolder?.name ?? "None";
            GUILayout.Label($"Holder: {holderName}");

            // Timer progress bar
            Rect progressRect = GUILayoutUtility.GetRect(350, 20);
            GUI.Box(progressRect, "");

            Rect fillRect = new Rect(progressRect.x, progressRect.y,
                progressRect.width * GetHoldProgress(), progressRect.height);

            Color barColor = hasAppliedPenalty ? corruptionColor :
                           isInDangerPhase ? dangerColor :
                           isShowingWarning ? warningColor : Color.green;

            GUI.color = barColor;
            GUI.Box(fillRect, "");
            GUI.color = Color.white;

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}