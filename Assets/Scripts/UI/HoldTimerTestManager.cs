using UnityEngine;

/// <summary>
/// Test Manager for Ball Hold Timer System
/// Provides easy testing controls and configuration
/// </summary>
public class HoldTimerTestManager : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool enableTestMode = true;
    [SerializeField] private float testMaxHoldTime = 5f;
    [SerializeField] private float testWarningTime = 3f;
    [SerializeField] private float testDangerTime = 4f;
    [SerializeField] private float testDamagePerSecond = 2f;

    [Header("Test Controls")]
    [SerializeField] private KeyCode forceBallPickupKey = KeyCode.P;
    [SerializeField] private KeyCode forceThrowBallKey = KeyCode.Space;
    [SerializeField] private KeyCode skipToWarningKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode skipToDangerKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode skipToPenaltyKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode resetTimerKey = KeyCode.Alpha0;
    [SerializeField] private KeyCode toggleUIKey = KeyCode.U;

    [Header("Debug")]
    [SerializeField] private bool showTestInstructions = true;
    [SerializeField] private bool showTimerDebugInfo = true;

    // References
    private BallManager ballManager;
    private BallController currentBall;
    private PlayerCharacter testPlayer;
    private CharacterController legacyTestPlayer;

    void Start()
    {
        if (!enableTestMode) return;

        ballManager = BallManager.Instance;

        testPlayer = FindObjectOfType<PlayerCharacter>();
        if (testPlayer == null)
        {
            legacyTestPlayer = FindObjectOfType<CharacterController>();
        }

        if (ballManager == null)
        {
            Debug.LogError("HoldTimerTestManager: No BallManager found! Please ensure BallManager exists in scene.");
            return;
        }

        Debug.Log("Hold Timer Test Manager initialized! Press H for help.");
    }

    void Update()
    {
        if (!enableTestMode) return;

        HandleTestInput();
        UpdateCurrentBallReference();
    }

    void UpdateCurrentBallReference()
    {
        if (ballManager != null)
        {
            currentBall = ballManager.GetCurrentBall();
        }
    }

    void HandleTestInput()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            ShowTestInstructions();
        }

        if (Input.GetKeyDown(forceBallPickupKey))
        {
            ForceBallPickup();
        }

        if (Input.GetKeyDown(forceThrowBallKey))
        {
            ForceThrowBall();
        }

        if (Input.GetKeyDown(skipToWarningKey))
        {
            SkipToPhase("Warning");
        }

        if (Input.GetKeyDown(skipToDangerKey))
        {
            SkipToPhase("Danger");
        }

        if (Input.GetKeyDown(skipToPenaltyKey))
        {
            SkipToPhase("Penalty");
        }

        if (Input.GetKeyDown(resetTimerKey))
        {
            ResetHoldTimer();
        }

        if (Input.GetKeyDown(toggleUIKey))
        {
            ToggleHoldTimerUI();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ApplyTestConfiguration();
        }
    }

    void ForceBallPickup()
    {
        if (currentBall == null || ballManager == null)
        {
            Debug.LogWarning("No ball available for pickup test!");
            return;
        }

        if (currentBall.IsHeld())
        {
            Debug.LogWarning("Ball is already held!");
            return;
        }

        if (testPlayer != null)
        {
            ballManager.RequestBallPickup(testPlayer);
            Debug.Log($"Forced ball pickup for {testPlayer.name}");
        }
        else if (legacyTestPlayer != null)
        {
            ballManager.RequestBallPickup(legacyTestPlayer);
            Debug.Log($"Forced ball pickup for {legacyTestPlayer.name}");
        }
        else
        {
            Debug.LogWarning("No player found for ball pickup test!");
        }
    }

    void ForceThrowBall()
    {
        if (currentBall == null || !currentBall.IsHeld())
        {
            Debug.LogWarning("No ball is currently held!");
            return;
        }

        if (testPlayer != null && currentBall.GetHolder() == testPlayer)
        {
            ballManager.RequestBallThrowSimple(testPlayer, 1f);
            Debug.Log($"Forced ball throw for {testPlayer.name}");
        }
        else if (legacyTestPlayer != null && currentBall.GetHolderLegacy() == legacyTestPlayer)
        {
            ballManager.RequestBallThrowSimple(legacyTestPlayer, 1f);
            Debug.Log($"Forced ball throw for {legacyTestPlayer.name}");
        }
        else
        {
            Debug.LogWarning("Ball holder mismatch or no holder found!");
        }
    }

    void SkipToPhase(string phaseName)
    {
        if (currentBall == null || !currentBall.IsHeld())
        {
            Debug.LogWarning($"Cannot skip to {phaseName} phase - ball is not held!");
            return;
        }

        float currentTime = Time.time;
        float targetHoldTime = phaseName switch
        {
            "Warning" => testWarningTime + 0.1f,
            "Danger" => testDangerTime + 0.1f,
            "Penalty" => testMaxHoldTime + 0.1f,
            _ => 0f
        };

        var ballHoldStartTimeField = typeof(BallController).GetField("ballHoldStartTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (ballHoldStartTimeField != null)
        {
            float newStartTime = currentTime - targetHoldTime;
            ballHoldStartTimeField.SetValue(currentBall, newStartTime);
            Debug.Log($"⏩ Skipped to {phaseName} phase! Hold duration: {currentBall.GetHoldDuration():F1}s");
        }
        else
        {
            Debug.LogError("Could not access ballHoldStartTime field for phase skipping!");
        }
    }

    void ResetHoldTimer()
    {
        if (currentBall == null)
        {
            Debug.LogWarning("No ball to reset timer for!");
            return;
        }

        if (currentBall.IsHeld())
        {
            ForceThrowBall();
            StartCoroutine(ResetTimerCoroutine());
        }
        else
        {
            Debug.LogWarning("Ball is not held - no timer to reset!");
        }
    }

    System.Collections.IEnumerator ResetTimerCoroutine()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);
        ForceBallPickup();
        Debug.Log("🔄 Hold timer reset!");
    }

    void ToggleHoldTimerUI()
    {
        if (ballManager != null)
        {
            if (ballManager.IsTimerUIShown())
            {
                ballManager.ForceHideHoldTimerUI();
                Debug.Log("Hold timer UI hidden");
            }
            else
            {
                ballManager.ForceShowHoldTimerUI();
                Debug.Log("Hold timer UI shown");
            }
        }
    }

    void ApplyTestConfiguration()
    {
        if (currentBall == null)
        {
            Debug.LogWarning("No ball to apply test configuration to!");
            return;
        }

        var maxHoldTimeField = typeof(BallController).GetField("maxHoldTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var warningStartTimeField = typeof(BallController).GetField("warningStartTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dangerStartTimeField = typeof(BallController).GetField("dangerStartTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var holdDamagePerSecondField = typeof(BallController).GetField("holdDamagePerSecond",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (maxHoldTimeField != null) maxHoldTimeField.SetValue(currentBall, testMaxHoldTime);
        if (warningStartTimeField != null) warningStartTimeField.SetValue(currentBall, testWarningTime);
        if (dangerStartTimeField != null) dangerStartTimeField.SetValue(currentBall, testDangerTime);
        if (holdDamagePerSecondField != null) holdDamagePerSecondField.SetValue(currentBall, testDamagePerSecond);

        Debug.Log($"✅ Applied test config: MaxHold={testMaxHoldTime}s, Warning={testWarningTime}s, Danger={testDangerTime}s, DPS={testDamagePerSecond}");
    }

    void ShowTestInstructions()
    {
        if (!showTestInstructions) return;

        Debug.Log(
@"--- Hold Timer Test Controls ---
P: Force Ball Pickup
Space: Force Ball Throw
1: Skip to Warning Phase
2: Skip to Danger Phase
3: Skip to Penalty Phase
0: Reset Timer
U: Toggle Hold Timer UI
C: Apply Test Configuration
H: Show this Help Menu");
    }
}
