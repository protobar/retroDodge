using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// ==================== HUD CONTROLLER ====================
public class HUDController : MonoBehaviour
{
    private static HUDController instance;
    public static HUDController Instance => instance;

    [Header("UI Elements")]
    public Image player1HealthBar;
    public Image player2HealthBar;
    public Text player1HealthText;
    public Text player2HealthText;
    public Text timerText;
    public Text roundText;
    public Text scoreText;

    [Header("Ultimate Meters")]
    public Image ultimateMeter1;
    public Image ultimateMeter2;
    public GameObject ultimateReady1;
    public GameObject ultimateReady2;

    [Header("Ball Hold Timer")]
    public GameObject ballHoldTimerParent;
    public Image ballHoldTimerFill;
    public Text ballHoldTimerText;

    [Header("Network Status")]
    public GameObject networkStatusIcon;
    public Text pingText;
    public Image connectionIcon;

    [Header("Round UI")]
    public GameObject roundStartPanel;
    public Text roundStartText;
    public Animator roundStartAnimator;

    [Header("Match End UI")]
    public GameObject matchEndPanel;
    public Text winnerText;
    public Text finalScoreText;
    public Button rematchButton;
    public Button mainMenuButton;

    [Header("Effects")]
    public AnimationCurve healthBarShakeCurve;
    public float ultimateGlowIntensity = 2f;
    public Color lowHealthColor = Color.red;
    public Color normalHealthColor = Color.green;

    // Internal state
    private Dictionary<int, Image> healthBars = new Dictionary<int, Image>();
    private Dictionary<int, Text> healthTexts = new Dictionary<int, Text>();
    private Dictionary<int, Image> ultimateMeters = new Dictionary<int, Image>();
    private Dictionary<int, GameObject> ultimateReadyIndicators = new Dictionary<int, GameObject>();

    private bool ballHoldTimerActive = false;
    private Transform ballHoldTarget;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeUI();

