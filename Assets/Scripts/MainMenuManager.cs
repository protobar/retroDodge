using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Main Menu Manager - Network Ready but Locally Testable
/// Handles main menu navigation and scene transitions
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button characterSelectButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button backFromSettingsButton;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Scene Management")]
    [SerializeField] private string characterSelectionScene = "CharacterSelection";
    [SerializeField] private string gameplayScene = "GameplayArena";

    [Header("Network Settings (Future)")]
    [SerializeField] private bool enableNetworking = false;
    [SerializeField] private bool isHost = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip backgroundMusic;

    [Header("Visual Effects")]
    [SerializeField] private GameObject menuTransitionEffect;
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Game state
    private bool isTransitioning = false;

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
    }

    void Start()
    {
        InitializeMainMenu();
        SetupButtonListeners();
        LoadUserSettings();
        PlayBackgroundMusic();

        if (debugMode)
        {
            Debug.Log("MainMenuManager initialized - Network Ready: " + enableNetworking);
        }
    }

    void InitializeMainMenu()
    {
        // Show main menu, hide other panels
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Set initial button states
        if (playButton != null)
            playButton.interactable = true;

        isTransitioning = false;
    }

    void SetupButtonListeners()
    {
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayButtonClicked);

        if (characterSelectButton != null)
            characterSelectButton.onClick.AddListener(OnCharacterSelectClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

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
            Debug.Log("MainMenu button listeners setup complete");
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

    #region Button Event Handlers

    public void OnPlayButtonClicked()
    {
        if (isTransitioning) return;

        PlayButtonSound();

        if (debugMode)
        {
            Debug.Log("Play button clicked - Starting character selection");
        }

        // For now, go directly to character selection
        // In multiplayer, this would handle host/join logic
        StartCharacterSelection();
    }

    public void OnCharacterSelectClicked()
    {
        if (isTransitioning) return;

        PlayButtonSound();
        StartCharacterSelection();
    }

    public void OnSettingsClicked()
    {
        if (isTransitioning) return;

        PlayButtonSound();
        ShowSettingsPanel();
    }

    public void OnQuitClicked()
    {
        PlayButtonSound();

        if (debugMode)
        {
            Debug.Log("Quit button clicked");
        }

        StartCoroutine(QuitGameWithDelay());
    }

    public void OnBackFromSettingsClicked()
    {
        PlayButtonSound();
        HideSettingsPanel();
    }

    #endregion

    #region Settings Event Handlers

    public void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (audioSource != null)
            audioSource.volume = value;

        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
    }

    public void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();

        if (debugMode)
        {
            Debug.Log($"Fullscreen toggled: {isFullscreen}");
        }
    }

    #endregion

    #region Scene Transitions

    void StartCharacterSelection()
    {
        if (enableNetworking)
        {
            // Future: Handle network character selection
            StartCoroutine(TransitionToNetworkCharacterSelection());
        }
        else
        {
            // Local character selection
            StartCoroutine(TransitionToLocalCharacterSelection());
        }
    }

    IEnumerator TransitionToLocalCharacterSelection()
    {
        isTransitioning = true;

        // Disable buttons
        SetButtonsInteractable(false);

        // Play transition effect
        if (menuTransitionEffect != null)
        {
            GameObject effect = Instantiate(menuTransitionEffect, transform.position, Quaternion.identity);
            Destroy(effect, transitionDuration);
        }

        // Wait for transition
        yield return new WaitForSeconds(transitionDuration);

        // Load character selection scene
        SceneManager.LoadScene(characterSelectionScene);
    }

    IEnumerator TransitionToNetworkCharacterSelection()
    {
        isTransitioning = true;

        if (debugMode)
        {
            Debug.Log("Starting network character selection (Future Implementation)");
        }

        // Future: Network setup logic here
        // For now, fall back to local
        yield return StartCoroutine(TransitionToLocalCharacterSelection());
    }

    IEnumerator QuitGameWithDelay()
    {
        SetButtonsInteractable(false);

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

    #endregion

    #region UI Utilities

    void SetButtonsInteractable(bool interactable)
    {
        if (playButton != null)
            playButton.interactable = interactable;

        if (characterSelectButton != null)
            characterSelectButton.interactable = interactable;

        if (settingsButton != null)
            settingsButton.interactable = interactable;

        if (quitButton != null)
            quitButton.interactable = interactable;
    }

    void ShowSettingsPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (debugMode)
        {
            Debug.Log("Settings panel shown");
        }
    }

    void HideSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (debugMode)
        {
            Debug.Log("Settings panel hidden");
        }
    }

    void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }

    #endregion

    #region Network Ready Methods (Future)

    /// <summary>
    /// Future: Enable networking mode
    /// </summary>
    public void EnableNetworking(bool enable)
    {
        enableNetworking = enable;

        if (debugMode)
        {
            Debug.Log($"Networking {(enable ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// Future: Set as host or client
    /// </summary>
    public void SetNetworkRole(bool host)
    {
        isHost = host;

        if (debugMode)
        {
            Debug.Log($"Network role set to: {(host ? "Host" : "Client")}");
        }
    }

    /// <summary>
    /// Future: Called when network connection established
    /// </summary>
    public void OnNetworkConnected()
    {
        if (debugMode)
        {
            Debug.Log("Network connection established");
        }
    }

    /// <summary>
    /// Future: Called when network connection lost
    /// </summary>
    public void OnNetworkDisconnected()
    {
        if (debugMode)
        {
            Debug.Log("Network connection lost");
        }

        // Return to main menu
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    #endregion

    #region Debug Methods

    void Update()
    {
        // Debug controls
        if (debugMode)
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                OnPlayButtonClicked();
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                OnSettingsClicked();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    OnBackFromSettingsClicked();
                }
                else
                {
                    OnQuitClicked();
                }
            }
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== MAIN MENU DEBUG ===");
        GUILayout.Label($"Networking: {enableNetworking}");
        GUILayout.Label($"Role: {(isHost ? "Host" : "Client")}");
        GUILayout.Label($"Transitioning: {isTransitioning}");

        GUILayout.Space(10);
        GUILayout.Label("Debug Controls:");
        GUILayout.Label("F1 - Start Game");
        GUILayout.Label("F2 - Settings");
        GUILayout.Label("ESC - Back/Quit");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion
}