using UnityEngine;
using Photon.Pun;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// FIXED: PlayerInputHandler with proper PUN2 ownership model
/// Each player processes their own input regardless of master client status
/// Only blocks input processing for remote players' characters
/// </summary>
public class PlayerInputHandler : MonoBehaviourPun
{
    [Header("Player Configuration")]
    [SerializeField] private PlayerInputType playerType = PlayerInputType.Player1;
    [SerializeField] private bool enableKeyboardInput = true;
    [SerializeField] public bool enableMobileInput = true;
    [SerializeField] private bool enableGamepadInput = true;

    [Header("Network Input Settings")]
    [SerializeField] private bool enableInputPrediction = true;
    [SerializeField] private float inputSendRate = 30f;

    private PlayerInputHandler inputHandler;
    private float lastInputSend = 0f;

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
    [SerializeField] public bool isPUN2Enabled = false;

    // FIXED: Changed from isLocalPlayer to clearer ownership check
    [Header("Ownership Detection")]
    [SerializeField] private bool autoDetectOwnership = true;
    [SerializeField] private bool forceLocalControl = false; // For testing

    [Header("Input Buffer Settings")]
    [SerializeField] private float inputBufferTime = 0.15f;
    
    [Header("Gamepad Settings")]
    [SerializeField] [Range(0.1f, 0.5f)] private float gamepadDeadzone = 0.2f;
    [SerializeField] private ControllerMappingData controllerMapping;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    [Header("Testing (Single Player)")]
    [SerializeField] private bool forceMobileMode = false;
    [SerializeField] private bool autoRegisterInSinglePlayer = true;

    // FIXED: Ownership state tracking
    private bool isMyCharacter = true; // Default to true for single player
    private bool isNetworkReady = false;

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
    private bool ultimateHeld = false; // NEW: Track if ultimate key is being held
    private bool ultimateReleased = false; // NEW: Track if ultimate key was released this frame
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

        inputHandler = GetComponent<PlayerInputHandler>();

