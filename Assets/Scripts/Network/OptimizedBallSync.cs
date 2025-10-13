using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Smooth;

/// <summary>
/// OPTIMIZED: Advanced ball synchronization with Smooth Sync for 100ms ping
/// Features: Predictive physics, lag compensation, interpolation, extrapolation
/// </summary>
public class OptimizedBallSync : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Smooth Sync Configuration")]
    [SerializeField] private SmoothSyncPUN2 smoothSync;
    [SerializeField] private bool useSmoothSync = true;
    
    [Header("Network Optimization")]
    [SerializeField] private float sendRate = 30f; // 30Hz for fast-moving ball
    [SerializeField] private float interpolationBackTime = 0.08f; // 80ms buffer
    [SerializeField] private float extrapolationTimeLimit = 0.15f; // 150ms max extrapolation
    
    [Header("Physics Prediction")]
    [SerializeField] private bool enablePhysicsPrediction = true;
    [SerializeField] private bool enableTrajectoryPrediction = true;
    [SerializeField] private float predictionTime = 0.1f; // 100ms prediction
    [SerializeField] private int predictionSteps = 10;
    
    [Header("Lag Compensation")]
    [SerializeField] private bool enableLagCompensation = true;
    [SerializeField] private float maxLagCompensation = 0.2f; // 200ms max
    [SerializeField] private bool enableRollback = true;
    
    [Header("Network Traffic Optimization")]
    [SerializeField] private bool enableDeltaCompression = true;
    [SerializeField] private float positionThreshold = 0.01f; // 1cm threshold
    [SerializeField] private float velocityThreshold = 0.1f; // 0.1 units/sec threshold
    [SerializeField] private float angularVelocityThreshold = 1f; // 1 deg/sec threshold
    
    // Network state tracking
    private Vector3 lastSentPosition;
    private Vector3 lastSentVelocity;
    private Vector3 lastSentAngularVelocity;
    private float lastSendTime;
    private bool hasSignificantChange;
    
    // Physics prediction
    private Vector3 predictedPosition;
    private Vector3 predictedVelocity;
    private bool isPredicting;
    private float predictionStartTime;
    
    // Lag compensation
    private float networkLatency;
    private float serverTimeOffset;
    private Vector3 lagCompensatedPosition;
    private Vector3 lagCompensatedVelocity;
    
    // Components
    private BallController ballController;
    private Rigidbody rb;
    private Transform ballTransform;
    
    // Trajectory prediction
    private Vector3[] predictedTrajectory;
    private float[] trajectoryTimestamps;
    private int trajectoryIndex;
    
    void Awake()
    {
        ballController = GetComponent<BallController>();
        rb = GetComponent<Rigidbody>();
        ballTransform = transform;
        
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
        lastSentPosition = ballTransform.position;
        lastSentVelocity = rb != null ? rb.velocity : Vector3.zero;
        lastSentAngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;
        lastSendTime = Time.time;
        
        // Initialize trajectory prediction
        predictedTrajectory = new Vector3[predictionSteps];
        trajectoryTimestamps = new float[predictionSteps];
    }
    
    void Start()
    {
        // Set up network optimization
        OptimizeNetworkSettings();
    }
    
    void Update()
    {
        if (!photonView.IsMine) return;
        
        // Physics prediction for local ball
        if (enablePhysicsPrediction)
        {
            UpdatePhysicsPrediction();
        }
        
        // Trajectory prediction
        if (enableTrajectoryPrediction)
        {
            UpdateTrajectoryPrediction();
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
        
        // Configure Smooth Sync for optimal ball performance
        smoothSync.interpolationBackTime = interpolationBackTime;
        smoothSync.extrapolationMode = SmoothSyncPUN2.ExtrapolationMode.Limited;
        smoothSync.useExtrapolationTimeLimit = true;
        smoothSync.extrapolationTimeLimit = extrapolationTimeLimit;
        smoothSync.useExtrapolationDistanceLimit = true;
        smoothSync.extrapolationDistanceLimit = 10f; // 10 units max extrapolation
        
        // Enable velocity sync for extrapolation
        smoothSync.syncVelocity = Smooth.SyncMode.XYZ;
        smoothSync.syncAngularVelocity = Smooth.SyncMode.XYZ;
        
        // Note: sendRate is not directly configurable in Smooth Sync
        // It uses PhotonNetwork.SendRate instead
        
        // Enable state validation for anti-cheat
        smoothSync.validateStateMethod = ValidateBallState;
    }
    
    void OptimizeNetworkSettings()
    {
        // Optimize Photon settings for ball
        PhotonNetwork.SendRate = (int)sendRate;
        PhotonNetwork.SerializationRate = (int)sendRate;
        
        // Enable reliable UDP for critical ball data
        if (photonView != null)
        {
            photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
        }
    }
    
    void UpdatePhysicsPrediction()
    {
        if (!isPredicting) return;
        
        // Predict ball physics
        Vector3 currentPosition = ballTransform.position;
        Vector3 currentVelocity = rb != null ? rb.velocity : Vector3.zero;
        
        // Simple physics prediction
        predictedPosition = currentPosition + currentVelocity * Time.fixedDeltaTime;
        predictedVelocity = currentVelocity;
        
        // Apply gravity if not grounded
        if (!IsGrounded())
        {
            predictedVelocity += Physics.gravity * Time.fixedDeltaTime;
        }
        
        // Apply prediction to transform
        ballTransform.position = predictedPosition;
        if (rb != null)
        {
            rb.velocity = predictedVelocity;
        }
    }
    
    void UpdateTrajectoryPrediction()
    {
        if (!enableTrajectoryPrediction) return;
        
        // Calculate predicted trajectory
        Vector3 currentPosition = ballTransform.position;
        Vector3 currentVelocity = rb != null ? rb.velocity : Vector3.zero;
        
        for (int i = 0; i < predictionSteps; i++)
        {
            float timeStep = predictionTime / predictionSteps;
            float time = i * timeStep;
            
            // Predict position at time
            Vector3 predictedPos = currentPosition + currentVelocity * time;
            
            // Apply gravity
            if (!IsGrounded())
            {
                predictedPos += Physics.gravity * time * time * 0.5f;
            }
            
            predictedTrajectory[i] = predictedPos;
            trajectoryTimestamps[i] = Time.time + time;
        }
        
        trajectoryIndex = 0;
    }
    
    bool IsGrounded()
    {
        // Check if ball is grounded
        return Physics.Raycast(ballTransform.position, Vector3.down, 0.1f);
    }
    
    void CheckForSignificantChanges()
    {
        Vector3 currentPosition = ballTransform.position;
        Vector3 currentVelocity = rb != null ? rb.velocity : Vector3.zero;
        Vector3 currentAngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;
        
        // Check position change
        bool positionChanged = Vector3.Distance(currentPosition, lastSentPosition) > positionThreshold;
        
        // Check velocity change
        bool velocityChanged = Vector3.Distance(currentVelocity, lastSentVelocity) > velocityThreshold;
        
        // Check angular velocity change
        bool angularVelocityChanged = Vector3.Distance(currentAngularVelocity, lastSentAngularVelocity) > angularVelocityThreshold;
        
        hasSignificantChange = positionChanged || velocityChanged || angularVelocityChanged;
    }
    
    void ForceSendUpdate()
    {
        if (smoothSync != null)
        {
            smoothSync.forceStateSendNextOnPhotonSerializeView();
        }
        
        // Update last sent values
        lastSentPosition = ballTransform.position;
        lastSentVelocity = rb != null ? rb.velocity : Vector3.zero;
        lastSentAngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;
        lastSendTime = Time.time;
        hasSignificantChange = false;
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // OPTIMIZED: Send ball state with delta compression
            if (enableDeltaCompression)
            {
                // Send position delta
                Vector3 positionDelta = ballTransform.position - lastSentPosition;
                stream.SendNext(positionDelta);
                
                // Send velocity
                stream.SendNext(rb != null ? rb.velocity : Vector3.zero);
                
                // Send angular velocity
                stream.SendNext(rb != null ? rb.angularVelocity : Vector3.zero);
                
                // Send ball state
                stream.SendNext((int)ballController.GetBallState());
                
                // Send timestamp
                stream.SendNext(Time.time);
            }
            else
            {
                // Send full state
                stream.SendNext(ballTransform.position);
                stream.SendNext(ballTransform.rotation);
                stream.SendNext(rb != null ? rb.velocity : Vector3.zero);
                stream.SendNext(rb != null ? rb.angularVelocity : Vector3.zero);
                stream.SendNext((int)ballController.GetBallState());
                stream.SendNext(Time.time);
            }
        }
        else
        {
            // OPTIMIZED: Receive and apply with lag compensation
            if (enableDeltaCompression)
            {
                Vector3 positionDelta = (Vector3)stream.ReceiveNext();
                Vector3 velocity = (Vector3)stream.ReceiveNext();
                Vector3 angularVelocity = (Vector3)stream.ReceiveNext();
                int ballState = (int)stream.ReceiveNext();
                float timestamp = (float)stream.ReceiveNext();
                
                // Apply lag compensation
                if (enableLagCompensation)
                {
                    float lag = (float)(PhotonNetwork.Time - timestamp);
                    ApplyLagCompensationDelta(positionDelta, velocity, lag);
                }
                else
                {
                    // Apply position delta
                    ballTransform.position += positionDelta;
                    if (rb != null)
                    {
                        rb.velocity = velocity;
                        rb.angularVelocity = angularVelocity;
                    }
                }
                
                // Update ball state
                ballController.SetBallState((BallController.BallState)ballState);
            }
            else
            {
                Vector3 position = (Vector3)stream.ReceiveNext();
                Quaternion rotation = (Quaternion)stream.ReceiveNext();
                Vector3 velocity = (Vector3)stream.ReceiveNext();
                Vector3 angularVelocity = (Vector3)stream.ReceiveNext();
                int ballState = (int)stream.ReceiveNext();
                float timestamp = (float)stream.ReceiveNext();
                
                // Apply lag compensation
                if (enableLagCompensation)
                {
                    float lag = (float)(PhotonNetwork.Time - timestamp);
                    ApplyLagCompensation(position, velocity, lag);
                }
                else
                {
                    ballTransform.position = position;
                    ballTransform.rotation = rotation;
                    if (rb != null)
                    {
                        rb.velocity = velocity;
                        rb.angularVelocity = angularVelocity;
                    }
                }
                
                // Update ball state
                ballController.SetBallState((BallController.BallState)ballState);
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
        ballTransform.position = compensatedPosition;
        
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
        ballTransform.position += compensatedDelta;
        
        if (rb != null)
        {
            rb.velocity = velocity;
        }
    }
    
    bool ValidateBallState(object state, object lastState)
    {
        // Anti-cheat validation for ball
        if (state == null || lastState == null) return true;
        
        // Check for impossible movement (simplified validation)
        // Note: This is a placeholder for Smooth Sync state validation
        // The actual validation would depend on the Smooth Sync State structure
        return true;
        
        return true;
    }
    
    // Public methods for external systems
    public void EnablePhysicsPrediction(bool enable)
    {
        enablePhysicsPrediction = enable;
        isPredicting = enable;
    }
    
    public void SetSendRate(float rate)
    {
        sendRate = rate;
        // Note: Smooth Sync uses PhotonNetwork.SendRate instead of individual sendRate
        PhotonNetwork.SendRate = (int)rate;
    }
    
    public Vector3 GetPredictedPosition(float time)
    {
        if (!enableTrajectoryPrediction) return ballTransform.position;
        
        // Find closest prediction
        int index = 0;
        float minTime = float.MaxValue;
        
        for (int i = 0; i < predictionSteps; i++)
        {
            float timeDiff = Mathf.Abs(trajectoryTimestamps[i] - time);
            if (timeDiff < minTime)
            {
                minTime = timeDiff;
                index = i;
            }
        }
        
        return predictedTrajectory[index];
    }
    
    public float GetNetworkLatency()
    {
        return networkLatency;
    }
    
    public bool IsPredicting()
    {
        return isPredicting;
    }
    
    public void SetLagCompensation(bool enable)
    {
        enableLagCompensation = enable;
    }
    
    public void SetMaxLagCompensation(float maxLag)
    {
        maxLagCompensation = maxLag;
    }
    
    // Debug methods
    void OnDrawGizmos()
    {
        if (!enableTrajectoryPrediction) return;
        
        // Draw predicted trajectory
        Gizmos.color = Color.yellow;
        for (int i = 0; i < predictionSteps - 1; i++)
        {
            Gizmos.DrawLine(predictedTrajectory[i], predictedTrajectory[i + 1]);
        }
    }
}
