using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ==================== MENU MANAGER ====================
public class MenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject characterSelectPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("Main Menu Buttons")]
    public Button playButton;
    public Button characterSelectButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Character Select")]
    public Button grudgeButton;
    public Button novaButton;
    public Button echoButton;
    public Button backButton;
    public Button confirmButton;

    [Header("Character Info Display")]
    public Text characterNameText;
    public Text characterDescriptionText;
    public Image characterIcon;
    public Text healthStatText;
    public Text speedStatText;
    public Text damageStatText;

    // Character data
    [Header("Character Data")]
    public CharacterStats grudgeStats;
    public CharacterStats novaStats;
    public CharacterStats echoStats;

    private CharacterStats selectedCharacter;
    private string selectedCharacterName = "";

    void Start()
    {
        InitializeMenu();
        ShowMainMenu();
    }

    void InitializeMenu()
    {
        // Setup button events
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        if (characterSelectButton != null)
            characterSelectButton.onClick.AddListener(ShowCharacterSelect);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettings);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(ShowCredits);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // Character select buttons
        if (grudgeButton != null)
            grudgeButton.onClick.AddListener(() => SelectCharacter(grudgeStats, "Grudge"));

        if (novaButton != null)
            novaButton.onClick.AddListener(() => SelectCharacter(novaStats, "Nova"));

        if (echoButton != null)
            echoButton.onClick.AddListener(() => SelectCharacter(echoStats, "Echo"));

        if (backButton != null)
            backButton.onClick.AddListener(ShowMainMenu);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmCharacter);

        // Default character selection
        if (grudgeStats != null)
        {
            SelectCharacter(grudgeStats, "Grudge");
        }
    }

    void ShowMainMenu()
    {
        SetActivePanel(mainMenuPanel);
        AudioManager.Instance?.PlaySound("MenuOpen");
    }

    void ShowCharacterSelect()
    {
        SetActivePanel(characterSelectPanel);
        AudioManager.Instance?.PlaySound("MenuTransition");
    }

    void ShowSettings()
    {
        SetActivePanel(settingsPanel);
        AudioManager.Instance?.PlaySound("MenuTransition");
    }

    void ShowCredits()
    {
        SetActivePanel(creditsPanel);
        AudioManager.Instance?.PlaySound("MenuTransition");
    }

    void SetActivePanel(GameObject activePanel)
    {
        // Hide all panels
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        // Show active panel
        if (activePanel != null)
        {
            activePanel.SetActive(true);
        }
    }

    void SelectCharacter(CharacterStats character, string characterName)
    {
        selectedCharacter = character;
        selectedCharacterName = characterName;

        // Update character info display
        if (characterNameText != null)
            characterNameText.text = character.characterName;

        if (characterDescriptionText != null)
            characterDescriptionText.text = character.description;

        if (characterIcon != null)
            characterIcon.sprite = character.characterIcon;

        if (healthStatText != null)
            healthStatText.text = $"Health: {character.maxHealth}";

        if (speedStatText != null)
            speedStatText.text = $"Speed: {character.movementSpeed:F1}";

        if (damageStatText != null)
            damageStatText.text = $"Damage: {character.damageMultiplier:F1}x";

        // Visual feedback for selection
        UpdateCharacterButtonVisuals(characterName);

        AudioManager.Instance?.PlaySound("CharacterSelect");
    }

    void UpdateCharacterButtonVisuals(string selectedName)
    {
        // Reset all button colors
        ResetButtonColor(grudgeButton);
        ResetButtonColor(novaButton);
        ResetButtonColor(echoButton);

        // Highlight selected button
        Button selectedButton = null;
        switch (selectedName)
        {
            case "Grudge":
                selectedButton = grudgeButton;
                break;
            case "Nova":
                selectedButton = novaButton;
                break;
            case "Echo":
                selectedButton = echoButton;
                break;
        }

        if (selectedButton != null)
        {
            ColorBlock colors = selectedButton.colors;
            colors.normalColor = Color.yellow;
            selectedButton.colors = colors;
        }
    }

    void ResetButtonColor(Button button)
    {
        if (button != null)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            button.colors = colors;
        }
    }

    void OnPlayClicked()
    {
        AudioManager.Instance?.PlaySound("ButtonClick");

        // Start matchmaking
        StartMatchmaking();
    }

    void OnConfirmCharacter()
    {
        if (selectedCharacter != null)
        {
            // Save character selection
            PlayerPrefs.SetString("SelectedCharacter", selectedCharacterName);
            PlayerPrefs.Save();

            AudioManager.Instance?.PlaySound("CharacterConfirm");
            ShowMainMenu();
        }
    }

    void OnQuitClicked()
    {
        AudioManager.Instance?.PlaySound("ButtonClick");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void StartMatchmaking()
    {
        // Connect to Photon if not already connected
        if (!Photon.Pun.PhotonNetwork.IsConnected)
        {
            Photon.Pun.PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            // Join or create a room
            JoinRandomRoom();
        }
    }

    void JoinRandomRoom()
    {
        // Try to join a random room
        Photon.Pun.PhotonNetwork.JoinRandomRoom();
    }

    public void OnJoinRandomFailed()
    {
        // Create a new room if no rooms available
        string roomName = "Room_" + Random.Range(1000, 9999);
        Photon.Pun.PhotonNetwork.CreateRoom(roomName, new Photon.Realtime.RoomOptions { MaxPlayers = 2 });
    }

    public void OnJoinedRoom()
    {
        // Load game scene
        if (Photon.Pun.PhotonNetwork.IsMasterClient)
        {
            Photon.Pun.PhotonNetwork.LoadLevel("GameScene");
        }
    }
}