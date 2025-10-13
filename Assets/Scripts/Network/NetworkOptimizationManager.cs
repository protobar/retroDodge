using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// OPTIMIZED: Central network optimization manager for 100ms ping performance
/// Features: Dynamic send rate adjustment, bandwidth optimization, latency compensation
/// </summary>
public class NetworkOptimizationManager : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Network Performance")]
    [SerializeField] private float targetLatency = 100f; // 100ms target
    [SerializeField] private float maxLatency = 200f; // 200ms max acceptable
    [SerializeField] private bool enableDynamicOptimization = true;
    [SerializeField] private bool enableBandwidthOptimization = true;
    
    [Header("Send Rate Optimization")]
    [SerializeField] private float baseSendRate = 20f; // 20Hz base rate
    [SerializeField] private float maxSendRate = 30f; // 30Hz max rate
    [SerializeField] private float minSendRate = 10f; // 10Hz min rate
    [SerializeField] private float sendRateAdjustmentSpeed = 2f;
    
    [Header("Bandwidth Optimization")]
    [SerializeField] private bool enableDeltaCompression = true;
    [SerializeField] private bool enableInterestManagement = true;
    [SerializeField] private float interestRadius = 20f;
    [SerializeField] private bool enablePrioritySystem = true;
    
    [Header("Lag Compensation")]
    [SerializeField] private bool enableLagCompensation = true;
    [SerializeField] private float maxLagCompensation = 0.2f; // 200ms max
    [SerializeField] private bool enableClientPrediction = true;
    [SerializeField] private bool enableServerReconciliation = true;
    
    [Header("Performance Monitoring")]
    [SerializeField] private bool enablePerformanceMonitoring = true;
    [SerializeField] private float monitoringInterval = 1f;
    [SerializeField] private int maxSamples = 60; // 1 minute of samples
    
    // Network state tracking
    private float currentLatency;
    private float averageLatency;
    private float currentSendRate;
    private float bandwidthUsage;
    private float packetLoss;
    
    // Performance history
    private Queue<float> latencyHistory = new Queue<float>();
    private Queue<float> sendRateHistory = new Queue<float>();
    private Queue<float> bandwidthHistory = new Queue<float>();
    
    // Optimization state
    private bool isOptimizing = false;
    private float lastOptimizationTime;
    private float optimizationInterval = 2f; // Optimize every 2 seconds
    
    // Network components
    private List<OptimizedPlayerSync> playerSyncs = new List<OptimizedPlayerSync>();
    private List<OptimizedHitDetection> hitDetections = new List<OptimizedHitDetection>();
    
    // Interest management
    private Dictionary<int, float> playerDistances = new Dictionary<int, float>();
    private Dictionary<int, bool> playerVisibility = new Dictionary<int, bool>();
    
    void Start()
    {
        // Initialize network optimization
        InitializeNetworkOptimization();
        
        // Start performance monitoring
        if (enablePerformanceMonitoring)
        {
            InvokeRepeating(nameof(MonitorPerformance), 0f, monitoringInterval);
        }
        
        // Start optimization loop
        if (enableDynamicOptimization)
        {
            InvokeRepeating(nameof(OptimizeNetwork), 0f, optimizationInterval);
        }
    }
    
    void InitializeNetworkOptimization()
    {
        // Set initial Photon settings
        PhotonNetwork.SendRate = (int)baseSendRate;
        PhotonNetwork.SerializationRate = (int)baseSendRate;
        
        // Enable reliable UDP for critical data
        // Note: EnableLobbyStatistics is read-only in PUN2
        // Lobby statistics are automatically enabled when connected
        
        // Initialize current send rate
        currentSendRate = baseSendRate;
        
        // Find all network components
        FindNetworkComponents();
        
        // Configure components
        ConfigureNetworkComponents();
    }
    
    void FindNetworkComponents()
    {
        // Find all player sync components
        playerSyncs.Clear();
        OptimizedPlayerSync[] syncs = FindObjectsOfType<OptimizedPlayerSync>();
        playerSyncs.AddRange(syncs);
        
        // Find all hit detection components
        hitDetections.Clear();
        OptimizedHitDetection[] hits = FindObjectsOfType<OptimizedHitDetection>();
        hitDetections.AddRange(hits);
    }
    
    void ConfigureNetworkComponents()
    {
        // Configure player sync components
        foreach (OptimizedPlayerSync sync in playerSyncs)
        {
            sync.SetSendRate(currentSendRate);
        }
        
        // Configure hit detection components
        foreach (OptimizedHitDetection hit in hitDetections)
        {
            hit.SetLagCompensation(enableLagCompensation);
            hit.SetMaxLagCompensation(maxLagCompensation);
        }
    }
    
    void MonitorPerformance()
    {
        if (!enablePerformanceMonitoring) return;
        
        // Get current network statistics
        currentLatency = GetCurrentLatencyFromPhoton();
        bandwidthUsage = GetBandwidthUsageFromSendRate();
        packetLoss = GetPacketLoss();
        
        // Update history
        UpdatePerformanceHistory();
        
        // Calculate averages
        CalculateAverages();
        
        // Log performance if needed
        if (PhotonNetwork.IsMasterClient)
        {
            LogPerformanceStats();
        }
    }
    
    float GetCurrentLatencyFromPhoton()
    {
        // Get latency from Photon
        if (PhotonNetwork.IsConnected)
        {
            return PhotonNetwork.GetPing();
        }
        return 0f;
    }
    
    float GetBandwidthUsageFromSendRate()
    {
        // Estimate bandwidth usage based on send rate and data size
        float dataPerPacket = 64f; // bytes per packet (estimated)
        float packetsPerSecond = currentSendRate;
        return (dataPerPacket * packetsPerSecond) / 1024f; // KB/s
    }
    
    float GetPacketLoss()
    {
        // Estimate packet loss based on network conditions
        if (currentLatency > maxLatency)
        {
            return Mathf.Clamp((currentLatency - targetLatency) / maxLatency, 0f, 0.1f);
        }
        return 0f;
    }
    
    void UpdatePerformanceHistory()
    {
        // Add current values to history
        latencyHistory.Enqueue(currentLatency);
        sendRateHistory.Enqueue(currentSendRate);
        bandwidthHistory.Enqueue(bandwidthUsage);
        
        // Remove old values
        if (latencyHistory.Count > maxSamples)
        {
            latencyHistory.Dequeue();
        }
        if (sendRateHistory.Count > maxSamples)
        {
            sendRateHistory.Dequeue();
        }
        if (bandwidthHistory.Count > maxSamples)
        {
            bandwidthHistory.Dequeue();
        }
    }
    
    void CalculateAverages()
    {
        if (latencyHistory.Count > 0)
        {
            float totalLatency = 0f;
            foreach (float latency in latencyHistory)
            {
                totalLatency += latency;
            }
            averageLatency = totalLatency / latencyHistory.Count;
        }
    }
    
    void LogPerformanceStats()
    {
        Debug.Log($"[Network Optimization] Latency: {currentLatency}ms, Avg: {averageLatency}ms, Send Rate: {currentSendRate}Hz, Bandwidth: {bandwidthUsage:F2}KB/s");
    }
    
    void OptimizeNetwork()
    {
        if (!enableDynamicOptimization) return;
        
        // Adjust send rate based on latency
        AdjustSendRate();
        
        // Optimize bandwidth usage
        if (enableBandwidthOptimization)
        {
            OptimizeBandwidth();
        }
        
        // Update interest management
        if (enableInterestManagement)
        {
            UpdateInterestManagement();
        }
        
        // Apply optimizations
        ApplyOptimizations();
    }
    
    void AdjustSendRate()
    {
        float targetSendRate = baseSendRate;
        
        if (currentLatency > targetLatency)
        {
            // High latency - reduce send rate
            float latencyRatio = currentLatency / targetLatency;
            targetSendRate = baseSendRate / latencyRatio;
        }
        else if (currentLatency < targetLatency * 0.5f)
        {
            // Low latency - increase send rate
            targetSendRate = Mathf.Min(baseSendRate * 1.5f, maxSendRate);
        }
        
        // Clamp send rate
        targetSendRate = Mathf.Clamp(targetSendRate, minSendRate, maxSendRate);
        
        // Smooth send rate changes
        currentSendRate = Mathf.Lerp(currentSendRate, targetSendRate, sendRateAdjustmentSpeed * Time.deltaTime);
    }
    
    void OptimizeBandwidth()
    {
        // Enable delta compression if bandwidth is high
        if (bandwidthUsage > 50f) // 50KB/s threshold
        {
            EnableDeltaCompression(true);
        }
        else
        {
            EnableDeltaCompression(false);
        }
        
        // Adjust interest management based on bandwidth
        if (bandwidthUsage > 100f) // 100KB/s threshold
        {
            interestRadius = Mathf.Max(10f, interestRadius - 2f);
        }
        else if (bandwidthUsage < 20f) // 20KB/s threshold
        {
            interestRadius = Mathf.Min(30f, interestRadius + 2f);
        }
    }
    
    void UpdateInterestManagement()
    {
        if (!enableInterestManagement) return;
        
        // Update player distances
        UpdatePlayerDistances();
        
        // Update player visibility
        UpdatePlayerVisibility();
    }
    
    void UpdatePlayerDistances()
    {
        playerDistances.Clear();
        
        // Get all players
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player == null) continue;
            
            PhotonView playerView = player.GetComponent<PhotonView>();
            if (playerView == null) continue;
            
            // Calculate distance to local player
            Vector3 localPlayerPos = GetLocalPlayerPosition();
            float distance = Vector3.Distance(localPlayerPos, player.transform.position);
            
            playerDistances[playerView.ViewID] = distance;
        }
    }
    
    void UpdatePlayerVisibility()
    {
        playerVisibility.Clear();
        
        foreach (var kvp in playerDistances)
        {
            int playerID = kvp.Key;
            float distance = kvp.Value;
            
            // Player is visible if within interest radius
            bool visible = distance <= interestRadius;
            playerVisibility[playerID] = visible;
        }
    }
    
    Vector3 GetLocalPlayerPosition()
    {
        // Find local player
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            PhotonView playerView = player.GetComponent<PhotonView>();
            if (playerView != null && playerView.IsMine)
            {
                return player.transform.position;
            }
        }
        return Vector3.zero;
    }
    
    void ApplyOptimizations()
    {
        // Update Photon settings
        PhotonNetwork.SendRate = (int)currentSendRate;
        PhotonNetwork.SerializationRate = (int)currentSendRate;
        
        // Update player sync components
        foreach (OptimizedPlayerSync sync in playerSyncs)
        {
            if (sync != null)
            {
                sync.SetSendRate(currentSendRate);
            }
        }
        
        // Update hit detection components
        foreach (OptimizedHitDetection hit in hitDetections)
        {
            if (hit != null)
            {
                hit.SetLagCompensation(enableLagCompensation);
                hit.SetMaxLagCompensation(maxLagCompensation);
            }
        }
    }
    
    void EnableDeltaCompression(bool enable)
    {
        // Enable delta compression on all network components
        foreach (OptimizedPlayerSync sync in playerSyncs)
        {
            if (sync != null)
            {
                // This would need to be implemented in OptimizedPlayerSync
                // sync.SetDeltaCompression(enable);
            }
        }
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send optimization settings
            stream.SendNext(currentSendRate);
            stream.SendNext(enableLagCompensation);
            stream.SendNext(maxLagCompensation);
            stream.SendNext(Time.time);
        }
        else
        {
            // Receive optimization settings
            float receivedSendRate = (float)stream.ReceiveNext();
            bool receivedLagCompensation = (bool)stream.ReceiveNext();
            float receivedMaxLagCompensation = (float)stream.ReceiveNext();
            float timestamp = (float)stream.ReceiveNext();
            
            // Apply received settings
            if (receivedSendRate > 0)
            {
                currentSendRate = receivedSendRate;
            }
            
            enableLagCompensation = receivedLagCompensation;
            maxLagCompensation = receivedMaxLagCompensation;
        }
    }
    
    // Public methods for external systems
    public float GetCurrentLatency()
    {
        return currentLatency;
    }
    
    public float GetAverageLatency()
    {
        return averageLatency;
    }
    
    public float GetCurrentSendRate()
    {
        return currentSendRate;
    }
    
    public float GetBandwidthUsage()
    {
        return bandwidthUsage;
    }
    
    public bool IsPlayerVisible(int playerID)
    {
        return playerVisibility.ContainsKey(playerID) && playerVisibility[playerID];
    }
    
    public void SetTargetLatency(float latency)
    {
        targetLatency = latency;
    }
    
    public void SetMaxLatency(float maxLatency)
    {
        this.maxLatency = maxLatency;
    }
    
    public void EnableOptimization(bool enable)
    {
        enableDynamicOptimization = enable;
    }
    
    public void SetInterestRadius(float radius)
    {
        interestRadius = radius;
    }
    
    // Debug methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void OnGUI()
    {
        if (!enablePerformanceMonitoring) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Latency: {currentLatency:F1}ms");
        GUILayout.Label($"Avg Latency: {averageLatency:F1}ms");
        GUILayout.Label($"Send Rate: {currentSendRate:F1}Hz");
        GUILayout.Label($"Bandwidth: {bandwidthUsage:F2}KB/s");
        GUILayout.Label($"Packet Loss: {packetLoss:P1}");
        GUILayout.EndArea();
    }
}
