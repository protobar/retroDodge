using UnityEngine;
using Photon.Pun;

/// <summary>
/// FIXED: CatchSystem with network multiplayer support and PlayerCharacter-only compatibility
/// REMOVED: All legacy CharacterController support
/// ADDED: Proper PUN2 network synchronization for catching
/// </summary>
public class CatchSystem : MonoBehaviourPunCallbacks
{
    [Header("Catch Settings")]
    [SerializeField] private float catchRange = 2.5f;
    [SerializeField] private float perfectCatchWindow = 0.8f;
    [SerializeField] private float goodCatchWindow = 1.2f;
    [SerializeField] private float catchCooldown = 0.5f;
    [SerializeField] private bool debugMode = true;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject catchIndicator;
    [SerializeField] private bool alwaysShowCatchRange = false;
    [SerializeField] private Color perfectZoneColor = Color.green;
    [SerializeField] private Color goodZoneColor = Color.yellow;
    [SerializeField] private Color missZoneColor = Color.red;
    [SerializeField] private Color idleZoneColor = Color.gray;
    [SerializeField] private float idleIndicatorAlpha = 0.3f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip catchSuccessSound;
    [SerializeField] private AudioClip catchFailSound;
    [SerializeField] private AudioClip ballIncomingSound;

    [Header("Network Settings")]
    [SerializeField] private bool enableNetworkSync = true;
    [SerializeField] private float networkSyncRate = 20f;

    // Catch state
    private bool isCatchingAvailable = true;
    private float lastCatchAttempt = 0f;
    private BallController nearestThrownBall = null;
    private float ballDetectionTime = 0f;
    private float lastNetworkSync = 0f;

    // References - CLEANED UP: Only PlayerCharacter support
    private PlayerCharacter playerCharacter;
    private PlayerInputHandler inputHandler;
    private SphereCollider catchTrigger;

    // Visual effects
    private LineRenderer catchRangeIndicator;

    // Network sync variables
    private bool networkIsCatchingAvailable;
    private bool networkIsBallInRange;
    private float networkBallDetectionTime;

    public enum CatchResult { Perfect, Good, Miss, TooEarly, TooLate }

    void Awake()
    {
        // FIXED: Only support PlayerCharacter system
        playerCharacter = GetComponent<PlayerCharacter>();

        if (playerCharacter == null)
        {
            Debug.LogError($"CatchSystem on {gameObject.name}: PlayerCharacter component is required!");
            enabled = false;
            return;
        }

        SetupCatchTrigger();
        SetupVisualIndicators();
        SetupAudio();

        if (debugMode)
        {
            Debug.Log($"CatchSystem initialized for PlayerCharacter: {gameObject.name}");
        }
    }

