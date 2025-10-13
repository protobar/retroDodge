using UnityEngine;
using TMPro;
using UnityEngine.UI;
/// <summary>
/// Simple setup script to connect your existing graphics buttons with GraphicsSettingsManager
/// Just attach this to your main menu and assign your buttons
/// </summary>
public class GraphicsSettingsSetup : MonoBehaviour
{
    [Header("Your Existing Buttons")]
    [SerializeField] private Button performantButton;
    [SerializeField] private Button balancedButton;
    [SerializeField] private Button highFidelityButton;
    
    [Header("Optional UI Elements")]
    [SerializeField] private TMP_Text currentPresetText;
    [SerializeField] private TMP_Text fpsCounterText;
    
    [Header("Settings")]
    [SerializeField] private bool showFPS = true;
    [SerializeField] private bool autoDetectHardware = true;
    
    private GraphicsSettingsManager graphicsManager;
    
    void Start()
    {
        // Create or find graphics manager
        graphicsManager = FindObjectOfType<GraphicsSettingsManager>();
        if (graphicsManager == null)
        {
            // Create graphics manager
            GameObject managerObj = new GameObject("GraphicsSettingsManager");
            graphicsManager = managerObj.AddComponent<GraphicsSettingsManager>();
        }
        
        // Connect your buttons to the manager
        ConnectButtons();
        
        // Setup optional UI elements
        SetupOptionalUI();
    }
    
    void ConnectButtons()
    {
        // Connect your existing buttons to the graphics manager
        if (performantButton != null)
        {
            performantButton.onClick.RemoveAllListeners();
            performantButton.onClick.AddListener(() => {
                graphicsManager.SetGraphicsPreset(GraphicsSettingsManager.GraphicsPreset.Performant);
                Debug.Log("Graphics set to PERFORMANT");
            });
        }
        
        if (balancedButton != null)
        {
            balancedButton.onClick.RemoveAllListeners();
            balancedButton.onClick.AddListener(() => {
                graphicsManager.SetGraphicsPreset(GraphicsSettingsManager.GraphicsPreset.Balanced);
                Debug.Log("Graphics set to BALANCED");
            });
        }
        
        if (highFidelityButton != null)
        {
            highFidelityButton.onClick.RemoveAllListeners();
            highFidelityButton.onClick.AddListener(() => {
                graphicsManager.SetGraphicsPreset(GraphicsSettingsManager.GraphicsPreset.HighFidelity);
                Debug.Log("Graphics set to HIGH FIDELITY");
            });
        }
    }
    
    void SetupOptionalUI()
    {
        // Setup current preset text
        if (currentPresetText != null)
        {
            // This will be updated by the graphics manager
        }
        
        // Setup FPS counter
        if (fpsCounterText != null)
        {
            graphicsManager.SetEnableFPSDisplay(showFPS);
        }
    }
    
    // Public methods for external access
    public void SetPerformantGraphics()
    {
        if (graphicsManager != null)
        {
            graphicsManager.SetGraphicsPreset(GraphicsSettingsManager.GraphicsPreset.Performant);
        }
    }
    
    public void SetBalancedGraphics()
    {
        if (graphicsManager != null)
        {
            graphicsManager.SetGraphicsPreset(GraphicsSettingsManager.GraphicsPreset.Balanced);
        }
    }
    
    public void SetHighFidelityGraphics()
    {
        if (graphicsManager != null)
        {
            graphicsManager.SetGraphicsPreset(GraphicsSettingsManager.GraphicsPreset.HighFidelity);
        }
    }
    
    public GraphicsSettingsManager.GraphicsPreset GetCurrentPreset()
    {
        return graphicsManager != null ? graphicsManager.GetCurrentPreset() : GraphicsSettingsManager.GraphicsPreset.Balanced;
    }
    
    public float GetCurrentFPS()
    {
        return graphicsManager != null ? graphicsManager.GetCurrentFPS() : 0f;
    }
}
