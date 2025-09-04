using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// UI Controller for Room Settings
/// Handles all UI interactions for custom room creation and configuration
/// </summary>
public class RoomSettingsUI : MonoBehaviour
{
    [Header("Match Length")]
    [SerializeField] private TMP_Dropdown matchLengthDropdown;
    [SerializeField] private TMP_Text matchLengthDescriptionText;
    
    [Header("Map Selection")]
    [SerializeField] private TMP_Dropdown mapSelectionDropdown;
    [SerializeField] private Image mapPreviewImage;
    [SerializeField] private TMP_Text mapDescriptionText;
    [SerializeField] private TMP_Text mapSizeText;
    
    [Header("Settings Preview")]
    [SerializeField] private TMP_Text settingsPreviewText;
    [SerializeField] private GameObject settingsPreviewPanel;
    
    [Header("Action Buttons")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resetButton;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // ═══════════════════════════════════════════════════════════════
    // PRIVATE VARIABLES
    // ═══════════════════════════════════════════════════════════════
    
    private RoomSettings currentSettings;
    private MapData[] availableMaps;
    private bool isInitialized = false;
    
    // ═══════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════
    
    public System.Action<RoomSettings> OnRoomSettingsChanged;
    public System.Action<RoomSettings> OnCreateRoomRequested;
    public System.Action OnCancelRequested;
    
    // ═══════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════
    
    private void Awake()
    {
        currentSettings = new RoomSettings();
        InitializeUI();
    }
    
    private void Start()
    {
        LoadAvailableMaps();
        SetupDropdowns();
        UpdateUI();
        isInitialized = true;
    }
    
    private void InitializeUI()
    {
        // Setup button listeners
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
        
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);
        
        // Setup dropdown listeners
        if (matchLengthDropdown != null)
            matchLengthDropdown.onValueChanged.AddListener(OnMatchLengthChanged);
        
        if (mapSelectionDropdown != null)
            mapSelectionDropdown.onValueChanged.AddListener(OnMapSelectionChanged);
        
        // Initialize UI state
        if (settingsPreviewPanel != null)
            settingsPreviewPanel.SetActive(true);
    }
    
    private void LoadAvailableMaps()
    {
        availableMaps = MapRegistry.Instance.GetUnlockedMaps();
        if (debugMode) Debug.Log($"[ROOM SETTINGS UI] Loaded {availableMaps.Length} available maps");
    }
    
    private void SetupDropdowns()
    {
        // Setup match length dropdown
        if (matchLengthDropdown != null)
        {
            matchLengthDropdown.ClearOptions();
            var options = new List<string>(RoomSettings.GetMatchLengthDescriptions());
            matchLengthDropdown.AddOptions(options);
            matchLengthDropdown.value = 1; // Default to 1 minute
        }
        
        // Setup map selection dropdown
        if (mapSelectionDropdown != null)
        {
            mapSelectionDropdown.ClearOptions();
            var mapNames = new List<string>();
            foreach (var map in availableMaps)
            {
                mapNames.Add(map.mapName);
            }
            mapSelectionDropdown.AddOptions(mapNames);
            mapSelectionDropdown.value = 0; // Default to first map
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // UI EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════
    
    
    private void OnMatchLengthChanged(int index)
    {
        currentSettings = currentSettings.Clone();
        currentSettings.matchLengthSeconds = RoomSettings.GetMatchLengthOptions()[index];
        
        if (matchLengthDescriptionText != null)
            matchLengthDescriptionText.text = currentSettings.GetMatchLengthDescription();
        
        UpdateSettingsPreview();
    }
    
    private void OnMapSelectionChanged(int index)
    {
        if (index < 0 || index >= availableMaps.Length) return;
        
        currentSettings = currentSettings.Clone();
        currentSettings.selectedMap = availableMaps[index].mapId;
        
        UpdateMapPreview();
        UpdateSettingsPreview();
    }
    
    private void OnCreateRoomClicked()
    {
        if (ValidateAllSettings())
        {
            OnCreateRoomRequested?.Invoke(currentSettings);
        }
    }
    
    private void OnCancelClicked()
    {
        OnCancelRequested?.Invoke();
    }
    
    private void OnResetClicked()
    {
        ResetToDefaults();
    }
    
    // ═══════════════════════════════════════════════════════════════
    // VALIDATION METHODS
    // ═══════════════════════════════════════════════════════════════
    
    private bool ValidateAllSettings()
    {
        // Validate settings
        string settingsError = currentSettings.ValidateSettings();
        if (!string.IsNullOrEmpty(settingsError))
        {
            ShowError(settingsError);
            return false;
        }
        
        return true;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // UI UPDATE METHODS
    // ═══════════════════════════════════════════════════════════════
    
    private void UpdateUI()
    {
        UpdateMapPreview();
        UpdateSettingsPreview();
    }
    
    private void UpdateMapPreview()
    {
        if (mapSelectionDropdown == null) return;
        
        int selectedIndex = mapSelectionDropdown.value;
        if (selectedIndex < 0 || selectedIndex >= availableMaps.Length) return;
        
        var selectedMap = availableMaps[selectedIndex];
        
        // Update map preview image
        if (mapPreviewImage != null && selectedMap.mapPreview != null)
            mapPreviewImage.sprite = selectedMap.mapPreview;
        
        // Update map description
        if (mapDescriptionText != null)
            mapDescriptionText.text = selectedMap.GetFormattedDescription();
        
        // Update map size
        if (mapSizeText != null)
            mapSizeText.text = selectedMap.GetSizeDescription();
    }
    
    private void UpdateSettingsPreview()
    {
        if (settingsPreviewText == null) return;
        
        string preview = $"<b>Match Length:</b> {currentSettings.GetMatchLengthDescription()}\n";
        preview += $"<b>Map:</b> {currentSettings.selectedMap}\n";
        preview += $"<b>Max Players:</b> {currentSettings.maxPlayers}";
        
        settingsPreviewText.text = preview;
    }
    
    private void ShowError(string message)
    {
        if (debugMode) Debug.LogError($"[ROOM SETTINGS UI] Error: {message}");
        // You can add a popup or notification system here
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Reset all settings to defaults
    /// </summary>
    public void ResetToDefaults()
    {
        currentSettings = new RoomSettings();
        
        if (matchLengthDropdown != null)
            matchLengthDropdown.value = 1; // 1 minute
        
        if (mapSelectionDropdown != null)
            mapSelectionDropdown.value = 0; // First map
        
        UpdateUI();
    }
    
    /// <summary>
    /// Get current room settings
    /// </summary>
    public RoomSettings GetCurrentSettings()
    {
        return currentSettings.Clone();
    }
    
    /// <summary>
    /// Set room settings (for loading saved settings)
    /// </summary>
    public void SetRoomSettings(RoomSettings settings)
    {
        if (settings == null) return;
        
        currentSettings = settings.Clone();
        
        // Update dropdowns
        if (matchLengthDropdown != null)
        {
            var options = RoomSettings.GetMatchLengthOptions();
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == settings.matchLengthSeconds)
                {
                    matchLengthDropdown.value = i;
                    break;
                }
            }
        }
        
        if (mapSelectionDropdown != null)
        {
            for (int i = 0; i < availableMaps.Length; i++)
            {
                if (availableMaps[i].mapId == settings.selectedMap)
                {
                    mapSelectionDropdown.value = i;
                    break;
                }
            }
        }
        
        UpdateUI();
    }
}
