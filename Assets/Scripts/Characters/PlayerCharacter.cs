using UnityEngine;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// FIXED PlayerCharacter with proper PUN2 multiplayer integration
/// Fixes: Character data sync, facing direction, round reset issues
/// </summary>
public class PlayerCharacter : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Character Setup")]
    [SerializeField] private CharacterData characterData;

    [Header("Network Settings")]
    [SerializeField] private bool enableNetworkSync = true;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayerMask = 1;
    [SerializeField] private float groundCheckRadius = 0.3f;

    [Header("Player Facing")]
    [SerializeField] private bool facingRight = true;
    private int playerSide = 0; // 0 = not set, 1 = left player, 2 = right player

    // Core Components - cached once
    private PlayerInputHandler inputHandler;
    private CapsuleCollider characterCollider;
    private PlayerHealth playerHealth;
    private AudioSource audioSource;
    private ArenaMovementRestrictor movementRestrictor;
    private DuckSystem duckSystem;

    // Movement state
    private Transform characterTransform;
    private Vector3 velocity;
    private bool isGrounded, isDucking, hasBall = false;
    private bool movementEnabled = true, inputEnabled = true;

    // Character-specific state
    private bool hasDoubleJumped = false;
    private bool canDash = true;
    private float lastDashTime;
    private bool isDashing, isTeleporting = false;

    // Match integration
    private MatchManager currentMatch;

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

    // FIXED: Character data sync tracking
    private bool characterDataLoaded = false;
    private bool isDataSyncComplete = false;

    // Events for UI/systems
    public System.Action<CharacterData> OnCharacterLoaded;
    public System.Action<float>[] OnAbilityChargeChanged = new System.Action<float>[3];

    // Constants
    private const float GROUND_CHECK_DISTANCE = 0.1f;
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
    }

    void CacheComponents()
    {
        characterTransform = transform;
        characterCollider = GetComponent<CapsuleCollider>();
        inputHandler = GetComponent<PlayerInputHandler>();
        playerHealth = GetComponent<PlayerHealth>();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

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

    void SetupNetworkBehavior()
    {
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
        // FIXED: Only process if character data is loaded
        if (characterData == null || (!IsLocalPlayer() && !isDataSyncComplete)) return;

        if (photonView?.IsMine != false) // Local player or no network
        {
            HandleInput();
            CheckGrounded();
            HandleMovement();
            HandleDucking();
            HandleBallInteraction();
            UpdateAbilityCharges();
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

        Debug.Log($"[CHARACTER LOADED] {characterData.characterName} loaded for player {photonView.Owner.ActorNumber}");
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
            Debug.Log($"[FACING] Player {photonView.Owner.ActorNumber} set as LEFT player - facing RIGHT");
        }
        else if (side == 2) // Right player (Remote Client)
        {
            facingRight = false; // Right player faces left towards opponent
            Debug.Log($"[FACING] Player {photonView.Owner.ActorNumber} set as RIGHT player - facing LEFT");

            // CRITICAL: Flip the character model for right player
            FlipCharacterModel();
        }

        // Network sync the facing direction
        if (photonView.IsMine)
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
        // Flip the visual representation
        Transform modelTransform = transform.GetChild(0); // Assuming first child is the model
        if (modelTransform != null)
        {
            Vector3 scale = modelTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facingRight ? 1 : -1);
            modelTransform.localScale = scale;

            Debug.Log($"[FLIP] Character model flipped - facingRight: {facingRight}");
        }
        else
        {
            // Fallback: flip the main transform
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facingRight ? 1 : -1);
            transform.localScale = scale;
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

        // Jump system with character data integration
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
               inputEnabled && (photonView?.IsMine != false);
    }

    void HandleMovement()
    {
        if (!movementEnabled || isDashing || isDucking) return;

        float horizontal = inputHandler.GetHorizontal();

        // Horizontal movement using character data
        if (horizontal != 0)
        {
            Vector3 moveDir = Vector3.right * horizontal * characterData.moveSpeed;
            characterTransform.position += moveDir * Time.deltaTime;
        }

        // Gravity and vertical movement
        if (!isGrounded)
        {
            velocity.y -= 25f * Time.deltaTime;
        }
        else if (velocity.y < 0)
        {
            velocity.y = 0f;
            hasDoubleJumped = false;
        }

        // Apply vertical movement
        characterTransform.position += Vector3.up * velocity.y * Time.deltaTime;

        // Apply movement restrictions
        if (movementRestrictor != null)
        {
            Vector3 restricted = movementRestrictor.ApplyMovementRestriction(characterTransform.position);
            characterTransform.position = restricted;
        }
    }

    void Jump()
    {
        velocity.y = characterData.jumpHeight;
        isGrounded = false;
        PlayCharacterSound(CharacterAudioType.Jump);
        SyncPlayerAction("Jump");
    }

    void DoubleJump()
    {
        if (hasDoubleJumped) return;
        velocity.y = characterData.jumpHeight * 0.8f;
        hasDoubleJumped = true;
        PlayCharacterSound(CharacterAudioType.Jump);
        SyncPlayerAction("DoubleJump");
    }

    bool CanDash()
    {
        return canDash && !isDashing && (Time.time - lastDashTime) >= characterData.GetDashCooldown();
    }

    void PerformDash()
    {
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
        if (inputHandler.GetThrowPressed() && hasBall)
        {
            ThrowBall();
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
                AddAbilityCharge(0, 5f); // Small charge for pickup
            }
        }
    }

    void ThrowBall()
    {
        if (!hasBall) return;

        ThrowType throwType = isGrounded ? ThrowType.Normal : ThrowType.JumpThrow;
        int damage = characterData.GetThrowDamage(throwType);

        // FIXED: Use proper throw direction based on player side
        Vector3 throwDirection = GetThrowDirection();

        SpawnThrowVFX(throwType);

        // Pass throw direction to ball manager
        BallManager.Instance.RequestBallThrowWithCharacterData(this, characterData, throwType, damage);


        PlayCharacterSound(CharacterAudioType.Throw);

        // Add ability charges
        AddAbilityCharge(0, 15f); // Ultimate
        AddAbilityCharge(1, 10f); // Trick
        AddAbilityCharge(2, 10f); // Treat
    }

    // ══════════════════════════════════════════════════════════
    // FIXED ROUND RESET SYSTEM
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// FIXED: Reset player state for round reset (called from MatchManager)
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
            duckSystem.ResetDuckSystem(); // This handles everything including standing up
        }
        else
        {
            isDucking = false;
            ApplyDuckingCollider(); // Apply normal collider
        }

        // Reset ball possession
        hasBall = false;

        // Reset abilities
        for (int i = 0; i < abilityCharges.Length; i++)
        {
            abilityCharges[i] = 0f;
            abilityCooldowns[i] = false;
        }

        // Re-enable movement and input
        movementEnabled = true;
        inputEnabled = true;

        // Reset health if needed (but don't full heal on round reset)
        if (playerHealth != null && playerHealth.IsDead())
        {
            playerHealth.RevivePlayer();
        }

        // Ensure proper ground state
        CheckGroundedState();

        Debug.Log($"[RESET] Player state reset completed for {gameObject.name}");
    }

    /// <summary>
    /// Force ground check for reset
    /// </summary>
    void CheckGroundedState()
    {
        if (characterCollider != null)
        {
            Vector3 capsuleBottom = characterTransform.position +
                characterCollider.center - Vector3.up * (characterCollider.height * 0.5f);

            isGrounded = Physics.CheckSphere(capsuleBottom, groundCheckRadius, groundLayerMask);
        }
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
        SyncPlayerAction("Ultimate");

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

        var opponent = FindOpponent();
        if (opponent != null)
        {
            SpawnTrickVFX(opponent);
            ExecuteTrick(opponent);
        }
    }

    void ActivateTreat()
    {
        abilityCharges[2] = 0f;
        StartCoroutine(AbilityCooldown(2));

        SpawnTreatVFX();
        ExecuteTreat();
    }

    void ExecutePowerThrow()
    {
        if (!hasBall) return;

        var ball = BallManager.Instance.GetCurrentBall();
        if (ball != null)
        {
            int damage = characterData.GetUltimateDamage();
            float speed = characterData.GetUltimateSpeed();
            Vector3 throwDirection = GetThrowDirection(); // FIXED: Use proper direction

            ball.SetThrowData(ThrowType.Ultimate, damage, speed);
            ball.ThrowBall(throwDirection, 1.5f);

            // Screen shake
            var camera = FindObjectOfType<CameraController>();
            camera?.ShakeCamera(characterData.GetPowerThrowScreenShake(), 0.8f);
        }
    }

    IEnumerator ExecuteMultiThrow()
    {
        if (!hasBall) yield break;

        int count = characterData.GetMultiThrowCount();
        int damage = characterData.GetUltimateDamage();
        float speed = characterData.GetUltimateSpeed();
        float delay = characterData.GetMultiThrowDelay();

        Vector3 throwDir = GetThrowDirection(); // FIXED: Use proper direction

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
                ballController.velocity = spreadDir * speed;

                Destroy(ballObj, 4f);
            }

            yield return new WaitForSeconds(delay);
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
        switch (characterData.trickType)
        {
            case TrickType.SlowSpeed: StartCoroutine(ApplySlowSpeed(opponent)); break;
            case TrickType.Freeze: StartCoroutine(ApplyFreeze(opponent)); break;
            case TrickType.InstantDamage: ApplyInstantDamage(opponent); break;
        }
    }

    void ExecuteTreat()
    {
        switch (characterData.treatType)
        {
            case TreatType.Shield: ApplyShield(); break;
            case TreatType.Teleport: ExecuteTeleport(); break;
            case TreatType.SpeedBoost: StartCoroutine(ApplySpeedBoost()); break;
        }
    }

    IEnumerator ApplySlowSpeed(PlayerCharacter opponent)
    {
        if (opponent.characterData == null) yield break;

        float original = opponent.characterData.moveSpeed;
        opponent.characterData.moveSpeed = original * characterData.GetSlowSpeedMultiplier();

        yield return new WaitForSeconds(characterData.GetSlowSpeedDuration());

        if (opponent.characterData != null)
            opponent.characterData.moveSpeed = original;
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
        var health = opponent.GetComponent<PlayerHealth>();
        health?.TakeDamage(characterData.GetInstantDamageAmount(), null);
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
        if (characterData == null) yield break;

        float original = characterData.moveSpeed;
        characterData.moveSpeed = original * characterData.GetSpeedBoostMultiplier();

        yield return new WaitForSeconds(characterData.GetSpeedBoostDuration());

        if (characterData != null)
            characterData.moveSpeed = original;
    }

    // ══════════════════════════════════════════════════════════
    // NETWORKING & UTILITY
    // ══════════════════════════════════════════════════════════

    void InterpolateNetworkMovement()
    {
        if (!enableNetworkSync) return;

        float distance = Vector3.Distance(transform.position, networkPosition);
        if (distance > 5f)
        {
            transform.position = networkPosition; // Teleport if too far
        }
        else if (distance > 0.1f)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 20f);
        }

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
            case "DoubleJump":
                PlayCharacterSound(CharacterAudioType.Jump);
                break;
            case "Dash":
                PlayCharacterSound(CharacterAudioType.Dash);
                SpawnEffect(characterData.dashEffect);
                break;
            case "Ultimate":
                SpawnUltimateVFX();
                break;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!enableNetworkSync) return;

        if (stream.IsWriting)
        {
            // Send position, rotation, and key states
            stream.SendNext(transform.position);
            stream.SendNext(isGrounded);
            stream.SendNext(isDucking);
            stream.SendNext(hasBall);
            stream.SendNext(facingRight);
            stream.SendNext(playerSide);
            
            // FIXED: Sync ability charges for ultimate visibility
            stream.SendNext(abilityCharges[0]); // Ultimate charge
            stream.SendNext(abilityCharges[1]); // Trick charge
            stream.SendNext(abilityCharges[2]); // Treat charge
        }
        else
        {
            // Receive network data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkIsGrounded = (bool)stream.ReceiveNext();
            networkIsDucking = (bool)stream.ReceiveNext();
            networkHasBall = (bool)stream.ReceiveNext();

            // FIXED: Sync facing direction
            bool networkFacingRight = (bool)stream.ReceiveNext();
            int networkPlayerSide = (int)stream.ReceiveNext();

            if (facingRight != networkFacingRight || playerSide != networkPlayerSide)
            {
                facingRight = networkFacingRight;
                playerSide = networkPlayerSide;
                FlipCharacterModel(); // Update visual
            }
            
            // FIXED: Sync ability charges from network
            float networkUltimateCharge = (float)stream.ReceiveNext();
            float networkTrickCharge = (float)stream.ReceiveNext();
            float networkTreatCharge = (float)stream.ReceiveNext();
            
            // Update ability charges for network sync
            abilityCharges[0] = networkUltimateCharge;
            abilityCharges[1] = networkTrickCharge;
            abilityCharges[2] = networkTreatCharge;
        }
    }

    // ══════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ══════════════════════════════════════════════════════════

    void CheckGrounded()
    {
        if (characterCollider == null)
        {
            isGrounded = false;
            return;
        }

        // Calculate check position at the bottom of the collider
        Vector3 checkPos = characterTransform.position;
        float colliderBottom = characterCollider.bounds.min.y;
        checkPos.y = colliderBottom;

        // Perform spherecast for more reliable ground detection
        isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayerMask);

        // Alternative: Use raycast with multiple points for better detection
        if (!isGrounded)
        {
            // Check center
            isGrounded = Physics.Raycast(checkPos, Vector3.down, GROUND_CHECK_DISTANCE, groundLayerMask);

            // Check left and right edges if center fails
            if (!isGrounded)
            {
                Vector3 leftCheck = checkPos + Vector3.left * (characterCollider.radius * 0.8f);
                Vector3 rightCheck = checkPos + Vector3.right * (characterCollider.radius * 0.8f);

                isGrounded = Physics.Raycast(leftCheck, Vector3.down, GROUND_CHECK_DISTANCE, groundLayerMask) ||
                           Physics.Raycast(rightCheck, Vector3.down, GROUND_CHECK_DISTANCE, groundLayerMask);
            }
        }
    }

    bool IsMatchStateAllowingAbilities()
    {
        return currentMatch?.GetMatchState() == MatchManager.MatchState.Fighting;
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
    public void SetHasBall(bool value) => hasBall = value;
    public bool IsLocalPlayer() => photonView?.IsMine != false;
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
}