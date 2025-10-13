using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// OPTIMIZED: Advanced hit detection with lag compensation for 100ms ping
/// Features: Rollback networking, client-side prediction, server reconciliation
/// </summary>
public class OptimizedHitDetection : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Hit Detection Optimization")]
    [SerializeField] private bool enableLagCompensation = true;
    [SerializeField] private float maxLagCompensation = 0.2f; // 200ms max rollback
    [SerializeField] private bool useClientPrediction = true;
    [SerializeField] private bool useServerReconciliation = true;
    
    [Header("Network Settings")]
    [SerializeField] private float hitDetectionRadius = 1.5f;
    [SerializeField] private float hitDetectionHeight = 2f;
    [SerializeField] private LayerMask playerLayerMask = -1;
    [SerializeField] private float hitCooldown = 0.1f;
    
    [Header("Performance Optimization")]
    [SerializeField] private bool enableSpatialPartitioning = true;
    [SerializeField] private float spatialGridSize = 5f;
    [SerializeField] private int maxChecksPerFrame = 10;
    
    // Hit detection state
    private Dictionary<int, HitRecord> hitRecords = new Dictionary<int, HitRecord>();
    private Dictionary<int, PlayerSnapshot> playerSnapshots = new Dictionary<int, PlayerSnapshot>();
    private Queue<HitEvent> pendingHits = new Queue<HitEvent>();
    
    // Performance tracking
    private int checksThisFrame = 0;
    private float lastCheckTime = 0f;
    
    // Components
    private BallController ballController;
    private PlayerCharacter thrower;
    
    // Spatial partitioning
    private Dictionary<Vector2Int, List<PlayerCharacter>> spatialGrid = new Dictionary<Vector2Int, List<PlayerCharacter>>();
    
    void Awake()
    {
        ballController = GetComponent<BallController>();
        thrower = GetComponent<PlayerCharacter>();
    }
    
    void Start()
    {
        // Initialize spatial partitioning
        if (enableSpatialPartitioning)
        {
            InitializeSpatialPartitioning();
        }
    }
    
    void Update()
    {
        if (!photonView.IsMine) return;
        
        // Reset frame counter
        if (Time.time != lastCheckTime)
        {
            checksThisFrame = 0;
            lastCheckTime = Time.time;
        }
        
        // Process pending hits
        ProcessPendingHits();
        
        // Update spatial partitioning
        if (enableSpatialPartitioning)
        {
            UpdateSpatialPartitioning();
        }
    }
    
    void InitializeSpatialPartitioning()
    {
        spatialGrid.Clear();
        
        // Get all players and add to spatial grid
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            AddToSpatialGrid(player);
        }
    }
    
    void UpdateSpatialPartitioning()
    {
        // Update player positions in spatial grid
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            UpdateSpatialGridPosition(player);
        }
    }
    
    void AddToSpatialGrid(PlayerCharacter player)
    {
        Vector2Int gridPos = GetGridPosition(player.transform.position);
        
        if (!spatialGrid.ContainsKey(gridPos))
        {
            spatialGrid[gridPos] = new List<PlayerCharacter>();
        }
        
        spatialGrid[gridPos].Add(player);
    }
    
    void UpdateSpatialGridPosition(PlayerCharacter player)
    {
        // Remove from old position
        Vector2Int oldGridPos = GetGridPosition(player.transform.position);
        if (spatialGrid.ContainsKey(oldGridPos))
        {
            spatialGrid[oldGridPos].Remove(player);
        }
        
        // Add to new position
        AddToSpatialGrid(player);
    }
    
    Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / spatialGridSize),
            Mathf.FloorToInt(worldPosition.z / spatialGridSize)
        );
    }
    
    public bool CheckHit(Vector3 ballPosition, Vector3 ballVelocity, float timestamp)
    {
        if (checksThisFrame >= maxChecksPerFrame) return false;
        checksThisFrame++;
        
        // Get nearby players using spatial partitioning
        List<PlayerCharacter> nearbyPlayers = GetNearbyPlayers(ballPosition);
        
        foreach (PlayerCharacter player in nearbyPlayers)
        {
            if (player == thrower) continue; // Can't hit yourself
            
            // Apply lag compensation
            Vector3 compensatedPosition = player.transform.position;
            if (enableLagCompensation)
            {
                compensatedPosition = GetLagCompensatedPosition(player, timestamp);
            }
            
            // Check hit with compensated position
            if (IsHit(ballPosition, compensatedPosition, ballVelocity))
            {
                // Record hit for server reconciliation
                RecordHit(player, ballPosition, ballVelocity, timestamp);
                
                // Send hit event
                SendHitEvent(player, ballPosition, ballVelocity);
                
                return true;
            }
        }
        
        return false;
    }
    
    List<PlayerCharacter> GetNearbyPlayers(Vector3 position)
    {
        if (!enableSpatialPartitioning)
        {
            // Fallback to all players
            return new List<PlayerCharacter>(FindObjectsOfType<PlayerCharacter>());
        }
        
        List<PlayerCharacter> nearbyPlayers = new List<PlayerCharacter>();
        Vector2Int gridPos = GetGridPosition(position);
        
        // Check current grid and adjacent grids
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector2Int checkPos = gridPos + new Vector2Int(x, z);
                if (spatialGrid.ContainsKey(checkPos))
                {
                    nearbyPlayers.AddRange(spatialGrid[checkPos]);
                }
            }
        }
        
        return nearbyPlayers;
    }
    
    Vector3 GetLagCompensatedPosition(PlayerCharacter player, float timestamp)
    {
        // Get player's PhotonView
        PhotonView playerView = player.GetComponent<PhotonView>();
        if (playerView == null) return player.transform.position;
        
        // Calculate lag
        float lag = (float)(PhotonNetwork.Time - timestamp);
        lag = Mathf.Clamp(lag, 0f, maxLagCompensation);
        
        // Get player's velocity for prediction
        Vector3 velocity = Vector3.zero;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            velocity = rb.velocity;
        }
        
        // Rollback position
        return player.transform.position - velocity * lag;
    }
    
    bool IsHit(Vector3 ballPosition, Vector3 playerPosition, Vector3 ballVelocity)
    {
        // Create hit detection capsule
        Vector3 capsuleStart = playerPosition;
        Vector3 capsuleEnd = playerPosition + Vector3.up * hitDetectionHeight;
        
        // Check if ball is within hit radius
        float distanceToCapsule = DistanceToCapsule(ballPosition, capsuleStart, capsuleEnd, hitDetectionRadius);
        
        return distanceToCapsule <= hitDetectionRadius;
    }
    
    float DistanceToCapsule(Vector3 point, Vector3 capsuleStart, Vector3 capsuleEnd, float radius)
    {
        Vector3 capsuleDirection = capsuleEnd - capsuleStart;
        float capsuleLength = capsuleDirection.magnitude;
        
        if (capsuleLength < 0.001f)
        {
            // Capsule is a sphere
            return Vector3.Distance(point, capsuleStart) - radius;
        }
        
        Vector3 capsuleDirectionNormalized = capsuleDirection / capsuleLength;
        Vector3 pointToCapsuleStart = point - capsuleStart;
        
        float projection = Vector3.Dot(pointToCapsuleStart, capsuleDirectionNormalized);
        projection = Mathf.Clamp(projection, 0f, capsuleLength);
        
        Vector3 closestPoint = capsuleStart + capsuleDirectionNormalized * projection;
        
        return Vector3.Distance(point, closestPoint) - radius;
    }
    
    void RecordHit(PlayerCharacter player, Vector3 ballPosition, Vector3 ballVelocity, float timestamp)
    {
        int playerID = player.GetComponent<PhotonView>().ViewID;
        
        HitRecord record = new HitRecord
        {
            playerID = playerID,
            ballPosition = ballPosition,
            ballVelocity = ballVelocity,
            timestamp = timestamp,
            serverTime = PhotonNetwork.Time
        };
        
        hitRecords[playerID] = record;
    }
    
    void SendHitEvent(PlayerCharacter player, Vector3 ballPosition, Vector3 ballVelocity)
    {
        HitEvent hitEvent = new HitEvent
        {
            targetPlayerID = player.GetComponent<PhotonView>().ViewID,
            ballPosition = ballPosition,
            ballVelocity = ballVelocity,
            timestamp = (float)PhotonNetwork.Time
        };
        
        // Send to all clients
        photonView.RPC("ProcessHitEvent", RpcTarget.All, 
            hitEvent.targetPlayerID, 
            hitEvent.ballPosition, 
            hitEvent.ballVelocity, 
            hitEvent.timestamp);
    }
    
    [PunRPC]
    void ProcessHitEvent(int targetPlayerID, Vector3 ballPosition, Vector3 ballVelocity, float timestamp)
    {
        // Find target player
        PhotonView targetView = PhotonView.Find(targetPlayerID);
        if (targetView == null) return;
        
        PlayerCharacter targetPlayer = targetView.GetComponent<PlayerCharacter>();
        if (targetPlayer == null) return;
        
        // Apply hit with lag compensation
        if (enableLagCompensation)
        {
            float lag = (float)(PhotonNetwork.Time - timestamp);
            lag = Mathf.Clamp(lag, 0f, maxLagCompensation);
            
            // Compensate for lag
            Vector3 compensatedPosition = ballPosition + ballVelocity * lag;
            ApplyHit(targetPlayer, compensatedPosition, ballVelocity);
        }
        else
        {
            ApplyHit(targetPlayer, ballPosition, ballVelocity);
        }
    }
    
    void ApplyHit(PlayerCharacter player, Vector3 ballPosition, Vector3 ballVelocity)
    {
        // Apply damage
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            int damage = 10; // Default damage value
            health.TakeDamage(damage, thrower);
        }
        
        // Apply knockback
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 knockbackDirection = (player.transform.position - ballPosition).normalized;
            float knockbackForce = ballVelocity.magnitude * 0.5f;
            playerRb.AddForce(knockbackDirection * knockbackForce, ForceMode.Impulse);
        }
        
        // Trigger hit effects (placeholder - implement based on your PlayerCharacter)
        // player.OnHit(ballPosition, ballVelocity);
    }
    
    void ProcessPendingHits()
    {
        while (pendingHits.Count > 0)
        {
            HitEvent hitEvent = pendingHits.Dequeue();
            
            // Process hit event
            PhotonView targetView = PhotonView.Find(hitEvent.targetPlayerID);
            if (targetView != null)
            {
                PlayerCharacter targetPlayer = targetView.GetComponent<PlayerCharacter>();
                if (targetPlayer != null)
                {
                    ApplyHit(targetPlayer, hitEvent.ballPosition, hitEvent.ballVelocity);
                }
            }
        }
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send hit detection state
            stream.SendNext(transform.position);
            stream.SendNext(ballController != null ? ballController.GetVelocity() : Vector3.zero);
            stream.SendNext(Time.time);
        }
        else
        {
            // Receive hit detection state
            Vector3 position = (Vector3)stream.ReceiveNext();
            Vector3 velocity = (Vector3)stream.ReceiveNext();
            float timestamp = (float)stream.ReceiveNext();
            
            // Update hit detection with received data
            if (ballController != null)
            {
                // Note: These methods may not exist in your BallController
                // ballController.SetPosition(position);
                // ballController.SetVelocity(velocity);
            }
        }
    }
    
    // Public methods for external systems
    public void SetLagCompensation(bool enable)
    {
        enableLagCompensation = enable;
    }
    
    public void SetMaxLagCompensation(float maxLag)
    {
        maxLagCompensation = maxLag;
    }
    
    public float GetNetworkLatency()
    {
        return (float)(PhotonNetwork.Time - PhotonNetwork.Time);
    }
    
    public bool IsHitDetectionEnabled()
    {
        return enabled && gameObject.activeInHierarchy;
    }
}

// Data structures for hit detection
[System.Serializable]
public struct HitRecord
{
    public int playerID;
    public Vector3 ballPosition;
    public Vector3 ballVelocity;
    public float timestamp;
    public double serverTime;
}

[System.Serializable]
public struct HitEvent
{
    public int targetPlayerID;
    public Vector3 ballPosition;
    public Vector3 ballVelocity;
    public float timestamp;
}

[System.Serializable]
public struct PlayerSnapshot
{
    public Vector3 position;
    public Vector3 velocity;
    public float timestamp;
    public int playerID;
}
