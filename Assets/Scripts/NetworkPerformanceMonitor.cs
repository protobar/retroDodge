using UnityEngine;
using TMPro;
using Photon.Pun;

/// <summary>
/// OPTIMIZED: Network Performance Monitor
/// Tracks network sync performance and displays metrics
/// </summary>
public class NetworkPerformanceMonitor : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI performanceText;
    [SerializeField] private bool showOnScreen = true;
    
    [Header("Monitoring")]
    [SerializeField] private bool enableMonitoring = true;
    
    // Performance metrics
    private int frameCount = 0;
    private float deltaTime = 0.0f;
    private float fps = 0.0f;
    
    // Network metrics
    private int networkUpdatesPerSecond = 0;
    private float lastNetworkUpdateTime = 0f;
    private int totalNetworkUpdates = 0;
    
    // Bandwidth estimation
    private float estimatedBandwidth = 0f;
    private int bytesPerUpdate = 0; // Rough estimate
    
    void Start()
    {
        if (performanceText == null && showOnScreen)
        {
            // Create on-screen text if not assigned
            CreateOnScreenText();
        }
    }
    
    void Update()
    {
        if (!enableMonitoring) return;
        
        // Calculate FPS
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        fps = 1.0f / deltaTime;
        frameCount++;
        
        // Update network metrics
        UpdateNetworkMetrics();
        
        // Update display
        if (showOnScreen && performanceText != null)
        {
            UpdatePerformanceDisplay();
        }
    }
    
    void UpdateNetworkMetrics()
    {
        // Count network updates (rough estimation)
        if (PhotonNetwork.IsConnected)
        {
            // FIXED: PUN2 uses global send rate settings
            networkUpdatesPerSecond = Mathf.RoundToInt(PhotonNetwork.SendRate);
            
            // OPTIMIZED: More accurate bandwidth estimation
            // Position (12 bytes) + Velocity (12 bytes) + States (5 bytes) + Abilities (12 bytes) = ~41 bytes per update
            bytesPerUpdate = 41;
            estimatedBandwidth = (networkUpdatesPerSecond * bytesPerUpdate) / 1024f; // KB/s
        }
    }
    
    void UpdatePerformanceDisplay()
    {
        string displayText = $"FPS: {fps:F1}\n";
        displayText += $"Network Updates/s: {networkUpdatesPerSecond}\n";
        displayText += $"Est. Bandwidth: {estimatedBandwidth:F1} KB/s\n";
        displayText += $"Players: {PhotonNetwork.PlayerList.Length}\n";
        displayText += $"Ping: {PhotonNetwork.GetPing()}ms";
        
        performanceText.text = displayText;
    }
    
    void CreateOnScreenText()
    {
        GameObject textObj = new GameObject("PerformanceMonitor");
        textObj.transform.SetParent(transform);
        
        performanceText = textObj.AddComponent<TextMeshProUGUI>();
        performanceText.text = "Performance Monitor";
        performanceText.fontSize = 16;
        performanceText.color = Color.white;
        
        // Position in top-left corner
        RectTransform rectTransform = performanceText.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(10, -10);
        rectTransform.sizeDelta = new Vector2(200, 100);
    }
    
    // Public methods for external monitoring
    public float GetFPS() => fps;
    public int GetNetworkUpdatesPerSecond() => networkUpdatesPerSecond;
    public float GetEstimatedBandwidth() => estimatedBandwidth;
    public int GetPing() => PhotonNetwork.GetPing();
    
    void OnGUI()
    {
        if (!showOnScreen || performanceText != null) return;
        
        // Fallback GUI display
        GUILayout.BeginArea(new Rect(10, 10, 200, 100));
        GUILayout.Label($"FPS: {fps:F1}");
        GUILayout.Label($"Network Updates/s: {networkUpdatesPerSecond}");
        GUILayout.Label($"Est. Bandwidth: {estimatedBandwidth:F1} KB/s");
        GUILayout.Label($"Ping: {PhotonNetwork.GetPing()}ms");
        GUILayout.EndArea();
    }
}
