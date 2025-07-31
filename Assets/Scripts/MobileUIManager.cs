using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    [Header("Action Buttons")]
    [SerializeField] private Button jumpButton;
    [SerializeField] private Button throwButton;
    [SerializeField] private Button catchButton;
    [SerializeField] private Button pickupButton;
    [SerializeField] private Button duckButton;

    [Header("Settings")]
    [SerializeField] private bool autoShowOnMobile = true;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoSetupButtons = true; // NEW: Control automatic setup

    // Runtime assignment
    private PlayerInputHandler assignedInputHandler;
    private bool isInputHandlerAssigned = false;

    // Button states for hold detection
    private bool isLeftHeld = false;
    private bool isRightHeld = false;
    private bool isThrowHeld = false;
    private bool isDuckHeld = false;

    void Start()
    {
        SetupMobileUI();

        // Auto-show on mobile platforms or when forced
        if (autoShowOnMobile)
        {
            bool shouldShowMobile = false;

#if UNITY_ANDROID || UNITY_IOS
            shouldShowMobile = true;
#else
            // Check if any assigned input handler has force mobile mode
            if (assignedInputHandler != null)
            {
                // Check if the assigned handler is forcing mobile mode
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

    bool CheckIfMobileModeForced()
    {
        // Use reflection to check the forceMobileMode field
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
        // Create mobile canvas if not assigned
        if (mobileCanvas == null)
        {
            CreateMobileCanvas();
        }

        // Ensure canvas persists across scenes
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

        // Add CanvasScaler for responsive design
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Add GraphicRaycaster
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create controls panel
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

        // Create buttons programmatically or assign them in inspector
        if (debugMode)
        {
            Debug.Log("MobileUIManager: Created mobile controls panel");
        }
    }

    void SetupMobileUI()
    {
        if (mobileControlsPanel == null || !autoSetupButtons) return;

        // Setup movement buttons
        SetupMovementButton(leftButton, true);  // true = left
        SetupMovementButton(rightButton, false); // false = right

        // Setup action buttons - FIXED: Only setup if no manual events exist
        SetupActionButton(jumpButton, ActionType.Jump);
        SetupActionButton(pickupButton, ActionType.Pickup);
        SetupActionButton(catchButton, ActionType.Catch);

        // Setup hold buttons (throw and duck)
        SetupHoldButton(throwButton, ActionType.Throw);
        SetupHoldButton(duckButton, ActionType.Duck);

        if (debugMode)
        {
            Debug.Log("MobileUIManager: Mobile UI setup complete");
        }
    }

    void SetupMovementButton(Button button, bool isLeft)
    {
        if (button == null) return;

        // Add EventTrigger for pointer down/up events
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        // Only clear if we're managing this automatically
        // If user has manually assigned events, preserve them
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

            // Pointer Exit (in case finger drags off button)
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

        // FIXED: Check if button already has manual listeners before removing them
        bool hasExistingListeners = button.onClick.GetPersistentEventCount() > 0;

        if (!hasExistingListeners)
        {
            // Use OnClick for instant actions
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

        // Add EventTrigger for hold behavior
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        // Check if button has manual onClick listeners
        bool hasManualOnClick = button.onClick.GetPersistentEventCount() > 0;

        // Only setup if not already configured AND no manual onClick events
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

    #region Public Manual Input Methods (for manual UI setup)

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
                isDuckHeld = true;
                assignedInputHandler.OnMobileDuckDown();
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

    #region Public API

    /// <summary>
    /// Assign a PlayerInputHandler to receive mobile input events
    /// Call this when the local player spawns
    /// </summary>
    public void AssignInputHandler(PlayerInputHandler inputHandler)
    {
        assignedInputHandler = inputHandler;
        isInputHandlerAssigned = (inputHandler != null);

        if (debugMode)
        {
            Debug.Log($"MobileUIManager: Input handler assigned - {(isInputHandlerAssigned ? "SUCCESS" : "NULL")}");
        }

        // Auto-show controls when local player is assigned
        if (isInputHandlerAssigned)
        {
            bool shouldShowMobile = false;

#if UNITY_ANDROID || UNITY_IOS
            shouldShowMobile = true;
#else
            // On desktop, check if mobile input is enabled on the assigned handler
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
        isInputHandlerAssigned = false;

        // Release all held inputs
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
        // Release all inputs when app is paused
        if (pauseStatus)
        {
            ReleaseAllInputs();
        }
    }

    #endregion

    // Enum for action types
    private enum ActionType
    {
        Jump,
        Throw,
        Catch,
        Pickup,
        Duck
    }
}