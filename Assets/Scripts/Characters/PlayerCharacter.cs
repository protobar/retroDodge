using UnityEngine;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using RetroDodgeRumble.Animation;
/// <summary>
/// FIXED PlayerCharacter with proper PUN2 multiplayer integration
/// Fixes: Character data sync, facing direction, round reset issues
/// </summary>
public class PlayerCharacter : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Character Setup")]
    [SerializeField] public CharacterData characterData;

    [Header("Network Settings")]
    [SerializeField] private bool enableNetworkSync = true;
    [SerializeField] private bool debugMode = false;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayerMask = 1;
    [SerializeField] private float groundCheckRadius = 0.3f;

    [Header("Player Facing")]
    [SerializeField] private bool facingRight = true;
    private int playerSide = 0; // 0 = not set, 1 = left player, 2 = right player

    [Header("Animation Throw Settings")]
    [SerializeField] private bool useAnimationEvents = true;
    [SerializeField] private float throwAnimationDelay = 0.3f; // Fallback if no animation event
    private bool ballThrowQueued = false;
    private ThrowType queuedThrowType;
    private int queuedDamage;

    // Core Components - cached once
    private PlayerInputHandler inputHandler;
    private CapsuleCollider characterCollider;
    private PlayerHealth playerHealth;
    private AudioSource audioSource;
    private ArenaMovementRestrictor movementRestrictor;
    private DuckSystem duckSystem;
    private PlayerAnimationController animationController;

    // Movement state
    private Transform characterTransform;
    private Vector3 velocity;
    public bool isGrounded, isDucking, hasBall = false;
    public bool movementEnabled = true, inputEnabled = true;

    // Character-specific state
    public bool hasDoubleJumped = false;
    private bool canDash = true;
    private float lastDashTime;
    public bool isDashing, isTeleporting = false;
    
    // Speed boost state - per player instance
    private float originalMoveSpeed = -1f;
    private bool isSpeedBoostActive = false;
    private bool isSlowSpeedActive = false;
    private float slowSpeedMultiplier = 1f;

    // Match integration
    private MatchManager currentMatch;

    // AFK System
    private SimpleAFKDetector afkDetector;

    // Ability system - unified
    private float[] abilityCharges = new float[3]; // Ultimate, Trick, Treat
    private bool[] abilityCooldowns = new bool[3];
    private readonly float[] cooldownTimes = { 3f, 8f, 10f };

    // Collider cache for ducking
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;

    // Network sync data
    private Vector3 networkPosition;
    private bool networkIsGrounded, networkIsDucking, networkHasBall;
    
    // OPTIMIZED: Network interpolation and optimization
    private Vector3 networkVelocity;
    private float lastNetworkTime;
    
    // Performance optimization flags
    private bool hasSignificantMovement = false;
    private bool hasStateChanged = false;
    private bool hasAbilityChanged = false;
    
    // PUN2 optimization tracking
    private float lastSendRateChange = 0f;
    private const float SEND_RATE_CHANGE_COOLDOWN = 1f; // Don't change send rate too frequently

    // FIXED: Character data sync tracking
    private bool characterDataLoaded = false;
    private bool isDataSyncComplete = false;

    // Events for UI/systems
    public System.Action<CharacterData> OnCharacterLoaded;
    public System.Action<float>[] OnAbilityChargeChanged = new System.Action<float>[3];

    // Constants
    private const float GROUND_CHECK_DISTANCE = 0.15f; // Distance to check below collider
    private const float GROUND_STICK_FORCE = 2f; // Small downward force to stick to ground
    private const float PICKUP_RANGE = 1.2f;
    private const float VFX_SPAWN_HEIGHT = 1.5f;

    void Awake()
    {
        CacheComponents();
        SetupSystemComponents();
    }

    void Start()
    {
        currentMatch = FindObjectOfType<MatchManager>();
        SetupNetworkBehavior();

        // FIXED: Wait for character data from network if not local player
        if (!IsLocalPlayer())
        {
            StartCoroutine(WaitForCharacterDataSync());
        }
        
        // OPTIMIZED: Set initial network send rate
        OptimizeNetworkSendRate();
        
        // Initialize AFK System
        InitializeAFKSystem();
    }

    void CacheComponents()
    {
        characterTransform = transform;
        characterCollider = GetComponent<CapsuleCollider>();
        inputHandler = GetComponent<PlayerInputHandler>();
        playerHealth = GetComponent<PlayerHealth>();
        afkDetector = GetComponent<SimpleAFKDetector>();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        animationController = GetComponent<PlayerAnimationController>();

        if (characterCollider != null)
        {
            originalColliderHeight = characterCollider.height;
            originalColliderCenter = characterCollider.center;
        }

        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;
    }

    void SetupSystemComponents()
    {
        movementRestrictor = GetComponent<ArenaMovementRestrictor>() ??
                           gameObject.AddComponent<ArenaMovementRestrictor>();
        duckSystem = GetComponent<DuckSystem>() ??
                    gameObject.AddComponent<DuckSystem>();
    }
    
    void InitializeAFKSystem()
    {
        // Add Simple AFK Detector if not present
        if (afkDetector == null)
        {
            afkDetector = gameObject.AddComponent<SimpleAFKDetector>();
        }
        
        if (debugMode)
        {
            Debug.Log($"[PlayerCharacter] Simple AFK detector initialized for {name}");
        }
    }

    void SetupNetworkBehavior()
    {
        if (PhotonNetwork.OfflineMode)
        {
            if (inputHandler != null) inputHandler.isPUN2Enabled = false;
            return;
        }
        if (photonView?.IsMine == true && inputHandler != null)
        {
            inputHandler.isPUN2Enabled = true;
        }
    }

    /// <summary>
    /// FIXED: Wait for character data sync on remote clients
    /// </summary>
    IEnumerator WaitForCharacterDataSync()
    {
        float timeout = 5f; // 5 second timeout
        float elapsed = 0f;

        while (!isDataSyncComplete && elapsed < timeout)
        {
            // Try to get character data from player properties
            if (photonView.Owner.CustomProperties.ContainsKey("CharacterIndex"))
            {
                int characterIndex = (int)photonView.Owner.CustomProperties["CharacterIndex"];
                LoadCharacterFromNetwork(characterIndex);
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isDataSyncComplete)
        {
            Debug.LogWarning($"[SYNC TIMEOUT] Character data sync timed out for player {photonView.Owner.ActorNumber}");
        }
    }

    void Update()
    {
        // FIXED: Check ground FIRST before any input or movement
        CheckGrounded();

        // FIXED: Only process if character data is loaded
        if (characterData == null || (!IsLocalPlayer() && !isDataSyncComplete)) return;

        if (PhotonNetwork.OfflineMode || photonView?.IsMine != false)
        {
            HandleInput();
            HandleMovement(); // Gravity is handled here
            HandleDucking();
            HandleBallInteraction();
            UpdateAbilityCharges();
            UpdateNetworkOptimization();
        }
        else
        {
            InterpolateNetworkMovement();
        }
    }

    // ══════════════════════════════════════════════════════════
    // FIXED CHARACTER DATA INTEGRATION
    // ══════════════════════════════════════════════════════════

    public void LoadCharacter(CharacterData newCharacterData)
    {
        characterData = newCharacterData;
        characterDataLoaded = true;
        isDataSyncComplete = true;

        ApplyCharacterStats();
        OnCharacterLoaded?.Invoke(characterData);

        int actor = 0;
        try { actor = photonView?.Owner != null ? photonView.Owner.ActorNumber : 0; } catch { actor = 0; }
        Debug.Log($"[CHARACTER LOADED] {characterData.characterName} loaded for player {(actor != 0 ? actor.ToString() : "LOCAL")}" );
    }

    /// <summary>
    /// FIXED: Load character data from network properties
    /// </summary>
    void LoadCharacterFromNetwork(int characterIndex)
    {
        if (currentMatch != null)
        {
            CharacterData networkCharacterData = currentMatch.GetCharacterData(characterIndex);
            if (networkCharacterData != null)
            {
                LoadCharacter(networkCharacterData);

                // Apply network synced color if available
                if (photonView.Owner.CustomProperties.ContainsKey("CharacterColor_R"))
                {
                    float r = (float)photonView.Owner.CustomProperties["CharacterColor_R"];
                    float g = (float)photonView.Owner.CustomProperties["CharacterColor_G"];
                    float b = (float)photonView.Owner.CustomProperties["CharacterColor_B"];
                    Color networkColor = new Color(r, g, b, 1f);
                    ForceApplyColor(networkColor);
                }

                Debug.Log($"[NETWORK LOAD] Loaded character {networkCharacterData.characterName} from network properties");
            }
        }
    }

    void ApplyCharacterStats()
    {
        if (characterData == null) return;

        // Apply health stats
        playerHealth?.SetMaxHealth(characterData.maxHealth);
        playerHealth?.SetHealth(characterData.maxHealth);

        // Reset ability charges based on character data
        abilityCharges[0] = 0f; // Ultimate
        abilityCharges[1] = 0f; // Trick  
        abilityCharges[2] = 0f; // Treat

        // Reset movement states
        hasDoubleJumped = false;
        canDash = true;

        // Apply visual changes
        ApplyCharacterVisuals();
    }

    void ApplyCharacterVisuals()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null && characterData.characterColor != Color.white)
        {
            renderer.material.color = characterData.characterColor;
        }
    }

    /// <summary>
    /// FIXED: Force apply color for network sync
    /// </summary>
    public void ForceApplyColor(Color networkColor)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer.material != null)
            {
                renderer.material.color = networkColor;
            }
        }

        Debug.Log($"[COLOR SYNC] Applied network color: {networkColor} for player {photonView.Owner.ActorNumber}");
    }

    // ══════════════════════════════════════════════════════════
    // FIXED PLAYER FACING SYSTEM
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// CRITICAL FIX: Set player side and facing direction based on spawn position
    /// </summary>
    public void SetPlayerSide(int side)
    {
        playerSide = side;

        if (side == 1) // Left player (Master Client)
        {
            facingRight = true; // Left player faces right towards opponent
            Debug.Log($"[FACING] Player set as LEFT player - facing RIGHT");

            // Ensure player 1 has 0° rotation
            transform.rotation = Quaternion.Euler(0, 0f, 0);
        }
        else if (side == 2) // Right player (Remote Client)
        {
            facingRight = false; // Right player faces left towards opponent
            Debug.Log($"[FACING] Player set as RIGHT player - facing LEFT");

            // CRITICAL: Force 180° rotation for player 2
            transform.rotation = Quaternion.Euler(0, 180f, 0);
            Debug.Log($"[FACING] Applied 180° rotation to Player 2");
        }

        // Network sync the facing direction
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncPlayerFacing", RpcTarget.Others, facingRight, side);
        }
    }


    /// <summary>
    /// Network sync for player facing direction
    /// </summary>
    [PunRPC]
    void SyncPlayerFacing(bool isFacingRight, int side)
    {
        facingRight = isFacingRight;
        playerSide = side;

        if (!facingRight && side == 2)
        {
            FlipCharacterModel();
        }

        Debug.Log($"[NETWORK] Synced facing for player {photonView.Owner.ActorNumber}: facingRight={facingRight}, side={side}");
    }

    /// <summary>
    /// Flip the character model visually (not the collider)
    /// </summary>
    void FlipCharacterModel()
    {
        Transform modelTransform = transform.GetChild(0); // Assuming first child is the model
        if (modelTransform != null)
        {
            // Rotate instead of flipping scale
            float targetYRotation = facingRight ? 0f : 180f;
            modelTransform.localRotation = Quaternion.Euler(0, targetYRotation, 0);

            Debug.Log($"[FLIP] Character model rotated - facingRight: {facingRight}");
        }
        else
        {
            // Fallback: rotate the main transform
            float targetYRotation = facingRight ? 0f : 180f;
            transform.localRotation = Quaternion.Euler(0, targetYRotation, 0);
        }
    }


    /// <summary>
    /// FIXED: Get throw direction based on player facing
    /// </summary>
    public Vector3 GetThrowDirection()
    {
        // Always throw towards opponent
        if (playerSide == 1) // Left player always throws right
        {
            return Vector3.right;
        }
        else if (playerSide == 2) // Right player always throws left
        {
            return Vector3.left;
        }

        // Fallback: use facing direction
        return facingRight ? Vector3.right : Vector3.left;
    }

    // ══════════════════════════════════════════════════════════
    // INPUT & MOVEMENT
    // ══════════════════════════════════════════════════════════

    void HandleInput()
    {
        if (!ShouldProcessInput()) return;

        // FIXED: Check if player actually has ball but hasBall is true
        if (hasBall)
        {
            var currentBall = BallManager.Instance?.GetCurrentBall();
            if (currentBall == null || currentBall.GetHolder() != this)
            {
                Debug.Log($"[BALL STATE] Player {name} has hasBall=true but no actual ball - clearing state");
                SetHasBall(false);
            }
        }

        // Jump system with character data integration
        if (inputHandler.GetJumpPressed())
        {
            Debug.Log($"[INPUT] Jump pressed - grounded:{isGrounded} ducking:{isDucking} canDJ:{characterData?.canDoubleJump} hasDJ:{hasDoubleJumped}");
            
            if (isGrounded && !isDucking)
            {
                Jump();
            }
            else if (characterData != null && characterData.canDoubleJump && !hasDoubleJumped && !isGrounded)
            {
                DoubleJump();
            }
        }

        // Dash system with character data integration
        if (inputHandler.GetDashPressed() && characterData.canDash && CanDash())
        {
            PerformDash();
        }

        // Abilities - only in fighting state
        if (IsMatchStateAllowingAbilities())
        {
            if (inputHandler.GetUltimatePressed() && CanUseAbility(0)) ActivateUltimate();
            if (inputHandler.GetTrickPressed() && CanUseAbility(1)) ActivateTrick();
            if (inputHandler.GetTreatPressed() && CanUseAbility(2)) ActivateTreat();
        }

        HandleDuckingInput();
    }

    bool ShouldProcessInput()
    {
        return inputHandler != null && characterData != null &&
               inputEnabled && (PhotonNetwork.OfflineMode || (photonView?.IsMine != false));
    }

    void HandleMovement()
    {
        if (!movementEnabled || isDashing || isDucking) return;

        float horizontal = inputHandler.GetHorizontal();

        // Horizontal movement using character data
        if (Mathf.Abs(horizontal) > 0.01f)
        {
            Vector3 moveDir = Vector3.right * horizontal * GetEffectiveMoveSpeed();
            characterTransform.position += moveDir * Time.deltaTime;
        }

        // FIXED: Apply gravity ONLY if not grounded
        if (!isGrounded)
        {
            // Standard gravity
            velocity.y -= 25f * Time.deltaTime;

            // Clamp falling speed to prevent falling through ground
            velocity.y = Mathf.Max(velocity.y, -50f); // Terminal velocity
        }
        else
        {
            // FIXED: Stick to ground when grounded
            if (velocity.y < 0)
            {
                velocity.y = -GROUND_STICK_FORCE; // Small downward force to stay on ground
            }
            else if (velocity.y > 0)
            {
                // Allow jumping upward
                // Don't modify velocity.y here
            }
            else
            {
                // On ground, not jumping - zero velocity
                velocity.y = 0f;
            }
        }

        // Apply vertical movement
        characterTransform.position += Vector3.up * velocity.y * Time.deltaTime;

        // Apply movement restrictions
        if (movementRestrictor != null)
        {
            Vector3 restricted = movementRestrictor.ApplyMovementRestriction(characterTransform.position);
            characterTransform.position = restricted;
        }

        // Update animations
        UpdateMovementAnimations();
    }

    void Jump()
    {
        // FIXED: Don't manually set isGrounded here - let CheckGrounded handle it
        velocity.y = characterData.jumpHeight;

        // Animation
        animationController?.TriggerJump();
        animationController?.SetGrounded(false); // Update animation immediately

        PlayCharacterSound(CharacterAudioType.Jump);
        SyncPlayerAction("Jump");

        if (debugMode)
        {
            Debug.Log($"[JUMP] Executed jump - velocity.y set to {velocity.y}");
        }
    }

    void DoubleJump()
    {
        Debug.Log($"[DOUBLE JUMP] Attempting double jump - hasDoubleJumped: {hasDoubleJumped}, canDoubleJump: {characterData?.canDoubleJump}, isGrounded: {isGrounded}");

        // Validation checks
        if (hasDoubleJumped)
        {
            Debug.Log($"[DOUBLE JUMP] Blocked - already double jumped");
            return;
        }

        if (characterData == null || !characterData.canDoubleJump)
        {
            Debug.Log($"[DOUBLE JUMP] Blocked - character cannot double jump");
            return;
        }

        if (isGrounded)
        {
            Debug.Log($"[DOUBLE JUMP] Blocked - player is grounded");
            return;
        }

        // FIXED: Allow double jump when in air (removed velocity.y check)
        Debug.Log($"[DOUBLE JUMP] Executing double jump!");
        velocity.y = characterData.jumpHeight * 0.8f;
        hasDoubleJumped = true;

        // Animation
        animationController?.TriggerDoubleJump();

        PlayCharacterSound(CharacterAudioType.Jump);

        // Sync to network
        if (photonView != null && photonView.IsMine)
        {
            SyncPlayerAction("DoubleJump");
            photonView.RPC("SyncDoubleJumpState", RpcTarget.Others, hasDoubleJumped);
        }
    }

    /// <summary>
    /// FIXED: Force ground check immediately after spawn/teleport
    /// Call this after any position change that might affect ground state
    /// </summary>
    public void ForceGroundCheck()
    {
        // Wait one physics frame for colliders to settle
        StartCoroutine(ForceGroundCheckCoroutine());
    }

    IEnumerator ForceGroundCheckCoroutine()
    {
        // Wait for physics to settle
        yield return new WaitForFixedUpdate();

        // Force ground check
        CheckGrounded();

        if (isGrounded)
        {
            // Snap to ground if we're close
            if (characterCollider != null)
            {
                RaycastHit hit;
                Vector3 origin = characterTransform.position + characterCollider.center;
                float distance = characterCollider.height * 0.5f + 1f;

                if (Physics.Raycast(origin, Vector3.down, out hit, distance, groundLayerMask))
                {
                    // Snap to ground surface
                    float targetY = hit.point.y + (characterCollider.height * 0.5f) - characterCollider.center.y;
                    characterTransform.position = new Vector3(
                        characterTransform.position.x,
                        targetY,
                        characterTransform.position.z
                    );

                    velocity.y = 0f;

                    Debug.Log($"[SPAWN] Snapped to ground at Y={targetY:F3}");
                }
            }
        }

        Debug.Log($"[SPAWN] Force ground check complete - isGrounded: {isGrounded}, Y: {characterTransform.position.y:F3}");
    }

    bool CanDash()
    {
        return canDash && !isDashing && (Time.time - lastDashTime) >= characterData.GetDashCooldown();
    }

    void PerformDash()
    {
        // Animation
        animationController?.TriggerDash();
        animationController?.SetDashing(true);
        
        SyncPlayerAction("Dash");
        StartCoroutine(DashCoroutine());
    }

    IEnumerator DashCoroutine()
    {
        isDashing = true;
        lastDashTime = Time.time;
        canDash = false;

        float horizontal = inputHandler.GetHorizontal();
        Vector3 dashDir = horizontal != 0 ? Vector3.right * Mathf.Sign(horizontal) : characterTransform.right;

        float dashSpeed = characterData.GetDashDistance() / characterData.GetDashDuration();
        float elapsed = 0f;

        PlayCharacterSound(CharacterAudioType.Dash);
        SpawnEffect(characterData.dashEffect);

        while (elapsed < characterData.GetDashDuration())
        {
            characterTransform.Translate(dashDir * dashSpeed * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }

        isDashing = false;
        // Animation
        animationController?.SetDashing(false);
        
        yield return new WaitForSeconds(characterData.GetDashCooldown());
        canDash = true;
    }

    void HandleDuckingInput()
    {
        bool newDuckState = duckSystem?.IsDucking() ?? inputHandler.GetDuckHeld() && isGrounded;

        if (newDuckState != isDucking)
        {
            isDucking = newDuckState;
            ApplyDuckingCollider();
            
            // Animation
            animationController?.SetDucking(isDucking);
        }
    }

    void HandleDucking()
    {
        // Duck system handles the logic, we just apply visual changes
        ApplyDuckingCollider();
    }

    void ApplyDuckingCollider()
    {
        if (characterCollider == null) return;

        if (isDucking)
        {
            characterCollider.height = originalColliderHeight * 0.5f;
            characterCollider.center = new Vector3(originalColliderCenter.x,
                originalColliderCenter.y - (originalColliderHeight * 0.25f), originalColliderCenter.z);
        }
        else
        {
            characterCollider.height = originalColliderHeight;
            characterCollider.center = originalColliderCenter;
        }
    }

    // ══════════════════════════════════════════════════════════
    // BALL SYSTEM - FIXED THROW DIRECTION
    // ══════════════════════════════════════════════════════════

    void HandleBallInteraction()
    {
        if (BallManager.Instance == null || !IsMatchStateAllowingAbilities()) return;

        // Pickup
        if (inputHandler.GetPickupPressed() && !hasBall)
        {
            TryPickupBall();
        }

        // Throw
        if (inputHandler.GetThrowPressed())
        {
            Debug.Log($"[THROW] Button pressed! hasBall={hasBall}");

            if (hasBall)
            {
                ThrowBall();
            }
            else
            {
                Debug.Log("[THROW] No ball to throw");
            }
        }
    }

    void TryPickupBall()
    {
        var ball = BallManager.Instance.GetCurrentBall();
        if (ball?.IsFree() == true &&
            Vector3.Distance(transform.position, ball.transform.position) <= PICKUP_RANGE)
        {
        if (BallManager.Instance.RequestBallPickup(this))
        {
            // Animation
            animationController?.TriggerCatch();
            animationController?.SetHasBall(true);
            
            AddAbilityCharge(0, 5f); // Small charge for pickup
        }
        }
    }

    void ThrowBall()
    {
        // Always trigger animation first
        animationController?.TriggerThrow();

        if (!hasBall)
        {
            // Play animation but don't actually throw
            PlayCharacterSound(CharacterAudioType.Throw);
            return;
        }

        ThrowType throwType = isGrounded ? ThrowType.Normal : ThrowType.JumpThrow;
        int damage = characterData.GetThrowDamage(throwType);

        // Queue the ball throw to happen at animation event
        if (useAnimationEvents)
        {
            // Wait for animation event to call ExecuteBallThrow()
            ballThrowQueued = true;
            queuedThrowType = throwType;
            queuedDamage = damage;

            Debug.Log($"[THROW] Queued ball throw - waiting for animation event");

            // Fallback: if animation event doesn't fire, throw after delay
            StartCoroutine(ThrowBallFallback());
        }
        else
        {
            // Immediate throw (old behavior)
            ExecuteBallThrow(throwType, damage);
        }
    }

    /// <summary>
    /// Called by Animation Event at the exact throw frame
    /// </summary>
    public void OnThrowAnimationEvent()
    {
        Debug.Log($"[ANIM EVENT] OnThrowAnimationEvent called - ballThrowQueued: {ballThrowQueued}, hasBall: {hasBall}");

        if (ballThrowQueued && hasBall)
        {
            ExecuteBallThrow(queuedThrowType, queuedDamage);
            ballThrowQueued = false;
        }
    }

    /// <summary>
    /// Fallback in case animation event doesn't fire
    /// </summary>
    IEnumerator ThrowBallFallback()
    {
        yield return new WaitForSeconds(throwAnimationDelay);

        if (ballThrowQueued && hasBall)
        {
            Debug.LogWarning("[THROW] Animation event didn't fire, using fallback timing");
            ExecuteBallThrow(queuedThrowType, queuedDamage);
            ballThrowQueued = false;
        }
    }

    /// <summary>
    /// Extracted actual throw logic - called by animation event or fallback
    /// </summary>
    void ExecuteBallThrow(ThrowType throwType, int damage)
    {
        if (!hasBall)
        {
            Debug.LogWarning("[THROW] ExecuteBallThrow called but player doesn't have ball!");
            return;
        }

        Vector3 throwDirection = GetThrowDirection();

        SpawnThrowVFX(throwType);

        SetHasBall(false);
        animationController?.SetHasBall(false);

        BallManager.Instance.RequestBallThrowWithCharacterData(this, characterData, throwType, damage);

        PlayCharacterSound(CharacterAudioType.Throw);

        // Add ability charges
        AddAbilityCharge(0, 15f);
        AddAbilityCharge(1, 10f);
        AddAbilityCharge(2, 10f);

        Debug.Log($"[THROW] Ball thrown successfully! Type: {throwType}, Damage: {damage}");
    }


    // ══════════════════════════════════════════════════════════
    // FIXED ROUND RESET SYSTEM
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// FIXED: Reset player state for round reset
    /// </summary>
    public void ResetPlayerState()
    {
        // Reset movement state
        velocity = Vector3.zero;
        hasDoubleJumped = false;
        isDashing = false;
        isTeleporting = false;
        canDash = true;

        // Reset ducking state
        if (duckSystem != null)
        {
            duckSystem.ResetDuckSystem();
        }
        else
        {
            isDucking = false;
            ApplyDuckingCollider();
        }

        // Reset ball possession
        hasBall = false;

        // Reset abilities
        for (int i = 0; i < abilityCharges.Length; i++)
        {
            abilityCharges[i] = 0f;
            abilityCooldowns[i] = false;
        }

        // Reset speed effects
        isSpeedBoostActive = false;
        isSlowSpeedActive = false;
        slowSpeedMultiplier = 1f;
        originalMoveSpeed = -1f;

        // Re-enable movement and input
        movementEnabled = true;
        inputEnabled = true;

        // Reset health if needed
        if (playerHealth != null && playerHealth.IsDead())
        {
            playerHealth.RevivePlayer();
        }

        // FIXED: Force ground check after reset
        ForceGroundCheck();

        Debug.Log($"[RESET] Player state reset completed for {gameObject.name}");
    }

    /// <summary>
    /// Force ground check for reset
    /// </summary>
    void CheckGroundedState()
    {
        // This method is now redundant - use CheckGrounded() instead
        CheckGrounded();
    }

    // ══════════════════════════════════════════════════════════
    // UNIFIED ABILITY SYSTEM (UNCHANGED - WORKING)
    // ══════════════════════════════════════════════════════════

    bool CanUseAbility(int abilityIndex)
    {
        if (characterData == null || abilityCooldowns[abilityIndex]) return false;

        float required = GetRequiredCharge(abilityIndex);
        return abilityCharges[abilityIndex] >= required;
    }

    float GetRequiredCharge(int abilityIndex)
    {
        switch (abilityIndex)
        {
            case 0: return characterData.ultimateChargeRequired;
            case 1: return characterData.trickChargeRequired;
            case 2: return characterData.treatChargeRequired;
            default: return 100f;
        }
    }

    void AddAbilityCharge(int abilityIndex, float amount)
    {
        if (!IsLocalPlayer()) return;

        float rate = GetChargeRate(abilityIndex);
        float required = GetRequiredCharge(abilityIndex);

        abilityCharges[abilityIndex] = Mathf.Min(abilityCharges[abilityIndex] + (amount * rate), required);
        OnAbilityChargeChanged[abilityIndex]?.Invoke(abilityCharges[abilityIndex] / required);
    }

    float GetChargeRate(int abilityIndex)
    {
        switch (abilityIndex)
        {
            case 0: return characterData.ultimateChargeRate;
            case 1: return characterData.trickChargeRate;
            case 2: return characterData.treatChargeRate;
            default: return 1f;
        }
    }

    void UpdateAbilityCharges()
    {
        for (int i = 0; i < 3; i++)
        {
            float required = GetRequiredCharge(i);
            OnAbilityChargeChanged[i]?.Invoke(abilityCharges[i] / required);
        }
    }

    IEnumerator AbilityCooldown(int abilityIndex)
    {
        abilityCooldowns[abilityIndex] = true;
        yield return new WaitForSeconds(cooldownTimes[abilityIndex]);
        abilityCooldowns[abilityIndex] = false;
    }

    // ══════════════════════════════════════════════════════════
    // SPECIFIC ABILITIES (UNCHANGED - WORKING)
    // ══════════════════════════════════════════════════════════

    void ActivateUltimate()
    {
        abilityCharges[0] = 0f;
        StartCoroutine(AbilityCooldown(0));
        
        // Animation
        animationController?.TriggerUltimate();
        
        if (!PhotonNetwork.OfflineMode) SyncPlayerAction("Ultimate");

        SpawnUltimateVFX();

        switch (characterData.ultimateType)
        {
            case UltimateType.PowerThrow: ExecutePowerThrow(); break;
            case UltimateType.MultiThrow: StartCoroutine(ExecuteMultiThrow()); break;
            case UltimateType.Curveball: ExecuteCurveball(); break;
        }
    }

    void ActivateTrick()
    {
        abilityCharges[1] = 0f;
        StartCoroutine(AbilityCooldown(1));

        // Animation
        animationController?.TriggerTrick();

        // FIXED: Sync trick activation to other players
        if (!PhotonNetwork.OfflineMode && photonView.IsMine)
        {
            photonView.RPC("SyncPlayerAction", RpcTarget.Others, "Trick");
        }
        else if (PhotonNetwork.OfflineMode)
        {
            // In offline mode, execute directly
            var opponent = FindOpponent();
            if (opponent != null)
            {
                SpawnTrickVFX(opponent);
                ExecuteTrick(opponent);
            }
        }

        // For online mode, also execute locally for immediate feedback
        if (!PhotonNetwork.OfflineMode)
        {
            var opponent = FindOpponent();
            if (opponent != null)
            {
                SpawnTrickVFX(opponent);
                ExecuteTrick(opponent);
            }
        }
    }

    public void ActivateTreat()
    {
        abilityCharges[2] = 0f;
        StartCoroutine(AbilityCooldown(2));

        // Animation
        animationController?.TriggerTreat();

        // FIXED: Sync treat activation to other players
        if (!PhotonNetwork.OfflineMode && photonView.IsMine)
        {
            photonView.RPC("SyncPlayerAction", RpcTarget.Others, "Treat");
        }

        // Always execute treat locally (affects self)
        SpawnTreatVFX();
        ExecuteTreat();
    }

    void ExecutePowerThrow()
    {
        if (!hasBall) return;

        var ball = BallManager.Instance.GetCurrentBall();
        if (ball != null)
        {
            if (useAnimationEvents)
            {
                // Queue ultimate throw for animation event
                ballThrowQueued = true;
                queuedThrowType = ThrowType.Ultimate;
                queuedDamage = characterData.GetUltimateDamage();

                Debug.Log($"[ULTIMATE] Queued ultimate throw - waiting for animation event");

                StartCoroutine(ThrowBallFallback());
            }
            else
            {
                // Immediate ultimate throw (old behavior)
                ExecuteUltimateThrow();
            }
        }
    }

    /// <summary>
    /// Called by Ultimate Animation Event
    /// </summary>
    public void OnUltimateThrowAnimationEvent()
    {
        Debug.Log($"[ANIM EVENT] OnUltimateThrowAnimationEvent called");

        if (ballThrowQueued && hasBall)
        {
            ExecuteUltimateThrow();
            ballThrowQueued = false;
        }
    }

    /// <summary>
    /// Execute the actual ultimate throw
    /// </summary>
    void ExecuteUltimateThrow()
    {
        if (!hasBall) return;

        var ball = BallManager.Instance.GetCurrentBall();
        if (ball != null)
        {
            int damage = characterData.GetUltimateDamage();
            float speed = characterData.GetUltimateSpeed();
            Vector3 throwDirection = GetThrowDirection();

            float powerMultiplier = isGrounded ? 1.5f : 2.0f;

            ball.SetThrowData(ThrowType.Ultimate, damage, speed);
            ball.ThrowBall(throwDirection, powerMultiplier);
            SetHasBall(false);

            float shakeIntensity = characterData.GetPowerThrowScreenShake() * 1.5f;
            CameraShakeManager.Instance.TriggerShake(shakeIntensity, 0.8f, $"PowerThrow_{characterData?.characterName ?? "Unknown"}");

            Debug.Log($"[ULTIMATE] Ball thrown! Damage: {damage}, Speed: {speed}");
        }
    }

    IEnumerator ExecuteMultiThrow()
    {
        if (!hasBall) yield break;

        int count = characterData.GetMultiThrowCount();
        int damage = characterData.GetUltimateDamage();
        float speed = characterData.GetUltimateSpeed();
        float delay = characterData.GetMultiThrowDelay();

        Vector3 throwDir = GetThrowDirection();

        // FIXED: First throw the original ball, then spawn multi-balls
        var originalBall = BallManager.Instance.GetCurrentBall();
        if (originalBall != null)
        {
            // Throw the original ball first
            originalBall.SetThrowData(ThrowType.Ultimate, damage, speed);
            originalBall.ThrowBall(throwDir, 1.5f);
            SetHasBall(false); // FIXED: Set hasBall to false after throwing original ball
        }

        // Wait a bit before spawning multi-balls
        yield return new WaitForSeconds(0.1f);

        // FIXED: Use BallManager for proper network instantiation
        if (BallManager.Instance != null)
        {
            // Use BallManager's MultiThrowCoroutine method for proper network handling
            BallManager.Instance.StartCoroutine(BallManager.Instance.MultiThrowCoroutine(this, characterData));
        }
        else
        {
            // Fallback: Create balls locally (for testing)
        for (int i = 0; i < count; i++)
        {
            var ballObj = Instantiate(BallManager.Instance.ballPrefab,
                transform.position + characterData.GetMultiThrowSpawnOffset(), Quaternion.identity);

            var ballController = ballObj.GetComponent<BallController>();
            if (ballController != null)
            {
                float angleOffset = (i - (count - 1) * 0.5f) * (characterData.GetMultiThrowSpread() / count);
                Vector3 spreadDir = Quaternion.Euler(0, angleOffset, 0) * throwDir;

                ballController.SetThrowData(ThrowType.Ultimate, damage, speed);
                ballController.SetThrower(this);
                    ballController.ThrowBallInternal(spreadDir.normalized, 1f);

                Destroy(ballObj, 4f);
            }

            yield return new WaitForSeconds(delay);
            }
        }
    }

    void ExecuteCurveball()
    {
        if (!hasBall) return;

        var ball = BallManager.Instance.GetCurrentBall();
        if (ball != null)
        {
            Vector3 throwDir = GetThrowDirection(); // FIXED: Use proper direction

            int damage = characterData.GetUltimateDamage();
            float speed = characterData.GetUltimateSpeed();

            ball.SetThrowData(ThrowType.Ultimate, damage, speed);
            ball.ThrowBall(throwDir, 1f);
            SetHasBall(false); // FIXED: Set hasBall to false after throwing

            StartCoroutine(CurveballBehavior(ball));
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
            var velocity = ball.GetVelocity();
            velocity.y = Mathf.Sin(elapsed * frequency) * amplitude;
            ball.SetVelocity(velocity);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void ExecuteTrick(PlayerCharacter opponent)
    {
        if (opponent == null) return;

        if (PhotonNetwork.OfflineMode)
        {
            // Offline mode: apply trick effect directly
            ApplyTrickEffectLocal(opponent, characterData.trickType);
        }
        else if (photonView.IsMine)
        {
            // Online mode: sync trick effects across network
            PhotonView opponentView = opponent.GetComponent<PhotonView>();
            if (opponentView != null)
            {
                photonView.RPC("ApplyTrickEffect", RpcTarget.All, opponentView.ViewID, (int)characterData.trickType);
            }
        }
    }

    void ExecuteTreat()
    {
        if (PhotonNetwork.OfflineMode)
        {
            // Offline mode: apply treat effect directly
            ApplyTreatEffectLocal(this, characterData.treatType);
        }
        else if (photonView.IsMine)
        {
            // Online mode: sync treat effects across network
            photonView.RPC("ApplyTreatEffect", RpcTarget.All, photonView.ViewID, (int)characterData.treatType);
        }
    }

    IEnumerator ApplySlowSpeed(PlayerCharacter opponent)
    {
        if (opponent.characterData == null || opponent.isSlowSpeedActive) yield break;

        // Apply slow speed to opponent using per-player system
        opponent.isSlowSpeedActive = true;
        opponent.slowSpeedMultiplier = characterData.GetSlowSpeedMultiplier();

        yield return new WaitForSeconds(characterData.GetSlowSpeedDuration());

        // Restore opponent's speed
        if (opponent != null)
        {
            opponent.isSlowSpeedActive = false;
            opponent.slowSpeedMultiplier = 1f;
        }
    }

    // Local versions for offline mode
    void ApplyTrickEffectLocal(PlayerCharacter targetPlayer, TrickType trickType)
    {
        if (targetPlayer == null) return;

        switch (trickType)
        {
            case TrickType.SlowSpeed:
                StartCoroutine(ApplySlowSpeed(targetPlayer));
                break;
            case TrickType.Freeze:
                StartCoroutine(ApplyFreeze(targetPlayer));
                break;
            case TrickType.InstantDamage:
                ApplyInstantDamage(targetPlayer);
                break;
        }
    }

    void ApplyTreatEffectLocal(PlayerCharacter targetPlayer, TreatType treatType)
    {
        if (targetPlayer == null) return;

        switch (treatType)
        {
            case TreatType.Shield:
                targetPlayer.ApplyShield();
                break;
            case TreatType.Teleport:
                targetPlayer.ExecuteTeleport();
                break;
            case TreatType.SpeedBoost:
                targetPlayer.StartCoroutine(targetPlayer.ApplySpeedBoost());
                break;
        }
    }

    IEnumerator ApplyFreeze(PlayerCharacter opponent)
    {
        opponent.SetMovementEnabled(false);
        opponent.SetInputEnabled(false);

        yield return new WaitForSeconds(characterData.GetFreezeDuration());

        opponent.SetMovementEnabled(true);
        opponent.SetInputEnabled(true);
    }

    void ApplyInstantDamage(PlayerCharacter opponent)
    {
        Debug.Log($"[INSTANT DAMAGE] Applying {characterData.GetInstantDamageAmount()} damage to {opponent.name}");
        var health = opponent.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(characterData.GetInstantDamageAmount(), null);
            Debug.Log($"[INSTANT DAMAGE] Damage applied successfully to {opponent.name}");
        }
        else
        {
            Debug.LogWarning($"[INSTANT DAMAGE] No PlayerHealth component found on {opponent.name}");
        }
    }

    void ApplyShield()
    {
        playerHealth?.SetTemporaryInvulnerability(characterData.GetShieldDuration());
    }

    void ExecuteTeleport()
    {
        if (isTeleporting) return;
        StartCoroutine(TeleportSequence());
    }

    IEnumerator TeleportSequence()
    {
        isTeleporting = true;
        movementRestrictor?.StartTeleportOverride();

        Vector3 teleportPos = CalculateTeleportPosition();
        transform.position = teleportPos;

        yield return new WaitForSeconds(1.2f);

        Vector3 returnPos = CalculateReturnPosition();
        transform.position = returnPos;

        movementRestrictor?.EndTeleportOverride();
        isTeleporting = false;
    }

    Vector3 CalculateTeleportPosition()
    {
        var opponent = FindOpponent();
        if (opponent != null)
        {
            // Flank behind opponent using proper direction
            Vector3 dir = GetThrowDirection(); // Use same logic as throw
            return opponent.transform.position + dir * characterData.GetTeleportRange() * 0.7f;
        }

        // Default: move toward center
        return Vector3.Lerp(transform.position, Vector3.zero, 0.5f);
    }

    Vector3 CalculateReturnPosition()
    {
        if (movementRestrictor != null)
        {
            movementRestrictor.GetPlayerBounds(out float minX, out float maxX);
            float centerX = (minX + maxX) * 0.5f;
            return new Vector3(centerX, transform.position.y, transform.position.z);
        }
        return transform.position;
    }

    IEnumerator ApplySpeedBoost()
    {
        if (characterData == null || isSpeedBoostActive) yield break;

        // Store original speed if not already stored
        if (originalMoveSpeed < 0f)
        {
            originalMoveSpeed = characterData.moveSpeed;
        }

        // Apply speed boost to this player instance only
        isSpeedBoostActive = true;
        // Don't modify the shared characterData, use a multiplier in movement calculation instead
        // The actual speed modification will be handled in the movement code

        yield return new WaitForSeconds(characterData.GetSpeedBoostDuration());

        // Restore original speed
        isSpeedBoostActive = false;
    }
    
    // Get the effective move speed for this player (includes speed boost and slow effects)
    public float GetEffectiveMoveSpeed()
    {
        if (characterData == null) return 5f; // Default speed
        
        float baseSpeed = characterData.moveSpeed;
        float multiplier = 1f;
        
        if (isSpeedBoostActive)
        {
            multiplier *= characterData.GetSpeedBoostMultiplier();
        }
        
        if (isSlowSpeedActive)
        {
            multiplier *= slowSpeedMultiplier;
        }
        
        return baseSpeed * multiplier;
    }

    // ══════════════════════════════════════════════════════════
    // NETWORKING & UTILITY
    // ══════════════════════════════════════════════════════════

    void InterpolateNetworkMovement()
    {
        if (!enableNetworkSync) return;

        // OPTIMIZED: Improved interpolation with velocity prediction
        float distance = Vector3.Distance(transform.position, networkPosition);
        
        if (distance > 5f)
        {
            // Teleport if too far (network lag or desync)
            transform.position = networkPosition;
        }
        else if (distance > 0.1f)
        {
            // Use velocity-based interpolation for smoother movement
            Vector3 targetPosition = networkPosition + (networkVelocity * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 25f);
        }

        // Apply network state
        isGrounded = networkIsGrounded;
        isDucking = networkIsDucking;
        hasBall = networkHasBall;
    }

    [PunRPC]
    void SyncPlayerAction(string actionType)
    {
        if (photonView.IsMine) return;

        switch (actionType)
        {
            case "Jump":
                animationController?.TriggerJump();
                PlayCharacterSound(CharacterAudioType.Jump);
                break;
            case "DoubleJump":
                animationController?.TriggerDoubleJump();
                PlayCharacterSound(CharacterAudioType.Jump);
                break;
            case "Dash":
                animationController?.TriggerDash();
                PlayCharacterSound(CharacterAudioType.Dash);
                SpawnEffect(characterData.dashEffect);
                break;
            case "Ultimate":
                animationController?.TriggerUltimate();
                SpawnUltimateVFX();
                break;
            case "Trick":
                animationController?.TriggerTrick();
                // FIXED: Sync trick effects to other players
                var opponent = FindOpponent();
                if (opponent != null)
                {
                    SpawnTrickVFX(opponent);
                    ExecuteTrick(opponent); // FIXED: Also execute trick effect
                }
                break;
            case "Treat":
                animationController?.TriggerTreat();
                // FIXED: Sync treat effects to other players
                SpawnTreatVFX();
                break;
        }
    }

    [PunRPC]
    void SyncDoubleJumpState(bool doubleJumped)
    {
        if (photonView.IsMine) return;
        hasDoubleJumped = doubleJumped;
    }

    [PunRPC]
    void ApplyTrickEffect(int targetViewID, int trickType)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;

        PlayerCharacter targetPlayer = targetView.GetComponent<PlayerCharacter>();
        if (targetPlayer == null) return;

        // FIXED: Apply trick effect based on type and target ownership
        switch ((TrickType)trickType)
        {
            case TrickType.SlowSpeed:
                if (!targetView.IsMine) // Apply to opponent
                    StartCoroutine(ApplySlowSpeed(targetPlayer));
                break;
            case TrickType.Freeze:
                if (!targetView.IsMine) // Apply to opponent
                    StartCoroutine(ApplyFreeze(targetPlayer));
                break;
            case TrickType.InstantDamage:
                if (!targetView.IsMine) // Apply to opponent
                    ApplyInstantDamage(targetPlayer);
                break;
        }
    }

    [PunRPC]
    void ApplyTreatEffect(int targetViewID, int treatType)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null || !targetView.IsMine) return;

        PlayerCharacter targetPlayer = targetView.GetComponent<PlayerCharacter>();
        if (targetPlayer == null) return;

        // Apply treat effect based on type
        switch ((TreatType)treatType)
        {
            case TreatType.Shield:
                ApplyShield();
                break;
            case TreatType.Teleport:
                ExecuteTeleport();
                break;
            case TreatType.SpeedBoost:
                StartCoroutine(ApplySpeedBoost());
                break;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!enableNetworkSync) return;

        if (stream.IsWriting)
        {
            // FIXED: Always send consistent data structure
            // Position and velocity (always sent for smooth movement)
            stream.SendNext(transform.position);
            stream.SendNext(velocity);
            
            // State data (always sent, but we'll optimize frequency internally)
            stream.SendNext(isGrounded);
            stream.SendNext(isDucking);
            stream.SendNext(hasBall);
            stream.SendNext(facingRight);
            stream.SendNext(playerSide);
            
            // Ability charges (always sent for UI sync)
            stream.SendNext(abilityCharges[0]); // Ultimate charge
            stream.SendNext(abilityCharges[1]); // Trick charge
            stream.SendNext(abilityCharges[2]); // Treat charge
        }
        else
        {
            // FIXED: Always receive consistent data structure
            // Receive position and velocity
            Vector3 newPosition = (Vector3)stream.ReceiveNext();
            Vector3 newVelocity = (Vector3)stream.ReceiveNext();
            
            // Calculate network velocity for interpolation
            float currentTime = Time.time;
            float deltaTime = currentTime - lastNetworkTime;
            lastNetworkTime = currentTime;
            
            if (deltaTime > 0)
            {
                networkVelocity = (newPosition - networkPosition) / deltaTime;
            }
            
            networkPosition = newPosition;
            
            // Receive state changes
            networkIsGrounded = (bool)stream.ReceiveNext();
            networkIsDucking = (bool)stream.ReceiveNext();
            networkHasBall = (bool)stream.ReceiveNext();

            // Handle facing direction changes
            bool networkFacingRight = (bool)stream.ReceiveNext();
            int networkPlayerSide = (int)stream.ReceiveNext();

            if (facingRight != networkFacingRight || playerSide != networkPlayerSide)
            {
                facingRight = networkFacingRight;
                playerSide = networkPlayerSide;
                FlipCharacterModel();
            }
            
            // Receive ability charges
            abilityCharges[0] = (float)stream.ReceiveNext();
            abilityCharges[1] = (float)stream.ReceiveNext();
            abilityCharges[2] = (float)stream.ReceiveNext();
        }
    }

    // ══════════════════════════════════════════════════════════
    // NETWORK OPTIMIZATION
    // ══════════════════════════════════════════════════════════
    
    void OptimizeNetworkSendRate()
    {
        // FIXED: PUN2 uses global send rate settings, not per PhotonView
        if (currentMatch != null && currentMatch.GetMatchState() == MatchManager.MatchState.Fighting)
        {
            // High frequency during active gameplay
            PhotonNetwork.SendRate = 20; // 20 updates per second
            PhotonNetwork.SerializationRate = 20; // 20 serializations per second
        }
        else
        {
            // Lower frequency during non-active states
            PhotonNetwork.SendRate = 10; // 10 updates per second
            PhotonNetwork.SerializationRate = 10; // 10 serializations per second
        }
    }
    
    void UpdateNetworkOptimization()
    {
        if (!enableNetworkSync) return;
        
        // Check for significant movement
        float movementThreshold = 0.1f;
        hasSignificantMovement = velocity.magnitude > movementThreshold;
        
        // Check for state changes
        hasStateChanged = (isGrounded != networkIsGrounded) || 
                         (isDucking != networkIsDucking) || 
                         (hasBall != networkHasBall);
        
        // Check for ability changes
        hasAbilityChanged = false;
        for (int i = 0; i < abilityCharges.Length; i++)
        {
            if (Mathf.Abs(abilityCharges[i] - (i == 0 ? 0f : abilityCharges[i])) > 0.01f)
            {
                hasAbilityChanged = true;
                break;
            }
        }
        
        // FIXED: PUN2 optimization with cooldown to prevent constant changes
        // Only change send rate if enough time has passed and we're the master client
        if (Time.time - lastSendRateChange >= SEND_RATE_CHANGE_COOLDOWN && PhotonNetwork.IsMasterClient)
        {
            bool isActive = hasSignificantMovement || hasStateChanged || hasAbilityChanged;
            int targetSendRate = isActive ? 20 : 15; // More conservative approach
            
            if (PhotonNetwork.SendRate != targetSendRate)
            {
                PhotonNetwork.SendRate = targetSendRate;
                PhotonNetwork.SerializationRate = targetSendRate;
                lastSendRateChange = Time.time;
                
                if (debugMode)
                    Debug.Log($"[NETWORK OPT] Send rate changed to {targetSendRate} (Active: {isActive})");
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// FIXED: Robust ground detection using multiple methods
    /// </summary>
    void CheckGrounded()
    {
        if (characterCollider == null)
        {
            isGrounded = false;
            return;
        }

        // FIXED: Calculate check position CORRECTLY
        // Start from the center of the collider
        Vector3 colliderCenter = characterTransform.position + characterCollider.center;

        // Calculate the actual bottom of the capsule (local offset from center)
        float halfHeight = characterCollider.height * 0.5f;
        Vector3 capsuleBottom = colliderCenter - Vector3.up * halfHeight;

        // FIXED: Check slightly BELOW the collider bottom, not AT it
        Vector3 checkPosition = capsuleBottom + Vector3.down * GROUND_CHECK_DISTANCE;

        // Primary check: Sphere check at capsule bottom
        bool sphereCheck = Physics.CheckSphere(
            checkPosition,
            groundCheckRadius,
            groundLayerMask
        );

        // Secondary check: Raycast from center downward (more reliable for edges)
        bool raycastCheck = Physics.Raycast(
            colliderCenter,
            Vector3.down,
            halfHeight + GROUND_CHECK_DISTANCE * 2f,
            groundLayerMask
        );

        // Tertiary check: Multiple raycasts at edges (catches narrow platforms)
        bool edgeCheck = false;
        if (!sphereCheck && !raycastCheck)
        {
            float radius = characterCollider.radius * 0.8f;
            Vector3[] edgePoints = new Vector3[]
            {
            capsuleBottom + Vector3.left * radius,
            capsuleBottom + Vector3.right * radius,
            capsuleBottom + Vector3.forward * radius,
            capsuleBottom + Vector3.back * radius
            };

            foreach (Vector3 edgePoint in edgePoints)
            {
                if (Physics.Raycast(edgePoint, Vector3.down, GROUND_CHECK_DISTANCE * 2f, groundLayerMask))
                {
                    edgeCheck = true;
                    break;
                }
            }
        }

        // Consider grounded if ANY check passes
        bool wasGrounded = isGrounded;
        isGrounded = sphereCheck || raycastCheck || edgeCheck;

        // Debug visualization
        if (debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[GROUND] Sphere:{sphereCheck} Ray:{raycastCheck} Edge:{edgeCheck} => Grounded:{isGrounded} | Pos:{checkPosition.y:F3}");
        }

        // FIXED: Landing detection - reset double jump when landing
        if (!wasGrounded && isGrounded)
        {
            hasDoubleJumped = false;

            // Reset vertical velocity when landing
            if (velocity.y < 0)
            {
                velocity.y = 0f;
            }

            if (debugMode)
            {
                Debug.Log($"[GROUND] Landed! Resetting double jump and velocity");
            }
        }

        // FIXED: Update animations
        if (animationController != null)
        {
            animationController.SetGrounded(isGrounded);
        }
    }

    bool IsMatchStateAllowingAbilities()
    {
        if (currentMatch == null) return true; // Offline fallback: allow
        return currentMatch.GetMatchState() == MatchManager.MatchState.Fighting;
    }

    PlayerCharacter FindOpponent()
    {
        var players = FindObjectsOfType<PlayerCharacter>();
        foreach (var player in players)
        {
            if (player != this) return player;
        }
        return null;
    }

    void PlayCharacterSound(CharacterAudioType audioType)
    {
        var clip = characterData?.GetRandomAudioClip(audioType);
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void SpawnEffect(GameObject effect)
    {
        if (effect != null)
        {
            Instantiate(effect, transform.position, Quaternion.identity);
        }
    }

    void SpawnThrowVFX(ThrowType throwType)
    {
        if (VFXManager.Instance == null) return;

        Vector3 pos = transform.position + Vector3.up * VFX_SPAWN_HEIGHT;
        VFXManager.Instance.SpawnThrowVFX(pos, this, throwType);
    }

    void SpawnUltimateVFX()
    {
        if (VFXManager.Instance == null) return;

        Vector3 pos = transform.position + Vector3.up * VFX_SPAWN_HEIGHT;
        VFXManager.Instance.SpawnUltimateActivationVFX(pos, this);
    }

    void SpawnTrickVFX(PlayerCharacter opponent)
    {
        if (VFXManager.Instance == null) return;

        Vector3 pos = opponent.transform.position + Vector3.up * VFX_SPAWN_HEIGHT;
        VFXManager.Instance.SpawnTrickVFX(pos, this, opponent);
    }

    void SpawnTreatVFX()
    {
        if (VFXManager.Instance == null) return;

        Vector3 pos = transform.position + Vector3.up * VFX_SPAWN_HEIGHT;
        VFXManager.Instance.SpawnTreatVFX(pos, this);
    }

    // ══════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════

    public CharacterData GetCharacterData() => characterData;
    public bool IsGrounded() => isGrounded;
    public bool IsDucking() => duckSystem?.IsDucking() ?? isDucking;
    public bool HasBall() => hasBall;
    public void SetHasBall(bool value) 
    { 
        hasBall = value;
        animationController?.SetHasBall(value);
    }
    public bool IsLocalPlayer() => PhotonNetwork.OfflineMode || (photonView?.IsMine != false);
    public PlayerInputHandler GetInputHandler() => inputHandler;
    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;
    public void SetMovementEnabled(bool enabled) => movementEnabled = enabled;
    public int GetPlayerSide() => playerSide;
    public bool IsFacingRight() => facingRight;

    public void OnDamageTaken(int damage)
    {
        if (!IsLocalPlayer()) return;
        AddAbilityCharge(0, damage * 0.5f);
        AddAbilityCharge(1, damage * 0.3f);
        AddAbilityCharge(2, damage * 0.4f);
    }

    public void OnSuccessfulCatch()
    {
        if (!IsLocalPlayer()) return;
        
        // Animation
        animationController?.TriggerCatch();
        
        AddAbilityCharge(0, 25f);
        AddAbilityCharge(1, 15f);
        AddAbilityCharge(2, 15f);
    }

    public void OnSuccessfulDodge()
    {
        if (!IsLocalPlayer()) return;
        AddAbilityCharge(0, 20f);
        AddAbilityCharge(1, 12f);
        AddAbilityCharge(2, 12f);
    }

    // ═══════════════════════════════════════════════════════════════
    // ANIMATION SYSTEM INTEGRATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Update movement animations based on current movement state
    /// FIXED: Inverts speed for player 2 (180° rotated) so animations play correctly
    /// </summary>
    void UpdateMovementAnimations()
    {
        if (animationController == null) return;

        // Update states
        animationController.SetGrounded(isGrounded);
        animationController.SetHasBall(hasBall);
        animationController.SetDucking(isDucking);

        if (inputHandler != null)
        {
            float horizontal = inputHandler.GetHorizontal(); // -1 to 1

            // Use signed speed: positive = right, negative = left
            float signedSpeed = horizontal * GetEffectiveMoveSpeed();

            // CRITICAL FIX: Invert speed for player 2 (right side player who is rotated 180°)
            // When player 2 moves left (negative speed), they're actually moving "forward" relative to their rotation
            // When player 2 moves right (positive speed), they're actually moving "backward" relative to their rotation
            if (playerSide == 2 && !facingRight)
            {
                signedSpeed = -signedSpeed; // Flip the sign for rotated player

                if (debugMode && Mathf.Abs(horizontal) > 0.1f)
                {
                    Debug.Log($"[ANIM] Player 2 speed inverted: input={horizontal:F2}, original speed={horizontal * GetEffectiveMoveSpeed():F2}, inverted speed={signedSpeed:F2}");
                }
            }

            animationController.SetSpeed(signedSpeed);
        }
        else
        {
            animationController.SetSpeed(0f);
        }
    }

    /// <summary>
    /// CharacterController reference for movement speed calculation
    /// Fallback property if you want to use Unity's CharacterController
    /// </summary>
    private CharacterController CharacterController => GetComponent<CharacterController>();

    public void ResetForNewMatch()
    {
        if (!IsLocalPlayer()) return;

        SetInputEnabled(true);
        SetMovementEnabled(true);
        velocity = Vector3.zero;
        isDashing = false;
        isTeleporting = false;

        // Reset abilities
        for (int i = 0; i < 3; i++)
        {
            abilityCharges[i] = 0f;
            abilityCooldowns[i] = false;
        }

        // Reset character states
        hasDoubleJumped = false;
        canDash = true;
        isDucking = false;

        // Reset duck system
        duckSystem?.ResetDuckSystem();

        // Apply normal collider (in case was ducking)
        ApplyDuckingCollider();

        // Reset movement restrictor teleport state
        if (movementRestrictor != null)
        {
            movementRestrictor.EndTeleportOverride();
        }
    }

    /// <summary>
    /// FIXED: Proper round revival method for MatchManager integration
    /// </summary>
    public void ReviveForNewRound()
    {
        // Re-enable the component if it was disabled
        enabled = true;

        // Reset for new match
        ResetForNewMatch();

        // Reset health through PlayerHealth component
        if (playerHealth != null)
        {
            playerHealth.RevivePlayer();
        }

        // Reset ball state
        SetHasBall(false);

        // Sync revival across network if local player
        if (IsLocalPlayer() && photonView != null)
        {
            photonView.RPC("SyncPlayerRevival", RpcTarget.Others);
        }
    }

    [PunRPC]
    void SyncPlayerRevival()
    {
        // Remote player revival sync
        enabled = true;
        SetInputEnabled(true);
        SetMovementEnabled(true);
        SetHasBall(false);
    }

    // Ability charge getters for UI
    public float GetUltimateChargePercentage() =>
        characterData != null ? abilityCharges[0] / characterData.ultimateChargeRequired : 0f;
    public float GetTrickChargePercentage() =>
        characterData != null ? abilityCharges[1] / characterData.trickChargeRequired : 0f;
    public float GetTreatChargePercentage() =>
        characterData != null ? abilityCharges[2] / characterData.treatChargeRequired : 0f;

    public int GetThrowDamage(ThrowType throwType) => characterData?.GetThrowDamage(throwType) ?? 10;
    public float GetDamageResistance() => characterData?.damageResistance ?? 1f;
    
    // ═══════════════════════════════════════════════════════════════
    // SIMPLE AFK SYSTEM INTEGRATION
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Check if player is AFK
    /// </summary>
    public bool IsAFK()
    {
        return afkDetector != null && afkDetector.IsAFK();
    }
    
    /// <summary>
    /// Force reset AFK timer (for external use)
    /// </summary>
    public void ResetAFKTimer()
    {
        if (afkDetector != null)
        {
            afkDetector.ForceResetAFKTimer();
        }
    }
    
    
}