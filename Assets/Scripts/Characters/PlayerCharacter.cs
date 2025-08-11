using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main character controller that applies CharacterData stats to gameplay
/// Integrates with existing PlayerInputHandler and BallController systems
/// </summary>
public class PlayerCharacter : MonoBehaviour
{
    [Header("Character Setup")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private bool autoLoadCharacterOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showCharacterInfo = true;

    // Core Components (existing systems)
    private PlayerInputHandler inputHandler;
    private CapsuleCollider characterCollider;
    private CatchSystem catchSystem;
    private PlayerHealth playerHealth;
    private AudioSource audioSource;

    // Movement variables
    private Transform characterTransform;
    private Vector3 velocity;
    private bool isGrounded = false;
    private bool isDucking = false;
    private bool hasBall = false;
    private bool movementEnabled = true;

    // Character-specific state
    private bool hasDoubleJumped = false;
    private bool canDash = true;
    private float lastDashTime = 0f;
    private bool isDashing = false;
    private float currentUltimateCharge = 0f;

    // Original collider dimensions for ducking
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;
    private bool duckingStateChanged = false;

    // Ground check settings
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float groundCheckDistance = 0.1f;

    // Events for other systems
    public System.Action<CharacterData> OnCharacterLoaded;
    public System.Action<float> OnUltimateChargeChanged;
    public System.Action OnUltimateActivated;

    void Awake()
    {
        CacheComponents();

        if (autoLoadCharacterOnStart && characterData != null)
        {
            LoadCharacter(characterData);
        }
    }

    void CacheComponents()
    {
        characterTransform = transform;
        characterCollider = GetComponent<CapsuleCollider>();
        inputHandler = GetComponent<PlayerInputHandler>();
        catchSystem = GetComponent<CatchSystem>();
        playerHealth = GetComponent<PlayerHealth>();
        audioSource = GetComponent<AudioSource>();

        // Validate critical components
        if (inputHandler == null)
        {
            Debug.LogError($"{gameObject.name} - PlayerInputHandler component is missing!");
        }

        // Store original collider dimensions for ducking
        if (characterCollider != null)
        {
            originalColliderHeight = characterCollider.height;
            originalColliderCenter = characterCollider.center;
        }

        // Setup audio source
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }
    }

    void Update()
    {
        if (characterData == null) return;

        HandleInput();
        CheckGrounded();
        HandleMovement();
        HandleDucking();
        HandleBallInteraction();
        UpdateUltimateCharge();
    }

    /// <summary>
    /// Load a new character and apply all stats
    /// </summary>
    public void LoadCharacter(CharacterData newCharacterData)
    {
        characterData = newCharacterData;
        ApplyCharacterStats();
        OnCharacterLoaded?.Invoke(characterData);

        if (debugMode)
        {
            Debug.Log($"Loaded character: {characterData.characterName}");
        }
    }

    /// <summary>
    /// Apply character stats to all systems
    /// </summary>
    void ApplyCharacterStats()
    {
        if (characterData == null) return;

        // Apply health stats
        if (playerHealth != null)
        {
            playerHealth.SetMaxHealth(characterData.maxHealth);
            playerHealth.SetHealth(characterData.maxHealth);
        }

        // Reset character-specific state
        currentUltimateCharge = 0f;
        hasDoubleJumped = false;
        canDash = true;

        // Apply visual changes if character has prefab
        ApplyCharacterVisuals();

        if (debugMode)
        {
            Debug.Log($"Applied stats for {characterData.characterName}: " +
                     $"Speed={characterData.moveSpeed}, Health={characterData.maxHealth}, " +
                     $"Jump={characterData.jumpHeight}");
        }
    }

    void ApplyCharacterVisuals()
    {
        // Apply character color to renderer
        Renderer characterRenderer = GetComponentInChildren<Renderer>();
        if (characterRenderer != null && characterData.characterColor != Color.white)
        {
            characterRenderer.material.color = characterData.characterColor;
        }

        // TODO: Instantiate character prefab if provided
        // This would replace the basic capsule with the actual character model
    }

    void HandleInput()
    {
        if (inputHandler == null || characterData == null) return;

        // Jump input (support double jump if character has it)
        if (inputHandler.GetJumpPressed())
        {
            if (isGrounded && !isDucking)
            {
                Jump();
            }
            else if (characterData.canDoubleJump && !hasDoubleJumped && !isGrounded)
            {
                DoubleJump();
            }
        }

        // Dash input (if character can dash)
        if (inputHandler.GetDashPressed() && characterData.canDash && CanDash())
        {
            PerformDash();
        }

        // Ultimate input
        if (inputHandler.GetUltimatePressed() && CanUseUltimate())
        {
            ActivateUltimate();
        }

        // Duck input - state-based crouching
        bool duckInput = inputHandler.GetDuckHeld() && isGrounded;

        // Check if ducking state changed
        if (duckInput != isDucking)
        {
            isDucking = duckInput;
            duckingStateChanged = true;
        }
    }

