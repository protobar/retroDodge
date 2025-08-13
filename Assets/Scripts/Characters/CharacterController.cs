using UnityEngine;

public class CharacterController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer = 1;

    [Header("Character State")]
    [SerializeField] private bool isGrounded = false;
    [SerializeField] private bool isDucking = false;
    [SerializeField] private bool hasBall = false;

    [Header("Ball Interaction")]
    [SerializeField] private float throwPower = 1f;

    // Components
    private Transform characterTransform;
    private CapsuleCollider characterCollider;
    private CatchSystem catchSystem;
    private PlayerInputHandler inputHandler;

    // Movement variables
    private Vector3 velocity;
    private float horizontalInput;
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;
    private bool duckingStateChanged = false;

    // Ground check
    private Vector3 groundCheckPosition;

    void Awake()
    {
        // Cache components
        characterTransform = transform;
        characterCollider = GetComponent<CapsuleCollider>();
        catchSystem = GetComponent<CatchSystem>();
        inputHandler = GetComponent<PlayerInputHandler>();

        // Validate input handler
        if (inputHandler == null)
        {
            Debug.LogError($"{gameObject.name} - PlayerInputHandler component is missing! Please add it.");
        }

        // Store original collider dimensions for ducking
        if (characterCollider != null)
        {
            originalColliderHeight = characterCollider.height;
            originalColliderCenter = characterCollider.center;
        }
    }

    void Update()
    {
        HandleInput();
        CheckGrounded();
        HandleMovement();
        HandleDucking();
        HandleBallInteraction();
    }

    void HandleInput()
    {
        if (inputHandler == null) return;

        // Get horizontal movement input from input handler
        horizontalInput = inputHandler.GetHorizontal();

        // Debug horizontal input
        if (horizontalInput != 0)
        {
            Debug.Log($"{gameObject.name} - Horizontal Input: {horizontalInput}");
        }

        // Jump input (only when grounded and not ducking)
        if (inputHandler.GetJumpPressed() && isGrounded && !isDucking)
        {
            Jump();
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
        // Only allow horizontal movement when not ducking
        if (!isDucking)
        {
            // Horizontal movement (instant response)
            Vector3 moveDirection = Vector3.right * horizontalInput * moveSpeed;
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
                velocity.y = 0f;
        }

        // Apply vertical movement
        characterTransform.Translate(Vector3.up * velocity.y * Time.deltaTime, Space.World);
    }

    void Jump()
    {
        velocity.y = jumpForce;
        isGrounded = false;
        Debug.Log($"{gameObject.name} Jumped!");
    }

    void HandleDucking()
    {
        if (characterCollider == null || !duckingStateChanged) return;

        if (isDucking)
        {
            // Duck down - reduce collider height and move center down (visual crouch)
            characterCollider.height = originalColliderHeight * 0.5f;
            characterCollider.center = new Vector3(
                originalColliderCenter.x,
                originalColliderCenter.y - (originalColliderHeight * 0.25f),
                originalColliderCenter.z
            );
            Debug.Log($"{gameObject.name} Ducked!");
        }
        else
        {
            // Stand up - restore original collider dimensions
            characterCollider.height = originalColliderHeight;
            characterCollider.center = originalColliderCenter;
            Debug.Log($"{gameObject.name} Stood Up!");
        }

        // Reset the state change flag
        duckingStateChanged = false;
    }

    void HandleBallInteraction()
    {
        if (BallManager.Instance == null || inputHandler == null) return;

        // Pickup ball
        if (inputHandler.GetPickupPressed() && !hasBall)
        {
            BallManager.Instance.RequestBallPickup(this);
        }

        // SIMPLIFIED: Direct throw with no charging system
        if (inputHandler.GetThrowPressed() && hasBall)
        {
            ThrowBall();
        }
    }

    void ThrowBall()
    {
        if (!hasBall || BallManager.Instance == null) return;

        // Execute simple throw - BallController handles targeting
        BallManager.Instance.RequestBallThrowSimple(this, throwPower);

        Debug.Log($"{gameObject.name} executed throw!");
    }

    void CheckGrounded()
    {
        // Ground check from bottom of original collider (not modified collider)
        groundCheckPosition = characterTransform.position;
        groundCheckPosition.y -= (originalColliderHeight * 0.5f);

        // Raycast downward to check for ground
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

    // Public methods for other systems
    public bool IsGrounded() => isGrounded;
    public bool IsDucking() => isDucking;
    public bool HasBall() => hasBall;
    public void SetHasBall(bool value) => hasBall = value;
    public PlayerInputHandler GetInputHandler() => inputHandler;

    // Get current facing direction (for ball throwing)
    public Vector3 GetFacingDirection()
    {
        // For now, assume facing right is positive X
        return Vector3.right;
    }

    void OnDrawGizmosSelected()
    {
        // Visualize ground check in editor
        if (characterCollider != null)
        {
            // Always use original collider height for ground check visualization
            Vector3 checkPos = transform.position;
            checkPos.y -= (originalColliderHeight * 0.5f);

            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawRay(checkPos, Vector3.down * groundCheckDistance);

            // Show the ground check point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(checkPos, 0.1f);
        }
    }
}