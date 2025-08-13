using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Ball Hold Timer UI System - Visual feedback for ball possession timer
/// Shows warning, danger, and penalty phases with progress indicators
/// </summary>
public class BallHoldTimerUI : MonoBehaviour
{
    public static BallHoldTimerUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Canvas timerCanvas;
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private Image timerFillImage;
    [SerializeField] private Text timerText;
    [SerializeField] private Text phaseText;
    [SerializeField] private Image backgroundImage;

    [Header("Timer Colors")]
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;
    [SerializeField] private Color penaltyColor = Color.magenta;

    [Header("Animation Settings")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float shakeIntensity = 5f;
    [SerializeField] private bool enablePulseAnimation = true;
    [SerializeField] private bool enableShakeAnimation = true;

    [Header("Positioning")]
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private Vector3 offsetFromPlayer = new Vector3(0, 2f, 0);
    [SerializeField] private bool useScreenSpaceOverlay = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warningBeep;
    [SerializeField] private AudioClip dangerBeep;
    [SerializeField] private AudioClip penaltyAlarm;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Current timer state
    private BallController currentBall;
    private bool isTimerActive = false;
    private string currentPhase = "Normal";
    private Camera mainCamera;
    private RectTransform timerRectTransform;
    private Vector3 originalPosition;
    private Coroutine pulseCoroutine;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeUI();
    }

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (timerPanel != null)
        {
            timerRectTransform = timerPanel.GetComponent<RectTransform>();
            originalPosition = timerRectTransform.localPosition;
        }

