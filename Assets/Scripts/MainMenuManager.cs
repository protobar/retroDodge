using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// PUN2 Main Menu Manager - Fixed multiplayer implementation
/// Handles connection, nickname, matchmaking, and scene transitions
/// </summary>
public class MainMenuManager : MonoBehaviourPunCallbacks
{
    [Header("Connection UI")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Main Menu UI")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button customMatchButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Custom Match UI")]
    [SerializeField] private GameObject customMatchPanel;
    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button backFromCustomButton;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button backFromSettingsButton;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Scene Management")]
    [SerializeField] private string characterSelectionScene = "CharacterSelection";
    [SerializeField] private float sceneTransitionDelay = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip errorSound;

    [Header("Visual Effects")]
    [SerializeField] private GameObject menuTransitionEffect;
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("Network Settings")]
    [SerializeField] private byte maxPlayersPerRoom = 2;
    [SerializeField] private float connectionTimeout = 10f;
    [SerializeField] private float matchmakingTimeout = 15f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Game state
    private bool isConnecting = false;
    private bool isTransitioning = false;
    private bool isMatchmaking = false;
    private Coroutine connectionTimeoutCoroutine;
    private Coroutine matchmakingTimeoutCoroutine;

    #region Unity Lifecycle

    void Awake()
    {
        // Ensure only one MainMenuManager exists
        MainMenuManager[] managers = FindObjectsOfType<MainMenuManager>();
        if (managers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        // CRITICAL: Set PhotonNetwork settings BEFORE connecting
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = "1.0";

        // IMPORTANT: SendRate and SerializationRate for better performance
        PhotonNetwork.SendRate = 20;
        PhotonNetwork.SerializationRate = 10;
    }

    void Start()
    {
        InitializeMainMenu();
        SetupButtonListeners();
        LoadUserSettings();
        PlayBackgroundMusic();

        if (debugMode)
        {
            Debug.Log("PUN2 MainMenuManager initialized");
        }
    }

    #endregion

    #region Initialization

    void InitializeMainMenu()
    {
        // Show connection panel first, hide everything else
        SetPanelActive(connectionPanel, true);
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(customMatchPanel, false);
        SetPanelActive(settingsPanel, false);

        // Set loading indicator off
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        // Set default nickname
        if (nicknameInputField != null)
        {
            string savedNickname = PlayerPrefs.GetString("PlayerNickname", "Player" + Random.Range(1000, 9999));
            nicknameInputField.text = savedNickname;
        }

        UpdateConnectionStatus("Enter your nickname and connect to start playing!");

        isTransitioning = false;
        isConnecting = false;
        isMatchmaking = false;
    }

    void SetupButtonListeners()
    {
        // Connection buttons
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);

        // Main menu buttons
        if (quickMatchButton != null)
            quickMatchButton.onClick.AddListener(OnQuickMatchClicked);

        if (customMatchButton != null)
            customMatchButton.onClick.AddListener(OnCustomMatchClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // Custom match buttons
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);

        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);

        if (backFromCustomButton != null)
            backFromCustomButton.onClick.AddListener(OnBackFromCustomClicked);

        // Settings buttons
        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(OnBackFromSettingsClicked);

        // Settings controls
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);

        if (debugMode)
        {
            Debug.Log("All button listeners setup complete");
        }
    }

    void LoadUserSettings()
    {
        // Load settings from PlayerPrefs
        float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        if (masterVolumeSlider != null)
            masterVolumeSlider.value = masterVolume;

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = sfxVolume;

        if (fullscreenToggle != null)
            fullscreenToggle.isOn = fullscreen;

        // Apply settings
        AudioListener.volume = masterVolume;
        if (audioSource != null)
            audioSource.volume = sfxVolume;

        Screen.fullScreen = fullscreen;

        if (debugMode)
        {
            Debug.Log($"Settings loaded - Master: {masterVolume}, SFX: {sfxVolume}, Fullscreen: {fullscreen}");
        }
    }

    void PlayBackgroundMusic()
    {
        if (audioSource != null && backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    #endregion

    #region Connection Flow

    public void OnConnectButtonClicked()
    {
        if (isConnecting) return;

        string nickname = nicknameInputField?.text?.Trim();

        if (string.IsNullOrEmpty(nickname))
        {
            UpdateConnectionStatus("Please enter a valid nickname!");
            PlaySound(errorSound);
            return;
        }

        if (nickname.Length < 2)
        {
            UpdateConnectionStatus("Nickname must be at least 2 characters!");
            PlaySound(errorSound);
            return;
        }

        if (nickname.Length > 20)
        {
            UpdateConnectionStatus("Nickname must be 20 characters or less!");
            PlaySound(errorSound);
            return;
        }

        StartConnection(nickname);
    }

    void StartConnection(string nickname)
    {
        isConnecting = true;
        PlaySound(buttonClickSound);

        // Save nickname
        PhotonNetwork.NickName = nickname;
        PlayerPrefs.SetString("PlayerNickname", nickname);
        PlayerPrefs.Save();

        // Update UI
        SetButtonInteractable(connectButton, false);
        if (loadingIndicator != null)
            loadingIndicator.SetActive(true);

        UpdateConnectionStatus("Connecting to Photon servers...");

        // Start connection timeout
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
        }
        connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutCoroutine());

        // Connect to Photon
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            // Already connected, just proceed to main menu
            OnConnectedToMaster();
        }

        if (debugMode)
        {
            Debug.Log($"Starting connection with nickname: {nickname}");
        }
    }

    IEnumerator ConnectionTimeoutCoroutine()
    {
        yield return new WaitForSeconds(connectionTimeout);

        // Only timeout if we're still connecting and this coroutine wasn't cancelled
        if (isConnecting)
        {
            isConnecting = false;
            UpdateConnectionStatus("Connection timeout! Please try again.");
            PlaySound(errorSound);

            SetButtonInteractable(connectButton, true);
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);

            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }

            if (debugMode)
            {
                Debug.LogError("Connection timed out");
            }
        }

        connectionTimeoutCoroutine = null;
    }

    #endregion

    #region Button Handlers

    void OnQuickMatchClicked()
    {
        if (isTransitioning || isMatchmaking) return;

        PlaySound(buttonClickSound);
        StartQuickMatch();
    }

    void OnCustomMatchClicked()
    {
        if (isTransitioning) return;

        PlaySound(buttonClickSound);
        ShowCustomMatchPanel();
    }

    void OnCreateRoomClicked()
    {
        if (isTransitioning || isMatchmaking) return;

        string roomName = roomNameInputField?.text?.Trim();

        if (string.IsNullOrEmpty(roomName))
        {
            UpdateConnectionStatus("Please enter a room name!");
            PlaySound(errorSound);
            return;
        }

        PlaySound(buttonClickSound);
        CreateCustomRoom(roomName);
    }

    void OnJoinRoomClicked()
    {
        if (isTransitioning || isMatchmaking) return;

        string roomName = roomNameInputField?.text?.Trim();

        if (string.IsNullOrEmpty(roomName))
        {
            UpdateConnectionStatus("Please enter a room name!");
            PlaySound(errorSound);
            return;
        }

        PlaySound(buttonClickSound);
        JoinCustomRoom(roomName);
    }

    void OnBackFromCustomClicked()
    {
        PlaySound(buttonClickSound);
        ShowMainMenuPanel();
    }

    void OnSettingsClicked()
    {
        PlaySound(buttonClickSound);
        ShowSettingsPanel();
    }

    void OnBackFromSettingsClicked()
    {
        PlaySound(buttonClickSound);
        HideSettingsPanel();
    }

    void OnQuitClicked()
    {
        PlaySound(buttonClickSound);
        StartCoroutine(QuitGameWithDelay());
    }

    #endregion

    #region Matchmaking

    void StartQuickMatch()
    {
        // CRITICAL FIX: Check for IsConnectedAndReady instead of just IsConnected
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateConnectionStatus("Not connected to Photon servers!");
            PlaySound(errorSound);
            return;
        }

        isMatchmaking = true;
        SetMainMenuButtonsInteractable(false);
        UpdateConnectionStatus("Looking for a match...");

        // Start matchmaking timeout
        if (matchmakingTimeoutCoroutine != null)
        {
            StopCoroutine(matchmakingTimeoutCoroutine);
        }
        matchmakingTimeoutCoroutine = StartCoroutine(MatchmakingTimeoutCoroutine());

        // IMPROVED: Use JoinRandomOrCreateRoom for better reliability
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true,
            // IMPORTANT: Set custom properties to help with matchmaking
            CustomRoomProperties = new Hashtable() { { "gameVersion", PhotonNetwork.GameVersion } },
            CustomRoomPropertiesForLobby = new string[] { "gameVersion" }
        };

        PhotonNetwork.JoinRandomOrCreateRoom(
            null, // No expected custom properties
            maxPlayersPerRoom,
            Photon.Realtime.MatchmakingMode.FillRoom,
            null, // No type of lobby
            null, // No SQL lobby filter
            "QuickMatch_" + System.DateTime.Now.Ticks, // Unique room name
            roomOptions
        );

        if (debugMode)
        {
            Debug.Log("Starting quick match with JoinRandomOrCreateRoom...");
        }
    }

    void CreateCustomRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateConnectionStatus("Not connected to Photon servers!");
            PlaySound(errorSound);
            return;
        }

        isMatchmaking = true;
        SetCustomMatchButtonsInteractable(false);
        UpdateConnectionStatus($"Creating room '{roomName}'...");

        // Start timeout
        if (matchmakingTimeoutCoroutine != null)
        {
            StopCoroutine(matchmakingTimeoutCoroutine);
        }
        matchmakingTimeoutCoroutine = StartCoroutine(MatchmakingTimeoutCoroutine());

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = new Hashtable() { { "gameVersion", PhotonNetwork.GameVersion } },
            CustomRoomPropertiesForLobby = new string[] { "gameVersion" }
        };

        PhotonNetwork.CreateRoom(roomName, roomOptions);

        if (debugMode)
        {
            Debug.Log($"Creating custom room: {roomName}");
        }
    }

    void JoinCustomRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateConnectionStatus("Not connected to Photon servers!");
            PlaySound(errorSound);
            return;
        }

        isMatchmaking = true;
        SetCustomMatchButtonsInteractable(false);
        UpdateConnectionStatus($"Joining room '{roomName}'...");

        // Start timeout
        if (matchmakingTimeoutCoroutine != null)
        {
            StopCoroutine(matchmakingTimeoutCoroutine);
        }
        matchmakingTimeoutCoroutine = StartCoroutine(MatchmakingTimeoutCoroutine());

        PhotonNetwork.JoinRoom(roomName);

        if (debugMode)
        {
            Debug.Log($"Joining custom room: {roomName}");
        }
    }

    IEnumerator MatchmakingTimeoutCoroutine()
    {
        yield return new WaitForSeconds(matchmakingTimeout);

        if (isMatchmaking)
        {
            isMatchmaking = false;
            UpdateConnectionStatus("Matchmaking timeout! Please try again.");
            PlaySound(errorSound);

            // Reset UI
            SetMainMenuButtonsInteractable(true);
            SetCustomMatchButtonsInteractable(true);

            // Leave room if we're in one
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            if (debugMode)
            {
                Debug.LogError("Matchmaking timed out");
            }
        }

        matchmakingTimeoutCoroutine = null;
    }

    #endregion

    #region UI Helpers

    void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    void SetMainMenuButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(quickMatchButton, interactable);
        SetButtonInteractable(customMatchButton, interactable);
        SetButtonInteractable(settingsButton, interactable);
        SetButtonInteractable(quitButton, interactable);
    }

    void SetCustomMatchButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(createRoomButton, interactable);
        SetButtonInteractable(joinRoomButton, interactable);
        SetButtonInteractable(backFromCustomButton, interactable);
    }

    void ShowMainMenuPanel()
    {
        SetPanelActive(connectionPanel, false);
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(customMatchPanel, false);
        SetPanelActive(settingsPanel, false);

        UpdateConnectionStatus($"Connected as {PhotonNetwork.NickName}");
    }

    void ShowCustomMatchPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(customMatchPanel, true);
    }

    void ShowSettingsPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(settingsPanel, true);
    }

    void HideSettingsPanel()
    {
        SetPanelActive(settingsPanel, false);
        SetPanelActive(mainMenuPanel, true);
    }

    void UpdateConnectionStatus(string message)
    {
        if (connectionStatusText != null)
            connectionStatusText.text = message;

        if (debugMode)
        {
            Debug.Log($"Status: {message}");
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Settings

    void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    void OnSFXVolumeChanged(float value)
    {
        if (audioSource != null)
            audioSource.volume = value;
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }

    #endregion

    #region Scene Transition

    IEnumerator QuitGameWithDelay()
    {
        SetMainMenuButtonsInteractable(false);
        yield return new WaitForSeconds(0.5f);

        if (debugMode)
        {
            Debug.Log("Quitting game...");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void TransitionToCharacterSelection()
    {
        if (isTransitioning) return;

        StartCoroutine(TransitionToCharacterSelectionCoroutine());
    }

    IEnumerator TransitionToCharacterSelectionCoroutine()
    {
        isTransitioning = true;
        UpdateConnectionStatus("Loading character selection...");

        // Optional: Show transition effect
        if (menuTransitionEffect != null)
        {
            menuTransitionEffect.SetActive(true);
            yield return new WaitForSeconds(transitionDuration);
        }

        // Load character selection scene
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(characterSelectionScene);
        }
    }

    #endregion

    #region Photon PUN2 Callbacks

    public override void OnConnectedToMaster()
    {
        // Stop the timeout first
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }

        // Set connecting to false
        isConnecting = false;

        PlaySound(successSound);
        UpdateConnectionStatus($"Connected successfully as {PhotonNetwork.NickName}!");

        // Update UI
        SetButtonInteractable(connectButton, true);
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        // Show main menu after a short delay
        StartCoroutine(ShowMainMenuDelayed());

        if (debugMode)
        {
            Debug.Log($"Connected to Master Server as {PhotonNetwork.NickName}");
            Debug.Log($"Network State: {PhotonNetwork.NetworkClientState}");
        }
    }

    IEnumerator ShowMainMenuDelayed()
    {
        yield return new WaitForSeconds(1f);
        ShowMainMenuPanel();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }

        if (matchmakingTimeoutCoroutine != null)
        {
            StopCoroutine(matchmakingTimeoutCoroutine);
            matchmakingTimeoutCoroutine = null;
        }

        isConnecting = false;
        isTransitioning = false;
        isMatchmaking = false;

        UpdateConnectionStatus($"Disconnected: {cause}");
        PlaySound(errorSound);

        // Reset UI
        SetButtonInteractable(connectButton, true);
        SetMainMenuButtonsInteractable(true);
        SetCustomMatchButtonsInteractable(true);

        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        SetPanelActive(connectionPanel, true);
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(customMatchPanel, false);

        if (debugMode)
        {
            Debug.Log($"Disconnected from Photon: {cause}");
        }
    }

    // REMOVED OnJoinRandomFailed - Using JoinRandomOrCreateRoom now

    public override void OnJoinedRoom()
    {
        // Stop matchmaking timeout
        if (matchmakingTimeoutCoroutine != null)
        {
            StopCoroutine(matchmakingTimeoutCoroutine);
            matchmakingTimeoutCoroutine = null;
        }

        isMatchmaking = false;
        PlaySound(successSound);
        UpdateConnectionStatus($"Joined room: {PhotonNetwork.CurrentRoom.Name}");

        // Re-enable buttons
        SetMainMenuButtonsInteractable(true);
        SetCustomMatchButtonsInteractable(true);

        if (debugMode)
        {
            Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name} with {PhotonNetwork.CurrentRoom.PlayerCount} players");
            Debug.Log($"Is Master Client: {PhotonNetwork.IsMasterClient}");
        }

        // Check if we have enough players to start
        CheckRoomPlayerCount();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateConnectionStatus($"{newPlayer.NickName} joined the room!");

        if (debugMode)
        {
            Debug.Log($"Player {newPlayer.NickName} entered room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        }

        // Check if we have enough players to start
        CheckRoomPlayerCount();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateConnectionStatus($"{otherPlayer.NickName} left the room.");

        if (debugMode)
        {
            Debug.Log($"Player {otherPlayer.NickName} left room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (matchmakingTimeoutCoroutine != null)
        {
            StopCoroutine(matchmakingTimeoutCoroutine);
            matchmakingTimeoutCoroutine = null;
        }

        isMatchmaking = false;
        SetCustomMatchButtonsInteractable(true);

        UpdateConnectionStatus($"Failed to create room: {message}");
        PlaySound(errorSound);

        if (debugMode)
        {
            Debug.Log($"Create room failed: {message} (Return Code: {returnCode})");
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (matchmakingTimeoutCoroutine != null)
        {
            StopCoroutine(matchmakingTimeoutCoroutine);
            matchmakingTimeoutCoroutine = null;
        }

        isMatchmaking = false;
        SetCustomMatchButtonsInteractable(true);
        SetMainMenuButtonsInteractable(true);

        UpdateConnectionStatus($"Failed to join room: {message}");
        PlaySound(errorSound);

        if (debugMode)
        {
            Debug.Log($"Join room failed: {message} (Return Code: {returnCode})");
        }
    }

    // NEW: Handle OnLeftRoom callback
    public override void OnLeftRoom()
    {
        if (debugMode)
        {
            Debug.Log("Left room successfully");
        }

        // Reset matchmaking state
        isMatchmaking = false;
        SetMainMenuButtonsInteractable(true);
        SetCustomMatchButtonsInteractable(true);

        UpdateConnectionStatus($"Connected as {PhotonNetwork.NickName}");
    }

    void CheckRoomPlayerCount()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount >= maxPlayersPerRoom)
        {
            UpdateConnectionStatus("Match found! Starting game...");
            TransitionToCharacterSelection();
        }
        else
        {
            UpdateConnectionStatus($"Waiting for players... ({PhotonNetwork.CurrentRoom.PlayerCount}/{maxPlayersPerRoom})");
        }
    }

    #endregion

    #region Debug

    void Update()
    {
        if (!debugMode) return;

        // Debug controls
        if (Input.GetKeyDown(KeyCode.F1))
        {
            OnConnectButtonClicked();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            OnQuickMatchClicked();
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            OnCustomMatchClicked();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                OnBackFromSettingsClicked();
            }
            else if (customMatchPanel != null && customMatchPanel.activeSelf)
            {
                OnBackFromCustomClicked();
            }
        }

        // NEW: Debug key to leave room
        if (Input.GetKeyDown(KeyCode.L) && PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== PUN2 MAIN MENU DEBUG ===");
        GUILayout.Label($"Connection State: {PhotonNetwork.NetworkClientState}");
        GUILayout.Label($"Connected: {PhotonNetwork.IsConnected}");
        GUILayout.Label($"Connected & Ready: {PhotonNetwork.IsConnectedAndReady}");
        GUILayout.Label($"In Room: {PhotonNetwork.InRoom}");
        GUILayout.Label($"Is Master Client: {PhotonNetwork.IsMasterClient}");
        GUILayout.Label($"Nickname: {PhotonNetwork.NickName}");
        GUILayout.Label($"Players in Room: {(PhotonNetwork.CurrentRoom?.PlayerCount ?? 0)}");
        GUILayout.Label($"Room Name: {(PhotonNetwork.CurrentRoom?.Name ?? "None")}");
        GUILayout.Label($"Is Connecting: {isConnecting}");
        GUILayout.Label($"Is Matchmaking: {isMatchmaking}");
        GUILayout.Label($"Is Transitioning: {isTransitioning}");

        GUILayout.Space(10);
        GUILayout.Label("Debug Controls:");
        GUILayout.Label("F1 - Connect");
        GUILayout.Label("F2 - Quick Match");
        GUILayout.Label("F3 - Custom Match");
        GUILayout.Label("L - Leave Room");
        GUILayout.Label("ESC - Back");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion
}