    void Start()
    {
        // FIXED: Get input handler in Start() to handle initialization order
        if (playerCharacter != null)
        {
            inputHandler = playerCharacter.GetInputHandler();

            if (inputHandler == null)
            {
                // Try to get it directly from the GameObject as fallback
                inputHandler = GetComponent<PlayerInputHandler>();

                if (inputHandler == null)
                {
                    Debug.LogError($"CatchSystem on {gameObject.name}: PlayerInputHandler not found! Make sure the player prefab has PlayerInputHandler component.");
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"CatchSystem found PlayerInputHandler directly on GameObject");
                }
            }
            else
            {
                if (debugMode)
                    Debug.Log($"CatchSystem found PlayerInputHandler via PlayerCharacter");
            }
        }
    }

    void SetupCatchTrigger()
    {
        // Create a trigger collider for catch detection
        GameObject triggerObj = new GameObject("CatchTrigger");
        triggerObj.transform.SetParent(transform);
        triggerObj.transform.localPosition = Vector3.zero;

        catchTrigger = triggerObj.AddComponent<SphereCollider>();
        catchTrigger.isTrigger = true;
        catchTrigger.radius = catchRange;

        // Add CatchTriggerHandler
        CatchTriggerHandler handler = triggerObj.AddComponent<CatchTriggerHandler>();
        handler.Initialize(this);

        if (debugMode)
        {
            Debug.Log($"Catch trigger created with radius: {catchRange}");
        }
    }

    void SetupVisualIndicators()
    {
        // Create catch range indicator
        if (catchIndicator == null)
        {
            catchIndicator = new GameObject("CatchIndicator");
            catchIndicator.transform.SetParent(transform);
            catchIndicator.transform.localPosition = Vector3.zero;

            // Create a simple circle to show catch range
            catchRangeIndicator = catchIndicator.AddComponent<LineRenderer>();
            catchRangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
            catchRangeIndicator.startColor = goodZoneColor;
            catchRangeIndicator.endColor = goodZoneColor;
            catchRangeIndicator.startWidth = 0.1f;
            catchRangeIndicator.endWidth = 0.1f;
            catchRangeIndicator.useWorldSpace = false;

            CreateCircle(catchRangeIndicator, catchRange, 32);
        }

        // Set initial visibility based on setting
        SetCatchIndicatorVisible(alwaysShowCatchRange);

        if (alwaysShowCatchRange)
        {
            SetIdleIndicatorAppearance();
        }
    }

    void SetupAudio()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }
    }

    void CreateCircle(LineRenderer lr, float radius, int segments)
    {
        lr.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, 0, z));
        }
    }

    void Update()
    {
        // FIXED: Only process input and logic for local players
        PhotonView myPhotonView = GetComponent<PhotonView>();
        bool isMyPlayer = myPhotonView == null || myPhotonView.IsMine;

        if (isMyPlayer)
        {
            // Local player - process full logic
            CheckForThrownBallsInRange();
            HandleCatchInput();
            UpdateCatchCooldown();

            // Send network updates
            if (enableNetworkSync && Time.time - lastNetworkSync >= 1f / networkSyncRate)
            {
                SendNetworkUpdate();
                lastNetworkSync = Time.time;
            }
        }
        else
        {
            // Remote player - apply network state
            ApplyNetworkState();
        }

        // All players update visual indicators
        UpdateCatchIndicator();

        // Debug controls (local only)
        if (isMyPlayer && debugMode)
        {
            HandleDebugInput();
        }
    }

    void SendNetworkUpdate()
    {
        if (photonView != null && photonView.IsMine)
        {
            // Send catch state updates via RPC if there are significant changes
            bool stateChanged = (networkIsCatchingAvailable != isCatchingAvailable) ||
                               (networkIsBallInRange != (nearestThrownBall != null)) ||
                               Mathf.Abs(networkBallDetectionTime - ballDetectionTime) > 0.1f;

            if (stateChanged)
            {
                photonView.RPC("SyncCatchState", RpcTarget.Others,
                              isCatchingAvailable,
                              nearestThrownBall != null,
                              ballDetectionTime);
            }
        }
    }

    [PunRPC]
    void SyncCatchState(bool isAvailable, bool ballInRange, float detectionTime)
    {
        // Apply network state from remote player
        networkIsCatchingAvailable = isAvailable;
        networkIsBallInRange = ballInRange;
        networkBallDetectionTime = detectionTime;
    }

    void ApplyNetworkState()
    {
        // Apply network state to remote player
        isCatchingAvailable = networkIsCatchingAvailable;
        ballDetectionTime = networkBallDetectionTime;

        // Update ball reference based on network state
        if (networkIsBallInRange && nearestThrownBall == null)
        {
            // Try to find the ball that should be in range
            FindNearestThrownBall();
        }
        else if (!networkIsBallInRange)
        {
            nearestThrownBall = null;
        }
    }

    void CheckForThrownBallsInRange()
    {
        BallController previousBall = nearestThrownBall;
        nearestThrownBall = null;
        float closestDistance = catchRange;

        // Find all ball controllers in the scene
        BallController[] allBalls = FindObjectsOfType<BallController>();

        foreach (BallController ball in allBalls)
        {
            // Only consider thrown balls
            if (ball.GetBallState() != BallController.BallState.Thrown)
                continue;

            float distance = Vector3.Distance(transform.position, ball.transform.position);

            if (distance <= catchRange && distance < closestDistance)
            {
                nearestThrownBall = ball;
                closestDistance = distance;
            }
        }

        // If we found a new ball, record the time
        if (nearestThrownBall != null && nearestThrownBall != previousBall)
        {
            ballDetectionTime = Time.time;
            PlaySound(ballIncomingSound);

            if (debugMode)
            {
                Debug.Log($"Detected thrown ball in range: {nearestThrownBall.name}, Distance: {closestDistance:F2}");
            }
        }

        // If we lost the ball, clear detection time
        if (nearestThrownBall == null && previousBall != null)
        {
            if (debugMode)
            {
                Debug.Log("Lost thrown ball from range");
            }
        }
    }

    void FindNearestThrownBall()
    {
        nearestThrownBall = null;
        float closestDistance = catchRange;

        BallController[] allBalls = FindObjectsOfType<BallController>();
        foreach (BallController ball in allBalls)
        {
            if (ball.GetBallState() != BallController.BallState.Thrown)
                continue;

            float distance = Vector3.Distance(transform.position, ball.transform.position);
            if (distance <= catchRange && distance < closestDistance)
            {
                nearestThrownBall = ball;
                closestDistance = distance;
            }
        }
    }

    void HandleCatchInput()
    {
        // FIXED: Try to get input handler if we don't have one yet
        if (inputHandler == null && playerCharacter != null)
        {
            inputHandler = playerCharacter.GetInputHandler();

            // Fallback: try direct component lookup
            if (inputHandler == null)
            {
                inputHandler = GetComponent<PlayerInputHandler>();
            }
        }

        // FIXED: Use PlayerCharacter's input handler with fallback
        if (inputHandler == null)
        {
            // Don't spam the console - only log occasionally
            if (Time.frameCount % 120 == 0 && debugMode) // Every 2 seconds at 60fps
            {
                Debug.LogWarning($"CatchSystem on {gameObject.name}: PlayerInputHandler still not found! Make sure your player prefab has PlayerInputHandler component.");
            }
            return;
        }

        // Check for catch input
        if (inputHandler.GetCatchPressed() && isCatchingAvailable)
        {
            AttemptCatch();
        }
    }

    void HandleDebugInput()
    {
        // Debug key to force show catch indicator
        if (Input.GetKeyDown(KeyCode.G))
        {
            bool currentState = catchIndicator != null ? catchIndicator.activeSelf : false;
            SetCatchIndicatorVisible(!currentState);
            Debug.Log($"Force toggled catch indicator: {!currentState}");
        }

        // Debug key to test trigger manually + keep indicator on
        if (Input.GetKeyDown(KeyCode.H))
        {
            BallController ball = BallManager.Instance?.GetCurrentBall();
            if (ball != null)
            {
                OnBallEnterRange(ball);
                Debug.Log("Manually triggered ball enter range!");
                SetCatchIndicatorVisible(true);
            }
        }

        // Debug key to force clear ball range (stop indicator)
        if (Input.GetKeyDown(KeyCode.N))
        {
            nearestThrownBall = null;
            SetCatchIndicatorVisible(false);
            Debug.Log("Force cleared ball range!");
        }
    }

    void UpdateCatchIndicator()
    {
        if (catchRangeIndicator == null) return;

        if (alwaysShowCatchRange)
        {
            // Always visible mode
            if (!catchIndicator.activeSelf)
            {
                SetCatchIndicatorVisible(true);
            }

            if (nearestThrownBall != null)
            {
                // Ball approaching - show active timing colors
                float ballRangeTime = Time.time - ballDetectionTime;
                CatchResult timing = GetCatchTiming(ballRangeTime);
                Color indicatorColor = GetTimingColor(timing);

                catchRangeIndicator.startColor = indicatorColor;
                catchRangeIndicator.endColor = indicatorColor;

                // Pulse effect for perfect timing
                if (timing == CatchResult.Perfect)
                {
                    float pulse = Mathf.Sin(Time.time * 10f) * 0.3f + 0.7f;
                    Color pulseColor = Color.Lerp(indicatorColor, Color.white, pulse);
                    catchRangeIndicator.startColor = pulseColor;
                    catchRangeIndicator.endColor = pulseColor;
                }
            }
            else
            {
                // No ball - show idle appearance
                SetIdleIndicatorAppearance();
            }
        }
        else
        {
            // Original behavior - only show when ball approaching
            if (nearestThrownBall == null)
            {
                if (catchIndicator != null && catchIndicator.activeSelf)
                {
                    SetCatchIndicatorVisible(false);
                }
                return;
            }

            // Show indicator when ball is in range
            if (catchIndicator != null && !catchIndicator.activeSelf)
            {
                SetCatchIndicatorVisible(true);
            }

            // Calculate timing and update indicator color
            float ballRangeTime = Time.time - ballDetectionTime;
            CatchResult timing = GetCatchTiming(ballRangeTime);
            Color indicatorColor = GetTimingColor(timing);

            catchRangeIndicator.startColor = indicatorColor;
            catchRangeIndicator.endColor = indicatorColor;

            // Pulse effect for perfect timing
            if (timing == CatchResult.Perfect)
            {
                float pulse = Mathf.Sin(Time.time * 10f) * 0.3f + 0.7f;
                Color pulseColor = Color.Lerp(indicatorColor, Color.white, pulse);
                catchRangeIndicator.startColor = pulseColor;
                catchRangeIndicator.endColor = pulseColor;
            }
        }
    }

    void UpdateCatchCooldown()
    {
        if (!isCatchingAvailable && Time.time - lastCatchAttempt >= catchCooldown)
        {
            isCatchingAvailable = true;
        }
    }

    void AttemptCatch()
    {
        lastCatchAttempt = Time.time;
        isCatchingAvailable = false;

        if (debugMode)
        {
            Debug.Log($"🟢 === CATCH ATTEMPT === {playerCharacter.name}");
            Debug.Log($"Ball in range: {nearestThrownBall != null}");
            if (nearestThrownBall != null)
            {
                float debugBallRangeTime = Time.time - ballDetectionTime;
                Debug.Log($"Time since ball detected: {debugBallRangeTime:F2}");
                Debug.Log($"Ball distance: {Vector3.Distance(transform.position, nearestThrownBall.transform.position):F2}");
            }
        }

        if (nearestThrownBall == null)
        {
            // Missed - no ball in range
            OnCatchResult(CatchResult.Miss, null);
            return;
        }

        // Double-check the ball is still thrown and in range
        if (nearestThrownBall.GetBallState() != BallController.BallState.Thrown)
        {
            if (debugMode)
            {
                Debug.Log($"Ball is no longer thrown! Current state: {nearestThrownBall.GetBallState()}");
            }
            OnCatchResult(CatchResult.Miss, null);
            return;
        }

        float currentDistance = Vector3.Distance(transform.position, nearestThrownBall.transform.position);
        if (currentDistance > catchRange)
        {
            if (debugMode)
            {
                Debug.Log($"Ball is too far away! Distance: {currentDistance:F2}, Range: {catchRange}");
            }
            OnCatchResult(CatchResult.Miss, null);
            return;
        }

        // Calculate catch timing
        float ballRangeTime = Time.time - ballDetectionTime;
        CatchResult result = GetCatchTiming(ballRangeTime);

        if (debugMode)
        {
            Debug.Log($"Catch timing result: {result}");
        }

        if (result == CatchResult.Perfect || result == CatchResult.Good)
        {
            // FIXED: Network sync successful catch
            ExecuteSuccessfulCatch(nearestThrownBall, result);
        }
        else
        {
            // Failed catch
            OnCatchResult(result, nearestThrownBall);
        }
    }

    CatchResult GetCatchTiming(float timeSinceBallInRange)
    {
        // Simplified timing - much more forgiving
        // Ball is always catchable while in range during first 2 seconds

        if (timeSinceBallInRange <= 2f)
        {
            if (timeSinceBallInRange <= 1f)
                return CatchResult.Perfect;
            else
                return CatchResult.Good;
        }
        else
        {
            return CatchResult.TooLate;
        }
    }

    Color GetTimingColor(CatchResult timing)
    {
        switch (timing)
        {
            case CatchResult.Perfect:
                return perfectZoneColor;
            case CatchResult.Good:
                return goodZoneColor;
            case CatchResult.TooEarly:
            case CatchResult.TooLate:
            case CatchResult.Miss:
                return missZoneColor;
            default:
                return goodZoneColor;
        }
    }

    void OnCatchResult(CatchResult result, BallController ball)
    {
        switch (result)
        {
            case CatchResult.Perfect:
            case CatchResult.Good:
                // Success is handled in ExecuteSuccessfulCatch
                break;
            case CatchResult.Miss:
            case CatchResult.TooEarly:
            case CatchResult.TooLate:
                ExecuteFailedCatch(ball, result);
                break;
        }

        Debug.Log($"Catch attempt: {result}");
    }

    /// <summary>
    /// FIXED: Execute successful catch with network synchronization
    /// </summary>
    void ExecuteSuccessfulCatch(BallController ball, CatchResult result)
    {
        if (ball == null || playerCharacter == null) return;

        Debug.Log($"✅ === SUCCESSFUL CATCH === {playerCharacter.name}");

        // FIXED: Use PhotonView to sync catch across network
        PhotonView ballPhotonView = ball.GetComponent<PhotonView>();
        PhotonView myPhotonView = GetComponent<PhotonView>();

        if (myPhotonView != null && myPhotonView.IsMine)
        {
            // Local player caught ball - sync to others
            if (ballPhotonView != null)
            {
                // Send RPC to sync catch
                photonView.RPC("SyncCatchSuccess", RpcTarget.All,
                              ballPhotonView.ViewID,
                              myPhotonView.ViewID,
                              (int)result);
            }
            else
            {
                // Local ball - execute directly
                ExecuteCatchLocally(ball, result);
            }
        }
        else
        {
            Debug.LogWarning($"Cannot execute catch - not my player");
        }
    }

    [PunRPC]
    void SyncCatchSuccess(int ballViewID, int catcherViewID, int catchResult)
    {
        // Find the ball and catcher by their PhotonView IDs
        PhotonView ballView = PhotonView.Find(ballViewID);
        PhotonView catcherView = PhotonView.Find(catcherViewID);

        if (ballView != null && catcherView != null)
        {
            BallController ball = ballView.GetComponent<BallController>();
            PlayerCharacter catcher = catcherView.GetComponent<PlayerCharacter>();

            if (ball != null && catcher != null)
            {
                // Execute catch on all clients
                ball.OnCaught(catcher);
                catcher.SetHasBall(true);

                // Clear ball reference on the catcher
                CatchSystem catcherCatchSystem = catcher.GetComponent<CatchSystem>();
                if (catcherCatchSystem != null)
                {
                    catcherCatchSystem.nearestThrownBall = null;
                }

                // Play success effects on all clients
                PlayCatchSuccessEffects((CatchResult)catchResult);

                if (debugMode)
                {
                    Debug.Log($"Network synced successful catch: {catcher.name} caught ball");
                }
            }
        }
    }

    void ExecuteCatchLocally(BallController ball, CatchResult result)
    {
        // Execute catch locally
        ball.OnCaught(playerCharacter);
        playerCharacter.SetHasBall(true);

        // Clear ball reference
        nearestThrownBall = null;

        // Play success effects
        PlayCatchSuccessEffects(result);

        Debug.Log($"Local successful catch! ({result})");
    }

    void PlayCatchSuccessEffects(CatchResult result)
    {
        // Play success sound
        PlaySound(catchSuccessSound);

        // Visual effect for successful catch
        CreateCatchEffect(result == CatchResult.Perfect);

        // Add ability charges for successful catch
        if (playerCharacter != null)
        {
            playerCharacter.OnSuccessfulCatch();
        }
    }

    void ExecuteFailedCatch(BallController ball, CatchResult result)
    {
        // Ball continues its trajectory or bounces
        if (ball != null)
        {
            ball.OnCatchFailed();
        }

        // Play fail sound
        PlaySound(catchFailSound);

        // Visual effect for failed catch
        CreateMissEffect();

        Debug.Log($"Failed catch! ({result})");
    }

    void CreateCatchEffect(bool isPerfect)
    {
        // Create a simple visual effect for successful catch
        // This could be enhanced with particles later
        if (isPerfect)
        {
            // Perfect catch gets special effect
            Debug.Log("PERFECT CATCH EFFECT!");
        }
        else
        {
            // Good catch gets normal effect
            Debug.Log("Good catch effect!");
        }
    }

    void CreateMissEffect()
    {
        // Visual effect for missed catch
        Debug.Log("Miss effect!");
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void SetCatchIndicatorVisible(bool visible)
    {
        if (catchIndicator != null)
        {
            catchIndicator.SetActive(visible);
            if (debugMode)
            {
                Debug.Log($"Setting catch indicator visible: {visible}");
            }
        }
        else if (debugMode)
        {
            Debug.Log("CatchIndicator is null - cannot set visibility!");
        }
    }

    void SetIdleIndicatorAppearance()
    {
        if (catchRangeIndicator != null)
        {
            Color idleColor = idleZoneColor;
            idleColor.a = idleIndicatorAlpha;
            catchRangeIndicator.startColor = idleColor;
            catchRangeIndicator.endColor = idleColor;
        }
    }

    // Called by trigger handler (kept for compatibility but now secondary)
    public void OnBallEnterRange(BallController ball)
    {
        if (debugMode)
        {
            Debug.Log($"Trigger detected ball: {ball.name}, State: {ball.GetBallState()}");
        }
    }

    public void OnBallExitRange(BallController ball)
    {
        if (debugMode)
        {
            Debug.Log($"Trigger lost ball: {ball.name}");
        }
    }

    // Public getters
    public bool IsCatchingAvailable() => isCatchingAvailable;
    public bool IsBallInRange() => nearestThrownBall != null;
    public float GetCatchRange() => catchRange;
    public BallController GetNearestThrownBall() => nearestThrownBall;
    public PlayerCharacter GetPlayerCharacter() => playerCharacter;

    // Network sync methods
    public void SetNetworkSyncEnabled(bool enabled)
    {
        enableNetworkSync = enabled;
    }

    public bool IsNetworkSyncEnabled() => enableNetworkSync;

    // ═══════════════════════════════════════════════════════════════
    // LEGACY SUPPORT REMOVED
    // All CharacterController methods have been removed to clean up the code
    // ═══════════════════════════════════════════════════════════════
}

/// <summary>
/// Helper component for trigger detection - UNCHANGED
/// </summary>
public class CatchTriggerHandler : MonoBehaviour
{
    private CatchSystem catchSystem;

    public void Initialize(CatchSystem system)
    {
        catchSystem = system;
        Debug.Log("CatchTriggerHandler initialized!");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger Enter: {other.name} - Has BallController: {other.GetComponent<BallController>() != null}");

        BallController ball = other.GetComponent<BallController>();
        if (ball != null && catchSystem != null)
        {
            Debug.Log($"Ball entering catch range! Ball state: {ball.GetBallState()}");
            catchSystem.OnBallEnterRange(ball);
        }
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log($"Trigger Exit: {other.name}");

        BallController ball = other.GetComponent<BallController>();
        if (ball != null && catchSystem != null)
        {
            catchSystem.OnBallExitRange(ball);
        }
    }
}