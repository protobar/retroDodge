using UnityEngine;
using Photon.Pun;

/// <summary>
/// QUICK SETUP: Simple one-click network optimization for 100ms ping
/// Just add this script to any GameObject in your scene to enable all optimizations
/// </summary>
public class QuickNetworkOptimization : MonoBehaviourPunCallbacks
{
    [Header("Quick Optimization Settings")]
    [SerializeField] private bool enableOptimizations = true;
    [SerializeField] private float targetPing = 100f;
    [SerializeField] private bool showDebugInfo = false;
    
    [Header("Auto-Apply Settings")]
    [SerializeField] private bool autoApplyToPlayers = true;
    [SerializeField] private bool autoApplyToBalls = true;
    [SerializeField] private bool autoApplyToScene = true;
    
    private MultiplayerOptimizationSetup optimizationSetup;
    
    void Start()
    {
        if (enableOptimizations)
        {
            ApplyQuickOptimizations();
        }
    }
    
    void ApplyQuickOptimizations()
    {
        // Create optimization setup
        GameObject optimizationObj = new GameObject("NetworkOptimization");
        optimizationSetup = optimizationObj.AddComponent<MultiplayerOptimizationSetup>();
        
        // Configure for 100ms ping
        optimizationSetup.SetTargetPing(targetPing);
        optimizationSetup.EnableOptimizations(true);
        
        // Auto-apply to existing objects
        if (autoApplyToScene)
        {
            ApplyToExistingObjects();
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[Quick Network Optimization] Applied optimizations for {targetPing}ms ping");
        }
    }
    
    void ApplyToExistingObjects()
    {
        // Apply to all player characters
        if (autoApplyToPlayers)
        {
            PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
            foreach (PlayerCharacter player in allPlayers)
            {
                if (player.GetComponent<OptimizedPlayerSync>() == null)
                {
                    player.gameObject.AddComponent<OptimizedPlayerSync>();
                }
            }
        }
        
        // Apply to all ball controllers
        if (autoApplyToBalls)
        {
            BallController[] allBalls = FindObjectsOfType<BallController>();
            foreach (BallController ball in allBalls)
            {
                if (ball.GetComponent<OptimizedBallSync>() == null)
                {
                    ball.gameObject.AddComponent<OptimizedBallSync>();
                }
                
                if (ball.GetComponent<OptimizedHitDetection>() == null)
                {
                    ball.gameObject.AddComponent<OptimizedHitDetection>();
                }
            }
        }
    }
    
    void Update()
    {
        if (showDebugInfo && optimizationSetup != null)
        {
            ShowQuickDebugInfo();
        }
    }
    
    void ShowQuickDebugInfo()
    {
        float latency = optimizationSetup.GetCurrentLatency();
        float sendRate = optimizationSetup.GetCurrentSendRate();
        float bandwidth = optimizationSetup.GetBandwidthUsage();
        
        GUILayout.BeginArea(new Rect(10, 10, 250, 100));
        GUILayout.Label($"=== Network Optimization ===");
        GUILayout.Label($"Latency: {latency:F1}ms");
        GUILayout.Label($"Send Rate: {sendRate:F1}Hz");
        GUILayout.Label($"Bandwidth: {bandwidth:F2}KB/s");
        GUILayout.Label($"Target: {targetPing}ms");
        GUILayout.EndArea();
    }
    
    // Public methods for external control
    public void ToggleOptimizations()
    {
        enableOptimizations = !enableOptimizations;
        
        if (enableOptimizations)
        {
            ApplyQuickOptimizations();
        }
        else
        {
            if (optimizationSetup != null)
            {
                optimizationSetup.EnableOptimizations(false);
            }
        }
    }
    
    public void SetTargetPing(float ping)
    {
        targetPing = ping;
        
        if (optimizationSetup != null)
        {
            optimizationSetup.SetTargetPing(ping);
        }
    }
    
    public bool IsOptimized()
    {
        return optimizationSetup != null && optimizationSetup.IsOptimized();
    }
}



