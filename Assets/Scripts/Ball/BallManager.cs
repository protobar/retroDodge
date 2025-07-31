using UnityEngine;

public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; }

    [Header("Ball Settings")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2f, 0);
    [SerializeField] private float respawnDelay = 2f;

    [Header("Opponent Settings")]
    [SerializeField] private Transform dummyOpponent;
    [SerializeField] private Vector3 dummyOpponentPosition = new Vector3(8f, 0f, 0f);

    // Current ball reference
    private BallController currentBall;

    // Game state
    private bool gameActive = true;

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        SetupDummyOpponent();
        SpawnBall();
    }


    void SetupDummyOpponent()
    {
        if (dummyOpponent == null)
        {
            // Create a dummy opponent for targeting
            GameObject dummy = new GameObject("DummyOpponent");
            dummy.transform.position = dummyOpponentPosition;

            // Add a simple visual representation
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(dummy.transform);
            visual.transform.localPosition = Vector3.zero;

            // Make it look different from player
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.red;
            }

            dummyOpponent = dummy.transform;
        }
    }

    void SpawnBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("Ball prefab not assigned in BallManager!");
            return;
        }

        // Destroy existing ball if any
        if (currentBall != null)
        {
            Destroy(currentBall.gameObject);
        }

        // Spawn new ball
        GameObject ballObj = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        currentBall = ballObj.GetComponent<BallController>();

        if (currentBall == null)
        {
            Debug.LogError("Ball prefab doesn't have BallController component!");
        }

        Debug.Log("Ball spawned at center!");
    }

    public void RequestBallPickup(CharacterController character)
    {
        if (currentBall != null && gameActive)
        {
            bool success = currentBall.TryPickup(character);
            if (success)
            {
                Debug.Log($"{character.name} successfully picked up the ball!");
            }
        }
    }

    public void RequestBallThrow(CharacterController thrower, Vector3 targetDirection, float power)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            currentBall.ThrowBall(targetDirection, power);

            // Schedule respawn if ball goes out of bounds
            StartCoroutine(CheckForRespawn());
        }
    }

    public Vector3 GetOpponentPosition(CharacterController player)
    {
        // For now, return dummy opponent position
        // Later this will be actual opponent position in multiplayer
        if (dummyOpponent != null)
        {
            return dummyOpponent.position;
        }

        // Fallback: if player is on left, opponent is on right and vice versa
        Vector3 playerPos = player.transform.position;
        Vector3 opponentPos = playerPos.x < 0 ? new Vector3(8f, 0f, 0f) : new Vector3(-8f, 0f, 0f);

        return opponentPos;
    }

    public Vector3 GetThrowDirection(CharacterController thrower)
    {
        Vector3 throwerPos = thrower.transform.position;
        Vector3 targetPos = GetOpponentPosition(thrower);

        // Calculate direction with slight upward angle
        Vector3 direction = (targetPos - throwerPos).normalized;
        direction.y = 0.2f; // Add slight arc

        return direction.normalized;
    }

    public void ResetBall()
    {
        if (currentBall != null)
        {
            currentBall.ResetBall();
        }
        else
        {
            SpawnBall();
        }
    }

    System.Collections.IEnumerator CheckForRespawn()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Check if ball is out of bounds or needs respawn
        if (currentBall != null)
        {
            Vector3 ballPos = currentBall.transform.position;

            // If ball fell too low or went too far out
            if (ballPos.y < -3f || Mathf.Abs(ballPos.x) > 20f)
            {
                SpawnBall();
            }
        }
    }

    // Public getters
    public BallController GetCurrentBall() => currentBall;
    public bool HasActiveBall() => currentBall != null;
    public Vector3 GetBallSpawnPosition() => spawnPosition;

    public Vector3 GetCurrentOpponentPosition()
    {
        // Return current opponent position for homing
        if (dummyOpponent != null)
        {
            return dummyOpponent.position;
        }

        // Fallback position
        return dummyOpponentPosition;
    }

    // Game state control
    public void SetGameActive(bool active)
    {
        gameActive = active;
    }

    // Debug methods
    void Update()
    {
        SetupDummyOpponent();

        // Debug key to reset ball
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetBall();
        }

        // Debug key to respawn ball
        if (Input.GetKeyDown(KeyCode.T))
        {
            SpawnBall();
        }

        // Debug key to throw ball at player from dummy position
        if (Input.GetKeyDown(KeyCode.Y))
        {
            ThrowBallAtPlayer();
        }
    }

    void ThrowBallAtPlayer()
    {
        if (currentBall == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // Position ball at dummy opponent
        if (dummyOpponent != null)
        {
            currentBall.transform.position = dummyOpponent.position + Vector3.up * 1.5f;
        }

        // Calculate direction to player
        Vector3 direction = (player.transform.position - currentBall.transform.position).normalized;
        direction.y = 0.3f; // Add upward arc

        // Throw ball at player
        currentBall.ThrowBall(direction.normalized, 1.5f);

        Debug.Log("Debug: Ball thrown at player from dummy position!");
    }

    void OnDrawGizmosSelected()
    {
        // Draw spawn position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPosition, 0.5f);

        // Draw dummy opponent position
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(dummyOpponentPosition, Vector3.one);

        // Draw line between spawn and opponent
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(spawnPosition, dummyOpponentPosition);
    }
}