        // Setup audio
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        HideTimer();
    }

    void Update()
    {
        if (isTimerActive && currentBall != null)
        {
            UpdateTimerDisplay();

            if (followPlayer)
            {
                UpdateTimerPosition();
            }
        }
    }

    void InitializeUI()
    {
        // Create timer canvas if it doesn't exist
        if (timerCanvas == null)
        {
            CreateTimerCanvas();
        }

        // Ensure timer panel is initially hidden
        if (timerPanel != null)
        {
            timerPanel.SetActive(false);
        }
    }

    void CreateTimerCanvas()
    {
        GameObject canvasGO = new GameObject("BallHoldTimerCanvas");
        timerCanvas = canvasGO.AddComponent<Canvas>();

        if (useScreenSpaceOverlay)
        {
            timerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        else
        {
            timerCanvas.renderMode = RenderMode.WorldSpace;
        }

        timerCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        CreateTimerPanel();
    }

    void CreateTimerPanel()
    {
        if (timerCanvas == null) return;

        // Create timer panel
        GameObject panelGO = new GameObject("TimerPanel");
        panelGO.transform.SetParent(timerCanvas.transform, false);
        timerPanel = panelGO;

        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(200, 80);
        panelRect.anchoredPosition = new Vector2(0, 100);

        // Background image
        backgroundImage = panelGO.AddComponent<Image>();
        backgroundImage.color = new Color(0, 0, 0, 0.7f);

        // Create progress bar background
        GameObject progressBG = new GameObject("ProgressBackground");
        progressBG.transform.SetParent(panelGO.transform, false);

        RectTransform progressBGRect = progressBG.AddComponent<RectTransform>();
        progressBGRect.sizeDelta = new Vector2(180, 20);
        progressBGRect.anchoredPosition = new Vector2(0, 10);

        Image progressBGImage = progressBG.AddComponent<Image>();
        progressBGImage.color = Color.black;

        // Create progress bar fill
        GameObject progressFill = new GameObject("ProgressFill");
        progressFill.transform.SetParent(progressBG.transform, false);

        RectTransform fillRect = progressFill.AddComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(180, 20);
        fillRect.anchoredPosition = Vector2.zero;

        timerFillImage = progressFill.AddComponent<Image>();
        timerFillImage.color = normalColor;
        timerFillImage.type = Image.Type.Filled;
        timerFillImage.fillMethod = Image.FillMethod.Horizontal;

        // Create timer text
        GameObject timerTextGO = new GameObject("TimerText");
        timerTextGO.transform.SetParent(panelGO.transform, false);

        RectTransform timerTextRect = timerTextGO.AddComponent<RectTransform>();
        timerTextRect.sizeDelta = new Vector2(180, 30);
        timerTextRect.anchoredPosition = new Vector2(0, -15);

        timerText = timerTextGO.AddComponent<Text>();
        timerText.text = "5.0s";
        timerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        timerText.fontSize = 18;
        timerText.color = Color.white;
        timerText.alignment = TextAnchor.MiddleCenter;

        // Create phase text
        GameObject phaseTextGO = new GameObject("PhaseText");
        phaseTextGO.transform.SetParent(panelGO.transform, false);

        RectTransform phaseTextRect = phaseTextGO.AddComponent<RectTransform>();
        phaseTextRect.sizeDelta = new Vector2(180, 20);
        phaseTextRect.anchoredPosition = new Vector2(0, 30);

        phaseText = phaseTextGO.AddComponent<Text>();
        phaseText.text = "HOLDING BALL";
        phaseText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        phaseText.fontSize = 14;
        phaseText.color = Color.white;
        phaseText.alignment = TextAnchor.MiddleCenter;
        phaseText.fontStyle = FontStyle.Bold;

        if (debugMode)
        {
            Debug.Log("BallHoldTimerUI: Created timer UI elements");
        }
    }

    void UpdateTimerDisplay()
    {
        if (currentBall == null || timerFillImage == null || timerText == null) return;

        float holdDuration = currentBall.GetHoldDuration();
        float maxHoldTime = currentBall.GetMaxHoldTime();
        float progress = currentBall.GetHoldProgress();
        string phase = currentBall.GetCurrentHoldPhase();

        // Update progress bar
        timerFillImage.fillAmount = progress;

        // Update timer text
        float timeRemaining = maxHoldTime - holdDuration;
        timerText.text = $"{timeRemaining:F1}s";

        // Update phase text and colors based on current phase
        if (phase != currentPhase)
        {
            OnPhaseChanged(phase);
            currentPhase = phase;
        }

        UpdateColorsForPhase(phase, progress);

        if (debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Timer UI: {holdDuration:F1}s / {maxHoldTime}s | Phase: {phase} | Progress: {progress:P0}");
        }
    }

    void UpdateTimerPosition()
    {
        if (currentBall == null || timerRectTransform == null || mainCamera == null) return;

        Transform ballHolder = currentBall.GetHolder()?.transform ?? currentBall.GetHolderLegacy()?.transform;
        if (ballHolder == null) return;

        Vector3 worldPosition = ballHolder.position + offsetFromPlayer;

        if (useScreenSpaceOverlay)
        {
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
            timerRectTransform.position = screenPosition;
        }
        else
        {
            timerRectTransform.position = worldPosition;
        }
    }

    void OnPhaseChanged(string newPhase)
    {
        // Stop existing animations
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        // Update phase text
        switch (newPhase)
        {
            case "Normal":
                phaseText.text = "HOLDING BALL";
                break;
            case "Warning":
                phaseText.text = "⚠️ WARNING ⚠️";
                PlayTimerSound(warningBeep);
                if (enablePulseAnimation)
                {
                    pulseCoroutine = StartCoroutine(PulseAnimation());
                }
                break;
            case "Danger":
                phaseText.text = "🔶 DANGER 🔶";
                PlayTimerSound(dangerBeep);
                if (enablePulseAnimation)
                {
                    pulseCoroutine = StartCoroutine(PulseAnimation());
                }
                if (enableShakeAnimation)
                {
                    shakeCoroutine = StartCoroutine(ShakeAnimation());
                }
                break;
            case "Penalty":
                phaseText.text = "💀 TAKING DAMAGE 💀";
                PlayTimerSound(penaltyAlarm);
                if (enablePulseAnimation)
                {
                    pulseCoroutine = StartCoroutine(PulseAnimation());
                }
                if (enableShakeAnimation)
                {
                    shakeCoroutine = StartCoroutine(ShakeAnimation());
                }
                break;
        }

        if (debugMode)
        {
            Debug.Log($"BallHoldTimerUI: Phase changed to {newPhase}");
        }
    }

    void UpdateColorsForPhase(string phase, float progress)
    {
        Color targetColor = phase switch
        {
            "Warning" => warningColor,
            "Danger" => dangerColor,
            "Penalty" => penaltyColor,
            _ => normalColor
        };

        if (timerFillImage != null)
        {
            timerFillImage.color = targetColor;
        }

        if (backgroundImage != null)
        {
            Color bgColor = targetColor;
            bgColor.a = 0.3f;
            backgroundImage.color = bgColor;
        }
    }

    IEnumerator PulseAnimation()
    {
        Vector3 originalScale = timerPanel.transform.localScale;

        while (isTimerActive)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.1f;
            timerPanel.transform.localScale = originalScale * pulse;
            yield return null;
        }

        timerPanel.transform.localScale = originalScale;
    }

    IEnumerator ShakeAnimation()
    {
        while (isTimerActive && (currentPhase == "Danger" || currentPhase == "Penalty"))
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-shakeIntensity, shakeIntensity),
                Random.Range(-shakeIntensity, shakeIntensity),
                0
            );

            timerRectTransform.localPosition = originalPosition + randomOffset;
            yield return new WaitForSeconds(0.05f);
        }

        timerRectTransform.localPosition = originalPosition;
    }

    void PlayTimerSound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API METHODS (Called by BallController)
    // ═══════════════════════════════════════════════════════════════

    public void ShowTimer(BallController ball)
    {
        currentBall = ball;
        isTimerActive = true;
        currentPhase = "Normal";

        if (timerPanel != null)
        {
            timerPanel.SetActive(true);
        }

        if (debugMode)
        {
            Debug.Log("BallHoldTimerUI: Timer shown");
        }
    }

    public void ShowWarning(BallController ball)
    {
        if (currentBall == ball && isTimerActive)
        {
            // Warning phase logic is handled in UpdateTimerDisplay
            if (debugMode)
            {
                Debug.Log("BallHoldTimerUI: Warning phase activated");
            }
        }
    }

    public void ShowDanger(BallController ball)
    {
        if (currentBall == ball && isTimerActive)
        {
            // Danger phase logic is handled in UpdateTimerDisplay
            if (debugMode)
            {
                Debug.Log("BallHoldTimerUI: Danger phase activated");
            }
        }
    }

    public void ShowPenalty(BallController ball)
    {
        if (currentBall == ball && isTimerActive)
        {
            // Penalty phase logic is handled in UpdateTimerDisplay
            if (debugMode)
            {
                Debug.Log("BallHoldTimerUI: Penalty phase activated");
            }
        }
    }

    public void HideTimer()
    {
        isTimerActive = false;
        currentBall = null;
        currentPhase = "Normal";

        if (timerPanel != null)
        {
            timerPanel.SetActive(false);
        }

        // Stop animations
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        // Reset transforms
        if (timerPanel != null)
        {
            timerPanel.transform.localScale = Vector3.one;
        }
        if (timerRectTransform != null)
        {
            timerRectTransform.localPosition = originalPosition;
        }

        if (debugMode)
        {
            Debug.Log("BallHoldTimerUI: Timer hidden");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CONFIGURATION METHODS
    // ═══════════════════════════════════════════════════════════════

    public void SetTimerPosition(Vector3 screenPosition)
    {
        if (timerRectTransform != null)
        {
            timerRectTransform.position = screenPosition;
        }
    }

    public void SetFollowPlayer(bool follow)
    {
        followPlayer = follow;
    }

    public void SetOffset(Vector3 offset)
    {
        offsetFromPlayer = offset;
    }

    public void SetPulseEnabled(bool enabled)
    {
        enablePulseAnimation = enabled;
    }

    public void SetShakeEnabled(bool enabled)
    {
        enableShakeAnimation = enabled;
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    public bool IsTimerActive() => isTimerActive;
    public string GetCurrentPhase() => currentPhase;
    public BallController GetCurrentBall() => currentBall;

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}