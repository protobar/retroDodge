using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Fixed PUN2 Character Selection Manager
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

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip characterSelectSound;
    [SerializeField] private AudioClip lockInSound;
    [SerializeField] private AudioClip timerWarningSound;
    [SerializeField] private AudioClip transitionSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Selection state
    private int selectedCharacterIndex = -1;
    private bool isCharacterLocked = false;
    private float timeRemaining;
    private bool timerStarted = false;
    private bool isTransitioning = false;
    private double selectionStartTime = 0;

    // Room properties keys
    private const string PLAYER_CHARACTER_KEY = "SelectedCharacter";
    private const string PLAYER_LOCKED_KEY = "CharacterLocked";
    private const string TIMER_STARTED_KEY = "TimerStarted";
    private const string SELECTION_START_TIME_KEY = "SelectionStartTime";

    // UI References
    private Button[] characterButtons;

    #region Unity Lifecycle

    void Start()
    {
        // Small delay to ensure room is properly initialized
        StartCoroutine(InitializeWithDelay());
    }

    IEnumerator InitializeWithDelay()
    {
        yield return new WaitForSeconds(0.5f);
        InitializeCharacterSelection();
        UpdatePlayerStatus();

        // Wait a bit more then start timer
        yield return new WaitForSeconds(1f);
        StartSelectionTimer();
    }

    void Update()
    {
        if (timerStarted && !isTransitioning)
        {
            UpdateTimer();
        }

        // Debug controls
        if (debugMode)
        {
            HandleDebugInput();
        }
    }

    #endregion

    #region Initialization

    // COMPLETE REPLACEMENT FOR THE PROBLEMATIC METHODS

    void InitializeCharacterSelection()
    {
        // Validate we're in a room
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("Not in a Photon room! Returning to main menu.");
            ReturnToMainMenu();
            return;
        }

        if (debugMode)
        {
            Debug.Log($"Initializing character selection. Room: {PhotonNetwork.CurrentRoom.Name}, Players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        }

        // CRITICAL FIX: Proper panel activation - DON'T hide character selection panel!
        SetPanelActive(characterSelectionPanel, true);   // KEEP THIS VISIBLE
        SetPanelActive(selectedCharacterPanel, false);   // Hide selected character details initially
        SetPanelActive(loadingPanel, false);            // Hide loading
        SetPanelActive(timerPanel, true);              // Show timer
        SetPanelActive(playerStatusPanel, true);       // Show player status

        // Validate character data
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogError("No characters available! Check CharacterData array in inspector.");
            return;
        }

        // Create character selection buttons FIRST
        CreateCharacterButtons();

        // Initialize timer display
        timeRemaining = selectionTimeLimit;
        UpdateTimerDisplay();

        // Set initial player properties
        Hashtable playerProps = new Hashtable();
        playerProps[PLAYER_CHARACTER_KEY] = -1;
        playerProps[PLAYER_LOCKED_KEY] = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);

        // Setup button listeners
        SetupButtonListeners();

        if (debugMode)
        {
            Debug.Log($"Character selection initialized for {PhotonNetwork.LocalPlayer.NickName}");
            Debug.Log($"Available characters: {availableCharacters.Length}");
            Debug.Log($"Character buttons created: {(characterButtons != null ? characterButtons.Length : 0)}");
        }
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

        if (characterGridParent == null)
        {
            Debug.LogError("Character Grid Parent is not assigned!");
            return;
        }

        if (characterButtonPrefab == null)
        {
            Debug.LogError("Character Button Prefab is not assigned!");
            return;
        }

        // Clear existing buttons
        foreach (Transform child in characterGridParent)
        {
            DestroyImmediate(child.gameObject);
        }

        // Create character buttons
        characterButtons = new Button[availableCharacters.Length];

        for (int i = 0; i < availableCharacters.Length; i++)
        {
            int characterIndex = i; // Capture for closure
            CharacterData character = availableCharacters[i];

            if (character == null)
            {
                Debug.LogError($"Character at index {i} is null!");
                continue;
            }

            // Instantiate button
            GameObject buttonObj = Instantiate(characterButtonPrefab, characterGridParent);
            Button button = buttonObj.GetComponent<Button>();

            if (button == null)
            {
                Debug.LogError($"Character Button Prefab doesn't have a Button component!");
                continue;
            }

            characterButtons[i] = button;

            // CRITICAL FIX: Ensure button is properly configured
            button.interactable = true;
            button.enabled = true;

            // Setup button visual
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                if (character.characterIcon != null)
                {
                    buttonImage.sprite = character.characterIcon;
                }
                else
                {
                    // Set default color if no icon
                    buttonImage.color = Color.cyan;
                }

                // CRITICAL: Ensure the image can receive raycast events
                buttonImage.raycastTarget = true;
            }

            // Setup button text
            TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = character.characterName;
            }
            else
            {
                // Try regular Text component
                Text regularText = button.GetComponentInChildren<Text>();
                if (regularText != null)
                {
                    regularText.text = character.characterName;
                }
            }

            // CRITICAL FIX: Clear existing listeners and add new one
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => {
                if (debugMode)
                {
                    Debug.Log($"Button clicked for character {characterIndex}: {character.characterName}");
                }
                OnCharacterSelected(characterIndex);
            });

            // Ensure button object is active and visible
            buttonObj.SetActive(true);

            // CRITICAL: Ensure Canvas Group doesn't block interaction
            CanvasGroup canvasGroup = buttonObj.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (debugMode)
            {
                Debug.Log($"Created button {i} for {character.characterName} - Interactable: {button.interactable}");
            }
        }

        // CRITICAL: Force Canvas to rebuild layout
        StartCoroutine(ForceCanvasRebuild());

        if (debugMode)
        {
            Debug.Log($"Created {characterButtons.Length} character buttons");
        }
    }

    // FORCE CANVAS REBUILD TO ENSURE BUTTONS ARE PROPERLY POSITIONED
    IEnumerator ForceCanvasRebuild()
    {
        yield return new WaitForEndOfFrame();

        // Force canvas to rebuild
        Canvas.ForceUpdateCanvases();

        // Check if EventSystem exists
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogError("No EventSystem found! UI buttons won't work without EventSystem.");

            // Try to find EventSystem in scene
            var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogError("Creating new EventSystem...");
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        if (debugMode)
        {
            Debug.Log("Canvas rebuild completed");
        }
    }

    #endregion

    #region Character Selection Logic

    void OnCharacterSelected(int characterIndex)
    {
        if (debugMode)
        {
            Debug.Log($"OnCharacterSelected called with index: {characterIndex}");
            Debug.Log($"Current state - Locked: {isCharacterLocked}, Transitioning: {isTransitioning}");
        }

        if (isCharacterLocked || isTransitioning)
        {
            if (debugMode)
            {
                Debug.Log("Character selection blocked - already locked or transitioning");
            }
            return;
        }

        if (characterIndex < 0 || characterIndex >= availableCharacters.Length)
        {
            Debug.LogError($"Invalid character index: {characterIndex}");
            return;
        }

        selectedCharacterIndex = characterIndex;
        CharacterData selectedCharacter = availableCharacters[characterIndex];

        if (debugMode)
        {
            Debug.Log($"Character selected: {selectedCharacter.characterName} (Index: {characterIndex})");
        }

        // Update UI
        UpdateCharacterDisplay(selectedCharacter);
        UpdateCharacterButtons();

        // Update player properties
        Hashtable playerProps = new Hashtable();
        playerProps[PLAYER_CHARACTER_KEY] = characterIndex;
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);

        // Play sound and effects
        PlaySound(characterSelectSound);

        // Show selected character panel
        SetPanelActive(selectedCharacterPanel, true);

        // Update status immediately
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

        // Update button states
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

            // Highlight selected character
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

            // Disable button if character is locked
            button.interactable = !isCharacterLocked;
        }
    }

    public void OnLockInButtonClicked()
    {
        if (selectedCharacterIndex == -1 || isCharacterLocked || isTransitioning)
        {
            if (debugMode)
            {
                Debug.Log($"Lock in blocked - Index: {selectedCharacterIndex}, Locked: {isCharacterLocked}, Transitioning: {isTransitioning}");
            }
            return;
        }

        LockInCharacter();
    }

    public void OnChangeCharacterButtonClicked()
    {
        if (isCharacterLocked || isTransitioning)
        {
            return;
        }

        // Hide selected character panel, show selection again
        SetPanelActive(selectedCharacterPanel, false);
        selectedCharacterIndex = -1;

        // Update player properties
        Hashtable playerProps = new Hashtable();
        playerProps[PLAYER_CHARACTER_KEY] = -1;
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);

        UpdateCharacterButtons();
        UpdatePlayerStatus();
    }

    void LockInCharacter()
    {
        if (selectedCharacterIndex == -1) return;

        isCharacterLocked = true;
        CharacterData lockedCharacter = availableCharacters[selectedCharacterIndex];

        // Update player properties
        Hashtable playerProps = new Hashtable();
        playerProps[PLAYER_CHARACTER_KEY] = selectedCharacterIndex;
        playerProps[PLAYER_LOCKED_KEY] = true;
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);

        // Update UI
        if (lockInButton != null)
            lockInButton.interactable = false;

        if (changeCharacterButton != null)
            changeCharacterButton.interactable = false;

        UpdateCharacterButtons();

        // Play effects
        PlaySound(lockInSound);

        // Update status
        UpdatePlayerStatus();

        if (debugMode)
        {
            Debug.Log($"Locked in character: {lockedCharacter.characterName}");
        }

        // Check if both players are ready
        CheckBothPlayersReady();
    }

    #endregion

    #region Timer System

    void StartSelectionTimer()
    {
        // CRITICAL FIX: Any client can start the timer, but use synchronized time
        if (timerStarted) return;

        selectionStartTime = PhotonNetwork.Time;
        timerStarted = true;

        // Only master client sets room properties
        if (PhotonNetwork.IsMasterClient)
        {
            Hashtable roomProps = new Hashtable();
            roomProps[TIMER_STARTED_KEY] = true;
            roomProps[SELECTION_START_TIME_KEY] = selectionStartTime;
            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        }

        if (debugMode)
        {
            Debug.Log($"Selection timer started. Start time: {selectionStartTime}");
        }
    }

    void UpdateTimer()
    {
        if (!timerStarted) return;

        // Calculate time remaining using Photon synchronized time
        double elapsedTime = PhotonNetwork.Time - selectionStartTime;
        timeRemaining = selectionTimeLimit - (float)elapsedTime;

        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            OnTimerExpired();
            return;
        }

        UpdateTimerDisplay();

        // Play warning sound at 5 seconds
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

            // Change color based on time remaining
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

        if (debugMode)
        {
            Debug.Log("Selection timer expired!");
        }

        // If player hasn't selected anything, auto-select first character
        if (selectedCharacterIndex == -1 && !isCharacterLocked)
        {
            OnCharacterSelected(0);
            StartCoroutine(AutoLockAfterSelection());
        }
        else if (selectedCharacterIndex != -1 && !isCharacterLocked)
        {
            // Auto-lock current selection
            LockInCharacter();
        }

        // Check if both players are ready
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

        // Check if both players have locked characters
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
            OnMatchDodged();
        }
    }

    bool IsPlayerReady(Player player)
    {
        if (player.CustomProperties.TryGetValue(PLAYER_LOCKED_KEY, out object lockedObj) &&
            player.CustomProperties.TryGetValue(PLAYER_CHARACTER_KEY, out object characterObj))
        {
            bool isLocked = (bool)lockedObj;
            int characterIndex = (int)characterObj;
            return isLocked && characterIndex >= 0 && characterIndex < availableCharacters.Length;
        }
        return false;
    }

    #endregion

    #region Player Status & Room Management

    void UpdatePlayerStatus()
    {
        // Update local player status
        string localStatus = GetPlayerStatusText(PhotonNetwork.LocalPlayer);
        if (localPlayerStatusText != null)
            localPlayerStatusText.text = $"You: {localStatus}";

        // Update remote player status
        string remoteStatus = "Waiting...";
        foreach (Player player in PhotonNetwork.PlayerListOthers)
        {
            remoteStatus = GetPlayerStatusText(player);
            break; // Only one other player in 2-player game
        }

        if (remotePlayerStatusText != null)
            remotePlayerStatusText.text = $"Opponent: {remoteStatus}";

        // Update room info
        if (roomInfoText != null)
        {
            roomInfoText.text = $"Room: {PhotonNetwork.CurrentRoom.Name} | Players: {PhotonNetwork.CurrentRoom.PlayerCount}/2";
        }

        if (debugMode)
        {
            Debug.Log($"Status updated - Local: {localStatus}, Remote: {remoteStatus}");
        }
    }

    string GetPlayerStatusText(Player player)
    {
        string playerName = player.NickName;

        if (player.CustomProperties.TryGetValue(PLAYER_LOCKED_KEY, out object lockedObj) &&
            player.CustomProperties.TryGetValue(PLAYER_CHARACTER_KEY, out object characterObj))
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

        // Check all players in room
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
            if (debugMode)
            {
                Debug.Log("Both players ready! Transitioning to gameplay.");
            }

            StoreSelectionDataForGameplay();
            TransitionToGameplay();
        }
    }

    void StoreSelectionDataForGameplay()
    {
        // Get both players' selected characters
        CharacterData player1Character = null;
        CharacterData player2Character = null;

        Player[] sortedPlayers = PhotonNetwork.PlayerList;
        System.Array.Sort(sortedPlayers, (p1, p2) => p1.ActorNumber.CompareTo(p2.ActorNumber));

        for (int i = 0; i < sortedPlayers.Length && i < 2; i++)
        {
            Player player = sortedPlayers[i];
            if (player.CustomProperties.TryGetValue(PLAYER_CHARACTER_KEY, out object characterObj))
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

        if (debugMode)
        {
            Debug.Log($"Stored selection data - P1: {player1Character?.characterName}, P2: {player2Character?.characterName}");
        }
    }

    #endregion

    #region Scene Transitions

    void TransitionToGameplay()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        StartCoroutine(TransitionToGameplayCoroutine());
    }

    IEnumerator TransitionToGameplayCoroutine()
    {
        // Show loading panel
        SetPanelActive(characterSelectionPanel, false);
        SetPanelActive(selectedCharacterPanel, false);
        SetPanelActive(timerPanel, false);
        SetPanelActive(loadingPanel, true);

        if (loadingText != null)
            loadingText.text = "Starting match...";

        PlaySound(transitionSound);

        // Wait a moment for effect
        yield return new WaitForSeconds(2f);

        // Load gameplay scene (only master client loads)
        if (PhotonNetwork.IsMasterClient)
        {
            if (debugMode)
            {
                Debug.Log($"Loading gameplay scene: {gameplayScene}");
            }
            PhotonNetwork.LoadLevel(gameplayScene);
        }
    }

    void OnMatchDodged()
    {
        if (debugMode)
        {
            Debug.Log("Match dodged - not all players selected characters in time");
        }

        StartCoroutine(HandleMatchDodgeCoroutine());
    }

    IEnumerator HandleMatchDodgeCoroutine()
    {
        // Show message
        SetPanelActive(loadingPanel, true);
        if (loadingText != null)
            loadingText.text = "Match dodged! Returning to menu...";

        yield return new WaitForSeconds(3f);

        // Leave room and return to main menu
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

    #region UI Helpers

    void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
            if (debugMode)
            {
                Debug.Log($"Panel {panel.name} set to {active}");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"Trying to set null panel to {active}");
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

    #region MonoBehaviourPunCallbacks Overrides

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        UpdatePlayerStatus();

        if (debugMode)
        {
            Debug.Log($"Player {targetPlayer.NickName} updated properties");
            foreach (var prop in changedProps)
            {
                Debug.Log($"  {prop.Key}: {prop.Value}");
            }
        }

        // Check if both players are ready
        CheckBothPlayersReady();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // Handle timer synchronization
        if (propertiesThatChanged.ContainsKey(TIMER_STARTED_KEY) &&
            propertiesThatChanged.ContainsKey(SELECTION_START_TIME_KEY))
        {
            bool timerActive = (bool)propertiesThatChanged[TIMER_STARTED_KEY];
            double startTime = (double)propertiesThatChanged[SELECTION_START_TIME_KEY];

            if (timerActive && !timerStarted)
            {
                selectionStartTime = startTime;
                timerStarted = true;

                if (debugMode)
                {
                    Debug.Log($"Timer synchronized from room properties. Start time: {startTime}");
                }
            }
        }

        if (debugMode)
        {
            Debug.Log("Room properties updated");
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (debugMode)
        {
            Debug.Log($"Player {otherPlayer.NickName} left the room");
        }

        // If someone leaves during character selection, return to main menu
        StartCoroutine(HandlePlayerLeftCoroutine());
    }

    IEnumerator HandlePlayerLeftCoroutine()
    {
        SetPanelActive(loadingPanel, true);
        if (loadingText != null)
            loadingText.text = "Player left the match. Returning to menu...";

        yield return new WaitForSeconds(2f);
        ReturnToMainMenu();
    }

    public override void OnLeftRoom()
    {
        if (debugMode)
        {
            Debug.Log("Left room, returning to main menu");
        }
        SceneManager.LoadScene("MainMenu");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerStatus();

        if (debugMode)
        {
            Debug.Log($"Player {newPlayer.NickName} entered room");
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (debugMode)
        {
            Debug.Log($"Master client switched to {newMasterClient.NickName}");
        }
    }

    #endregion

    #region Debug

    void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && availableCharacters.Length > 0)
        {
            OnCharacterSelected(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && availableCharacters.Length > 1)
        {
            OnCharacterSelected(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) && availableCharacters.Length > 2)
        {
            OnCharacterSelected(2);
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            OnLockInButtonClicked();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            OnChangeCharacterButtonClicked();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToMainMenu();
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            // Force start timer
            StartSelectionTimer();
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 450, 500));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== CHARACTER SELECTION DEBUG ===");
        GUILayout.Label($"In Room: {PhotonNetwork.InRoom}");
        GUILayout.Label($"Room: {PhotonNetwork.CurrentRoom?.Name ?? "None"}");
        GUILayout.Label($"Players: {PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}/2");
        GUILayout.Label($"Master Client: {PhotonNetwork.IsMasterClient}");

        GUILayout.Space(5);
        GUILayout.Label($"Available Characters: {(availableCharacters != null ? availableCharacters.Length : 0)}");
        GUILayout.Label($"Character Buttons: {(characterButtons != null ? characterButtons.Length : 0)}");

        if (characterButtons != null)
        {
            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] != null)
                {
                    GUILayout.Label($"  Button {i}: Interactable={characterButtons[i].interactable}, Active={characterButtons[i].gameObject.activeInHierarchy}");
                }
            }
        }

        GUILayout.Label($"Selected Character: {(selectedCharacterIndex >= 0 && availableCharacters != null && selectedCharacterIndex < availableCharacters.Length ? availableCharacters[selectedCharacterIndex].characterName : "None")}");
        GUILayout.Label($"Character Locked: {isCharacterLocked}");
        GUILayout.Label($"Timer Started: {timerStarted}");
        GUILayout.Label($"Time Remaining: {timeRemaining:F1}s");
        GUILayout.Label($"Is Transitioning: {isTransitioning}");

        GUILayout.Space(5);
        GUILayout.Label("Panel States:");
        GUILayout.Label($"  Character Selection: {(characterSelectionPanel != null ? characterSelectionPanel.activeSelf.ToString() : "NULL")}");
        GUILayout.Label($"  Selected Character: {(selectedCharacterPanel != null ? selectedCharacterPanel.activeSelf.ToString() : "NULL")}");
        GUILayout.Label($"  Timer Panel: {(timerPanel != null ? timerPanel.activeSelf.ToString() : "NULL")}");
        GUILayout.Label($"  Player Status: {(playerStatusPanel != null ? playerStatusPanel.activeSelf.ToString() : "NULL")}");

        // EventSystem check
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        GUILayout.Label($"EventSystem: {(eventSystem != null ? "Found" : "MISSING!")}");

        GUILayout.Space(10);
        GUILayout.Label("Debug Controls:");
        GUILayout.Label("1/2/3 - Select Characters");
        GUILayout.Label("L - Lock In Character");
        GUILayout.Label("C - Change Character");
        GUILayout.Label("T - Force Start Timer");
        GUILayout.Label("ESC - Return to Menu");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion
}