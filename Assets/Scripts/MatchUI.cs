using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// PUN2 Multiplayer Match UI Manager
/// Handles all match-related UI elements with network synchronization
/// </summary>
public class MatchUI : MonoBehaviourPunCallbacks
{
    [Header("Player Health Bars")]
    [SerializeField] private Slider player1HealthBar;
    [SerializeField] private Slider player2HealthBar;
    [SerializeField] private TextMeshProUGUI player1HealthText;
    [SerializeField] private TextMeshProUGUI player2HealthText;
    [SerializeField] private Image player1HealthFill;
    [SerializeField] private Image player2HealthFill;

    [Header("Player Info")]
    [SerializeField] private Image player1Portrait;
    [SerializeField] private Image player2Portrait;
    [SerializeField] private TextMeshProUGUI player1Name;
    [SerializeField] private TextMeshProUGUI player2Name;

    [Header("Round Info")]
    [SerializeField] private TextMeshProUGUI roundNumberText;
    [SerializeField] private GameObject[] player1RoundWins;
    [SerializeField] private GameObject[] player2RoundWins;
    [SerializeField] private TextMeshProUGUI matchScoreText;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Image timerBackground;
    [SerializeField] private Color normalTimerColor = Color.white;
    [SerializeField] private Color warningTimerColor = Color.yellow;
    [SerializeField] private Color criticalTimerColor = Color.red;

    [Header("Round Announcements")]
    [SerializeField] private GameObject announcementPanel;
    [SerializeField] private TextMeshProUGUI announcementText;
    [SerializeField] private float announcementDuration = 2f;

