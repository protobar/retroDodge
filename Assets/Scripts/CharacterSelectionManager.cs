using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Character Selection Manager - Network Ready Sequential Selection
/// Player 1 picks first, then Player 2, then match starts
/// </summary>
public class CharacterSelectionManager : MonoBehaviour
{
    [Header("Character Data")]
    [SerializeField] private CharacterData[] availableCharacters;
    [SerializeField] private int maxCharactersPerRow = 3;

    [Header("UI Panels")]
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private Button backToMenuButton;

    [Header("Player 1 UI")]
    [SerializeField] private TextMeshProUGUI player1Label;
    [SerializeField] private Image player1Portrait;
    [SerializeField] private TextMeshProUGUI player1CharacterName;
    [SerializeField] private TextMeshProUGUI player1CharacterDescription;
    [SerializeField] private Button player1ConfirmButton;

    [Header("Player 2 UI")]
    [SerializeField] private TextMeshProUGUI player2Label;
    [SerializeField] private Image player2Portrait;
    [SerializeField] private TextMeshProUGUI player2CharacterName;
    [SerializeField] private TextMeshProUGUI player2CharacterDescription;
    [SerializeField] private Button player2ConfirmButton;

    [Header("Character Grid")]
    [SerializeField] private Transform characterGridParent;
    [SerializeField] private GameObject characterButtonPrefab;

    [Header("Preview Area")]
    [SerializeField] private Transform previewSpawnPoint;
    [SerializeField] private Camera previewCamera;

    [Header("Input Settings")]
    [SerializeField] private KeyCode player1ConfirmKey = KeyCode.Return;
    [SerializeField] private KeyCode player2ConfirmKey = KeyCode.RightControl;
    [SerializeField] private KeyCode backKey = KeyCode.Escape;

    [Header("Network Settings (Future)")]
    [SerializeField] private bool enableNetworking = false;
    [SerializeField] private bool isHost = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip characterSelectSound;
    [SerializeField] private AudioClip characterConfirmSound;
    [SerializeField] private AudioClip matchStartSound;

    [Header("Scene Management")]
    [SerializeField] private string gameplayScene = "GameplayArena";
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Selection state
    public enum SelectionPhase { Player1Selecting, Player1Confirmed, Player2Selecting, Player2Confirmed, MatchReady }
    private SelectionPhase currentPhase = SelectionPhase.Player1Selecting;

    private CharacterData player1SelectedCharacter;
    private CharacterData player2SelectedCharacter;
    private int currentlyHighlightedIndex = 0;
    private List<Button> characterButtons = new List<Button>();
    private GameObject currentPreviewCharacter;

    // Network data storage for multiplayer
    private static CharacterSelectionData selectionData = new CharacterSelectionData();

    void Start()
    {
        InitializeCharacterSelection();
        CreateCharacterGrid();
        SetupInputHandlers();
        UpdateUI();

        if (debugMode)
        {
            Debug.Log("CharacterSelectionManager initialized - Network Ready: " + enableNetworking);
        }
    }

    void InitializeCharacterSelection()
    {
        currentPhase = SelectionPhase.Player1Selecting;
        player1SelectedCharacter = null;
        player2SelectedCharacter = null;
        currentlyHighlightedIndex = 0;

        // Setup UI panels
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(true);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        // Setup audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }
    }

    void CreateCharacterGrid()
    {
        if (characterGridParent == null || characterButtonPrefab == null) return;

        // Clear existing buttons
        foreach (Transform child in characterGridParent)
        {
            Destroy(child.gameObject);
        }
        characterButtons.Clear();

        // Create character selection buttons
        for (int i = 0; i < availableCharacters.Length; i++)
        {
            CharacterData character = availableCharacters[i];
            GameObject buttonObj = Instantiate(characterButtonPrefab, characterGridParent);

            CharacterSelectionButton charButton = buttonObj.GetComponent<CharacterSelectionButton>();
            if (charButton == null)
                charButton = buttonObj.AddComponent<CharacterSelectionButton>();

            charButton.Initialize(character, i, this);

            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                characterButtons.Add(button);
                int index = i; // Capture for closure
                button.onClick.AddListener(() => OnCharacterSelected(index));
            }
        }

        // Highlight first character
        if (characterButtons.Count > 0)
        {
            HighlightCharacter(0);
        }

