using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// REFACTORED: Pure UI Display Component for Match
/// Handles ONLY UI display - no game logic, no networking, no disconnection handling
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

    [Header("Player Abilities")]
    [SerializeField] private Slider player1UltimateBar;
    [SerializeField] private Slider player1TrickBar;
    [SerializeField] private Slider player1TreatBar;
    [SerializeField] private Slider player2UltimateBar;
    [SerializeField] private Slider player2TrickBar;
    [SerializeField] private Slider player2TreatBar;
    [SerializeField] private TextMeshProUGUI player1UltimateText;
    [SerializeField] private TextMeshProUGUI player1TrickText;
    [SerializeField] private TextMeshProUGUI player1TreatText;
    [SerializeField] private TextMeshProUGUI player2UltimateText;
    [SerializeField] private TextMeshProUGUI player2TrickText;
    [SerializeField] private TextMeshProUGUI player2TreatText;

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

    [Header("Match End UI")]
    public Button returnToMenuButton;

    [Header("Visual Effects")]
    [SerializeField] private Color healthBarHealthyColor = Color.green;
    [SerializeField] private Color healthBarWarningColor = Color.yellow;
    [SerializeField] private Color healthBarCriticalColor = Color.red;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip uiUpdateSound;
    [SerializeField] private AudioClip countdownSound;
    [SerializeField] private AudioClip roundStartSound;
    [SerializeField] private AudioClip matchEndSound;

    [Header("Message Display")]
    public GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;

    // Private state
    private MatchManager matchManager;
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

        // Find MatchManager
        matchManager = FindObjectOfType<MatchManager>();
        if (matchManager == null)
        {
            Debug.LogError("[MATCH UI] MatchManager not found! MatchUI requires MatchManager to function.");
        }
    }

    void Start()
    {
        SetupButtonListeners();
        HideAllPanels();
    }

    void Update()
    {
        // Update ability bars if initialized
        if (isInitialized && matchManager != null)
        {
            UpdateAbilityBars();
        }
    }

    void SetupButtonListeners()
    {
        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
        }
    }

    void HideAllPanels()
    {
        if (announcementPanel != null) announcementPanel.SetActive(false);
        if (countdownPanel != null) countdownPanel.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(false);
        if (messagePanel != null) messagePanel.SetActive(false);
        if (returnToMenuButton != null) returnToMenuButton.gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API - Called by MatchManager
    // ═══════════════════════════════════════════════════════════════

    public void InitializeMatch(CharacterData p1Character, CharacterData p2Character, int p1Rounds, int p2Rounds, int totalRoundsToWin)
    {
        if (p1Character == null || p2Character == null)
        {
            Debug.LogError("[MATCH UI] Cannot initialize with null character data!");
            return;
        }

        // Setup player 1 info
        if (player1Name != null) player1Name.text = p1Character.characterName;
        if (player1Portrait != null && p1Character.characterIcon != null)
            player1Portrait.sprite = p1Character.characterIcon;

        // Setup player 2 info
        if (player2Name != null) player2Name.text = p2Character.characterName;
        if (player2Portrait != null && p2Character.characterIcon != null)
            player2Portrait.sprite = p2Character.characterIcon;

        // Initialize health bars
        UpdatePlayerHealth(1, p1Character.maxHealth, p1Character.maxHealth);
        UpdatePlayerHealth(2, p2Character.maxHealth, p2Character.maxHealth);

        // Initialize ability bars
        UpdateAbilityBars();

        // Initialize round info
        UpdateRoundInfo(1, p1Rounds, p2Rounds);

        isInitialized = true;
        PlaySound(uiUpdateSound);
    }

    public void UpdatePlayerHealth(int playerNumber, int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0) return;

        float healthPercentage = (float)currentHealth / maxHealth;
        Slider healthBar = playerNumber == 1 ? player1HealthBar : player2HealthBar;
        TextMeshProUGUI healthText = playerNumber == 1 ? player1HealthText : player2HealthText;
        Image healthFill = playerNumber == 1 ? player1HealthFill : player2HealthFill;

        if (healthBar != null)
        {
            healthBar.value = healthPercentage;
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }

        if (healthFill != null)
        {
            if (healthPercentage > 0.6f)
                healthFill.color = healthBarHealthyColor;
            else if (healthPercentage > 0.3f)
                healthFill.color = healthBarWarningColor;
            else
                healthFill.color = healthBarCriticalColor;
        }
    }

    public void UpdateRoundInfo(int currentRound, int player1Rounds, int player2Rounds)
    {
        if (roundNumberText != null)
        {
            roundNumberText.text = $"Round {currentRound}";
        }

        if (matchScoreText != null)
        {
            matchScoreText.text = $"{player1Rounds} - {player2Rounds}";
        }

        // Update round win indicators
        UpdateRoundWinIndicators(player1RoundWins, player1Rounds);
        UpdateRoundWinIndicators(player2RoundWins, player2Rounds);
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Update timer color based on remaining time
        if (timerBackground != null)
        {
            if (timeRemaining > 30f)
                timerBackground.color = normalTimerColor;
            else if (timeRemaining > 10f)
                timerBackground.color = warningTimerColor;
            else
                timerBackground.color = criticalTimerColor;
        }
    }

    public void ShowRoundAnnouncement(int roundNumber)
    {
        if (announcementPanel == null || announcementText == null) return;

        announcementText.text = $"Round {roundNumber}";
        announcementPanel.SetActive(true);
        PlaySound(roundStartSound);

        StartCoroutine(HideAnnouncementAfterDelay());
    }

    public void ShowCountdown(int number)
    {
        if (countdownPanel == null || countdownText == null) return;

        countdownText.text = number > 0 ? number.ToString() : "FIGHT!";
        countdownPanel.SetActive(true);
        PlaySound(countdownSound);

        StartCoroutine(HideCountdownAfterDelay());
    }

    public void ShowFightStart()
    {
        ShowCountdown(0);
    }

    public void ShowRoundResult(int winner)
    {
        if (announcementPanel == null || announcementText == null) return;

        string winnerName = winner == 1 ? player1Name?.text : player2Name?.text;
        announcementText.text = $"{winnerName} Wins Round!";
        announcementPanel.SetActive(true);

        StartCoroutine(HideAnnouncementAfterDelay());
    }

    public void ShowMatchResult(int winner, CharacterData winnerCharacter)
    {
        if (resultsPanel == null) return;

        resultsPanel.SetActive(true);

        if (resultsText != null)
        {
            resultsText.text = $"{winnerCharacter.characterName} Wins!";
        }

        if (winnerName != null)
        {
            winnerName.text = winnerCharacter.characterName;
        }

        if (winnerPortrait != null && winnerCharacter.characterIcon != null)
        {
            winnerPortrait.sprite = winnerCharacter.characterIcon;
        }

        PlaySound(matchEndSound);
    }

    public void ShowForfeitVictory(int winner, CharacterData winnerCharacter, string forfeitPlayerName)
    {
        if (resultsPanel == null) return;

        resultsPanel.SetActive(true);

        if (resultsText != null)
        {
            resultsText.text = $"{winnerCharacter.characterName} Wins by Forfeit!";
        }

        if (winnerName != null)
        {
            winnerName.text = $"{winnerCharacter.characterName}\n{forfeitPlayerName} Forfeited";
        }

        if (winnerPortrait != null && winnerCharacter.characterIcon != null)
        {
            winnerPortrait.sprite = winnerCharacter.characterIcon;
        }

        PlaySound(matchEndSound);
    }

    public void ShowReturnToMenuButton(bool show)
    {
        if (returnToMenuButton != null)
        {
            returnToMenuButton.gameObject.SetActive(show);
        }
    }

    public void ShowMessage(string message, float duration)
    {
        if (messagePanel == null || messageText == null) return;

        messageText.text = message;
        messagePanel.SetActive(true);

        StartCoroutine(HideMessageAfterDelay(duration));
    }

    // ═══════════════════════════════════════════════════════════════
    // ABILITY BAR UPDATES
    // ═══════════════════════════════════════════════════════════════

    void UpdateAbilityBars()
    {
        if (matchManager == null) return;

        // Get all players in the scene
        PlayerCharacter[] players = FindObjectsOfType<PlayerCharacter>();

        if (PhotonNetwork.OfflineMode)
        {
            foreach (PlayerCharacter player in players)
            {
                if (player == null) continue;

                int playerNumber = GetPlayerNumber(player);
                if (playerNumber == -1)
                {
                    // Derive from position if side not set
                    playerNumber = player.transform.position.x <= 0 ? 1 : 2;
                }

                bool isLocalPlayer = (playerNumber == 1);
                UpdatePlayerAbilityBars(playerNumber, player, isLocalPlayer);
            }
            return;
        }

        foreach (PlayerCharacter player in players)
        {
            if (player == null || player.photonView == null) continue;

            int playerNumber = GetPlayerNumber(player);
            if (playerNumber == -1) continue;

            // FIXED: Only show abilities to the respective player
            bool isLocalPlayer = player.photonView.IsMine;
            UpdatePlayerAbilityBars(playerNumber, player, isLocalPlayer);
        }
    }

    int GetPlayerNumber(PlayerCharacter player)
    {
        if (PhotonNetwork.OfflineMode)
        {
            int playerSide = player.GetPlayerSide();
            if (playerSide > 0) return playerSide;
            return player.transform.position.x <= 0 ? 1 : 2;
        }

        if (player.photonView == null || player.photonView.Owner == null) return -1;

        // Get player number based on actor number or player side
        int actorNumber = player.photonView.Owner.ActorNumber;
        int pSide = player.GetPlayerSide();

        // Use player side if available, otherwise use actor number
        if (pSide > 0) return pSide;
        return actorNumber;
    }

    void UpdatePlayerAbilityBars(int playerNumber, PlayerCharacter player, bool isLocalPlayer)
    {
        Slider ultimateBar = playerNumber == 1 ? player1UltimateBar : player2UltimateBar;
        Slider trickBar = playerNumber == 1 ? player1TrickBar : player2TrickBar;
        Slider treatBar = playerNumber == 1 ? player1TreatBar : player2TreatBar;

        TextMeshProUGUI ultimateText = playerNumber == 1 ? player1UltimateText : player2UltimateText;
        TextMeshProUGUI trickText = playerNumber == 1 ? player1TrickText : player2TrickText;
        TextMeshProUGUI treatText = playerNumber == 1 ? player1TreatText : player2TreatText;

        // FIXED: Ultimate is visible to both players (for strategic gameplay)
        if (ultimateBar != null)
        {
            float ultimateCharge = player.GetUltimateChargePercentage();
            ultimateBar.value = ultimateCharge;
        }

        if (ultimateText != null)
        {
            float ultimateCharge = player.GetUltimateChargePercentage();
            ultimateText.text = ultimateCharge >= 1f ? "READY" : $"{Mathf.RoundToInt(ultimateCharge * 100)}%";
        }

        // FIXED: Trick and Treat are only visible to the respective player
        if (isLocalPlayer)
        {
            // Show Trick bar for local player
            if (trickBar != null)
            {
                float trickCharge = player.GetTrickChargePercentage();
                trickBar.value = trickCharge;
            }

            if (trickText != null)
            {
                float trickCharge = player.GetTrickChargePercentage();
                trickText.text = trickCharge >= 1f ? "READY" : $"{Mathf.RoundToInt(trickCharge * 100)}%";
            }

            // Show Treat bar for local player
            if (treatBar != null)
            {
                float treatCharge = player.GetTreatChargePercentage();
                treatBar.value = treatCharge;
            }

            if (treatText != null)
            {
                float treatCharge = player.GetTreatChargePercentage();
                treatText.text = treatCharge >= 1f ? "READY" : $"{Mathf.RoundToInt(treatCharge * 100)}%";
            }
        }
        else
        {
            // Hide Trick and Treat bars for opponent
            if (trickBar != null) trickBar.value = 0f;
            if (trickText != null) trickText.text = "???";
            if (treatBar != null) treatBar.value = 0f;
            if (treatText != null) treatText.text = "???";
        }
    }

    void UpdateRoundWinIndicators(GameObject[] indicators, int wins)
    {
        if (indicators == null) return;

        for (int i = 0; i < indicators.Length; i++)
        {
            if (indicators[i] != null)
            {
                indicators[i].SetActive(i < wins);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // UI ANIMATIONS AND COROUTINES
    // ═══════════════════════════════════════════════════════════════

    IEnumerator HideAnnouncementAfterDelay()
    {
        yield return new WaitForSeconds(announcementDuration);
        if (announcementPanel != null)
            announcementPanel.SetActive(false);
    }

    IEnumerator HideCountdownAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        if (countdownPanel != null)
            countdownPanel.SetActive(false);
    }

    IEnumerator HideMessageAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (messagePanel != null)
            messagePanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    // BUTTON HANDLERS
    // ═══════════════════════════════════════════════════════════════

    void OnReturnToMenuClicked()
    {
        // REFACTORED: Only notify MatchManager, don't handle networking
        if (matchManager != null)
        {
            matchManager.OnReturnToMenuRequested();
        }
        else
        {
            Debug.LogError("[MATCH UI] MatchManager not found! Cannot handle return to menu request.");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}