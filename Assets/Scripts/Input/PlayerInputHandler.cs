using UnityEngine;

/// <summary>
/// Enhanced PlayerInputHandler with Duck System integration and complete mobile support
/// Includes Ultimate/Trick/Treat mobile inputs that were missing
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Player Configuration")]
    [SerializeField] private PlayerInputType playerType = PlayerInputType.Player1;
    [SerializeField] private bool enableKeyboardInput = true;
    [SerializeField] public bool enableMobileInput = true;

    [Header("Custom Key Bindings (Optional Override)")]
    [SerializeField] private bool useCustomKeys = false;
    [SerializeField] private KeyCode customLeftKey = KeyCode.A;
    [SerializeField] private KeyCode customRightKey = KeyCode.D;
    [SerializeField] private KeyCode customJumpKey = KeyCode.W;
    [SerializeField] private KeyCode customDuckKey = KeyCode.S;
    [SerializeField] private KeyCode customThrowKey = KeyCode.K;
    [SerializeField] private KeyCode customCatchKey = KeyCode.L;
    [SerializeField] private KeyCode customPickupKey = KeyCode.J;
    [SerializeField] private KeyCode customDashKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode customUltimateKey = KeyCode.U;
    [SerializeField] private KeyCode customTrickKey = KeyCode.I;
    [SerializeField] private KeyCode customTreatKey = KeyCode.O;

    [Header("Duck System Integration")]
    [SerializeField] private bool useDuckSystem = true;

    [Header("PUN2 Network Settings")]
    [SerializeField] private bool isPUN2Enabled = false;
    [SerializeField] private bool isLocalPlayer = true;

    [Header("Input Buffer Settings")]
    [SerializeField] private float inputBufferTime = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    [Header("Testing (Single Player)")]
    [SerializeField] private bool forceMobileMode = false;
    [SerializeField] private bool autoRegisterInSinglePlayer = true;

    // Input state - current frame
    private float horizontalInput = 0f;
    private bool jumpPressed = false;
    private bool duckHeld = false;
    private bool throwPressed = false;
    private bool throwHeld = false;
    private bool catchPressed = false;
    private bool pickupPressed = false;
    private bool dashPressed = false;
    private bool ultimatePressed = false;
    private bool trickPressed = false;
    private bool treatPressed = false;

    // Input buffering for responsive controls
    private float lastJumpInput = -1f;
    private float lastThrowInput = -1f;
    private float lastCatchInput = -1f;
    private float lastPickupInput = -1f;
    private float lastDashInput = -1f;
    private float lastUltimateInput = -1f;
    private float lastTrickInput = -1f;
    private float lastTreatInput = -1f;

    // Mobile input state management
    private float mobileHorizontal = 0f;
    private bool mobileLeftPressed = false;
    private bool mobileRightPressed = false;
    private bool mobileDuckPressed = false;
    private bool mobileThrowPressedThisFrame = false;
    private bool mobileJumpPressedThisFrame = false;
    private bool mobileCatchPressedThisFrame = false;
    private bool mobilePickupPressedThisFrame = false;
    private bool mobileDashPressedThisFrame = false;
    private bool mobileUltimatePressedThisFrame = false;
    private bool mobileTrickPressedThisFrame = false;
    private bool mobileTreatPressedThisFrame = false;

    // Key mappings based on player type
    private KeyCode leftKey, rightKey, jumpKey, duckKey, throwKey, catchKey, pickupKey, dashKey, ultimateKey, trickKey, treatKey;

    // Component references
    private DuckSystem duckSystem;

    public enum PlayerInputType
    {
        Player1,
        Player2
    }

    void Awake()
    {
        SetupKeyMappings();

        // Get duck system if using it
        if (useDuckSystem)
        {
            duckSystem = GetComponent<DuckSystem>();
            if (duckSystem == null && useDuckSystem)
            {
                duckSystem = gameObject.AddComponent<DuckSystem>();
            }
        }

        // Auto-detect mobile platform or force mobile mode for testing
#if UNITY_ANDROID || UNITY_IOS
        enableMobileInput = true;
#else
        if (forceMobileMode)
        {
            enableMobileInput = true;
            Debug.Log($"{gameObject.name} - Mobile mode forced for testing");
        }
#endif
    }

    void Start()
    {
        if (autoRegisterInSinglePlayer)
        {
            RegisterWithMobileUI();
        }
    }

    void RegisterWithMobileUI()
    {
        if (MobileUIManager.Instance != null)
        {
            MobileUIManager.Instance.AssignInputHandler(this);
            Debug.Log($"{gameObject.name} - Registered with MobileUIManager for testing");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} - MobileUIManager not found! Make sure it exists in the scene.");
        }
    }

    void SetupKeyMappings()
    {
        if (useCustomKeys)
        {
            leftKey = customLeftKey;
            rightKey = customRightKey;
            jumpKey = customJumpKey;
            duckKey = customDuckKey;
            throwKey = customThrowKey;
            catchKey = customCatchKey;
            pickupKey = customPickupKey;
            dashKey = customDashKey;
            ultimateKey = customUltimateKey;
            trickKey = customTrickKey;
            treatKey = customTreatKey;
        }
        else
        {
            switch (playerType)
            {
                case PlayerInputType.Player1:
                    leftKey = KeyCode.A;
                    rightKey = KeyCode.D;
                    jumpKey = KeyCode.W;
                    duckKey = KeyCode.S;
                    throwKey = KeyCode.K;
                    catchKey = KeyCode.L;
                    pickupKey = KeyCode.J;
                    dashKey = KeyCode.LeftShift;
                    ultimateKey = KeyCode.U;
                    trickKey = KeyCode.I;
                    treatKey = KeyCode.O;
                    break;

                case PlayerInputType.Player2:
                    leftKey = KeyCode.LeftArrow;
                    rightKey = KeyCode.RightArrow;
                    jumpKey = KeyCode.UpArrow;
                    duckKey = KeyCode.DownArrow;
                    throwKey = KeyCode.Keypad1;
                    catchKey = KeyCode.Keypad2;
                    pickupKey = KeyCode.Keypad0;
                    dashKey = KeyCode.RightShift;
                    ultimateKey = KeyCode.Keypad3;
                    trickKey = KeyCode.Keypad7;
                    treatKey = KeyCode.Keypad8;
                    break;
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} - Player {playerType} keys: " +
                     $"Move: {leftKey}/{rightKey}, Jump: {jumpKey}, Duck: {duckKey}, " +
                     $"Throw: {throwKey}, Catch: {catchKey}, Pickup: {pickupKey}, " +
                     $"Dash: {dashKey}, Ultimate: {ultimateKey}, Trick: {trickKey}, Treat: {treatKey}");
        }
    }

    void Update()
    {
        if (isPUN2Enabled && !isLocalPlayer) return;

        ResetFrameInputs();

        if (enableKeyboardInput)
        {
            HandleKeyboardInput();
        }

        if (enableMobileInput)
        {
            HandleMobileInput();
        }

        HandleInputBuffering();

        if (showDebugInfo)
        {
            DisplayDebugInfo();
        }

        ResetMobileFrameInputs();
    }

    void ResetFrameInputs()
    {
        jumpPressed = false;
        throwPressed = false;
        catchPressed = false;
        pickupPressed = false;
        dashPressed = false;
        ultimatePressed = false;
        trickPressed = false;
        treatPressed = false;
    }

    void ResetMobileFrameInputs()
    {
        mobileJumpPressedThisFrame = false;
        mobileThrowPressedThisFrame = false;
        mobileCatchPressedThisFrame = false;
        mobilePickupPressedThisFrame = false;
        mobileDashPressedThisFrame = false;
        mobileUltimatePressedThisFrame = false;
        mobileTrickPressedThisFrame = false;
        mobileTreatPressedThisFrame = false;
    }

    void HandleKeyboardInput()
    {
        // Horizontal movement
        horizontalInput = 0f;
        if (Input.GetKey(leftKey))
            horizontalInput = -1f;
        else if (Input.GetKey(rightKey))
            horizontalInput = 1f;

        // Jump input
        if (Input.GetKeyDown(jumpKey))
        {
            jumpPressed = true;
            lastJumpInput = Time.time;
        }

        // Duck input - ENHANCED with duck system integration
        HandleDuckInput();

        // Throw input
        if (Input.GetKeyDown(throwKey))
        {
            throwPressed = true;
            lastThrowInput = Time.time;
        }
        throwHeld = Input.GetKey(throwKey);

        // Catch input
        if (Input.GetKeyDown(catchKey))
        {
            catchPressed = true;
            lastCatchInput = Time.time;
        }

        // Pickup input
        if (Input.GetKeyDown(pickupKey))
        {
            pickupPressed = true;
            lastPickupInput = Time.time;
        }

        // Dash input
        if (Input.GetKeyDown(dashKey))
        {
            dashPressed = true;
            lastDashInput = Time.time;
        }

        // Ultimate input
        if (Input.GetKeyDown(ultimateKey))
        {
            ultimatePressed = true;
            lastUltimateInput = Time.time;
        }

        // Trick input
        if (Input.GetKeyDown(trickKey))
        {
            trickPressed = true;
            lastTrickInput = Time.time;
        }

        // Treat input
        if (Input.GetKeyDown(treatKey))
        {
            treatPressed = true;
            lastTreatInput = Time.time;
        }
    }

    void HandleDuckInput()
    {
        bool duckKeyPressed = Input.GetKey(duckKey);

        if (useDuckSystem && duckSystem != null)
        {
            // Let DuckSystem handle the logic
            duckHeld = duckKeyPressed && duckSystem.CanDuck();

            // Override duck state if system says we're ducking
            if (duckSystem.IsDucking())
            {
                duckHeld = true;
            }
        }
        else
        {
            // Original duck behavior
            duckHeld = duckKeyPressed;
        }
    }

    void HandleMobileInput()
    {
        // Handle horizontal input
        if (mobileLeftPressed && !mobileRightPressed)
        {
            horizontalInput = -1f;
        }
        else if (mobileRightPressed && !mobileLeftPressed)
        {
            horizontalInput = 1f;
        }
        else if (!mobileLeftPressed && !mobileRightPressed)
        {
            horizontalInput = 0f;
        }

        // Handle frame-based inputs
        if (mobileJumpPressedThisFrame)
        {
            jumpPressed = true;
        }

        if (mobileThrowPressedThisFrame)
        {
            throwPressed = true;
        }

        if (mobileCatchPressedThisFrame)
        {
            catchPressed = true;
        }

        if (mobilePickupPressedThisFrame)
        {
            pickupPressed = true;
        }

        if (mobileDashPressedThisFrame)
        {
            dashPressed = true;
        }

        // FIXED: Add missing Ultimate/Trick/Treat mobile inputs
        if (mobileUltimatePressedThisFrame)
        {
            ultimatePressed = true;
        }

        if (mobileTrickPressedThisFrame)
        {
            trickPressed = true;
        }

        if (mobileTreatPressedThisFrame)
        {
            treatPressed = true;
        }

        // Handle duck with duck system integration
        if (useDuckSystem && duckSystem != null)
        {
            // Let DuckSystem handle mobile duck logic
            duckHeld = mobileDuckPressed && duckSystem.CanDuck();

            if (duckSystem.IsDucking())
            {
                duckHeld = true;
            }
        }
        else
        {
            // Original mobile duck behavior
            duckHeld = mobileDuckPressed;
        }

        if (showDebugInfo && (mobileLeftPressed || mobileRightPressed))
        {
            Debug.Log($"{gameObject.name} - Mobile Input: L:{mobileLeftPressed} R:{mobileRightPressed} H:{horizontalInput}");
        }
    }

    void HandleInputBuffering()
    {
        // Input buffering implementation remains the same
    }

    // ===========================================
    // MOBILE INPUT CALLBACKS (Enhanced)
    // ===========================================

    public void SetMobileHorizontal(float value)
    {
        mobileHorizontal = Mathf.Clamp(value, -1f, 1f);
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - SetMobileHorizontal({value})");
    }

    public void OnMobileJump()
    {
        mobileJumpPressedThisFrame = true;
        lastJumpInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Jump");
    }

    public void OnMobileDuckDown()
    {
        mobileDuckPressed = true;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Duck Down");
    }

    public void OnMobileDuckUp()
    {
        mobileDuckPressed = false;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Duck Up");
    }

    public void OnMobileThrow()
    {
        mobileThrowPressedThisFrame = true;
        lastThrowInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Throw");
    }

    public void OnMobileThrowDown()
    {
        mobileThrowPressedThisFrame = true;
        throwHeld = true;
        lastThrowInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Throw Down");
    }

    public void OnMobileThrowUp()
    {
        throwHeld = false;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Throw Up");
    }

    public void OnMobileCatch()
    {
        mobileCatchPressedThisFrame = true;
        lastCatchInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Catch");
    }

    public void OnMobilePickup()
    {
        mobilePickupPressedThisFrame = true;
        lastPickupInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Pickup");
    }

    public void OnMobileDash()
    {
        mobileDashPressedThisFrame = true;
        lastDashInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Dash");
    }

    // FIXED: Add missing Ultimate/Trick/Treat mobile callbacks
    public void OnMobileUltimate()
    {
        mobileUltimatePressedThisFrame = true;
        lastUltimateInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Ultimate");
    }

    public void OnMobileTrick()
    {
        mobileTrickPressedThisFrame = true;
        lastTrickInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Trick");
    }

    public void OnMobileTreat()
    {
        mobileTreatPressedThisFrame = true;
        lastTreatInput = Time.time;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Treat");
    }

    public void OnMobileLeftDown()
    {
        mobileLeftPressed = true;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Left Down");
    }

    public void OnMobileLeftUp()
    {
        mobileLeftPressed = false;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Left Up");
    }

    public void OnMobileRightDown()
    {
        mobileRightPressed = true;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Right Down");
    }

    public void OnMobileRightUp()
    {
        mobileRightPressed = false;
        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Right Up");
    }

    // ===========================================
    // PUBLIC GETTER METHODS FOR GAME SCRIPTS
    // ===========================================

    public float GetHorizontal() => horizontalInput;

    public bool GetJumpPressed() => jumpPressed;

    public bool GetJumpBuffered()
    {
        if (Time.time - lastJumpInput <= inputBufferTime)
        {
            lastJumpInput = -1f;
            return true;
        }
        return false;
    }

    // ENHANCED: Duck getter with system integration
    public bool GetDuckHeld()
    {
        if (useDuckSystem && duckSystem != null)
        {
            // Return true if duck system says we're ducking, even if input released
            return duckSystem.IsDucking() || (duckHeld && duckSystem.CanDuck());
        }
        return duckHeld;
    }

    // NEW: Additional duck system getters
    public bool GetDuckPressed()
    {
        if (useDuckSystem && duckSystem != null)
        {
            // Return true only if we just started ducking this frame
            return duckSystem.IsDucking() && duckHeld;
        }
        return duckHeld; // Fallback for non-duck-system
    }

    public bool CanDuck()
    {
        if (useDuckSystem && duckSystem != null)
        {
            return duckSystem.CanDuck();
        }
        return true; // Always can duck without system
    }

    public bool GetThrowPressed() => throwPressed;
    public bool GetThrowHeld() => throwHeld;

    public bool GetThrowBuffered()
    {
        if (Time.time - lastThrowInput <= inputBufferTime)
        {
            lastThrowInput = -1f;
            return true;
        }
        return false;
    }

    public bool GetCatchPressed() => catchPressed;

    public bool GetCatchBuffered()
    {
        if (Time.time - lastCatchInput <= inputBufferTime)
        {
            lastCatchInput = -1f;
            return true;
        }
        return false;
    }

    public bool GetPickupPressed() => pickupPressed;

    public bool GetPickupBuffered()
    {
        if (Time.time - lastPickupInput <= inputBufferTime)
        {
            lastPickupInput = -1f;
            return true;
        }
        return false;
    }

    public bool GetDashPressed() => dashPressed;

    public bool GetDashBuffered()
    {
        if (Time.time - lastDashInput <= inputBufferTime)
        {
            lastDashInput = -1f;
            return true;
        }
        return false;
    }

    // FIXED: Complete Ultimate/Trick/Treat getters
    public bool GetUltimatePressed() => ultimatePressed;

    public bool GetUltimateBuffered()
    {
        if (Time.time - lastUltimateInput <= inputBufferTime)
        {
            lastUltimateInput = -1f;
            return true;
        }
        return false;
    }

    public bool GetTrickPressed() => trickPressed;

    public bool GetTrickBuffered()
    {
        if (Time.time - lastTrickInput <= inputBufferTime)
        {
            lastTrickInput = -1f;
            return true;
        }
        return false;
    }

    public bool GetTreatPressed() => treatPressed;

    public bool GetTreatBuffered()
    {
        if (Time.time - lastTreatInput <= inputBufferTime)
        {
            lastTreatInput = -1f;
            return true;
        }
        return false;
    }

    // ===========================================
    // UTILITY METHODS
    // ===========================================

    public PlayerInputType GetPlayerType() => playerType;

    public bool IsMobileInputActive()
    {
        return mobileLeftPressed || mobileRightPressed || mobileDuckPressed ||
               mobileJumpPressedThisFrame || mobileThrowPressedThisFrame ||
               mobileCatchPressedThisFrame || mobilePickupPressedThisFrame ||
               mobileDashPressedThisFrame || mobileUltimatePressedThisFrame ||
               mobileTrickPressedThisFrame || mobileTreatPressedThisFrame;
    }

    public void SetPlayerType(PlayerInputType type)
    {
        playerType = type;
        SetupKeyMappings();
    }

    // Duck system utilities
    public DuckSystem GetDuckSystem() => duckSystem;
    public bool IsUsingDuckSystem() => useDuckSystem && duckSystem != null;

    void DisplayDebugInfo()
    {
        if (horizontalInput != 0 || jumpPressed || duckHeld || throwPressed || catchPressed ||
            pickupPressed || dashPressed || ultimatePressed || trickPressed || treatPressed)
        {
            string duckInfo = "";
            if (useDuckSystem && duckSystem != null)
            {
                duckInfo = $" Duck:[{duckHeld}|{duckSystem.IsDucking()}|{duckSystem.CanDuck()}]";
            }
            else
            {
                duckInfo = $" Duck:{duckHeld}";
            }

            Debug.Log($"{gameObject.name} ({playerType}) - " +
                     $"H:{horizontalInput:F1} J:{jumpPressed}{duckInfo} " +
                     $"T:{throwPressed} C:{catchPressed} P:{pickupPressed} " +
                     $"Dash:{dashPressed} Ult:{ultimatePressed} Trick:{trickPressed} Treat:{treatPressed} " +
                     $"Mobile:{IsMobileInputActive()}");
        }
    }

    public bool GetKeyEquivalent(KeyCode key)
    {
        if (key == leftKey) return horizontalInput < -0.5f;
        if (key == rightKey) return horizontalInput > 0.5f;
        if (key == jumpKey) return jumpPressed;
        if (key == duckKey) return GetDuckHeld();
        if (key == throwKey) return throwPressed;
        if (key == catchKey) return catchPressed;
        if (key == pickupKey) return pickupPressed;
        if (key == dashKey) return dashPressed;
        if (key == ultimateKey) return ultimatePressed;
        if (key == trickKey) return trickPressed;
        if (key == treatKey) return treatPressed;

        return false;
    }

    public void ResetMobileInputs()
    {
        mobileLeftPressed = false;
        mobileRightPressed = false;
        mobileDuckPressed = false;
        mobileJumpPressedThisFrame = false;
        mobileThrowPressedThisFrame = false;
        mobileCatchPressedThisFrame = false;
        mobilePickupPressedThisFrame = false;
        mobileDashPressedThisFrame = false;
        mobileUltimatePressedThisFrame = false;
        mobileTrickPressedThisFrame = false;
        mobileTreatPressedThisFrame = false;
        mobileHorizontal = 0f;

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} - Mobile inputs reset");
        }
    }

    void OnDestroy()
    {
        ResetMobileInputs();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ResetMobileInputs();
        }
    }
}