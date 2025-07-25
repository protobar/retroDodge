using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== INPUT CUSTOMIZATION UI ====================
public class InputCustomizationUI : MonoBehaviour
{
    [Header("UI References")]
    public UnityEngine.UI.Toggle inputBufferToggle;
    public UnityEngine.UI.Slider inputBufferSlider;
    public UnityEngine.UI.Toggle vibrationToggle;
    public UnityEngine.UI.Slider sensitivitySlider;

    private InputSettings currentSettings;

    void Start()
    {
        LoadSettings();
        InitializeUI();
    }

    void LoadSettings()
    {
        // Load from PlayerPrefs or use defaults
        currentSettings = new InputSettings();
        currentSettings.inputBufferEnabled = PlayerPrefs.GetInt("InputBufferEnabled", 1) == 1;
        currentSettings.inputBufferTime = PlayerPrefs.GetFloat("InputBufferTime", 0.1f);
        currentSettings.enableVibration = PlayerPrefs.GetInt("VibrationEnabled", 1) == 1;
        currentSettings.mobileInputSensitivity = PlayerPrefs.GetFloat("MobileSensitivity", 1.5f);
    }

    void InitializeUI()
    {
        if (inputBufferToggle != null)
        {
            inputBufferToggle.isOn = currentSettings.inputBufferEnabled;
            inputBufferToggle.onValueChanged.AddListener(OnInputBufferToggle);
        }

        if (inputBufferSlider != null)
        {
            inputBufferSlider.value = currentSettings.inputBufferTime;
            inputBufferSlider.onValueChanged.AddListener(OnInputBufferTimeChanged);
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.isOn = currentSettings.enableVibration;
            vibrationToggle.onValueChanged.AddListener(OnVibrationToggle);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = currentSettings.mobileInputSensitivity;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
    }

    void OnInputBufferToggle(bool enabled)
    {
        currentSettings.inputBufferEnabled = enabled;
        if (InputManager.Instance != null)
        {
            InputManager.Instance.SetInputBufferEnabled(enabled);
        }
        SaveSettings();
    }

    void OnInputBufferTimeChanged(float time)
    {
        currentSettings.inputBufferTime = time;
        if (InputManager.Instance != null)
        {
            InputManager.Instance.SetInputBufferTime(time);
        }
        SaveSettings();
    }

    void OnVibrationToggle(bool enabled)
    {
        currentSettings.enableVibration = enabled;
        SaveSettings();
    }

    void OnSensitivityChanged(float sensitivity)
    {
        currentSettings.mobileInputSensitivity = sensitivity;
        SaveSettings();
    }

    void SaveSettings()
    {
        PlayerPrefs.SetInt("InputBufferEnabled", currentSettings.inputBufferEnabled ? 1 : 0);
        PlayerPrefs.SetFloat("InputBufferTime", currentSettings.inputBufferTime);
        PlayerPrefs.SetInt("VibrationEnabled", currentSettings.enableVibration ? 1 : 0);
        PlayerPrefs.SetFloat("MobileSensitivity", currentSettings.mobileInputSensitivity);
        PlayerPrefs.Save();
    }

    public void ResetToDefaults()
    {
        currentSettings = new InputSettings();
        InitializeUI();
        SaveSettings();
    }
}