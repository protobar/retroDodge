using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Match UI Manager - Handles all match-related UI elements
/// Displays round info, health bars, timer, and match results
/// </summary>
public class MatchUI : MonoBehaviour
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
    [SerializeField] private GameObject[] player1RoundWins; // Array of round win indicators
    [SerializeField] private GameObject[] player2RoundWins; // Array of round win indicators
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

    [Header("Visual Effects")]
    [SerializeField] private Color healthBarHealthyColor = Color.green;
    [SerializeField] private Color healthBarWarningColor = Color.yellow;
    [SerializeField] private Color healthBarCriticalColor = Color.red;
    [SerializeField] private AnimationCurve healthBarFlashCurve;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip uiUpdateSound;
    [SerializeField] private AudioClip announcementSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // State tracking
    private CharacterData player1Character;
    private CharacterData player2Character;
    private int roundsToWin = 2;
    private Coroutine healthBarFlashCoroutine;
    private Coroutine timerFlashCoroutine;

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

    void HideAllPanels()
    {
        if (announcementPanel != null)
            announcementPanel.SetActive(false);

        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        if (resultsPanel != null)
            resultsPanel.SetActive(false);
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

    /// <summary>
    /// Initialize the match UI with character data
    /// </summary>
    public void InitializeMatch(CharacterData p1Character, CharacterData p2Character, int p1Rounds, int p2Rounds, int totalRoundsToWin)
    {
        player1Character = p1Character;
        player2Character = p2Character;
        roundsToWin = totalRoundsToWin;

        // Setup player portraits and names
        SetupPlayerInfo();

        // Initialize health bars
        InitializeHealthBars();

        // Initialize round indicators
        InitializeRoundIndicators();

        // Update initial scores
        UpdateRoundInfo(1, p1Rounds, p2Rounds);

        if (debugMode)
        {
            Debug.Log($"MatchUI initialized: {p1Character.characterName} vs {p2Character.characterName}");
        }
    }

    void SetupPlayerInfo()
    {
        // Player 1
        if (player1Portrait != null && player1Character.characterIcon != null)
        {
            player1Portrait.sprite = player1Character.characterIcon;
        }

        if (player1Name != null)
        {
            player1Name.text = player1Character.characterName;
        }

        // Player 2
        if (player2Portrait != null && player2Character.characterIcon != null)
        {
            player2Portrait.sprite = player2Character.characterIcon;
        }

        if (player2Name != null)
        {
            player2Name.text = player2Character.characterName;
        }
    }

    void InitializeHealthBars()
    {
        // Player 1 health bar
        if (player1HealthBar != null)
        {
            player1HealthBar.maxValue = player1Character.maxHealth;
            player1HealthBar.value = player1Character.maxHealth;
        }

        if (player1HealthFill != null)
        {
            player1HealthFill.color = healthBarHealthyColor;
        }

        // Player 2 health bar
        if (player2HealthBar != null)
        {
            player2HealthBar.maxValue = player2Character.maxHealth;
            player2HealthBar.value = player2Character.maxHealth;
        }

        if (player2HealthFill != null)
        {
            player2HealthFill.color = healthBarHealthyColor;
        }

        // Update health text
        UpdateHealthText(1, player1Character.maxHealth, player1Character.maxHealth);
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
            indicatorImage.color = won ? Color.blue : Color.gray;
        }
    }

    /// <summary>
    /// Update player health display
    /// </summary>
    public void UpdatePlayerHealth(int playerNumber, int currentHealth, int maxHealth)
    {
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

            // Flash effect for low health
            if (healthPercentage <= 0.3f)
            {
                StartHealthBarFlash(healthFill);
            }
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

    void StartHealthBarFlash(Image healthFill)
    {
        if (healthBarFlashCoroutine != null)
        {
            StopCoroutine(healthBarFlashCoroutine);
        }

        healthBarFlashCoroutine = StartCoroutine(HealthBarFlashCoroutine(healthFill));
    }

    IEnumerator HealthBarFlashCoroutine(Image healthFill)
    {
        Color originalColor = healthFill.color;

        while (healthFill != null)
        {
            float flashValue = healthBarFlashCurve.Evaluate(Time.time % 1f);
            healthFill.color = Color.Lerp(originalColor, Color.white, flashValue * 0.3f);
            yield return null;
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
        }
        else if (timeRemaining > 10f)
        {
            targetColor = warningTimerColor;
        }
        else
        {
            targetColor = criticalTimerColor;

            // Flash timer when critical
            if (timerFlashCoroutine == null)
            {
                timerFlashCoroutine = StartCoroutine(TimerFlashCoroutine());
            }
        }

        if (timerText != null)
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
    /// Show round announcement
    /// </summary>
    public void ShowRoundAnnouncement(int roundNumber)
    {
        if (announcementPanel == null || announcementText == null) return;

        announcementText.text = $"ROUND {roundNumber}";
        StartCoroutine(ShowAnnouncementCoroutine(announcementDuration));
        PlaySound(announcementSound);
    }

    /// <summary>
    /// Show countdown number
    /// </summary>
    public void ShowCountdown(int number)
    {
        if (countdownPanel == null || countdownText == null) return;

        countdownText.text = number.ToString();
        countdownPanel.SetActive(true);
        StartCoroutine(HideCountdownCoroutine(1f));
    }

    /// <summary>
    /// Show "FIGHT!" message
    /// </summary>
    public void ShowFightStart()
    {
        if (countdownPanel == null || countdownText == null) return;

        countdownText.text = "FIGHT!";
        countdownPanel.SetActive(true);
        StartCoroutine(HideCountdownCoroutine(1f));
    }

    /// <summary>
    /// Show round result
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

        // Setup winner display
        if (winnerPortrait != null && winnerCharacter.characterIcon != null)
        {
            winnerPortrait.sprite = winnerCharacter.characterIcon;
        }

        if (winnerName != null)
        {
            winnerName.text = winnerCharacter.characterName;
        }

        if (resultsText != null)
        {
            resultsText.text = $"{winnerCharacter.characterName}\nWINS THE MATCH!";
        }

        // Show results panel
        resultsPanel.SetActive(true);

        PlaySound(announcementSound);
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
        // Find MatchManager and restart match
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        if (matchManager != null)
        {
            matchManager.RestartMatch();
        }

        // Hide results panel
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }

        PlaySound(uiUpdateSound);
    }

    void OnMainMenuClicked()
    {
        // Return to main menu
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        if (matchManager != null)
        {
            // This will be implemented in MatchManager
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        PlaySound(uiUpdateSound);
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

    #region Debug

    void Update()
    {
        if (!debugMode) return;

        // Debug controls
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            UpdatePlayerHealth(1, 50, 100); // Test P1 health
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            UpdatePlayerHealth(2, 25, 100); // Test P2 health
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ShowRoundAnnouncement(Random.Range(1, 4)); // Test round announcement
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ShowCountdown(3); // Test countdown
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 310, 150));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== MATCH UI DEBUG ===");
        GUILayout.Label($"P1 Character: {(player1Character?.characterName ?? "None")}");
        GUILayout.Label($"P2 Character: {(player2Character?.characterName ?? "None")}");
        GUILayout.Label($"Rounds to Win: {roundsToWin}");

        GUILayout.Space(10);
        GUILayout.Label("Test Controls:");
        GUILayout.Label("1 - P1 Health 50%");
        GUILayout.Label("2 - P2 Health 25%");
        GUILayout.Label("3 - Round Announcement");
        GUILayout.Label("4 - Countdown");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion
}