        if (debugMode)
        {
            Debug.Log($"Created character grid with {availableCharacters.Length} characters");
        }
    }

    void SetupInputHandlers()
    {
        if (startMatchButton != null)
            startMatchButton.onClick.AddListener(OnStartMatchClicked);

        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);

        if (player1ConfirmButton != null)
            player1ConfirmButton.onClick.AddListener(OnPlayer1Confirm);

        if (player2ConfirmButton != null)
            player2ConfirmButton.onClick.AddListener(OnPlayer2Confirm);
    }

    void Update()
    {
        HandleKeyboardInput();

        if (debugMode)
        {
            HandleDebugInput();
        }
    }

    void HandleKeyboardInput()
    {
        // Navigation
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            NavigateCharacterSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            NavigateCharacterSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            NavigateCharacterSelection(-maxCharactersPerRow);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            NavigateCharacterSelection(maxCharactersPerRow);
        }

        // Selection confirmation
        if (currentPhase == SelectionPhase.Player1Selecting && Input.GetKeyDown(player1ConfirmKey))
        {
            OnCharacterSelected(currentlyHighlightedIndex);
        }
        else if (currentPhase == SelectionPhase.Player2Selecting && Input.GetKeyDown(player2ConfirmKey))
        {
            OnCharacterSelected(currentlyHighlightedIndex);
        }

        // Back/Cancel
        if (Input.GetKeyDown(backKey))
        {
            OnBackPressed();
        }
    }

    void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            // Quick select for testing
            if (currentPhase == SelectionPhase.Player1Selecting)
            {
                OnCharacterSelected(0);
            }
            else if (currentPhase == SelectionPhase.Player2Selecting)
            {
                OnCharacterSelected(1 % availableCharacters.Length);
            }
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            // Skip to match ready for testing
            if (availableCharacters.Length >= 2)
            {
                player1SelectedCharacter = availableCharacters[0];
                player2SelectedCharacter = availableCharacters[1];
                currentPhase = SelectionPhase.MatchReady;
                UpdateUI();
            }
        }
    }

    void NavigateCharacterSelection(int direction)
    {
        if (currentPhase != SelectionPhase.Player1Selecting && currentPhase != SelectionPhase.Player2Selecting)
            return;

        int newIndex = currentlyHighlightedIndex + direction;
        newIndex = Mathf.Clamp(newIndex, 0, availableCharacters.Length - 1);

        if (newIndex != currentlyHighlightedIndex)
        {
            HighlightCharacter(newIndex);
        }
    }

    void HighlightCharacter(int index)
    {
        currentlyHighlightedIndex = index;

        // Update button highlights
        for (int i = 0; i < characterButtons.Count; i++)
        {
            CharacterSelectionButton charButton = characterButtons[i].GetComponent<CharacterSelectionButton>();
            if (charButton != null)
            {
                charButton.SetHighlighted(i == index);
            }
        }

        // Update preview
        if (index >= 0 && index < availableCharacters.Length)
        {
            UpdateCharacterPreview(availableCharacters[index]);
        }

        // Play sound
        PlaySound(characterSelectSound);
    }

    void UpdateCharacterPreview(CharacterData character)
    {
        if (character == null || previewSpawnPoint == null) return;

        // Clear existing preview
        if (currentPreviewCharacter != null)
        {
            Destroy(currentPreviewCharacter);
        }

        // Spawn new preview if character has prefab
        if (character.characterPrefab != null)
        {
            currentPreviewCharacter = Instantiate(character.characterPrefab, previewSpawnPoint.position, previewSpawnPoint.rotation);

            // Disable unnecessary components for preview
            DisablePreviewComponents(currentPreviewCharacter);
        }

        // Update current player's UI
        UpdatePlayerUI(character);
    }

    void DisablePreviewComponents(GameObject previewObj)
    {
        // Disable input and physics for preview
        PlayerInputHandler inputHandler = previewObj.GetComponent<PlayerInputHandler>();
        if (inputHandler != null)
            inputHandler.enabled = false;

        Rigidbody rb = previewObj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        Collider col = previewObj.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        // Scale down for preview
        previewObj.transform.localScale *= 0.8f;
    }

    void UpdatePlayerUI(CharacterData character)
    {
        if (character == null) return;

        switch (currentPhase)
        {
            case SelectionPhase.Player1Selecting:
                UpdatePlayerDisplay(player1Portrait, player1CharacterName, player1CharacterDescription, character);
                break;
            case SelectionPhase.Player2Selecting:
                UpdatePlayerDisplay(player2Portrait, player2CharacterName, player2CharacterDescription, character);
                break;
        }
    }

    void UpdatePlayerDisplay(Image portrait, TextMeshProUGUI nameText, TextMeshProUGUI descText, CharacterData character)
    {
        if (portrait != null && character.characterIcon != null)
            portrait.sprite = character.characterIcon;

        if (nameText != null)
            nameText.text = character.characterName;

        if (descText != null)
            descText.text = character.characterDescription;
    }

    public void OnCharacterSelected(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= availableCharacters.Length) return;

        CharacterData selectedCharacter = availableCharacters[characterIndex];

        switch (currentPhase)
        {
            case SelectionPhase.Player1Selecting:
                player1SelectedCharacter = selectedCharacter;
                currentPhase = SelectionPhase.Player1Confirmed;
                PlaySound(characterConfirmSound);

                if (debugMode)
                {
                    Debug.Log($"Player 1 selected: {selectedCharacter.characterName}");
                }
                break;

            case SelectionPhase.Player2Selecting:
                player2SelectedCharacter = selectedCharacter;
                currentPhase = SelectionPhase.Player2Confirmed;
                PlaySound(characterConfirmSound);

                if (debugMode)
                {
                    Debug.Log($"Player 2 selected: {selectedCharacter.characterName}");
                }
                break;
        }

        UpdateUI();
        CheckMatchReady();
    }

    void CheckMatchReady()
    {
        if (player1SelectedCharacter != null && player2SelectedCharacter != null)
        {
            currentPhase = SelectionPhase.MatchReady;

            if (debugMode)
            {
                Debug.Log("Both players ready! Match can start.");
            }

            UpdateUI();
            StartCoroutine(ShowMatchReadySequence());
        }
        else if (currentPhase == SelectionPhase.Player1Confirmed)
        {
            // Move to Player 2 selection
            currentPhase = SelectionPhase.Player2Selecting;
            UpdateUI();
        }
    }

    IEnumerator ShowMatchReadySequence()
    {
        // Show confirmation panel
        if (confirmationPanel != null)
        {
            characterSelectionPanel.SetActive(false);
            confirmationPanel.SetActive(true);
        }

        // Wait a moment
        yield return new WaitForSeconds(1f);

        // Enable start button
        if (startMatchButton != null)
        {
            startMatchButton.interactable = true;
            startMatchButton.GetComponent<Image>().color = Color.green;
        }

        PlaySound(matchStartSound);
    }

    void UpdateUI()
    {
        // Update phase labels
        switch (currentPhase)
        {
            case SelectionPhase.Player1Selecting:
                SetPlayerUIState(player1Label, player1ConfirmButton, "PLAYER 1 - SELECT CHARACTER", true);
                SetPlayerUIState(player2Label, player2ConfirmButton, "PLAYER 2 - WAITING", false);
                break;

            case SelectionPhase.Player1Confirmed:
                SetPlayerUIState(player1Label, player1ConfirmButton, "PLAYER 1 - READY", false);
                SetPlayerUIState(player2Label, player2ConfirmButton, "PLAYER 2 - WAITING", false);
                currentPhase = SelectionPhase.Player2Selecting;
                break;

            case SelectionPhase.Player2Selecting:
                SetPlayerUIState(player1Label, player1ConfirmButton, "PLAYER 1 - READY", false);
                SetPlayerUIState(player2Label, player2ConfirmButton, "PLAYER 2 - SELECT CHARACTER", true);
                break;

            case SelectionPhase.Player2Confirmed:
                SetPlayerUIState(player1Label, player1ConfirmButton, "PLAYER 1 - READY", false);
                SetPlayerUIState(player2Label, player2ConfirmButton, "PLAYER 2 - READY", false);
                break;

            case SelectionPhase.MatchReady:
                SetPlayerUIState(player1Label, player1ConfirmButton, "PLAYER 1 - READY", false);
                SetPlayerUIState(player2Label, player2ConfirmButton, "PLAYER 2 - READY", false);
                break;
        }

        // Update character displays
        if (player1SelectedCharacter != null)
        {
            UpdatePlayerDisplay(player1Portrait, player1CharacterName, player1CharacterDescription, player1SelectedCharacter);
        }

        if (player2SelectedCharacter != null)
        {
            UpdatePlayerDisplay(player2Portrait, player2CharacterName, player2CharacterDescription, player2SelectedCharacter);
        }
    }

    void SetPlayerUIState(TextMeshProUGUI label, Button confirmButton, string labelText, bool buttonActive)
    {
        if (label != null)
            label.text = labelText;

        if (confirmButton != null)
            confirmButton.interactable = buttonActive;
    }

    #region Button Handlers

    public void OnPlayer1Confirm()
    {
        if (currentPhase == SelectionPhase.Player1Selecting)
        {
            OnCharacterSelected(currentlyHighlightedIndex);
        }
    }

    public void OnPlayer2Confirm()
    {
        if (currentPhase == SelectionPhase.Player2Selecting)
        {
            OnCharacterSelected(currentlyHighlightedIndex);
        }
    }

    public void OnStartMatchClicked()
    {
        if (currentPhase == SelectionPhase.MatchReady)
        {
            StartMatch();
        }
    }

    public void OnBackToMenuClicked()
    {
        SceneManager.LoadScene(mainMenuScene);
    }

    void OnBackPressed()
    {
        switch (currentPhase)
        {
            case SelectionPhase.Player1Selecting:
                OnBackToMenuClicked();
                break;

            case SelectionPhase.Player1Confirmed:
            case SelectionPhase.Player2Selecting:
                // Go back to Player 1 selection
                player1SelectedCharacter = null;
                currentPhase = SelectionPhase.Player1Selecting;
                UpdateUI();
                break;

            case SelectionPhase.Player2Confirmed:
            case SelectionPhase.MatchReady:
                // Go back to Player 2 selection
                player2SelectedCharacter = null;
                currentPhase = SelectionPhase.Player2Selecting;
                if (confirmationPanel != null)
                {
                    confirmationPanel.SetActive(false);
                    characterSelectionPanel.SetActive(true);
                }
                UpdateUI();
                break;
        }
    }

    #endregion

    #region Match Management

    void StartMatch()
    {
        if (player1SelectedCharacter == null || player2SelectedCharacter == null)
        {
            Debug.LogError("Cannot start match - missing character selections!");
            return;
        }

        // Store selection data for the gameplay scene
        selectionData.player1Character = player1SelectedCharacter;
        selectionData.player2Character = player2SelectedCharacter;
        selectionData.isNetworked = enableNetworking;
        selectionData.isHost = isHost;

        if (debugMode)
        {
            Debug.Log($"Starting match: {player1SelectedCharacter.characterName} vs {player2SelectedCharacter.characterName}");
        }

        // Transition to gameplay
        StartCoroutine(TransitionToGameplay());
    }

    IEnumerator TransitionToGameplay()
    {
        // Disable UI
        if (startMatchButton != null)
            startMatchButton.interactable = false;

        // Play transition effect
        PlaySound(matchStartSound);

        yield return new WaitForSeconds(1f);

        // Load gameplay scene
        SceneManager.LoadScene(gameplayScene);
    }

    #endregion

    #region Network Ready Methods (Future)

    /// <summary>
    /// Future: Handle networked character selection
    /// </summary>
    public void OnNetworkCharacterSelected(int playerID, CharacterData character)
    {
        if (debugMode)
        {
            Debug.Log($"Network: Player {playerID} selected {character.characterName}");
        }

        if (playerID == 1)
        {
            player1SelectedCharacter = character;
        }
        else if (playerID == 2)
        {
            player2SelectedCharacter = character;
        }

        UpdateUI();
        CheckMatchReady();
    }

    /// <summary>
    /// Future: Send character selection to network
    /// </summary>
    void SendCharacterSelectionToNetwork(CharacterData character)
    {
        if (!enableNetworking) return;

        // Future: PUN2 RPC implementation
        // photonView.RPC("OnCharacterSelected", RpcTarget.Others, character.name);

        if (debugMode)
        {
            Debug.Log($"Network: Sending character selection: {character.characterName}");
        }
    }

    /// <summary>
    /// Future: Called when both players are ready in network game
    /// </summary>
    public void OnNetworkMatchReady()
    {
        if (debugMode)
        {
            Debug.Log("Network: Both players ready, starting match");
        }

        currentPhase = SelectionPhase.MatchReady;
        UpdateUI();
        StartCoroutine(ShowMatchReadySequence());
    }

    #endregion

    #region Static Data Access

    /// <summary>
    /// Get the stored character selection data
    /// Called by MatchManager in gameplay scene
    /// </summary>
    public static CharacterSelectionData GetSelectionData()
    {
        return selectionData;
    }

    /// <summary>
    /// Clear selection data (call after match ends)
    /// </summary>
    public static void ClearSelectionData()
    {
        selectionData = new CharacterSelectionData();
    }

    #endregion

    #region Audio

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Debug

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 200));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== CHARACTER SELECTION DEBUG ===");
        GUILayout.Label($"Phase: {currentPhase}");
        GUILayout.Label($"Highlighted: {currentlyHighlightedIndex}");
        GUILayout.Label($"P1 Character: {(player1SelectedCharacter?.characterName ?? "None")}");
        GUILayout.Label($"P2 Character: {(player2SelectedCharacter?.characterName ?? "None")}");
        GUILayout.Label($"Networking: {enableNetworking}");

        GUILayout.Space(10);
        GUILayout.Label("Debug Controls:");
        GUILayout.Label("Arrow Keys - Navigate");
        GUILayout.Label("Enter/RCtrl - Confirm");
        GUILayout.Label("ESC - Back");
        GUILayout.Label("F1 - Quick Select");
        GUILayout.Label("F2 - Skip to Ready");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion
}

/// <summary>
/// Data structure for passing character selections between scenes
/// </summary>
[System.Serializable]
public class CharacterSelectionData
{
    public CharacterData player1Character;
    public CharacterData player2Character;
    public bool isNetworked = false;
    public bool isHost = true;

    public bool IsValid()
    {
        return player1Character != null && player2Character != null;
    }
}