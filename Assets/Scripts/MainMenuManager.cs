using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// UPDATED MainMenuManager - Authentication moved to Connection scene
/// Now assumes user is already authenticated when this scene loads
/// Handles only matchmaking, custom rooms, and AI gameplay
/// </summary>
public class MainMenuManager : MonoBehaviourPunCallbacks
{
    [Header("=== MAIN UI PANELS ===")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject customRoomPanel;
    [SerializeField] private GameObject joinRoomPanel;
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private GameObject roomLobbyPanel;
    [SerializeField] private GameObject matchFoundPanel;
    [SerializeField] private GameObject reconnectionPanel;

    [Header("=== MAIN MENU ===")]
    [SerializeField] private TMP_Text playerInfoText;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button competitiveButton;
    [SerializeField] private Button customRoomButton;
    [SerializeField] private Button playWithAIButton;
    [SerializeField] private TMP_Text quickMatchButtonText;
    [SerializeField] private TMP_Text competitiveButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TMP_Text countdownText;
    
    [Header("=== PLAY WITH AI PANEL ===")]
    [SerializeField] private GameObject playWithAIPanel;
    [SerializeField] private TMP_Dropdown aiDifficultyDropdown;
    [SerializeField] private Button confirmPlayWithAIButton;
    [SerializeField] private Button backFromAIButton;

    [Header("=== LEVEL REQUIREMENT POPUP ===")]
    [SerializeField] private GameObject levelRequirementPopup;
    [SerializeField] private TMP_Text levelRequirementText;
    [SerializeField] private Button levelRequirementOkButton;

    [Header("=== CUSTOM ROOM - MAIN OPTIONS ===")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button backFromCustomRoomButton;

    [Header("=== CREATE ROOM PANEL ===")]
    [SerializeField] private TMP_Text generatedRoomCodeText;
    [SerializeField] private TMP_Dropdown matchLengthDropdown;
    [SerializeField] private TMP_Dropdown mapSelectionDropdown;
    [SerializeField] private Button generateRoomCodeButton;
    [SerializeField] private Button confirmCreateRoomButton;
    [SerializeField] private Button backFromCreateRoomButton;

    [Header("=== JOIN ROOM PANEL ===")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button confirmJoinRoomButton;
    [SerializeField] private Button backFromJoinRoomButton;

    [Header("=== ROOM LOBBY PANEL ===")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text roomInfoText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button backFromRoomLobbyButton;

    [Header("=== GAME SETTINGS ===")]
    [SerializeField] private string characterSelectionScene = "CharacterSelection";
    [SerializeField] private byte maxPlayers = 2;
    [SerializeField] private bool debugMode = true;
    [SerializeField] private float aiSearchTimer = 20.0f;
    
    [Header("=== COMPETITIVE MODE SETTINGS ===")]
    [SerializeField] private int competitiveMaxMatches = 9; // Best of 9 (editable)
    [SerializeField] private int competitiveLevelRequirement = 20; // Level requirement (editable)



    // FIXED: Matchmaking state management
    private bool isMatchmaking = false;
    private float matchmakingTime = 0f;
    private Coroutine matchmakingCoroutine;
    private Coroutine countdownCoroutine;
    private bool matchFound = false;
    private Coroutine aiFallbackCoroutine;
    private bool pendingAIFallback = false;
    private bool isDisconnectingForAI = false;
    
    // Competitive mode state management
    private bool isCompetitiveMatchmaking = false;
    private Coroutine competitiveMatchmakingCoroutine;

    // FIXED: Custom room state with proper room code system
    private bool isCreatingCustomRoom = false;
    private bool isJoiningCustomRoom = false;
    private string currentRoomCode = "";

    // FIXED: Constants for room properties
    private const string ROOM_CODE_KEY = "RC"; // Room Code
    private const string MATCH_LENGTH_KEY = "ML"; // Match Length
    private const string SELECTED_MAP_KEY = "SM"; // Selected Map
    // Note: Using RoomStateManager.ROOM_TYPE_KEY instead of local constant for consistency

    void Start()
    {
        SetupUI();
        SetupButtonListeners();
        SetupCustomRoomUI();

        // Check if user is authenticated
        if (!PlayFabAuthManager.Instance.IsAuthenticated)
        {
            Debug.LogError("[MAIN MENU] User not authenticated! Returning to Connection scene.");
            SceneManager.LoadScene("Connection");
            return;
        }

        // Check Photon connection
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateStatus("Reconnecting to servers...");
            PhotonNetwork.ConnectUsingSettings();
            ShowReconnectingPanel();
        }
        else
        {
            ShowMainMenu();
        }

        if (debugMode) Debug.Log("[MAIN MENU] Initialized - user authenticated");
        
        // Refresh profile display when returning from matches
        RefreshProfileDisplay();
        
        // Update competitive button state based on level
        UpdateCompetitiveButtonState();
    }
    
    /// <summary>
    /// Refresh the profile display when returning from matches
    /// This ensures data is up-to-date without unnecessary polling
    /// </summary>
    private void RefreshProfileDisplay()
    {
        // Find and refresh MainMenuProfileUI if it exists
        var profileUI = FindObjectOfType<RetroDodge.Progression.MainMenuProfileUI>();
        if (profileUI != null)
        {
            profileUI.ForceRefreshProfile();
            if (debugMode) Debug.Log("[MainMenuManager] Refreshed profile display after returning from match");
        }
    }

    /// <summary>
    /// Update competitive button state based on player level
    /// </summary>
    private void UpdateCompetitiveButtonState()
    {
        if (competitiveButton == null) return;

        bool isLevelRequirementMet = true;
        
        if (RetroDodge.Progression.PlayerDataManager.Instance != null && 
            RetroDodge.Progression.PlayerDataManager.Instance.IsDataLoaded())
        {
            var playerData = RetroDodge.Progression.PlayerDataManager.Instance.GetPlayerData();
            isLevelRequirementMet = playerData.currentLevel >= competitiveLevelRequirement;
        }

        // Grey out button if level requirement not met
        var buttonImage = competitiveButton.GetComponent<Image>();
        var buttonText = competitiveButtonText;
        
        if (buttonImage != null)
        {
            buttonImage.color = isLevelRequirementMet ? Color.white : Color.gray;
        }
        
        if (buttonText != null)
        {
            buttonText.color = isLevelRequirementMet ? Color.white : Color.gray;
        }
        
        competitiveButton.interactable = isLevelRequirementMet;
    }

    /// <summary>
    /// Show level requirement popup
    /// </summary>
    private void ShowLevelRequirementPopup(int currentLevel)
    {
        if (levelRequirementPopup != null)
        {
            levelRequirementPopup.SetActive(true);
            
            if (levelRequirementText != null)
            {
                levelRequirementText.text = $"Competitive mode requires Level {competitiveLevelRequirement}.\nYou are currently Level {currentLevel}.\n\nPlay more matches to level up!";
            }
        }
    }

    /// <summary>
    /// Handle level requirement popup OK button
    /// </summary>
    private void OnLevelRequirementOkClicked()
    {
        if (levelRequirementPopup != null)
        {
            levelRequirementPopup.SetActive(false);
        }
    }

    #region UI Setup
    void SetupUI()
    {
        // Hide all panels except main menu initially
        mainMenuPanel.SetActive(false);
        customRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        createRoomPanel.SetActive(false);
        roomLobbyPanel.SetActive(false);
        playWithAIPanel?.SetActive(false);
        matchFoundPanel?.SetActive(false);
        loadingIndicator.SetActive(false);

        ResetQuickMatchButton();
    }

    private void ShowReconnectingPanel()
    {
        reconnectionPanel.SetActive(true);
    }

    private void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        UpdatePlayerInfo();
        reconnectionPanel.SetActive(false);
        UpdateStatus($"Welcome back, {PlayFabAuthManager.Instance.PlayerDisplayName}!");
    }

    private void UpdatePlayerInfo()
    {
        if (playerInfoText != null)
        {
            string guestStatus = PlayFabAuthManager.Instance.IsGuest ? " (Guest)" : "";
            playerInfoText.text = $"{PlayFabAuthManager.Instance.PlayerDisplayName}{guestStatus}";
        }
    }

    void SetupButtonListeners()
    {
        quickMatchButton?.onClick.AddListener(OnQuickMatchClicked);
        competitiveButton?.onClick.AddListener(OnCompetitiveClicked);
        customRoomButton?.onClick.AddListener(OnCustomRoomClicked);
        playWithAIButton?.onClick.AddListener(OnPlayWithAIClicked);
        createRoomButton?.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton?.onClick.AddListener(OnJoinRoomClicked);
        backFromCustomRoomButton?.onClick.AddListener(OnBackFromCustomRoomClicked);
        backFromJoinRoomButton?.onClick.AddListener(OnBackFromJoinRoomClicked);
        backFromCreateRoomButton?.onClick.AddListener(OnBackFromCreateRoomClicked);
        backFromRoomLobbyButton?.onClick.AddListener(OnBackFromRoomLobbyClicked);
        confirmCreateRoomButton?.onClick.AddListener(OnConfirmCreateRoomClicked);
        confirmJoinRoomButton?.onClick.AddListener(OnConfirmJoinRoomClicked);
        startGameButton?.onClick.AddListener(OnStartGameClicked);
        leaveRoomButton?.onClick.AddListener(OnLeaveRoomClicked);
        generateRoomCodeButton?.onClick.AddListener(OnGenerateRoomCodeClicked);
        confirmPlayWithAIButton?.onClick.AddListener(OnConfirmPlayWithAIClicked);
        backFromAIButton?.onClick.AddListener(OnBackFromAIClicked);
        levelRequirementOkButton?.onClick.AddListener(OnLevelRequirementOkClicked);
    }

    void SetupCustomRoomUI()
    {
        // Setup match length dropdown
        if (matchLengthDropdown != null)
        {
            matchLengthDropdown.ClearOptions();
            var options = new List<string> { "30 seconds", "1 minute", "2 minutes", "3 minutes", "5 minutes" };
            matchLengthDropdown.AddOptions(options);
            matchLengthDropdown.value = 1; // Default to 1 minute
        }
        
        // Setup AI difficulty dropdown
        if (aiDifficultyDropdown != null)
        {
            aiDifficultyDropdown.ClearOptions();
            var aiOptions = new List<string> { "Easy", "Normal", "Hard" };
            aiDifficultyDropdown.AddOptions(aiOptions);
            aiDifficultyDropdown.value = 1; // Default to Normal
        }

        // Setup map selection dropdown - using fallback if MapRegistry doesn't exist
        if (mapSelectionDropdown != null)
        {
            mapSelectionDropdown.ClearOptions();
            var mapNames = new List<string> { "Arena1", "Arena2", "Arena3" }; // Fallback maps
            mapSelectionDropdown.AddOptions(mapNames);
            mapSelectionDropdown.value = 0;
        }

        if (generatedRoomCodeText != null)
        {
            generatedRoomCodeText.text = "Click 'Generate Code' to create room";
        }

        // Setup level requirement popup
        if (levelRequirementPopup != null)
        {
            levelRequirementPopup.SetActive(false);
        }
    }
    #endregion

    #region Photon Connection Management
    // Connection is now handled in Connection scene
    // This section only handles reconnection if needed
    #endregion

    #region FIXED: Quick Match with proper PUN2 patterns
    void OnQuickMatchClicked()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateStatus("Not connected to servers!");
            return;
        }

        if (isMatchmaking)
            CancelMatchmaking();
        else
            StartQuickMatch();
    }
    
