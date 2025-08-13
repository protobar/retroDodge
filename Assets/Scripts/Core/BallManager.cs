using UnityEngine;

/// <summary>
/// Enhanced BallManager with Ball Hold Timer integration
/// Manages ball lifecycle and communicates with Hold Timer UI
/// </summary>
public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; }

    [Header("Ball Settings")]
    [SerializeField] public GameObject ballPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2f, 0);
    [SerializeField] private float respawnDelay = 2f;

    [Header("Ball Hold Timer Integration")]
    [SerializeField] private bool enableHoldTimerUI = true;
    [SerializeField] private bool autoShowTimerOnPickup = true;
    [SerializeField] private bool autoHideTimerOnThrow = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Current ball reference
    private BallController currentBall;

    // Game state
    private bool gameActive = true;

    // Hold timer UI tracking
    private bool isTimerUIShown = false;

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        SpawnBall();
    }

    void Update()
    {
        // Monitor ball state for hold timer UI integration
        if (enableHoldTimerUI && currentBall != null)
        {
            MonitorBallHoldState();
        }

        // Debug controls
        if (debugMode)
        {
            HandleDebugInput();
        }
    }

    void MonitorBallHoldState()
    {
        bool ballIsHeld = currentBall.IsHeld();

        // Show timer UI when ball is picked up
        if (ballIsHeld && !isTimerUIShown && autoShowTimerOnPickup)
        {
            ShowHoldTimerUI();
        }
        // Hide timer UI when ball is thrown or becomes free
        else if (!ballIsHeld && isTimerUIShown && autoHideTimerOnThrow)
        {
            HideHoldTimerUI();
        }
    }

    void HandleDebugInput()
    {
        // Debug key to reset ball
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetBall();
        }

        // Debug key to respawn ball
        if (Input.GetKeyDown(KeyCode.T))
        {
            SpawnBall();
        }

        // Debug key to toggle hold timer UI
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (isTimerUIShown)
            {
                HideHoldTimerUI();
            }
            else if (currentBall != null && currentBall.IsHeld())
            {
                ShowHoldTimerUI();
            }
        }

        // Debug key to show character info
        if (Input.GetKeyDown(KeyCode.I))
        {
            ShowCharacterDebugInfo();
        }
    }

    void SpawnBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("Ball prefab not assigned in BallManager!");
            return;
        }

        // Destroy existing ball if any
        if (currentBall != null)
        {
            // Hide timer UI before destroying ball
            if (isTimerUIShown)
            {
                HideHoldTimerUI();
            }

            Destroy(currentBall.gameObject);
        }

        // Spawn new ball
        GameObject ballObj = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        currentBall = ballObj.GetComponent<BallController>();

        if (currentBall == null)
        {
            Debug.LogError("Ball prefab doesn't have BallController component!");
            return;
        }

        // Subscribe to ball events for hold timer integration
        SubscribeToBallEvents();

        if (debugMode)
        {
            Debug.Log("Ball spawned at center with hold timer integration!");
        }
    }

    void SubscribeToBallEvents()
    {
        // Note: In a real implementation, you might want to add events to BallController
        // For now, we'll use the MonitorBallHoldState method in Update()

        if (debugMode)
        {
            Debug.Log("Subscribed to ball events for hold timer integration");
        }
    }

    /// <summary>
    /// Request ball pickup - works with any character type
    /// Now includes hold timer UI integration
    /// </summary>
    public void RequestBallPickup(PlayerCharacter character)
    {
        if (currentBall != null && gameActive)
        {
            bool success = currentBall.TryPickup(character);
            if (success)
            {
                // Ball pickup successful - timer is automatically started in BallController
                if (enableHoldTimerUI && autoShowTimerOnPickup)
                {
                    ShowHoldTimerUI();
                }

                if (debugMode)
                {
                    string characterName = character.GetCharacterData()?.characterName ?? character.name;
                    Debug.Log($"{characterName} successfully picked up the ball! Hold timer started.");
                }
            }
        }
    }

    /// <summary>
    /// LEGACY: For backward compatibility with old CharacterController
    /// </summary>
    public void RequestBallPickup(CharacterController character)
    {
        if (currentBall != null && gameActive)
        {
            bool success = currentBall.TryPickupLegacy(character);
            if (success)
            {
                // Ball pickup successful - timer is automatically started in BallController
                if (enableHoldTimerUI && autoShowTimerOnPickup)
                {
                    ShowHoldTimerUI();
                }

                if (debugMode)
                {
                    Debug.Log($"{character.name} successfully picked up the ball! Hold timer started.");
                }
            }
        }
    }

    /// <summary>
    /// NEW: Request ball throw with character data integration
    /// This method gets damage from the character's data
    /// Now includes hold timer UI integration
    /// </summary>
    public void RequestBallThrowWithCharacterData(PlayerCharacter thrower, CharacterData characterData, ThrowType throwType, int damage)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            // Hide hold timer UI before throwing
            if (isTimerUIShown && autoHideTimerOnThrow)
            {
                HideHoldTimerUI();
            }

            // Apply character-specific throw modifications
            float throwSpeed = characterData.GetThrowSpeed(GetBaseThrowSpeed(throwType));
            Vector3 direction = Vector3.right; // BallController will handle targeting

            // Apply accuracy modifier
            if (currentBall.GetCurrentTarget() != null)
            {
                Vector3 targetDir = (currentBall.GetCurrentTarget().position - currentBall.transform.position).normalized;
                direction = characterData.ApplyThrowAccuracy(targetDir);
            }

            // Set ball damage and throw properties
            currentBall.SetThrowData(throwType, damage, throwSpeed);

            // Execute throw (this will automatically reset the hold timer in BallController)
            currentBall.ThrowBall(direction, 1f);

            // Apply throw effects
            if (characterData.throwEffect != null)
            {
                Instantiate(characterData.throwEffect, currentBall.transform.position, Quaternion.identity);
            }

            if (debugMode)
            {
                Debug.Log($"{characterData.characterName} threw {throwType} ball: {damage} damage, {throwSpeed} speed. Hold timer stopped.");
            }

            // Schedule respawn if ball goes out of bounds
            StartCoroutine(CheckForRespawn());
        }
    }

    /// <summary>
    /// LEGACY: Simplified throw method for backward compatibility
    /// </summary>
    public void RequestBallThrowSimple(PlayerCharacter thrower, float power = 1f)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            // Hide hold timer UI before throwing
            if (isTimerUIShown && autoHideTimerOnThrow)
            {
                HideHoldTimerUI();
            }

            CharacterData characterData = thrower.GetCharacterData();
            if (characterData != null)
            {
                // Use character data for throw
                ThrowType throwType = thrower.IsGrounded() ? ThrowType.Normal : ThrowType.JumpThrow;
                int damage = characterData.GetThrowDamage(throwType);
                RequestBallThrowWithCharacterData(thrower, characterData, throwType, damage);
            }
            else
            {
                // Fallback to basic throw
                currentBall.ThrowBall(Vector3.right, power);
                StartCoroutine(CheckForRespawn());
            }
        }
    }

    /// <summary>
    /// LEGACY: For old CharacterController compatibility
    /// </summary>
    public void RequestBallThrowSimple(CharacterController thrower, float power = 1f)
    {
        if (currentBall != null && currentBall.GetHolderLegacy() == thrower)
        {
            // Hide hold timer UI before throwing
            if (isTimerUIShown && autoHideTimerOnThrow)
            {
                HideHoldTimerUI();
            }

            // Basic throw without character data
            currentBall.ThrowBall(Vector3.right, power);
            StartCoroutine(CheckForRespawn());
        }
    }

    /// <summary>
    /// Get base throw speed based on throw type
    /// </summary>
    float GetBaseThrowSpeed(ThrowType throwType)
    {
        switch (throwType)
        {
            case ThrowType.Normal:
                return 18f;
            case ThrowType.JumpThrow:
                return 22f;
            case ThrowType.Ultimate:
                return 25f;
            default:
                return 18f;
        }
    }

    /// <summary>
    /// FIXED: Handle ultimate throws with new UltimateType enum
    /// </summary>
    public void RequestUltimateThrow(PlayerCharacter thrower, UltimateType ultimateType)
    {
        if (currentBall == null || currentBall.GetHolder() != thrower) return;

        CharacterData characterData = thrower.GetCharacterData();
        if (characterData == null) return;

        int ultimateDamage = characterData.GetUltimateDamage();

        switch (ultimateType)
        {
            case UltimateType.PowerThrow:
                // Single powerful throw
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;

            case UltimateType.MultiThrow:
                // Create multiple balls (implementation handled by PlayerCharacter)
                StartCoroutine(MultiThrowCoroutine(thrower, characterData));
                break;

            case UltimateType.Curveball:
                // Enable curve behavior (implementation handled by PlayerCharacter)
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;

            default:
                // Default to power throw
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;
        }

        if (debugMode)
        {
            Debug.Log($"{characterData.characterName} used ultimate: {ultimateType}");
        }
    }

    /// <summary>
    /// Handle multi-throw ultimate (called from RequestUltimateThrow)
    /// </summary>
    System.Collections.IEnumerator MultiThrowCoroutine(PlayerCharacter thrower, CharacterData characterData)
    {
        // Hide hold timer UI for multi-throw
        if (isTimerUIShown)
        {
            HideHoldTimerUI();
        }

        int throwCount = characterData.GetMultiThrowCount();
        int damagePerBall = characterData.GetUltimateDamage();
        float throwSpeed = characterData.GetUltimateSpeed();

        for (int i = 0; i < throwCount; i++)
        {
            // Create a temporary ball for multi-throw
            GameObject tempBallObj = Instantiate(ballPrefab, thrower.transform.position + Vector3.up * 1.5f, Quaternion.identity);
            BallController tempBall = tempBallObj.GetComponent<BallController>();

            if (tempBall != null)
            {
                tempBall.SetThrowData(ThrowType.Ultimate, damagePerBall, throwSpeed);

                // Throw in spread pattern
                float spreadAngle = characterData.GetMultiThrowSpread();
                float angleOffset = (i - (throwCount - 1) * 0.5f) * (spreadAngle / throwCount);
                Vector3 throwDir = Quaternion.Euler(0, angleOffset, 0) * Vector3.right;
                throwDir.y = 0.1f;

                tempBall.ThrowBall(throwDir.normalized, 1f);
            }

            yield return new WaitForSeconds(characterData.GetMultiThrowDelay());
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HOLD TIMER UI INTEGRATION METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Show the ball hold timer UI
    /// </summary>
    void ShowHoldTimerUI()
    {
        if (BallHoldTimerUI.Instance != null && currentBall != null)
        {
            BallHoldTimerUI.Instance.ShowTimer(currentBall);
            isTimerUIShown = true;

            if (debugMode)
            {
                Debug.Log("BallManager: Hold timer UI shown");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("BallManager: Cannot show hold timer UI - Instance or current ball is null");
        }
    }

    /// <summary>
    /// Hide the ball hold timer UI
    /// </summary>
    void HideHoldTimerUI()
    {
        if (BallHoldTimerUI.Instance != null)
        {
            BallHoldTimerUI.Instance.HideTimer();
            isTimerUIShown = false;

            if (debugMode)
            {
                Debug.Log("BallManager: Hold timer UI hidden");
            }
        }
    }

    /// <summary>
    /// Force show hold timer UI (for debugging)
    /// </summary>
    public void ForceShowHoldTimerUI()
    {
        if (currentBall != null && currentBall.IsHeld())
        {
            ShowHoldTimerUI();
        }
    }

    /// <summary>
    /// Force hide hold timer UI (for debugging)
    /// </summary>
    public void ForceHideHoldTimerUI()
    {
        HideHoldTimerUI();
    }

    public void ResetBall()
    {
        if (currentBall != null)
        {
            // Hide timer UI before resetting
            if (isTimerUIShown)
            {
                HideHoldTimerUI();
            }

            currentBall.ResetBall();
        }
        else
        {
            SpawnBall();
        }
    }

    System.Collections.IEnumerator CheckForRespawn()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Check if ball is out of bounds or needs respawn
        if (currentBall != null)
        {
            Vector3 ballPos = currentBall.transform.position;

            // If ball fell too low or went too far out
            if (ballPos.y < -3f || Mathf.Abs(ballPos.x) > 20f)
            {
                SpawnBall();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC GETTERS AND CONFIGURATION
    // ═══════════════════════════════════════════════════════════════

    public BallController GetCurrentBall() => currentBall;
    public bool HasActiveBall() => currentBall != null;
    public Vector3 GetBallSpawnPosition() => spawnPosition;
    public bool IsHoldTimerUIEnabled() => enableHoldTimerUI;
    public bool IsTimerUIShown() => isTimerUIShown;

    // Game state control
    public void SetGameActive(bool active)
    {
        gameActive = active;

        // Hide timer UI if game becomes inactive
        if (!active && isTimerUIShown)
        {
            HideHoldTimerUI();
        }
    }

    // Hold timer UI configuration
    public void SetHoldTimerUIEnabled(bool enabled)
    {
        enableHoldTimerUI = enabled;

        // Hide UI if being disabled
        if (!enabled && isTimerUIShown)
        {
            HideHoldTimerUI();
        }
    }

    public void SetAutoShowTimerOnPickup(bool enabled)
    {
        autoShowTimerOnPickup = enabled;
    }

    public void SetAutoHideTimerOnThrow(bool enabled)
    {
        autoHideTimerOnThrow = enabled;
    }

    // Character registration for multiplayer
    public void RegisterPlayer(int playerId, Transform playerTransform, PlayerCharacter playerCharacter, bool isLocal = true)
    {
        // TODO: Implement player registration for multiplayer
        if (debugMode)
        {
            string characterName = playerCharacter.GetCharacterData()?.characterName ?? "Unknown";
            Debug.Log($"Registered player {playerId} ({characterName}) - Local: {isLocal}");
        }
    }

    // Debug methods
    void ShowCharacterDebugInfo()
    {
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();

        Debug.Log("=== CHARACTER DEBUG INFO ===");
        foreach (PlayerCharacter player in allPlayers)
        {
            CharacterData data = player.GetCharacterData();
            if (data != null)
            {
                Debug.Log($"{data.characterName}: HP={data.maxHealth}, Speed={data.moveSpeed}, " +
                         $"Ultimate={data.ultimateType}, Trick={data.trickType}, Treat={data.treatType}");
            }
        }

        // Show hold timer info
        if (currentBall != null && currentBall.IsHeld())
        {
            Debug.Log($"=== BALL HOLD TIMER INFO ===");
            Debug.Log($"Hold Duration: {currentBall.GetHoldDuration():F2}s");
            Debug.Log($"Current Phase: {currentBall.GetCurrentHoldPhase()}");
            Debug.Log($"Timer UI Shown: {isTimerUIShown}");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw spawn position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPosition, 0.5f);

        // Show character throw ranges (if ball exists)
        if (currentBall != null)
        {
            PlayerCharacter holder = currentBall.GetHolder();
            if (holder != null)
            {
                CharacterData data = holder.GetCharacterData();
                if (data != null)
                {
                    // Visualize throw accuracy
                    Gizmos.color = Color.yellow;
                    float accuracy = data.throwAccuracy;
                    Gizmos.DrawWireSphere(currentBall.transform.position, (1f - accuracy) * 2f);
                }

                // Visualize hold timer state
                if (currentBall.IsHeld())
                {
                    float holdProgress = currentBall.GetHoldProgress();
                    string phase = currentBall.GetCurrentHoldPhase();

                    // Color based on hold timer phase
                    Color timerColor = phase switch
                    {
                        "Warning" => Color.yellow,
                        "Danger" => Color.red,
                        "Penalty" => Color.magenta,
                        _ => Color.green
                    };

                    Gizmos.color = timerColor;
                    Gizmos.DrawWireSphere(currentBall.transform.position + Vector3.up * 2f, 0.3f + holdProgress * 0.3f);

                    // Draw timer progress arc
                    Gizmos.color = Color.white;
                    Vector3 timerPos = currentBall.transform.position + Vector3.up * 2.5f;
                    Gizmos.DrawWireSphere(timerPos, holdProgress * 0.5f);
                }
            }
        }

        // Draw UI state indicator
        if (isTimerUIShown)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 0.2f);
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 150, 400, 140));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== BALL MANAGER DEBUG ===");
        GUILayout.Label($"Current Ball: {(currentBall != null ? "Active" : "None")}");
        GUILayout.Label($"Game Active: {gameActive}");
        GUILayout.Label($"Hold Timer UI: {(enableHoldTimerUI ? "Enabled" : "Disabled")}");
        GUILayout.Label($"Timer UI Shown: {isTimerUIShown}");

        if (currentBall != null)
        {
            GUILayout.Label($"Ball State: {currentBall.GetBallState()}");

            if (currentBall.IsHeld())
            {
                GUILayout.Label($"Hold Duration: {currentBall.GetHoldDuration():F1}s");
                GUILayout.Label($"Hold Phase: {currentBall.GetCurrentHoldPhase()}");
                GUILayout.Label($"Hold Progress: {currentBall.GetHoldProgress():P0}");
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}