    [Header("Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Match Results")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TextMeshProUGUI resultsText;
    [SerializeField] private Image winnerPortrait;
    [SerializeField] private TextMeshProUGUI winnerName;
    [SerializeField] private Button rematchButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Match End UI")]
    public Button returnToMenuButton;
    public GameObject matchEndPanel;

    [Header("Network Status")]
    [SerializeField] private GameObject networkStatusPanel;
    [SerializeField] private TextMeshProUGUI networkStatusText;
    [SerializeField] private GameObject connectionLostPanel;

    [Header("Visual Effects")]
    [SerializeField] private Color healthBarHealthyColor = Color.green;
    [SerializeField] private Color healthBarWarningColor = Color.yellow;
    [SerializeField] private Color healthBarCriticalColor = Color.red;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip uiUpdateSound;
    [SerializeField] private AudioClip announcementSound;

    [Header("Message Display")]
    public GameObject messagePanel;
    public TextMeshProUGUI messageText;

    [Header("Forfeit Victory")]
    public AudioClip forfeitSound;
    public AudioClip victorySound;

    // State tracking
    private CharacterData player1Character;
    private CharacterData player2Character;
    private int roundsToWin = 2;
    private Coroutine timerFlashCoroutine;
    private bool isInitialized = false;

    void Awake()
    {
        // Setup audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        // Hide panels initially
        HideAllPanels();

        // Setup button listeners
        SetupButtonListeners();
    }

    void Start()
    {
        // Show network status
        UpdateNetworkStatus();
    }

    void HideAllPanels()
    {
        if (announcementPanel != null)
            announcementPanel.SetActive(false);

        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (connectionLostPanel != null)
            connectionLostPanel.SetActive(false);
    }

    void SetupButtonListeners()
    {
        if (rematchButton != null)
        {
            rematchButton.onClick.AddListener(OnRematchClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    void UpdateNetworkStatus()
    {
        if (networkStatusPanel == null || networkStatusText == null) return;

        string statusText = "";
        if (PhotonNetwork.IsConnected)
        {
            statusText = $"Room: {PhotonNetwork.CurrentRoom?.Name ?? "Unknown"}\n";
            statusText += $"Players: {PhotonNetwork.PlayerList.Length}/2\n";
            statusText += $"Ping: {PhotonNetwork.GetPing()}ms";
        }
        else
        {
            statusText = "Disconnected";
        }

        networkStatusText.text = statusText;

        // Auto-hide network status after a few seconds
        if (PhotonNetwork.PlayerList.Length >= 2)
        {
            StartCoroutine(HideNetworkStatusAfterDelay(3f));
        }
    }

    IEnumerator HideNetworkStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (networkStatusPanel != null)
        {
            networkStatusPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Initialize the match UI with character data
    /// </summary>
    public void InitializeMatch(CharacterData p1Character, CharacterData p2Character, int p1Rounds, int p2Rounds, int totalRoundsToWin)
    {
        player1Character = p1Character;
        player2Character = p2Character;
        roundsToWin = totalRoundsToWin;

        // Setup player info
        SetupPlayerInfo();

        // Initialize health bars
        InitializeHealthBars();

        // Initialize round indicators
        InitializeRoundIndicators();

        // Update initial scores
        UpdateRoundInfo(1, p1Rounds, p2Rounds);

        isInitialized = true;
    }

    void SetupPlayerInfo()
    {
        // Player 1 - determine if this is local player
        if (player1Portrait != null && player1Character != null && player1Character.characterIcon != null)
        {
            player1Portrait.sprite = player1Character.characterIcon;
        }

        if (player1Name != null && player1Character != null)
        {
            string displayName = player1Character.characterName;

            // Add network player info
            if (PhotonNetwork.PlayerList.Length >= 1)
            {
                Player networkPlayer1 = GetPlayerByActorNumber(1);
                if (networkPlayer1 != null)
                {
                    displayName += $"\n{networkPlayer1.NickName}";

                    // Highlight local player
                    if (networkPlayer1 == PhotonNetwork.LocalPlayer)
                    {
                        displayName += " (You)";
                        player1Name.color = Color.cyan;
                    }
                }
            }

            player1Name.text = displayName;
        }

        // Player 2
        if (player2Portrait != null && player2Character != null && player2Character.characterIcon != null)
        {
            player2Portrait.sprite = player2Character.characterIcon;
        }

        if (player2Name != null && player2Character != null)
        {
            string displayName = player2Character.characterName;

            // Add network player info
            if (PhotonNetwork.PlayerList.Length >= 2)
            {
                Player networkPlayer2 = GetPlayerByActorNumber(2);
                if (networkPlayer2 != null)
                {
                    displayName += $"\n{networkPlayer2.NickName}";

                    // Highlight local player
                    if (networkPlayer2 == PhotonNetwork.LocalPlayer)
                    {
                        displayName += " (You)";
                        player2Name.color = Color.cyan;
                    }
                }
            }

            player2Name.text = displayName;
        }
    }

    Player GetPlayerByActorNumber(int actorNumber)
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
            {
                return player;
            }
        }
        return null;
    }

    void InitializeHealthBars()
    {
        // Player 1 health bar
        if (player1HealthBar != null && player1Character != null)
        {
            player1HealthBar.maxValue = player1Character.maxHealth;
            player1HealthBar.value = player1Character.maxHealth;
        }

        if (player1HealthFill != null)
        {
            player1HealthFill.color = healthBarHealthyColor;
        }

        // Player 2 health bar
        if (player2HealthBar != null && player2Character != null)
        {
            player2HealthBar.maxValue = player2Character.maxHealth;
            player2HealthBar.value = player2Character.maxHealth;
        }

        if (player2HealthFill != null)
        {
            player2HealthFill.color = healthBarHealthyColor;
        }

        // Update health text
        if (player1Character != null)
            UpdateHealthText(1, player1Character.maxHealth, player1Character.maxHealth);
        if (player2Character != null)
            UpdateHealthText(2, player2Character.maxHealth, player2Character.maxHealth);
    }

    void InitializeRoundIndicators()
    {
        // Enable correct number of round indicators based on rounds to win
        for (int i = 0; i < player1RoundWins.Length; i++)
        {
            if (player1RoundWins[i] != null)
            {
                player1RoundWins[i].SetActive(i < roundsToWin);
                SetRoundWinIndicator(player1RoundWins[i], false);
            }
        }

        for (int i = 0; i < player2RoundWins.Length; i++)
        {
            if (player2RoundWins[i] != null)
            {
                player2RoundWins[i].SetActive(i < roundsToWin);
                SetRoundWinIndicator(player2RoundWins[i], false);
            }
        }
    }

    void SetRoundWinIndicator(GameObject indicator, bool won)
    {
        Image indicatorImage = indicator.GetComponent<Image>();
        if (indicatorImage != null)
        {
            indicatorImage.color = won ? Color.yellow : Color.gray;
        }
    }

    /// <summary>
    /// Update player health display
    /// </summary>
    public void UpdatePlayerHealth(int playerNumber, int currentHealth, int maxHealth)
    {
        if (!isInitialized) return;

        if (playerNumber == 1)
        {
            UpdateHealthBar(player1HealthBar, player1HealthFill, currentHealth, maxHealth);
            UpdateHealthText(1, currentHealth, maxHealth);
        }
        else if (playerNumber == 2)
        {
            UpdateHealthBar(player2HealthBar, player2HealthFill, currentHealth, maxHealth);
            UpdateHealthText(2, currentHealth, maxHealth);
        }

        PlaySound(uiUpdateSound);
    }

    void UpdateHealthBar(Slider healthBar, Image healthFill, int currentHealth, int maxHealth)
    {
        if (healthBar == null) return;

        float healthPercentage = (float)currentHealth / maxHealth;
        healthBar.value = currentHealth;

        // Update color based on health percentage
        if (healthFill != null)
        {
            Color targetColor;
            if (healthPercentage > 0.6f)
            {
                targetColor = healthBarHealthyColor;
            }
            else if (healthPercentage > 0.3f)
            {
                targetColor = healthBarWarningColor;
            }
            else
            {
                targetColor = healthBarCriticalColor;
            }

            healthFill.color = targetColor;
        }
    }

    void UpdateHealthText(int playerNumber, int currentHealth, int maxHealth)
    {
        TextMeshProUGUI healthText = playerNumber == 1 ? player1HealthText : player2HealthText;

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    /// <summary>
    /// Update round information
    /// </summary>
    public void UpdateRoundInfo(int currentRound, int player1Rounds, int player2Rounds)
    {
        // Update round number
        if (roundNumberText != null)
        {
            roundNumberText.text = $"ROUND {currentRound}";
        }

        // Update match score
        if (matchScoreText != null)
        {
            matchScoreText.text = $"{player1Rounds} - {player2Rounds}";
        }

        // Update round win indicators
        UpdateRoundWinIndicators(player1Rounds, player2Rounds);

        PlaySound(uiUpdateSound);
    }

    void UpdateRoundWinIndicators(int player1Rounds, int player2Rounds)
    {
        // Update Player 1 round wins
        for (int i = 0; i < player1RoundWins.Length && i < roundsToWin; i++)
        {
            if (player1RoundWins[i] != null)
            {
                SetRoundWinIndicator(player1RoundWins[i], i < player1Rounds);
            }
        }

        // Update Player 2 round wins
        for (int i = 0; i < player2RoundWins.Length && i < roundsToWin; i++)
        {
            if (player2RoundWins[i] != null)
            {
                SetRoundWinIndicator(player2RoundWins[i], i < player2Rounds);
            }
        }
    }

    /// <summary>
    /// Update timer display
    /// </summary>
    public void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;

        // Format time as MM:SS
        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Update timer color based on remaining time
        UpdateTimerColor(timeRemaining);
    }

    void UpdateTimerColor(float timeRemaining)
    {
        Color targetColor;

        if (timeRemaining > 30f)
        {
            targetColor = normalTimerColor;
            // Stop flashing if it was active
            if (timerFlashCoroutine != null)
            {
                StopCoroutine(timerFlashCoroutine);
                timerFlashCoroutine = null;
            }
        }
        else if (timeRemaining > 10f)
        {
            targetColor = warningTimerColor;
            // Stop flashing if it was active
            if (timerFlashCoroutine != null)
            {
                StopCoroutine(timerFlashCoroutine);
                timerFlashCoroutine = null;
            }
        }
        else
        {
            targetColor = criticalTimerColor;
            // Start flashing timer when critical
            if (timerFlashCoroutine == null)
            {
                timerFlashCoroutine = StartCoroutine(TimerFlashCoroutine());
            }
        }

        if (timerText != null && timerFlashCoroutine == null)
        {
            timerText.color = targetColor;
        }

        if (timerBackground != null)
        {
            timerBackground.color = Color.Lerp(Color.clear, targetColor, 0.2f);
        }
    }

    IEnumerator TimerFlashCoroutine()
    {
        while (timerText != null)
        {
            float flash = Mathf.PingPong(Time.time * 3f, 1f);
            timerText.color = Color.Lerp(criticalTimerColor, Color.white, flash);
            yield return null;
        }
    }

    /// <summary>
    /// Show round announcement (synchronized)
    /// </summary>
    public void ShowRoundAnnouncement(int roundNumber)
    {
        if (announcementPanel == null || announcementText == null) return;

        announcementText.text = $"ROUND {roundNumber}";
        StartCoroutine(ShowAnnouncementCoroutine(announcementDuration));
        PlaySound(announcementSound);
    }

    /// <summary>
    /// Show countdown number (synchronized)
    /// </summary>
    public void ShowCountdown(int number)
    {
        if (countdownPanel == null || countdownText == null) return;

        countdownText.text = number.ToString();
        countdownPanel.SetActive(true);
        StartCoroutine(HideCountdownCoroutine(1f));
    }

    /// <summary>
    /// Show "FIGHT!" message (synchronized)
    /// </summary>
    public void ShowFightStart()
    {
        if (countdownPanel == null || countdownText == null) return;

        countdownText.text = "FIGHT!";
        countdownPanel.SetActive(true);
        StartCoroutine(HideCountdownCoroutine(1f));
    }

    /// <summary>
    /// Show round result (synchronized)
    /// </summary>
    public void ShowRoundResult(int winner)
    {
        if (announcementPanel == null || announcementText == null) return;

        string winnerName = "";
        if (winner == 1 && player1Character != null)
        {
            winnerName = player1Character.characterName;
        }
        else if (winner == 2 && player2Character != null)
        {
            winnerName = player2Character.characterName;
        }

        if (winner == 0)
        {
            announcementText.text = "ROUND DRAW!";
        }
        else
        {
            announcementText.text = $"{winnerName} WINS ROUND!";
        }

        StartCoroutine(ShowAnnouncementCoroutine(announcementDuration));
        PlaySound(announcementSound);
    }

    /// <summary>
    /// Show final match result
    /// </summary>
    public void ShowMatchResult(int winner, CharacterData winnerCharacter)
    {
        if (resultsPanel == null) return;

        // Setup winner display (existing code)
        if (winnerPortrait != null && winnerCharacter.characterIcon != null)
        {
            winnerPortrait.sprite = winnerCharacter.characterIcon;
        }

        if (winnerName != null)
        {
            string displayText = winnerCharacter.characterName;

            Player networkWinner = GetPlayerByActorNumber(winner);
            if (networkWinner != null)
            {
                displayText += $"\n({networkWinner.NickName})";
            }

            winnerName.text = displayText;
        }

        if (resultsText != null)
        {
            resultsText.text = $"{winnerCharacter.characterName}\nWINS THE MATCH!";
        }

        // Show results panel
        resultsPanel.SetActive(true);

        // CHANGED: Don't show return button immediately
        // ShowReturnToMenuButton(false); // Hide initially
    }

    public void ShowReturnToMenuButton(bool show)
    {
        if (returnToMenuButton != null)
        {
            returnToMenuButton.gameObject.SetActive(show);

            // Ensure button is interactable
            returnToMenuButton.interactable = true;

            // Set up button click handler
            returnToMenuButton.onClick.RemoveAllListeners();
            returnToMenuButton.onClick.AddListener(() => {
                // Find MatchManager and call return method
                MatchManager matchManager = FindObjectOfType<MatchManager>();
                if (matchManager != null)
                {
                    matchManager.OnReturnToMenuButtonPressed();
                }
            });
        }

        if (matchEndPanel != null)
        {
            matchEndPanel.SetActive(show);
        }
    }

    // ADD to MatchUI.cs - Show forfeit victory
    public void ShowForfeitVictory(int winner, CharacterData winnerCharacter, string forfeitPlayerName)
    {
        if (resultsPanel == null) return;

        // Setup winner display
        if (winnerPortrait != null && winnerCharacter.characterIcon != null)
        {
            winnerPortrait.sprite = winnerCharacter.characterIcon;
        }

        if (winnerName != null)
        {
            string displayText = winnerCharacter.characterName;

            // Add network player name
            Player networkWinner = GetPlayerByActorNumber(winner);
            if (networkWinner != null)
            {
                displayText += $"\n({networkWinner.NickName})";
            }

            winnerName.text = displayText;
        }

        if (resultsText != null)
        {
            resultsText.text = $"VICTORY BY FORFEIT!\n{forfeitPlayerName} left the match";
        }

        // Show results panel
        resultsPanel.SetActive(true);

        PlaySound(victorySound);
    }

    // ADD to MatchUI.cs - Show temporary message
    public void ShowMessage(string message, float duration)
    {
        StartCoroutine(ShowMessageCoroutine(message, duration));
    }

    // ADD to MatchUI.cs - Message display coroutine
    private IEnumerator ShowMessageCoroutine(string message, float duration)
    {
        // Create or find a message display UI element
        GameObject messageObj = null;
        //Text messageText = null;

        // Try to find existing message display
        if (messagePanel != null)
        {
            messageObj = messagePanel;
            messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        }
        else if (announcementText != null)
        {
            // Use announcement text as fallback
            messageText = announcementText;
            messageObj = announcementText.gameObject;
        }

        if (messageText != null)
        {
            messageText.text = message;
            messageObj.SetActive(true);

            yield return new WaitForSeconds(duration);

            // Only hide if we're using the message panel (not announcement text)
            if (messagePanel != null)
            {
                messageObj.SetActive(false);
            }
        }
        else
        {
            // Fallback: Log to console if no UI available
            Debug.Log($"[MATCH MESSAGE] {message}");
        }
    }

    IEnumerator ShowAnnouncementCoroutine(float duration)
    {
        if (announcementPanel != null)
        {
            announcementPanel.SetActive(true);
            yield return new WaitForSeconds(duration);
            announcementPanel.SetActive(false);
        }
    }

    IEnumerator HideCountdownCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    #region Button Handlers

    void OnRematchClicked()
    {
        // Only Master Client can initiate rematch
        if (PhotonNetwork.IsMasterClient)
        {
            // Reset room for new match
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable());

            // Reload scene for all players
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.LoadLevel(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }

        PlaySound(uiUpdateSound);
    }

    public void OnMainMenuClicked()
    {
        // Check if we can actually leave the room
        var clientState = PhotonNetwork.NetworkClientState;

        if (PhotonNetwork.InRoom &&
            clientState == Photon.Realtime.ClientState.Joined)
        {
            // Safe to leave room
            PhotonNetwork.LeaveRoom();
        }
        else if (clientState == Photon.Realtime.ClientState.Leaving ||
                 clientState == Photon.Realtime.ClientState.Disconnecting)
        {
            // Already leaving, wait for OnLeftRoom callback
            Debug.Log("Already leaving room, waiting for callback...");
        }
        else
        {
            // Not in room or not connected, go directly to main menu
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    #endregion

    #region Network Callbacks

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateNetworkStatus();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateNetworkStatus();

        // Show connection lost if we're down to 1 player during a match
        if (PhotonNetwork.PlayerList.Length < 2 && isInitialized)
        {
            ShowConnectionLost();
        }
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        ShowConnectionLost();
    }

    void ShowConnectionLost()
    {
        if (connectionLostPanel != null)
        {
            connectionLostPanel.SetActive(true);
        }
    }

    #endregion

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void OnDestroy()
    {
        // Clean up coroutines
        if (timerFlashCoroutine != null)
        {
            StopCoroutine(timerFlashCoroutine);
        }
    }
}