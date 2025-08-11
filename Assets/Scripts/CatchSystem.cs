using UnityEngine;

public class CatchSystem : MonoBehaviour
{
    [Header("Catch Settings")]
    [SerializeField] private float catchRange = 2.5f;
    [SerializeField] private float perfectCatchWindow = 0.8f;
    [SerializeField] private float goodCatchWindow = 1.2f;
    [SerializeField] private float catchCooldown = 0.5f;
    [SerializeField] private bool debugMode = true;

    [Header("Input")]
    [SerializeField] private KeyCode catchKey = KeyCode.L;

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

    // Catch state
    private bool isCatchingAvailable = true;
    private float lastCatchAttempt = 0f;
    private BallController nearestThrownBall = null;
    private float ballDetectionTime = 0f;

    // References
    private CharacterController character;
    private SphereCollider catchTrigger;

    // Visual effects
    private LineRenderer catchRangeIndicator;

    public enum CatchResult { Perfect, Good, Miss, TooEarly, TooLate }

    // Replace the Awake method in CatchSystem.cs

    void Awake()
    {
        // Try to get CharacterController first (legacy system)
        character = GetComponent<CharacterController>();

        // If no CharacterController, we're probably using the new PlayerCharacter system
        if (character == null)
        {
            if (debugMode)
            {
                Debug.Log($"CatchSystem on {gameObject.name}: No CharacterController found, assuming new PlayerCharacter system");
            }
        }

        SetupCatchTrigger();
        SetupVisualIndicators();
        SetupAudio();
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
        // Continuously check for thrown balls in range
        CheckForThrownBallsInRange();

        HandleCatchInput();
        UpdateCatchIndicator();
        UpdateCatchCooldown();

        // Debug key to force show catch indicator
        if (debugMode && Input.GetKeyDown(KeyCode.G))
        {
            bool currentState = catchIndicator != null ? catchIndicator.activeSelf : false;
            SetCatchIndicatorVisible(!currentState);
            Debug.Log($"Force toggled catch indicator: {!currentState}");
        }

        // Debug key to test trigger manually + keep indicator on
        if (debugMode && Input.GetKeyDown(KeyCode.H))
        {
            BallController ball = BallManager.Instance?.GetCurrentBall();
            if (ball != null)
            {
                OnBallEnterRange(ball);
                Debug.Log("Manually triggered ball enter range!");

                // Force keep indicator visible for testing
                SetCatchIndicatorVisible(true);
            }
        }

        // Debug key to force clear ball range (stop indicator)
        if (debugMode && Input.GetKeyDown(KeyCode.N))
        {
            nearestThrownBall = null;
            SetCatchIndicatorVisible(false);
            Debug.Log("Force cleared ball range!");
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

    // In CatchSystem.cs, replace Input.GetKeyDown(catchKey) with:
    // Replace the HandleCatchInput method in CatchSystem.cs (around line 216)

    void HandleCatchInput()
    {
        PlayerInputHandler inputHandler = null;

        // Try to get input handler from both character systems
        if (character != null)
        {
            // Legacy CharacterController system
            inputHandler = character.GetInputHandler();
        }
        else
        {
            // New PlayerCharacter system - look for PlayerCharacter component
            PlayerCharacter playerCharacter = GetComponent<PlayerCharacter>();
            if (playerCharacter != null)
            {
                inputHandler = playerCharacter.GetInputHandler();
            }
        }

        // Fallback: try to get PlayerInputHandler directly from this GameObject
        if (inputHandler == null)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        // Check for catch input
        if (inputHandler != null && inputHandler.GetCatchPressed() && isCatchingAvailable)
        {
            AttemptCatch();
        }
        else if (inputHandler == null && debugMode)
        {
            // Only log this once to avoid spam
            if (Time.frameCount % 60 == 0) // Log once per second at 60fps
            {
                Debug.LogWarning($"CatchSystem on {gameObject.name}: No PlayerInputHandler found! Make sure your player has either CharacterController with GetInputHandler() or PlayerCharacter component.");
            }
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
            Debug.Log($"Catch attempt! Ball in range: {nearestThrownBall != null}");
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
            // Successful catch
            OnCatchResult(result, nearestThrownBall);
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
                ExecuteSuccessfulCatch(ball, result);
                break;
            case CatchResult.Miss:
            case CatchResult.TooEarly:
            case CatchResult.TooLate:
                ExecuteFailedCatch(ball, result);
                break;
        }

        Debug.Log($"Catch attempt: {result}");
    }

    // Replace the ExecuteSuccessfulCatch method in CatchSystem.cs (around line 476)

    void ExecuteSuccessfulCatch(BallController ball, CatchResult result)
    {
        if (ball != null)
        {
            // Determine which character system we're using and call appropriate method
            PlayerCharacter playerCharacter = GetComponent<PlayerCharacter>();

            if (playerCharacter != null)
            {
                // New character system - use PlayerCharacter
                ball.OnCaught(playerCharacter);
                playerCharacter.SetHasBall(true);
            }
            else if (character != null)
            {
                // Legacy character system - use CharacterController
                ball.OnCaught(character);
                character.SetHasBall(true);
            }
            else
            {
                Debug.LogError($"CatchSystem on {gameObject.name}: No valid character component found for catch!");
                return;
            }

            // Clear ball reference
            nearestThrownBall = null;
        }

        // Play success sound
        PlaySound(catchSuccessSound);

        // Visual effect for successful catch
        CreateCatchEffect(result == CatchResult.Perfect);

        Debug.Log($"Successful catch! ({result})");
    }

    /// <summary>
    /// Helper method to determine which character system we're using
    /// </summary>
    private bool IsUsingPlayerCharacterSystem()
    {
        return GetComponent<PlayerCharacter>() != null;
    }

    /// <summary>
    /// Get the appropriate character component for ball operations
    /// </summary>
    private void SetHasBallOnCharacter(bool hasBall)
    {
        PlayerCharacter playerCharacter = GetComponent<PlayerCharacter>();

        if (playerCharacter != null)
        {
            // New character system
            playerCharacter.SetHasBall(hasBall);
        }
        else if (character != null)
        {
            // Legacy character system
            character.SetHasBall(hasBall);
        }
        else
        {
            Debug.LogError($"CatchSystem on {gameObject.name}: No valid character component found!");
        }
    }

    // Also update the ExecuteFailedCatch method to be consistent:
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
}

// Helper component for trigger detection
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