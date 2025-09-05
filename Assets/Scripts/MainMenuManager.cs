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
/// FIXED MainMenuManager with proper PUN2 matchmaking and custom room sharing
/// Key fixes: JoinRandomOrCreateRoom, custom properties for lobby, proper room code system
/// </summary>
public class MainMenuManager : MonoBehaviourPunCallbacks
{
    [Header("=== MAIN UI PANELS ===")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject customRoomPanel;
    [SerializeField] private GameObject joinRoomPanel;
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private GameObject roomLobbyPanel;
    [SerializeField] private GameObject matchFoundPanel;

    [Header("=== CONNECTION & MAIN MENU ===")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button customRoomButton;
    [SerializeField] private Button playWithAIButton;
    [SerializeField] private TMP_Text quickMatchButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TMP_Text countdownText;
    
    [Header("=== PLAY WITH AI PANEL ===")]
    [SerializeField] private GameObject playWithAIPanel;
    [SerializeField] private TMP_Dropdown aiDifficultyDropdown;
    [SerializeField] private Button confirmPlayWithAIButton;
    [SerializeField] private Button backFromAIButton;

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



    // FIXED: Matchmaking state management
    private bool isMatchmaking = false;
    private float matchmakingTime = 0f;
    private Coroutine matchmakingCoroutine;
    private Coroutine countdownCoroutine;
    private bool matchFound = false;
    private Coroutine aiFallbackCoroutine;
    private bool pendingAIFallback = false;

    // FIXED: Custom room state with proper room code system
    private bool isCreatingCustomRoom = false;
    private bool isJoiningCustomRoom = false;
    private string currentRoomCode = "";

    // FIXED: Constants for room properties
    private const string ROOM_CODE_KEY = "RC"; // Room Code
    private const string MATCH_LENGTH_KEY = "ML"; // Match Length
    private const string SELECTED_MAP_KEY = "SM"; // Selected Map
    private const string ROOM_TYPE_KEY = "RT"; // Room Type (0=QuickMatch, 1=Custom)

    void Start()
    {
        SetupUI();
        SetupButtonListeners();
        SetupCustomRoomUI();

        // FIXED: Proper PUN2 setup
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = "1.0";
        PhotonNetwork.SendRate = 20; // Default is good
        PhotonNetwork.SerializationRate = 10; // Default is good

        if (debugMode) Debug.Log("[MAIN MENU] Initialized with PUN2 best practices");
    }

    #region UI Setup
    void SetupUI()
    {
        bool isReturning = PhotonNetwork.IsConnectedAndReady && !string.IsNullOrEmpty(PhotonNetwork.NickName);

        connectionPanel.SetActive(!isReturning);
        mainMenuPanel.SetActive(isReturning);
        customRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        createRoomPanel.SetActive(false);
        roomLobbyPanel.SetActive(false);
        playWithAIPanel?.SetActive(false);
        matchFoundPanel?.SetActive(false);
        loadingIndicator.SetActive(false);

        if (nicknameInput != null)
        {
            if (isReturning)
                nicknameInput.text = PhotonNetwork.NickName;
            else
                nicknameInput.text = PlayerPrefs.GetString("PlayerNickname", $"Player{Random.Range(1000, 9999)}");
        }

        UpdateStatus(isReturning ? $"Welcome back, {PhotonNetwork.NickName}!" : "Enter nickname to connect");
        ResetQuickMatchButton();
    }

    void SetupButtonListeners()
    {
        connectButton?.onClick.AddListener(OnConnectClicked);
        quickMatchButton?.onClick.AddListener(OnQuickMatchClicked);
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
    }
    #endregion

    #region Connection
    void OnConnectClicked()
    {
        string nickname = nicknameInput?.text?.Trim() ?? "";

        if (string.IsNullOrEmpty(nickname) || nickname.Length < 2)
        {
            UpdateStatus("Enter a valid nickname (2+ characters)");
            return;
        }

        PhotonNetwork.NickName = nickname;
        PlayerPrefs.SetString("PlayerNickname", nickname);

        loadingIndicator.SetActive(true);
        UpdateStatus("Connecting...");

        PhotonNetwork.ConnectUsingSettings();
    }
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
        Hashtable expectedProps = new Hashtable { { ROOM_TYPE_KEY, 0 } }; // Only join quick match rooms
        PhotonNetwork.JoinRandomRoom(expectedProps, maxPlayers);
    }

    Hashtable CreateQuickMatchProperties()
    {
        return new Hashtable
        {
            { ROOM_TYPE_KEY, 0 }, // 0 = Quick Match
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

        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.AutomaticallySyncScene = false;

        // Configure AI session (Normal by default; random AI character at runtime)
        var cfg = RetroDodge.AISessionConfig.Instance;
        cfg.SetPlayWithAI(RetroDodge.AI.AIDifficulty.Normal, -1, -1);

        UpdateStatus("No players found. Starting character selection vs AI...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelection");
        pendingAIFallback = false;
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
            CustomRoomPropertiesForLobby = new string[] { ROOM_CODE_KEY, ROOM_TYPE_KEY, MATCH_LENGTH_KEY, SELECTED_MAP_KEY }
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
            { ROOM_TYPE_KEY, 1 }, // 1 = Custom Room
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

            int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOM_TYPE_KEY) ?
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_TYPE_KEY] : 0;

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
        connectionPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        UpdateStatus($"Connected as {PhotonNetwork.NickName}");
        if (debugMode) Debug.Log("[MAIN MENU] Connected to master server");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        loadingIndicator.SetActive(false);
        connectionPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
        customRoomPanel.SetActive(false);

        if (pendingAIFallback)
        {
            StartOfflineAIMode();
            return;
        }

        UpdateStatus($"Disconnected: {cause}");
        CancelMatchmaking();
        ResetCustomRoomStates();

        if (debugMode) Debug.Log($"[MAIN MENU] Disconnected: {cause}");
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
            int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOM_TYPE_KEY) ? 
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_TYPE_KEY] : -1;
            
            if (roomType != 0) // Not a quick match room
            {
                if (debugMode) Debug.LogWarning($"[QUICK MATCH] Joined wrong room type: {roomType}, leaving...");
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
                CustomRoomPropertiesForLobby = new string[] { ROOM_TYPE_KEY, MATCH_LENGTH_KEY }
            };
            
            PhotonNetwork.CreateRoom($"QuickMatch_{System.DateTime.Now.Ticks}", quickMatchOptions);
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
        mainMenuPanel.SetActive(true);

        UpdateStatus($"Welcome back, {PhotonNetwork.NickName}!");

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

        if (Input.GetKeyDown(KeyCode.F1)) OnConnectClicked();
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