        // FIXED: Properly determine ownership
        DetermineOwnership();
    }

    // FIXED: New ownership determination method
    void DetermineOwnership()
    {
        if (PhotonNetwork.OfflineMode)
        {
            // CRITICAL: In offline mode, check if this is an AI character
            // AI characters should NOT respond to human controller input
            bool isAI = gameObject.name.Contains("AI") || GetComponent("AIControllerBrain") != null;
            
            if (isAI)
            {
                // This is an AI character - disable human input
                isMyCharacter = false;
                isNetworkReady = false;
                if (showDebugInfo)
                    Debug.Log($"{gameObject.name} - Ownership: AI CHARACTER (human input disabled)");
                return;
            }
            
            // This is the human player character
            isMyCharacter = true;
            isNetworkReady = false;
            if (showDebugInfo)
                Debug.Log($"{gameObject.name} - Ownership: HUMAN PLAYER (offline mode)");
            return;
        }

        if (forceLocalControl)
        {
            isMyCharacter = true;
            isNetworkReady = true;
            if (showDebugInfo)
                Debug.Log($"{gameObject.name} - Ownership: FORCED LOCAL CONTROL");
            return;
        }

        if (isPUN2Enabled && photonView != null)
        {
            // In networked game: only control characters that belong to this client
            isMyCharacter = photonView.IsMine;
            isNetworkReady = PhotonNetwork.IsConnected;

            if (showDebugInfo)
                Debug.Log($"{gameObject.name} - Ownership: PUN2 IsMine={isMyCharacter}, NetworkReady={isNetworkReady}");
        }
        else
        {
            // Single player or non-networked: control all characters
            isMyCharacter = true;
            isNetworkReady = true;

            if (showDebugInfo)
                Debug.Log($"{gameObject.name} - Ownership: SINGLE PLAYER (local control)");
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
                    leftKey = KeyCode.LeftArrow;
                    rightKey = KeyCode.RightArrow;
                    jumpKey = KeyCode.UpArrow;
                    duckKey = KeyCode.DownArrow;
                    throwKey = KeyCode.A;
                    pickupKey = KeyCode.D;
                    catchKey = KeyCode.S;
                    trickKey = KeyCode.W;
                    treatKey = KeyCode.E;
                    ultimateKey = KeyCode.Q;
                    dashKey = KeyCode.LeftShift;
                    break;

                case PlayerInputType.Player2:
                    leftKey = KeyCode.Keypad1;
                    rightKey = KeyCode.Keypad3;
                    jumpKey = KeyCode.Keypad5;
                    duckKey = KeyCode.Keypad2;
                    throwKey = KeyCode.J;
                    pickupKey = KeyCode.L;
                    catchKey = KeyCode.K;
                    trickKey = KeyCode.I;
                    treatKey = KeyCode.O;
                    ultimateKey = KeyCode.U;
                    dashKey = KeyCode.RightShift;
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
        // FIXED: Re-check ownership if network state changes
        if (!isNetworkReady)
        {
            DetermineOwnership();
        }

        // FIXED: Only block input for remote players' characters
        if (isPUN2Enabled && !isMyCharacter)
        {
            if (showDebugInfo && horizontalInput != 0f)
                Debug.Log($"{gameObject.name} - Input blocked: not my character");
            return;
        }

        ResetFrameInputs();

        // Send network updates for owned characters
        if (isPUN2Enabled && isMyCharacter && Time.time - lastInputSend >= 1f / inputSendRate)
        {
            SendInputToNetwork();
            lastInputSend = Time.time;
        }

        if (enableKeyboardInput)
        {
            HandleKeyboardInput();
        }

        if (enableMobileInput)
        {
            HandleMobileInput();
        }
        
        if (enableGamepadInput && isMyCharacter)
        {
            HandleGamepadInput();
        }

        // Apply external input override if provided this frame
        if (useExternalInputOverride)
        {
            ApplyExternalFrameToState();
        }

        HandleInputBuffering();

        if (showDebugInfo && HasAnyInput())
        {
            DisplayDebugInfo();
        }

        ResetMobileFrameInputs();

        // External input override is one-frame only
        useExternalInputOverride = false;
    }

    // FIXED: Improved input networking
    void SendInputToNetwork()
    {
        if (inputHandler == null || !photonView.IsMine) return;

        // Only send input if there's actual input to avoid spam
        float horizontal = inputHandler.GetHorizontal();
        bool hasInput = Mathf.Abs(horizontal) > 0.01f ||
                       inputHandler.GetJumpPressed() ||
                       inputHandler.GetDuckHeld() ||
                       inputHandler.GetThrowPressed() ||
                       inputHandler.GetPickupPressed();

        if (hasInput)
        {
            // Send critical input data as RPC for immediate response
            photonView.RPC("ReceivePlayerInput", RpcTarget.Others,
                          horizontal,
                          inputHandler.GetJumpPressed(),
                          inputHandler.GetDuckHeld(),
                          inputHandler.GetThrowPressed(),
                          inputHandler.GetPickupPressed(),
                          PhotonNetwork.Time);
        }
    }

    [PunRPC]
    void ReceivePlayerInput(float horizontal, bool jump, bool duck, bool throwInput, bool pickup, double timestamp)
    {
        // Apply received input to remote player for smoother prediction
        if (enableInputPrediction && !photonView.IsMine)
        {
            // Calculate lag and predict where player should be
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - timestamp));

            // For now, just store the input for visual feedback
            // The actual game logic will be handled by the owning client
            if (showDebugInfo)
            {
                Debug.Log($"{gameObject.name} - Received remote input: H={horizontal}, J={jump}, D={duck}, T={throwInput}, P={pickup}, Lag={lag:F3}");
            }
        }
    }

    bool HasAnyInput()
    {
        return horizontalInput != 0 || jumpPressed || duckHeld || throwPressed || catchPressed ||
               pickupPressed || dashPressed || ultimatePressed || trickPressed || treatPressed;
    }

    void ResetFrameInputs()
    {
        jumpPressed = false;
        throwPressed = false;
        catchPressed = false;
        pickupPressed = false;
        dashPressed = false;
        ultimatePressed = false;
        ultimateReleased = false; // NEW: Reset ultimate release state
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
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            // Fallback jump key for convenience
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

        // Ultimate input - NEW: Track press, hold, and release
        if (Input.GetKeyDown(ultimateKey))
        {
            ultimatePressed = true;
            lastUltimateInput = Time.time;
        }
        
        // NEW: Track held state for ultimate charging
        ultimateHeld = Input.GetKey(ultimateKey);
        
        // NEW: Track key release
        if (Input.GetKeyUp(ultimateKey))
        {
            ultimateReleased = true;
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
    
#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Handle gamepad/controller input using Unity Input System
    /// Supports PS5 DualSense, Xbox controllers, and generic gamepads
    /// CRITICAL: Only processes input for local player's character
    /// </summary>
    void HandleGamepadInput()
    {
        // CRITICAL: Only process gamepad input for this player's character
        if (!isMyCharacter) return;
        
        if (Gamepad.current == null) return;
        
        var gamepad = Gamepad.current;
        
        // Use custom mapping if provided, otherwise use defaults
        ControllerMappingData mapping = controllerMapping;
        float deadzone = mapping != null ? mapping.movementDeadzone : gamepadDeadzone;
        float triggerRelease = mapping != null ? mapping.triggerReleaseThreshold : 0.3f;
        
        // Horizontal movement from stick (based on mapping)
        ControllerMappingData.ControllerStick movementStick = mapping != null ? mapping.movementStick : ControllerMappingData.ControllerStick.LeftStick;
        Vector2 stickValue = GetStickValue(gamepad, movementStick);
        float stickX = stickValue.x;
        
        // Apply deadzone
        if (Mathf.Abs(stickX) > deadzone)
        {
            // Only override horizontal input if gamepad is providing input
            // This allows keyboard/mobile to work simultaneously
            if (Mathf.Abs(stickX) > Mathf.Abs(horizontalInput))
            {
                horizontalInput = Mathf.Clamp(stickX, -1f, 1f);
            }
        }
        
        // Process each action using flexible mapping (button OR trigger)
        ProcessActionMapping(gamepad, mapping != null ? mapping.jumpMapping : GetDefaultJumpMapping(), 
                            ref jumpPressed, ref lastJumpInput, 
                            () => { if (HapticFeedbackManager.Instance != null) HapticFeedbackManager.Instance.VibrateLight(); });
        
        // Throw - special handling for held state
        var throwMapping = mapping != null ? mapping.throwMapping : GetDefaultThrowMapping();
        if (throwMapping.inputType == ControllerMappingData.ActionMapping.InputType.Button)
        {
            if (GetButtonPressed(gamepad, throwMapping.button))
            {
                throwPressed = true;
                lastThrowInput = Time.time;
            }
            throwHeld = GetButtonHeld(gamepad, throwMapping.button);
        }
        else // Trigger
        {
            float triggerValue = GetTriggerValue(gamepad, throwMapping.trigger);
            if (triggerValue > throwMapping.triggerThreshold)
            {
                if (!throwPressed)
                {
                    throwPressed = true;
                    lastThrowInput = Time.time;
                }
                throwHeld = true;
            }
            else
            {
                throwHeld = false;
            }
        }
        
        ProcessActionMapping(gamepad, mapping != null ? mapping.catchMapping : GetDefaultCatchMapping(), 
                            ref catchPressed, ref lastCatchInput, 
                            () => { if (HapticFeedbackManager.Instance != null) HapticFeedbackManager.Instance.VibrateMedium(); });
        
        ProcessActionMapping(gamepad, mapping != null ? mapping.pickupMapping : GetDefaultPickupMapping(), 
                            ref pickupPressed, ref lastPickupInput, 
                            () => { if (HapticFeedbackManager.Instance != null) HapticFeedbackManager.Instance.VibrateMedium(0.15f); });
        
        ProcessActionMapping(gamepad, mapping != null ? mapping.dashMapping : GetDefaultDashMapping(), 
                            ref dashPressed, ref lastDashInput, 
                            () => { if (HapticFeedbackManager.Instance != null) HapticFeedbackManager.Instance.VibrateLight(0.15f); });
        
        // Ultimate - special handling for hold/release
        ProcessUltimateMapping(gamepad, mapping != null ? mapping.ultimateMapping : GetDefaultUltimateMapping(), triggerRelease);
        
        ProcessActionMapping(gamepad, mapping != null ? mapping.trickMapping : GetDefaultTrickMapping(), 
                            ref trickPressed, ref lastTrickInput, null);
        
        ProcessActionMapping(gamepad, mapping != null ? mapping.treatMapping : GetDefaultTreatMapping(), 
                            ref treatPressed, ref lastTreatInput, null);
        
        // Duck - special handling for hold/release
        ProcessDuckMapping(gamepad, mapping != null ? mapping.duckMapping : GetDefaultDuckMapping());
        
        if (showDebugInfo && (Mathf.Abs(stickX) > deadzone || jumpPressed || throwPressed))
        {
            Debug.Log($"{gameObject.name} - Gamepad Input: StickX={stickX:F2}, Jump={jumpPressed}, Throw={throwPressed}");
        }
    }
    
    /// <summary>
    /// Process action mapping (button OR trigger) - flexible system
    /// </summary>
    void ProcessActionMapping(Gamepad gamepad, ControllerMappingData.ActionMapping mapping, 
                              ref bool pressed, ref float lastInputTime, 
                              System.Action onPressCallback = null)
    {
        bool? dummy = null;
        ProcessActionMapping(gamepad, mapping, ref pressed, ref lastInputTime, onPressCallback, ref dummy);
    }
    
    /// <summary>
    /// Process action mapping (button OR trigger) - with held parameter
    /// </summary>
    void ProcessActionMapping(Gamepad gamepad, ControllerMappingData.ActionMapping mapping, 
                              ref bool pressed, ref float lastInputTime, 
                              System.Action onPressCallback, ref bool? held)
    {
        if (mapping.inputType == ControllerMappingData.ActionMapping.InputType.Button)
        {
            if (GetButtonPressed(gamepad, mapping.button))
            {
                pressed = true;
                lastInputTime = Time.time;
                onPressCallback?.Invoke();
            }
            // Note: held parameter is not used in this overload, handled separately for throw
        }
        else // Trigger
        {
            float triggerValue = GetTriggerValue(gamepad, mapping.trigger);
            if (triggerValue > mapping.triggerThreshold)
            {
                if (!pressed)
                {
                    pressed = true;
                    lastInputTime = Time.time;
                    onPressCallback?.Invoke();
                }
            }
            else
            {
                pressed = false;
            }
        }
    }
    
    /// <summary>
    /// Process ultimate mapping with hold/release support
    /// </summary>
    void ProcessUltimateMapping(Gamepad gamepad, ControllerMappingData.ActionMapping mapping, float triggerRelease)
    {
        if (mapping.inputType == ControllerMappingData.ActionMapping.InputType.Button)
        {
            // Button mode - treat as toggle
            if (GetButtonPressed(gamepad, mapping.button))
            {
                if (!ultimateHeld)
                {
                    ultimatePressed = true;
                    ultimateHeld = true;
                    lastUltimateInput = Time.time;
                    if (HapticFeedbackManager.Instance != null)
                        HapticFeedbackManager.Instance.StartContinuousVibration(0.7f);
                }
            }
            else if (GetButtonReleased(gamepad, mapping.button))
            {
                ultimateReleased = true;
                ultimateHeld = false;
                if (HapticFeedbackManager.Instance != null)
                {
                    HapticFeedbackManager.Instance.StopContinuousVibration();
                    HapticFeedbackManager.Instance.VibrateStrong(0.4f);
                }
            }
        }
        else // Trigger mode
        {
            float triggerValue = GetTriggerValue(gamepad, mapping.trigger);
            if (triggerValue > mapping.triggerThreshold && !ultimateHeld)
            {
                ultimatePressed = true;
                ultimateHeld = true;
                lastUltimateInput = Time.time;
                if (HapticFeedbackManager.Instance != null)
                    HapticFeedbackManager.Instance.StartContinuousVibration(0.7f);
            }
            else if (triggerValue <= triggerRelease && ultimateHeld)
            {
                ultimateReleased = true;
                ultimateHeld = false;
                if (HapticFeedbackManager.Instance != null)
                {
                    HapticFeedbackManager.Instance.StopContinuousVibration();
                    HapticFeedbackManager.Instance.VibrateStrong(0.4f);
                }
            }
            else if (triggerValue > mapping.triggerThreshold)
            {
                ultimateHeld = true;
            }
        }
    }
    
    /// <summary>
    /// Process duck mapping with hold/release support
    /// </summary>
    void ProcessDuckMapping(Gamepad gamepad, ControllerMappingData.ActionMapping mapping)
    {
        if (mapping.inputType == ControllerMappingData.ActionMapping.InputType.Button)
        {
            if (GetButtonPressed(gamepad, mapping.button))
            {
                if (useDuckSystem && duckSystem != null)
                {
                    duckHeld = duckSystem.CanDuck();
                }
                else
                {
                    duckHeld = true;
                }
            }
            else if (GetButtonReleased(gamepad, mapping.button))
            {
                duckHeld = false;
            }
        }
        else // Trigger mode
        {
            float triggerValue = GetTriggerValue(gamepad, mapping.trigger);
            if (triggerValue > mapping.triggerThreshold)
            {
                if (useDuckSystem && duckSystem != null)
                {
                    duckHeld = duckSystem.CanDuck();
                }
                else
                {
                    duckHeld = true;
                }
            }
            else
            {
                duckHeld = false;
            }
        }
    }
    
    // Default mappings (used when no ScriptableObject is assigned)
    ControllerMappingData.ActionMapping GetDefaultJumpMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonSouth };
    }
    
    ControllerMappingData.ActionMapping GetDefaultThrowMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonWest };
    }
    
    ControllerMappingData.ActionMapping GetDefaultCatchMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonEast };
    }
    
    ControllerMappingData.ActionMapping GetDefaultPickupMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.ButtonNorth };
    }
    
    ControllerMappingData.ActionMapping GetDefaultDashMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.RightShoulder };
    }
    
    ControllerMappingData.ActionMapping GetDefaultUltimateMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Trigger, trigger = ControllerMappingData.ControllerTrigger.RightTrigger, triggerThreshold = 0.5f };
    }
    
    ControllerMappingData.ActionMapping GetDefaultTrickMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.LeftShoulder };
    }
    
    ControllerMappingData.ActionMapping GetDefaultTreatMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Trigger, trigger = ControllerMappingData.ControllerTrigger.LeftTrigger, triggerThreshold = 0.5f };
    }
    
    ControllerMappingData.ActionMapping GetDefaultDuckMapping()
    {
        return new ControllerMappingData.ActionMapping { inputType = ControllerMappingData.ActionMapping.InputType.Button, button = ControllerMappingData.ControllerButton.DPadDown };
    }
    
    /// <summary>
    /// Get stick value based on mapping
    /// </summary>
    Vector2 GetStickValue(Gamepad gamepad, ControllerMappingData.ControllerStick stick)
    {
        switch (stick)
        {
            case ControllerMappingData.ControllerStick.LeftStick:
                return gamepad.leftStick.ReadValue();
            case ControllerMappingData.ControllerStick.RightStick:
                return gamepad.rightStick.ReadValue();
            default:
                return gamepad.leftStick.ReadValue();
        }
    }
    
    /// <summary>
    /// Check if button is pressed based on mapping
    /// </summary>
    bool GetButtonPressed(Gamepad gamepad, ControllerMappingData.ControllerButton button)
    {
        switch (button)
        {
            case ControllerMappingData.ControllerButton.ButtonSouth: return gamepad.buttonSouth.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.ButtonWest: return gamepad.buttonWest.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.ButtonEast: return gamepad.buttonEast.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.ButtonNorth: return gamepad.buttonNorth.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.LeftShoulder: return gamepad.leftShoulder.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.RightShoulder: return gamepad.rightShoulder.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.LeftStick: return gamepad.leftStickButton.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.RightStick: return gamepad.rightStickButton.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.DPadUp: return gamepad.dpad.up.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.DPadDown: return gamepad.dpad.down.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.DPadLeft: return gamepad.dpad.left.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.DPadRight: return gamepad.dpad.right.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.Start: return gamepad.startButton.wasPressedThisFrame;
            case ControllerMappingData.ControllerButton.Select: return gamepad.selectButton.wasPressedThisFrame;
            default: return false;
        }
    }
    
    /// <summary>
    /// Check if button is held based on mapping
    /// </summary>
    bool GetButtonHeld(Gamepad gamepad, ControllerMappingData.ControllerButton button)
    {
        switch (button)
        {
            case ControllerMappingData.ControllerButton.ButtonSouth: return gamepad.buttonSouth.isPressed;
            case ControllerMappingData.ControllerButton.ButtonWest: return gamepad.buttonWest.isPressed;
            case ControllerMappingData.ControllerButton.ButtonEast: return gamepad.buttonEast.isPressed;
            case ControllerMappingData.ControllerButton.ButtonNorth: return gamepad.buttonNorth.isPressed;
            case ControllerMappingData.ControllerButton.LeftShoulder: return gamepad.leftShoulder.isPressed;
            case ControllerMappingData.ControllerButton.RightShoulder: return gamepad.rightShoulder.isPressed;
            case ControllerMappingData.ControllerButton.LeftStick: return gamepad.leftStickButton.isPressed;
            case ControllerMappingData.ControllerButton.RightStick: return gamepad.rightStickButton.isPressed;
            case ControllerMappingData.ControllerButton.DPadUp: return gamepad.dpad.up.isPressed;
            case ControllerMappingData.ControllerButton.DPadDown: return gamepad.dpad.down.isPressed;
            case ControllerMappingData.ControllerButton.DPadLeft: return gamepad.dpad.left.isPressed;
            case ControllerMappingData.ControllerButton.DPadRight: return gamepad.dpad.right.isPressed;
            case ControllerMappingData.ControllerButton.Start: return gamepad.startButton.isPressed;
            case ControllerMappingData.ControllerButton.Select: return gamepad.selectButton.isPressed;
            default: return false;
        }
    }
    
    /// <summary>
    /// Check if button is released based on mapping
    /// </summary>
    bool GetButtonReleased(Gamepad gamepad, ControllerMappingData.ControllerButton button)
    {
        switch (button)
        {
            case ControllerMappingData.ControllerButton.ButtonSouth: return gamepad.buttonSouth.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.ButtonWest: return gamepad.buttonWest.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.ButtonEast: return gamepad.buttonEast.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.ButtonNorth: return gamepad.buttonNorth.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.LeftShoulder: return gamepad.leftShoulder.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.RightShoulder: return gamepad.rightShoulder.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.LeftStick: return gamepad.leftStickButton.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.RightStick: return gamepad.rightStickButton.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.DPadUp: return gamepad.dpad.up.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.DPadDown: return gamepad.dpad.down.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.DPadLeft: return gamepad.dpad.left.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.DPadRight: return gamepad.dpad.right.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.Start: return gamepad.startButton.wasReleasedThisFrame;
            case ControllerMappingData.ControllerButton.Select: return gamepad.selectButton.wasReleasedThisFrame;
            default: return false;
        }
    }
    
    /// <summary>
    /// Get trigger value based on mapping
    /// </summary>
    float GetTriggerValue(Gamepad gamepad, ControllerMappingData.ControllerTrigger trigger)
    {
        switch (trigger)
        {
            case ControllerMappingData.ControllerTrigger.LeftTrigger:
                return gamepad.leftTrigger.ReadValue();
            case ControllerMappingData.ControllerTrigger.RightTrigger:
                return gamepad.rightTrigger.ReadValue();
            default:
                return 0f;
        }
    }
