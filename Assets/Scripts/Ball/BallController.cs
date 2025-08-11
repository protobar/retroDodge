using UnityEngine;

/// <summary>
/// Updated BallController that integrates with character system
/// Gets damage values from character data instead of hardcoding
/// </summary>
public class BallController : MonoBehaviour
{
    [Header("Ball Settings")]
    [SerializeField] private float baseSpeed = 25f;
    [SerializeField] private float gravity = 15f;
    [SerializeField] private float pickupRange = 1.2f;
    [SerializeField] private float bounceMultiplier = 0.6f;

    [Header("NEO GEO 1996 Authentic Physics")]
    [SerializeField] private float normalThrowSpeed = 18f;
    [SerializeField] private float jumpThrowSpeed = 22f;
    [SerializeField] private bool useNeoGeoPhysics = true;

    [Header("Ball Hold Position")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0.5f, 1.5f, 0f);
    [SerializeField] private bool useRelativeToPlayer = true;
    [SerializeField] private float holdFollowSpeed = 10f;
    [SerializeField] private bool smoothHoldMovement = true;

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
    private int currentDamage = 10; // Will be set by character data
    private float currentThrowSpeed = 18f; // Will be set by character data

    // Physics
    public Vector3 velocity;
    private bool isGrounded = false;
    private bool hasHitTarget = false;
    private bool homingEnabled = false;

    // References
    private Transform ballTransform;
    private Renderer ballRenderer;
    private CollisionDamageSystem collisionSystem;

    // Character system integration
    private PlayerCharacter holder; // NEW: Support for PlayerCharacter
    private CharacterController legacyHolder; // OLD: Backward compatibility
    private PlayerCharacter thrower;
    private CharacterController legacyThrower;
    private Transform targetOpponent;
    private bool isJumpThrow = false;

    // Ground detection
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float groundCheckDistance = 0.6f;

    void Awake()
    {
        ballTransform = transform;
        ballRenderer = GetComponent<Renderer>();

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
        // NO GRAVITY during thrown state - ball maintains exact trajectory
        if (useNeoGeoPhysics)
        {
            HandleNeoGeoPhysics();
        }

        // Apply homing if enabled
        if (homingEnabled)
        {
            ApplyHomingBehavior();
        }

        // Move the ball (no gravity applied here)
        ballTransform.Translate(velocity * Time.deltaTime, Space.World);

        // Check for ground collision
        CheckGrounded();
        if (isGrounded && velocity.y <= 0)
        {
            velocity.y = -velocity.y * bounceMultiplier;
            if (velocity.magnitude < 5f)
            {
                SetBallState(BallState.Free);
            }
        }

        // Check if ball went out of bounds
        if (ballTransform.position.y < -5f)
        {
            ResetBall();
        }
    }

