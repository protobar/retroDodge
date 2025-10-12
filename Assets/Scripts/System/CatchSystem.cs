using UnityEngine;
using Photon.Pun;

/// <summary>
/// FIXED: Scale-aware CatchSystem that adapts to character size
/// Now properly positions catch zone at character center using CharacterScaleManager
/// </summary>
public class CatchSystem : MonoBehaviourPunCallbacks
{
    [Header("Catch Settings")]
    [SerializeField] private float baseCatchRange = 2.5f; // Base range that scales with character
    [SerializeField] private float catchCooldown = 0.5f;
    [SerializeField] private bool debugMode = false;

    [Header("Scale Settings")]
    [SerializeField] private bool useDynamicScaling = true;
    [SerializeField] private float catchHeightRatio = 0.5f; // 0.5 = center of character
    private float scaledCatchRange = 2.5f;
    private Vector3 catchCenterOffset = Vector3.zero;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject catchIndicator;
    [SerializeField] private Color perfectZoneColor = Color.green;
    [SerializeField] private Color goodZoneColor = Color.yellow;
    [SerializeField] private Color missZoneColor = Color.red;

    [Header("Audio")]
    [SerializeField] private AudioClip catchSuccessSound;
    [SerializeField] private AudioClip catchFailSound;
    [SerializeField] private AudioClip ballIncomingSound;

    // Core components - cached once
    private PlayerCharacter playerCharacter;
    private PlayerInputHandler inputHandler;
    private AudioSource audioSource;
    private LineRenderer catchRangeIndicator;
    private GameObject catchTriggerObj;
    private SphereCollider catchTriggerCollider;

    // State management
    private bool isCatchingAvailable = true;
    private float lastCatchAttempt = 0f;
    private BallController nearestThrownBall = null;
    private float ballDetectionTime = 0f;

    // Network sync
    private float lastNetworkSync = 0f;
    private const float NETWORK_SYNC_RATE = 10f;

    public enum CatchResult { Perfect, Good, Miss, TooLate }

    void Awake()
    {
        playerCharacter = GetComponent<PlayerCharacter>();
        if (playerCharacter == null)
        {
            Debug.LogError($"CatchSystem requires PlayerCharacter component!");
            enabled = false;
            return;
        }

        SetupComponents();

        // Wait for CharacterScaleManager to initialize
        StartCoroutine(InitializeAfterScaleManager());
    }

    System.Collections.IEnumerator InitializeAfterScaleManager()
    {
        // Wait for CharacterScaleManager to be ready
        while (CharacterScaleManager.Instance == null)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.1f); // Give it time to detect characters