    #region Competitive Mode Implementation
    void OnCompetitiveClicked()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateStatus("Not connected to servers!");
            return;
        }

        // Check level requirement first
        if (RetroDodge.Progression.PlayerDataManager.Instance != null && 
            RetroDodge.Progression.PlayerDataManager.Instance.IsDataLoaded())
        {
            var playerData = RetroDodge.Progression.PlayerDataManager.Instance.GetPlayerData();
            if (playerData.currentLevel < competitiveLevelRequirement)
            {
                ShowLevelRequirementPopup(playerData.currentLevel);
                return;
            }
        }
        else
        {
            UpdateStatus("Unable to verify level requirement. Please try again.");
            return;
        }

        if (isCompetitiveMatchmaking)
            CancelCompetitiveMatchmaking();
        else
            StartCompetitiveMatch();
    }
    
    void StartCompetitiveMatch()
    {
        
        isCompetitiveMatchmaking = true;
        matchFound = false;
        pendingAIFallback = false;
        matchmakingTime = 0f;
        
        if (competitiveButtonText != null)
            competitiveButtonText.text = "Cancel";
        
        UpdateStatus("Finding Competitive Match...");
        
        if (competitiveMatchmakingCoroutine != null)
            StopCoroutine(competitiveMatchmakingCoroutine);
        competitiveMatchmakingCoroutine = StartCoroutine(CompetitiveMatchmakingTimer());
        
        // Start AI fallback for competitive mode
        if (aiFallbackCoroutine != null) StopCoroutine(aiFallbackCoroutine);
        aiFallbackCoroutine = StartCoroutine(AIFallbackAfterDelay(aiSearchTimer));
        
        if (debugMode) Debug.Log($"[COMPETITIVE] Starting competitive matchmaking with JoinRandomRoom");
        
        // Join random competitive room
        Hashtable expectedProps = new Hashtable { { RoomStateManager.ROOM_TYPE_KEY, 2 } }; // Only join competitive rooms
        PhotonNetwork.JoinRandomRoom(expectedProps, maxPlayers);
        
        if (debugMode) Debug.Log($"[COMPETITIVE] JoinRandomRoom called with RoomType=2");
    }
    
    void CancelCompetitiveMatchmaking()
    {
        isCompetitiveMatchmaking = false;
        matchFound = false;
        
        if (competitiveMatchmakingCoroutine != null)
        {
            StopCoroutine(competitiveMatchmakingCoroutine);
            competitiveMatchmakingCoroutine = null;
        }
        
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        
        if (aiFallbackCoroutine != null)
        {
            StopCoroutine(aiFallbackCoroutine);
            aiFallbackCoroutine = null;
        }
        
        ResetCompetitiveButton();
        UpdateStatus("Competitive matchmaking cancelled");
        
        // Leave room if we're in one
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        
        // Hide match found panel
        matchFoundPanel?.SetActive(false);
    }
    
    IEnumerator CompetitiveMatchmakingTimer()
    {
        while (isCompetitiveMatchmaking && !matchFound)
        {
            matchmakingTime += Time.deltaTime;
            
            int minutes = Mathf.FloorToInt(matchmakingTime / 60);
            int seconds = Mathf.FloorToInt(matchmakingTime % 60);
            
            if (competitiveButtonText != null)
                competitiveButtonText.text = $"Cancel ({minutes:00}:{seconds:00})";
            
            yield return null;
        }
    }
    
    void ResetCompetitiveButton()
    {
        if (competitiveButtonText != null)
            competitiveButtonText.text = "Competitive";
    }
    
    Hashtable CreateCompetitiveRoomProperties()
    {
        return new Hashtable
        {
            { RoomStateManager.ROOM_TYPE_KEY, 2 }, // 2 = Competitive
            { RoomStateManager.ROOM_MATCH_LENGTH, 90 }, // 90 seconds for competitive
            { RoomStateManager.ROOM_SELECTED_MAP, "Arena1" }
        };
    }
    #endregion

    void StartQuickMatch()
    {
        isMatchmaking = true;
        matchFound = false;
        pendingAIFallback = false;
        matchmakingTime = 0f;
        quickMatchButtonText.text = "Cancel";

        UpdateStatus("Finding Match...");

        if (matchmakingCoroutine != null)
            StopCoroutine(matchmakingCoroutine);
        matchmakingCoroutine = StartCoroutine(MatchmakingTimer());

        // Start 20s fallback to AI
        if (aiFallbackCoroutine != null) StopCoroutine(aiFallbackCoroutine);
        aiFallbackCoroutine = StartCoroutine(AIFallbackAfterDelay(aiSearchTimer));

        if (debugMode) Debug.Log($"[QUICK MATCH] Starting with JoinRandomRoom");

        // FIXED: Use proper PUN2 pattern - JoinRandomRoom with room type filter
        Hashtable expectedProps = new Hashtable { { RoomStateManager.ROOM_TYPE_KEY, 0 } }; // Only join quick match rooms
        PhotonNetwork.JoinRandomRoom(expectedProps, maxPlayers);
    }

    Hashtable CreateQuickMatchProperties()
    {
        return new Hashtable
        {
            { RoomStateManager.ROOM_TYPE_KEY, 0 }, // 0 = Quick Match
            { MATCH_LENGTH_KEY, 60 }, // 60 seconds default
            { SELECTED_MAP_KEY, "Arena1" }
        };
    }

    void CancelMatchmaking()
    {
        isMatchmaking = false;
        matchFound = false;

        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
            matchmakingCoroutine = null;
        }

        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        if (aiFallbackCoroutine != null)
        {
            StopCoroutine(aiFallbackCoroutine);
            aiFallbackCoroutine = null;
        }

        ResetQuickMatchButton();
        UpdateStatus("Matchmaking cancelled");

        // Leave room if we're in one
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();

        // Hide match found panel
        matchFoundPanel?.SetActive(false);
    }
    
    void CancelAllMatchmaking()
    {
        CancelMatchmaking();
        CancelCompetitiveMatchmaking();
    }

    IEnumerator MatchmakingTimer()
    {
        while (isMatchmaking && !matchFound)
        {
            int minutes = (int)matchmakingTime / 60;
            int seconds = (int)matchmakingTime % 60;

            quickMatchButtonText.text = $"Cancel ({minutes:00}:{seconds:00})";

            // Animate dots for status
            int dots = ((int)matchmakingTime) % 4;
            UpdateStatus($"Finding Match{new string('.', dots)}");

            yield return new WaitForSeconds(1f);
            matchmakingTime += 1f;
        }
    }

    IEnumerator AIFallbackAfterDelay(float seconds)
    {
        float elapsed = 0f;
        while (isMatchmaking && !matchFound && elapsed < seconds)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (isMatchmaking && !matchFound)
        {
            // Begin safe fallback: leave room, disconnect, then enable OfflineMode
            isMatchmaking = false;
            pendingAIFallback = true;
            ResetQuickMatchButton();
            matchFoundPanel?.SetActive(false);

            // Avoid PUN auto scene sync during transition
            PhotonNetwork.AutomaticallySyncScene = false;

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
                yield break;
            }

            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
                yield break;
            }

            // If already disconnected, start offline AI immediately
            StartOfflineAIMode();
        }
    }

    void StartOfflineAIMode()
    {
        if (PhotonNetwork.IsConnected || PhotonNetwork.InRoom) return;

        // Set flag to prevent OnDisconnected from redirecting to Connection scene
        isDisconnectingForAI = true;

        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.AutomaticallySyncScene = false;

        // Configure AI session (Normal by default; random AI character at runtime)
        var cfg = RetroDodge.AISessionConfig.Instance;
        cfg.SetPlayWithAI(RetroDodge.AI.AIDifficulty.Normal, -1, -1);

        UpdateStatus("No players found. Starting character selection vs AI...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelection");
        pendingAIFallback = false;
        
        // Reset flag after scene load
        isDisconnectingForAI = false;
    }

    // FIXED: Proper countdown when match is found
    IEnumerator QuickMatchCountdown()
    {
        matchFoundPanel?.SetActive(true);

        // Countdown from 3 to 1
        for (int i = 3; i >= 1; i--)
        {
            if (countdownText != null)
                countdownText.text = i.ToString();

            UpdateStatus($"Match found! Starting in {i}...");
            yield return new WaitForSeconds(1f);
        }

        if (countdownText != null)
            countdownText.text = "GO!";

        UpdateStatus("Starting game...");
        yield return new WaitForSeconds(0.5f);

        // Only master client loads scene
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(characterSelectionScene);
        }
    }
    
    IEnumerator CompetitiveMatchCountdown()
    {
        matchFoundPanel?.SetActive(true);

        // Countdown from 3 to 1
        for (int i = 3; i >= 1; i--)
        {
            if (countdownText != null)
                countdownText.text = i.ToString();

            UpdateStatus($"Competitive match found! Starting in {i}...");
            yield return new WaitForSeconds(1f);
        }

        if (countdownText != null)
            countdownText.text = "GO!";

        UpdateStatus("Starting competitive series...");
        yield return new WaitForSeconds(0.5f);

        // Only master client loads scene
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(characterSelectionScene);
        }
        
        if (debugMode) Debug.Log("[COMPETITIVE] Loading character selection for competitive series");
    }

    void ResetQuickMatchButton()
    {
        quickMatchButtonText.text = "Find Match";
    }
    
    #region Play with AI System
    void OnPlayWithAIClicked()
    {
        mainMenuPanel.SetActive(false);
        playWithAIPanel.SetActive(true);
        UpdateStatus("Select AI difficulty");
    }
    
    void OnBackFromAIClicked()
    {
        playWithAIPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        UpdateStatus($"Welcome back, {PhotonNetwork.NickName}!");
    }
    
    void OnConfirmPlayWithAIClicked()
    {
        if (aiDifficultyDropdown == null) return;
        
        // Get selected difficulty
        RetroDodge.AI.AIDifficulty selectedDifficulty = (RetroDodge.AI.AIDifficulty)aiDifficultyDropdown.value;
        
        // Configure AI session
        var cfg = RetroDodge.AISessionConfig.Instance;
        cfg.SetPlayWithAI(selectedDifficulty, -1, -1); // Player and AI characters will be selected in character selection
        
        UpdateStatus($"Preparing character selection vs {selectedDifficulty} AI...");
        
        // Disconnect first if connected, then start offline mode
        StartCoroutine(StartOfflineAISession());
    }
    
    IEnumerator StartOfflineAISession()
    {
        // Set flag to prevent OnDisconnected from redirecting to Connection scene
        isDisconnectingForAI = true;
        
        // Disconnect from any existing connection
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            while (PhotonNetwork.InRoom)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            while (PhotonNetwork.IsConnected)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Now start offline mode
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.AutomaticallySyncScene = false;
        
        UpdateStatus("Starting character selection vs AI...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelection");
        
        // Reset flag after scene load
        isDisconnectingForAI = false;
    }
    #endregion
    
    #endregion

    #region FIXED: Custom Room System with proper room code sharing
    void OnCustomRoomClicked()
    {
        mainMenuPanel.SetActive(false);
        customRoomPanel.SetActive(true);
        UpdateStatus("Choose an option");
    }

    void OnCreateRoomClicked()
    {
        customRoomPanel.SetActive(false);
        createRoomPanel.SetActive(true);
        UpdateStatus("Configure your room settings");
    }

    void OnJoinRoomClicked()
    {
        customRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(true);
        UpdateStatus("Enter room code to join");
    }

    void OnGenerateRoomCodeClicked()
    {
        currentRoomCode = GenerateRoomCode();
        if (generatedRoomCodeText != null)
        {
            generatedRoomCodeText.text = $"Room Code: {currentRoomCode}";
        }
        UpdateStatus($"Generated room code: {currentRoomCode}");
        if (debugMode) Debug.Log($"[ROOM CODE] Generated: {currentRoomCode}");
    }

    // FIXED: Create room with proper custom properties for lobby sharing
    void OnConfirmCreateRoomClicked()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            UpdateStatus("Please generate a room code first");
            return;
        }

        isCreatingCustomRoom = true;
        loadingIndicator.SetActive(true);
        UpdateStatus("Creating custom room...");

        // FIXED: Create room with properties visible in lobby
        Hashtable customProps = CreateCustomRoomProperties();

        RoomOptions roomOptions = new RoomOptions
        {
            IsVisible = true, // Must be visible for room codes to work
            IsOpen = true,
            MaxPlayers = maxPlayers,
            PlayerTtl = 5000, // FIXED: Shorter TTL to clean up faster
            EmptyRoomTtl = 2000, // FIXED: Much shorter empty room TTL
            CustomRoomProperties = customProps,
            CustomRoomPropertiesForLobby = new string[] { ROOM_CODE_KEY, RoomStateManager.ROOM_TYPE_KEY, MATCH_LENGTH_KEY, SELECTED_MAP_KEY }
        };

        // FIXED: Use a predictable room name based on room code
        string roomName = $"CustomRoom_{currentRoomCode}";
        PhotonNetwork.CreateRoom(roomName, roomOptions);

        if (debugMode) Debug.Log($"[CUSTOM ROOM] Creating room '{roomName}' with code '{currentRoomCode}'");
    }

    Hashtable CreateCustomRoomProperties()
    {
        // Get settings from UI
        int matchLength = GetMatchLengthFromDropdown();
        string selectedMap = GetSelectedMapFromDropdown();

        return new Hashtable
        {
            { ROOM_CODE_KEY, currentRoomCode },
            { RoomStateManager.ROOM_TYPE_KEY, 1 }, // 1 = Custom Room
            { MATCH_LENGTH_KEY, matchLength },
            { SELECTED_MAP_KEY, selectedMap }
        };
    }

    int GetMatchLengthFromDropdown()
    {
        if (matchLengthDropdown == null) return 60;

        switch (matchLengthDropdown.value)
        {
            case 0: return 30;  // 30 seconds
            case 1: return 60;  // 1 minute  
            case 2: return 120; // 2 minutes
            case 3: return 180; // 3 minutes
            case 4: return 300; // 5 minutes
            default: return 60;
        }
    }

    string GetSelectedMapFromDropdown()
    {
        if (mapSelectionDropdown == null) return "Arena1";

        List<string> maps = new List<string> { "Arena1", "Arena2", "Arena3" };
        int index = mapSelectionDropdown.value;
        return (index >= 0 && index < maps.Count) ? maps[index] : "Arena1";
    }

    // FIXED: Join room using room code - search through visible rooms
    void OnConfirmJoinRoomClicked()
    {
        string roomCode = roomCodeInput?.text?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(roomCode) || roomCode.Length != 4)
        {
            UpdateStatus("Enter a valid 4-character room code");
            return;
        }

        isJoiningCustomRoom = true;
        loadingIndicator.SetActive(true);
        UpdateStatus($"Searching for room with code {roomCode}...");

        // FIXED: Try to join using the predictable room name format
        string roomName = $"CustomRoom_{roomCode}";
        PhotonNetwork.JoinRoom(roomName);

        if (debugMode) Debug.Log($"[CUSTOM ROOM] Attempting to join room '{roomName}' with code '{roomCode}'");
    }

    string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 4; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }
    #endregion

    #region Navigation Buttons
    void OnBackFromCustomRoomClicked()
    {
        customRoomPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        UpdateStatus("Welcome back!");
    }

    void OnBackFromJoinRoomClicked()
    {
        joinRoomPanel.SetActive(false);
        customRoomPanel.SetActive(true);
        UpdateStatus("Choose an option");
    }

    void OnBackFromCreateRoomClicked()
    {
        createRoomPanel.SetActive(false);
        customRoomPanel.SetActive(true);
        UpdateStatus("Choose an option");
    }

    void OnBackFromRoomLobbyClicked()
    {
        roomLobbyPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        PhotonNetwork.LeaveRoom();
        UpdateStatus("Left room");
    }

    void OnStartGameClicked()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            UpdateStatus("Only the room creator can start the game");
            return;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            UpdateStatus("Need at least 2 players to start");
            return;
        }

        PhotonNetwork.LoadLevel(characterSelectionScene);
    }

    void OnLeaveRoomClicked()
    {
        // FIXED: If master client is leaving a custom room, close it to prevent quick match from joining
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            // Check if this is a custom room (has room code)
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOM_CODE_KEY))
            {
                if (debugMode) Debug.Log("[LEAVE ROOM] Master client leaving custom room - closing it");
                // Close the room to prevent others from joining
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
            }
        }
        
        PhotonNetwork.LeaveRoom();
    }
    #endregion

    #region Room Lobby UI
    void UpdateRoomLobbyUI()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        // Update room name
        if (roomNameText != null)
        {
            // Get room code from properties if it's a custom room
            string roomCode = "";
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOM_CODE_KEY))
            {
                roomCode = (string)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_CODE_KEY];
            }

            string displayName = !string.IsNullOrEmpty(roomCode) ? $"Room Code: {roomCode}" : PhotonNetwork.CurrentRoom.Name;
            roomNameText.text = displayName;
        }

        // Update room info
        if (roomInfoText != null)
        {
            int matchLength = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MATCH_LENGTH_KEY) ?
                            (int)PhotonNetwork.CurrentRoom.CustomProperties[MATCH_LENGTH_KEY] : 60;

            string selectedMap = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(SELECTED_MAP_KEY) ?
                               (string)PhotonNetwork.CurrentRoom.CustomProperties[SELECTED_MAP_KEY] : "Arena1";

            int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.ROOM_TYPE_KEY) ?
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.ROOM_TYPE_KEY] : 0;

            string roomTypeText = roomType == 0 ? "Quick Match" : "Custom Room";
            string matchLengthText = matchLength < 60 ? $"{matchLength}s" : $"{matchLength / 60}m";

            roomInfoText.text = $"Type: {roomTypeText}\nMatch Length: {matchLengthText}\nMap: {selectedMap}\nPlayers: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
        }

        // Update player list
        if (playerListText != null)
        {
            string playerList = "Players in room:\n";
            foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                playerList += $"• {player.NickName}";
                if (player.IsMasterClient) playerList += " (Host)";
                playerList += "\n";
            }
            playerListText.text = playerList;
        }

        // Show/hide start button for master client
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
    }
    #endregion

    #region FIXED: Photon Callbacks
    public override void OnConnectedToMaster()
    {
        loadingIndicator.SetActive(false);
        ShowMainMenu();
        
        if (debugMode) Debug.Log("[MAIN MENU] Connected to master server");
    }

    /// <summary>
    /// Handle connect button click - redirect to Connection scene
    /// </summary>
    public void OnConnectClicked()
    {
        SceneManager.LoadScene("Connection");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"[MAIN MENU] Disconnected: {cause}");
        
        // Don't redirect to Connection scene if we're disconnecting for AI mode
        if (isDisconnectingForAI)
        {
            Debug.Log("[MAIN MENU] Disconnected for AI mode, not redirecting to Connection scene");
            return;
        }
        
        SceneManager.LoadScene("Connection");
    }
    
    /// <summary>
    /// Logout and return to connection screen
    /// </summary>
    public void OnLogoutClicked()
    {
        if (debugMode) Debug.Log("[MAIN MENU] Logout requested");
        
        UpdateStatus("Logging out...");
        
        // Disconnect from Photon
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        
        // Save progression data before logout
        if (RetroDodge.Progression.PlayerDataManager.Instance != null)
        {
            RetroDodge.Progression.PlayerDataManager.Instance.ForceSave();
        }
        
        // Clear any progression data
        if (RetroDodge.Progression.PlayerDataManager.Instance != null)
        {
            Destroy(RetroDodge.Progression.PlayerDataManager.Instance.gameObject);
        }
        
        // Clear PlayFab authentication and stored credentials
        if (PlayFabAuthManager.Instance != null)
        {
            PlayFabAuthManager.Instance.Logout();
        }
        
        // Load connection scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("Connection");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (roomLobbyPanel.activeInHierarchy)
        {
            UpdateRoomLobbyUI();
        }

        // FIXED: Check for full room in quick match
        if (isMatchmaking && PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            StartMatchFoundSequence();
        }
        // Check for full room in competitive matchmaking
        else if (isCompetitiveMatchmaking && PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            if (debugMode) Debug.Log($"[COMPETITIVE] Player entered room, now {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers} players - starting match found sequence");
            StartCompetitiveMatchFoundSequence();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (roomLobbyPanel.activeInHierarchy)
        {
            UpdateRoomLobbyUI();
        }
    }

    // FIXED: Handle successful room creation
    public override void OnCreatedRoom()
    {
        if (debugMode) Debug.Log($"[ROOM CREATED] {PhotonNetwork.CurrentRoom.Name}");

        if (isMatchmaking)
        {
            UpdateStatus("Waiting for players...");
        }
        else if (isCompetitiveMatchmaking)
        {
            UpdateStatus("Waiting for competitive players...");
            
            // Initialize competitive series
            if (RoomStateManager.GetOrCreateInstance() != null)
            {
                RoomStateManager.GetOrCreateInstance().InitializeCompetitiveSeries(competitiveMaxMatches);
                if (debugMode) Debug.Log($"[COMPETITIVE] Initialized series with {competitiveMaxMatches} max matches");
            }
        }
        else if (isCreatingCustomRoom)
        {
            UpdateStatus($"Custom room created! Code: {currentRoomCode}");
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        isCreatingCustomRoom = false;
        loadingIndicator.SetActive(false);
        UpdateStatus($"Failed to create room: {message}");
        if (debugMode) Debug.Log($"[CREATE ROOM FAILED] {message} (Code: {returnCode})");
    }

    // FIXED: Handle successful room join
    public override void OnJoinedRoom()
    {
        loadingIndicator.SetActive(false);

        if (debugMode) Debug.Log($"[JOINED ROOM] {PhotonNetwork.CurrentRoom.Name} with {PhotonNetwork.CurrentRoom.PlayerCount} players");

        // FIXED: Verify room type matches what we expected
        if (isMatchmaking)
        {
            // Check if this is actually a quick match room
            int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.ROOM_TYPE_KEY) ? 
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.ROOM_TYPE_KEY] : -1;
            
            if (roomType != 0) // Not a quick match room
            {
                if (debugMode) Debug.LogWarning($"[QUICK MATCH] Joined wrong room type: {roomType}, leaving...");
                PhotonNetwork.LeaveRoom();
                return;
            }
        }
        else if (isCompetitiveMatchmaking)
        {
            // Check if this is actually a competitive room
            int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.ROOM_TYPE_KEY) ? 
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.ROOM_TYPE_KEY] : -1;
            
            if (debugMode) Debug.Log($"[COMPETITIVE] Joined room with type: {roomType}");
            
            if (roomType != 2) // Not a competitive room
            {
                if (debugMode) Debug.LogWarning($"[COMPETITIVE] Joined wrong room type: {roomType}, leaving...");
                PhotonNetwork.LeaveRoom();
                return;
            }
        }

        if (isMatchmaking)
        {
            // FIXED: Reset RoomStateManager leaving flag and clear character properties
            RoomStateManager.GetOrCreateInstance()?.ResetLeavingFlag();
            
            if (PhotonNetwork.IsConnectedAndReady)
            {
                Hashtable clearProps = new Hashtable
                {
                    { RoomStateManager.PLAYER_CHARACTER_KEY, -1 },
                    { RoomStateManager.PLAYER_LOCKED_KEY, false }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(clearProps);
            }

            // Quick match logic - check if room is full
            if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                // Room is full, start match found sequence
                StartMatchFoundSequence();
            }
            else
            {
                // Room not full yet, keep waiting
                UpdateStatus($"Waiting for players ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})...");
            }
        }
        else if (isCompetitiveMatchmaking)
        {
            // FIXED: Reset RoomStateManager leaving flag and clear character properties
            RoomStateManager.GetOrCreateInstance()?.ResetLeavingFlag();
            
            if (PhotonNetwork.IsConnectedAndReady)
            {
                Hashtable clearProps = new Hashtable
                {
                    { RoomStateManager.PLAYER_CHARACTER_KEY, -1 },
                    { RoomStateManager.PLAYER_LOCKED_KEY, false }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(clearProps);
            }

            // Competitive match logic - check if room is full
            if (debugMode) Debug.Log($"[COMPETITIVE] Room has {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers} players");
            
            if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                // Room is full, start competitive match found sequence
                if (debugMode) Debug.Log("[COMPETITIVE] Room is full, starting match found sequence");
                StartCompetitiveMatchFoundSequence();
            }
            else
            {
                // Room not full yet, keep waiting
                UpdateStatus($"Waiting for competitive players ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})...");
            }
        }
        else if (isCreatingCustomRoom)
        {
            // Created custom room - go to lobby
            isCreatingCustomRoom = false;
            createRoomPanel.SetActive(false);
            roomLobbyPanel.SetActive(true);
            UpdateRoomLobbyUI();
            UpdateStatus("Room created! Waiting for players...");
        }
        else if (isJoiningCustomRoom)
        {
            // FIXED: Reset RoomStateManager leaving flag and clear character properties
            RoomStateManager.GetOrCreateInstance()?.ResetLeavingFlag();
            
            if (PhotonNetwork.IsConnectedAndReady)
            {
                Hashtable clearProps = new Hashtable
                {
                    { RoomStateManager.PLAYER_CHARACTER_KEY, -1 },
                    { RoomStateManager.PLAYER_LOCKED_KEY, false }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(clearProps);
            }

            // Joined custom room - go to lobby
            isJoiningCustomRoom = false;
            joinRoomPanel.SetActive(false);
            roomLobbyPanel.SetActive(true);
            UpdateRoomLobbyUI();
            UpdateStatus("Successfully joined room!");
        }
    }

    // FIXED: Handle join room failure with better error messages
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (isMatchmaking)
        {
            if (debugMode) Debug.Log($"[QUICK MATCH] No random room found, creating new one");
            
            // Create a new room when no random room is available
            RoomOptions quickMatchOptions = new RoomOptions
            {
                MaxPlayers = maxPlayers,
                IsVisible = true,
                IsOpen = true,
                PlayerTtl = 30000, // 30 seconds
                EmptyRoomTtl = 10000, // 10 seconds
                CustomRoomProperties = CreateQuickMatchProperties(),
                CustomRoomPropertiesForLobby = new string[] { RoomStateManager.ROOM_TYPE_KEY, RoomStateManager.ROOM_MATCH_LENGTH }
            };
            
            PhotonNetwork.CreateRoom($"QuickMatch_{System.DateTime.Now.Ticks}", quickMatchOptions);
        }
        else if (isCompetitiveMatchmaking)
        {
            if (debugMode) Debug.Log($"[COMPETITIVE] No random competitive room found, creating new one");
            
            // Create a new competitive room when no random room is available
            RoomOptions competitiveOptions = new RoomOptions
            {
                MaxPlayers = maxPlayers,
                IsVisible = true,
                IsOpen = true,
                PlayerTtl = 30000, // 30 seconds
                EmptyRoomTtl = 10000, // 10 seconds
                CustomRoomProperties = CreateCompetitiveRoomProperties(),
                CustomRoomPropertiesForLobby = new string[] { RoomStateManager.ROOM_TYPE_KEY, RoomStateManager.ROOM_MATCH_LENGTH }
            };
            
            PhotonNetwork.CreateRoom($"Competitive_{System.DateTime.Now.Ticks}", competitiveOptions);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        isJoiningCustomRoom = false;
        isCreatingCustomRoom = false;
        loadingIndicator.SetActive(false);

        string errorMessage = GetJoinRoomErrorMessage(returnCode, message);
        UpdateStatus($"❌ {errorMessage}");

        if (debugMode) Debug.Log($"[JOIN ROOM FAILED] {message} (Code: {returnCode})");
    }

    string GetJoinRoomErrorMessage(short returnCode, string message)
    {
        switch (returnCode)
        {
            case 32765: return "Room not found! Check the room code.";
            case 32764: return "Room is full! Try another room.";
            case 32763: return "Room is closed! Cannot join.";
            default: return $"Could not join room: {message}";
        }
    }

    public override void OnLeftRoom()
    {
        CancelMatchmaking();
        ResetCustomRoomStates();

        // FIXED: Clear player properties to prevent character persistence
        if (PhotonNetwork.IsConnectedAndReady)
        {
            Hashtable clearProps = new Hashtable
            {
                { RoomStateManager.PLAYER_CHARACTER_KEY, -1 },
                { RoomStateManager.PLAYER_LOCKED_KEY, false }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(clearProps);
        }

        // FIXED: Clear room code mapping to prevent reusing old rooms
        currentRoomCode = "";
        if (generatedRoomCodeText != null)
        {
            generatedRoomCodeText.text = "Click 'Generate Code' to create room";
        }

        if (pendingAIFallback)
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
            else
            {
                StartOfflineAIMode();
            }
            return;
        }

        // Hide all panels except main menu
        customRoomPanel.SetActive(false);
        roomLobbyPanel.SetActive(false);
        matchFoundPanel?.SetActive(false);
        ShowMainMenu();

        if (debugMode) Debug.Log("[LEFT ROOM] Returned to main menu and cleared player properties");
    }
    #endregion

    #region Match Found Logic
    void StartMatchFoundSequence()
    {
        if (matchFound) return; // Prevent multiple calls

        matchFound = true;
        isMatchmaking = false;
        
        // Stop matchmaking timer
        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
            matchmakingCoroutine = null;
        }
        
        ResetQuickMatchButton();
        matchFoundPanel?.SetActive(true);

        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(QuickMatchCountdown());
    }
    
    void StartCompetitiveMatchFoundSequence()
    {
        if (matchFound) return; // Prevent multiple calls

        matchFound = true;
        isCompetitiveMatchmaking = false;
        
        // Stop competitive matchmaking timer
        if (competitiveMatchmakingCoroutine != null)
        {
            StopCoroutine(competitiveMatchmakingCoroutine);
            competitiveMatchmakingCoroutine = null;
        }
        
        ResetCompetitiveButton();
        matchFoundPanel?.SetActive(true);

        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(CompetitiveMatchCountdown());
        
        if (debugMode) Debug.Log("[COMPETITIVE] Match found sequence started");
    }
    #endregion

    #region Utility
    void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        if (debugMode) Debug.Log($"[MAIN MENU] {message}");
    }

    void ResetCustomRoomStates()
    {
        isCreatingCustomRoom = false;
        isJoiningCustomRoom = false;
        currentRoomCode = "";

        if (generatedRoomCodeText != null)
        {
            generatedRoomCodeText.text = "Click 'Generate Code' to create room";
        }
    }
    #endregion

    #region Debug Shortcuts
    void Update()
    {
        if (!debugMode) return;

        if (Input.GetKeyDown(KeyCode.F1)) Debug.Log("[DEBUG] F1 pressed - authentication handled in Connection scene");
        if (Input.GetKeyDown(KeyCode.F2)) OnQuickMatchClicked();
        if (Input.GetKeyDown(KeyCode.L) && PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (customRoomPanel.activeInHierarchy) OnBackFromCustomRoomClicked();
            else if (joinRoomPanel.activeInHierarchy) OnBackFromJoinRoomClicked();
            else if (createRoomPanel.activeInHierarchy) OnBackFromCreateRoomClicked();
        }
    }
    #endregion

    #region Cleanup
    void OnDestroy()
    {
        if (matchmakingCoroutine != null)
            StopCoroutine(matchmakingCoroutine);

        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);
            
        if (aiFallbackCoroutine != null)
            StopCoroutine(aiFallbackCoroutine);
            
        // Stop all coroutines to prevent SerializedObject errors
        StopAllCoroutines();
    }
    #endregion
}