    void HandleNeoGeoPhysics()
    {
        // No additional physics during throw - maintains trajectory
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

        // Check both new PlayerCharacter and legacy CharacterController
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

        // Legacy support
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

        if (ballRenderer != null)
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
            ThrowType.PowerThrow => new Color(1f, 0.5f, 0f), // Orange
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

    // ================================
    // CHARACTER SYSTEM INTEGRATION
    // ================================

    /// <summary>
    /// Set throw data from character system
    /// </summary>
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
    /// NEW: Try pickup with PlayerCharacter
    /// </summary>
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

            if (debugMode)
            {
                string characterName = character.GetCharacterData()?.characterName ?? character.name;
                Debug.Log($"{characterName} picked up the ball!");
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// LEGACY: Try pickup with old CharacterController
    /// </summary>
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

            if (debugMode)
            {
                Debug.Log($"{character.name} picked up the ball!");
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// NEW: Ball caught by PlayerCharacter
    /// </summary>
    public void OnCaught(PlayerCharacter catcher)
    {
        if (catcher == null)
        {
            Debug.LogError("BallController.OnCaught: PlayerCharacter catcher is null!");
            return;
        }

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

        if (debugMode)
        {
            string characterName = catcher.GetCharacterData()?.characterName ?? catcher.name;
            Debug.Log($"{characterName} caught the ball!");
        }
    }

    /// <summary>
    /// LEGACY: Ball caught by old CharacterController
    /// </summary>
    public void OnCaught(CharacterController catcher)
    {
        if (catcher == null)
        {
            Debug.LogError("BallController.OnCaught: CharacterController catcher is null!");
            return;
        }

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

        if (debugMode)
        {
            Debug.Log($"{catcher.name} caught the ball!");
        }
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

    // Add this to your BallController.cs - replace the collision integration section around line 501

    /// <summary>
    /// Enhanced throw method with character system support
    /// </summary>
    public void ThrowBall(Vector3 direction, float power)
    {
        if (currentState != BallState.Held) return;

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

        // FIXED: Notify collision system with correct thrower type
        if (collisionSystem != null)
        {
            if (thrower != null)
            {
                // New character system
                collisionSystem.OnBallThrown(thrower);
            }
            else if (legacyThrower != null)
            {
                // Legacy character system
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
                // Jump throws: direct diagonal trajectory
                throwDirection = (targetPos - throwPos).normalized;
                currentThrowType = ThrowType.JumpThrow;
            }
            else
            {
                // Normal throws: horizontal trajectory
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
            // Fallback direction
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

        if (debugMode)
        {
            Debug.Log($"Ball thrown: {currentThrowType}, Damage: {currentDamage}, Speed: {throwSpeed}");
        }
    }

    /// <summary>
    /// Find opponent for targeting
    /// </summary>
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

    public void ResetBall()
    {
        Vector3 spawnPosition = new Vector3(0, 2f, 0);
        ballTransform.position = spawnPosition;
        velocity = Vector3.zero;
        hasHitTarget = false;

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
                thrower = null;
                legacyThrower = null;
                targetOpponent = null;
                hasHitTarget = false;
                isJumpThrow = false;
                homingEnabled = false;
                if (collisionSystem != null)
                {
                    collisionSystem.OnBallReset();
                }
                break;
            case BallState.Held:
                velocity = Vector3.zero;
                break;
            case BallState.Thrown:
                // Physics handled in ThrowBall()
                break;
        }
    }

    // ================================
    // CHARACTER SYSTEM ACCESSORS
    // ================================

    /// <summary>
    /// Get current damage value (set by character data)
    /// </summary>
    public int GetCurrentDamage() => currentDamage;

    /// <summary>
    /// Get current throw type
    /// </summary>
    public ThrowType GetThrowType() => currentThrowType;

    /// <summary>
    /// Enable/disable homing behavior
    /// </summary>
    public void EnableHoming(bool enable)
    {
        homingEnabled = enable;
        if (debugMode)
        {
            Debug.Log($"Ball homing {(enable ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// Get current holder (new system)
    /// </summary>
    public PlayerCharacter GetHolder() => holder;

    /// <summary>
    /// Get current holder (legacy system)
    /// </summary>
    public CharacterController GetHolderLegacy() => legacyHolder;

    /// <summary>
    /// Get current thrower (new system)
    /// </summary>
    public PlayerCharacter GetThrower() => thrower;

    /// <summary>
    /// Get current thrower (legacy system)
    /// </summary>
    public CharacterController GetThrowerLegacy() => legacyThrower;

    // Legacy compatibility methods
    public BallState GetBallState() => currentState;
    public bool IsHeld() => currentState == BallState.Held;
    public bool IsFree() => currentState == BallState.Free;
    public Transform GetCurrentTarget() => targetOpponent;
    public Vector3 GetVelocity() => velocity;
    public void SetVelocity(Vector3 newVelocity) => velocity = newVelocity;
    public void SetBallStatePublic(BallState newState) => SetBallState(newState);

    // Debug visualization
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

        // Draw current damage info
        if (debugMode && currentState == BallState.Thrown)
        {
            Gizmos.color = Color.white;
            Vector3 infoPos = transform.position + Vector3.up * 1.5f;
            Gizmos.DrawWireCube(infoPos, Vector3.one * 0.3f);
        }

        // Draw hold position preview when ball is held
        if (currentState == BallState.Held)
        {
            Transform holderTransform = holder?.transform ?? legacyHolder?.transform;
            if (holderTransform != null)
            {
                Vector3 holdPos = CalculateHoldPosition(holderTransform);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(holdPos, 0.3f);
                Gizmos.DrawLine(holderTransform.position, holdPos);
            }
        }

        // Draw character info if held by a character
        if (holder != null && holder.GetCharacterData() != null)
        {
            CharacterData data = holder.GetCharacterData();
            Gizmos.color = data.characterColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.4f);
        }
    }
}