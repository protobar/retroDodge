using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Enhanced MobileUIManager with complete button support including Ultimate/Trick/Treat
/// Plus Duck System integration for visual feedback
/// </summary>
public class MobileUIManager : MonoBehaviour
{
    #region Singleton
    private static MobileUIManager instance;
    public static MobileUIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<MobileUIManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("MobileUIManager");
                    instance = go.AddComponent<MobileUIManager>();
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeUI();
    }
    #endregion

    [Header("UI References")]
    [SerializeField] private Canvas mobileCanvas;
    [SerializeField] private GameObject mobileControlsPanel;

    [Header("Movement Buttons")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Basic Action Buttons")]
    [SerializeField] private Button jumpButton;
    [SerializeField] private Button throwButton;
    [SerializeField] private Button catchButton;
    [SerializeField] private Button pickupButton;
    [SerializeField] private Button duckButton;

    [Header("Special Ability Buttons (NEW)")]
    [SerializeField] private Button dashButton;
    [SerializeField] private Button ultimateButton;
    [SerializeField] private Button trickButton;
    [SerializeField] private Button treatButton;

    [Header("Duck System Visual Feedback (Optional)")]
    [SerializeField] private Image duckButtonImage;
    [SerializeField] private Image duckCooldownOverlay;
    [SerializeField] private Text duckTimerText;
    [SerializeField] private Color duckNormalColor = Color.white;
    [SerializeField] private Color duckActiveColor = Color.green;
    [SerializeField] private Color duckCooldownColor = Color.red;
    [SerializeField] private Color duckBlockedColor = Color.gray;

    [Header("Ability Button Visual Feedback (Optional)")]
    [SerializeField] private Image ultimateButtonImage;
    [SerializeField] private Image trickButtonImage;
    [SerializeField] private Image treatButtonImage;
    [SerializeField] private Color abilityReadyColor = Color.cyan;
    [SerializeField] private Color abilityNotReadyColor = Color.gray;
    [SerializeField] private Color abilityCooldownColor = Color.red;

    [Header("Settings")]
    [SerializeField] private bool autoShowOnMobile = true;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoSetupButtons = true;
    [SerializeField] private bool enableDuckSystemFeedback = false; // Optional feature
    [SerializeField] private bool enableAbilityFeedback = false; // Optional feature

    // Runtime assignment
    private PlayerInputHandler assignedInputHandler;
    private DuckSystem assignedDuckSystem;
    private PlayerCharacter assignedPlayerCharacter;
    private bool isInputHandlerAssigned = false;

    // Button states for hold detection
    private bool isLeftHeld = false;
    private bool isRightHeld = false;
    private bool isThrowHeld = false;
    private bool isDuckHeld = false;

    void Start()
    {
        SetupMobileUI();
        
        // CRITICAL: Try to auto-find and assign local player's input handler
        AutoFindAndAssignInputHandler();

        if (autoShowOnMobile)
        {
            bool shouldShowMobile = false;

#if UNITY_ANDROID || UNITY_IOS
            shouldShowMobile = true;
#else
            if (assignedInputHandler != null)
            {
                shouldShowMobile = CheckIfMobileModeForced();
            }
#endif

            ShowMobileControls(shouldShowMobile);

            if (debugMode)
            {
                Debug.Log($"MobileUIManager: Auto-show mobile controls = {shouldShowMobile}");
            }
        }
    }
    
    /// <summary>
    /// Automatically find and assign the local player's input handler
    /// </summary>
    void AutoFindAndAssignInputHandler()
    {
        // If already assigned, don't search
        if (isInputHandlerAssigned && assignedInputHandler != null) return;
        
        // Find all PlayerInputHandlers in scene
        PlayerInputHandler[] allHandlers = FindObjectsOfType<PlayerInputHandler>();
        
        foreach (var handler in allHandlers)
        {
            // Check if this handler is for the local player
            // We'll check if it has enableMobileInput and is the local player's character
            if (handler != null && handler.enableMobileInput)
            {
                // Try to determine if this is the local player
                // In offline mode, first non-AI character is usually the player
                // In online mode, check photonView.IsMine
                bool isLocalPlayer = true;
                
                // Check if it's an AI character (shouldn't be assigned)
                var playerChar = handler.GetComponent<PlayerCharacter>();
                if (playerChar != null)
                {
                    bool isAI = handler.gameObject.name.Contains("AI") || 
                               handler.GetComponent("AIControllerBrain") != null;
                    if (isAI) continue; // Skip AI characters
                }
                
                // Assign this handler
                AssignInputHandler(handler);
                
                if (debugMode)
                {
                    Debug.Log($"MobileUIManager: Auto-assigned input handler from {handler.gameObject.name}");
                }
                
                break; // Only assign one (the local player)
            }
        }
    }

    void Update()
    {
        // Update visual feedback systems (only if enabled and references exist)
        if (enableDuckSystemFeedback && assignedDuckSystem != null && duckButtonImage != null)
        {
            UpdateDuckButtonFeedback();
        }

        if (enableAbilityFeedback && assignedPlayerCharacter != null)
        {
            UpdateAbilityButtonFeedback();
        }
        
        // Note: Dash button visibility is updated when input handler is assigned
        // and can be manually refreshed via RefreshDashButtonVisibility()
        // No need to check every frame unless character data changes dynamically
    }
    
    /// <summary>
    /// Update dash button visibility based on character's dash ability
    /// Only show dash button if character has canDash = true (e.g., Nova)
    /// </summary>
    void UpdateDashButtonVisibility()
    {
        if (dashButton == null) return;
        
        bool shouldShowDash = false;
        
        if (assignedPlayerCharacter != null)
        {
            var characterData = assignedPlayerCharacter.GetCharacterData();
            if (characterData != null)
            {
                shouldShowDash = characterData.canDash;
                
                // Only update if state changed to avoid unnecessary SetActive calls
                if (dashButton.gameObject.activeSelf != shouldShowDash)
                {
                    dashButton.gameObject.SetActive(shouldShowDash);
                    
                    if (debugMode)
                    {
                        Debug.Log($"MobileUIManager: Dash button visibility updated - Character: {characterData.characterName}, canDash: {characterData.canDash}, Showing: {shouldShowDash}");
                    }
                }
            }
            else
            {
                // Character data not loaded yet - hide dash button
                if (dashButton.gameObject.activeSelf)
                {
                    dashButton.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // No player character assigned - hide dash button
            if (dashButton.gameObject.activeSelf)
            {
                dashButton.gameObject.SetActive(false);
            }
        }
    }

    bool CheckIfMobileModeForced()
    {
        var field = assignedInputHandler.GetType().GetField("forceMobileMode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            return (bool)field.GetValue(assignedInputHandler);
        }

        return false;
    }

    void InitializeUI()
    {
        if (mobileCanvas == null)
        {
            CreateMobileCanvas();
        }

        if (mobileCanvas != null)
        {
            DontDestroyOnLoad(mobileCanvas.gameObject);
        }
    }

    void CreateMobileCanvas()
    {
        GameObject canvasGO = new GameObject("MobileCanvas");
        mobileCanvas = canvasGO.AddComponent<Canvas>();
        mobileCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mobileCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        CreateMobileControlsPanel();

        if (debugMode)
        {
            Debug.Log("MobileUIManager: Created mobile canvas and controls");
        }
    }

    void CreateMobileControlsPanel()
    {
        if (mobileCanvas == null) return;

        GameObject panelGO = new GameObject("MobileControlsPanel");
        panelGO.transform.SetParent(mobileCanvas.transform, false);

        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        mobileControlsPanel = panelGO;

        if (debugMode)
        {
            Debug.Log("MobileUIManager: Created mobile controls panel");
        }
    }

    void SetupMobileUI()
    {
        if (mobileControlsPanel == null || !autoSetupButtons) return;

        // Setup movement buttons
        SetupMovementButton(leftButton, true);
        SetupMovementButton(rightButton, false);

        // Setup action buttons
        SetupActionButton(jumpButton, ActionType.Jump);
        SetupActionButton(pickupButton, ActionType.Pickup);
        SetupActionButton(catchButton, ActionType.Catch);
        SetupActionButton(dashButton, ActionType.Dash);

        // Setup hold buttons
        SetupHoldButton(throwButton, ActionType.Throw);
        SetupHoldButton(duckButton, ActionType.Duck);

        // ENHANCED: Setup ability buttons (Ultimate/Trick/Treat)
        SetupActionButton(ultimateButton, ActionType.Ultimate);
        SetupActionButton(trickButton, ActionType.Trick);
        SetupActionButton(treatButton, ActionType.Treat);

        if (debugMode)
        {
            Debug.Log("MobileUIManager: Mobile UI setup complete with all buttons");
        }
    }

    void SetupMovementButton(Button button, bool isLeft)
    {
        if (button == null) return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        if (trigger.triggers.Count == 0)
        {
            // Pointer Down
            EventTrigger.Entry downEntry = new EventTrigger.Entry();
            downEntry.eventID = EventTriggerType.PointerDown;
            downEntry.callback.AddListener((data) => OnMovementDown(isLeft));
            trigger.triggers.Add(downEntry);

            // Pointer Up
            EventTrigger.Entry upEntry = new EventTrigger.Entry();
            upEntry.eventID = EventTriggerType.PointerUp;
            upEntry.callback.AddListener((data) => OnMovementUp(isLeft));
            trigger.triggers.Add(upEntry);

            // Pointer Exit
            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => OnMovementUp(isLeft));
            trigger.triggers.Add(exitEntry);

            if (debugMode)
            {
                Debug.Log($"MobileUIManager: Auto-setup {(isLeft ? "Left" : "Right")} movement button");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"MobileUIManager: {(isLeft ? "Left" : "Right")} button already has events - preserving manual setup");
        }
    }

    void SetupActionButton(Button button, ActionType actionType)
    {
        if (button == null) return;

        bool hasExistingListeners = button.onClick.GetPersistentEventCount() > 0;

        if (!hasExistingListeners)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnActionPressed(actionType));

            if (debugMode)
            {
                Debug.Log($"MobileUIManager: Auto-setup {actionType} action button");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"MobileUIManager: {actionType} button already has manual listeners - preserving setup");
        }
    }

    void SetupHoldButton(Button button, ActionType actionType)
    {
        if (button == null) return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        bool hasManualOnClick = button.onClick.GetPersistentEventCount() > 0;

        if (trigger.triggers.Count == 0 && !hasManualOnClick)
        {
            // Pointer Down
            EventTrigger.Entry downEntry = new EventTrigger.Entry();
            downEntry.eventID = EventTriggerType.PointerDown;
            downEntry.callback.AddListener((data) => OnHoldDown(actionType));
            trigger.triggers.Add(downEntry);

            // Pointer Up
            EventTrigger.Entry upEntry = new EventTrigger.Entry();
            upEntry.eventID = EventTriggerType.PointerUp;
            upEntry.callback.AddListener((data) => OnHoldUp(actionType));
            trigger.triggers.Add(upEntry);

            // Pointer Exit
            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => OnHoldUp(actionType));
            trigger.triggers.Add(exitEntry);

            if (debugMode)
            {
                Debug.Log($"MobileUIManager: Auto-setup {actionType} hold button");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"MobileUIManager: {actionType} button already has events - preserving manual setup");
        }
    }

    void UpdateDuckButtonFeedback()
    {
        if (duckButtonImage == null || assignedDuckSystem == null) return;

        if (assignedDuckSystem.IsDucking())
        {
            duckButtonImage.color = duckActiveColor;

            if (duckTimerText != null)
            {
                float timeRemaining = assignedDuckSystem.GetDuckTimeRemaining();
                duckTimerText.text = $"{timeRemaining:F1}s";
                duckTimerText.gameObject.SetActive(true);
            }

            if (duckCooldownOverlay != null)
            {
                float progress = assignedDuckSystem.GetDuckProgress();
                duckCooldownOverlay.fillAmount = progress;
                duckCooldownOverlay.color = Color.Lerp(Color.clear, duckActiveColor, progress);
            }
        }
        else if (assignedDuckSystem.IsInCooldown())
        {
            duckButtonImage.color = duckCooldownColor;

            if (duckTimerText != null)
            {
                float cooldownRemaining = assignedDuckSystem.GetCooldownTimeRemaining();
                duckTimerText.text = $"{cooldownRemaining:F1}s";
                duckTimerText.gameObject.SetActive(true);
            }

            /*if (duckCooldownOverlay != null)
            {
                float progress = assignedDuckSystem.GetCooldownProgress();
                duckCooldownOverlay.fillAmount = 1f - progress;
                duckCooldownOverlay.color = Color.Lerp(duckCooldownColor, Color.clear, progress);
            }*/
        }
        else if (!assignedDuckSystem.CanDuck())
        {
            duckButtonImage.color = duckBlockedColor;

            if (duckTimerText != null)
            {
                duckTimerText.gameObject.SetActive(false);
            }
        }
        else
        {
            duckButtonImage.color = duckNormalColor;

            if (duckTimerText != null)
            {
                duckTimerText.gameObject.SetActive(false);
            }

            if (duckCooldownOverlay != null)
            {
                duckCooldownOverlay.fillAmount = 0f;
            }
        }
    }

    void UpdateAbilityButtonFeedback()
    {
        if (assignedPlayerCharacter == null) return;

        // Update Ultimate button
        if (ultimateButtonImage != null)
        {
            float ultimateCharge = assignedPlayerCharacter.GetUltimateChargePercentage();
            bool ultimateReady = ultimateCharge >= 1f;

            ultimateButtonImage.color = ultimateReady ? abilityReadyColor :
                Color.Lerp(abilityNotReadyColor, abilityReadyColor, ultimateCharge);
        }

        // Update Trick button
        if (trickButtonImage != null)
        {
            float trickCharge = assignedPlayerCharacter.GetTrickChargePercentage();
            bool trickReady = trickCharge >= 1f;

            trickButtonImage.color = trickReady ? abilityReadyColor :
                Color.Lerp(abilityNotReadyColor, abilityReadyColor, trickCharge);
        }

        // Update Treat button
        if (treatButtonImage != null)
        {
            float treatCharge = assignedPlayerCharacter.GetTreatChargePercentage();
            bool treatReady = treatCharge >= 1f;

            treatButtonImage.color = treatReady ? abilityReadyColor :
                Color.Lerp(abilityNotReadyColor, abilityReadyColor, treatCharge);
        }
    }

    #region Input Event Handlers

    void OnMovementDown(bool isLeft)
    {
        if (debugMode)
        {
            Debug.Log($"MobileUIManager: Movement {(isLeft ? "Left" : "Right")} Down - Input Handler Assigned: {isInputHandlerAssigned}");
        }

        if (!isInputHandlerAssigned)
        {
            Debug.LogWarning("MobileUIManager: No input handler assigned! Cannot send input to player.");
            return;
        }

        if (isLeft)
        {
            isLeftHeld = true;
            assignedInputHandler.OnMobileLeftDown();
            if (debugMode) Debug.Log("MobileUIManager: Called OnMobileLeftDown()");
        }
        else
        {
            isRightHeld = true;
            assignedInputHandler.OnMobileRightDown();
            if (debugMode) Debug.Log("MobileUIManager: Called OnMobileRightDown()");
        }
    }

    void OnMovementUp(bool isLeft)
    {
        if (!isInputHandlerAssigned) return;

        if (isLeft)
        {
            isLeftHeld = false;
            assignedInputHandler.OnMobileLeftUp();
        }
        else
        {
            isRightHeld = false;
            assignedInputHandler.OnMobileRightUp();
        }

        if (debugMode)
        {
            Debug.Log($"MobileUIManager: Movement {(isLeft ? "Left" : "Right")} Up");
        }
    }

    void OnActionPressed(ActionType actionType)
    {
        if (!isInputHandlerAssigned)
        {
            Debug.LogWarning("MobileUIManager: No input handler assigned! Cannot send action input.");
            return;
        }

        switch (actionType)
        {
            case ActionType.Jump:
                assignedInputHandler.OnMobileJump();
                break;
            case ActionType.Pickup:
                assignedInputHandler.OnMobilePickup();
                break;
            case ActionType.Catch:
                assignedInputHandler.OnMobileCatch();
                break;
            case ActionType.Dash:
                assignedInputHandler.OnMobileDash();
                break;
            // ENHANCED: Add Ultimate/Trick/Treat cases
            case ActionType.Ultimate:
                assignedInputHandler.OnMobileUltimate();
                break;
            case ActionType.Trick:
                assignedInputHandler.OnMobileTrick();
                break;
            case ActionType.Treat:
                assignedInputHandler.OnMobileTreat();
                break;
        }

        if (debugMode)
        {
            Debug.Log($"MobileUIManager: Action {actionType} Pressed");
        }
    }

    void OnHoldDown(ActionType actionType)
    {
        if (!isInputHandlerAssigned)
        {
            Debug.LogWarning("MobileUIManager: No input handler assigned! Cannot send hold input.");
            return;
        }

        switch (actionType)
        {
            case ActionType.Throw:
                isThrowHeld = true;
                assignedInputHandler.OnMobileThrowDown();
                break;
            case ActionType.Duck:
                // ENHANCED: Check duck system before allowing duck
                if (assignedDuckSystem == null || assignedDuckSystem.CanDuck())
                {
                    isDuckHeld = true;
                    assignedInputHandler.OnMobileDuckDown();
                }
                else if (debugMode)
                {
                    Debug.Log("Duck blocked by duck system");
                }
                break;
        }

        if (debugMode)
        {
            Debug.Log($"MobileUIManager: Hold {actionType} Down");
        }
    }

    void OnHoldUp(ActionType actionType)
    {
        if (!isInputHandlerAssigned) return;

        switch (actionType)
        {
            case ActionType.Throw:
                isThrowHeld = false;
                assignedInputHandler.OnMobileThrowUp();
                break;
            case ActionType.Duck:
                isDuckHeld = false;
                assignedInputHandler.OnMobileDuckUp();
                break;
        }

        if (debugMode)
        {
            Debug.Log($"MobileUIManager: Hold {actionType} Up");
        }
    }

    #endregion

    #region Public Manual Input Methods

    /// <summary>
    /// Public method for manual UI event assignment
    /// </summary>
    public void OnMovementDownPublic(bool isLeft)
    {
        OnMovementDown(isLeft);
    }

    /// <summary>
    /// Public method for manual UI event assignment
    /// </summary>
    public void OnMovementUpPublic(bool isLeft)
    {
        OnMovementUp(isLeft);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Jump
    /// </summary>
    public void OnJumpPressed()
    {
        OnActionPressed(ActionType.Jump);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Catch
    /// </summary>
    public void OnCatchPressed()
    {
        OnActionPressed(ActionType.Catch);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Pickup
    /// </summary>
    public void OnPickupPressed()
    {
        OnActionPressed(ActionType.Pickup);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Dash
    /// </summary>
    public void OnDashPressed()
    {
        OnActionPressed(ActionType.Dash);
    }

    // ENHANCED: Add missing public methods for Ultimate/Trick/Treat
    /// <summary>
    /// Public method for manual UI event assignment - Ultimate
    /// </summary>
    public void OnUltimatePressed()
    {
        OnActionPressed(ActionType.Ultimate);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Trick
    /// </summary>
    public void OnTrickPressed()
    {
        OnActionPressed(ActionType.Trick);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Treat
    /// </summary>
    public void OnTreatPressed()
    {
        OnActionPressed(ActionType.Treat);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Throw Down
    /// </summary>
    public void OnThrowDown()
    {
        OnHoldDown(ActionType.Throw);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Throw Up
    /// </summary>
    public void OnThrowUp()
    {
        OnHoldUp(ActionType.Throw);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Duck Down
    /// </summary>
    public void OnDuckDown()
    {
        OnHoldDown(ActionType.Duck);
    }

    /// <summary>
    /// Public method for manual UI event assignment - Duck Up
    /// </summary>
    public void OnDuckUp()
    {
        OnHoldUp(ActionType.Duck);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Enhanced assignment with duck system and player character integration
    /// </summary>
    public void AssignInputHandler(PlayerInputHandler inputHandler)
    {
        assignedInputHandler = inputHandler;
        isInputHandlerAssigned = (inputHandler != null);

        // ENHANCED: Also get duck system and player character references
        if (inputHandler != null)
        {
            // Always get player character for dash button visibility
            assignedPlayerCharacter = inputHandler.GetComponent<PlayerCharacter>();
            
            if (enableDuckSystemFeedback)
            {
                assignedDuckSystem = inputHandler.GetComponent<DuckSystem>();
                if (assignedDuckSystem == null)
                {
                    Debug.LogWarning("MobileUIManager: Duck system feedback enabled but no DuckSystem found on player!");
                }
            }

            if (enableAbilityFeedback)
            {
                if (assignedPlayerCharacter == null)
                {
                    Debug.LogWarning("MobileUIManager: Ability feedback enabled but no PlayerCharacter found on player!");
                }
            }
            
            // CRITICAL: Update dash button visibility based on character
            UpdateDashButtonVisibility();
        }

        if (debugMode)
        {
            Debug.Log($"MobileUIManager: Input handler assigned - {(isInputHandlerAssigned ? "SUCCESS" : "NULL")}");
            if (enableDuckSystemFeedback)
                Debug.Log($"MobileUIManager: Duck System: {(assignedDuckSystem != null ? "Found" : "None")}");
            if (enableAbilityFeedback)
                Debug.Log($"MobileUIManager: Player Character: {(assignedPlayerCharacter != null ? "Found" : "None")}");
        }

        // Auto-show controls when local player is assigned
        if (isInputHandlerAssigned)
        {
            bool shouldShowMobile = false;

#if UNITY_ANDROID || UNITY_IOS
            shouldShowMobile = true;
#else
            shouldShowMobile = inputHandler.enableMobileInput;
#endif

            if (autoShowOnMobile && shouldShowMobile)
            {
                ShowMobileControls(true);

                if (debugMode)
                {
                    Debug.Log($"MobileUIManager: Mobile controls shown (Mobile input enabled: {inputHandler.enableMobileInput})");
                }
            }
        }
    }

    /// <summary>
    /// Clear the assigned input handler
    /// Call this when the local player is destroyed
    /// </summary>
    public void ClearInputHandler()
    {
        assignedInputHandler = null;
        assignedDuckSystem = null;
        assignedPlayerCharacter = null;
        isInputHandlerAssigned = false;

        ReleaseAllInputs();

        if (debugMode)
        {
            Debug.Log("MobileUIManager: Input handler cleared");
        }
    }

    /// <summary>
    /// Show or hide mobile controls
    /// </summary>
    public void ShowMobileControls(bool show)
    {
        if (mobileControlsPanel != null)
        {
            mobileControlsPanel.SetActive(show);

            if (debugMode)
            {
                Debug.Log($"MobileUIManager: Mobile controls {(show ? "shown" : "hidden")}");
            }
        }
    }

    /// <summary>
    /// Check if mobile controls are currently visible
    /// </summary>
    public bool AreMobileControlsVisible()
    {
        return mobileControlsPanel != null && mobileControlsPanel.activeSelf;
    }

    /// <summary>
    /// Check if an input handler is currently assigned
    /// </summary>
    public bool HasAssignedInputHandler()
    {
        return isInputHandlerAssigned;
    }

    /// <summary>
    /// Get the currently assigned input handler
    /// </summary>
    public PlayerInputHandler GetAssignedInputHandler()
    {
        return assignedInputHandler;
    }

    /// <summary>
    /// Enable or disable automatic button setup (useful if you want full manual control)
    /// </summary>
    public void SetAutoSetupButtons(bool enable)
    {
        autoSetupButtons = enable;
    }

    // ENHANCED: Duck system specific methods
    public void SetDuckSystemFeedbackEnabled(bool enabled)
    {
        enableDuckSystemFeedback = enabled;
    }

    public void SetAbilityFeedbackEnabled(bool enabled)
    {
        enableAbilityFeedback = enabled;
    }

    public DuckSystem GetAssignedDuckSystem()
    {
        return assignedDuckSystem;
    }

    public PlayerCharacter GetAssignedPlayerCharacter()
    {
        return assignedPlayerCharacter;
    }
    
    /// <summary>
    /// Manually update dash button visibility
    /// Call this when character changes or after assigning input handler
    /// </summary>
    public void RefreshDashButtonVisibility()
    {
        UpdateDashButtonVisibility();
    }

    #endregion

    #region Utility Methods

    void ReleaseAllInputs()
    {
        if (!isInputHandlerAssigned) return;

        // Release movement inputs
        if (isLeftHeld)
        {
            assignedInputHandler.OnMobileLeftUp();
            isLeftHeld = false;
        }

        if (isRightHeld)
        {
            assignedInputHandler.OnMobileRightUp();
            isRightHeld = false;
        }

        // Release hold inputs
        if (isThrowHeld)
        {
            assignedInputHandler.OnMobileThrowUp();
            isThrowHeld = false;
        }

        if (isDuckHeld)
        {
            assignedInputHandler.OnMobileDuckUp();
            isDuckHeld = false;
        }
    }

    void OnDestroy()
    {
        ReleaseAllInputs();

        if (instance == this)
        {
            instance = null;
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ReleaseAllInputs();
        }
    }

    #endregion

    // ENHANCED: Complete ActionType enum with all abilities
    private enum ActionType
    {
        Jump,
        Throw,
        Catch,
        Pickup,
        Duck,
        Dash,      // Added
        Ultimate,  // Added
        Trick,     // Added
        Treat      // Added
    }
}