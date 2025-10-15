using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages audio settings UI and integrates with AudioManager
/// Connects main menu volume sliders to the centralized audio system
/// </summary>
public class AudioSettingsManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeText;
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [SerializeField] private TextMeshProUGUI sfxVolumeText;

    [Header("Settings")]
    [SerializeField] private bool saveSettings = true;
    [SerializeField] private string masterVolumeKey = "MasterVolume";
    [SerializeField] private string musicVolumeKey = "MusicVolume";
    [SerializeField] private string sfxVolumeKey = "SFXVolume";

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // AudioManager reference
    private AudioManager audioManager;

    void Start()
    {
        InitializeAudioManager();
        SetupVolumeSliders();
        LoadSavedSettings();
    }

    void InitializeAudioManager()
    {
        // Get or create AudioManager
        audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogError("[AudioSettingsManager] AudioManager not found! Creating one...");
            GameObject audioManagerGO = new GameObject("AudioManager");
            audioManager = audioManagerGO.AddComponent<AudioManager>();
        }
    }

    void SetupVolumeSliders()
    {
        // Setup master volume slider
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
        }

        // Setup music volume slider
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
        }

        // Setup SFX volume slider
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
        }

        if (debugMode)
        {
            Debug.Log("[AudioSettingsManager] Volume sliders configured");
        }
    }

    void LoadSavedSettings()
    {
        if (!saveSettings) return;

        // Load master volume
        float masterVolume = PlayerPrefs.GetFloat(masterVolumeKey, 1f);
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = masterVolume;
            audioManager.SetVolume(AudioType.Master, masterVolume);
        }

        // Load music volume
        float musicVolume = PlayerPrefs.GetFloat(musicVolumeKey, 0.6f);
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = musicVolume;
            audioManager.SetVolume(AudioType.Music, musicVolume);
        }

        // Load SFX volume
        float sfxVolume = PlayerPrefs.GetFloat(sfxVolumeKey, 0.8f);
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = sfxVolume;
            audioManager.SetVolume(AudioType.SFX, sfxVolume);
        }

        if (debugMode)
        {
            Debug.Log($"[AudioSettingsManager] Loaded settings - Master: {masterVolume:F2}, Music: {musicVolume:F2}, SFX: {sfxVolume:F2}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // VOLUME SLIDER CALLBACKS
    // ═══════════════════════════════════════════════════════════════

    void OnMasterVolumeChanged(float value)
    {
        if (audioManager != null)
        {
            audioManager.SetVolume(AudioType.Master, value);
        }

        if (masterVolumeText != null)
        {
            masterVolumeText.text = Mathf.RoundToInt(value * 100) + "%";
        }

        if (saveSettings)
        {
            PlayerPrefs.SetFloat(masterVolumeKey, value);
            PlayerPrefs.Save();
        }

        if (debugMode)
        {
            Debug.Log($"[AudioSettingsManager] Master volume changed to: {value:F2}");
        }
    }

    void OnMusicVolumeChanged(float value)
    {
        if (audioManager != null)
        {
            audioManager.SetVolume(AudioType.Music, value);
        }

        if (musicVolumeText != null)
        {
            musicVolumeText.text = Mathf.RoundToInt(value * 100) + "%";
        }

        if (saveSettings)
        {
            PlayerPrefs.SetFloat(musicVolumeKey, value);
            PlayerPrefs.Save();
        }

        if (debugMode)
        {
            Debug.Log($"[AudioSettingsManager] Music volume changed to: {value:F2}");
        }
    }

    void OnSFXVolumeChanged(float value)
    {
        if (audioManager != null)
        {
            audioManager.SetVolume(AudioType.SFX, value);
            audioManager.SetVolume(AudioType.Announcement, value); // Announcements use SFX volume
        }

        if (sfxVolumeText != null)
        {
            sfxVolumeText.text = Mathf.RoundToInt(value * 100) + "%";
        }

        if (saveSettings)
        {
            PlayerPrefs.SetFloat(sfxVolumeKey, value);
            PlayerPrefs.Save();
        }

        if (debugMode)
        {
            Debug.Log($"[AudioSettingsManager] SFX volume changed to: {value:F2}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Reset all volume settings to default
    /// </summary>
    public void ResetToDefaults()
    {
        float defaultMaster = 1f;
        float defaultMusic = 0.6f;
        float defaultSFX = 0.8f;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = defaultMaster;
            OnMasterVolumeChanged(defaultMaster);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = defaultMusic;
            OnMusicVolumeChanged(defaultMusic);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = defaultSFX;
            OnSFXVolumeChanged(defaultSFX);
        }

        if (debugMode)
        {
            Debug.Log("[AudioSettingsManager] Reset to default volumes");
        }
    }

    /// <summary>
    /// Get current volume settings
    /// </summary>
    public float GetMasterVolume()
    {
        return audioManager != null ? audioManager.GetVolume(AudioType.Master) : 1f;
    }

    public float GetMusicVolume()
    {
        return audioManager != null ? audioManager.GetVolume(AudioType.Music) : 0.6f;
    }

    public float GetSFXVolume()
    {
        return audioManager != null ? audioManager.GetVolume(AudioType.SFX) : 0.8f;
    }

    /// <summary>
    /// Set volume programmatically
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = volume;
        }
        OnMasterVolumeChanged(volume);
    }

    public void SetMusicVolume(float volume)
    {
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = volume;
        }
        OnMusicVolumeChanged(volume);
    }

    public void SetSFXVolume(float volume)
    {
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = volume;
        }
        OnSFXVolumeChanged(volume);
    }

    /// <summary>
    /// Test audio with current settings
    /// </summary>
    public void TestAudio()
    {
        if (audioManager != null)
        {
            // Play a test sound to verify audio is working
            AudioClip testClip = Resources.Load<AudioClip>("Audio/Test/TestSound");
            if (testClip != null)
            {
                audioManager.PlaySound(testClip, AudioType.SFX, false);
            }
            else if (debugMode)
            {
                Debug.Log("[AudioSettingsManager] Test audio clip not found at Audio/Test/TestSound");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // UNITY EVENTS
    // ═══════════════════════════════════════════════════════════════

    void OnDestroy()
    {
        // Save settings when component is destroyed
        if (saveSettings)
        {
            PlayerPrefs.Save();
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        // Save settings when application is paused
        if (saveSettings && pauseStatus)
        {
            PlayerPrefs.Save();
        }
    }
}

