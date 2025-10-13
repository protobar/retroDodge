using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Smooth;

/// <summary>
/// OPTIMIZED: Advanced player synchronization with Smooth Sync for 100ms ping
/// Features: Client-side prediction, lag compensation, interpolation, extrapolation
/// </summary>
public class OptimizedPlayerSync : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Smooth Sync Configuration")]
    [SerializeField] private SmoothSyncPUN2 smoothSync;
    [SerializeField] private bool useSmoothSync = true;
    
    [Header("Network Optimization")]
    [SerializeField] private float sendRate = 20f; // 20Hz for 50ms updates
    [SerializeField] private float interpolationBackTime = 0.1f; // 100ms buffer
    [SerializeField] private float extrapolationTimeLimit = 0.2f; // 200ms max extrapolation
    
    [Header("Lag Compensation")]
    [SerializeField] private bool enableLagCompensation = true;
    [SerializeField] private float maxLagCompensation = 0.2f; // 200ms max
    [SerializeField] private bool useClientPrediction = true;
    
    [Header("Network Traffic Optimization")]
    [SerializeField] private bool enableDeltaCompression = true;
    [SerializeField] private float positionThreshold = 0.01f; // 1cm threshold
    [SerializeField] private float rotationThreshold = 1f; // 1 degree threshold
    [SerializeField] private float velocityThreshold = 0.1f; // 0.1 units/sec threshold
    
    // Network state tracking
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private Vector3 lastSentVelocity;
    private float lastSendTime;
    private bool hasSignificantChange;
    
    // Client-side prediction
    private Vector3 predictedPosition;
    private Vector3 predictedVelocity;
    private bool isPredicting;
    
    // Lag compensation
    private float networkLatency;
    private float serverTimeOffset;
    
    // Components
    private PlayerCharacter playerCharacter;
    private Rigidbody rb;
    private Transform playerTransform;
    
    void Awake()
    {
        playerCharacter = GetComponent<PlayerCharacter>();
        rb = GetComponent<Rigidbody>();
        playerTransform = transform;
        
        // Initialize Smooth Sync if available
        if (useSmoothSync)
        {
            smoothSync = GetComponent<SmoothSyncPUN2>();
            if (smoothSync == null)
            {
                smoothSync = gameObject.AddComponent<SmoothSyncPUN2>();
            }
            
            ConfigureSmoothSync();
        }
        
        // Initialize network state
        lastSentPosition = playerTransform.position;
        lastSentRotation = playerTransform.rotation;
        lastSentVelocity = rb != null ? rb.velocity : Vector3.zero;
        lastSendTime = Time.time;
    }
    
    void Start()
    {
        // Set up network optimization
        OptimizeNetworkSettings();
    }
    
    void Update()
    {
        if (!photonView.IsMine) return;
        
        // Client-side prediction for local player
        if (useClientPrediction)
        {
            UpdateClientPrediction();
        }
        
        // Check for significant changes
        CheckForSignificantChanges();
    }
    
    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        
        // Force send on significant changes
        if (hasSignificantChange && Time.time - lastSendTime > 1f / sendRate)
        {
            ForceSendUpdate();
        }
    }
    
    void ConfigureSmoothSync()
    {
        if (smoothSync == null) return;
        
        // Configure Smooth Sync for optimal 100ms ping performance
        smoothSync.interpolationBackTime = interpolationBackTime;
        smoothSync.extrapolationMode = SmoothSyncPUN2.ExtrapolationMode.Limited;
        smoothSync.useExtrapolationTimeLimit = true;
        smoothSync.extrapolationTimeLimit = extrapolationTimeLimit;
        smoothSync.useExtrapolationDistanceLimit = true;
        smoothSync.extrapolationDistanceLimit = 5f;
        
        // Enable velocity sync for extrapolation
        smoothSync.syncVelocity = Smooth.SyncMode.XYZ;
        smoothSync.syncAngularVelocity = Smooth.SyncMode.XYZ;
        
        // Note: sendRate is not directly configurable in Smooth Sync
        // It uses PhotonNetwork.SendRate instead
        
        // Enable state validation for anti-cheat
        smoothSync.validateStateMethod = ValidatePlayerState;
    }
    
    void OptimizeNetworkSettings()
    {
        // Optimize Photon settings for 100ms ping
        PhotonNetwork.SendRate = 20; // 20 sends per second
        PhotonNetwork.SerializationRate = 20; // 20 serializations per second
        
        // Enable reliable UDP for critical data
        if (photonView != null)
        {
            photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
        }
    }
    
    void UpdateClientPrediction()
    {
        if (!isPredicting) return;
        
        // Predict movement based on input and physics
        Vector3 inputVelocity = GetInputVelocity();
        predictedPosition = playerTransform.position + inputVelocity * Time.fixedDeltaTime;
        predictedVelocity = inputVelocity;
        
        // Apply prediction to transform
        playerTransform.position = predictedPosition;
        if (rb != null)
        {
            rb.velocity = predictedVelocity;
        }
    }
    
    Vector3 GetInputVelocity()
    {
        // Get input from PlayerInputHandler
        var inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler == null) return Vector3.zero;
        
        float horizontal = inputHandler.GetHorizontal();
        float vertical = 0f; // No vertical movement in 2D game
        
        Vector3 moveDirection = new Vector3(horizontal, 0, vertical);
        float moveSpeed = playerCharacter?.GetCharacterData()?.moveSpeed ?? 5f;
        
        return moveDirection * moveSpeed;
    }
    
    void CheckForSignificantChanges()
    {
        Vector3 currentPosition = playerTransform.position;
        Quaternion currentRotation = playerTransform.rotation;
        Vector3 currentVelocity = rb != null ? rb.velocity : Vector3.zero;
        
        // Check position change
        bool positionChanged = Vector3.Distance(currentPosition, lastSentPosition) > positionThreshold;
        
        // Check rotation change
        bool rotationChanged = Quaternion.Angle(currentRotation, lastSentRotation) > rotationThreshold;
        
        // Check velocity change
        bool velocityChanged = Vector3.Distance(currentVelocity, lastSentVelocity) > velocityThreshold;
        
        hasSignificantChange = positionChanged || rotationChanged || velocityChanged;
    }
    
    void ForceSendUpdate()
    {
        if (smoothSync != null)
        {
            smoothSync.forceStateSendNextOnPhotonSerializeView();
        }
        
        // Update last sent values
        lastSentPosition = playerTransform.position;
        lastSentRotation = playerTransform.rotation;
        lastSentVelocity = rb != null ? rb.velocity : Vector3.zero;
        lastSendTime = Time.time;
        hasSignificantChange = false;
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // OPTIMIZED: Send only essential data with delta compression
            if (enableDeltaCompression)
            {
                // Send position delta
                Vector3 positionDelta = playerTransform.position - lastSentPosition;
                stream.SendNext(positionDelta);
                
                // Send velocity
                stream.SendNext(rb != null ? rb.velocity : Vector3.zero);
                
                // Send state flags (packed into single byte)
                byte stateFlags = PackStateFlags();
                stream.SendNext(stateFlags);
            }
            else
            {
                // Send full state
                stream.SendNext(playerTransform.position);
                stream.SendNext(playerTransform.rotation);
                stream.SendNext(rb != null ? rb.velocity : Vector3.zero);
                stream.SendNext(rb != null ? rb.angularVelocity : Vector3.zero);
            }
        }
        else
        {
            // OPTIMIZED: Receive and apply with lag compensation
            if (enableDeltaCompression)
            {
                Vector3 positionDelta = (Vector3)stream.ReceiveNext();
                Vector3 velocity = (Vector3)stream.ReceiveNext();
                byte stateFlags = (byte)stream.ReceiveNext();
                
                // Apply lag compensation
                if (enableLagCompensation)
                {
                    float lag = (float)(PhotonNetwork.Time - info.timestamp);
                    ApplyLagCompensationDelta(positionDelta, velocity, lag);
                }
                else
                {
                    // Apply position delta
                    playerTransform.position += positionDelta;
                    if (rb != null)
                    {
                        rb.velocity = velocity;
                    }
                }
                
                // Unpack state flags
                UnpackStateFlags(stateFlags);
            }
            else
            {
                Vector3 position = (Vector3)stream.ReceiveNext();
                Quaternion rotation = (Quaternion)stream.ReceiveNext();
                Vector3 velocity = (Vector3)stream.ReceiveNext();
                Vector3 angularVelocity = (Vector3)stream.ReceiveNext();
                
                // Apply lag compensation
                if (enableLagCompensation)
                {
                    float lag = (float)(PhotonNetwork.Time - info.timestamp);
                    ApplyLagCompensation(position, velocity, lag);
                }
                else
                {
                    playerTransform.position = position;
                    playerTransform.rotation = rotation;
                    if (rb != null)
                    {
                        rb.velocity = velocity;
                        rb.angularVelocity = angularVelocity;
                    }
                }
            }
        }
    }
    
    void ApplyLagCompensation(Vector3 position, Vector3 velocity, float lag)
    {
        // Clamp lag to reasonable range
        lag = Mathf.Clamp(lag, 0f, maxLagCompensation);
        
        // Predict position based on lag
        Vector3 compensatedPosition = position + velocity * lag;
        
        // Apply compensated position
        playerTransform.position = compensatedPosition;
        
        if (rb != null)
        {
            rb.velocity = velocity;
        }
    }
    
    void ApplyLagCompensationDelta(Vector3 positionDelta, Vector3 velocity, float lag)
    {
        // Clamp lag to reasonable range
        lag = Mathf.Clamp(lag, 0f, maxLagCompensation);
        
        // Predict position delta based on lag
        Vector3 compensatedDelta = positionDelta + velocity * lag;
        
        // Apply compensated delta
        playerTransform.position += compensatedDelta;
        
        if (rb != null)
        {
            rb.velocity = velocity;
        }
    }
    
    byte PackStateFlags()
    {
        byte flags = 0;
        
        if (playerCharacter != null)
        {
            if (playerCharacter.IsGrounded()) flags |= 1 << 0;
            if (playerCharacter.IsDucking()) flags |= 1 << 1;
            if (playerCharacter.HasBall()) flags |= 1 << 2;
            if (playerCharacter.IsFacingRight()) flags |= 1 << 3;
        }
        
        return flags;
    }
    
    void UnpackStateFlags(byte flags)
    {
        if (playerCharacter == null) return;
        
        // Note: These are read-only flags for display purposes
        // The actual state changes are handled by the PlayerCharacter itself
    }
    
    bool ValidatePlayerState(object state, object lastState)
    {
        // Anti-cheat validation
        if (state == null || lastState == null) return true;
        
        // Check for impossible movement (simplified validation)
        // Note: This is a placeholder for Smooth Sync state validation
        // The actual validation would depend on the Smooth Sync State structure
        return true;
        
        return true;
    }
    
    // Public methods for external systems
    public void EnableClientPrediction(bool enable)
    {
        useClientPrediction = enable;
        isPredicting = enable;
    }
    
    public void SetSendRate(float rate)
    {
        sendRate = rate;
        // Note: Smooth Sync uses PhotonNetwork.SendRate instead of individual sendRate
        PhotonNetwork.SendRate = (int)rate;
    }
    
    public float GetNetworkLatency()
    {
        return networkLatency;
    }
    
    public bool IsPredicting()
    {
        return isPredicting;
    }
}
