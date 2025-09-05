using UnityEngine;
using Photon.Pun;
/// <summary>
/// Centralized Camera Shake Manager
/// Handles all camera shake requests (network sync handled by MatchManager)
/// </summary>
public class CameraShakeManager : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private CameraController cameraController;
    
    [Header("Network Sync")]
    [SerializeField] private MatchManager matchManager;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Singleton pattern for easy access
    private static CameraShakeManager instance;
    public static CameraShakeManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CameraShakeManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CameraShakeManager");
                    instance = go.AddComponent<CameraShakeManager>();
                }
            }
            return instance;
        }
    }
    
    void Awake()
    {
        // Singleton setup
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Find camera controller
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            if (enableDebugLogs)
            {
                Debug.Log($"[CAMERA SHAKE MANAGER] CameraController found: {cameraController != null}");
            }
        }
        
        // Find match manager for network sync
        if (matchManager == null)
        {
            matchManager = FindObjectOfType<MatchManager>();
            if (enableDebugLogs)
            {
                Debug.Log($"[CAMERA SHAKE MANAGER] MatchManager found: {matchManager != null}");
            }
        }
    }
    
    void Start()
    {
        // Ensure camera is found
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            if (enableDebugLogs)
            {
                Debug.Log($"[CAMERA SHAKE MANAGER] CameraController found in Start: {cameraController != null}");
            }
        }
    }
    
    /// <summary>
    /// Trigger camera shake locally and sync across network
    /// </summary>
    public void TriggerShake(float intensity, float duration, string source = "Unknown")
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CAMERA SHAKE MANAGER] TriggerShake called: intensity={intensity}, duration={duration}, source={source}");
        }
        
        // Apply shake locally
        ApplyShake(intensity, duration);
        
        // Sync across network through MatchManager
        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode && matchManager != null)
        {
            matchManager.SyncCameraShake(intensity, duration, source);
        }
        else if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode && matchManager == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[CAMERA SHAKE MANAGER] MatchManager is null, shake not synced across network");
            }
        }
    }
    
    /// <summary>
    /// Apply shake locally (no network sync)
    /// </summary>
    public void ApplyShake(float intensity, float duration)
    {
        if (cameraController == null)
        {
            // Try to find camera controller again
            cameraController = FindObjectOfType<CameraController>();
            if (cameraController == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogError("[CAMERA SHAKE MANAGER] CameraController not found! Cannot apply shake.");
                }
                return;
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[CAMERA SHAKE MANAGER] Applying shake: intensity={intensity}, duration={duration}");
        }
        
        cameraController.ShakeCamera(intensity, duration);
    }
    
    /// <summary>
    /// Called by MatchManager to sync shake from network
    /// </summary>
    public void SyncShakeFromNetwork(float intensity, float duration, string source)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CAMERA SHAKE MANAGER] Received shake sync: intensity={intensity}, duration={duration}, source={source}");
        }
        
        ApplyShake(intensity, duration);
    }
    
    /// <summary>
    /// Refresh camera controller reference
    /// </summary>
    public void RefreshCameraController()
    {
        cameraController = FindObjectOfType<CameraController>();
        if (enableDebugLogs)
        {
            Debug.Log($"[CAMERA SHAKE MANAGER] CameraController refreshed: {cameraController != null}");
        }
    }
    
    /// <summary>
    /// Check if camera controller is available
    /// </summary>
    public bool IsCameraAvailable()
    {
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
        }
        return cameraController != null;
    }
    
    /// <summary>
    /// Test method to verify shake system is working
    /// </summary>
    [ContextMenu("Test Shake")]
    public void TestShake()
    {
        Debug.Log("[CAMERA SHAKE MANAGER] Testing shake with intensity 5.0");
        TriggerShake(5.0f, 2.0f, "Test");
    }
}
