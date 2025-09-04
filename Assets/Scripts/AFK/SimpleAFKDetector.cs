using UnityEngine;
using Photon.Pun;

/// <summary>
/// Simple AFK Detection System
/// If player doesn't move for 10 seconds, show message and start dealing damage
/// </summary>
public class SimpleAFKDetector : MonoBehaviourPun, IPunObservable
{
    [Header("AFK Settings")]
    [SerializeField] private float afkThreshold = 10f; // Seconds of inactivity before AFK
    [SerializeField] private float damageInterval = 1f; // Damage every X seconds
    [SerializeField] private int afkDamage = 5; // Damage per interval
    [SerializeField] private float positionThreshold = 0.5f; // Minimum position change to reset AFK timer
    
    // AFK State
    private bool isAFK = false;
    private float lastActivityTime;
    private Vector3 lastPosition;
    
    // References
    private PlayerCharacter playerCharacter;
    private PlayerHealth playerHealth;
    private AFKUIManager afkUIManager;
    
    // Network sync
    private bool networkIsAFK;
    
    // Events
    public System.Action OnAFKStarted;
    public System.Action OnAFKEnded;
    
    void Awake()
    {
        playerCharacter = GetComponent<PlayerCharacter>();
        playerHealth = GetComponent<PlayerHealth>();
        afkUIManager = FindObjectOfType<AFKUIManager>();
        
        // Initialize tracking
        lastActivityTime = Time.time;
        lastPosition = transform.position;
    }
    
    void Update()
    {
        if (!photonView.IsMine) return;
        
        // Check for movement activity
        bool hasMovement = CheckMovementActivity();
        
        // Reset AFK timer if movement detected
        if (hasMovement)
        {
            ResetAFKTimer();
        }
        
        float timeSinceActivity = Time.time - lastActivityTime;
        
        // Check for AFK
        if (!isAFK && timeSinceActivity >= afkThreshold)
        {
            StartAFK();
        }
        
        // Handle AFK damage
        if (isAFK)
        {
            HandleAFKDamage();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // NETWORK SYNC (no-op to satisfy Observed Components)
    // ═══════════════════════════════════════════════════════════════
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // We don't stream any data here. This exists only to avoid
        // runtime errors if this component is listed in Observed Components.
        // AFK UI sync is handled via RPCs in AFKUIManager.
    }
    
    /// <summary>
    /// Check if player has movement activity
    /// </summary>
    bool CheckMovementActivity()
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        
        if (distanceMoved > positionThreshold)
        {
            lastPosition = currentPosition;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Reset AFK timer due to activity
    /// </summary>
    void ResetAFKTimer()
    {
        lastActivityTime = Time.time;
        
        if (isAFK)
        {
            EndAFK();
        }
    }
    
    /// <summary>
    /// Start AFK state
    /// </summary>
    void StartAFK()
    {
        if (isAFK) return;
        
        isAFK = true;
        OnAFKStarted?.Invoke();
        
        // Sync AFK state to all players
        photonView.RPC("SyncAFKState", RpcTarget.All, true);
        
        // Show AFK message using UI manager
        if (afkUIManager != null)
        {
            afkUIManager.ShowAFKMessage(playerCharacter.name, true);
            afkUIManager.UpdateAFKStatus(playerCharacter.name, true);
        }
        
        Debug.Log($"[AFK] {playerCharacter.name} is now AFK!");
    }
    
    /// <summary>
    /// End AFK state
    /// </summary>
    void EndAFK()
    {
        if (!isAFK) return;
        
        isAFK = false;
        OnAFKEnded?.Invoke();
        
        // Sync AFK state to all players
        photonView.RPC("SyncAFKState", RpcTarget.All, false);
        
        // Show normal message using UI manager
        if (afkUIManager != null)
        {
            afkUIManager.ShowAFKMessage(playerCharacter.name, false);
            afkUIManager.UpdateAFKStatus(playerCharacter.name, false);
        }
        
        Debug.Log($"[AFK] {playerCharacter.name} is no longer AFK!");
    }
    
    /// <summary>
    /// Handle AFK damage
    /// </summary>
    void HandleAFKDamage()
    {
        if (playerHealth == null) return;
        
        // Apply damage at intervals
        if (Time.time - lastActivityTime >= damageInterval)
        {
            playerHealth.TakeDamage(afkDamage, null);
            Debug.Log($"[AFK] {playerCharacter.name} took {afkDamage} AFK damage");
            
            // Reset timer for next damage
            lastActivityTime = Time.time;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // RPC METHODS
    // ═══════════════════════════════════════════════════════════════
    
    [PunRPC]
    void SyncAFKState(bool afkState)
    {
        if (photonView.IsMine) return;
        
        isAFK = afkState;
        
        if (afkState)
        {
            OnAFKStarted?.Invoke();
        }
        else
        {
            OnAFKEnded?.Invoke();
        }
    }
    
    
    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════
    
    public bool IsAFK() => isAFK;
    public float GetTimeSinceActivity() => Time.time - lastActivityTime;
    
    /// <summary>
    /// Force reset AFK timer (for external use)
    /// </summary>
    public void ForceResetAFKTimer()
    {
        ResetAFKTimer();
    }
}
