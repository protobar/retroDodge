using UnityEngine;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// PUN2 Multiplayer Round Manager - Simplified utility class for ball and environment management
/// Works with MatchManager without competing timer logic
/// </summary>
public class RoundManager : MonoBehaviourPunCallbacks
{
    [Header("Ball Management")]
    [SerializeField] private float ballSpawnDelay = 1f;
    [SerializeField] private Vector3 ballSpawnPosition = new Vector3(0, 2f, 0);

    [Header("Environment")]
    [SerializeField] private GameObject[] environmentObjects;
    [SerializeField] private bool resetEnvironmentEachRound = true;

    // Component references
    private MatchManager matchManager;
    private BallManager ballManager;

    // Events for external systems
    public System.Action OnRoundReset;
    public System.Action<int> OnPlayerKnockout;

    void Awake()
    {
        // Get component references
        matchManager = GetComponent<MatchManager>();
        if (matchManager == null)
        {
            matchManager = FindObjectOfType<MatchManager>();
        }

        ballManager = FindObjectOfType<BallManager>();
        if (ballManager == null)
        {
            Debug.LogWarning("RoundManager: BallManager not found in scene!");
        }
    }

    void Start()
    {
        // Subscribe to match manager events if available
        if (matchManager != null)
        {
            // This would require adding events to MatchManager
            // matchManager.OnRoundStarted += HandleRoundStarted;
        }
    }

    /// <summary>
    /// Reset ball to starting position (called by MatchManager)
    /// </summary>
    public void ResetBall()
    {
        if (ballManager != null)
        {
            ballManager.ResetBall();
        }
        else
        {
            // Fallback: find ball directly
            BallController ball = FindObjectOfType<BallController>();
            if (ball != null)
            {
                ball.ResetBall();
            }
        }
    }

    /// <summary>
    /// Reset environment objects for new round
    /// </summary>
    public void ResetEnvironment()
    {
        if (!resetEnvironmentEachRound) return;

        foreach (GameObject obj in environmentObjects)
        {
            if (obj != null)
            {
                // Reset position, rotation, scale or whatever is needed
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;

                // If object has a reset method, call it
                var resettable = obj.GetComponent<IResettable>();
                resettable?.ResetState();
            }
        }
    }

    /// <summary>
    /// Handle player knockout - notifies MatchManager
    /// </summary>
    public void HandlePlayerKnockout(int defeatedPlayerActorNumber)
    {
        OnPlayerKnockout?.Invoke(defeatedPlayerActorNumber);

        // Let MatchManager handle the actual round ending logic
        if (matchManager != null)
        {
            // MatchManager will handle this through its health system callbacks
        }
    }

    /// <summary>
    /// Enable/disable ball physics and interactions
    /// </summary>
    public void SetBallActive(bool active)
    {
        if (ballManager != null)
        {
            ballManager.SetGameActive(active);
        }
    }

    /// <summary>
    /// Spawn ball with delay (useful for round start sequence)
    /// </summary>
    public IEnumerator SpawnBallWithDelay()
    {
        yield return new WaitForSeconds(ballSpawnDelay);
        ResetBall();
    }

    /// <summary>
    /// Full round reset - combines ball and environment reset
    /// </summary>
    public void ResetForNewRound()
    {
        ResetBall();
        ResetEnvironment();
        OnRoundReset?.Invoke();
    }

    void OnDestroy()
    {
        // Cleanup event subscriptions
        if (matchManager != null)
        {
            // Unsubscribe from events if they were added
        }
    }
}

/// <summary>
/// Interface for objects that can be reset between rounds
/// </summary>
public interface IResettable
{
    void ResetState();
}