        // Now setup scaled components
        UpdateScaledValues();
        SetupCatchTrigger();
        SetupVisualIndicator();
    }

    void Start()
    {
        // Get input handler through PlayerCharacter
        inputHandler = playerCharacter.GetInputHandler();
        if (inputHandler == null)
        {
            inputHandler = GetComponent<PlayerInputHandler>();
            if (debugMode && inputHandler == null)
            {
                Debug.LogWarning($"CatchSystem: No PlayerInputHandler found on {gameObject.name}");
            }
        }
    }

    /// <summary>
    /// NEW: Update scaled values from CharacterScaleManager
    /// </summary>
    void UpdateScaledValues()
    {
        if (useDynamicScaling && CharacterScaleManager.Instance != null)
        {
            // Scale catch range with character size
            float scaleFactor = CharacterScaleManager.Instance.GetScaleFactor();
            scaledCatchRange = baseCatchRange * scaleFactor;

            // Get character height and calculate center offset
            float characterHeight = CharacterScaleManager.Instance.GetCharacterHeight();
            catchCenterOffset = Vector3.up * (characterHeight * catchHeightRatio);

            if (debugMode)
            {
                Debug.Log($"[CATCH SCALE] Updated catch system:");
                Debug.Log($"  Scale Factor: {scaleFactor:F2}");
                Debug.Log($"  Catch Range: {baseCatchRange:F2} → {scaledCatchRange:F2}");
                Debug.Log($"  Character Height: {characterHeight:F2}");
                Debug.Log($"  Catch Center Offset: {catchCenterOffset}");
            }
        }
        else
        {
            // Fallback to base values
            scaledCatchRange = baseCatchRange;
            catchCenterOffset = Vector3.up * 2.5f; // Approximate center for 2.5x scaled character
        }
    }

    void SetupComponents()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;
    }

    void SetupCatchTrigger()
    {
        // FIXED: Position catch trigger at character center, not ground level
        catchTriggerObj = new GameObject("CatchTrigger");
        catchTriggerObj.transform.SetParent(transform);
        catchTriggerObj.transform.localPosition = catchCenterOffset; // NEW: Offset to center

        catchTriggerCollider = catchTriggerObj.AddComponent<SphereCollider>();
        catchTriggerCollider.isTrigger = true;
        catchTriggerCollider.radius = scaledCatchRange; // NEW: Use scaled range

        var handler = catchTriggerObj.AddComponent<CatchTriggerHandler>();
        handler.Initialize(this);

        if (debugMode)
        {
            Debug.Log($"[CATCH TRIGGER] Created at offset {catchCenterOffset} with radius {scaledCatchRange:F2}");
        }
    }

    void SetupVisualIndicator()
    {
        if (catchIndicator == null)
        {
            catchIndicator = new GameObject("CatchIndicator");
            catchIndicator.transform.SetParent(transform);
            catchIndicator.transform.localPosition = catchCenterOffset; // NEW: Position at center

            catchRangeIndicator = catchIndicator.AddComponent<LineRenderer>();
            catchRangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
            catchRangeIndicator.startColor = goodZoneColor;
            catchRangeIndicator.endColor = goodZoneColor;
            catchRangeIndicator.startWidth = 0.1f;
            catchRangeIndicator.endWidth = 0.1f;
            catchRangeIndicator.useWorldSpace = false;

            CreateCircle(catchRangeIndicator, scaledCatchRange, 24); // NEW: Use scaled range
        }

        catchIndicator.SetActive(false);
    }

    void CreateCircle(LineRenderer lr, float radius, int segments)
    {
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, 0, z)); // Y=0 because indicator is already offset
        }
    }

    void Update()
    {
        bool isLocalPlayer = PhotonNetwork.OfflineMode || (photonView?.IsMine != false);

        if (isLocalPlayer)
        {
            CheckForThrownBalls();
            HandleCatchInput();
            UpdateCatchCooldown();

            if (Time.time - lastNetworkSync >= 1f / NETWORK_SYNC_RATE)
            {
                SendNetworkUpdate();
                lastNetworkSync = Time.time;
            }
        }

        UpdateVisualIndicator();

        // NEW: Periodically update scaling (in case character size changes)
        if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
        {
            UpdateScaledValues();
            if (catchTriggerCollider != null)
            {
                catchTriggerCollider.radius = scaledCatchRange;
            }
            if (catchTriggerObj != null)
            {
                catchTriggerObj.transform.localPosition = catchCenterOffset;
            }
            if (catchIndicator != null)
            {
                catchIndicator.transform.localPosition = catchCenterOffset;
            }
        }
    }

    void CheckForThrownBalls()
    {
        var previousBall = nearestThrownBall;
        nearestThrownBall = null;
        float closestDistance = scaledCatchRange;

        // NEW: Calculate catch center in world space
        Vector3 catchCenter = transform.position + catchCenterOffset;

        var allBalls = FindObjectsOfType<BallController>();
        foreach (var ball in allBalls)
        {
            if (ball.GetBallState() != BallController.BallState.Thrown) continue;

            // NEW: Check distance from catch center, not character feet
            float distance = Vector3.Distance(catchCenter, ball.transform.position);
            if (distance <= scaledCatchRange && distance < closestDistance)
            {
                nearestThrownBall = ball;
                closestDistance = distance;
            }
        }

        if (nearestThrownBall != null && nearestThrownBall != previousBall)
        {
            ballDetectionTime = Time.time;
            PlaySound(ballIncomingSound);
        }
    }

    void HandleCatchInput()
    {
        if (inputHandler?.GetCatchPressed() == true && isCatchingAvailable)
        {
            AttemptCatch();
        }
    }

    void UpdateCatchCooldown()
    {
        if (!isCatchingAvailable && Time.time - lastCatchAttempt >= catchCooldown)
        {
            isCatchingAvailable = true;
        }
    }

    void SendNetworkUpdate()
    {
        if (!PhotonNetwork.OfflineMode && photonView?.IsMine == true)
        {
            bool ballInRange = nearestThrownBall != null;
            photonView.RPC("SyncCatchState", RpcTarget.Others, isCatchingAvailable, ballInRange);
        }
    }

    [PunRPC]
    void SyncCatchState(bool isAvailable, bool ballInRange)
    {
        isCatchingAvailable = isAvailable;
        if (!ballInRange) nearestThrownBall = null;
    }

    void UpdateVisualIndicator()
    {
        bool shouldShow = nearestThrownBall != null;

        if (catchIndicator.activeSelf != shouldShow)
        {
            catchIndicator.SetActive(shouldShow);
        }

        if (shouldShow && catchRangeIndicator != null)
        {
            float timeSinceBall = Time.time - ballDetectionTime;
            Color indicatorColor = timeSinceBall <= 2f ? goodZoneColor : missZoneColor;

            catchRangeIndicator.startColor = indicatorColor;
            catchRangeIndicator.endColor = indicatorColor;
        }
    }

    void AttemptCatch()
    {
        lastCatchAttempt = Time.time;
        isCatchingAvailable = false;

        // Trigger catch animation
        var animController = playerCharacter.GetComponent<RetroDodgeRumble.Animation.PlayerAnimationController>();
        animController?.TriggerCatch();

        // NEW: Check distance from catch center
        Vector3 catchCenter = transform.position + catchCenterOffset;

        if (nearestThrownBall == null ||
            nearestThrownBall.GetBallState() != BallController.BallState.Thrown ||
            Vector3.Distance(catchCenter, nearestThrownBall.transform.position) > scaledCatchRange)
        {
            OnCatchResult(CatchResult.Miss, null);
            return;
        }

        float timeSinceBall = Time.time - ballDetectionTime;
        CatchResult result = timeSinceBall <= 2f ? CatchResult.Good : CatchResult.TooLate;

        if (result == CatchResult.Good)
        {
            ExecuteSuccessfulCatch(nearestThrownBall);
        }
        else
        {
            OnCatchResult(result, nearestThrownBall);
        }
    }

    void ExecuteSuccessfulCatch(BallController ball)
    {
        if (photonView?.IsMine == true)
        {
            var ballView = ball.GetComponent<PhotonView>();
            if (ballView != null)
            {
                photonView.RPC("SyncCatchSuccess", RpcTarget.All, ballView.ViewID, photonView.ViewID);
            }
        }
    }

    [PunRPC]
    void SyncCatchSuccess(int ballViewID, int catcherViewID)
    {
        var ballView = PhotonView.Find(ballViewID);
        var catcherView = PhotonView.Find(catcherViewID);

        if (ballView?.GetComponent<BallController>() is BallController ball &&
            catcherView?.GetComponent<PlayerCharacter>() is PlayerCharacter catcher)
        {
            ball.OnCaught(catcher);
            catcher.SetHasBall(true);

            var catchSystem = catcher.GetComponent<CatchSystem>();
            if (catchSystem != null) catchSystem.nearestThrownBall = null;

            var catcherCatchSystem = catcher.GetComponent<CatchSystem>();
            catcherCatchSystem?.PlayCatchSuccessEffects();

            catcher.OnSuccessfulCatch();
        }
    }

    void OnCatchResult(CatchResult result, BallController ball)
    {
        if (result != CatchResult.Good)
        {
            PlaySound(catchFailSound);
            ball?.OnCatchFailed();
        }

        if (debugMode)
        {
            Debug.Log($"Catch result: {result}");
        }
    }

    void PlayCatchSuccessEffects()
    {
        PlaySound(catchSuccessSound);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Public API
    public bool IsCatchingAvailable() => isCatchingAvailable;
    public bool IsBallInRange() => nearestThrownBall != null;
    public float GetCatchRange() => scaledCatchRange; // NEW: Return scaled range
    public BallController GetNearestThrownBall() => nearestThrownBall;
    public Vector3 GetCatchCenter() => transform.position + catchCenterOffset; // NEW: Get catch center position

    // Trigger callbacks
    public void OnBallEnterRange(BallController ball) { }
    public void OnBallExitRange(BallController ball) { }

    // NEW: Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!debugMode) return;

        // Draw catch range sphere at center
        Gizmos.color = Color.cyan;
        Vector3 catchCenter = transform.position + catchCenterOffset;
        Gizmos.DrawWireSphere(catchCenter, scaledCatchRange);

        // Draw line from feet to catch center
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, catchCenter);

        // Draw small sphere at catch center
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(catchCenter, 0.2f);
    }
}

/// <summary>
/// Simplified trigger handler
/// </summary>
public class CatchTriggerHandler : MonoBehaviour
{
    private CatchSystem catchSystem;

    public void Initialize(CatchSystem system) => catchSystem = system;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BallController>() is BallController ball)
        {
            catchSystem?.OnBallEnterRange(ball);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BallController>() is BallController ball)
        {
            catchSystem?.OnBallExitRange(ball);
        }
    }
}