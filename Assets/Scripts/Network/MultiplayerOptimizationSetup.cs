using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// OPTIMIZED: Complete multiplayer optimization setup for 100ms ping
/// Integrates all optimization systems for smooth gameplay
/// </summary>
public class MultiplayerOptimizationSetup : MonoBehaviourPunCallbacks
{
    [Header("Optimization Configuration")]
    [SerializeField] private bool enableOptimizations = true;
    [SerializeField] private float targetPing = 100f; // 100ms target
    [SerializeField] private bool enableSmoothSync = true;
    [SerializeField] private bool enableLagCompensation = true;
    [SerializeField] private bool enableClientPrediction = true;
    
    [Header("Network Components")]
    [SerializeField] private NetworkOptimizationManager optimizationManager;
    [SerializeField] private OptimizedPlayerSync playerSync;
    [SerializeField] private OptimizedHitDetection hitDetection;
    [SerializeField] private OptimizedBallSync ballSync;
    
    [Header("Performance Settings")]
    [SerializeField] private float baseSendRate = 20f;
    [SerializeField] private float maxSendRate = 30f;
    [SerializeField] private float minSendRate = 10f;
    [SerializeField] private bool enableDynamicOptimization = true;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogging = false;
    [SerializeField] private bool showPerformanceStats = false;
    
    // Optimization state
    private bool isOptimized = false;
    private float optimizationStartTime;
    
    void Awake()
    {
        // Initialize optimization components
        InitializeOptimizationComponents();
    }
    
    void Start()
    {
        // Set up network optimization
        SetupNetworkOptimization();
        
        // Apply optimizations
        if (enableOptimizations)
        {
            ApplyOptimizations();
        }
    }
    
    void InitializeOptimizationComponents()
    {
        // Find or create optimization manager
        if (optimizationManager == null)
        {
            optimizationManager = FindObjectOfType<NetworkOptimizationManager>();
            if (optimizationManager == null)
            {
                GameObject managerObj = new GameObject("NetworkOptimizationManager");
                optimizationManager = managerObj.AddComponent<NetworkOptimizationManager>();
            }
        }
        
        // Find player sync components
        if (playerSync == null)
        {
            playerSync = FindObjectOfType<OptimizedPlayerSync>();
        }
        
        // Find hit detection components
        if (hitDetection == null)
        {
            hitDetection = FindObjectOfType<OptimizedHitDetection>();
        }
        
        // Find ball sync components
        if (ballSync == null)
        {
            ballSync = FindObjectOfType<OptimizedBallSync>();
        }
    }
    
