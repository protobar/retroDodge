using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Enhanced Main Menu Manager with Custom Room Features
/// Handles connection, matchmaking, custom room creation/joining, and transition flow
/// </summary>
public class MainMenuManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject customRoomPanel;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button backFromCustomRoomButton;
    [SerializeField] private TMP_Text quickMatchButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private GameObject matchFoundPanel;
    [SerializeField] private TMP_Text countdownText;

    [Header("Custom Room UI")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_InputField joinRoomNameInput;
    [SerializeField] private Button confirmCreateRoomButton;
    [SerializeField] private Button confirmJoinRoomButton;

    [Header("Settings")]
    [SerializeField] private string characterSelectionScene = "CharacterSelection";
    [SerializeField] private byte maxPlayers = 2;
    [SerializeField] private bool debugMode = true;

    // Matchmaking state
    private bool isMatchmaking = false;
    private float matchmakingTime = 0f;
    private Coroutine matchmakingCoroutine;
    private Coroutine countdownCoroutine;

    // Custom room state
    private bool isCreatingCustomRoom = false;
    private bool isJoiningCustomRoom = false;

    void Start()
    {
        SetupUI();
        SetupButtonListeners();
        SetupCustomRoomUI();

        // Photon basic setup
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = "1.0";

        if (debugMode) Debug.Log("[MAIN MENU] Initialized");
    }

    #region UI Setup
    void SetupUI()
    {
        bool isReturning = PhotonNetwork.IsConnected && !string.IsNullOrEmpty(PhotonNetwork.NickName);

        connectionPanel.SetActive(!isReturning);
        mainMenuPanel.SetActive(isReturning);
        customRoomPanel.SetActive(false);
        loadingIndicator.SetActive(false);
        matchFoundPanel?.SetActive(false);

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
        createRoomButton?.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton?.onClick.AddListener(OnJoinRoomClicked);
        backFromCustomRoomButton?.onClick.AddListener(OnBackFromCustomRoomClicked);
        confirmCreateRoomButton?.onClick.AddListener(OnConfirmCreateRoomClicked);
        confirmJoinRoomButton?.onClick.AddListener(OnConfirmJoinRoomClicked);
    }

    void SetupCustomRoomUI()
    {

        // Setup room name placeholder
        if (roomNameInput != null)
        {
            roomNameInput.text = $"Room_{Random.Range(100, 999)}";
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

        connectionPanel.SetActive(false);
        loadingIndicator.SetActive(true);
        UpdateStatus("Connecting...");

        PhotonNetwork.ConnectUsingSettings();
    }
    #endregion

    #region Matchmaking
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
            StartMatchmaking();
    }

    void StartMatchmaking()
    {
        isMatchmaking = true;
        matchmakingTime = 0f;
        quickMatchButtonText.text = "Cancel";

        UpdateStatus("Finding Match...");

        if (matchmakingCoroutine != null)
            StopCoroutine(matchmakingCoroutine);
        matchmakingCoroutine = StartCoroutine(MatchmakingTimer());

        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers };
        PhotonNetwork.JoinRandomOrCreateRoom(null, maxPlayers,
            MatchmakingMode.FillRoom, null, null,
            $"QuickMatch_{System.DateTime.Now.Ticks}", options);
    }

    void CancelMatchmaking()
    {
        isMatchmaking = false;

        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
            matchmakingCoroutine = null;
        }

        ResetQuickMatchButton();
        UpdateStatus("Matchmaking cancelled");

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
    }

    IEnumerator MatchmakingTimer()
    {
        while (isMatchmaking)
        {
            matchmakingTime += Time.deltaTime;

            int minutes = Mathf.FloorToInt(matchmakingTime / 60f);
            int seconds = Mathf.FloorToInt(matchmakingTime % 60f);
            quickMatchButtonText.text = $"Cancel ({minutes:00}:{seconds:00})";

            int dots = Mathf.FloorToInt(matchmakingTime * 2f) % 4;
            UpdateStatus($"Finding Match{new string('.', dots)}");

            yield return new WaitForSeconds(0.1f);
        }
    }

    void ResetQuickMatchButton()
    {
        quickMatchButtonText.text = "Find Match";
    }
    #endregion

    #region Custom Room Management
    void OnCreateRoomClicked()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateStatus("Not connected to servers!");
            return;
        }

        mainMenuPanel.SetActive(false);
        customRoomPanel.SetActive(true);
        UpdateStatus("Enter room details");
    }

    void OnJoinRoomClicked()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            UpdateStatus("Not connected to servers!");
            return;
        }

        mainMenuPanel.SetActive(false);
        customRoomPanel.SetActive(true);
        UpdateStatus("Enter room name to join");
    }

    void OnBackFromCustomRoomClicked()
    {
        customRoomPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        UpdateStatus($"Welcome back, {PhotonNetwork.NickName}!");
    }

    void OnConfirmCreateRoomClicked()
    {
        string roomName = roomNameInput?.text?.Trim() ?? "";

        if (string.IsNullOrEmpty(roomName) || roomName.Length < 3)
        {
            UpdateStatus("Enter a valid room name (3+ characters)");
            return;
        }

        isCreatingCustomRoom = true;
        loadingIndicator.SetActive(true);
        UpdateStatus("Creating room...");

        RoomOptions roomOptions = new RoomOptions
        {
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    void OnConfirmJoinRoomClicked()
    {
        string roomName = joinRoomNameInput?.text?.Trim() ?? "";

        if (string.IsNullOrEmpty(roomName) || roomName.Length < 3)
        {
            UpdateStatus("Enter a valid room name (3+ characters)");
            return;
        }

        isJoiningCustomRoom = true;
        loadingIndicator.SetActive(true);
        UpdateStatus("Joining room...");

        PhotonNetwork.JoinRoom(roomName);
    }
    #endregion

    #region Photon Callbacks
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

        UpdateStatus($"Disconnected: {cause}");
        CancelMatchmaking();
        ResetCustomRoomStates();

        if (debugMode) Debug.Log($"[MAIN MENU] Disconnected: {cause}");
    }

    public override void OnCreatedRoom()
    {
        UpdateStatus($"Room '{PhotonNetwork.CurrentRoom.Name}' created successfully!");
        if (debugMode) Debug.Log($"[MAIN MENU] Created room: {PhotonNetwork.CurrentRoom.Name}");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        isCreatingCustomRoom = false;
        loadingIndicator.SetActive(false);
        UpdateStatus($"Failed to create room: {message}");
        if (debugMode) Debug.Log($"[MAIN MENU] Create room failed: {message}");
    }

    public override void OnJoinedRoom()
    {
        loadingIndicator.SetActive(false);

        if (isCreatingCustomRoom)
        {
            isCreatingCustomRoom = false;
            customRoomPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }
        else if (isJoiningCustomRoom)
        {
            isJoiningCustomRoom = false;
            customRoomPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }

        UpdateStatus($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        if (debugMode) Debug.Log($"[MAIN MENU] Joined room with {PhotonNetwork.CurrentRoom.PlayerCount} players");

        CheckIfGameCanStart();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        isJoiningCustomRoom = false;
        isCreatingCustomRoom = false;
        loadingIndicator.SetActive(false);

        if (isMatchmaking)
        {
            CancelMatchmaking();
            UpdateStatus("Failed to find match, try again");
        }
        else
        {
            UpdateStatus($"Failed to join room: {message}");
        }

        if (debugMode) Debug.Log($"[MAIN MENU] Join room failed: {message}");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateStatus($"{newPlayer.NickName} joined!");
        if (debugMode) Debug.Log($"[MAIN MENU] {newPlayer.NickName} entered room");

        CheckIfGameCanStart();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateStatus($"{otherPlayer.NickName} left the room");
        if (debugMode) Debug.Log($"[MAIN MENU] {otherPlayer.NickName} left room");

        // Update player count display
        CheckIfGameCanStart();
    }

    public override void OnLeftRoom()
    {
        CancelMatchmaking();
        ResetCustomRoomStates();

        customRoomPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        UpdateStatus($"Welcome back, {PhotonNetwork.NickName}!");

        if (debugMode) Debug.Log("[MAIN MENU] Left room");
    }
    #endregion

    #region Game Start
    void CheckIfGameCanStart()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            isMatchmaking = false;
            if (matchmakingCoroutine != null)
            {
                StopCoroutine(matchmakingCoroutine);
                matchmakingCoroutine = null;
            }

            StartCoroutine(ShowMatchFoundAndStartCountdown());
        }
        else
        {
            UpdateStatus($"Waiting for players ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
        }
    }

    IEnumerator ShowMatchFoundAndStartCountdown()
    {
        matchFoundPanel?.SetActive(true);
        UpdateStatus("Match Found!");

        yield return new WaitForSeconds(1f);

        // Start countdown from 3 to 1
        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(StartCountdown());
    }

    IEnumerator StartCountdown()
    {
        for (int i = 3; i > 0; i--)
        {
            if (countdownText != null)
                countdownText.text = i.ToString();

            UpdateStatus($"Starting in {i}...");

            yield return new WaitForSeconds(1f);
        }

        if (countdownText != null)
            countdownText.text = "GO!";

        UpdateStatus("Starting game!");

        yield return new WaitForSeconds(0.5f);

        // Only master client loads the scene
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(characterSelectionScene);
        }
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
    }
    #endregion

    #region Debug Shortcuts
    void Update()
    {
        if (!debugMode) return;

        if (Input.GetKeyDown(KeyCode.F1)) OnConnectClicked();
        if (Input.GetKeyDown(KeyCode.F2)) OnQuickMatchClicked();
        if (Input.GetKeyDown(KeyCode.F3))
        {
            mainMenuPanel.SetActive(false);
            customRoomPanel.SetActive(true);
            UpdateStatus("Enter room name and choose action");
        }
        if (Input.GetKeyDown(KeyCode.L) && PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        if (Input.GetKeyDown(KeyCode.Escape) && customRoomPanel.activeInHierarchy) OnBackFromCustomRoomClicked();
    }
    #endregion

    #region Cleanup
    void OnDestroy()
    {
        // Clean up coroutines
        if (matchmakingCoroutine != null)
            StopCoroutine(matchmakingCoroutine);

        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);
    }
    #endregion
}