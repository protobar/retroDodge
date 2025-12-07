using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// PUN2 Character Selection Manager with Dodge Detection
/// </summary>
public class NetworkCharacterSelectionManager : MonoBehaviourPunCallbacks
{
    [Header("Character Selection")]
    [SerializeField] private CharacterData[] availableCharacters;
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private Transform characterGridParent;
    [SerializeField] private GameObject characterButtonPrefab;

    [Header("Selected Character Display")]
    [SerializeField] private GameObject selectedCharacterPanel;
    [SerializeField] private Image selectedCharacterImage;
    [SerializeField] private TMP_Text selectedCharacterName;
    [SerializeField] private TMP_Text selectedCharacterDescription;
    [SerializeField] private Button lockInButton;
    [SerializeField] private Button changeCharacterButton;

    [Header("Timer System")]
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Image timerFillImage;
    [SerializeField] private float selectionTimeLimit = 30f;

    [Header("Player Status")]
    [SerializeField] private GameObject playerStatusPanel;
    [SerializeField] private TMP_Text localPlayerStatusText;
    [SerializeField] private TMP_Text remotePlayerStatusText;
    [SerializeField] private TMP_Text roomInfoText;

    [Header("Loading & Transition")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string gameplayScene = "GameplayArena";

    [Header("Character Preview")]
    [SerializeField] private Transform characterPreviewParent;
    [SerializeField] private Vector3 previewPosition = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 previewRotation = new Vector3(0, 0, 0);
    [SerializeField] private float previewScale = 1f;
    [SerializeField] private bool enablePreviewAnimations = true;
    [SerializeField] private RuntimeAnimatorController previewAnimatorController;
    
    [Header("Preview Prefabs (Animation Only)")]
    [SerializeField] private GameObject[] characterPreviewPrefabs; // Separate preview prefabs with only animators

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip characterSelectSound;
    [SerializeField] private AudioClip lockInSound;
    [SerializeField] private AudioClip timerWarningSound;
    [SerializeField] private bool debugMode;


    // FIXED: Flag to prevent room property updates when leaving
    private bool isLeavingRoom = false;
    [SerializeField] private AudioClip transitionSound;

    // Selection state
    private int selectedCharacterIndex = -1;
    private bool isCharacterLocked = false;
    private float timeRemaining;
    private bool timerStarted = false;
    private bool isTransitioning = false;
    private double selectionStartTime = 0;

    // Character preview state
    private GameObject currentPreviewCharacter;
    private Animator currentPreviewAnimator;


    // FIXED: Use centralized room state manager
    // Room property constants are now in RoomStateManager

    // UI References
    private Button[] characterButtons;

    #region Unity Lifecycle

    void Start()
    {
        StartCoroutine(InitializeWithDelay());
    }

    IEnumerator InitializeWithDelay()
    {
        yield return new WaitForSeconds(0.5f);
        InitializeCharacterSelection();
        UpdatePlayerStatus();

        yield return new WaitForSeconds(1f);
        StartSelectionTimer();
    }

    void Update()
    {
        if (timerStarted && !isTransitioning)
        {
            UpdateTimer();
        }
    }

    #endregion

    #region Initialization

    void InitializeCharacterSelection()
    {
        // Allow OfflineMode character selection
        if (!PhotonNetwork.OfflineMode)
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("Not in a Photon room! Returning to main menu.");
                ReturnToMainMenu();
                return;
            }
        }