    void SetupNetworkOptimization()
    {
        // Configure Photon settings for optimal performance
        PhotonNetwork.SendRate = (int)baseSendRate;
        PhotonNetwork.SerializationRate = (int)baseSendRate;
        
        // Note: EnableLobbyStatistics is read-only in PUN2
        // Lobby statistics are automatically enabled when connected
        
        // Set up network optimization manager
        if (optimizationManager != null)
        {
            optimizationManager.SetTargetLatency(targetPing);
            optimizationManager.SetMaxLatency(targetPing * 2f);
            optimizationManager.EnableOptimization(enableDynamicOptimization);
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"[Multiplayer Optimization] Setup complete - Target Ping: {targetPing}ms");
        }
    }
    
    void ApplyOptimizations()
    {
        if (isOptimized) return;
        
        optimizationStartTime = Time.time;
        
        // Apply player movement optimizations
        ApplyPlayerMovementOptimizations();
        
        // Apply hit detection optimizations
        ApplyHitDetectionOptimizations();
        
        // Apply ball synchronization optimizations
        ApplyBallSyncOptimizations();
        
        // Apply network optimization manager settings
        ApplyNetworkOptimizationSettings();
        
        isOptimized = true;
        
        if (enableDebugLogging)
        {
            Debug.Log("[Multiplayer Optimization] All optimizations applied successfully");
        }
    }
    
    void ApplyPlayerMovementOptimizations()
    {
        // Find all player characters and add optimized sync
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player == null) continue;
            
            // Add optimized player sync if not present
            OptimizedPlayerSync sync = player.GetComponent<OptimizedPlayerSync>();
            if (sync == null)
            {
                sync = player.gameObject.AddComponent<OptimizedPlayerSync>();
            }
            
            // Configure sync settings
            sync.SetSendRate(baseSendRate);
            sync.EnableClientPrediction(enableClientPrediction);
            
            if (enableDebugLogging)
            {
                Debug.Log($"[Multiplayer Optimization] Applied player sync to {player.name}");
            }
        }
    }
    
    void ApplyHitDetectionOptimizations()
    {
        // Find all ball controllers and add optimized hit detection
        BallController[] allBalls = FindObjectsOfType<BallController>();
        
        foreach (BallController ball in allBalls)
        {
            if (ball == null) continue;
            
            // Add optimized hit detection if not present
            OptimizedHitDetection hitDetection = ball.GetComponent<OptimizedHitDetection>();
            if (hitDetection == null)
            {
                hitDetection = ball.gameObject.AddComponent<OptimizedHitDetection>();
            }
            
            // Configure hit detection settings
            hitDetection.SetLagCompensation(enableLagCompensation);
            hitDetection.SetMaxLagCompensation(targetPing / 1000f); // Convert to seconds
            
            if (enableDebugLogging)
            {
                Debug.Log($"[Multiplayer Optimization] Applied hit detection to {ball.name}");
            }
        }
    }
    
    void ApplyBallSyncOptimizations()
    {
        // Find all ball controllers and add optimized ball sync
        BallController[] allBalls = FindObjectsOfType<BallController>();
        
        foreach (BallController ball in allBalls)
        {
            if (ball == null) continue;
            
            // Add optimized ball sync if not present
            OptimizedBallSync ballSync = ball.GetComponent<OptimizedBallSync>();
            if (ballSync == null)
            {
                ballSync = ball.gameObject.AddComponent<OptimizedBallSync>();
            }
            
            // Configure ball sync settings
            ballSync.SetSendRate(baseSendRate);
            ballSync.SetLagCompensation(enableLagCompensation);
            ballSync.SetMaxLagCompensation(targetPing / 1000f); // Convert to seconds
            
            if (enableDebugLogging)
            {
                Debug.Log($"[Multiplayer Optimization] Applied ball sync to {ball.name}");
            }
        }
    }
    
    void ApplyNetworkOptimizationSettings()
    {
        if (optimizationManager == null) return;
        
        // Configure optimization manager
        optimizationManager.SetTargetLatency(targetPing);
        optimizationManager.SetMaxLatency(targetPing * 2f);
        optimizationManager.EnableOptimization(enableDynamicOptimization);
        optimizationManager.SetInterestRadius(20f); // 20 unit interest radius
        
        if (enableDebugLogging)
        {
            Debug.Log("[Multiplayer Optimization] Network optimization manager configured");
        }
    }
    
    void Update()
    {
        // Monitor optimization performance
        if (enableOptimizations && isOptimized)
        {
            MonitorOptimizationPerformance();
        }
        
        // Show performance stats if enabled
        if (showPerformanceStats)
        {
            ShowPerformanceStats();
        }
    }
    
    void MonitorOptimizationPerformance()
    {
        // Check if optimizations are still active
        if (optimizationManager != null)
        {
            float currentLatency = optimizationManager.GetCurrentLatency();
            float currentSendRate = optimizationManager.GetCurrentSendRate();
            
            // Log performance if latency is high
            if (currentLatency > targetPing * 1.5f && enableDebugLogging)
            {
                Debug.LogWarning($"[Multiplayer Optimization] High latency detected: {currentLatency}ms");
            }
            
            // Log performance if send rate is low
            if (currentSendRate < minSendRate && enableDebugLogging)
            {
                Debug.LogWarning($"[Multiplayer Optimization] Low send rate detected: {currentSendRate}Hz");
            }
        }
    }
    
    void ShowPerformanceStats()
    {
        if (optimizationManager == null) return;
        
        // Display performance stats on screen
        float latency = optimizationManager.GetCurrentLatency();
        float sendRate = optimizationManager.GetCurrentSendRate();
        float bandwidth = optimizationManager.GetBandwidthUsage();
        
        // Create GUI display
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label($"=== Multiplayer Optimization Stats ===");
        GUILayout.Label($"Latency: {latency:F1}ms");
        GUILayout.Label($"Send Rate: {sendRate:F1}Hz");
        GUILayout.Label($"Bandwidth: {bandwidth:F2}KB/s");
        GUILayout.Label($"Target Ping: {targetPing}ms");
        GUILayout.Label($"Optimized: {isOptimized}");
        GUILayout.EndArea();
    }
    
    // Public methods for external systems
    public void EnableOptimizations(bool enable)
    {
        enableOptimizations = enable;
        
        if (enable)
        {
            ApplyOptimizations();
        }
        else
        {
            DisableOptimizations();
        }
    }
    
    public void DisableOptimizations()
    {
        isOptimized = false;
        
        // Disable optimization components
        if (optimizationManager != null)
        {
            optimizationManager.EnableOptimization(false);
        }
        
        if (enableDebugLogging)
        {
            Debug.Log("[Multiplayer Optimization] Optimizations disabled");
        }
    }
    
    public void SetTargetPing(float ping)
    {
        targetPing = ping;
        
        if (optimizationManager != null)
        {
            optimizationManager.SetTargetLatency(ping);
            optimizationManager.SetMaxLatency(ping * 2f);
        }
    }
    
    public void SetSendRate(float rate)
    {
        baseSendRate = rate;
        
        // Update all sync components
        OptimizedPlayerSync[] playerSyncs = FindObjectsOfType<OptimizedPlayerSync>();
        foreach (OptimizedPlayerSync sync in playerSyncs)
        {
            sync.SetSendRate(rate);
        }
        
        OptimizedBallSync[] ballSyncs = FindObjectsOfType<OptimizedBallSync>();
        foreach (OptimizedBallSync sync in ballSyncs)
        {
            sync.SetSendRate(rate);
        }
    }
    
    public bool IsOptimized()
    {
        return isOptimized;
    }
    
    public float GetCurrentLatency()
    {
        return optimizationManager != null ? optimizationManager.GetCurrentLatency() : 0f;
    }
    
    public float GetCurrentSendRate()
    {
        return optimizationManager != null ? optimizationManager.GetCurrentSendRate() : 0f;
    }
    
    public float GetBandwidthUsage()
    {
        return optimizationManager != null ? optimizationManager.GetBandwidthUsage() : 0f;
    }
    
    // Event handlers
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[Multiplayer Optimization] Player {newPlayer.NickName} joined - Reapplying optimizations");
        }
        
        // Reapply optimizations for new player
        if (enableOptimizations)
        {
            ApplyOptimizations();
        }
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[Multiplayer Optimization] Player {otherPlayer.NickName} left - Optimizations remain active");
        }
    }
    
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[Multiplayer Optimization] Master client switched to {newMasterClient.NickName}");
        }
        
        // Reapply optimizations for new master client
        if (enableOptimizations)
        {
            ApplyOptimizations();
        }
    }
    
    // Debug methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void OnGUI()
    {
        if (!showPerformanceStats) return;
        
        ShowPerformanceStats();
    }
}
