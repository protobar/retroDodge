using UnityEngine;

public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; }

    [Header("Ball Settings")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2f, 0);
    [SerializeField] private float respawnDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

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
        SpawnBall();
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
            if (success && debugMode)
            {
                Debug.Log($"{character.name} successfully picked up the ball!");
            }
        }
    }

    // SIMPLIFIED: Direct throw method - no targeting system needed
    public void RequestBallThrow(CharacterController thrower, Vector3 direction, float power)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            // Ball controller handles its own targeting now
            currentBall.ThrowBall(direction, power);

            // Schedule respawn if ball goes out of bounds
            StartCoroutine(CheckForRespawn());
        }
    }

    // NEW: Simplified throw method that lets BallController handle everything
    public void RequestBallThrowSimple(CharacterController thrower, float power = 1f)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            // BallController determines direction internally
            Vector3 anyDirection = Vector3.right; // Will be overridden by BallController
            currentBall.ThrowBall(anyDirection, power);

            // Schedule respawn if ball goes out of bounds
            StartCoroutine(CheckForRespawn());
        }
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

    // Game state control
    public void SetGameActive(bool active)
    {
        gameActive = active;
    }

    // Debug methods
    void Update()
    {
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
            ThrowBallAtPlayerFromDummy();
        }
    }

    void ThrowBallAtPlayerFromDummy()
    {
        if (currentBall == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // Find dummy opponent
        GameObject dummy = GameObject.Find("DummyOpponent");
        if (dummy == null) return;

        // Position ball at dummy
        currentBall.transform.position = dummy.transform.position + Vector3.up * 1.5f;

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
    }
}