using UnityEngine;
using System.Collections;

/// <summary>
/// Clean PlayerCharacter with streamlined ability system
/// Focuses on core movement + 9 clean abilities (3 Ultimate, 3 Trick, 3 Treat)
/// </summary>
public class PlayerCharacter : MonoBehaviour
{
    [Header("Character Setup")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private bool autoLoadCharacterOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showCharacterInfo = true;

    // Core Components
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
    private bool inputEnabled = true;
    private bool isTeleporting = false; // FIXED: Add teleporting state

    // Ability charge system
    private float currentUltimateCharge = 0f;
    private float currentTrickCharge = 0f;
    private float currentTreatCharge = 0f;

    // Ability cooldown system
    private bool ultimateOnCooldown = false;
    private bool trickOnCooldown = false;
    private bool treatOnCooldown = false;
    private float ultimateCooldownTime = 3f;
    private float trickCooldownTime = 8f;
    private float treatCooldownTime = 10f;

    // Original collider dimensions for ducking
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;
    private bool duckingStateChanged = false;

    // Ground check settings
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float groundCheckDistance = 0.1f;

    [Header("Movement Restriction")]
    private ArenaMovementRestrictor movementRestrictor;

    [Header("Duck System Integration")]
    private DuckSystem duckSystem;

    // Events for other systems
    public System.Action<CharacterData> OnCharacterLoaded;
    public System.Action<float> OnUltimateChargeChanged;
    public System.Action<float> OnTrickChargeChanged;
    public System.Action<float> OnTreatChargeChanged;

    void Awake()
    {
        CacheComponents();

        if (autoLoadCharacterOnStart && characterData != null)
        {
            LoadCharacter(characterData);
        }

        // Get or add movement restrictor
        movementRestrictor = GetComponent<ArenaMovementRestrictor>();
        if (movementRestrictor == null)
        {
            movementRestrictor = gameObject.AddComponent<ArenaMovementRestrictor>();
        }

        duckSystem = GetComponent<DuckSystem>();
        if (duckSystem == null)
        {
            duckSystem = gameObject.AddComponent<DuckSystem>();
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
        UpdateAbilityCharges();
    }

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
        currentTrickCharge = 0f;
        currentTreatCharge = 0f;
        hasDoubleJumped = false;
        canDash = true;

        // Apply visual changes if character has prefab
        ApplyCharacterVisuals();

        if (debugMode)
        {
            Debug.Log($"Applied stats for {characterData.characterName}: " +
                     $"Speed={characterData.moveSpeed}, Health={characterData.maxHealth}, " +
                     $"Ultimate={characterData.ultimateType}, Trick={characterData.trickType}, Treat={characterData.treatType}");
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
    }

    void HandleInput()
    {
        if (inputHandler == null || characterData == null || !inputEnabled) return;

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

        // Ability inputs
        if (inputHandler.GetUltimatePressed() && CanUseUltimate())
        {
            ActivateUltimate();
        }

        if (inputHandler.GetTrickPressed() && CanUseTrick())
        {
            ActivateTrick();
        }

        if (inputHandler.GetTreatPressed() && CanUseTreat())
        {
            ActivateTreat();
        }

        // ENHANCED: Duck input with duck system integration
        HandleDuckingInput();
    }

    void HandleDuckingInput()
    {
        if (duckSystem != null)
        {
            // Duck system handles all the logic - we just read the state
            bool newDuckState = duckSystem.IsDucking();

            // Check if ducking state changed
            if (newDuckState != isDucking)
            {
                isDucking = newDuckState;
                duckingStateChanged = true;

                if (debugMode)
                {
                    Debug.Log($"{characterData?.characterName} ducking state changed: {isDucking} (System managed)");
                }
            }
        }
        else
        {
            // Fallback to original duck handling
            bool duckInput = inputHandler.GetDuckHeld() && isGrounded;

            if (duckInput != isDucking)
            {
                isDucking = duckInput;
                duckingStateChanged = true;

                if (debugMode)
                {
                    Debug.Log($"{characterData?.characterName} ducking state changed: {isDucking} (Manual)");
                }
            }
        }
    }

    void HandleMovement()
    {
        if (characterData == null || !movementEnabled) return;

        // Get horizontal input
        float horizontalInput = inputHandler.GetHorizontal();

        // Don't move while dashing or ducking
        if (isDashing || isDucking) return;

        // CALCULATE movement (existing code)
        Vector3 newPosition = characterTransform.position;

        if (horizontalInput != 0)
        {
            Vector3 moveDirection = Vector3.right * horizontalInput * characterData.moveSpeed;
            newPosition += moveDirection * Time.deltaTime;
        }

        // Apply gravity when not grounded
        if (!isGrounded)
        {
            velocity.y -= 25f * Time.deltaTime;
        }
        else
        {
            if (velocity.y < 0)
            {
                velocity.y = 0f;
                hasDoubleJumped = false;
            }
        }

        // Apply vertical movement
        newPosition += Vector3.up * velocity.y * Time.deltaTime;

        // *** APPLY MOVEMENT RESTRICTION WITH TELEPORT OVERRIDE SUPPORT ***
        if (movementRestrictor != null)
        {
            Vector3 restrictedPosition = movementRestrictor.ApplyMovementRestriction(newPosition);
            characterTransform.position = restrictedPosition;
        }
        else
        {
            characterTransform.position = newPosition;
        }
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

        velocity.y = characterData.jumpHeight * 0.8f;
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

            if (debugMode)
            {
                Debug.Log($"{characterData?.characterName} Ducked! (Collider height: {characterCollider.height})");
            }
        }
        else
        {
            // Stand up - restore original collider dimensions
            characterCollider.height = originalColliderHeight;
            characterCollider.center = originalColliderCenter;

            if (debugMode)
            {
                Debug.Log($"{characterData?.characterName} Stood up! (Collider height: {characterCollider.height})");
            }
        }

        duckingStateChanged = false;
    }

    // Enhanced public getters that work with duck system
    public bool IsDucking()
    {
        if (duckSystem != null)
        {
            return duckSystem.IsDucking();
        }
        return isDucking;
    }

    public bool CanDuck()
    {
        if (duckSystem != null)
        {
            return duckSystem.CanDuck() && isGrounded;
        }
        return isGrounded;
    }

    // Additional duck system getters for UI/feedback
    public float GetDuckTimeRemaining()
    {
        return duckSystem?.GetDuckTimeRemaining() ?? 0f;
    }

    public float GetDuckProgress()
    {
        return duckSystem?.GetDuckProgress() ?? 0f;
    }

    public bool IsInDuckCooldown()
    {
        return duckSystem?.IsInCooldown() ?? false;
    }

    public float GetDuckCooldownRemaining()
    {
        return duckSystem?.GetCooldownTimeRemaining() ?? 0f;
    }

    public DuckSystem GetDuckSystem()
    {
        return duckSystem;
    }

    void HandleBallInteraction()
    {
        if (BallManager.Instance == null || inputHandler == null || characterData == null || !inputEnabled) return; // FIXED: Check inputEnabled

        // Pickup ball (only if input enabled)
        if (inputHandler.GetPickupPressed() && !hasBall)
        {
            BallManager.Instance.RequestBallPickup(this);
        }

        // Throw ball (only if input enabled)
        if (inputHandler.GetThrowPressed() && hasBall)
        {
            ThrowBall();
        }
    }

    void ThrowBall()
    {
        if (!hasBall || BallManager.Instance == null || characterData == null) return;

        // Determine throw type
        ThrowType throwType = isGrounded ? ThrowType.Normal : ThrowType.JumpThrow;

        // Get damage from character data
        int throwDamage = characterData.GetThrowDamage(throwType);

        // Execute throw
        BallManager.Instance.RequestBallThrowWithCharacterData(this, characterData, throwType, throwDamage);

        // Play throw sound
        PlayCharacterSound(CharacterAudioType.Throw);

        // Add ability charges for throwing
        AddUltimateCharge(15f);
        AddTrickCharge(10f);
        AddTreatCharge(10f);

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} threw {throwType} for {throwDamage} damage!");
        }
    }

    void UpdateAbilityCharges()
    {
        // Update UI events
        OnUltimateChargeChanged?.Invoke(currentUltimateCharge / characterData.ultimateChargeRequired);
        OnTrickChargeChanged?.Invoke(currentTrickCharge / characterData.trickChargeRequired);
        OnTreatChargeChanged?.Invoke(currentTreatCharge / characterData.treatChargeRequired);
    }

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

    // ═══════════════════════════════════════════════════════════════
    // ABILITY CHARGE SYSTEM
    // ═══════════════════════════════════════════════════════════════

    public void AddUltimateCharge(float amount)
    {
        if (characterData == null) return;

        if (currentUltimateCharge >= characterData.ultimateChargeRequired) return;

        float chargeToAdd = amount * characterData.ultimateChargeRate;
        currentUltimateCharge = Mathf.Min(currentUltimateCharge + chargeToAdd, characterData.ultimateChargeRequired);

        if (debugMode && currentUltimateCharge >= characterData.ultimateChargeRequired)
        {
            Debug.Log($"💥 {characterData.characterName} ULTIMATE READY! ({characterData.ultimateType})");
        }
    }

    public void AddTrickCharge(float amount)
    {
        if (characterData == null) return;

        if (currentTrickCharge >= characterData.trickChargeRequired) return;

        float chargeToAdd = amount * characterData.trickChargeRate;
        currentTrickCharge = Mathf.Min(currentTrickCharge + chargeToAdd, characterData.trickChargeRequired);

        if (debugMode && currentTrickCharge >= characterData.trickChargeRequired)
        {
            Debug.Log($"🎯 {characterData.characterName} TRICK READY! ({characterData.trickType})");
        }
    }

    public void AddTreatCharge(float amount)
    {
        if (characterData == null) return;

        if (currentTreatCharge >= characterData.treatChargeRequired) return;

        float chargeToAdd = amount * characterData.treatChargeRate;
        currentTreatCharge = Mathf.Min(currentTreatCharge + chargeToAdd, characterData.treatChargeRequired);

        if (debugMode && currentTreatCharge >= characterData.treatChargeRequired)
        {
            Debug.Log($"✨ {characterData.characterName} TREAT READY! ({characterData.treatType})");
        }
    }

    // Helper methods for gaining charge
    public void OnDamageTaken(int damage)
    {
        AddUltimateCharge(damage * 0.5f);
        AddTrickCharge(damage * 0.3f);
        AddTreatCharge(damage * 0.4f);
    }

    public void OnSuccessfulCatch()
    {
        AddUltimateCharge(25f);
        AddTrickCharge(15f);
        AddTreatCharge(15f);
    }

    public void OnSuccessfulDodge()
    {
        AddUltimateCharge(20f);
        AddTrickCharge(12f);
        AddTreatCharge(12f);
    }

    // ═══════════════════════════════════════════════════════════════
    // ULTIMATE ABILITIES
    // ═══════════════════════════════════════════════════════════════

    bool CanUseUltimate()
    {
        return characterData != null &&
               currentUltimateCharge >= characterData.ultimateChargeRequired &&
               !ultimateOnCooldown;
    }

    void ActivateUltimate()
    {
        if (!CanUseUltimate()) return;

        // Consume charge and start cooldown
        currentUltimateCharge = 0f;
        StartCoroutine(UltimateCooldown());

        // Play ultimate sound and effects
        AudioClip ultimateSound = characterData.GetUltimateSound();
        if (ultimateSound != null)
        {
            audioSource.PlayOneShot(ultimateSound);
        }

        GameObject ultimateEffect = characterData.GetUltimateEffect();
        if (ultimateEffect != null)
        {
            Instantiate(ultimateEffect, characterTransform.position, Quaternion.identity);
        }

        // Execute specific ultimate based on character data
        switch (characterData.ultimateType)
        {
            case UltimateType.PowerThrow:
                ExecutePowerThrow();
                break;
            case UltimateType.MultiThrow:
                StartCoroutine(ExecuteMultiThrow());
                break;
            case UltimateType.Curveball:
                ExecuteCurveball();
                break;
        }

        if (debugMode)
        {
            Debug.Log($"💥 {characterData.characterName} activated {characterData.ultimateType}!");
        }
    }

    void ExecutePowerThrow()
    {
        if (!hasBall) return;

        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall == null) return;

        // Set ultra-powerful throw data
        int damage = characterData.GetUltimateDamage();
        float speed = characterData.GetUltimateSpeed();

        currentBall.SetThrowData(ThrowType.Ultimate, damage, speed);

        // Throw with extra power
        Vector3 direction = Vector3.right; // BallController handles targeting
        currentBall.ThrowBall(direction, 1.5f);

        // Screen shake for power
        CameraController camera = FindObjectOfType<CameraController>();
        if (camera != null)
        {
            camera.ShakeCamera(characterData.GetPowerThrowScreenShake(), 0.8f);
        }

        if (debugMode)
        {
            Debug.Log($"💥 POWER THROW: {damage} damage at {speed} speed with {characterData.GetPowerThrowKnockback()} knockback!");
        }
    }

    IEnumerator ExecuteMultiThrow()
    {
        if (!hasBall) yield break;

        int ballCount = characterData.GetMultiThrowCount();
        int damagePerBall = characterData.GetUltimateDamage();
        float throwSpeed = characterData.GetUltimateSpeed();
        float spreadAngle = characterData.GetMultiThrowSpread();
        float delay = characterData.GetMultiThrowDelay();
        Vector3 spawnOffset = characterData.GetMultiThrowSpawnOffset();

        // FIXED: Determine throw direction based on opponent position
        PlayerCharacter opponent = FindOpponent();
        Vector3 throwDirection = Vector3.right; // Default

        if (opponent != null)
        {
            // Calculate direction towards opponent
            Vector3 toOpponent = (opponent.transform.position - transform.position).normalized;
            throwDirection = new Vector3(toOpponent.x, 0f, 0f).normalized; // Only horizontal component

            if (debugMode)
            {
                Debug.Log($"MultiThrow direction: {throwDirection} (towards {opponent.name})");
            }
        }

        if (debugMode)
        {
            Debug.Log($"🔥 MULTI THROW: {ballCount} balls x {damagePerBall} damage towards opponent!");
        }

        for (int i = 0; i < ballCount; i++)
        {
            // Apply spawn offset relative to throw direction
            Vector3 spawnPos = transform.position;
            spawnPos += throwDirection * spawnOffset.x; // Forward towards opponent
            spawnPos += Vector3.up * spawnOffset.y;     // Height
            spawnPos += Vector3.forward * spawnOffset.z; // Z offset (if needed)

            GameObject ballObj = Instantiate(BallManager.Instance.ballPrefab, spawnPos, Quaternion.identity);
            ballObj.transform.localScale = Vector3.one;

            BallController ballController = ballObj.GetComponent<BallController>();
            if (ballController != null)
            {
                // FIXED: Calculate spread based on throw direction (not always right)
                float angleOffset = (i - (ballCount - 1) * 0.5f) * (spreadAngle / ballCount);

                // Create rotation around Y-axis from the throw direction
                Vector3 spreadDir = Quaternion.Euler(0, angleOffset, 0) * throwDirection;
                spreadDir.y = 0f; // Keep horizontal
                spreadDir = spreadDir.normalized;

                // Setup ball
                ballController.SetBallState(BallController.BallState.Thrown);
                ballController.SetThrowData(ThrowType.Ultimate, damagePerBall, throwSpeed);

                // Set velocity towards opponent with spread
                ballController.velocity = spreadDir * throwSpeed;

                // Setup collision system
                CollisionDamageSystem collisionSystem = ballController.GetComponent<CollisionDamageSystem>();
                if (collisionSystem != null)
                {
                    collisionSystem.OnBallThrown(this);
                }

                // Add visual trail effect
                TrailRenderer trail = ballController.GetComponent<TrailRenderer>();
                if (trail != null)
                {
                    trail.enabled = true;
                    trail.startColor = characterData.characterColor;
                    trail.endColor = Color.clear;
                }

                // FIXED: Auto-destroy MultiThrow balls after 4 seconds (network-efficient)
                Destroy(ballObj, 4f);

                if (debugMode)
                {
                    Debug.Log($"MultiThrow ball {i + 1}: Direction {spreadDir}, Velocity {ballController.velocity} (Auto-destroy in 4s)");
                }
            }

            yield return new WaitForSeconds(delay);
        }
    }

    void ExecuteCurveball()
    {
        if (!hasBall) return;

        BallController currentBall = BallManager.Instance.GetCurrentBall();
        if (currentBall == null) return;

        // Find opponent direction
        PlayerCharacter opponent = FindOpponent();
        Vector3 throwDirection = Vector3.right; // Default

        if (opponent != null)
        {
            Vector3 toOpponent = (opponent.transform.position - transform.position).normalized;
            throwDirection = new Vector3(toOpponent.x, 0f, 0f).normalized;
        }

        // Set curveball data
        int damage = characterData.GetUltimateDamage();
        float speed = characterData.GetUltimateSpeed();

        currentBall.SetThrowData(ThrowType.Ultimate, damage, speed);

        // Throw towards opponent
        currentBall.ThrowBall(throwDirection, 1f);

        // Start curveball Y-axis oscillation
        StartCoroutine(CurveballBehavior(currentBall));

        if (debugMode)
        {
            Debug.Log($"🌊 CURVEBALL: Single ball with Y-axis oscillation towards opponent, {damage} damage!");
        }
    }

    IEnumerator CurveballBehavior(BallController ball)
    {
        float amplitude = characterData.GetCurveballAmplitude();
        float frequency = characterData.GetCurveballFrequency();
        float duration = characterData.GetCurveballDuration();

        float elapsed = 0f;

        while (elapsed < duration && ball != null && ball.GetBallState() == BallController.BallState.Thrown)
        {
            Vector3 velocity = ball.GetVelocity();

            // Apply Y-axis oscillation (up and down movement)
            float curveOffset = Mathf.Sin(elapsed * frequency) * amplitude;
            velocity.y = curveOffset;

            ball.SetVelocity(velocity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (debugMode)
        {
            Debug.Log("🌊 Curveball oscillation complete");
        }
    }

    IEnumerator UltimateCooldown()
    {
        ultimateOnCooldown = true;
        yield return new WaitForSeconds(ultimateCooldownTime);
        ultimateOnCooldown = false;
    }

    // ═══════════════════════════════════════════════════════════════
    // TRICK ABILITIES (Opponent-Focused)
    // ═══════════════════════════════════════════════════════════════

    bool CanUseTrick()
    {
        return characterData != null &&
               currentTrickCharge >= characterData.trickChargeRequired &&
               !trickOnCooldown;
    }

    void ActivateTrick()
    {
        if (!CanUseTrick()) return;

        // Consume charge and start cooldown
        currentTrickCharge = 0f;
        StartCoroutine(TrickCooldown());

        // Play trick sound and effects
        AudioClip trickSound = characterData.GetTrickSound();
        if (trickSound != null)
        {
            audioSource.PlayOneShot(trickSound);
        }

        GameObject trickEffect = characterData.GetTrickEffect();
        if (trickEffect != null)
        {
            Instantiate(trickEffect, characterTransform.position, Quaternion.identity);
        }

        // Execute specific trick based on character data
        switch (characterData.trickType)
        {
            case TrickType.SlowSpeed:
                ExecuteSlowSpeed();
                break;
            case TrickType.Freeze:
                ExecuteFreeze();
                break;
            case TrickType.InstantDamage:
                ExecuteInstantDamage();
                break;
        }

        if (debugMode)
        {
            Debug.Log($"🎯 {characterData.characterName} activated {characterData.trickType}!");
        }
    }

    void ExecuteSlowSpeed()
    {
        // Find opponent and apply slow effect
        PlayerCharacter opponent = FindOpponent();
        if (opponent != null)
        {
            StartCoroutine(ApplySlowSpeedToOpponent(opponent));
        }

        if (debugMode)
        {
            Debug.Log($"🐌 SLOW SPEED: Opponent slowed to {characterData.GetSlowSpeedMultiplier() * 100}% speed for {characterData.GetSlowSpeedDuration()}s");
        }
    }

    IEnumerator ApplySlowSpeedToOpponent(PlayerCharacter opponent)
    {
        if (opponent.characterData == null) yield break;

        float originalSpeed = opponent.characterData.moveSpeed;
        float slowedSpeed = originalSpeed * characterData.GetSlowSpeedMultiplier();
        float duration = characterData.GetSlowSpeedDuration();

        // Apply slow effect
        opponent.characterData.moveSpeed = slowedSpeed;

        // Visual effect on opponent
        Renderer opponentRenderer = opponent.GetComponentInChildren<Renderer>();
        Color originalColor = opponentRenderer?.material.color ?? Color.white;
        if (opponentRenderer != null)
        {
            opponentRenderer.material.color = Color.blue; // Blue tint for slow
        }

        yield return new WaitForSeconds(duration);

        // Restore original speed and color
        if (opponent != null && opponent.characterData != null)
        {
            opponent.characterData.moveSpeed = originalSpeed;
        }

        if (opponentRenderer != null)
        {
            opponentRenderer.material.color = originalColor;
        }
    }

    void ExecuteFreeze()
    {
        // Find opponent and apply freeze effect
        PlayerCharacter opponent = FindOpponent();
        if (opponent != null)
        {
            StartCoroutine(ApplyFreezeToOpponent(opponent));
        }

        if (debugMode)
        {
            Debug.Log($"🧊 FREEZE: Opponent frozen for {characterData.GetFreezeDuration()}s");
        }
    }

    IEnumerator ApplyFreezeToOpponent(PlayerCharacter opponent)
    {
        float duration = characterData.GetFreezeDuration();

        // Disable opponent movement AND input
        opponent.SetMovementEnabled(false);
        opponent.SetInputEnabled(false); // FIXED: Disable all input including catching

        // Visual freeze effect
        Renderer opponentRenderer = opponent.GetComponentInChildren<Renderer>();
        Color originalColor = opponentRenderer?.material.color ?? Color.white;
        if (opponentRenderer != null)
        {
            opponentRenderer.material.color = Color.cyan; // Cyan tint for freeze
        }

        yield return new WaitForSeconds(duration);

        // Restore movement, input, and color
        if (opponent != null)
        {
            opponent.SetMovementEnabled(true);
            opponent.SetInputEnabled(true); // FIXED: Re-enable input
        }

        if (opponentRenderer != null)
        {
            opponentRenderer.material.color = originalColor;
        }
    }

    void ExecuteInstantDamage()
    {
        // Find opponent and apply instant damage
        PlayerCharacter opponent = FindOpponent();
        if (opponent != null)
        {
            PlayerHealth opponentHealth = opponent.GetComponent<PlayerHealth>();
            if (opponentHealth != null)
            {
                int damage = characterData.GetInstantDamageAmount();
                opponentHealth.TakeDamage(damage, null);

                // Create damage effect at opponent position
                GameObject damageEffect = characterData.GetTrickEffect();
                if (damageEffect != null)
                {
                    Instantiate(damageEffect, opponent.transform.position, Quaternion.identity);
                }

                if (debugMode)
                {
                    Debug.Log($"⚡ INSTANT DAMAGE: {damage} damage dealt to {opponent.name}!");
                }
            }
        }
    }

    IEnumerator TrickCooldown()
    {
        trickOnCooldown = true;
        yield return new WaitForSeconds(trickCooldownTime);
        trickOnCooldown = false;
    }

    // ═══════════════════════════════════════════════════════════════
    // TREAT ABILITIES (Self-Focused)
    // ═══════════════════════════════════════════════════════════════

    bool CanUseTreat()
    {
        return characterData != null &&
               currentTreatCharge >= characterData.treatChargeRequired &&
               !treatOnCooldown &&
               !isTeleporting; // FIXED: Don't allow treat while teleporting
    }

    void ActivateTreat()
    {
        if (!CanUseTreat()) return;

        // Consume charge and start cooldown
        currentTreatCharge = 0f;
        StartCoroutine(TreatCooldown());

        // Play treat sound and effects
        AudioClip treatSound = characterData.GetTreatSound();
        if (treatSound != null)
        {
            audioSource.PlayOneShot(treatSound);
        }

        GameObject treatEffect = characterData.GetTreatEffect();
        if (treatEffect != null)
        {
            Instantiate(treatEffect, characterTransform.position, Quaternion.identity);
        }

        // Execute specific treat based on character data
        switch (characterData.treatType)
        {
            case TreatType.Shield:
                ExecuteShield();
                break;
            case TreatType.Teleport:
                ExecuteTeleport();
                break;
            case TreatType.SpeedBoost:
                ExecuteSpeedBoost();
                break;
        }

        if (debugMode)
        {
            Debug.Log($"✨ {characterData.characterName} activated {characterData.treatType}!");
        }
    }

    void ExecuteShield()
    {
        float duration = characterData.GetShieldDuration();

        // Make player temporarily invulnerable
        if (playerHealth != null)
        {
            playerHealth.SetTemporaryInvulnerability(duration);
        }

        if (debugMode)
        {
            Debug.Log($"🛡️ SHIELD: Invulnerable for {duration}s");
        }
    }

    void ExecuteTeleport()
    {
        // Don't teleport if already teleporting
        if (isTeleporting) return;

        float range = characterData.GetTeleportRange();

        // Start bounds override before teleporting
        if (movementRestrictor != null)
        {
            movementRestrictor.StartTeleportOverride();
        }

        // Enhanced teleport with strategic positioning
        StartCoroutine(EnhancedTeleport(range));

        if (debugMode)
        {
            Debug.Log($"🌀 ENHANCED TELEPORT: Started!");
        }
    }

    IEnumerator EnhancedTeleport(float range)
    {
        isTeleporting = true;

        // Store original position
        Vector3 originalPosition = transform.position;

        if (debugMode)
        {
            Debug.Log($"🌀 TELEPORT START: Original position {originalPosition}");
        }

        // Calculate strategic teleport position
        Vector3 teleportPosition = CalculateStrategicTeleportPosition(range);

        // Create teleport effect at old position
        GameObject teleportEffect = characterData.GetTreatEffect();
        if (teleportEffect != null)
        {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }

        // Teleport TO new position (can cross bounds now!)
        transform.position = teleportPosition;

        // Create teleport effect at new position
        if (teleportEffect != null)
        {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }

        // Begin grace period (allows movement in opponent's side)
        if (movementRestrictor != null)
        {
            movementRestrictor.BeginTeleportGracePeriod();
        }

        if (debugMode)
        {
            Debug.Log($"🌀 TELEPORTED TO: {teleportPosition} - Grace period active");
        }

        // Wait for strategic positioning time
        yield return new WaitForSeconds(1.2f);

        if (debugMode)
        {
            Debug.Log($"🌀 TELEPORT RETURN: Going back to home side");
        }

        // Create teleport effect before returning
        if (teleportEffect != null)
        {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }

        // Teleport BACK to safe position in home side
        Vector3 returnPosition = CalculateReturnPosition(originalPosition);
        transform.position = returnPosition;

        // Create teleport effect at return position
        if (teleportEffect != null)
        {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }

        // End teleport override - restore normal bounds
        if (movementRestrictor != null)
        {
            movementRestrictor.EndTeleportOverride();
        }

        if (debugMode)
        {
            Debug.Log($"🌀 TELEPORT COMPLETE: Returned to home side at {returnPosition}");
        }

        isTeleporting = false;
    }

    Vector3 CalculateStrategicTeleportPosition(float range)
    {
        Vector3 currentPos = transform.position;
        PlayerCharacter opponent = FindOpponent();

        // Check for incoming ball - dodge mode
        BallController incomingBall = FindIncomingBall();

        if (incomingBall != null)
        {
            // DODGE MODE: Smart evasion
            return CalculateDodgePosition(currentPos, incomingBall, range);
        }
        else if (opponent != null)
        {
            // ATTACK MODE: Flank behind opponent for surprise attack
            return CalculateFlankPosition(currentPos, opponent, range);
        }
        else
        {
            // REPOSITIONING MODE: Move to tactical position
            return CalculateRepositionPosition(currentPos, range);
        }
    }

    Vector3 CalculateDodgePosition(Vector3 currentPos, BallController incomingBall, float range)
    {
        Vector3 ballPos = incomingBall.transform.position;
        Vector3 ballVel = incomingBall.GetVelocity().normalized;

        // Calculate perpendicular dodge positions
        Vector3 dodgeLeft = currentPos + Vector3.left * range;
        Vector3 dodgeRight = currentPos + Vector3.right * range;

        // Choose position that maximizes distance from ball path
        float leftSafety = CalculateSafetyScore(dodgeLeft, ballPos, ballVel);
        float rightSafety = CalculateSafetyScore(dodgeRight, ballPos, ballVel);

        Vector3 bestDodge = leftSafety > rightSafety ? dodgeLeft : dodgeRight;
        bestDodge.y = currentPos.y;
        bestDodge.z = currentPos.z;

        if (debugMode)
        {
            Debug.Log($"🌀 DODGE TELEPORT: Ball incoming, dodging {(leftSafety > rightSafety ? "LEFT" : "RIGHT")} (Safety: {Mathf.Max(leftSafety, rightSafety):F1})");
        }

        return ClampToArena(bestDodge);
    }

    Vector3 CalculateFlankPosition(Vector3 currentPos, PlayerCharacter opponent, float range)
    {
        Vector3 opponentPos = opponent.transform.position;

        // Determine optimal flank position
        Vector3 teleportPos;

        // Always try to get behind opponent (surprise attack)
        if (movementRestrictor != null)
        {
            var playerSide = movementRestrictor.GetPlayerSide();

            if (playerSide == ArenaMovementRestrictor.PlayerSide.Left)
            {
                // Left player teleports to right side behind opponent
                teleportPos = new Vector3(opponentPos.x + range * 0.7f, currentPos.y, currentPos.z);
                if (debugMode) Debug.Log("🌀 FLANK ATTACK: Left player flanking right");
            }
            else
            {
                // Right player teleports to left side behind opponent
                teleportPos = new Vector3(opponentPos.x - range * 0.7f, currentPos.y, currentPos.z);
                if (debugMode) Debug.Log("🌀 FLANK ATTACK: Right player flanking left");
            }
        }
        else
        {
            // Fallback: flank based on current position
            float direction = (currentPos.x < opponentPos.x) ? 1f : -1f;
            teleportPos = new Vector3(opponentPos.x + direction * range * 0.7f, currentPos.y, currentPos.z);
        }

        return ClampToArena(teleportPos);
    }

    Vector3 CalculateRepositionPosition(Vector3 currentPos, float range)
    {
        // Move toward center for better court coverage
        Vector3 centerPos = new Vector3(0f, currentPos.y, currentPos.z);
        Vector3 directionToCenter = (centerPos - currentPos).normalized;

        Vector3 teleportPos = currentPos + directionToCenter * range * 0.5f;

        if (debugMode)
        {
            Debug.Log("🌀 REPOSITION TELEPORT: Moving toward center");
        }

        return ClampToArena(teleportPos);
    }

    Vector3 CalculateReturnPosition(Vector3 originalPosition)
    {
        // Return to a safe position in home side
        if (movementRestrictor != null)
        {
            movementRestrictor.GetPlayerBounds(out float minX, out float maxX);

            // Position at 75% toward center of home side
            float homeCenterX = (minX + maxX) * 0.5f;
            float returnX = Mathf.Lerp(originalPosition.x, homeCenterX, 0.75f);

            return new Vector3(returnX, originalPosition.y, originalPosition.z);
        }

        // Fallback
        return originalPosition;
    }

    float CalculateSafetyScore(Vector3 position, Vector3 ballPos, Vector3 ballVel)
    {
        // Calculate how safe a position is from ball trajectory
        Vector3 ballToBall = position - ballPos;
        float distanceFromBall = ballToBall.magnitude;

        // Check if position is in ball's path
        float pathAlignment = Vector3.Dot(ballToBall.normalized, ballVel);

        // Higher score = safer position
        float safetyScore = distanceFromBall;
        if (pathAlignment > 0.5f) // In ball's path
        {
            safetyScore *= 0.3f; // Much less safe
        }

        return safetyScore;
    }

    Vector3 ClampToArena(Vector3 position)
    {
        // Use arena bounds (can be wider than player bounds during teleport)
        position.x = Mathf.Clamp(position.x, -12f, 12f);
        position.y = Mathf.Max(position.y, 0f);
        return position;
    }

    void ExecuteSpeedBoost()
    {
        StartCoroutine(ApplySpeedBoost());

        if (debugMode)
        {
            Debug.Log($"💨 SPEED BOOST: {characterData.GetSpeedBoostMultiplier()}x speed for {characterData.GetSpeedBoostDuration()}s");
        }
    }

    IEnumerator ApplySpeedBoost()
    {
        if (characterData == null) yield break;

        float originalSpeed = characterData.moveSpeed;
        float boostedSpeed = originalSpeed * characterData.GetSpeedBoostMultiplier();
        float duration = characterData.GetSpeedBoostDuration();

        // Apply speed boost
        characterData.moveSpeed = boostedSpeed;

        // Visual effect
        Renderer characterRenderer = GetComponentInChildren<Renderer>();
        Color originalColor = characterRenderer?.material.color ?? Color.white;
        if (characterRenderer != null)
        {
            characterRenderer.material.color = Color.yellow; // Yellow tint for speed
        }

        yield return new WaitForSeconds(duration);

        // Restore original speed and color
        if (characterData != null)
        {
            characterData.moveSpeed = originalSpeed;
        }

        if (characterRenderer != null)
        {
            characterRenderer.material.color = originalColor;
        }
    }

    IEnumerator TreatCooldown()
    {
        treatOnCooldown = true;
        yield return new WaitForSeconds(treatCooldownTime);
        treatOnCooldown = false;
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    PlayerCharacter FindOpponent()
    {
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player != this)
            {
                return player;
            }
        }
        return null;
    }

    // FIXED: Add back the missing FindIncomingBall method
    BallController FindIncomingBall()
    {
        BallController[] allBalls = FindObjectsOfType<BallController>();

        foreach (BallController ball in allBalls)
        {
            if (ball.GetBallState() == BallController.BallState.Thrown)
            {
                // Check if ball is coming toward this player
                Vector3 ballPos = ball.transform.position;
                Vector3 ballVel = ball.GetVelocity();
                Vector3 playerPos = transform.position;

                // Simple check: ball is moving toward player and is within reasonable range
                Vector3 ballToPlayer = (playerPos - ballPos).normalized;
                float dot = Vector3.Dot(ballVel.normalized, ballToPlayer);

                if (dot > 0.5f && Vector3.Distance(ballPos, playerPos) < 10f)
                {
                    return ball;
                }
            }
        }

        return null;
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
    }

    // FIXED: Add input control method
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    // Public interface methods for other systems
    public CharacterData GetCharacterData() => characterData;
    public bool IsGrounded() => isGrounded;
    //public bool IsDucking() => isDucking;
    public bool HasBall() => hasBall;
    public void SetHasBall(bool value) => hasBall = value;
    public PlayerInputHandler GetInputHandler() => inputHandler;

    // Charge getters for UI
    public float GetUltimateChargePercentage() => characterData != null ?
        currentUltimateCharge / characterData.ultimateChargeRequired : 0f;
    public float GetTrickChargePercentage() => characterData != null ?
        currentTrickCharge / characterData.trickChargeRequired : 0f;
    public float GetTreatChargePercentage() => characterData != null ?
        currentTreatCharge / characterData.treatChargeRequired : 0f;

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

            // Show ability charge status
            if (currentUltimateCharge >= characterData.ultimateChargeRequired)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);
            }
        }
    }

    // Enhanced debug GUI with duck system info
    void OnGUI()
    {
        if (!debugMode || characterData == null) return;

        float yOffset = gameObject.name.Contains("2") ? 200f : 50f;

        GUILayout.BeginArea(new Rect(10, yOffset, 450, 250));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"Character: {characterData.characterName}");
        GUILayout.Label($"Health: {playerHealth?.GetCurrentHealth()}/{characterData.maxHealth}");
        GUILayout.Label($"Ultimate ({characterData.ultimateType}): {currentUltimateCharge:F1}/{characterData.ultimateChargeRequired}");
        GUILayout.Label($"Trick ({characterData.trickType}): {currentTrickCharge:F1}/{characterData.trickChargeRequired}");
        GUILayout.Label($"Treat ({characterData.treatType}): {currentTreatCharge:F1}/{characterData.treatChargeRequired}");
        GUILayout.Label($"State: Ground={isGrounded} Duck={isDucking} Ball={hasBall} Input={inputEnabled}");

        // ENHANCED: Duck system info
        if (duckSystem != null)
        {
            GUILayout.Label($"Duck System: Can={duckSystem.CanDuck()} Cooldown={duckSystem.IsInCooldown()}");

            if (duckSystem.IsDucking())
            {
                float timeRemaining = duckSystem.GetDuckTimeRemaining();
                GUILayout.Label($"Duck Time Remaining: {timeRemaining:F1}s");

                // Duck progress bar
                Rect duckRect = GUILayoutUtility.GetRect(400, 15);
                GUI.Box(duckRect, "");

                Rect duckFill = new Rect(duckRect.x, duckRect.y,
                    duckRect.width * duckSystem.GetDuckProgress(), duckRect.height);

                Color duckColor = timeRemaining < 0.3f ? Color.red :
                                timeRemaining < 0.7f ? Color.yellow : Color.green;
                GUI.color = duckColor;
                GUI.Box(duckFill, "");
                GUI.color = Color.white;
            }

            if (duckSystem.IsInCooldown())
            {
                float cooldownRemaining = duckSystem.GetCooldownTimeRemaining();
                GUILayout.Label($"Duck Cooldown: {cooldownRemaining:F1}s");

                // Cooldown progress bar
                Rect cooldownRect = GUILayoutUtility.GetRect(400, 15);
                GUI.Box(cooldownRect, "");

                Rect cooldownFill = new Rect(cooldownRect.x, cooldownRect.y,
                    cooldownRect.width * (1f - (cooldownRemaining / 1.2f)), cooldownRect.height);

                GUI.color = Color.cyan;
                GUI.Box(cooldownFill, "");
                GUI.color = Color.white;
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}