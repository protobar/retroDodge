using UnityEngine;
using System.Collections;

/// <summary>
/// Round Manager - Handles individual round logic and state
/// Works with MatchManager to control round flow
/// </summary>
public class RoundManager : MonoBehaviour
{
    [Header("Round Settings")]
    [SerializeField] private float roundDuration = 90f;
    [SerializeField] private float preFightDelay = 3f;
    [SerializeField] private float postRoundDelay = 2f;

    [Header("Ball Management")]
    [SerializeField] private float ballSpawnDelay = 1f;
    [SerializeField] private Vector3 ballSpawnPosition = new Vector3(0, 2f, 0);

    [Header("Network Settings (Future)")]
    [SerializeField] private bool enableNetworking = false;
    [SerializeField] private bool isHost = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Round state
    public enum RoundState { Inactive, PreFight, Active, Paused, Ended }
    private RoundState currentState = RoundState.Inactive;

    // Round data
    private int roundNumber = 0;
    private float currentRoundTime = 0f;
    private bool roundActive = false;
    private int roundWinner = 0; // 0 = no winner, 1 = player1, 2 = player2

    // Component references
    private MatchManager matchManager;
    private BallManager ballManager;

    // Events
    public System.Action<int> OnRoundStarted; // Round number
    public System.Action<int> OnRoundEnded; // Winner (0 = draw, 1 = player1, 2 = player2)
    public System.Action<float> OnRoundTimeChanged; // Current time remaining
    public System.Action OnRoundPaused;
    public System.Action OnRoundResumed;

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
        currentState = RoundState.Inactive;

