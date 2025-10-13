using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Graphics Settings Manager - Handles Low/Medium/High graphics presets with URP
/// Integrates with Unity's Quality Settings and URP Render Pipeline
/// </summary>
public class GraphicsSettingsManager : MonoBehaviour
{
    [Header("Graphics Settings")]
    [SerializeField] private GraphicsPreset currentPreset = GraphicsPreset.Balanced;
    [SerializeField] private bool autoDetectHardware = true;
    
    [Header("UI References")]
    [SerializeField] private Button performantButton;
    [SerializeField] private Button balancedButton;
    [SerializeField] private Button highFidelityButton;
    [SerializeField] private TMP_Text currentPresetText;
    [SerializeField] private TMP_Text fpsCounterText;
    
    [Header("Resolution Settings")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private bool includeRefreshRates = true;
    
    [Header("VSync Settings")]
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private bool allowVSyncControl = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool enableFPSDisplay = true;
    
    // Graphics presets - matches your existing Quality Settings
    public enum GraphicsPreset
    {
        Performant = 0,    // Your existing "Performant" quality
        Balanced = 1,      // Your existing "Balanced" quality  
        HighFidelity = 2   // Your existing "High Fidelity" quality
    }
    
    // Note: URP assets are read-only at runtime, so we use Quality Settings instead
    
    // Performance monitoring
    private float fpsUpdateInterval = 0.5f;
    private float fpsAccumulator = 0f;
    private int fpsFrames = 0;
    private float fpsTimeLeft;
    private float currentFPS = 0f;
    
    // Hardware detection
    private bool isLowEndDevice = false;
    private bool isHighEndDevice = false;
    
    void Awake()
    {
        // Initialize FPS counter
        fpsTimeLeft = fpsUpdateInterval;
        
        // Load saved graphics settings
        LoadGraphicsSettings();
    }
    
    void Start()
    {
        // Setup UI
        SetupUI();
        
        // Auto-detect hardware if enabled
        if (autoDetectHardware)
        {
            DetectHardwareCapabilities();
        }
        
        // Apply current preset
        ApplyGraphicsPreset(currentPreset);
        
        // Update UI
        UpdateUI();
    }
    
    void Update()
    {
        // Update FPS counter
        if (enableFPSDisplay)
        {
            UpdateFPSCounter();
        }
    }
    
    void SetupUI()
    {
        // Setup button listeners
        if (performantButton != null)
            performantButton.onClick.AddListener(() => SetGraphicsPreset(GraphicsPreset.Performant));
        
        if (balancedButton != null)
            balancedButton.onClick.AddListener(() => SetGraphicsPreset(GraphicsPreset.Balanced));
        
        if (highFidelityButton != null)
            highFidelityButton.onClick.AddListener(() => SetGraphicsPreset(GraphicsPreset.HighFidelity));
        
        // Setup resolution dropdown
        SetupResolutionDropdown();
        
        // Setup VSync toggle
        SetupVSyncToggle();
    }
    
    void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;
        
        // Clear existing options
        resolutionDropdown.ClearOptions();
        
        // Get available resolutions
        Resolution[] resolutions = Screen.resolutions;
        List<string> resolutionOptions = new List<string>();
        
        // Add current resolution first
        string currentRes = $"{Screen.currentResolution.width}x{Screen.currentResolution.height}";
        if (includeRefreshRates)
        {
            currentRes += $" @ {Screen.currentResolution.refreshRate}Hz";
        }
        resolutionOptions.Add(currentRes);
        
        // Add other resolutions (avoid duplicates)
        HashSet<string> addedResolutions = new HashSet<string>();
        addedResolutions.Add(currentRes);
        
        foreach (Resolution res in resolutions)
        {
            string resString = $"{res.width}x{res.height}";
            if (includeRefreshRates)
            {
                resString += $" @ {res.refreshRate}Hz";
            }
            
            if (!addedResolutions.Contains(resString))
            {
                resolutionOptions.Add(resString);
                addedResolutions.Add(resString);
            }
        }
        
        // Add options to dropdown
        resolutionDropdown.AddOptions(resolutionOptions);
        
        // Set current resolution as selected
        resolutionDropdown.value = 0;
        
        // Add listener
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }
    
    void SetupVSyncToggle()
    {
        if (vSyncToggle == null || !allowVSyncControl) return;
        
        // Set current VSync state
        vSyncToggle.isOn = QualitySettings.vSyncCount > 0;
        
        // Add listener
        vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
    }
    
    void OnResolutionChanged(int index)
    {
        if (resolutionDropdown == null) return;
        
        string selectedResolution = resolutionDropdown.options[index].text;
        
        // Parse resolution string (format: "1920x1080 @ 60Hz" or "1920x1080")
        string[] parts = selectedResolution.Split('x');
        if (parts.Length >= 2)
        {
            int width = int.Parse(parts[0]);
            string[] heightParts = parts[1].Split(' ');
            int height = int.Parse(heightParts[0]);
            
            // Find matching resolution with refresh rate
            Resolution targetResolution = new Resolution();
            bool found = false;
            
            foreach (Resolution res in Screen.resolutions)
            {
                if (res.width == width && res.height == height)
                {
                    targetResolution = res;
                    found = true;
                    break;
                }
            }
            
            if (found)
            {
                Screen.SetResolution(targetResolution.width, targetResolution.height, Screen.fullScreen, targetResolution.refreshRate);
                if (showDebugInfo)
                {
                    Debug.Log($"[Graphics] Resolution changed to {targetResolution.width}x{targetResolution.height} @ {targetResolution.refreshRate}Hz");
                }
            }
        }
    }
    