        SetPanelActive(characterSelectionPanel, true);
        SetPanelActive(selectedCharacterPanel, false);
        SetPanelActive(loadingPanel, false);
        SetPanelActive(timerPanel, true);
        SetPanelActive(playerStatusPanel, true);

        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogError("No characters available! Check CharacterData array in inspector.");
            return;
        }

        CreateCharacterButtons();

        timeRemaining = selectionTimeLimit;
        UpdateTimerDisplay();

        // Skip room state when offline
        if (!PhotonNetwork.OfflineMode)
        {
            // FIXED: Use centralized room state manager with fallback
            RoomStateManager.GetOrCreateInstance()?.SetPlayerSelectionState(-1, false);
        }

        SetupButtonListeners();
    }

    void SetupButtonListeners()
    {
        if (lockInButton != null)
        {
            lockInButton.onClick.RemoveAllListeners();
            lockInButton.onClick.AddListener(OnLockInButtonClicked);
        }

        if (changeCharacterButton != null)
        {
            changeCharacterButton.onClick.RemoveAllListeners();
            changeCharacterButton.onClick.AddListener(OnChangeCharacterButtonClicked);
        }
    }

    void CreateCharacterButtons()
    {
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogError("No characters available for selection!");
            return;
        }

        if (characterGridParent == null || characterButtonPrefab == null)
        {
            Debug.LogError("Character Grid Parent or Button Prefab not assigned!");
            return;
        }

        // Clear existing buttons
        foreach (Transform child in characterGridParent)
        {
            DestroyImmediate(child.gameObject);
        }

        characterButtons = new Button[availableCharacters.Length];

        for (int i = 0; i < availableCharacters.Length; i++)
        {
            int characterIndex = i;
            CharacterData character = availableCharacters[i];

            if (character == null)
            {
                Debug.LogError($"Character at index {i} is null!");
                continue;
            }

            GameObject buttonObj = Instantiate(characterButtonPrefab, characterGridParent);

            // GET THE CharacterSelectionButton COMPONENT
            CharacterSelectionButton selectionButton = buttonObj.GetComponent<CharacterSelectionButton>();

            // CALL INITIALIZE - THIS IS WHAT WAS MISSING!
            if (selectionButton != null)
            {
                selectionButton.Initialize(character, i);
            }
            else
            {
                Debug.LogError("CharacterButtonPrefab doesn't have CharacterSelectionButton component!");
            }

            Button button = buttonObj.GetComponent<Button>();

            if (button == null)
            {
                Debug.LogError("Character Button Prefab doesn't have a Button component!");
                continue;
            }

            characterButtons[i] = button;
            button.interactable = true;
            button.enabled = true;

            // Add click listener
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => {
                OnCharacterSelected(characterIndex);
            });

            buttonObj.SetActive(true);

            CanvasGroup canvasGroup = buttonObj.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        StartCoroutine(ForceCanvasRebuild());
    }

    IEnumerator ForceCanvasRebuild()
    {
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogError("No EventSystem found! UI buttons won't work without EventSystem.");

            var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
    }

    #endregion

    #region Character Selection Logic

    void OnCharacterSelected(int characterIndex)
    {
        if (isCharacterLocked || isTransitioning)
        {
            return;
        }

        if (characterIndex < 0 || characterIndex >= availableCharacters.Length)
        {
            Debug.LogError($"Invalid character index: {characterIndex}");
            return;
        }

        selectedCharacterIndex = characterIndex;
        CharacterData selectedCharacter = availableCharacters[characterIndex];

        UpdateCharacterDisplay(selectedCharacter);
        UpdateCharacterButtons();
        
        // Spawn character preview
        SpawnCharacterPreview(selectedCharacter);

        if (!PhotonNetwork.OfflineMode)
        {
            // FIXED: Use centralized room state manager with fallback
            bool success = RoomStateManager.GetOrCreateInstance()?.SetPlayerProperty(RoomStateManager.PLAYER_CHARACTER_KEY, characterIndex) ?? false;
            if (debugMode) Debug.Log($"[CHAR SELECT] Set character property: {success}, CharacterIndex: {characterIndex}");
            
            // FIXED: If property update failed, try to reset the leaving flag
            if (!success && RoomStateManager.GetOrCreateInstance() != null)
            {
                RoomStateManager.GetOrCreateInstance().ResetLeavingFlag();
                // Try again after resetting the flag
                success = RoomStateManager.GetOrCreateInstance().SetPlayerProperty(RoomStateManager.PLAYER_CHARACTER_KEY, characterIndex);
                if (debugMode) Debug.Log($"[CHAR SELECT] Retry after reset flag: {success}");
            }
        }

        PlaySound(characterSelectSound);

        SetPanelActive(selectedCharacterPanel, true);
        UpdatePlayerStatus();
    }
    

    void UpdateCharacterDisplay(CharacterData character)
    {
        if (selectedCharacterImage != null && character.characterIcon != null)
            selectedCharacterImage.sprite = character.characterIcon;

        if (selectedCharacterName != null)
            selectedCharacterName.text = character.characterName;

        if (selectedCharacterDescription != null)
            selectedCharacterDescription.text = character.characterDescription;

        if (lockInButton != null)
            lockInButton.interactable = true;

        if (changeCharacterButton != null)
            changeCharacterButton.interactable = true;
    }

    void UpdateCharacterButtons()
    {
        if (characterButtons == null) return;

        for (int i = 0; i < characterButtons.Length; i++)
        {
            Button button = characterButtons[i];
            if (button == null) continue;

            ColorBlock colors = button.colors;
            if (i == selectedCharacterIndex)
            {
                colors.normalColor = Color.yellow;
                colors.selectedColor = Color.yellow;
                colors.highlightedColor = Color.yellow;
            }
            else
            {
                colors.normalColor = Color.white;
                colors.selectedColor = Color.white;
                colors.highlightedColor = Color.gray;
            }
            button.colors = colors;
            button.interactable = !isCharacterLocked;
        }
    }

    public void OnLockInButtonClicked()
    {
        if (selectedCharacterIndex == -1 || isCharacterLocked || isTransitioning)
        {
            return;
        }

        LockInCharacter();
    }


    void LockInCharacter()
    {
        if (selectedCharacterIndex == -1) return;

        isCharacterLocked = true;
        CharacterData lockedCharacter = availableCharacters[selectedCharacterIndex];

        if (!PhotonNetwork.OfflineMode)
        {
            // FIXED: Use centralized room state manager with fallback
            bool success = RoomStateManager.GetOrCreateInstance()?.SetPlayerSelectionState(selectedCharacterIndex, true) ?? false;
            if (debugMode) Debug.Log($"[CHAR SELECT] Set lock-in property: {success}, CharacterIndex: {selectedCharacterIndex}");
            
            // FIXED: If property update failed, try to reset the leaving flag
            if (!success && RoomStateManager.GetOrCreateInstance() != null)
            {
                RoomStateManager.GetOrCreateInstance().ResetLeavingFlag();
                // Try again after resetting the flag
                success = RoomStateManager.GetOrCreateInstance().SetPlayerSelectionState(selectedCharacterIndex, true);
                if (debugMode) Debug.Log($"[CHAR SELECT] Retry after reset flag: {success}");
            }
        }

        if (lockInButton != null)
            lockInButton.interactable = false;

        if (changeCharacterButton != null)
            changeCharacterButton.interactable = false;

        UpdateCharacterButtons();
        PlaySound(lockInSound);
        UpdatePlayerStatus();

        if (PhotonNetwork.OfflineMode)
        {
            // Offline: set AISessionConfig with player selection and random AI, then start gameplay
            var cfg = RetroDodge.AISessionConfig.Instance;
            int aiIndex = availableCharacters != null && availableCharacters.Length > 0 ? Random.Range(0, availableCharacters.Length) : 0;
            cfg.SetPlayWithAI(cfg.difficulty, selectedCharacterIndex, aiIndex);

            TransitionToGameplay();
        }
        else
        {
            CheckBothPlayersReady();
        }
    }
    

    #endregion

    #region Timer System

    void StartSelectionTimer()
    {
        if (PhotonNetwork.OfflineMode)
        {
            // No timer needed offline
            timerPanel?.SetActive(false);
            return;
        }
        if (timerStarted) return;

        selectionStartTime = PhotonNetwork.Time;
        timerStarted = true;

        if (PhotonNetwork.IsMasterClient)
        {
            // FIXED: Safe room property update with proper checks
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && 
                PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Joined)
            {
                // FIXED: Use centralized room state manager with fallback
                RoomStateManager.GetOrCreateInstance()?.SetCharacterSelectionState(true, selectionStartTime);
            }
        }
    }

    void UpdateTimer()
    {
        if (!timerStarted || PhotonNetwork.OfflineMode) return;

        double elapsedTime = PhotonNetwork.Time - selectionStartTime;
        timeRemaining = selectionTimeLimit - (float)elapsedTime;

        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            OnTimerExpired();
            return;
        }

        UpdateTimerDisplay();

        if (timeRemaining <= 5f && timeRemaining > 4.8f && !isCharacterLocked)
        {
            PlaySound(timerWarningSound);
        }
    }

    void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";

            if (timeRemaining <= 10f)
                timerText.color = Color.red;
            else if (timeRemaining <= 30f)
                timerText.color = Color.yellow;
            else
                timerText.color = Color.white;
        }

        if (timerFillImage != null)
        {
            timerFillImage.fillAmount = timeRemaining / selectionTimeLimit;
        }
    }

    void OnTimerExpired()
    {
        if (isTransitioning) return;

        timerStarted = false;

        if (selectedCharacterIndex == -1 && !isCharacterLocked)
        {
            OnCharacterSelected(0);
            StartCoroutine(AutoLockAfterSelection());
        }
        else if (selectedCharacterIndex != -1 && !isCharacterLocked)
        {
            LockInCharacter();
        }

        StartCoroutine(CheckReadyAfterDelay());
    }

    IEnumerator AutoLockAfterSelection()
    {
        yield return new WaitForSeconds(0.5f);
        LockInCharacter();
    }

    IEnumerator CheckReadyAfterDelay()
    {
        yield return new WaitForSeconds(1f);

        bool allPlayersReady = true;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (!IsPlayerReady(player))
            {
                allPlayersReady = false;
                break;
            }
        }

        if (allPlayersReady)
        {
            TransitionToGameplay();
        }
        else
        {
            HandleOpponentDodged();
        }
    }

    bool IsPlayerReady(Player player)
    {
        if (debugMode) Debug.Log($"[CHAR SELECT] Checking if player {player.NickName} is ready...");
        
        if (player.CustomProperties.TryGetValue(RoomStateManager.PLAYER_LOCKED_KEY, out object lockedObj) &&
            player.CustomProperties.TryGetValue(RoomStateManager.PLAYER_CHARACTER_KEY, out object characterObj))
        {
            bool isLocked = (bool)lockedObj;
            int characterIndex = (int)characterObj;
            bool isValidCharacter = characterIndex >= 0 && characterIndex < availableCharacters.Length;
            
            if (debugMode) Debug.Log($"[CHAR SELECT] Player {player.NickName}: Locked={isLocked}, CharacterIndex={characterIndex}, ValidCharacter={isValidCharacter}");
            
            return isLocked && isValidCharacter;
        }
        
        if (debugMode) Debug.Log($"[CHAR SELECT] Player {player.NickName}: Missing properties in CustomProperties. Available keys: {string.Join(", ", player.CustomProperties.Keys)}");
        return false;
    }

    #endregion

    #region Player Status & Room Management

    void UpdatePlayerStatus()
    {
        if (PhotonNetwork.OfflineMode)
        {
            if (localPlayerStatusText != null)
                localPlayerStatusText.text = "You: Selecting (Offline)";
            if (remotePlayerStatusText != null)
                remotePlayerStatusText.text = "Opponent: AI (Random)";
            if (roomInfoText != null)
                roomInfoText.text = "Mode: Offline vs AI";
            return;
        }

        string localStatus = GetPlayerStatusText(PhotonNetwork.LocalPlayer);
        if (localPlayerStatusText != null)
            localPlayerStatusText.text = $"You: {localStatus}";

        string remoteStatus = "Waiting...";
        foreach (Player player in PhotonNetwork.PlayerListOthers)
        {
            remoteStatus = GetPlayerStatusText(player);
            break;
        }

        if (remotePlayerStatusText != null)
            remotePlayerStatusText.text = $"Opponent: {remoteStatus}";

        if (roomInfoText != null && PhotonNetwork.CurrentRoom != null)
        {
            roomInfoText.text = $"Room: {PhotonNetwork.CurrentRoom.Name} | Players: {PhotonNetwork.CurrentRoom.PlayerCount}/2";
        }
    }

    string GetPlayerStatusText(Player player)
    {
        string playerName = player.NickName;

        if (player.CustomProperties.TryGetValue(RoomStateManager.PLAYER_LOCKED_KEY, out object lockedObj) &&
            player.CustomProperties.TryGetValue(RoomStateManager.PLAYER_CHARACTER_KEY, out object characterObj))
        {
            bool isLocked = (bool)lockedObj;
            int characterIndex = (int)characterObj;

            if (isLocked && characterIndex >= 0 && characterIndex < availableCharacters.Length)
            {
                return $"{playerName} (Locked: {availableCharacters[characterIndex].characterName})";
            }
            else if (characterIndex >= 0 && characterIndex < availableCharacters.Length)
            {
                return $"{playerName} (Selected: {availableCharacters[characterIndex].characterName})";
            }
        }

        return $"{playerName} (Selecting...)";
    }

    void CheckBothPlayersReady()
    {
        bool allPlayersReady = true;
        int readyCount = 0;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            bool isReady = IsPlayerReady(player);
            if (isReady) readyCount++;
            
            if (!isReady)
            {
                allPlayersReady = false;
            }
            
            if (debugMode) Debug.Log($"[CHAR SELECT] Player {player.NickName}: Ready={isReady}");
        }

        if (debugMode) Debug.Log($"[CHAR SELECT] Ready check: {readyCount}/{PhotonNetwork.PlayerList.Length} players ready, AllReady={allPlayersReady}");

        if (allPlayersReady)
        {
            if (debugMode) Debug.Log("[CHAR SELECT] All players ready! Transitioning to gameplay...");
            StoreSelectionDataForGameplay();
            TransitionToGameplay();
        }
    }

    void StoreSelectionDataForGameplay()
    {
        CharacterData player1Character = null;
        CharacterData player2Character = null;

        Player[] sortedPlayers = PhotonNetwork.PlayerList;
        System.Array.Sort(sortedPlayers, (p1, p2) => p1.ActorNumber.CompareTo(p2.ActorNumber));

        for (int i = 0; i < sortedPlayers.Length && i < 2; i++)
        {
            Player player = sortedPlayers[i];
            if (player.CustomProperties.TryGetValue(RoomStateManager.PLAYER_CHARACTER_KEY, out object characterObj))
            {
                int characterIndex = (int)characterObj;
                if (characterIndex >= 0 && characterIndex < availableCharacters.Length)
                {
                    CharacterData character = availableCharacters[characterIndex];

                    if (i == 0)
                        player1Character = character;
                    else
                        player2Character = character;
                }
            }
        }
    }

    #endregion

    #region Scene Transitions & Dodge Handling

    void TransitionToGameplay()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        StartCoroutine(TransitionToGameplayCoroutine());
    }

    IEnumerator TransitionToGameplayCoroutine()
    {
        SetPanelActive(characterSelectionPanel, false);
        SetPanelActive(selectedCharacterPanel, false);
        SetPanelActive(timerPanel, false);
        SetPanelActive(loadingPanel, true);

        if (loadingText != null)
            loadingText.text = "Starting match...";

        PlaySound(transitionSound);

        yield return new WaitForSeconds(2f);

        // Decide which gameplay scene to load based on selected map / mode
        string sceneToLoad = ResolveGameplaySceneName();

        if (PhotonNetwork.OfflineMode)
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(sceneToLoad);
        }
    }

    void HandleOpponentDodged()
    {
        StartCoroutine(HandleOpponentDodgedCoroutine());
    }

    IEnumerator HandleOpponentDodgedCoroutine()
    {
        SetPanelActive(characterSelectionPanel, false);
        SetPanelActive(selectedCharacterPanel, false);
        SetPanelActive(timerPanel, false);
        SetPanelActive(loadingPanel, true);

        if (loadingText != null)
            loadingText.text = "Opponent disconnected. Returning to main menu...";

        yield return new WaitForSeconds(3f);

        PhotonNetwork.LeaveRoom();
    }

    void ReturnToMainMenu()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    #endregion

    #region Map / Scene Resolution

    /// <summary>
    /// Resolve the gameplay scene name based on room properties (online)
    /// or AI session config / random map (offline).
    /// </summary>
    string ResolveGameplaySceneName()
    {
        // OFFLINE: AI / local play uses MapRegistry for a fresh random pick every time
        if (PhotonNetwork.OfflineMode)
        {
            var cfg = RetroDodge.AISessionConfig.Instance;
            var registry = MapRegistry.Instance;

            if (registry != null)
            {
                var maps = registry.GetUnlockedMaps();
                if (maps != null && maps.Length > 0)
                {
                    int index = UnityEngine.Random.Range(0, maps.Length);
                    string sceneName = maps[index].sceneName;

                    if (cfg != null)
                    {
                        cfg.gameplaySceneName = sceneName;
                    }

                    if (debugMode)
                    {
                        Debug.Log($"[NET CHAR SELECT] OFFLINE random map selected: {maps[index].mapId} -> scene '{sceneName}'");
                    }

                    return sceneName;
                }
            }

            // Fallback to existing config value, then serialized default
            if (cfg != null && !string.IsNullOrEmpty(cfg.gameplaySceneName))
            {
                if (debugMode)
                {
                    Debug.Log($"[NET CHAR SELECT] OFFLINE using AISessionConfig scene '{cfg.gameplaySceneName}' (no maps from registry)");
                }
                return cfg.gameplaySceneName;
            }

            if (debugMode)
            {
                Debug.Log($"[NET CHAR SELECT] OFFLINE fallback to default gameplayScene '{gameplayScene}'");
            }
            return gameplayScene;
        }

        // ONLINE: use room properties to determine map, then MapRegistry for scene
        string mapId = null;
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null)
        {
            var props = PhotonNetwork.CurrentRoom.CustomProperties;

            // Competitive / some modes use RoomStateManager.ROOM_SELECTED_MAP ("SelectedMap")
            if (props.ContainsKey(RoomStateManager.ROOM_SELECTED_MAP))
            {
                mapId = props[RoomStateManager.ROOM_SELECTED_MAP] as string;
            }
            // Quick / Custom rooms use SELECTED_MAP_KEY ("SM")
            else if (props.ContainsKey("SM"))
            {
                mapId = props["SM"] as string;
            }
        }

        var mapRegistry = MapRegistry.Instance;
        if (!string.IsNullOrEmpty(mapId) && mapRegistry != null)
        {
            var map = mapRegistry.GetMapById(mapId);
            if (map != null && !string.IsNullOrEmpty(map.sceneName))
            {
                if (debugMode)
                {
                    Debug.Log($"[NET CHAR SELECT] ONLINE mapId='{mapId}' -> scene '{map.sceneName}'");
                }
                return map.sceneName;
            }

            if (debugMode)
            {
                Debug.Log($"[NET CHAR SELECT] ONLINE mapId='{mapId}' not found in registry, falling back to default scene '{gameplayScene}'");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"[NET CHAR SELECT] ONLINE no valid mapId in room properties, using default scene '{gameplayScene}'");
        }

        // Fallback: default gameplay scene
        return gameplayScene;
    }

    #endregion

    #region UI Helpers

    void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
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

    #region MonoBehaviourPunCallbacks Overrides - Dodge Detection

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (debugMode) Debug.Log($"[CHAR SELECT] Player properties updated for {targetPlayer.NickName}: {string.Join(", ", changedProps.Keys)}");
        
        UpdatePlayerStatus();
        CheckBothPlayersReady();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(RoomStateManager.TIMER_STARTED_KEY) &&
            propertiesThatChanged.ContainsKey(RoomStateManager.SELECTION_START_TIME_KEY))
        {
            bool timerActive = (bool)propertiesThatChanged[RoomStateManager.TIMER_STARTED_KEY];
            double startTime = (double)propertiesThatChanged[RoomStateManager.SELECTION_START_TIME_KEY];

            if (timerActive && !timerStarted)
            {
                selectionStartTime = startTime;
                timerStarted = true;
            }
        }
    }

    // VALORANT-STYLE DODGE DETECTION
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Player has dodged by leaving the room (Alt+F4, network disconnect, etc.)
        if (!isTransitioning)
        {
            StartCoroutine(HandlePlayerDodgedCoroutine());
        }
    }

    IEnumerator HandlePlayerDodgedCoroutine()
    {
        isTransitioning = true;

        SetPanelActive(characterSelectionPanel, false);
        SetPanelActive(selectedCharacterPanel, false);
        SetPanelActive(timerPanel, false);
        SetPanelActive(loadingPanel, true);

        if (loadingText != null)
            loadingText.text = "Opponent disconnected. Returning to main menu...";

        yield return new WaitForSeconds(3f);

        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[CHARACTER SELECTION] Left room, returning to main menu");
        isLeavingRoom = true; // FIXED: Set flag to prevent any remaining property updates
        SceneManager.LoadScene("MainMenu");
    }
    
    /// <summary>
    /// FIXED: Method to safely leave room and prevent property updates
    /// </summary>
    public void SafeLeaveRoom()
    {
        isLeavingRoom = true;
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerStatus();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Handle master client switching if needed
    }

    // Handle network disconnection from Photon servers
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SceneManager.LoadScene("MainMenu");
        }
    }


    #endregion

    #region Character Preview System

    /// <summary>
    /// Get preview prefab for character (separate from gameplay prefab)
    /// </summary>
    GameObject GetPreviewPrefabForCharacter(CharacterData characterData)
    {
        if (characterPreviewPrefabs == null || characterPreviewPrefabs.Length == 0)
        {
            return null;
        }

        // Find preview prefab by character name
        foreach (var previewPrefab in characterPreviewPrefabs)
        {
            if (previewPrefab != null && previewPrefab.name.Contains(characterData.characterName))
            {
                return previewPrefab;
            }
        }

        // Fallback: try to match by index if names don't match
        int characterIndex = GetCharacterIndex(characterData);
        if (characterIndex >= 0 && characterIndex < characterPreviewPrefabs.Length)
        {
            return characterPreviewPrefabs[characterIndex];
        }

        return null;
    }

    /// <summary>
    /// Get character index for preview prefab matching
    /// </summary>
    int GetCharacterIndex(CharacterData characterData)
    {
        for (int i = 0; i < availableCharacters.Length; i++)
        {
            if (availableCharacters[i] == characterData)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Spawn character preview when character is selected
    /// </summary>
    void SpawnCharacterPreview(CharacterData characterData)
    {
        if (characterData == null || characterData.characterPrefab == null)
        {
            Debug.LogWarning($"[CHAR PREVIEW] Character data or prefab is null for {characterData?.characterName ?? "Unknown"}");
            return;
        }

        // Destroy current preview if exists
        DestroyCurrentPreview();

        // Create preview parent if not assigned
        if (characterPreviewParent == null)
        {
            GameObject previewParent = new GameObject("CharacterPreviewParent");
            characterPreviewParent = previewParent.transform;
            characterPreviewParent.position = previewPosition;
        }

        // Use separate preview prefab instead of gameplay prefab
        GameObject previewPrefab = GetPreviewPrefabForCharacter(characterData);
        if (previewPrefab == null)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[CHAR PREVIEW] No preview prefab found for {characterData.characterName}. Using gameplay prefab as fallback.");
            }
            // Fallback to gameplay prefab if no preview prefab
            currentPreviewCharacter = Instantiate(characterData.characterPrefab, characterPreviewParent);
            DisablePreviewComponents(); // Disable all scripts as fallback
        }
        else
        {
            // Spawn preview character using dedicated preview prefab (no network components!)
            currentPreviewCharacter = Instantiate(previewPrefab, characterPreviewParent);
        }
        
        // Set position, rotation, and scale
        currentPreviewCharacter.transform.localPosition = Vector3.zero;
        currentPreviewCharacter.transform.localRotation = Quaternion.Euler(previewRotation);
        currentPreviewCharacter.transform.localScale = Vector3.one * previewScale;

        // Get animator for preview animations
        currentPreviewAnimator = currentPreviewCharacter.GetComponent<Animator>();
        
        // Apply custom animator controller if provided
        if (previewAnimatorController != null && currentPreviewAnimator != null)
        {
            currentPreviewAnimator.runtimeAnimatorController = previewAnimatorController;
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Applied custom animator controller: {previewAnimatorController.name}");
            }
        }

        // Start preview animation (simplified for custom controller)
        if (enablePreviewAnimations && currentPreviewAnimator != null)
        {
            StartPreviewAnimation();
        }

        if (debugMode)
        {
            Debug.Log($"[CHAR PREVIEW] Spawned preview for {characterData.characterName}");
        }
    }

    /// <summary>
    /// Destroy current character preview
    /// </summary>
    void DestroyCurrentPreview()
    {
        if (currentPreviewCharacter != null)
        {
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Destroying current preview: {currentPreviewCharacter.name}");
            }
            
            DestroyImmediate(currentPreviewCharacter);
            currentPreviewCharacter = null;
            currentPreviewAnimator = null;
        }
    }

    /// <summary>
    /// Disable components that shouldn't be active in character preview
    /// </summary>
    void DisablePreviewComponents()
    {
        if (currentPreviewCharacter == null) return;

        // DISABLE ALL SCRIPTS - Keep only Animator for preview
        var allMonoBehaviours = currentPreviewCharacter.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in allMonoBehaviours)
        {
            if (script != null && !(script is Animator))
            {
                script.enabled = false;
                if (debugMode)
                {
                    Debug.Log($"[CHAR PREVIEW] Disabled script: {script.GetType().Name}");
                }
            }
        }

        // CRITICAL: DESTROY all network components to prevent Photon errors
        // This is better than disabling because it completely removes network functionality
        var photonView = currentPreviewCharacter.GetComponent<PhotonView>();
        if (photonView != null)
        {
            DestroyImmediate(photonView);
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Destroyed PhotonView component");
            }
        }

        // Destroy SmoothSync components
        var smoothSync = currentPreviewCharacter.GetComponent<Smooth.SmoothSyncPUN2>();
        if (smoothSync != null)
        {
            DestroyImmediate(smoothSync);
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Destroyed SmoothSyncPUN2 component");
            }
        }

        // Destroy OptimizedPlayerSync
        var optimizedPlayerSync = currentPreviewCharacter.GetComponent<OptimizedPlayerSync>();
        if (optimizedPlayerSync != null)
        {
            DestroyImmediate(optimizedPlayerSync);
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Destroyed OptimizedPlayerSync component");
            }
        }

        // Destroy any other network-related components
        var networkComponents = currentPreviewCharacter.GetComponents<MonoBehaviourPunCallbacks>();
        foreach (var component in networkComponents)
        {
            DestroyImmediate(component);
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Destroyed network component: {component.GetType().Name}");
            }
        }

        // Also destroy any PhotonAnimatorView components
        var photonAnimatorView = currentPreviewCharacter.GetComponent<PhotonAnimatorView>();
        if (photonAnimatorView != null)
        {
            DestroyImmediate(photonAnimatorView);
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Destroyed PhotonAnimatorView component");
            }
        }

        // Destroy any remaining Photon components recursively
        var allPhotonComponents = currentPreviewCharacter.GetComponentsInChildren<Component>()
            .Where(c => c != null && (c.GetType().Name.Contains("Photon") || 
                                     c.GetType().Name.Contains("SmoothSync") ||
                                     c.GetType().Name.Contains("Optimized")))
            .ToArray();

        foreach (var component in allPhotonComponents)
        {
            if (component != null && !(component is Transform) && !(component is Animator))
            {
                DestroyImmediate(component);
                if (debugMode)
                {
                    Debug.Log($"[CHAR PREVIEW] Destroyed network component: {component.GetType().Name}");
                }
            }
        }

        // Disable colliders to prevent physics interactions
        var colliders = currentPreviewCharacter.GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        // Disable rigidbodies
        var rigidbodies = currentPreviewCharacter.GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigidbodies)
        {
            rb.isKinematic = true;
        }

        if (debugMode)
        {
            Debug.Log($"[CHAR PREVIEW] Disabled preview components for {currentPreviewCharacter.name}");
        }
    }

    /// <summary>
    /// Start preview animation (simplified for custom controller)
    /// </summary>
    void StartPreviewAnimation()
    {
        if (currentPreviewAnimator == null) return;

        // If using custom animator controller, it will handle transitions automatically
        if (previewAnimatorController != null)
        {
            // Custom controller handles all transitions automatically
            if (debugMode)
            {
                Debug.Log($"[CHAR PREVIEW] Using custom animator controller - animations will play automatically");
            }
        }
        else
        {
            // Fallback for manual animation control
            currentPreviewAnimator.SetTrigger("Idle");
            
            if (enablePreviewAnimations)
            {
                StartCoroutine(PlayPreviewVictoryAnimation());
            }
        }

        if (debugMode)
        {
            Debug.Log($"[CHAR PREVIEW] Started preview animation");
        }
    }

    /// <summary>
    /// Play victory animation for preview
    /// </summary>
    System.Collections.IEnumerator PlayPreviewVictoryAnimation()
    {
        yield return new WaitForSeconds(1f);
        
        if (currentPreviewAnimator != null)
        {
            currentPreviewAnimator.SetTrigger("Victory");
        }
        
        yield return new WaitForSeconds(2f);
        
        // Return to idle
        if (currentPreviewAnimator != null)
        {
            currentPreviewAnimator.SetTrigger("Idle");
        }
    }

    /// <summary>
    /// Update character preview when changing characters
    /// </summary>
    public void OnChangeCharacterButtonClicked()
    {
        if (isCharacterLocked || isTransitioning)
        {
            return;
        }

        // Destroy current preview
        DestroyCurrentPreview();

        SetPanelActive(selectedCharacterPanel, false);
        selectedCharacterIndex = -1;

        // FIXED: Use centralized room state manager with fallback
        RoomStateManager.GetOrCreateInstance()?.SetPlayerProperty(RoomStateManager.PLAYER_CHARACTER_KEY, -1);

        UpdateCharacterButtons();
        UpdatePlayerStatus();
    }

    #endregion
}