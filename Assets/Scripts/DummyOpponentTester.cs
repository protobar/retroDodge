using UnityEngine;

public class DummyOpponentTester : MonoBehaviour
{
    [Header("Auto Throw Settings")]
    [SerializeField] private bool enableAutoThrow = true;
    [SerializeField] private float throwInterval = 3f;
    [SerializeField] private float throwPower = 1.5f;

    [Header("Manual Control")]
    [SerializeField] private KeyCode manualThrowKey = KeyCode.T;

    private float lastThrowTime = 0f;
    private CharacterController player;

    void Start()
    {
        // Find the player
        player = FindObjectOfType<CharacterController>();

        // Add components to dummy if missing
        SetupDummyComponents();

        Debug.Log("Dummy Opponent Tester Ready! Press T to make dummy throw at you.");
    }

    void SetupDummyComponents()
    {
        // Add CharacterController component to dummy for ball interaction
        if (GetComponent<CharacterController>() == null)
        {
            // Add a simple version for testing
            gameObject.AddComponent<DummyCharacterController>();
        }
    }

    void Update()
    {
        // Manual throw
        if (Input.GetKeyDown(manualThrowKey))
        {
            TryThrowAtPlayer();
        }

        // Auto throw
        if (enableAutoThrow && Time.time - lastThrowTime >= throwInterval)
        {
            TryThrowAtPlayer();
        }
    }

    void TryThrowAtPlayer()
    {
        if (BallManager.Instance == null || player == null) return;

        BallController ball = BallManager.Instance.GetCurrentBall();
        if (ball == null || ball.IsHeld()) return;

        // Spawn ball near dummy if no ball exists
        if (!BallManager.Instance.HasActiveBall())
        {
            BallManager.Instance.ResetBall();
            return;
        }

        // Move ball to dummy position and throw at player
        if (ball.IsFree())
        {
            // Position ball near dummy
            ball.transform.position = transform.position + Vector3.up * 1.5f;

            // Calculate direction to player
            Vector3 direction = (player.transform.position - transform.position).normalized;
            direction.y = 0.2f; // Add arc

            // Throw the ball
            ball.ThrowBall(direction.normalized, throwPower);

            lastThrowTime = Time.time;

            Debug.Log("Dummy threw ball at player!");
        }
    }
}

// Simple dummy character controller for testing
public class DummyCharacterController : MonoBehaviour
{
    private bool hasBall = false;

    public bool HasBall() => hasBall;
    public void SetHasBall(bool value) => hasBall = value;

    // Dummy methods to satisfy ball system requirements
    public Vector3 GetFacingDirection() => Vector3.left; // Face toward player
}