        // Subscribe to game events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += OnRoundStart;
            GameManager.Instance.OnRoundEnd += OnRoundEnd;
            GameManager.Instance.OnMatchEnd += OnMatchEnd;
        }
    }

    void Update()
    {
        UpdateBallHoldTimerPosition();
        UpdateNetworkStatus();
    }

    void InitializeUI()
    {
        // Initialize health bars
        healthBars[1] = player1HealthBar;
        healthBars[2] = player2HealthBar;

        healthTexts[1] = player1HealthText;
        healthTexts[2] = player2HealthText;

        // Initialize ultimate meters
        ultimateMeters[1] = ultimateMeter1;
        ultimateMeters[2] = ultimateMeter2;

        ultimateReadyIndicators[1] = ultimateReady1;
        ultimateReadyIndicators[2] = ultimateReady2;

        // Hide match end panel initially
        if (matchEndPanel != null)
            matchEndPanel.SetActive(false);

        // Setup button events
        if (rematchButton != null)
            rematchButton.onClick.AddListener(OnRematchClicked);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // Initialize ball hold timer as inactive
        if (ballHoldTimerParent != null)
            ballHoldTimerParent.SetActive(false);
    }

    public void UpdateHealthBar(int playerNumber, float health, float maxHealth = 120f)
    {
        if (healthBars.ContainsKey(playerNumber))
        {
            float normalizedHealth = health / maxHealth;
            healthBars[playerNumber].fillAmount = normalizedHealth;

            // Color change based on health
            Color healthColor;
            if (normalizedHealth > 0.6f)
                healthColor = normalHealthColor;
            else if (normalizedHealth > 0.3f)
                healthColor = Color.Lerp(normalHealthColor, Color.yellow, (0.6f - normalizedHealth) / 0.3f);
            else
                healthColor = Color.Lerp(Color.yellow, lowHealthColor, (0.3f - normalizedHealth) / 0.3f);

            healthBars[playerNumber].color = healthColor;

            // Update health text
            if (healthTexts.ContainsKey(playerNumber))
            {
                healthTexts[playerNumber].text = $"{Mathf.RoundToInt(health)}/{Mathf.RoundToInt(maxHealth)}";
            }

            // Shake effect on damage
            StartCoroutine(ShakeHealthBar(healthBars[playerNumber].transform));
        }
    }

    public void UpdateUltimateMeter(int playerNumber, float charge)
    {
        if (ultimateMeters.ContainsKey(playerNumber))
        {
            ultimateMeters[playerNumber].fillAmount = charge;

            // Show/hide ready indicator
            bool isReady = charge >= 1f;
            if (ultimateReadyIndicators.ContainsKey(playerNumber))
            {
                ultimateReadyIndicators[playerNumber].SetActive(isReady);

                if (isReady)
                {
                    ApplyUltimateReadyEffect(ultimateMeters[playerNumber]);
                }
            }
        }
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60);
            int seconds = Mathf.FloorToInt(timeRemaining % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

            // Flash timer when low
            if (timeRemaining <= 10f)
            {
                timerText.color = Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 2, 1));
            }
            else
            {
                timerText.color = Color.white;
            }
        }
    }

    public void UpdateScore(int player1Score, int player2Score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"{player1Score} - {player2Score}";
        }
    }

    public void ShowBallHoldTimer(Transform target, float holdTime, float maxTime)
    {
        ballHoldTimerActive = true;
        ballHoldTarget = target;

        if (ballHoldTimerParent != null)
        {
            ballHoldTimerParent.SetActive(true);
        }

        if (ballHoldTimerFill != null)
        {
            ballHoldTimerFill.fillAmount = holdTime / maxTime;

            // Color based on time
            Color timerColor;
            if (holdTime < 3f)
                timerColor = Color.green;
            else if (holdTime < 5f)
                timerColor = Color.Lerp(Color.yellow, Color.red, (holdTime - 3f) / 2f);
            else
                timerColor = Color.red;

            ballHoldTimerFill.color = timerColor;
        }

        // Show countdown text
        if (ballHoldTimerText != null && holdTime >= 3f)
        {
            float timeLeft = maxTime - holdTime;
            ballHoldTimerText.text = timeLeft.ToString("F1");
            ballHoldTimerText.gameObject.SetActive(true);
        }
        else if (ballHoldTimerText != null)
        {
            ballHoldTimerText.gameObject.SetActive(false);
        }
    }

    public void HideBallHoldTimer()
    {
        ballHoldTimerActive = false;
        ballHoldTarget = null;

        if (ballHoldTimerParent != null)
        {
            ballHoldTimerParent.SetActive(false);
        }
    }

    void UpdateBallHoldTimerPosition()
    {
        if (ballHoldTimerActive && ballHoldTarget != null && ballHoldTimerParent != null)
        {
            // Position above character
            Vector3 worldPos = ballHoldTarget.position + Vector3.up * 2f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Check if position is on screen
            if (screenPos.z > 0)
            {
                ballHoldTimerParent.transform.position = screenPos;
                ballHoldTimerParent.SetActive(true);
            }
            else
            {
                ballHoldTimerParent.SetActive(false);
            }
        }
    }

    void UpdateNetworkStatus()
    {
        if (networkStatusIcon != null && Photon.Pun.PhotonNetwork.IsConnected)
        {
            int ping = Photon.Pun.PhotonNetwork.GetPing();

            if (pingText != null)
            {
                pingText.text = $"{ping}ms";
            }

            if (connectionIcon != null)
            {
                Color iconColor;
                if (ping < 50)
                    iconColor = Color.green;
                else if (ping < 100)
                    iconColor = Color.yellow;
                else
                    iconColor = Color.red;

                connectionIcon.color = iconColor;
            }

            networkStatusIcon.SetActive(true);
        }
        else if (networkStatusIcon != null)
        {
            networkStatusIcon.SetActive(false);
        }
    }

    public void ShowRoundStart(int roundNumber)
    {
        if (roundStartPanel != null && roundStartText != null)
        {
            roundStartPanel.SetActive(true);
            roundStartText.text = $"ROUND {roundNumber}";

            if (roundStartAnimator != null)
            {
                roundStartAnimator.SetTrigger("ShowRound");
            }
            else
            {
                StartCoroutine(HideRoundStartDelayed());
            }
        }
    }

    IEnumerator HideRoundStartDelayed()
    {
        yield return new WaitForSeconds(2f);

        if (roundStartPanel != null)
        {
            // Fade out
            CanvasGroup canvasGroup = roundStartPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = roundStartPanel.AddComponent<CanvasGroup>();
            }

            float elapsed = 0f;
            float fadeTime = 0.5f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }

            roundStartPanel.SetActive(false);
            canvasGroup.alpha = 1f;
        }
    }

    IEnumerator ShakeHealthBar(Transform bar)
    {
        Vector3 originalPos = bar.localPosition;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = healthBarShakeCurve.Evaluate(elapsed / duration);

            Vector3 randomOffset = Random.insideUnitSphere * strength * 10f;
            randomOffset.z = 0; // Keep it 2D

            bar.localPosition = originalPos + randomOffset;
            yield return null;
        }

        bar.localPosition = originalPos;
    }

    void ApplyUltimateReadyEffect(Image meter)
    {
        // Pulsing glow effect
        if (meter.material != null)
        {
            float glowValue = Mathf.PingPong(Time.time * ultimateGlowIntensity, 1f) + 1f;
            meter.material.SetFloat("_GlowIntensity", glowValue);
        }

        // Scale pulsing
        float scale = 1f + Mathf.Sin(Time.time * 4f) * 0.1f;
        meter.transform.localScale = Vector3.one * scale;
    }

    // Event handlers
    void OnRoundStart(int roundNumber)
    {
        ShowRoundStart(roundNumber);

        if (roundText != null)
        {
            roundText.text = $"Round {roundNumber}";
        }
    }

    void OnRoundEnd(int winner)
    {
        // Update score display
        if (GameManager.Instance != null)
        {
            UpdateScore(GameManager.Instance.player1Score, GameManager.Instance.player2Score);
        }
    }

    void OnMatchEnd(int winner)
    {
        ShowMatchEndScreen(winner);
    }

    void ShowMatchEndScreen(int winner)
    {
        if (matchEndPanel != null)
        {
            matchEndPanel.SetActive(true);

            if (winnerText != null)
            {
                if (winner == Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    winnerText.text = "VICTORY!";
                    winnerText.color = Color.green;
                }
                else
                {
                    winnerText.text = "DEFEAT";
                    winnerText.color = Color.red;
                }
            }

            if (finalScoreText != null && GameManager.Instance != null)
            {
                finalScoreText.text = $"Final Score: {GameManager.Instance.player1Score} - {GameManager.Instance.player2Score}";
            }
        }
    }

    void OnRematchClicked()
    {
        // Request rematch
        AudioManager.Instance?.PlaySound("ButtonClick");

        // TODO: Implement rematch logic
    }

    void OnMainMenuClicked()
    {
        // Return to main menu
        AudioManager.Instance?.PlaySound("ButtonClick");

        // Disconnect from room
        Photon.Pun.PhotonNetwork.LeaveRoom();

        // Load main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= OnRoundStart;
            GameManager.Instance.OnRoundEnd -= OnRoundEnd;
            GameManager.Instance.OnMatchEnd -= OnMatchEnd;
        }
    }
}