    void OnVSyncChanged(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        
        if (showDebugInfo)
        {
            Debug.Log($"[Graphics] VSync {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    void DetectHardwareCapabilities()
    {
        // Get system info
        int processorCount = SystemInfo.processorCount;
        int systemMemory = SystemInfo.systemMemorySize;
        int graphicsMemory = SystemInfo.graphicsMemorySize;
        string graphicsDevice = SystemInfo.graphicsDeviceName.ToLower();
        
        // Detect low-end devices
        isLowEndDevice = (
            processorCount < 4 ||
            systemMemory < 4000 ||
            graphicsMemory < 1000 ||
            graphicsDevice.Contains("intel") ||
            graphicsDevice.Contains("integrated")
        );
        
        // Detect high-end devices
        isHighEndDevice = (
            processorCount >= 8 &&
            systemMemory >= 8000 &&
            graphicsMemory >= 4000 &&
            (graphicsDevice.Contains("rtx") || graphicsDevice.Contains("gtx 10") || graphicsDevice.Contains("gtx 16"))
        );
        
        // Auto-select appropriate preset
        if (isLowEndDevice && currentPreset == GraphicsPreset.Balanced)
        {
            currentPreset = GraphicsPreset.Performant;
            if (showDebugInfo)
                Debug.Log("[Graphics] Auto-detected low-end device, switching to Performant preset");
        }
        else if (isHighEndDevice && currentPreset == GraphicsPreset.Balanced)
        {
            currentPreset = GraphicsPreset.HighFidelity;
            if (showDebugInfo)
                Debug.Log("[Graphics] Auto-detected high-end device, switching to High Fidelity preset");
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[Graphics] Hardware Detection:");
            Debug.Log($"  - Processors: {processorCount}");
            Debug.Log($"  - System Memory: {systemMemory}MB");
            Debug.Log($"  - Graphics Memory: {graphicsMemory}MB");
            Debug.Log($"  - Graphics Device: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"  - Low-end: {isLowEndDevice}, High-end: {isHighEndDevice}");
        }
    }
    
    public void SetGraphicsPreset(GraphicsPreset preset)
    {
        currentPreset = preset;
        ApplyGraphicsPreset(preset);
        SaveGraphicsSettings();
        UpdateUI();
        
        if (showDebugInfo)
        {
            Debug.Log($"[Graphics] Switched to {preset} preset");
        }
    }
    
    void ApplyGraphicsPreset(GraphicsPreset preset)
    {
        // Set Unity Quality Settings
        int qualityLevel = (int)preset;
        QualitySettings.SetQualityLevel(qualityLevel, true);
        
        // Apply URP-specific settings
        ApplyURPSettings(preset);
        
        // Apply additional optimizations
        ApplyAdditionalOptimizations(preset);
        
        if (showDebugInfo)
        {
            Debug.Log($"[Graphics] Applied {preset} preset (Quality Level {qualityLevel})");
        }
    }
    
    void ApplyURPSettings(GraphicsPreset preset)
    {
        // URP assets are read-only at runtime, so we use Quality Settings instead
        // The URP settings should be pre-configured in your Quality Settings
        
        // Set the quality level which will use the pre-configured URP settings
        int qualityLevel = (int)preset;
        QualitySettings.SetQualityLevel(qualityLevel, true);
        
        if (showDebugInfo)
        {
            Debug.Log($"[Graphics] Applied URP settings for {preset} preset (Quality Level {qualityLevel})");
        }
    }
    
    void ApplyLowSettings()
    {
        // Low quality settings - maximum performance
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
        QualitySettings.shadowDistance = 50f;
        QualitySettings.shadowCascades = 2; // Two cascades
        QualitySettings.lodBias = 0.5f; // Lower LOD bias
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.antiAliasing = 0; // No anti-aliasing
        QualitySettings.softVegetation = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.billboardsFaceCameraPosition = false;
        QualitySettings.vSyncCount = 0; // No VSync for maximum FPS
        QualitySettings.maxQueuedFrames = 1;
    }
    
    void ApplyMediumSettings()
    {
        // Medium quality settings - balanced performance
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
        QualitySettings.shadowDistance = 100f;
        QualitySettings.shadowCascades = 4; // Four cascades
        QualitySettings.lodBias = 1.0f; // Standard LOD bias
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.antiAliasing = 2; // 2x anti-aliasing
        QualitySettings.softVegetation = true;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.billboardsFaceCameraPosition = true;
        QualitySettings.vSyncCount = 0; // No VSync
        QualitySettings.maxQueuedFrames = 2;
    }
    
    void ApplyHighSettings()
    {
        // High quality settings - maximum quality
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
        QualitySettings.shadowDistance = 200f;
        QualitySettings.shadowCascades = 4; // Four cascades
        QualitySettings.lodBias = 1.5f; // Higher LOD bias
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.antiAliasing = 4; // 4x anti-aliasing
        QualitySettings.softVegetation = true;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.billboardsFaceCameraPosition = true;
        QualitySettings.vSyncCount = 1; // VSync for smooth gameplay
        QualitySettings.maxQueuedFrames = 3;
    }
    
    void ApplyAdditionalOptimizations(GraphicsPreset preset)
    {
        // Apply quality-specific settings
        switch (preset)
        {
            case GraphicsPreset.Performant:
                ApplyLowSettings();
                break;
            case GraphicsPreset.Balanced:
                ApplyMediumSettings();
                break;
            case GraphicsPreset.HighFidelity:
                ApplyHighSettings();
                break;
        }
        
        // Application settings
        Application.targetFrameRate = preset == GraphicsPreset.Performant ? 60 : (preset == GraphicsPreset.Balanced ? 60 : 60);
        
        // Physics settings
        Physics.defaultSolverIterations = preset == GraphicsPreset.Performant ? 4 : (preset == GraphicsPreset.Balanced ? 6 : 8);
        Physics.defaultSolverVelocityIterations = preset == GraphicsPreset.Performant ? 1 : (preset == GraphicsPreset.Balanced ? 2 : 4);
        
        // Audio settings (affects performance)
        AudioSettings.SetDSPBufferSize(preset == GraphicsPreset.Performant ? 64 : (preset == GraphicsPreset.Balanced ? 128 : 256), 2);
    }
    
    void UpdateUI()
    {
        // Update button states
        UpdateButtonStates();
        
        // Update current preset text
        if (currentPresetText != null)
        {
            currentPresetText.text = $"Current: {currentPreset}";
        }
    }
    
    void UpdateButtonStates()
    {
        // Update button colors/interactions based on current preset
        if (performantButton != null)
        {
            var colors = performantButton.colors;
            colors.normalColor = currentPreset == GraphicsPreset.Performant ? Color.green : Color.white;
            performantButton.colors = colors;
        }
        
        if (balancedButton != null)
        {
            var colors = balancedButton.colors;
            colors.normalColor = currentPreset == GraphicsPreset.Balanced ? Color.green : Color.white;
            balancedButton.colors = colors;
        }
        
        if (highFidelityButton != null)
        {
            var colors = highFidelityButton.colors;
            colors.normalColor = currentPreset == GraphicsPreset.HighFidelity ? Color.green : Color.white;
            highFidelityButton.colors = colors;
        }
    }
    
    void UpdateFPSCounter()
    {
        fpsTimeLeft -= Time.deltaTime;
        fpsAccumulator += Time.timeScale / Time.deltaTime;
        fpsFrames++;
        
        if (fpsTimeLeft <= 0.0f)
        {
            currentFPS = fpsAccumulator / fpsFrames;
            fpsTimeLeft = fpsUpdateInterval;
            fpsAccumulator = 0.0f;
            fpsFrames = 0;
            
            if (fpsCounterText != null)
            {
                fpsCounterText.text = $"FPS: {currentFPS:F1}";
            }
        }
    }
    
    void SaveGraphicsSettings()
    {
        PlayerPrefs.SetInt("GraphicsPreset", (int)currentPreset);
        PlayerPrefs.Save();
    }
    
    void LoadGraphicsSettings()
    {
        if (PlayerPrefs.HasKey("GraphicsPreset"))
        {
            currentPreset = (GraphicsPreset)PlayerPrefs.GetInt("GraphicsPreset");
        }
    }
    
    // Public methods for external access
    public GraphicsPreset GetCurrentPreset()
    {
        return currentPreset;
    }
    
    public float GetCurrentFPS()
    {
        return currentFPS;
    }
    
    public bool IsLowEndDevice()
    {
        return isLowEndDevice;
    }
    
    public bool IsHighEndDevice()
    {
        return isHighEndDevice;
    }
    
    public void SetShowDebugInfo(bool show)
    {
        showDebugInfo = show;
    }
    
    public void SetEnableFPSDisplay(bool enable)
    {
        enableFPSDisplay = enable;
        if (fpsCounterText != null)
        {
            fpsCounterText.gameObject.SetActive(enable);
        }
    }
    
    // Debug methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"=== Graphics Settings Debug ===");
        GUILayout.Label($"Current Preset: {currentPreset}");
        GUILayout.Label($"Quality Level: {QualitySettings.GetQualityLevel()}");
        GUILayout.Label($"FPS: {currentFPS:F1}");
        GUILayout.Label($"Shadow Distance: {QualitySettings.shadowDistance}");
        GUILayout.Label($"Anti-aliasing: {QualitySettings.antiAliasing}");
        GUILayout.Label($"VSync: {QualitySettings.vSyncCount}");
        GUILayout.Label($"Shadow Resolution: {QualitySettings.shadowResolution}");
        GUILayout.EndArea();
    }
}
