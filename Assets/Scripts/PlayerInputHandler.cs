using UnityEngine;

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

    // Input buffering for responsive controls
    private float lastJumpInput = -1f;
    private float lastThrowInput = -1f;
    private float lastCatchInput = -1f;
    private float lastPickupInput = -1f;
    private float lastDashInput = -1f;
    private float lastUltimateInput = -1f;

    // FIXED: Better mobile input state management
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

    // Key mappings based on player type
    private KeyCode leftKey, rightKey, jumpKey, duckKey, throwKey, catchKey, pickupKey, dashKey, ultimateKey;

    public enum PlayerInputType
    {
        Player1,
        Player2
    }

    void Awake()
    {
        SetupKeyMappings();

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
        // For single-player testing, auto-register with MobileUIManager
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
            // Use custom key bindings
            leftKey = customLeftKey;
            rightKey = customRightKey;
            jumpKey = customJumpKey;
            duckKey = customDuckKey;
            throwKey = customThrowKey;
            catchKey = customCatchKey;
            pickupKey = customPickupKey;
            dashKey = customDashKey;
            ultimateKey = customUltimateKey;
        }
        else
        {
            // Use default mappings based on player type
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
                    ultimateKey = KeyCode.Q;
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
                    break;
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} - Player {playerType} keys: " +
                     $"Move: {leftKey}/{rightKey}, Jump: {jumpKey}, Duck: {duckKey}, " +
                     $"Throw: {throwKey}, Catch: {catchKey}, Pickup: {pickupKey}, " +
                     $"Dash: {dashKey}, Ultimate: {ultimateKey}");
        }
    }

    void Update()
    {
        // Clear previous frame input
        ResetFrameInputs();

        // Handle keyboard input first 
        if (enableKeyboardInput)
        {
            HandleKeyboardInput();
        }

        // Handle mobile input (mobile inputs override keyboard)
        if (enableMobileInput)
        {
            HandleMobileInput();
        }

        // Process input buffering
        HandleInputBuffering();

        // Debug display
        if (showDebugInfo)
        {
            DisplayDebugInfo();
        }

        // FIXED: Reset mobile frame-based inputs at end of frame
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
    }

    void ResetMobileFrameInputs()
    {
        // Reset frame-based mobile inputs
        mobileJumpPressedThisFrame = false;
        mobileThrowPressedThisFrame = false;
        mobileCatchPressedThisFrame = false;
        mobilePickupPressedThisFrame = false;
        mobileDashPressedThisFrame = false;
        mobileUltimatePressedThisFrame = false;
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

        // Duck input
        duckHeld = Input.GetKey(duckKey);

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

        // FIXED: Add dash input
        if (Input.GetKeyDown(dashKey))
        {
            dashPressed = true;
            lastDashInput = Time.time;
        }

        // FIXED: Add ultimate input
        if (Input.GetKeyDown(ultimateKey))
        {
            ultimatePressed = true;
            lastUltimateInput = Time.time;
        }
    }

    void HandleMobileInput()
    {
        // FIXED: Better mobile input handling

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
        // If both pressed, keep previous horizontal input

        // Handle frame-based inputs (override keyboard if mobile input occurred)
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

        if (mobileUltimatePressedThisFrame)
        {
            ultimatePressed = true;
        }

        // Handle duck (mobile overrides keyboard)
        if (mobileDuckPressed)
        {
            duckHeld = true;
        }

        if (showDebugInfo && (mobileLeftPressed || mobileRightPressed))
        {
            Debug.Log($"{gameObject.name} - Mobile Input: L:{mobileLeftPressed} R:{mobileRightPressed} H:{horizontalInput}");
        }
    }

    void HandleInputBuffering()
    {
        // This allows for slightly delayed input processing for better responsiveness
        // Useful for frame-perfect inputs in fighting games
    }

    // ===========================================
    // PUBLIC METHODS FOR MOBILE INPUT CALLBACKS
    // ===========================================

    /// <summary>
    /// Call this from mobile left/right buttons or touch controls
    /// </summary>
    public void SetMobileHorizontal(float value)
    {
        mobileHorizontal = Mathf.Clamp(value, -1f, 1f);

        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - SetMobileHorizontal({value})");
    }

    /// <summary>
    /// Call this from mobile jump button OnClick
    /// </summary>
    public void OnMobileJump()
    {
        mobileJumpPressedThisFrame = true;
        lastJumpInput = Time.time;

        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Jump");
    }

    /// <summary>
    /// Call this from mobile duck button - use OnPointerDown/Up for hold behavior
    /// </summary>
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

    /// <summary>
    /// Call this from mobile throw button OnClick
    /// </summary>
    public void OnMobileThrow()
    {
        mobileThrowPressedThisFrame = true;
        lastThrowInput = Time.time;

        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Throw");
    }

    /// <summary>
    /// Call this from mobile throw button OnPointerDown/Up for charging
    /// </summary>
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

    /// <summary>
    /// Call this from mobile catch button OnClick
    /// </summary>
    public void OnMobileCatch()
    {
        mobileCatchPressedThisFrame = true;
        lastCatchInput = Time.time;

        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Catch");
    }

    /// <summary>
    /// Call this from mobile pickup button OnClick
    /// </summary>
    public void OnMobilePickup()
    {
        mobilePickupPressedThisFrame = true;
        lastPickupInput = Time.time;

        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Pickup");
    }

    /// <summary>
    /// FIXED: Call this from mobile dash button OnClick
    /// </summary>
    public void OnMobileDash()
    {
        mobileDashPressedThisFrame = true;
        lastDashInput = Time.time;

        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Dash");
    }

    /// <summary>
    /// FIXED: Call this from mobile ultimate button OnClick
    /// </summary>
    public void OnMobileUltimate()
    {
        mobileUltimatePressedThisFrame = true;
        lastUltimateInput = Time.time;

        if (showDebugInfo)
            Debug.Log($"{gameObject.name} - Mobile Ultimate");
    }

    /// <summary>
    /// Call this from mobile left button OnPointerDown/Up
    /// </summary>
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

    /// <summary>
    /// Call this from mobile right button OnPointerDown/Up
    /// </summary>
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

    /// <summary>
    /// Get horizontal movement input (-1 to 1)
    /// </summary>
    public float GetHorizontal() => horizontalInput;

    /// <summary>
    /// Check if jump was pressed this frame
    /// </summary>
    public bool GetJumpPressed() => jumpPressed;

    /// <summary>
    /// Check if jump was pressed with input buffering
    /// </summary>
    public bool GetJumpBuffered()
    {
        if (Time.time - lastJumpInput <= inputBufferTime)
        {
            lastJumpInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if duck is being held
    /// </summary>
    public bool GetDuckHeld() => duckHeld;

    /// <summary>
    /// Check if throw was pressed this frame
    /// </summary>
    public bool GetThrowPressed() => throwPressed;

    /// <summary>
    /// Check if throw is being held (for charging)
    /// </summary>
    public bool GetThrowHeld() => throwHeld;

    /// <summary>
    /// Check if throw was pressed with input buffering
    /// </summary>
    public bool GetThrowBuffered()
    {
        if (Time.time - lastThrowInput <= inputBufferTime)
        {
            lastThrowInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if catch was pressed this frame
    /// </summary>
    public bool GetCatchPressed() => catchPressed;

    /// <summary>
    /// Check if catch was pressed with input buffering
    /// </summary>
    public bool GetCatchBuffered()
    {
        if (Time.time - lastCatchInput <= inputBufferTime)
        {
            lastCatchInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if pickup was pressed this frame
    /// </summary>
    public bool GetPickupPressed() => pickupPressed;

    /// <summary>
    /// Check if pickup was pressed with input buffering
    /// </summary>
    public bool GetPickupBuffered()
    {
        if (Time.time - lastPickupInput <= inputBufferTime)
        {
            lastPickupInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    /// <summary>
    /// FIXED: Check if dash was pressed this frame
    /// </summary>
    public bool GetDashPressed() => dashPressed;

    /// <summary>
    /// FIXED: Check if dash was pressed with input buffering
    /// </summary>
    public bool GetDashBuffered()
    {
        if (Time.time - lastDashInput <= inputBufferTime)
        {
            lastDashInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    /// <summary>
    /// FIXED: Check if ultimate was pressed this frame
    /// </summary>
    public bool GetUltimatePressed() => ultimatePressed;

    /// <summary>
    /// FIXED: Check if ultimate was pressed with input buffering
    /// </summary>
    public bool GetUltimateBuffered()
    {
        if (Time.time - lastUltimateInput <= inputBufferTime)
        {
            lastUltimateInput = -1f; // Consume the input
            return true;
        }
        return false;
    }

    // ===========================================
    // UTILITY METHODS
    // ===========================================

    /// <summary>
    /// Get player type for identification
    /// </summary>
    public PlayerInputType GetPlayerType() => playerType;

    /// <summary>
    /// Check if this player is using mobile input currently
    /// </summary>
    public bool IsMobileInputActive()
    {
        return mobileLeftPressed || mobileRightPressed || mobileDuckPressed ||
               mobileJumpPressedThisFrame || mobileThrowPressedThisFrame ||
               mobileCatchPressedThisFrame || mobilePickupPressedThisFrame ||
               mobileDashPressedThisFrame || mobileUltimatePressedThisFrame;
    }

    /// <summary>
    /// Manually set player type (useful for dynamic assignment)
    /// </summary>
    public void SetPlayerType(PlayerInputType type)
    {
        playerType = type;
        SetupKeyMappings();
    }

    void DisplayDebugInfo()
    {
        if (horizontalInput != 0 || jumpPressed || duckHeld || throwPressed || catchPressed ||
            pickupPressed || dashPressed || ultimatePressed)
        {
            Debug.Log($"{gameObject.name} ({playerType}) - " +
                     $"H:{horizontalInput:F1} J:{jumpPressed} D:{duckHeld} " +
                     $"T:{throwPressed} C:{catchPressed} P:{pickupPressed} " +
                     $"Dash:{dashPressed} Ult:{ultimatePressed} " +
                     $"Mobile:{IsMobileInputActive()}");
        }
    }

    // ===========================================
    // INTEGRATION HELPER METHODS
    // ===========================================

    /// <summary>
    /// Helper method to replace Input.GetKey() calls in existing scripts
    /// </summary>
    public bool GetKeyEquivalent(KeyCode key)
    {
        if (key == leftKey) return horizontalInput < -0.5f;
        if (key == rightKey) return horizontalInput > 0.5f;
        if (key == jumpKey) return jumpPressed;
        if (key == duckKey) return duckHeld;
        if (key == throwKey) return throwPressed;
        if (key == catchKey) return catchPressed;
        if (key == pickupKey) return pickupPressed;
        if (key == dashKey) return dashPressed;
        if (key == ultimateKey) return ultimatePressed;

        return false;
    }

    // ===========================================
    // CLEANUP METHODS
    // ===========================================

    /// <summary>
    /// Reset all mobile input states - useful when switching scenes or pausing
    /// </summary>
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