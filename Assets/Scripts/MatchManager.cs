using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Match Manager - Handles Best of 3 Match System
/// Network Ready with Local Testing Support
/// </summary>
public class MatchManager : MonoBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private int roundsToWin = 2;
    [SerializeField] private float roundDuration = 90f;
    [SerializeField] private float preFightCountdown = 3f;
    [SerializeField] private float postRoundDelay = 3f;
    [SerializeField] private float matchEndDelay = 5f;

    [Header("Player Spawn Points")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Scene References")]
    [SerializeField] private string characterSelectionScene = "CharacterSelection";
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("UI References")]
    [SerializeField] private MatchUI matchUI;

    [Header("Network Settings (Future)")]
    [SerializeField] private bool enableNetworking = false;
    [SerializeField] private bool isHost = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip roundStartSound;
    [SerializeField] private AudioClip roundEndSound;
    [SerializeField] private AudioClip matchWinSound;
    [SerializeField] private AudioClip countdownSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Match state
    public enum MatchState { Initializing, PreFight, Fighting, RoundEnd, MatchEnd }
    private MatchState currentState = MatchState.Initializing;

    // Match data
    private CharacterSelectionData selectionData;
    private int currentRound = 0;
    private int player1RoundsWon = 0;
    private int player2RoundsWon = 0;
    private float currentRoundTime = 0f;
    private int matchWinner = 0; // 0 = none, 1 = player1, 2 = player2

    // Player references
    private GameObject player1GameObject;
    private GameObject player2GameObject;
    private PlayerCharacter player1Character;
    private PlayerCharacter player2Character;
    private PlayerHealth player1Health;
    private PlayerHealth player2Health;

    // Round management
    private RoundManager roundManager;
    private bool roundActive = false;
    private Coroutine roundTimerCoroutine;

    void Awake()
    {
        // Get selection data from character selection
        selectionData = CharacterSelectionManager.GetSelectionData();

        if (!selectionData.IsValid())
        {
            Debug.LogError("Invalid character selection data! Returning to character selection.");
            SceneManager.LoadScene(characterSelectionScene);
            return;
        }

        // Setup audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        // Find or create UI
        if (matchUI == null)
        {
            matchUI = FindObjectOfType<MatchUI>();
        }

        // Find or create round manager
        roundManager = GetComponent<RoundManager>();
        if (roundManager == null)
        {
            roundManager = gameObject.AddComponent<RoundManager>();
        }
    }

    void Start()
    {
        StartCoroutine(InitializeMatch());
    }

    IEnumerator InitializeMatch()
    {
        currentState = MatchState.Initializing;

        if (debugMode)
        {
            Debug.Log($"Initializing match: {selectionData.player1Character.characterName} vs {selectionData.player2Character.characterName}");
        }

        // Spawn players
        yield return StartCoroutine(SpawnPlayers());

        // Initialize UI
        InitializeMatchUI();

        // Wait a moment for everything to settle
        yield return new WaitForSeconds(1f);

        // Start first round
        StartRound(1);
    }

    IEnumerator SpawnPlayers()
    {
        // Destroy existing players
        if (player1GameObject != null)
            Destroy(player1GameObject);
        if (player2GameObject != null)
            Destroy(player2GameObject);

        // Spawn Player 1
        if (selectionData.player1Character.characterPrefab != null)
        {
            Vector3 spawnPos = player1SpawnPoint != null ? player1SpawnPoint.position : new Vector3(-5f, 0f, 0f);
            player1GameObject = Instantiate(selectionData.player1Character.characterPrefab, spawnPos, Quaternion.identity);
            player1GameObject.name = "Player1";

            // Setup Player 1 components
            SetupPlayer(player1GameObject, selectionData.player1Character, PlayerInputHandler.PlayerInputType.Player1);

            player1Character = player1GameObject.GetComponent<PlayerCharacter>();
            player1Health = player1GameObject.GetComponent<PlayerHealth>();
        }

        yield return new WaitForEndOfFrame();

        // Spawn Player 2
        if (selectionData.player2Character.characterPrefab != null)
        {
            Vector3 spawnPos = player2SpawnPoint != null ? player2SpawnPoint.position : new Vector3(5f, 0f, 0f);
            player2GameObject = Instantiate(selectionData.player2Character.characterPrefab, spawnPos, Quaternion.identity);
            player2GameObject.name = "Player2";

            // Setup Player 2 components
            SetupPlayer(player2GameObject, selectionData.player2Character, PlayerInputHandler.PlayerInputType.Player2);

            player2Character = player2GameObject.GetComponent<PlayerCharacter>();
            player2Health = player2GameObject.GetComponent<PlayerHealth>();
        }

        yield return new WaitForEndOfFrame();

        // Validate spawns
        if (player1Character == null || player2Character == null)
        {
            Debug.LogError("Failed to spawn players properly!");
            yield break;
        }

        // Load character data
        if (player1Character != null)
            player1Character.LoadCharacter(selectionData.player1Character);

        if (player2Character != null)
            player2Character.LoadCharacter(selectionData.player2Character);

        // Setup health event listeners
        SetupHealthEventListeners();

        if (debugMode)
        {
            Debug.Log("Players spawned successfully");
        }
    }

    void SetupPlayer(GameObject playerObj, CharacterData characterData, PlayerInputHandler.PlayerInputType inputType)
    {
        // Ensure required components exist
        PlayerInputHandler inputHandler = playerObj.GetComponent<PlayerInputHandler>();
        if (inputHandler == null)
        {
            inputHandler = playerObj.AddComponent<PlayerInputHandler>();
        }
        inputHandler.SetPlayerType(inputType);

        PlayerCharacter playerCharacter = playerObj.GetComponent<PlayerCharacter>();
        if (playerCharacter == null)
        {
            playerCharacter = playerObj.AddComponent<PlayerCharacter>();
        }

        PlayerHealth playerHealth = playerObj.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = playerObj.AddComponent<PlayerHealth>();
        }

        ArenaMovementRestrictor restrictor = playerObj.GetComponent<ArenaMovementRestrictor>();
        if (restrictor == null)
        {
            restrictor = playerObj.AddComponent<ArenaMovementRestrictor>();
        }

        // Set arena side based on input type
        if (inputType == PlayerInputHandler.PlayerInputType.Player1)
        {
            restrictor.SetPlayerSide(ArenaMovementRestrictor.PlayerSide.Left);
        }
        else
        {
            restrictor.SetPlayerSide(ArenaMovementRestrictor.PlayerSide.Right);
        }
    }

    void SetupHealthEventListeners()
    {
        if (player1Health != null)
        {
            player1Health.OnPlayerDeath += OnPlayer1Death;
            player1Health.OnHealthChanged += OnPlayer1HealthChanged;
        }

        if (player2Health != null)
        {
            player2Health.OnPlayerDeath += OnPlayer2Death;
            player2Health.OnHealthChanged += OnPlayer2HealthChanged;
        }
    }

    void InitializeMatchUI()
    {
        if (matchUI != null)
        {
            matchUI.InitializeMatch(
                selectionData.player1Character,
                selectionData.player2Character,
                player1RoundsWon,
                player2RoundsWon,
                roundsToWin
            );
        }
    }

    void StartRound(int roundNumber)
    {
        currentRound = roundNumber;
        currentState = MatchState.PreFight;
        currentRoundTime = roundDuration;

        if (debugMode)
        {
            Debug.Log($"Starting Round {roundNumber}");
        }

        // Reset player positions and health
        ResetPlayersForNewRound();

        // Update UI
        if (matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
            matchUI.UpdateTimer(currentRoundTime);
        }

        // Start pre-fight sequence
        StartCoroutine(PreFightSequence());
    }

    void ResetPlayersForNewRound()
    {
        // Reset positions
        if (player1GameObject != null && player1SpawnPoint != null)
        {
            player1GameObject.transform.position = player1SpawnPoint.position;
        }

        if (player2GameObject != null && player2SpawnPoint != null)
        {
            player2GameObject.transform.position = player2SpawnPoint.position;
        }

        // Reset health to full
        if (player1Health != null)
        {
            player1Health.SetHealth(player1Health.GetMaxHealth());
        }

        if (player2Health != null)
        {
            player2Health.SetHealth(player2Health.GetMaxHealth());
        }

        // Clear any effects
        if (player1Character != null)
        {
            // Reset any character-specific states
        }

        if (player2Character != null)
        {
            // Reset any character-specific states
        }

        // Reset ball
        BallManager ballManager = FindObjectOfType<BallManager>();
        if (ballManager != null)
        {
            ballManager.ResetBall();
        }
    }

    IEnumerator PreFightSequence()
    {
        currentState = MatchState.PreFight;

        // Show "Round X" announcement
        if (matchUI != null)
        {
            matchUI.ShowRoundAnnouncement(currentRound);
        }

        PlaySound(roundStartSound);

        // Countdown
        for (int i = Mathf.RoundToInt(preFightCountdown); i > 0; i--)
        {
            if (matchUI != null)
            {
                matchUI.ShowCountdown(i);
            }

            PlaySound(countdownSound);
            yield return new WaitForSeconds(1f);
        }

        // Show "FIGHT!"
        if (matchUI != null)
        {
            matchUI.ShowFightStart();
        }

        // Start the round
        StartFightPhase();
    }

    void StartFightPhase()
    {
        currentState = MatchState.Fighting;
        roundActive = true;

        // Enable player input
        EnablePlayerInput(true);

        // Start round timer
        if (roundTimerCoroutine != null)
        {
            StopCoroutine(roundTimerCoroutine);
        }
        roundTimerCoroutine = StartCoroutine(RoundTimerCoroutine());

        if (debugMode)
        {
            Debug.Log($"Round {currentRound} fight started!");
        }
    }

    IEnumerator RoundTimerCoroutine()
    {
        while (currentRoundTime > 0 && roundActive)
        {
            currentRoundTime -= Time.deltaTime;

            // Update UI
            if (matchUI != null)
            {
                matchUI.UpdateTimer(currentRoundTime);
            }

            yield return null;
        }

        // Time's up!
        if (roundActive)
        {
            EndRoundByTimeOut();
        }
    }

    void EndRoundByTimeOut()
    {
        if (!roundActive) return;

        roundActive = false;
        currentState = MatchState.RoundEnd;

        // Determine winner by health
        int roundWinner = DetermineWinnerByHealth();

        if (debugMode)
        {
            Debug.Log($"Round {currentRound} ended by timeout. Winner: Player {roundWinner}");
        }

        EndRound(roundWinner);
    }

    void EndRoundByKnockout(int winner)
    {
        if (!roundActive) return;

        roundActive = false;
        currentState = MatchState.RoundEnd;

        if (debugMode)
        {
            Debug.Log($"Round {currentRound} ended by knockout. Winner: Player {winner}");
        }

        EndRound(winner);
    }

    void EndRound(int winner)
    {
        // Stop round timer
        if (roundTimerCoroutine != null)
        {
            StopCoroutine(roundTimerCoroutine);
        }

        // Disable player input
        EnablePlayerInput(false);

        // Update round scores
        if (winner == 1)
        {
            player1RoundsWon++;
        }
        else if (winner == 2)
        {
            player2RoundsWon++;
        }

        // Update UI
        if (matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
            matchUI.ShowRoundResult(winner);
        }

        PlaySound(roundEndSound);

        // Check for match end
        if (player1RoundsWon >= roundsToWin || player2RoundsWon >= roundsToWin)
        {
            matchWinner = player1RoundsWon >= roundsToWin ? 1 : 2;
            StartCoroutine(EndMatchSequence());
        }
        else
        {
            // Continue to next round
            StartCoroutine(NextRoundDelay());
        }
    }

    IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(postRoundDelay);

        // Start next round
        StartRound(currentRound + 1);
    }

    IEnumerator EndMatchSequence()
    {
        currentState = MatchState.MatchEnd;

        if (debugMode)
        {
            Debug.Log($"Match ended! Winner: Player {matchWinner}");
        }

        // Show match results
        if (matchUI != null)
        {
            CharacterData winnerCharacter = matchWinner == 1 ? selectionData.player1Character : selectionData.player2Character;
            matchUI.ShowMatchResult(matchWinner, winnerCharacter);
        }

        PlaySound(matchWinSound);

        // Wait for player input or timeout
        yield return new WaitForSeconds(matchEndDelay);

        // Return to character selection
        ReturnToCharacterSelection();
    }

    int DetermineWinnerByHealth()
    {
        float player1HealthPercent = player1Health != null ? player1Health.GetHealthPercentage() : 0f;
        float player2HealthPercent = player2Health != null ? player2Health.GetHealthPercentage() : 0f;

        if (player1HealthPercent > player2HealthPercent)
            return 1;
        else if (player2HealthPercent > player1HealthPercent)
            return 2;
        else
            return 0; // Draw - no winner
    }

    void EnablePlayerInput(bool enable)
    {
        if (player1Character != null)
        {
            player1Character.SetInputEnabled(enable);
        }

        if (player2Character != null)
        {
            player2Character.SetInputEnabled(enable);
        }
    }

    #region Health Event Handlers

    void OnPlayer1Death(CharacterController deadPlayer)
    {
        if (!roundActive) return;
        EndRoundByKnockout(2); // Player 2 wins
    }

    void OnPlayer2Death(CharacterController deadPlayer)
    {
        if (!roundActive) return;
        EndRoundByKnockout(1); // Player 1 wins
    }

    void OnPlayer1HealthChanged(int currentHealth, int maxHealth)
    {
        if (matchUI != null)
        {
            matchUI.UpdatePlayerHealth(1, currentHealth, maxHealth);
        }
    }

    void OnPlayer2HealthChanged(int currentHealth, int maxHealth)
    {
        if (matchUI != null)
        {
            matchUI.UpdatePlayerHealth(2, currentHealth, maxHealth);
        }
    }

    #endregion

    #region Scene Management

    void ReturnToCharacterSelection()
    {
        // Clear selection data
        CharacterSelectionManager.ClearSelectionData();

        // Load character selection scene
        SceneManager.LoadScene(characterSelectionScene);
    }

    void ReturnToMainMenu()
    {
        // Clear selection data
        CharacterSelectionManager.ClearSelectionData();

        // Load main menu scene
        SceneManager.LoadScene(mainMenuScene);
    }

    #endregion

    #region Audio

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Network Ready Methods (Future)

    /// <summary>
    /// Future: Handle networked round end
    /// </summary>
    public void OnNetworkRoundEnd(int winner)
    {
        if (debugMode)
        {
            Debug.Log($"Network: Round ended, winner: Player {winner}");
        }

        EndRound(winner);
    }

    /// <summary>
    /// Future: Handle network player disconnect
    /// </summary>
    public void OnNetworkPlayerDisconnected(int playerID)
    {
        if (debugMode)
        {
            Debug.Log($"Network: Player {playerID} disconnected");
        }

        // Award win to remaining player
        int winner = playerID == 1 ? 2 : 1;
        matchWinner = winner;
        StartCoroutine(EndMatchSequence());
    }

    /// <summary>
    /// Future: Sync match state across network
    /// </summary>
    void SyncMatchState()
    {
        if (!enableNetworking) return;

        // Future: PUN2 RPC implementation
        // photonView.RPC("SyncMatchData", RpcTarget.Others, currentRound, player1RoundsWon, player2RoundsWon);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get current match state
    /// </summary>
    public MatchState GetMatchState() => currentState;

    /// <summary>
    /// Get current round number
    /// </summary>
    public int GetCurrentRound() => currentRound;

    /// <summary>
    /// Get rounds won by each player
    /// </summary>
    public void GetRoundScores(out int player1Wins, out int player2Wins)
    {
        player1Wins = player1RoundsWon;
        player2Wins = player2RoundsWon;
    }

    /// <summary>
    /// Get remaining round time
    /// </summary>
    public float GetRemainingTime() => currentRoundTime;

    /// <summary>
    /// Check if round is currently active
    /// </summary>
    public bool IsRoundActive() => roundActive;

    /// <summary>
    /// Manually end round (for debug/admin)
    /// </summary>
    public void ForceEndRound(int winner = 0)
    {
        if (roundActive)
        {
            if (winner == 0)
            {
                EndRoundByTimeOut();
            }
            else
            {
                EndRoundByKnockout(winner);
            }
        }
    }

    /// <summary>
    /// Restart current round (for debug)
    /// </summary>
    public void RestartRound()
    {
        if (currentState == MatchState.Fighting || currentState == MatchState.RoundEnd)
        {
            roundActive = false;
            if (roundTimerCoroutine != null)
            {
                StopCoroutine(roundTimerCoroutine);
            }

            StartRound(currentRound);
        }
    }

    /// <summary>
    /// Restart entire match (for debug)
    /// </summary>
    public void RestartMatch()
    {
        player1RoundsWon = 0;
        player2RoundsWon = 0;
        matchWinner = 0;
        roundActive = false;

        if (roundTimerCoroutine != null)
        {
            StopCoroutine(roundTimerCoroutine);
        }

        StartRound(1);
    }

    #endregion

    #region Debug Methods

    void Update()
    {
        if (!debugMode) return;

        // Debug controls
        if (Input.GetKeyDown(KeyCode.F3))
        {
            ForceEndRound(1); // Player 1 wins round
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            ForceEndRound(2); // Player 2 wins round
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            RestartRound();
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            RestartMatch();
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            ReturnToCharacterSelection();
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            ReturnToMainMenu();
        }

        // Emergency pause/unpause
        if (Input.GetKeyDown(KeyCode.P))
        {
            Time.timeScale = Time.timeScale > 0 ? 0 : 1;
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 250));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== MATCH MANAGER DEBUG ===");
        GUILayout.Label($"State: {currentState}");
        GUILayout.Label($"Round: {currentRound}");
        GUILayout.Label($"Timer: {currentRoundTime:F1}s");
        GUILayout.Label($"P1 Rounds: {player1RoundsWon}/{roundsToWin}");
        GUILayout.Label($"P2 Rounds: {player2RoundsWon}/{roundsToWin}");
        GUILayout.Label($"Round Active: {roundActive}");
        GUILayout.Label($"Match Winner: {(matchWinner == 0 ? "None" : $"Player {matchWinner}")}");

        if (player1Health != null && player2Health != null)
        {
            GUILayout.Label($"P1 Health: {player1Health.GetCurrentHealth()}/{player1Health.GetMaxHealth()}");
            GUILayout.Label($"P2 Health: {player2Health.GetCurrentHealth()}/{player2Health.GetMaxHealth()}");
        }

        GUILayout.Space(10);
        GUILayout.Label("Debug Controls:");
        GUILayout.Label("F3 - P1 Wins Round");
        GUILayout.Label("F4 - P2 Wins Round");
        GUILayout.Label("F5 - Restart Round");
        GUILayout.Label("F6 - Restart Match");
        GUILayout.Label("F7 - Character Select");
        GUILayout.Label("F8 - Main Menu");
        GUILayout.Label("P - Pause/Unpause");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        // Clean up event listeners
        if (player1Health != null)
        {
            player1Health.OnPlayerDeath -= OnPlayer1Death;
            player1Health.OnHealthChanged -= OnPlayer1HealthChanged;
        }

        if (player2Health != null)
        {
            player2Health.OnPlayerDeath -= OnPlayer2Death;
            player2Health.OnHealthChanged -= OnPlayer2HealthChanged;
        }
    }

    #endregion
}