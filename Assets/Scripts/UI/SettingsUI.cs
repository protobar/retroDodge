using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ==================== SETTINGS UI ====================
public class SettingsUI : MonoBehaviour
{
    [Header("Audio Settings")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Graphics Settings")]
    public Dropdown qualityDropdown;
    public Toggle fullscreenToggle;
    public Dropdown resolutionDropdown;

    [Header("Gameplay Settings")]
    public Toggle inputBufferToggle;
    public Slider inputBufferSlider;
    public Toggle vibrationToggle;

    void Start()
    {
        LoadSettings();
        InitializeUI();
    }

    void LoadSettings()
    {
        // Load audio settings
        float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

        if (masterVolumeSlider != null) masterVolumeSlider.value = masterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = musicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVolume;

        // Load graphics settings
        if (qualityDropdown != null)
            qualityDropdown.value = QualitySettings.GetQualityLevel();

        if (fullscreenToggle != null)
            fullscreenToggle.isOn = Screen.fullScreen;

        // Load gameplay settings
        bool inputBufferEnabled = PlayerPrefs.GetInt("InputBufferEnabled", 1) == 1;
        float inputBufferTime = PlayerPrefs.GetFloat("InputBufferTime", 0.1f);
        bool vibrationEnabled = PlayerPrefs.GetInt("VibrationEnabled", 1) == 1;

        if (inputBufferToggle != null) inputBufferToggle.isOn = inputBufferEnabled;
        if (inputBufferSlider != null) inputBufferSlider.value = inputBufferTime;
        if (vibrationToggle != null) vibrationToggle.isOn = vibrationEnabled;
    }

    void InitializeUI()
    {
        // Setup audio slider events
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // Setup graphics events
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        // Setup gameplay events
        if (inputBufferToggle != null)
            inputBufferToggle.onValueChanged.AddListener(OnInputBufferToggled);

        if (inputBufferSlider != null)
            inputBufferSlider.onValueChanged.AddListener(OnInputBufferTimeChanged);

        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.AddListener(OnVibrationToggled);
    }

    void OnMasterVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MasterVolume", value);
        if (AudioManager.Instance != null)
            AudioManager.Instance.masterVolume = value;
    }

    void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        if (AudioManager.Instance != null)
            AudioManager.Instance.musicVolume = value;
    }

    void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        if (AudioManager.Instance != null)
            AudioManager.Instance.sfxVolume = value;
    }

    void OnQualityChanged(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
        PlayerPrefs.SetInt("QualityLevel", qualityIndex);
    }

    void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }

    void OnInputBufferToggled(bool enabled)
    {
        PlayerPrefs.SetInt("InputBufferEnabled", enabled ? 1 : 0);
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputBufferEnabled(enabled);
    }

    void OnInputBufferTimeChanged(float time)
    {
        PlayerPrefs.SetFloat("InputBufferTime", time);
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputBufferTime(time);
    }

    void OnVibrationToggled(bool enabled)
    {
        PlayerPrefs.SetInt("VibrationEnabled", enabled ? 1 : 0);
    }

    public void ResetToDefaults()
    {
        // Reset all settings to default values
        PlayerPrefs.DeleteAll();
        LoadSettings();

        AudioManager.Instance?.PlaySound("ButtonClick");
    }

    public void SaveSettings()
    {
        PlayerPrefs.Save();
        AudioManager.Instance?.PlaySound("ButtonClick");
    }
}