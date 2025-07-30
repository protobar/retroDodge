using UnityEngine;

public class BallController : MonoBehaviour
{
    [Header("Ball Settings")]
    [SerializeField] private float baseSpeed = 25f;
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float pickupRange = 1.2f;
    [SerializeField] private float bounceMultiplier = 0.6f;
    [SerializeField] private bool homingEnabled = true;
    [SerializeField] private float homingStrength = 1f;

    [Header("Visual Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color heldColor = Color.yellow;

    // Ball state
    public enum BallState { Free, Held, Thrown }
    [SerializeField] private BallState currentState = BallState.Free;

    // Physics
    private Vector3 velocity;
    private bool isGrounded = false;

    // References
    private Transform ballTransform;
    private Renderer ballRenderer;
    private CharacterController holder;

    // Throw properties
    private Vector3 throwDirection;
    private float throwPower;

    // Ground detection
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float groundCheckDistance = 0.6f;

    void Awake()
    {
        ballTransform = transform;
        ballRenderer = GetComponent<Renderer>();

        // Ensure ball has a collider for catch detection
        if (GetComponent<Collider>() == null)
        {
            SphereCollider ballCollider = gameObject.AddComponent<SphereCollider>();
            ballCollider.radius = 0.5f;
            ballCollider.isTrigger = false; // For physics collision
        }

        // Set initial state
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
        // Apply gravity when not grounded
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
                if (velocity.y < 2f) // Stop small bounces
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
            // Follow the holder
            Vector3 holdPosition = holder.transform.position + Vector3.up * 1.5f + holder.transform.right * 0.5f;
            ballTransform.position = Vector3.Lerp(ballTransform.position, holdPosition, 10f * Time.deltaTime);

            // Reset velocity
            velocity = Vector3.zero;
        }
        else
        {
            // Lost holder, become free
            SetBallState(BallState.Free);
        }
    }

    void HandleThrownBall()
    {
        // Apply homing behavior - ball tracks toward opponent like a missile
        if (homingEnabled && BallManager.Instance != null)
        {
            Vector3 targetPos = BallManager.Instance.GetCurrentOpponentPosition();
            Vector3 currentPos = ballTransform.position;

            // Calculate direct line to opponent
            Vector3 homingDirection = (targetPos - currentPos).normalized;

            // Adjust velocity to track toward opponent
            Vector3 currentDirection = velocity.normalized;
            Vector3 newDirection = Vector3.Lerp(currentDirection, homingDirection, homingStrength * Time.deltaTime);

            // Maintain speed but update direction
            float currentSpeed = velocity.magnitude;
            velocity = newDirection * currentSpeed;

            // Debug line showing homing direction
            Debug.DrawLine(currentPos, targetPos, Color.magenta, 0.1f);
        }
        else
        {
            // Apply gravity only if not homing
            velocity.y -= gravity * Time.deltaTime;
        }

        // Move the ball
        ballTransform.Translate(velocity * Time.deltaTime, Space.World);

        // Check for ground collision
        CheckGrounded();

        if (isGrounded && velocity.y <= 0)
        {
            // Ball hit ground, bounce or become free
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

    void CheckPickupRange()
    {
        if (currentState != BallState.Free) return;

        // Find nearby players
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(ballTransform.position, player.transform.position);
            if (distance <= pickupRange)
            {
                // Player can pick up ball
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
        // Raycast down to check for ground
        Vector3 rayStart = ballTransform.position;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayer);

        // Debug ray
        Debug.DrawRay(rayStart, Vector3.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
    }

    void UpdateVisuals()
    {
        // Rotate the ball for visual effect
        if (currentState == BallState.Thrown || velocity.magnitude > 0.1f)
        {
            ballTransform.Rotate(Vector3.right * rotationSpeed * Time.deltaTime, Space.Self);
        }

        // Update color based on state
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
        // Simple scale effect for pickup indication
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

            Debug.Log($"{character.name} picked up the ball!");
            return true;
        }

        return false;
    }

    public void OnCaught(CharacterController catcher)
    {
        // Ball was successfully caught
        holder = catcher;
        SetBallState(BallState.Held);
        catcher.SetHasBall(true);

        // Stop all movement
        velocity = Vector3.zero;

        Debug.Log($"{catcher.name} caught the ball!");
    }

    public void OnCatchFailed()
    {
        // Ball catch was attempted but failed
        // Ball continues with modified trajectory (bounce effect)

        // Reduce speed slightly
        velocity *= 0.8f;

        // Add some random deviation
        velocity.x += Random.Range(-2f, 2f);
        velocity.y += Random.Range(1f, 3f);

        Debug.Log("Ball catch failed - ball deflected!");
    }

    public void ThrowBall(Vector3 direction, float power)
    {
        if (currentState != BallState.Held) return;

        // Set throw properties
        throwDirection = direction.normalized;
        throwPower = power;

        // Calculate throw velocity
        velocity = throwDirection * baseSpeed * power;

        // Add slight upward arc for better visual
        velocity.y += 3f * power;

        // Release from holder
        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }

        SetBallState(BallState.Thrown);

        Debug.Log($"Ball thrown with power {power} in direction {direction}!");
    }

    public void ResetBall()
    {
        // Reset to center spawn position
        Vector3 spawnPosition = new Vector3(0, 2f, 0); // Slightly elevated
        ballTransform.position = spawnPosition;
        velocity = Vector3.zero;

        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }

        SetBallState(BallState.Free);

        Debug.Log("Ball reset to center!");
    }

    void SetBallState(BallState newState)
    {
        currentState = newState;

        // Handle state changes
        switch (newState)
        {
            case BallState.Free:
                // Ball is available for pickup
                break;
            case BallState.Held:
                // Ball is being carried
                velocity = Vector3.zero;
                break;
            case BallState.Thrown:
                // Ball is in flight
                break;
        }
    }

    // Getters
    public BallState GetBallState() => currentState;
    public bool IsHeld() => currentState == BallState.Held;
    public bool IsFree() => currentState == BallState.Free;
    public CharacterController GetHolder() => holder;

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        // Draw ground check
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);

        // Draw velocity vector when thrown
        if (currentState == BallState.Thrown)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, velocity.normalized * 2f);
        }
    }
}