        if (debugMode)
        {
            Debug.Log("RoundManager initialized");
        }
    }

    void Update()
    {
        if (currentState == RoundState.Active)
        {
            UpdateRoundTimer();
        }

        // Debug controls
        if (debugMode)
        {
            HandleDebugInput();
        }
    }

    void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (currentState == RoundState.Active)
            {
                PauseRound();
            }
            else if (currentState == RoundState.Paused)
            {
                ResumeRound();
            }
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            EndRound(1); // Force Player 1 win
        }

        if (Input.GetKeyDown(KeyCode.F11))
        {
            EndRound(2); // Force Player 2 win
        }

        if (Input.GetKeyDown(KeyCode.F12))
        {
            EndRound(0); // Force draw
        }
    }

    void UpdateRoundTimer()
    {
        if (!roundActive) return;

        currentRoundTime -= Time.deltaTime;

        // Trigger time change event
        OnRoundTimeChanged?.Invoke(currentRoundTime);

        // Check for time up
        if (currentRoundTime <= 0f)
        {
            currentRoundTime = 0f;
            TimeUpEndRound();
        }
    }

    /// <summary>
    /// Start a new round
    /// </summary>
    public void StartRound(int round)
    {
        if (currentState == RoundState.Active)
        {
            Debug.LogWarning("Round already active! Cannot start new round.");
            return;
        }

        roundNumber = round;
        currentRoundTime = roundDuration;
        roundWinner = 0;
        roundActive = false; // Will be set to true after pre-fight

        currentState = RoundState.PreFight;

        if (debugMode)
        {
            Debug.Log($"Starting Round {roundNumber}");
        }

        // Trigger round started event
        OnRoundStarted?.Invoke(roundNumber);

        // Start pre-fight sequence
        StartCoroutine(PreFightSequence());
    }

    IEnumerator PreFightSequence()
    {
        currentState = RoundState.PreFight;

        // Reset ball position
        ResetBall();

        // Wait for pre-fight delay
        yield return new WaitForSeconds(preFightDelay);

        // Spawn ball after delay
        if (ballManager != null)
        {
            yield return new WaitForSeconds(ballSpawnDelay);
            ballManager.ResetBall();
        }

        // Activate round
        ActivateRound();
    }

    void ActivateRound()
    {
        currentState = RoundState.Active;
        roundActive = true;

        if (debugMode)
        {
            Debug.Log($"Round {roundNumber} activated - FIGHT!");
        }
    }

    /// <summary>
    /// End the round with specified winner
    /// </summary>
    public void EndRound(int winner)
    {
        if (currentState != RoundState.Active && currentState != RoundState.Paused)
        {
            Debug.LogWarning("Cannot end round - round not active!");
            return;
        }

        roundActive = false;
        roundWinner = winner;
        currentState = RoundState.Ended;

        if (debugMode)
        {
            string winnerText = winner == 0 ? "Draw" : $"Player {winner}";
            Debug.Log($"Round {roundNumber} ended - Winner: {winnerText}");
        }

        // Trigger round ended event
        OnRoundEnded?.Invoke(winner);

        StartCoroutine(PostRoundSequence());
    }

    IEnumerator PostRoundSequence()
    {
        // Disable ball interactions
        if (ballManager != null)
        {
            ballManager.SetGameActive(false);
        }

        // Wait for post-round delay
        yield return new WaitForSeconds(postRoundDelay);

        // Re-enable ball for next round
        if (ballManager != null)
        {
            ballManager.SetGameActive(true);
        }

        // Return to inactive state
        currentState = RoundState.Inactive;
    }

    /// <summary>
    /// Handle round end by time up
    /// </summary>
    void TimeUpEndRound()
    {
        if (!roundActive) return;

        // Determine winner by health comparison
        int winner = DetermineWinnerByHealth();

        if (debugMode)
        {
            Debug.Log($"Round {roundNumber} time up! Winner by health: {(winner == 0 ? "Draw" : $"Player {winner}")}");
        }

        EndRound(winner);
    }

    /// <summary>
    /// Determine winner by comparing player health
    /// </summary>
    int DetermineWinnerByHealth()
    {
        // Find players and compare health
        PlayerHealth[] playerHealths = FindObjectsOfType<PlayerHealth>();

        if (playerHealths.Length < 2)
        {
            Debug.LogWarning("Not enough players found for health comparison!");
            return 0;
        }

        PlayerHealth player1Health = null;
        PlayerHealth player2Health = null;

        // Identify players by their GameObject names or components
        foreach (PlayerHealth health in playerHealths)
        {
            if (health.gameObject.name.Contains("Player1") ||
                health.GetComponent<PlayerInputHandler>()?.GetPlayerType() == PlayerInputHandler.PlayerInputType.Player1)
            {
                player1Health = health;
            }
            else if (health.gameObject.name.Contains("Player2") ||
                     health.GetComponent<PlayerInputHandler>()?.GetPlayerType() == PlayerInputHandler.PlayerInputType.Player2)
            {
                player2Health = health;
            }
        }

        if (player1Health == null || player2Health == null)
        {
            Debug.LogWarning("Could not identify both players for health comparison!");
            return 0;
        }

        float player1HealthPercent = player1Health.GetHealthPercentage();
        float player2HealthPercent = player2Health.GetHealthPercentage();

        if (player1HealthPercent > player2HealthPercent)
        {
            return 1; // Player 1 wins
        }
        else if (player2HealthPercent > player1HealthPercent)
        {
            return 2; // Player 2 wins
        }
        else
        {
            return 0; // Draw
        }
    }

    /// <summary>
    /// Pause the current round
    /// </summary>
    public void PauseRound()
    {
        if (currentState != RoundState.Active) return;

        currentState = RoundState.Paused;
        roundActive = false;

        OnRoundPaused?.Invoke();

        if (debugMode)
        {
            Debug.Log($"Round {roundNumber} paused");
        }
    }

    /// <summary>
    /// Resume the paused round
    /// </summary>
    public void ResumeRound()
    {
        if (currentState != RoundState.Paused) return;

        currentState = RoundState.Active;
        roundActive = true;

        OnRoundResumed?.Invoke();

        if (debugMode)
        {
            Debug.Log($"Round {roundNumber} resumed");
        }
    }

    /// <summary>
    /// Reset ball to starting position
    /// </summary>
    void ResetBall()
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
    /// Handle player knockout
    /// </summary>
    public void OnPlayerKnockout(int defeatedPlayer)
    {
        if (!roundActive) return;

        int winner = defeatedPlayer == 1 ? 2 : 1;

        if (debugMode)
        {
            Debug.Log($"Player {defeatedPlayer} knocked out! Player {winner} wins round.");
        }

        EndRound(winner);
    }

    #region Network Ready Methods (Future)

    /// <summary>
    /// Future: Handle networked round state sync
    /// </summary>
    public void SyncRoundState(RoundState state, float timeRemaining)
    {
        if (!enableNetworking) return;

        currentState = state;
        currentRoundTime = timeRemaining;
        roundActive = (state == RoundState.Active);

        if (debugMode)
        {
            Debug.Log($"Network: Round state synced - {state}, Time: {timeRemaining:F1}");
        }
    }

    /// <summary>
    /// Future: Send round state to network
    /// </summary>
    void SendRoundStateToNetwork()
    {
        if (!enableNetworking || !isHost) return;

        // Future: PUN2 RPC implementation
        // photonView.RPC("SyncRoundState", RpcTarget.Others, currentState, currentRoundTime);
    }

    /// <summary>
    /// Future: Handle network round end
    /// </summary>
    public void OnNetworkRoundEnd(int winner)
    {
        if (debugMode)
        {
            Debug.Log($"Network: Round ended with winner {winner}");
        }

        EndRound(winner);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get current round state
    /// </summary>
    public RoundState GetRoundState() => currentState;

    /// <summary>
    /// Get current round number
    /// </summary>
    public int GetRoundNumber() => roundNumber;

    /// <summary>
    /// Get remaining round time
    /// </summary>
    public float GetRemainingTime() => currentRoundTime;

    /// <summary>
    /// Check if round is currently active
    /// </summary>
    public bool IsRoundActive() => roundActive;

    /// <summary>
    /// Get round winner (only valid after round ends)
    /// </summary>
    public int GetRoundWinner() => roundWinner;

    /// <summary>
    /// Set round duration (useful for different game modes)
    /// </summary>
    public void SetRoundDuration(float duration)
    {
        roundDuration = duration;
        if (currentState == RoundState.Inactive)
        {
            currentRoundTime = duration;
        }
    }

    /// <summary>
    /// Add time to current round (power-up effect)
    /// </summary>
    public void AddTime(float additionalTime)
    {
        if (currentState == RoundState.Active)
        {
            currentRoundTime += additionalTime;

            if (debugMode)
            {
                Debug.Log($"Added {additionalTime}s to round. New time: {currentRoundTime:F1}s");
            }
        }
    }

    /// <summary>
    /// Force end round (admin/debug function)
    /// </summary>
    public void ForceEndRound(int winner = 0)
    {
        if (currentState == RoundState.Active || currentState == RoundState.Paused)
        {
            EndRound(winner);
        }
    }

    #endregion

    #region Debug

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(Screen.width - 320, 170, 310, 180));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== ROUND MANAGER DEBUG ===");
        GUILayout.Label($"State: {currentState}");
        GUILayout.Label($"Round: {roundNumber}");
        GUILayout.Label($"Time: {currentRoundTime:F1}s");
        GUILayout.Label($"Active: {roundActive}");
        GUILayout.Label($"Winner: {(roundWinner == 0 ? "None" : $"Player {roundWinner}")}");
        GUILayout.Label($"Ball Manager: {(ballManager != null ? "Found" : "Missing")}");

        GUILayout.Space(10);
        GUILayout.Label("Debug Controls:");
        GUILayout.Label("F9 - Pause/Resume");
        GUILayout.Label("F10 - P1 Wins");
        GUILayout.Label("F11 - P2 Wins");
        GUILayout.Label("F12 - Draw");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion
}