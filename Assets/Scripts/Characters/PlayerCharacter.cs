using UnityEngine;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Enhanced PlayerCharacter with comprehensive PUN2 multiplayer integration
/// Includes match management compatibility and streamlined ability system
/// Focuses on core movement + 9 clean abilities (3 Ultimate, 3 Trick, 3 Treat)
/// </summary>
public class PlayerCharacter : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Character Setup")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private bool autoLoadCharacterOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showCharacterInfo = true;

    [Header("Network Settings")]
    [SerializeField] private bool enableNetworkSync = true;
    [SerializeField] private float sendRate = 20f;

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
    private bool isTeleporting = false;

    // Network input/movement state
    private bool networkInputEnabled = true;
    private bool networkMovementEnabled = true;

    // Match integration
    private MatchManager currentMatch;
    private RoundManager currentRound;

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

    // Network sync variables
    private Vector3 networkPosition;
    private Vector3 networkVelocity;
    private bool networkIsGrounded;
    private bool networkIsDucking;
    private bool networkHasBall;
    private float networkLag;

    [Header("Movement Restriction")]
    private ArenaMovementRestrictor movementRestrictor;

    [Header("Duck System Integration")]
    private DuckSystem duckSystem;

    [Header("Simplified VFX Integration")]
    [SerializeField] private bool useVFXManager = true;
    [SerializeField] private float vfxSpawnHeight = 1.5f;

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

    void Start()
    {
        // Cache match and round managers
        currentMatch = FindObjectOfType<MatchManager>();
        currentRound = FindObjectOfType<RoundManager>();

        // Enhanced network setup
        if (photonView != null && photonView.IsMine)
        {
            // This is the local player - enable input
            if (inputHandler != null)
            {
                inputHandler.isPUN2Enabled = true;
            }
        }
        else if (photonView != null)
        {
            // This is a remote player - disable input
            if (inputHandler != null)
            {
                inputHandler.isPUN2Enabled = true;
            }
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

        // Only local player processes input and physics
        if (photonView == null || photonView.IsMine)
        {
            // Local player - your existing Update code
            HandleInput();
            CheckGrounded();
            HandleMovement();
            HandleDucking();
            HandleBallInteraction();
            UpdateAbilityCharges();
        }
        else
        {
            // Remote player - interpolate to network position
            InterpolateNetworkMovement();
        }
    }

    /// <summary>
    /// Smooth interpolation for remote players
    /// </summary>
    void InterpolateNetworkMovement()
    {
        if (!enableNetworkSync) return;

        // Position interpolation
        float distance = Vector3.Distance(transform.position, networkPosition);

        if (distance > 0.1f) // Teleport if too far (lag spike)
        {
            if (distance > 5f)
            {
                transform.position = networkPosition;
            }
            else
            {
                // Smooth interpolation
                float lerpRate = Time.deltaTime * sendRate;
                transform.position = Vector3.Lerp(transform.position, networkPosition, lerpRate);
            }
        }

        // Apply network state to visuals
        isGrounded = networkIsGrounded;
        isDucking = networkIsDucking;
        hasBall = networkHasBall;

        // Update visual state based on network data
        if (duckingStateChanged != networkIsDucking)
        {
            isDucking = networkIsDucking;
            duckingStateChanged = true;
            HandleDucking(); // Apply visual changes
        }
    }

    /// <summary>
    /// RPC for synchronized actions (abilities, jumps, etc.)
    /// </summary>
    [PunRPC]
    void SyncPlayerAction(string actionType)
    {
        // Only apply to remote players (local player already did the action)
        if (photonView.IsMine) return;

        switch (actionType)
        {
            case "Jump":
                PlayCharacterSound(CharacterAudioType.Jump);
                break;

            case "DoubleJump":
                PlayCharacterSound(CharacterAudioType.Jump);
                break;

            case "Dash":
                PlayCharacterSound(CharacterAudioType.Dash);
                if (characterData.dashEffect != null)
                {
                    Instantiate(characterData.dashEffect, transform.position, Quaternion.identity);
                }
                break;

            case "ThrowBall":
                PlayCharacterSound(CharacterAudioType.Throw);
                break;

            case "Ultimate":
                if (useVFXManager && VFXManager.Instance != null)
                {
                    Vector3 position = transform.position + Vector3.up * vfxSpawnHeight;
                    VFXManager.Instance.SpawnUltimateActivationVFX(position, this);
                }
                break;

            case "Trick":
                break;

            case "Treat":
                break;
        }
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

    /// <summary>
    /// Enhanced input handling with network input state checking
    /// </summary>
    void HandleInput()
    {
        if (inputHandler == null || characterData == null || !inputEnabled || !networkInputEnabled) return;

        // Only process input for local player
        if (photonView != null && !photonView.IsMine) return;

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

        // Ability inputs - only if match allows
        bool matchAllowsAbilities = IsMatchStateAllowingAbilities();

        if (matchAllowsAbilities && inputHandler.GetUltimatePressed() && CanUseUltimate())
        {
            ActivateUltimate();
        }

        if (matchAllowsAbilities && inputHandler.GetTrickPressed() && CanUseTrick())
        {
            ActivateTrick();
        }

        if (matchAllowsAbilities && inputHandler.GetTreatPressed() && CanUseTreat())
        {
            ActivateTreat();
        }

        // Duck input with system integration
        HandleDuckingInput();
    }

    /// <summary>
    /// Check if current match state allows abilities
    /// </summary>
    bool IsMatchStateAllowingAbilities()
    {
        // Only check MatchManager since RoundManager no longer handles round states
        if (currentMatch != null)
        {
            var matchState = currentMatch.GetMatchState();
            return matchState == MatchManager.MatchState.Fighting;
        }

        // Fallback: if no match manager found, default to allow abilities
        return true;
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

    /// <summary>
    /// Enhanced movement handling with network movement state checking
    /// </summary>
    void HandleMovement()
    {
        if (characterData == null || !movementEnabled || !networkMovementEnabled) return;

        // Only process movement for local player
        if (photonView != null && !photonView.IsMine) return;

        // Get horizontal input
        float horizontalInput = inputHandler.GetHorizontal();

        // Don't move while dashing or ducking
        if (isDashing || isDucking) return;

        // Calculate movement
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

        // Apply movement restriction with teleport override support
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

    /// <summary>
    /// Modified Jump method with network sync
    /// </summary>
    void Jump()
    {
        if (characterData == null) return;

        velocity.y = characterData.jumpHeight;
        isGrounded = false;

        // Play jump sound
        PlayCharacterSound(CharacterAudioType.Jump);

        // Sync action to other players
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncPlayerAction", RpcTarget.Others, "Jump");
        }

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} Jumped! (Height: {characterData.jumpHeight})");
        }
    }

    /// <summary>
    /// Modified DoubleJump method with network sync
    /// </summary>
    void DoubleJump()
    {
        if (characterData == null || hasDoubleJumped) return;

        velocity.y = characterData.jumpHeight * 0.8f;
        hasDoubleJumped = true;

        // Play jump sound
        PlayCharacterSound(CharacterAudioType.Jump);

        // Sync action to other players
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncPlayerAction", RpcTarget.Others, "DoubleJump");
        }

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

    /// <summary>
    /// Modified PerformDash method with network sync
    /// </summary>
    void PerformDash()
    {
        if (characterData == null) return;

        // Sync dash to other players immediately
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncPlayerAction", RpcTarget.Others, "Dash");
        }

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

    /// <summary>
    /// Enhanced ball interaction with match state awareness
    /// </summary>
    void HandleBallInteraction()
    {
        if (BallManager.Instance == null || inputHandler == null || characterData == null || !inputEnabled || !networkInputEnabled)
        {
            return;
        }

        // Check if match allows ball interaction
        if (!IsMatchStateAllowingAbilities())
        {
            return;
        }

        // Only process for local player
        if (photonView != null && !photonView.IsMine) return;

        // Pickup ball when D is pressed
        if (inputHandler.GetPickupPressed() && !hasBall)
        {
            Debug.Log($"🎮 === PICKUP ATTEMPT START === {characterData.characterName}");

            BallController ball = BallManager.Instance.GetCurrentBall();
            if (ball == null)
            {
                Debug.LogError($"❌ No ball found in BallManager!");
                return;
            }

            if (!ball.IsFree())
            {
                Debug.LogWarning($"❌ Ball not free - state: {ball.GetBallState()}");
                return;
            }

            float distance = Vector3.Distance(transform.position, ball.transform.position);
            Debug.Log($"📏 Distance to ball: {distance:F2} (pickup range: 1.2)");

            if (distance <= 1.2f)
            {
                PhotonView ballView = ball.GetComponent<PhotonView>();
                PhotonView myView = GetComponent<PhotonView>();

                if (myView != null && myView.IsMine)
                {
                    Debug.Log($"✅ Confirmed: This is MY local player");

                    // Request ownership if we don't have it
                    if (ballView != null && !ballView.IsMine)
                    {
                        Debug.Log($"🔄 Ball not owned by me - requesting ownership");
                        ballView.RequestOwnership();
                    }

                    // Try pickup
                    Debug.Log($"🎯 Calling BallManager.RequestBallPickup()...");
                    bool success = BallManager.Instance.RequestBallPickup(this);
                    Debug.Log($"🎯 Pickup result: {(success ? "SUCCESS ✅" : "FAILED ❌")}");
                }
                else
                {
                    Debug.LogWarning($"❌ NOT my local player - myView.IsMine = {myView?.IsMine}");
                }
            }
            else
            {
                Debug.LogWarning($"❌ Too far from ball: {distance:F2} > 1.2");
            }

            Debug.Log($"🎮 === PICKUP ATTEMPT END ===");
        }

        // Throw ball
        if (inputHandler.GetThrowPressed() && hasBall)
        {
            Debug.Log($"🚀 {characterData.characterName} throwing ball");
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

        // SIMPLIFIED VFX: Spawn character-specific throw VFX
        if (useVFXManager && VFXManager.Instance != null)
        {
            Vector3 throwVFXPosition = transform.position + Vector3.up * vfxSpawnHeight;
            VFXManager.Instance.SpawnThrowVFX(throwVFXPosition, this, throwType);
        }

        // Execute throw
        BallManager.Instance.RequestBallThrowWithCharacterData(this, characterData, throwType, throwDamage);

        // Play throw sound (keep existing)
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
    // ABILITY CHARGE SYSTEM (Enhanced with Network Sync)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Enhanced ability charge management with network awareness
    /// </summary>
    public void AddUltimateCharge(float amount)
    {
        if (characterData == null || !IsLocalPlayer()) return;

        if (currentUltimateCharge >= characterData.ultimateChargeRequired) return;

        float chargeToAdd = amount * characterData.ultimateChargeRate;
        currentUltimateCharge = Mathf.Min(currentUltimateCharge + chargeToAdd, characterData.ultimateChargeRequired);

        // Sync charge across network if significant change
        if (chargeToAdd > 5f && photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncUltimateCharge", RpcTarget.Others, currentUltimateCharge);
        }

        if (debugMode && currentUltimateCharge >= characterData.ultimateChargeRequired)
        {
            Debug.Log($"💥 {characterData.characterName} ULTIMATE READY! ({characterData.ultimateType})");
        }
    }

    public void AddTrickCharge(float amount)
    {
        if (characterData == null || !IsLocalPlayer()) return;

        if (currentTrickCharge >= characterData.trickChargeRequired) return;

        float chargeToAdd = amount * characterData.trickChargeRate;
        currentTrickCharge = Mathf.Min(currentTrickCharge + chargeToAdd, characterData.trickChargeRequired);

        // Sync charge across network if significant change
        if (chargeToAdd > 5f && photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncTrickCharge", RpcTarget.Others, currentTrickCharge);
        }

        if (debugMode && currentTrickCharge >= characterData.trickChargeRequired)
        {
            Debug.Log($"🎯 {characterData.characterName} TRICK READY! ({characterData.trickType})");
        }
    }

    public void AddTreatCharge(float amount)
    {
        if (characterData == null || !IsLocalPlayer()) return;

        if (currentTreatCharge >= characterData.treatChargeRequired) return;

        float chargeToAdd = amount * characterData.treatChargeRate;
        currentTreatCharge = Mathf.Min(currentTreatCharge + chargeToAdd, characterData.treatChargeRequired);

        // Sync charge across network if significant change
        if (chargeToAdd > 5f && photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncTreatCharge", RpcTarget.Others, currentTreatCharge);
        }

        if (debugMode && currentTreatCharge >= characterData.treatChargeRequired)
        {
            Debug.Log($"✨ {characterData.characterName} TREAT READY! ({characterData.treatType})");
        }
    }

    [PunRPC]
    void SyncUltimateCharge(float charge)
    {
        currentUltimateCharge = charge;
        OnUltimateChargeChanged?.Invoke(currentUltimateCharge / characterData.ultimateChargeRequired);
    }

    [PunRPC]
    void SyncTrickCharge(float charge)
    {
        currentTrickCharge = charge;
        OnTrickChargeChanged?.Invoke(currentTrickCharge / characterData.trickChargeRequired);
    }

    [PunRPC]
    void SyncTreatCharge(float charge)
    {
        currentTreatCharge = charge;
        OnTreatChargeChanged?.Invoke(currentTreatCharge / characterData.treatChargeRequired);
    }

    /// <summary>
    /// Network-aware damage processing for charge gain
    /// </summary>
    public void OnDamageTaken(int damage)
    {
        if (!IsLocalPlayer()) return;

        AddUltimateCharge(damage * 0.5f);
        AddTrickCharge(damage * 0.3f);
        AddTreatCharge(damage * 0.4f);
    }

    /// <summary>
    /// Network-aware successful catch processing
    /// </summary>
    public void OnSuccessfulCatch()
    {
        if (!IsLocalPlayer()) return;

        AddUltimateCharge(25f);
        AddTrickCharge(15f);
        AddTreatCharge(15f);
    }

    /// <summary>
    /// Network-aware successful dodge processing
    /// </summary>
    public void OnSuccessfulDodge()
    {
        if (!IsLocalPlayer()) return;

        AddUltimateCharge(20f);
        AddTrickCharge(12f);
        AddTreatCharge(12f);
    }

    // ═══════════════════════════════════════════════════════════════
    // ULTIMATE ABILITIES (Enhanced with Network Sync)
    // ═══════════════════════════════════════════════════════════════

    bool CanUseUltimate()
    {
        return characterData != null &&
               currentUltimateCharge >= characterData.ultimateChargeRequired &&
               !ultimateOnCooldown;
    }

    /// <summary>
    /// Enhanced ultimate activation with match integration
    /// </summary>
    void ActivateUltimate()
    {
        if (!CanUseUltimate()) return;

        // Consume charge and start cooldown
        currentUltimateCharge = 0f;
        StartCoroutine(UltimateCooldown());

        // Sync ultimate activation across network
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncUltimateActivation", RpcTarget.Others, characterData.ultimateType.ToString());
        }

        // VFX and execution
        if (useVFXManager && VFXManager.Instance != null)
        {
            Vector3 activationPosition = transform.position + Vector3.up * vfxSpawnHeight;
            VFXManager.Instance.SpawnUltimateActivationVFX(activationPosition, this);
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

    [PunRPC]
    void SyncUltimateActivation(string ultimateType)
    {
        // Remote player ultimate activation - play VFX only
        if (useVFXManager && VFXManager.Instance != null)
        {
            Vector3 activationPosition = transform.position + Vector3.up * vfxSpawnHeight;
            VFXManager.Instance.SpawnUltimateActivationVFX(activationPosition, this);
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

        // Determine throw direction based on opponent position
        PlayerCharacter opponent = FindOpponent();
        Vector3 throwDirection = Vector3.right; // Default

        if (opponent != null)
        {
            Vector3 toOpponent = (opponent.transform.position - transform.position).normalized;
            throwDirection = new Vector3(toOpponent.x, 0f, 0f).normalized;

            if (debugMode)
            {
                Debug.Log($"Enhanced MultiThrow direction: {throwDirection} (towards {opponent.name})");
            }
        }

        if (debugMode)
        {
            Debug.Log($"🔥 ENHANCED MULTI THROW: {ballCount} balls x {damagePerBall} damage towards opponent with VFX!");
        }

        for (int i = 0; i < ballCount; i++)
        {
            // Apply spawn offset relative to throw direction
            Vector3 spawnPos = transform.position;
            spawnPos += throwDirection * spawnOffset.x;
            spawnPos += Vector3.up * spawnOffset.y;
            spawnPos += Vector3.forward * spawnOffset.z;

            GameObject ballObj = Instantiate(BallManager.Instance.ballPrefab, spawnPos, Quaternion.identity);
            ballObj.transform.localScale = Vector3.one;

            BallController ballController = ballObj.GetComponent<BallController>();
            if (ballController != null)
            {
                // Calculate spread based on throw direction
                float angleOffset = (i - (ballCount - 1) * 0.5f) * (spreadAngle / ballCount);
                Vector3 spreadDir = Quaternion.Euler(0, angleOffset, 0) * throwDirection;
                spreadDir.y = 0f;
                spreadDir = spreadDir.normalized;

                // Setup ball
                ballController.SetBallState(BallController.BallState.Thrown);
                ballController.SetThrowData(ThrowType.Ultimate, damagePerBall, throwSpeed);

                // Set thrower reference for multithrow balls
                ballController.SetThrower(this);

                ballController.velocity = spreadDir * throwSpeed;
                ballController.ApplyUltimateBallVFX();

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

                // Auto-destroy MultiThrow balls after 4 seconds
                Destroy(ballObj, 4f);

                if (debugMode)
                {
                    Debug.Log($"Enhanced MultiThrow ball {i + 1}: Direction {spreadDir}, Velocity {ballController.velocity}");
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
    // TRICK ABILITIES (Opponent-Focused) - Enhanced with Network Sync
    // ═══════════════════════════════════════════════════════════════

    bool CanUseTrick()
    {
        return characterData != null &&
               currentTrickCharge >= characterData.trickChargeRequired &&
               !trickOnCooldown;
    }

    /// <summary>
    /// Enhanced trick activation with network sync
    /// </summary>
    void ActivateTrick()
    {
        if (!CanUseTrick()) return;

        currentTrickCharge = 0f;
        StartCoroutine(TrickCooldown());

        // Find opponent for trick target
        PlayerCharacter opponent = FindOpponent();
        if (opponent != null)
        {
            // Sync trick activation
            if (photonView != null && photonView.IsMine)
            {
                PhotonView opponentView = opponent.GetComponent<PhotonView>();
                if (opponentView != null)
                {
                    photonView.RPC("SyncTrickActivation", RpcTarget.All,
                                 characterData.trickType.ToString(), opponentView.ViewID);
                }
            }

            // VFX on opponent
            if (useVFXManager && VFXManager.Instance != null)
            {
                Vector3 opponentPosition = opponent.transform.position + Vector3.up * vfxSpawnHeight;
                VFXManager.Instance.SpawnTrickVFX(opponentPosition, this, opponent);
            }

            // Execute specific trick based on character data
            switch (characterData.trickType)
            {
                case TrickType.SlowSpeed:
                    ExecuteSlowSpeed(opponent);
                    break;
                case TrickType.Freeze:
                    ExecuteFreeze(opponent);
                    break;
                case TrickType.InstantDamage:
                    ExecuteInstantDamage(opponent);
                    break;
            }
        }

        if (debugMode)
        {
            Debug.Log($"🎯 {characterData.characterName} activated {characterData.trickType}!");
        }
    }

    [PunRPC]
    void SyncTrickActivation(string trickType, int opponentViewID)
    {
        // Find opponent by ViewID
        PhotonView opponentView = PhotonView.Find(opponentViewID);
        if (opponentView != null)
        {
            PlayerCharacter opponent = opponentView.GetComponent<PlayerCharacter>();
            if (opponent != null && useVFXManager && VFXManager.Instance != null)
            {
                Vector3 opponentPosition = opponent.transform.position + Vector3.up * vfxSpawnHeight;
                VFXManager.Instance.SpawnTrickVFX(opponentPosition, this, opponent);
            }
        }
    }

    void ExecuteSlowSpeed(PlayerCharacter opponent)
    {
        StartCoroutine(ApplySlowSpeedToOpponent(opponent));

        if (debugMode)
        {
            Debug.Log($"🐌 SLOW SPEED: Opponent slowed for {characterData.GetSlowSpeedDuration()}s");
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

    void ExecuteFreeze(PlayerCharacter opponent)
    {
        StartCoroutine(ApplyFreezeToOpponent(opponent));

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
        opponent.SetInputEnabled(false);

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
            opponent.SetInputEnabled(true);
        }

        if (opponentRenderer != null)
        {
            opponentRenderer.material.color = originalColor;
        }
    }

    void ExecuteInstantDamage(PlayerCharacter opponent)
    {
        PlayerHealth opponentHealth = opponent.GetComponent<PlayerHealth>();
        if (opponentHealth != null)
        {
            int damage = characterData.GetInstantDamageAmount();
            opponentHealth.TakeDamage(damage, null);

            if (debugMode)
            {
                Debug.Log($"⚡ INSTANT DAMAGE: {damage} damage dealt to {opponent.name}!");
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
    // TREAT ABILITIES (Self-Focused) - Enhanced with Network Sync
    // ═══════════════════════════════════════════════════════════════

    bool CanUseTreat()
    {
        return characterData != null &&
               currentTreatCharge >= characterData.treatChargeRequired &&
               !treatOnCooldown &&
               !isTeleporting;
    }

    /// <summary>
    /// Enhanced treat activation with network sync
    /// </summary>
    void ActivateTreat()
    {
        if (!CanUseTreat()) return;

        currentTreatCharge = 0f;
        StartCoroutine(TreatCooldown());

        // Sync treat activation
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncTreatActivation", RpcTarget.Others, characterData.treatType.ToString());
        }

        // VFX on self
        if (useVFXManager && VFXManager.Instance != null)
        {
            Vector3 selfPosition = transform.position + Vector3.up * vfxSpawnHeight;
            VFXManager.Instance.SpawnTreatVFX(selfPosition, this);
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

    [PunRPC]
    void SyncTreatActivation(string treatType)
    {
        // Remote player treat activation - play VFX only
        if (useVFXManager && VFXManager.Instance != null)
        {
            Vector3 selfPosition = transform.position + Vector3.up * vfxSpawnHeight;
            VFXManager.Instance.SpawnTreatVFX(selfPosition, this);
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

        // Simplified teleport with VFX
        StartCoroutine(SimplifiedTeleport(range));

        if (debugMode)
        {
            Debug.Log($"🌀 TELEPORT: Started!");
        }
    }

    IEnumerator SimplifiedTeleport(float range)
    {
        isTeleporting = true;

        // Store original position
        Vector3 originalPosition = transform.position;
        Vector3 departurePosition = originalPosition;

        // Calculate strategic teleport position
        Vector3 teleportPosition = CalculateStrategicTeleportPosition(range);

        // SIMPLIFIED VFX: Departure and arrival effects
        if (useVFXManager && VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnTeleportVFX(departurePosition, teleportPosition, this);
        }

        // Small delay for VFX
        yield return new WaitForSeconds(0.1f);

        // Teleport TO new position
        transform.position = teleportPosition;

        // Begin grace period
        if (movementRestrictor != null)
        {
            movementRestrictor.BeginTeleportGracePeriod();
        }

        // Wait for strategic positioning time
        yield return new WaitForSeconds(1.2f);

        // Teleport BACK to safe position
        Vector3 returnPosition = CalculateReturnPosition(originalPosition);

        // Small delay for return VFX
        yield return new WaitForSeconds(0.1f);

        transform.position = returnPosition;

        // End teleport override
        if (movementRestrictor != null)
        {
            movementRestrictor.EndTeleportOverride();
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

    /// <summary>
    /// Enhanced input enabled control for match management
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        networkInputEnabled = enabled;

        // Also disable movement when input is disabled
        if (!enabled)
        {
            velocity = Vector3.zero;
        }

        // Sync across network if this is the local player
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncInputState", RpcTarget.Others, enabled);
        }
    }

    /// <summary>
    /// Enhanced movement enabled control
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        networkMovementEnabled = enabled;

        if (!enabled)
        {
            velocity = Vector3.zero;
        }

        // Sync across network if this is the local player
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncMovementState", RpcTarget.Others, enabled);
        }
    }

    [PunRPC]
    void SyncInputState(bool enabled)
    {
        networkInputEnabled = enabled;
        inputEnabled = enabled;
    }

    [PunRPC]
    void SyncMovementState(bool enabled)
    {
        networkMovementEnabled = enabled;
        movementEnabled = enabled;

        if (!enabled)
        {
            velocity = Vector3.zero;
        }
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

    // Public interface methods for other systems
    public CharacterData GetCharacterData() => characterData;
    public bool IsGrounded() => isGrounded;
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

    /// <summary>
    /// Set VFX Manager usage (useful for testing/performance)
    /// </summary>
    public void SetUseVFXManager(bool useVFX)
    {
        useVFXManager = useVFX;

        if (debugMode)
        {
            Debug.Log($"{characterData?.characterName} VFX Manager usage: {useVFX}");
        }
    }

    /// <summary>
    /// Get VFX spawn position with offset
    /// </summary>
    public Vector3 GetVFXSpawnPosition()
    {
        return transform.position + Vector3.up * vfxSpawnHeight;
    }

    /// <summary>
    /// Get match manager reference for external systems
    /// </summary>
    public MatchManager GetMatchManager() => currentMatch;

    /// <summary>
    /// Get round manager reference for external systems  
    /// </summary>
    public RoundManager GetRoundManager() => currentRound;

    /// <summary>
    /// Check if this is the local player
    /// </summary>
    public bool IsLocalPlayer()
    {
        return photonView != null && photonView.IsMine;
    }

    /// <summary>
    /// Get network player actor number
    /// </summary>
    public int GetActorNumber()
    {
        return photonView != null ? photonView.Owner.ActorNumber : -1;
    }

    /// <summary>
    /// Enhanced ability cooldown management for match restarts
    /// </summary>
    public void ResetAbilityCooldowns()
    {
        if (!IsLocalPlayer()) return;

        ultimateOnCooldown = false;
        trickOnCooldown = false;
        treatOnCooldown = false;

        // Reset charges
        currentUltimateCharge = 0f;
        currentTrickCharge = 0f;
        currentTreatCharge = 0f;

        // Sync reset across network
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncAbilityReset", RpcTarget.Others);
        }
    }

    [PunRPC]
    void SyncAbilityReset()
    {
        ultimateOnCooldown = false;
        trickOnCooldown = false;
        treatOnCooldown = false;
        currentUltimateCharge = 0f;
        currentTrickCharge = 0f;
        currentTreatCharge = 0f;

        // Update UI
        OnUltimateChargeChanged?.Invoke(0f);
        OnTrickChargeChanged?.Invoke(0f);
        OnTreatChargeChanged?.Invoke(0f);
    }

    /// <summary>
    /// Match restart compatibility - reset player state
    /// </summary>
    public void ResetForNewMatch()
    {
        if (!IsLocalPlayer()) return;

        // Reset movement and input
        SetInputEnabled(true);
        SetMovementEnabled(true);

        // Reset physics state
        velocity = Vector3.zero;
        isDashing = false;
        isTeleporting = false;

        // Reset abilities
        ResetAbilityCooldowns();

        // Reset duck state
        isDucking = false;
        duckingStateChanged = true;

        // Reset character specific states
        hasDoubleJumped = false;
        canDash = true;
    }

    /// <summary>
    /// Enhanced OnPhotonSerializeView with match state data
    /// </summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!enableNetworkSync) return;

        if (stream.IsWriting)
        {
            // Local player - send data to others
            stream.SendNext(transform.position);
            stream.SendNext(velocity);
            stream.SendNext(isGrounded);
            stream.SendNext(isDucking);
            stream.SendNext(hasBall);
            stream.SendNext(isDashing);
            stream.SendNext(inputEnabled);
            stream.SendNext(movementEnabled);
            stream.SendNext(Time.time); // Timestamp for lag compensation
        }
        else
        {
            // Remote player - receive data from others
            networkPosition = (Vector3)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            networkIsGrounded = (bool)stream.ReceiveNext();
            networkIsDucking = (bool)stream.ReceiveNext();
            networkHasBall = (bool)stream.ReceiveNext();
            bool networkIsDashing = (bool)stream.ReceiveNext();
            networkInputEnabled = (bool)stream.ReceiveNext();
            networkMovementEnabled = (bool)stream.ReceiveNext();
            float timestamp = (float)stream.ReceiveNext();

            // Calculate network lag for prediction
            networkLag = Mathf.Abs((float)(PhotonNetwork.Time - timestamp));

            // Apply lag compensation to position
            if (networkVelocity.magnitude > 0.1f)
            {
                networkPosition += networkVelocity * networkLag;
            }

            // Update dash state for remote players
            if (networkIsDashing != isDashing)
            {
                isDashing = networkIsDashing;
            }

            // Update input/movement states
            inputEnabled = networkInputEnabled;
            movementEnabled = networkMovementEnabled;
        }
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

    /// <summary>
    /// Enhanced debug info with network state
    /// </summary>
    void OnGUI()
    {
        if (!debugMode || characterData == null) return;

        // Determine which player this is based on actor number
        int actorNumber = GetActorNumber();
        float yOffset = actorNumber == 2 ? 200f : 50f;

        GUILayout.BeginArea(new Rect(10, yOffset, 450, 350));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"Character: {characterData.characterName} (Actor {actorNumber})");
        GUILayout.Label($"Network: {(IsLocalPlayer() ? "LOCAL" : "REMOTE")} | Owner: {photonView?.Owner?.NickName ?? "None"}");
        GUILayout.Label($"Health: {playerHealth?.GetCurrentHealth()}/{characterData.maxHealth}");
        GUILayout.Label($"Ultimate ({characterData.ultimateType}): {currentUltimateCharge:F1}/{characterData.ultimateChargeRequired}");
        GUILayout.Label($"Trick ({characterData.trickType}): {currentTrickCharge:F1}/{characterData.trickChargeRequired}");
        GUILayout.Label($"Treat ({characterData.treatType}): {currentTreatCharge:F1}/{characterData.treatChargeRequired}");
        GUILayout.Label($"State: Ground={isGrounded} Duck={isDucking} Ball={hasBall}");
        GUILayout.Label($"Input: Enabled={inputEnabled} NetworkEnabled={networkInputEnabled}");
        GUILayout.Label($"Movement: Enabled={movementEnabled} NetworkEnabled={networkMovementEnabled}");

        // Match state info
        if (currentMatch != null)
        {
            GUILayout.Label($"Match State: {currentMatch.GetMatchState()}");
            GUILayout.Label($"Round: {currentMatch.GetCurrentRound()} | Time: {currentMatch.GetRemainingTime():F1}s");

            currentMatch.GetRoundScores(out int p1Rounds, out int p2Rounds);
            GUILayout.Label($"Score: {p1Rounds} - {p2Rounds}");
        }

        // Duck system info
        if (duckSystem != null)
        {
            GUILayout.Label($"Duck System: Can={duckSystem.CanDuck()} Active={duckSystem.IsDucking()}");

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

        // VFX System Info
        GUILayout.Label($"VFX Manager: {(useVFXManager ? "ENABLED" : "DISABLED")}");
        if (useVFXManager)
        {
            bool vfxManagerExists = VFXManager.Instance != null;
            GUILayout.Label($"VFX Status: {(vfxManagerExists ? "ACTIVE" : "MISSING")}");

            if (vfxManagerExists)
            {
                VFXManager.Instance.GetVFXStats(out int total, out int hit, out int ultimate, out int ability);
                GUILayout.Label($"VFX: {total} (H:{hit} U:{ultimate} A:{ability})");
            }
        }

        // Network lag info
        if (PhotonNetwork.IsConnected)
        {
            GUILayout.Label($"Network Lag: {networkLag:F3}s | Ping: {PhotonNetwork.GetPing()}ms");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}