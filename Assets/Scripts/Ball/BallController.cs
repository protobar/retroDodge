using UnityEngine;

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
    [Header("Collision Settings")]
    [SerializeField] private float collisionDistance = 1.0f; // Ball radius + Player radius
    [SerializeField] private float duckingHeightThreshold = 1.0f; // Ball must be below this to hit ducking player
    [SerializeField] private float playerCollisionHeight = 1.0f; // Height offset for player collision center
    [SerializeField] private bool enablePredictiveCollision = true;
    [SerializeField] private float predictionDistance = 1.5f;

    [Header("Visual Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color heldColor = Color.yellow;

    [Header("Ball Hold Position")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0.5f, 1.5f, 0f); // Right, Up, Forward
    [SerializeField] private bool useRelativeToPlayer = true; // Use player's right/forward or world space
    [SerializeField] private float holdFollowSpeed = 10f;
    [SerializeField] private bool smoothHoldMovement = true;

    [Header("Debug Visualization")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showCollisionGizmos = true;
    [SerializeField] private bool showPlayerColliders = true;
    [SerializeField] private bool showDuckingThreshold = true;
    [SerializeField] private bool showBallHeight = true;
    [SerializeField] private bool showCollisionLines = true;

    // Ball state
    public enum BallState { Free, Held, Thrown }
    public enum ThrowType { Normal, JumpThrow, PowerShot }

    [SerializeField] private BallState currentState = BallState.Free;
    private ThrowType currentThrowType = ThrowType.Normal;

    // Physics
    public Vector3 velocity;
    private bool isGrounded = false;
    private bool hasHitTarget = false;

    // References
    private Transform ballTransform;
    private Renderer ballRenderer;
    private CharacterController holder;
    private CollisionDamageSystem collisionSystem;

    // FIXED: Simple targeting without BallTargetManager
    private CharacterController thrower;
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
        // FIXED: Apply gravity when ball is FREE
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
        if (holder != null)
        {
            Vector3 holdPosition = CalculateHoldPosition();

            if (smoothHoldMovement)
            {
                // Smooth following with lerp
                ballTransform.position = Vector3.Lerp(ballTransform.position, holdPosition, holdFollowSpeed * Time.deltaTime);
            }
            else
            {
                // Instant positioning
                ballTransform.position = holdPosition;
            }

            velocity = Vector3.zero;
        }
        else
        {
            SetBallState(BallState.Free);
        }
    }

    /// <summary>
    /// Calculate ball position when held by player based on inspector settings
    /// </summary>
    Vector3 CalculateHoldPosition()
    {
        Vector3 basePosition = holder.transform.position;

        if (useRelativeToPlayer)
        {
            // Position relative to player's orientation
            Vector3 holdPosition = basePosition;
            holdPosition += holder.transform.right * holdOffset.x;    // Right/Left offset
            holdPosition += Vector3.up * holdOffset.y;                // Up/Down offset  
            holdPosition += holder.transform.forward * holdOffset.z;  // Forward/Back offset

            return holdPosition;
        }
        else
        {
            // Position in world space
            return basePosition + holdOffset;
        }
    }

    void HandleThrownBall()
    {
        // FIXED: NO GRAVITY during thrown state - ball maintains exact trajectory
        // This gives us authentic Neo Geo physics:
        // - Normal throws: perfectly horizontal
        // - Jump throws: sharp diagonal lines

        if (useNeoGeoPhysics)
        {
            HandleNeoGeoPhysics();
        }
        else
        {
            HandleLegacyPhysics();
        }

        // Move the ball (no gravity applied here)
        ballTransform.Translate(velocity * Time.deltaTime, Space.World);

        // REMOVED: CheckForCollision() - CollisionDamageSystem handles this now
        // REMOVED: CheckWallCollisions() - CollisionDamageSystem handles this now

        // Check for ground collision only
        CheckGrounded();
        if (isGrounded && velocity.y <= 0)
        {
            // When ball hits ground, apply bounce and transition to FREE state
            velocity.y = -velocity.y * bounceMultiplier;
            if (velocity.magnitude < 5f)
            {
                SetBallState(BallState.Free); // Gravity resumes in FREE state
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
        // FIXED: No gravity during thrown state - gravity handled by ball state
        // This allows for perfectly horizontal normal throws and sharp diagonal jump throws

        if (debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Neo Geo Physics: {currentThrowType}, Speed: {velocity.magnitude:F1}, Velocity: {velocity}");
        }
    }

    void HandleLegacyPhysics()
    {
        velocity.y -= gravity * Time.deltaTime;
    }

    void CheckForCollision()
    {
        if (hasHitTarget) return;

        // Find all players and check for collision
        CharacterController[] allPlayers = FindObjectsOfType<CharacterController>();

        foreach (CharacterController player in allPlayers)
        {
            if (player == null || player == thrower) continue;

            // FIXED: Proper collision with ducking pass-through
            bool shouldCollide = CheckPlayerCollision(player);

            if (shouldCollide)
            {
                if (debugMode)
                {
                    Debug.Log($"COLLISION: Ball hit {player.name} (Ducking: {player.IsDucking()})");
                }
                OnPlayerHit(player);
                return;
            }
        }

        // FIXED: Check wall collisions separately
        CheckWallCollisions();
    }

    /// <summary>
    /// Check if ball should collide with player - handles ducking pass-through
    /// </summary>
    bool CheckPlayerCollision(CharacterController player)
    {
        Vector3 playerCenter = player.transform.position + Vector3.up * playerCollisionHeight;
        float distanceToPlayer = Vector3.Distance(ballTransform.position, playerCenter);

        // First check: Is ball close enough to player?
        if (distanceToPlayer > collisionDistance)
            return false;

        // Second check: If player is ducking, ball must be low enough to hit
        if (player.IsDucking())
        {
            if (ballTransform.position.y > duckingHeightThreshold)
            {
                // Ball is too high - passes over ducking player
                if (debugMode)
                {
                    Debug.Log($"Ball passed over ducking {player.name} (Ball Y: {ballTransform.position.y:F2}, Threshold: {duckingHeightThreshold})");
                }
                return false;
            }
        }

        // Ball should hit the player
        return true;
    }

    /// <summary>
    /// Check collisions with walls and environment
    /// </summary>
    void CheckWallCollisions()
    {
        // Cast a sphere to detect wall collisions
        RaycastHit hit;
        float ballRadius = 0.5f; // Ball's sphere collider radius

        if (Physics.SphereCast(ballTransform.position, ballRadius, velocity.normalized, out hit,
            velocity.magnitude * Time.deltaTime, groundLayer))
        {
            // Hit a wall - bounce off
            if (hit.collider.CompareTag("Wall") || hit.collider.name.Contains("Wall"))
            {
                OnWallHit(hit);
            }
        }
    }

    /// <summary>
    /// Handle ball hitting walls
    /// </summary>
    void OnWallHit(RaycastHit hit)
    {
        // Reflect velocity off the wall
        Vector3 reflection = Vector3.Reflect(velocity, hit.normal);
        velocity = reflection * 0.8f; // Reduce speed slightly

        // Move ball slightly away from wall to prevent sticking
        ballTransform.position = hit.point + hit.normal * 0.6f;

        // Ball becomes free after wall hit - gravity resumes
        SetBallState(BallState.Free);

        if (debugMode)
        {
            Debug.Log($"Ball hit wall: {hit.collider.name}, bounced with velocity: {velocity}");
        }
    }

    // CALLED BY CollisionDamageSystem when collision is detected
    void OnPlayerHit(CharacterController hitPlayer)
    {
        hasHitTarget = true;

        if (debugMode)
        {
            Debug.Log($"BallController: Player hit confirmed by CollisionDamageSystem - {hitPlayer.name}");
        }

        // Check for catch attempt (this logic can stay here as it's ball-specific)
        CatchSystem catchSystem = hitPlayer.GetComponent<CatchSystem>();
        bool caughtSuccessfully = false;

        if (catchSystem != null && catchSystem.IsBallInRange())
        {
            PlayerInputHandler inputHandler = hitPlayer.GetComponent<PlayerInputHandler>();
            if (inputHandler != null && inputHandler.GetCatchPressed())
            {
                // Successful catch
                OnCaught(hitPlayer);
                caughtSuccessfully = true;
                return;
            }
        }

        // If not caught, CollisionDamageSystem will handle damage and bounce
        // FIXED: Ball becomes FREE immediately (gravity resumes)
        SetBallState(BallState.Free);
    }

    // REMOVED: CreateNeoGeoBounce() - CollisionDamageSystem handles all post-impact physics

    void MakeBallFree()
    {
        if (currentState == BallState.Thrown)
        {
            SetBallState(BallState.Free);
        }
    }

    void CheckPickupRange()
    {
        if (currentState != BallState.Free) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(ballTransform.position, player.transform.position);
            if (distance <= pickupRange)
            {
                ShowPickupIndicator(true);
            }
            else
            {
                ShowPickupIndicator(false);
            }
        }
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
            switch (currentState)
            {
                case BallState.Free:
                    ballRenderer.material.color = availableColor;
                    break;
                case BallState.Held:
                    ballRenderer.material.color = heldColor;
                    break;
                case BallState.Thrown:
                    ballRenderer.material.color = Color.red;
                    break;
            }
        }
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
    // FIXED: SIMPLE TARGETING SYSTEM
    // ================================

    /// <summary>
    /// Find opponent for the given thrower using simple position-based logic
    /// </summary>
    private Transform FindOpponentForThrower(CharacterController throwerPlayer)
    {
        if (throwerPlayer == null) return null;

        // Find all character controllers
        CharacterController[] allPlayers = FindObjectsOfType<CharacterController>();

        foreach (CharacterController player in allPlayers)
        {
            // Skip self
            if (player == throwerPlayer) continue;

            // Skip if no health component (dummy objects)
            if (player.GetComponent<PlayerHealth>() == null) continue;

            // Return first valid opponent found
            return player.transform;
        }

        // Fallback: Look for dummy opponent
        GameObject dummy = GameObject.Find("DummyOpponent");
        if (dummy != null)
        {
            return dummy.transform;
        }

        if (debugMode)
        {
            Debug.LogWarning($"No opponent found for {throwerPlayer.name}!");
        }

        return null;
    }

    // Public methods
    public bool TryPickup(CharacterController character)
    {
        if (currentState != BallState.Free) return false;

        float distance = Vector3.Distance(ballTransform.position, character.transform.position);
        if (distance <= pickupRange)
        {
            holder = character;
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

    public void OnCaught(CharacterController catcher)
    {
        holder = catcher;
        SetBallState(BallState.Held);
        catcher.SetHasBall(true);
        velocity = Vector3.zero;
        thrower = null;
        targetOpponent = null;
        hasHitTarget = false;

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

    // FIXED: ThrowBall with simple targeting system
    public void ThrowBall(Vector3 direction, float power)
    {
        if (currentState != BallState.Held || holder == null) return;

        thrower = holder;
        hasHitTarget = false;

        // Detect if this is a jump throw
        CharacterController character = holder;
        isJumpThrow = !character.IsGrounded();

        if (collisionSystem != null)
        {
            collisionSystem.OnBallThrown(thrower);
        }

        if (debugMode)
        {
            Debug.Log($"=== NEO GEO THROW START ===");
            Debug.Log($"Thrower: {thrower.name}");
            Debug.Log($"Jump Throw: {isJumpThrow}");
        }

        // FIXED: Find opponent using simple system
        targetOpponent = FindOpponentForThrower(thrower);

        Vector3 throwDirection;
        float throwSpeed;

        if (targetOpponent != null)
        {
            Vector3 throwPos = ballTransform.position;
            Vector3 targetPos = targetOpponent.position;

            if (isJumpThrow)
            {
                // FIXED: Jump throws create sharp diagonal trajectory
                // Calculate direct diagonal line to target
                Vector3 directDirection = (targetPos - throwPos).normalized;
                throwDirection = directDirection; // Direct diagonal - no arc!
                throwSpeed = jumpThrowSpeed;
                currentThrowType = ThrowType.JumpThrow;

                if (debugMode)
                {
                    Debug.Log($"JUMP THROW: Direct diagonal to target, no arc");
                }
            }
            else
            {
                // FIXED: Normal throws go perfectly horizontal (no Y component)
                Vector3 horizontalDirection = new Vector3(
                    targetPos.x - throwPos.x,
                    0f, // NO upward component for normal throws
                    targetPos.z - throwPos.z
                );

                throwDirection = horizontalDirection.normalized;
                throwSpeed = normalThrowSpeed;
                currentThrowType = ThrowType.Normal;

                if (debugMode)
                {
                    Debug.Log($"NORMAL THROW: Perfectly horizontal, no arc");
                }
            }

            // DON'T normalize again - we want the exact direction calculated above

            if (debugMode)
            {
                Debug.Log($"Target: {targetOpponent.name} at {targetPos}");
                Debug.Log($"Throw Direction: {throwDirection}");
                Debug.Log($"Throw Type: {currentThrowType}");
            }
        }
        else
        {
            // Fallback to forward direction
            Debug.LogWarning($"No target found for thrower: {thrower.name}! Using forward direction.");

            if (isJumpThrow)
            {
                // Jump throw fallback: 45-degree diagonal
                throwDirection = new Vector3(1f, -0.5f, 0f).normalized; // Diagonal down-forward
                throwSpeed = jumpThrowSpeed;
            }
            else
            {
                // Normal throw fallback: perfectly horizontal
                throwDirection = Vector3.right; // Perfectly horizontal
                throwSpeed = normalThrowSpeed;
            }
            currentThrowType = isJumpThrow ? ThrowType.JumpThrow : ThrowType.Normal;
        }

        // Set velocity
        velocity = throwDirection * throwSpeed * power;

        // Release from holder
        holder.SetHasBall(false);
        holder = null;
        SetBallState(BallState.Thrown);

        if (debugMode)
        {
            Debug.Log($"NEO GEO: Ball thrown with velocity {velocity} (speed: {velocity.magnitude:F1})");
            Debug.Log($"=== NEO GEO THROW END ===");
        }
    }

    public void ResetBall()
    {
        Vector3 spawnPosition = new Vector3(0, 2f, 0);
        ballTransform.position = spawnPosition;
        velocity = Vector3.zero;
        hasHitTarget = false;

        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }

        thrower = null;
        targetOpponent = null;
        isJumpThrow = false;

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
                targetOpponent = null;
                hasHitTarget = false;
                isJumpThrow = false;
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

    // Getters
    public BallState GetBallState() => currentState;
    public bool IsHeld() => currentState == BallState.Held;
    public bool IsFree() => currentState == BallState.Free;
    public CharacterController GetHolder() => holder;
    public Transform GetCurrentTarget() => targetOpponent;
    public CharacterController GetThrower() => thrower;
    public Vector3 GetVelocity() => velocity;
    public void SetVelocity(Vector3 newVelocity) => velocity = newVelocity;
    public ThrowType GetThrowType() => currentThrowType;
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

        // Draw collision distance (for reference - CollisionDamageSystem does actual collision)
        if (debugMode)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Semi-transparent gray
            Gizmos.DrawWireSphere(transform.position, collisionDistance);
        }

        // Draw target opponent
        if (targetOpponent != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, targetOpponent.position);
            Gizmos.DrawWireSphere(targetOpponent.position, 0.5f);
        }
        {
            Vector3 holdPos = CalculateHoldPosition();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(holdPos, 0.3f);
            Gizmos.DrawLine(holder.transform.position, holdPos);

            // Draw coordinate axes for hold position
            Gizmos.color = Color.red;
            Gizmos.DrawRay(holdPos, Vector3.right * 0.5f); // X axis
            Gizmos.color = Color.green;
            Gizmos.DrawRay(holdPos, Vector3.up * 0.5f);    // Y axis
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(holdPos, Vector3.forward * 0.5f); // Z axis
        }

        // NEW: Draw hold position preview even when not held (for setup)
        if (currentState != BallState.Held && debugMode)
        {
            // Find any character controller to preview hold position
            CharacterController previewHolder = FindObjectOfType<CharacterController>();
            if (previewHolder != null)
            {
                Vector3 previewPos = previewHolder.transform.position;

                if (useRelativeToPlayer)
                {
                    previewPos += previewHolder.transform.right * holdOffset.x;
                    previewPos += Vector3.up * holdOffset.y;
                    previewPos += previewHolder.transform.forward * holdOffset.z;
                }
                else
                {
                    previewPos += holdOffset;
                }

                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(previewPos, 0.2f);
                Gizmos.DrawLine(previewHolder.transform.position, previewPos);
            }
        }
    }
}