#else
    /// <summary>
    /// Stub method when Input System is not available
    /// </summary>
    void HandleGamepadInput()
    {
        // Input System not available - gamepad input disabled
    }
#endif

    void HandleInputBuffering()
    {
        // Allow buffered inputs to be used within the buffer window
        float currentTime = Time.time;

        // Jump buffering
        if (!jumpPressed && (currentTime - lastJumpInput <= inputBufferTime))
        {
            jumpPressed = true;
        }

        // Throw buffering
        if (!throwPressed && (currentTime - lastThrowInput <= inputBufferTime))
        {
            throwPressed = true;
        }

        // Catch buffering
        if (!catchPressed && (currentTime - lastCatchInput <= inputBufferTime))
        {
            catchPressed = true;
        }

        // Pickup buffering
        if (!pickupPressed && (currentTime - lastPickupInput <= inputBufferTime))
        {
            pickupPressed = true;
        }

        // Dash buffering
        if (!dashPressed && (currentTime - lastDashInput <= inputBufferTime))
        {
            dashPressed = true;
        }

        // Ultimate buffering
        if (!ultimatePressed && (currentTime - lastUltimateInput <= inputBufferTime))
        {
            ultimatePressed = true;
        }

        // Trick buffering
        if (!trickPressed && (currentTime - lastTrickInput <= inputBufferTime))
        {
            trickPressed = true;
        }

        // Treat buffering
        if (!treatPressed && (currentTime - lastTreatInput <= inputBufferTime))
        {
            treatPressed = true;
        }
    }

    void DisplayDebugInfo()
    {
        string inputInfo = $"{gameObject.name} - Input: ";
        inputInfo += $"H:{horizontalInput:F2} ";
        if (jumpPressed) inputInfo += "JUMP ";
        if (duckHeld) inputInfo += "DUCK ";
        if (throwPressed) inputInfo += "THROW ";
        if (throwHeld) inputInfo += "THROW_HELD ";
        if (catchPressed) inputInfo += "CATCH ";
        if (pickupPressed) inputInfo += "PICKUP ";
        if (dashPressed) inputInfo += "DASH ";
        if (ultimatePressed) inputInfo += "ULTIMATE ";
        if (trickPressed) inputInfo += "TRICK ";
        if (treatPressed) inputInfo += "TREAT ";

        Debug.Log(inputInfo);
    }

    // ===========================================
    // EXTERNAL INPUT OVERRIDE (for AI/controller replays)
    // ===========================================

    public struct ExternalInputFrame
    {
        public float horizontal;
        public bool jumpPressed;
        public bool duckHeld;
        public bool throwPressed;
        public bool throwHeld;
        public bool catchPressed;
        public bool pickupPressed;
        public bool dashPressed;
        public bool ultimatePressed;
        public bool trickPressed;
        public bool treatPressed;
    }

    private bool useExternalInputOverride = false;
    private ExternalInputFrame externalInputFrame;

    public void ApplyExternalInput(ExternalInputFrame frame)
    {
        externalInputFrame = frame;
        useExternalInputOverride = true;
    }

    private void ApplyExternalFrameToState()
    {
        horizontalInput = Mathf.Clamp(externalInputFrame.horizontal, -1f, 1f);
        jumpPressed = externalInputFrame.jumpPressed;
        duckHeld = externalInputFrame.duckHeld;
        throwPressed = externalInputFrame.throwPressed;
        throwHeld = externalInputFrame.throwHeld;
        catchPressed = externalInputFrame.catchPressed;
        pickupPressed = externalInputFrame.pickupPressed;
        dashPressed = externalInputFrame.dashPressed;
        ultimatePressed = externalInputFrame.ultimatePressed;
        trickPressed = externalInputFrame.trickPressed;
        treatPressed = externalInputFrame.treatPressed;
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

    // ===========================================
    // PUBLIC INPUT GETTERS
    // ===========================================

    public float GetHorizontal()
    {
        return horizontalInput;
    }

    public bool GetJumpPressed()
    {
        return jumpPressed;
    }

    public bool GetDuckHeld()
    {
        return duckHeld;
    }

    public bool GetThrowPressed()
    {
        return throwPressed;
    }

    public bool GetThrowHeld()
    {
        return throwHeld;
    }

    public bool GetCatchPressed()
    {
        return catchPressed;
    }

    public bool GetPickupPressed()
    {
        return pickupPressed;
    }

    public bool GetDashPressed()
    {
        return dashPressed;
    }

    public bool GetUltimatePressed()
    {
        return ultimatePressed;
    }

    // NEW: Get ultimate key held state
    public bool GetUltimateHeld()
    {
        return ultimateHeld;
    }

    // NEW: Get ultimate key released state
    public bool GetUltimateReleased()
    {
        return ultimateReleased;
    }

    public bool GetTrickPressed()
    {
        return trickPressed;
    }

    public bool GetTreatPressed()
    {
        return treatPressed;
    }

    // ===========================================
    // NETWORK STATE GETTERS
    // ===========================================

    public bool IsMyCharacter()
    {
        return isMyCharacter;
    }

    public bool IsNetworkReady()
    {
        return isNetworkReady;
    }

    public PlayerInputType GetPlayerType()
    {
        return playerType;
    }

    // ===========================================
    // UTILITY METHODS
    // ===========================================

    public void SetPlayerType(PlayerInputType newType)
    {
        playerType = newType;
        SetupKeyMappings();
    }

    public void EnableDebugInfo(bool enable)
    {
        showDebugInfo = enable;
    }

    public void ForceOwnershipRefresh()
    {
        DetermineOwnership();
    }

    // Configure this input handler for AI control (no human inputs)
    public void ConfigureForAI()
    {
        enableKeyboardInput = false;
        enableMobileInput = false;
        enableGamepadInput = false; // CRITICAL: Disable gamepad input for AI
        isMyCharacter = false; // Ensure AI doesn't respond to human input
    }


    // ===========================================
    // GIZMOS FOR DEBUG VISUALIZATION
    // ===========================================

    void OnDrawGizmosSelected()
    {
        if (!showDebugInfo) return;

        // Draw input state visualization
        Gizmos.color = isMyCharacter ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);

        // Draw horizontal input as arrow
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            Vector3 direction = Vector3.right * horizontalInput;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, direction);
        }
    }
}