    void HandleMovement()
    {
        if (characterData == null || !movementEnabled) return;

        // Get horizontal input
        float horizontalInput = inputHandler.GetHorizontal();

        // Don't move while dashing or ducking
        if (isDashing || isDucking) return;

        // Apply character's move speed
        if (horizontalInput != 0)
        {
            Vector3 moveDirection = Vector3.right * horizontalInput * characterData.moveSpeed;
            characterTransform.Translate(moveDirection * Time.deltaTime, Space.World);
        }

        // Apply gravity when not grounded
        if (!isGrounded)
        {
            velocity.y -= 25f * Time.deltaTime; // Custom gravity
        }
        else
        {
            // Reset vertical velocity when grounded
            if (velocity.y < 0)
            {
                velocity.y = 0f;
                hasDoubleJumped = false; // Reset double jump when landing
            }
        }

        // Apply vertical movement
        characterTransform.Translate(Vector3.up * velocity.y * Time.deltaTime, Space.World);
    }

    void Jump()
    {
        if (characterData == null) return;

        velocity.y = characterData.jumpHeight;
        isGrounded = false;

        // Play jump sound
        PlayCharacterSound(CharacterAudioType.Jump);

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} Jumped! (Height: {characterData.jumpHeight})");
        }
    }

    void DoubleJump()
    {
        if (characterData == null || hasDoubleJumped) return;

        velocity.y = characterData.jumpHeight * 0.8f; // Double jump is slightly weaker
        hasDoubleJumped = true;

        // Play jump sound
        PlayCharacterSound(CharacterAudioType.Jump);

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} Double Jumped!");
        }
    }

    bool CanDash()
    {
        if (!canDash || isDashing) return false;

        float timeSinceLastDash = Time.time - lastDashTime;
        return timeSinceLastDash >= characterData.GetDashCooldown();
    }

    void PerformDash()
    {
        if (characterData == null) return;

        StartCoroutine(DashCoroutine());
    }

    IEnumerator DashCoroutine()
    {
        isDashing = true;
        lastDashTime = Time.time;
        canDash = false;

        // Get dash direction from input
        float horizontalInput = inputHandler.GetHorizontal();
        Vector3 dashDirection = horizontalInput != 0 ?
            Vector3.right * Mathf.Sign(horizontalInput) :
            characterTransform.right; // Default to facing direction

        // Perform dash movement
        float dashSpeed = characterData.GetDashDistance() / characterData.GetDashDuration();
        float elapsed = 0f;

        // Play dash sound and effect
        PlayCharacterSound(CharacterAudioType.Dash);
        if (characterData.dashEffect != null)
        {
            Instantiate(characterData.dashEffect, characterTransform.position, Quaternion.identity);
        }

        while (elapsed < characterData.GetDashDuration())
        {
            characterTransform.Translate(dashDirection * dashSpeed * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }

        isDashing = false;

        // Reset dash availability after cooldown
        yield return new WaitForSeconds(characterData.GetDashCooldown());
        canDash = true;

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} Dash completed!");
        }
    }

    void HandleDucking()
    {
        if (characterCollider == null || !duckingStateChanged) return;

        if (isDucking)
        {
            // Duck down - reduce collider height
            characterCollider.height = originalColliderHeight * 0.5f;
            characterCollider.center = new Vector3(
                originalColliderCenter.x,
                originalColliderCenter.y - (originalColliderHeight * 0.25f),
                originalColliderCenter.z
            );
        }
        else
        {
            // Stand up - restore original collider dimensions
            characterCollider.height = originalColliderHeight;
            characterCollider.center = originalColliderCenter;
        }

        duckingStateChanged = false;
    }

    void HandleBallInteraction()
    {
        if (BallManager.Instance == null || inputHandler == null || characterData == null) return;

        // Pickup ball
        if (inputHandler.GetPickupPressed() && !hasBall)
        {
            BallManager.Instance.RequestBallPickup(this);
        }

        // Throw ball with character-specific damage
        if (inputHandler.GetThrowPressed() && hasBall)
        {
            ThrowBall();
        }
    }

    #region Special Throw Types

    ThrowType DetermineThrowType()
    {
        // Check if this is a jump throw
        if (!isGrounded)
        {
            return ThrowType.JumpThrow;
        }

        // Check for special throw based on character's specialThrowType
        if (characterData.specialThrowType != ThrowType.Normal)
        {
            return characterData.specialThrowType;
        }

        return ThrowType.Normal;
    }

    void ThrowBall()
    {
        if (!hasBall || BallManager.Instance == null || characterData == null) return;

        // Determine throw type based on character state and special abilities
        ThrowType throwType = DetermineThrowType();

        // Get damage directly from character data
        int throwDamage = characterData.GetThrowDamage(throwType);

        // Apply special throw modifications
        ApplySpecialThrowBehavior(throwType, throwDamage);

        // Play throw sound
        PlayCharacterSound(CharacterAudioType.Throw);

        // Add ultimate charge for throwing
        AddUltimateCharge(15f);

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} threw {throwType} for {throwDamage} damage!");
        }
    }

    void ApplySpecialThrowBehavior(ThrowType throwType, int damage)
    {
        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall == null) return;

        switch (throwType)
        {
            case ThrowType.Normal:
                ExecuteNormalThrow(damage);
                break;

            case ThrowType.JumpThrow:
                ExecuteJumpThrow(damage);
                break;

            case ThrowType.PowerThrow:
                ExecutePowerThrow(damage);
                break;

            case ThrowType.CurveThrow:
                ExecuteCurveThrow(damage);
                break;

            case ThrowType.MultiThrow:
                StartCoroutine(ExecuteMultiThrowSpecial(damage));
                break;

            default:
                ExecuteNormalThrow(damage);
                break;
        }
    }

    void ExecuteNormalThrow(int damage)
    {
        BallManager.Instance.RequestBallThrowWithCharacterData(this, characterData, ThrowType.Normal, damage);
    }

    void ExecuteJumpThrow(int damage)
    {
        BallManager.Instance.RequestBallThrowWithCharacterData(this, characterData, ThrowType.JumpThrow, damage);

        // Add extra visual effect for jump throws
        if (characterData.throwEffect != null)
        {
            GameObject effect = Instantiate(characterData.throwEffect, transform.position, Quaternion.identity);
            Destroy(effect, 1f);
        }
    }

    void ExecutePowerThrow(int damage)
    {
        // Power throw: Extra damage and speed
        int powerDamage = Mathf.RoundToInt(damage * 1.3f);

        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall != null)
        {
            currentBall.SetThrowData(ThrowType.PowerThrow, powerDamage, characterData.GetThrowSpeed(25f));

            Vector3 direction = Vector3.right; // BallController will handle targeting
            currentBall.ThrowBall(direction, 1.2f); // Extra power

            // Screen shake for power
            CameraController camera = FindObjectOfType<CameraController>();
            if (camera != null)
            {
                camera.ShakeCamera(0.4f, 0.3f);
            }

            if (debugMode)
            {
                Debug.Log($"?? POWER THROW: {powerDamage} damage at high speed!");
            }
        }
    }

    void ExecuteCurveThrow(int damage)
    {
        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall != null)
        {
            currentBall.SetThrowData(ThrowType.CurveThrow, damage, characterData.GetThrowSpeed(20f));

            Vector3 direction = Vector3.right;
            currentBall.ThrowBall(direction, 1f);

            // Apply curve behavior after throw
            StartCoroutine(ApplyCurveBehavior(currentBall));

            if (debugMode)
            {
                Debug.Log($"??? CURVE THROW: Ball will curve mid-flight!");
            }
        }
    }

    IEnumerator ExecuteMultiThrowSpecial(int damage)
    {
        // Special multi-throw (different from ultimate version)
        int ballCount = 3;
        int damagePerBall = damage / 2; // Spread damage

        for (int i = 0; i < ballCount; i++)
        {
            GameObject ballObj = Instantiate(BallManager.Instance.ballPrefab,
                transform.position + Vector3.up * 1.5f + Vector3.right * (i * 0.3f - 0.3f),
                Quaternion.identity);

            BallController ballController = ballObj.GetComponent<BallController>();
            if (ballController != null)
            {
                ballController.SetThrowData(ThrowType.MultiThrow, damagePerBall, 20f);

                // Slight spread
                float angle = (i - 1) * 10f; // -10, 0, 10 degrees
                Vector3 throwDir = Quaternion.Euler(0, angle, 0) * Vector3.right;
                throwDir.y = 0.05f;

                ballController.ThrowBall(throwDir.normalized, 1f);
            }

            yield return new WaitForSeconds(0.2f);
        }

        if (debugMode)
        {
            Debug.Log($"?? MULTI THROW SPECIAL: {ballCount} balls x {damagePerBall} damage!");
        }
    }

    IEnumerator ApplyCurveBehavior(BallController ball)
    {
        yield return new WaitForSeconds(0.3f); // Let ball travel straight first

        float curveDuration = 1f;
        float elapsed = 0f;
        float curveIntensity = 8f;
        bool curveLeft = Random.Range(0f, 1f) > 0.5f; // Random curve direction

        while (elapsed < curveDuration && ball != null && ball.GetBallState() == BallController.BallState.Thrown)
        {
            Vector3 velocity = ball.GetVelocity();

            // Apply curve force
            float curveForce = Mathf.Sin(elapsed * 4f) * curveIntensity * (curveLeft ? -1f : 1f);
            velocity.z += curveForce * Time.deltaTime;

            ball.SetVelocity(velocity);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    void UpdateUltimateCharge()
    {
        // Only update UI if charge changed
        OnUltimateChargeChanged?.Invoke(currentUltimateCharge / characterData.ultimateChargeRequired);

        if (debugMode && currentUltimateCharge >= characterData.ultimateChargeRequired)
        {
            // Flash ready indicator every 2 seconds
            if (Time.time % 2f < 0.1f)
            {
                Debug.Log($"{characterData.characterName} Ultimate Ready! Press U to activate!");
            }
        }
    }

    public void AddUltimateCharge(float amount)
    {
        if (characterData == null) return;

        // Don't add charge if already at max
        if (currentUltimateCharge >= characterData.ultimateChargeRequired) return;

        float chargeToAdd = amount * characterData.ultimateChargeRate;
        float previousCharge = currentUltimateCharge;
        currentUltimateCharge = Mathf.Min(currentUltimateCharge + chargeToAdd, characterData.ultimateChargeRequired);

        OnUltimateChargeChanged?.Invoke(currentUltimateCharge / characterData.ultimateChargeRequired);

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} Ultimate Charge: {previousCharge:F1} + {chargeToAdd:F1} = {currentUltimateCharge:F1}/{characterData.ultimateChargeRequired}");

            if (currentUltimateCharge >= characterData.ultimateChargeRequired)
            {
                Debug.Log($"?? {characterData.characterName} ULTIMATE READY! ??");
            }
        }
    }

    bool CanUseUltimate()
    {
        return characterData != null && currentUltimateCharge >= characterData.ultimateChargeRequired;
    }

    // FIXED: Add more ways to gain ultimate charge
    public void OnDamageTaken(int damage)
    {
        AddUltimateCharge(damage * 0.5f); // Gain charge when taking damage
    }

    public void OnSuccessfulCatch()
    {
        AddUltimateCharge(25f); // Good reward for catching
    }

    public void OnSuccessfulDodge()
    {
        AddUltimateCharge(20f); // Reward for dodging
    }

    #region Ultimate System

    void ActivateUltimate()
    {
        if (!CanUseUltimate())
        {
            if (debugMode)
            {
                Debug.Log($"{characterData.characterName} ultimate not ready! Charge: {currentUltimateCharge:F1}/{characterData.ultimateChargeRequired}");
            }
            return;
        }

        // Check if ultimate requires ball
        bool requiresBall = UltimateRequiresBall(characterData.ultimateType);

        if (requiresBall && !hasBall)
        {
            if (debugMode)
            {
                Debug.Log($"{characterData.characterName} ultimate requires ball but player doesn't have one!");
            }
            return;
        }

        if (debugMode)
        {
            Debug.Log($"?? {characterData.characterName} activated {characterData.ultimateType}!");
        }

        // Execute ultimate based on type
        ExecuteUltimateAbility();

        // CONSUME ultimate charge AFTER successful activation
        currentUltimateCharge = 0f;
        OnUltimateChargeChanged?.Invoke(0f);

        OnUltimateActivated?.Invoke();
    }

    bool UltimateRequiresBall(UltimateAbilityType ultimateType)
    {
        switch (ultimateType)
        {
            case UltimateAbilityType.PowerThrow:
            case UltimateAbilityType.MultiThrow:
            case UltimateAbilityType.HomingBall:
            case UltimateAbilityType.GravitySlam:
            case UltimateAbilityType.ExplosiveBall:
            case UltimateAbilityType.Curveball:
                return true;

            case UltimateAbilityType.SpeedBoost:
            case UltimateAbilityType.Shield:
            case UltimateAbilityType.TimeFreeze:
            case UltimateAbilityType.Teleport:
                return false;

            default:
                return false;
        }
    }

    void ExecuteUltimateAbility()
    {
        // Play ultimate sound and effects
        PlayCharacterSound(CharacterAudioType.Ultimate);
        if (characterData.ultimateEffect != null)
        {
            Instantiate(characterData.ultimateEffect, characterTransform.position, Quaternion.identity);
        }

        switch (characterData.ultimateType)
        {
            case UltimateAbilityType.PowerThrow:
                ExecutePowerThrowUltimate();
                break;

            case UltimateAbilityType.MultiThrow:
                StartCoroutine(ExecuteMultiThrowUltimate());
                break;

            case UltimateAbilityType.GravitySlam:
                ExecuteGravitySlamUltimate();
                break;

            case UltimateAbilityType.HomingBall:
                ExecuteHomingBallUltimate();
                break;

            case UltimateAbilityType.ExplosiveBall:
                ExecuteExplosiveBallUltimate();
                break;

            case UltimateAbilityType.TimeFreeze:
                StartCoroutine(ExecuteTimeFreezeUltimate());
                break;

            case UltimateAbilityType.Shield:
                StartCoroutine(ExecuteShieldUltimate());
                break;

            case UltimateAbilityType.SpeedBoost:
                StartCoroutine(ExecuteSpeedBoostUltimate());
                break;

            case UltimateAbilityType.Teleport:
                ExecuteTeleportUltimate();
                break;

            case UltimateAbilityType.Curveball:
                ExecuteCurveballUltimate();
                break;

            default:
                Debug.LogWarning($"Ultimate type {characterData.ultimateType} not implemented yet");
                break;
        }
    }

    #region Ball-Based Ultimates

    void ExecutePowerThrowUltimate()
    {
        if (!hasBall) return;

        // Massive damage throw with screen effects
        int ultimateDamage = characterData.GetThrowDamage(ThrowType.Ultimate);
        BallManager.Instance.RequestBallThrowWithCharacterData(this, characterData, ThrowType.Ultimate, ultimateDamage);

        // Screen effects
        CameraController camera = FindObjectOfType<CameraController>();
        if (camera != null)
        {
            camera.ShakeCamera(1.2f, 1.5f);
        }

        if (debugMode)
        {
            Debug.Log($"?? POWER THROW: {ultimateDamage} damage!");
        }
    }

    IEnumerator ExecuteMultiThrowUltimate()
    {
        if (!hasBall) yield break;

        int ballCount = 5;
        int damagePerBall = characterData.GetThrowDamage(ThrowType.Ultimate) / 3; // Spread damage
        float throwSpeed = 22f;

        for (int i = 0; i < ballCount; i++)
        {
            // Create temporary ball
            GameObject ballObj = Instantiate(BallManager.Instance.ballPrefab,
                transform.position + Vector3.up * 2f + Vector3.right * (i * 0.2f - 0.4f),
                Quaternion.identity);

            BallController ballController = ballObj.GetComponent<BallController>();
            if (ballController != null)
            {
                ballController.SetThrowData(ThrowType.Ultimate, damagePerBall, throwSpeed);

                // Spread pattern
                float angle = (i - 2) * 15f; // -30, -15, 0, 15, 30 degrees
                Vector3 throwDir = Quaternion.Euler(0, angle, 0) * Vector3.right;
                throwDir.y = 0.1f; // Slight upward angle

                ballController.ThrowBall(throwDir.normalized, 1f);

                // Add visual trail effect
                TrailRenderer trail = ballController.GetComponent<TrailRenderer>();
                if (trail != null)
                {
                    trail.enabled = true;
                    trail.startColor = characterData.characterColor;
                    trail.endColor = Color.clear;
                }
            }

            yield return new WaitForSeconds(0.15f); // Rapid fire
        }

        if (debugMode)
        {
            Debug.Log($"?? MULTI THROW: {ballCount} balls x {damagePerBall} damage!");
        }
    }

    void ExecuteGravitySlamUltimate()
    {
        if (!hasBall) return;

        StartCoroutine(GravitySlamCoroutine());
    }

    IEnumerator GravitySlamCoroutine()
    {
        // Phase 1: Launch ball high
        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall != null)
        {
            Vector3 highArcDirection = new Vector3(0.6f, 1.4f, 0f).normalized; // High arc
            int ultimateDamage = characterData.GetThrowDamage(ThrowType.Ultimate);

            currentBall.SetThrowData(ThrowType.Ultimate, ultimateDamage, 18f);
            currentBall.ThrowBall(highArcDirection, 1.2f);

            if (debugMode)
            {
                Debug.Log("?? GRAVITY SLAM: Phase 1 - Ball launched high!");
            }

            // Phase 2: Wait for peak, then apply massive downward force
            yield return new WaitForSeconds(0.8f);

            if (currentBall != null && currentBall.GetBallState() == BallController.BallState.Thrown)
            {
                Vector3 velocity = currentBall.GetVelocity();
                velocity.y = -35f; // Massive downward slam
                velocity.x = velocity.x > 0 ? 15f : -15f; // Maintain horizontal direction
                currentBall.SetVelocity(velocity);

                // Create slam effect at predicted impact point
                Vector3 impactPoint = new Vector3(currentBall.transform.position.x + (velocity.x * 0.5f), 0f, 0f);
                if (characterData.ultimateEffect != null)
                {
                    Instantiate(characterData.ultimateEffect, impactPoint, Quaternion.identity);
                }

                if (debugMode)
                {
                    Debug.Log("?? GRAVITY SLAM: Phase 2 - Massive downward force applied!");
                }
            }
        }
    }

    void ExecuteHomingBallUltimate()
    {
        if (!hasBall) return;

        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall != null)
        {
            // Enable aggressive homing
            currentBall.EnableHoming(true);

            // Throw with ultimate damage and special homing speed
            int ultimateDamage = characterData.GetThrowDamage(ThrowType.Ultimate);
            currentBall.SetThrowData(ThrowType.Ultimate, ultimateDamage, 25f);

            // Start with normal direction, homing will take over
            Vector3 direction = Vector3.right;
            currentBall.ThrowBall(direction, 1f);

            // Add homing visual effect
            StartCoroutine(HomingEffectCoroutine(currentBall));

            if (debugMode)
            {
                Debug.Log("?? HOMING BALL: Locked on target!");
            }
        }
    }

    IEnumerator HomingEffectCoroutine(BallController ball)
    {
        float duration = 3f;
        float elapsed = 0f;

        while (elapsed < duration && ball != null && ball.GetBallState() == BallController.BallState.Thrown)
        {
            // Create homing trail particles
            if (characterData.ultimateEffect != null && Random.Range(0f, 1f) < 0.3f)
            {
                GameObject effect = Instantiate(characterData.ultimateEffect, ball.transform.position, Quaternion.identity);
                Destroy(effect, 0.5f);
            }

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }
    }

    void ExecuteExplosiveBallUltimate()
    {
        if (!hasBall) return;

        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall != null)
        {
            int ultimateDamage = characterData.GetThrowDamage(ThrowType.Ultimate);
            currentBall.SetThrowData(ThrowType.Ultimate, ultimateDamage, 20f);

            // Normal throw, but add explosion behavior
            Vector3 direction = Vector3.right;
            currentBall.ThrowBall(direction, 1f);

            // Start explosion timer
            StartCoroutine(ExplosionTimerCoroutine(currentBall));

            if (debugMode)
            {
                Debug.Log("?? EXPLOSIVE BALL: Armed and dangerous!");
            }
        }
    }

    IEnumerator ExplosionTimerCoroutine(BallController ball)
    {
        float explosionTime = 2f;
        float elapsed = 0f;

        while (elapsed < explosionTime && ball != null)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ball != null)
        {
            // Create explosion effect
            Vector3 explosionPoint = ball.transform.position;

            if (characterData.ultimateEffect != null)
            {
                GameObject explosion = Instantiate(characterData.ultimateEffect, explosionPoint, Quaternion.identity);
                Destroy(explosion, 2f);
            }

            // Damage all players in explosion radius
            float explosionRadius = 4f;
            PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();

            foreach (PlayerCharacter player in allPlayers)
            {
                if (player != this)
                {
                    float distance = Vector3.Distance(player.transform.position, explosionPoint);
                    if (distance <= explosionRadius)
                    {
                        int explosionDamage = characterData.GetThrowDamage(ThrowType.Ultimate) / 2;
                        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                        if (playerHealth != null)
                        {
                            playerHealth.TakeDamage(explosionDamage, null);
                        }

                        if (debugMode)
                        {
                            Debug.Log($"?? EXPLOSION hit {player.name} for {explosionDamage} damage!");
                        }
                    }
                }
            }

            // Screen shake
            CameraController camera = FindObjectOfType<CameraController>();
            if (camera != null)
            {
                camera.ShakeCamera(0.8f, 1f);
            }

            // Remove the ball
            ball.ResetBall();
        }
    }

    void ExecuteCurveballUltimate()
    {
        if (!hasBall) return;

        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall != null)
        {
            int ultimateDamage = characterData.GetThrowDamage(ThrowType.Ultimate);
            currentBall.SetThrowData(ThrowType.Ultimate, ultimateDamage, 22f);

            // Throw normally, then apply curve
            Vector3 direction = Vector3.right;
            currentBall.ThrowBall(direction, 1f);

            // Start curve behavior
            StartCoroutine(CurveballCoroutine(currentBall));

            if (debugMode)
            {
                Debug.Log("??? CURVEBALL: Ball will curve mid-flight!");
            }
        }
    }

    IEnumerator CurveballCoroutine(BallController ball)
    {
        yield return new WaitForSeconds(0.5f); // Let ball travel normally first

        float curveDuration = 1.5f;
        float elapsed = 0f;
        float curveIntensity = 15f;

        while (elapsed < curveDuration && ball != null && ball.GetBallState() == BallController.BallState.Thrown)
        {
            Vector3 velocity = ball.GetVelocity();

            // Apply sinusoidal curve
            float curveForce = Mathf.Sin(elapsed * 6f) * curveIntensity;
            velocity.z += curveForce * Time.deltaTime;

            ball.SetVelocity(velocity);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    #region Non-Ball Ultimates

    IEnumerator ExecuteSpeedBoostUltimate()
    {
        float originalSpeed = characterData.moveSpeed;
        float boostMultiplier = 2.5f;
        float duration = 6f;

        // Apply speed boost
        characterData.moveSpeed *= boostMultiplier;

        // Visual effect
        GameObject speedEffect = null;
        if (characterData.ultimateEffect != null)
        {
            speedEffect = Instantiate(characterData.ultimateEffect, transform.position, Quaternion.identity);
            speedEffect.transform.SetParent(transform);
        }

        // Change character color temporarily
        Renderer characterRenderer = GetComponentInChildren<Renderer>();
        Color originalColor = characterRenderer?.material.color ?? Color.white;
        if (characterRenderer != null)
        {
            characterRenderer.material.color = Color.cyan;
        }

        if (debugMode)
        {
            Debug.Log($"? SPEED BOOST: {characterData.characterName} speed x{boostMultiplier} for {duration}s!");
        }

        yield return new WaitForSeconds(duration);

        // Restore original speed and appearance
        characterData.moveSpeed = originalSpeed;

        if (speedEffect != null)
        {
            Destroy(speedEffect);
        }

        if (characterRenderer != null)
        {
            characterRenderer.material.color = originalColor;
        }

        if (debugMode)
        {
            Debug.Log("? Speed boost ended");
        }
    }

    IEnumerator ExecuteShieldUltimate()
    {
        float duration = 4f;

        // Make player temporarily invulnerable
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.SetTemporaryInvulnerability(duration);
        }

        // Visual shield effect
        GameObject shieldEffect = null;
        if (characterData.ultimateEffect != null)
        {
            shieldEffect = Instantiate(characterData.ultimateEffect, transform.position, Quaternion.identity);
            shieldEffect.transform.SetParent(transform);
        }

        // Shield visual feedback
        Renderer characterRenderer = GetComponentInChildren<Renderer>();
        Color originalColor = characterRenderer?.material.color ?? Color.white;

        // Pulsing shield effect
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (characterRenderer != null)
            {
                float pulse = Mathf.Sin(elapsed * 8f) * 0.3f + 0.7f;
                characterRenderer.material.color = Color.Lerp(originalColor, Color.blue, pulse);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore appearance
        if (characterRenderer != null)
        {
            characterRenderer.material.color = originalColor;
        }

        if (shieldEffect != null)
        {
            Destroy(shieldEffect);
        }

        if (debugMode)
        {
            Debug.Log("??? Shield expired");
        }
    }

    IEnumerator ExecuteTimeFreezeUltimate()
    {
        float freezeDuration = 3f;

        // Find all other characters and freeze them
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        CharacterController[] allLegacyPlayers = FindObjectsOfType<CharacterController>();

        List<PlayerCharacter> frozenPlayers = new List<PlayerCharacter>();
        List<CharacterController> frozenLegacyPlayers = new List<CharacterController>();

        // Freeze PlayerCharacters
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player != this)
            {
                player.SetMovementEnabled(false);
                frozenPlayers.Add(player);

                // Visual freeze effect
                Renderer renderer = player.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.cyan;
                }
            }
        }

        // Freeze legacy CharacterControllers
        foreach (CharacterController player in allLegacyPlayers)
        {
            if (player != null)
            {
                // Disable their input handler
                PlayerInputHandler inputHandler = player.GetComponent<PlayerInputHandler>();
                if (inputHandler != null)
                {
                    inputHandler.enabled = false;
                    frozenLegacyPlayers.Add(player);

                    // Visual freeze effect
                    Renderer renderer = player.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.cyan;
                    }
                }
            }
        }

        // Freeze any thrown balls
        BallController[] allBalls = FindObjectsOfType<BallController>();
        Dictionary<BallController, Vector3> frozenBallVelocities = new Dictionary<BallController, Vector3>();

        foreach (BallController ball in allBalls)
        {
            if (ball.GetBallState() == BallController.BallState.Thrown)
            {
                frozenBallVelocities[ball] = ball.GetVelocity();
                ball.SetVelocity(Vector3.zero);
            }
        }

        if (debugMode)
        {
            Debug.Log($"? TIME FREEZE: {frozenPlayers.Count + frozenLegacyPlayers.Count} players frozen for {freezeDuration}s!");
        }

        yield return new WaitForSeconds(freezeDuration);

        // Unfreeze all players
        foreach (PlayerCharacter player in frozenPlayers)
        {
            if (player != null)
            {
                player.SetMovementEnabled(true);

                // Restore original color
                Renderer renderer = player.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = player.GetCharacterData().characterColor;
                }
            }
        }

        foreach (CharacterController player in frozenLegacyPlayers)
        {
            if (player != null)
            {
                PlayerInputHandler inputHandler = player.GetComponent<PlayerInputHandler>();
                if (inputHandler != null)
                {
                    inputHandler.enabled = true;
                }

                // Restore original color
                Renderer renderer = player.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.white; // Default color
                }
            }
        }

        // Unfreeze balls
        foreach (var kvp in frozenBallVelocities)
        {
            if (kvp.Key != null)
            {
                kvp.Key.SetVelocity(kvp.Value);
            }
        }

        if (debugMode)
        {
            Debug.Log("? Time freeze ended");
        }
    }

    void ExecuteTeleportUltimate()
    {
        // Find safe teleport positions around the arena
        Vector3[] teleportPositions = {
            new Vector3(-10f, 0f, 0f),   // Far left
            new Vector3(10f, 0f, 0f),    // Far right
            new Vector3(-5f, 0f, 3f),    // Left back
            new Vector3(5f, 0f, 3f),     // Right back
            new Vector3(-5f, 0f, -3f),   // Left front
            new Vector3(5f, 0f, -3f),    // Right front
            new Vector3(0f, 0f, 5f),     // Center back
            new Vector3(0f, 0f, -5f)     // Center front
        };

        // Choose position farthest from current position
        Vector3 bestPosition = transform.position;
        float maxDistance = 0f;

        foreach (Vector3 pos in teleportPositions)
        {
            float distance = Vector3.Distance(transform.position, pos);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                bestPosition = pos;
            }
        }

        // Teleport with effects
        if (characterData.ultimateEffect != null)
        {
            // Exit effect
            Instantiate(characterData.ultimateEffect, transform.position, Quaternion.identity);
        }

        transform.position = bestPosition;

        if (characterData.ultimateEffect != null)
        {
            // Entry effect
            Instantiate(characterData.ultimateEffect, transform.position, Quaternion.identity);
        }

        // Brief invulnerability after teleport
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.SetTemporaryInvulnerability(1f);
        }

        if (debugMode)
        {
            Debug.Log($"?? TELEPORT: {characterData.characterName} teleported {maxDistance:F1} units!");
        }
    }

    #endregion

    // Helper method for movement disabling during time freeze
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
    }

    #endregion

    void CheckGrounded()
    {
        // Ground check from bottom of original collider
        Vector3 groundCheckPosition = characterTransform.position;
        groundCheckPosition.y -= (originalColliderHeight * 0.5f);

        isGrounded = Physics.Raycast(
            groundCheckPosition,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );

        // Debug visualization
        Debug.DrawRay(groundCheckPosition, Vector3.down * groundCheckDistance,
            isGrounded ? Color.green : Color.red);
    }

    void PlayCharacterSound(CharacterAudioType audioType)
    {
        if (audioSource == null || characterData == null) return;

        AudioClip clipToPlay = characterData.GetRandomAudioClip(audioType);
        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    // Public interface methods for other systems
    public CharacterData GetCharacterData() => characterData;
    public bool IsGrounded() => isGrounded;
    public bool IsDucking() => isDucking;
    public bool HasBall() => hasBall;
    public void SetHasBall(bool value) => hasBall = value;
    public PlayerInputHandler GetInputHandler() => inputHandler;
    public float GetUltimateChargePercentage() => characterData != null ?
        currentUltimateCharge / characterData.ultimateChargeRequired : 0f;

    // Methods for damage system integration
    public int GetThrowDamage(ThrowType throwType)
    {
        return characterData?.GetThrowDamage(throwType) ?? 10;
    }

    public float GetDamageResistance()
    {
        return characterData?.damageResistance ?? 1f;
    }

    void OnDrawGizmosSelected()
    {
        // Visualize ground check
        if (characterCollider != null)
        {
            Vector3 checkPos = transform.position;
            checkPos.y -= (originalColliderHeight * 0.5f);

            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawRay(checkPos, Vector3.down * groundCheckDistance);
        }

        // Show character info in scene view
        if (showCharacterInfo && characterData != null)
        {
            Gizmos.color = characterData.characterColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
        }
    }

    void OnGUI()
    {
        if (!debugMode || characterData == null) return;

        // Show character debug info
        float yOffset = gameObject.name.Contains("2") ? 150f : 50f;

        GUILayout.BeginArea(new Rect(10, yOffset, 300, 150));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"Character: {characterData.characterName}");
        GUILayout.Label($"Health: {playerHealth?.GetCurrentHealth()}/{characterData.maxHealth}");
        GUILayout.Label($"Ultimate: {currentUltimateCharge:F1}/{characterData.ultimateChargeRequired}");
        GUILayout.Label($"Grounded: {isGrounded} | Ducking: {isDucking}");
        GUILayout.Label($"Can Dash: {canDash} | Has Ball: {hasBall}");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}