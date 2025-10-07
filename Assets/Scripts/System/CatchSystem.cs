using UnityEngine;
using Photon.Pun;

/// <summary>
/// Optimized CatchSystem with streamlined PUN2 networking and PlayerCharacter integration
/// Removed redundant code and fixed compatibility issues
/// </summary>
public class CatchSystem : MonoBehaviourPunCallbacks
{
    [Header("Catch Settings")]
    [SerializeField] private float catchRange = 2.5f;
    [SerializeField] private float catchCooldown = 0.5f;
    [SerializeField] private bool debugMode = false; // Disabled by default for performance

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

    // State management
    private bool isCatchingAvailable = true;
    private float lastCatchAttempt = 0f;
    private BallController nearestThrownBall = null;
    private float ballDetectionTime = 0f;

    // Network sync
    private float lastNetworkSync = 0f;
    private const float NETWORK_SYNC_RATE = 10f; // Reduced for performance

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
        SetupCatchTrigger();
        SetupVisualIndicator();
    }

    void Start()
    {
        // Get input handler through PlayerCharacter (fixed compatibility)
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

    void SetupComponents()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;
    }

    void SetupCatchTrigger()
    {
        var triggerObj = new GameObject("CatchTrigger");
        triggerObj.transform.SetParent(transform);
        triggerObj.transform.localPosition = Vector3.zero;

        var trigger = triggerObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = catchRange;

        var handler = triggerObj.AddComponent<CatchTriggerHandler>();
        handler.Initialize(this);
    }

    void SetupVisualIndicator()
    {
        if (catchIndicator == null)
        {
            catchIndicator = new GameObject("CatchIndicator");
            catchIndicator.transform.SetParent(transform);
            catchIndicator.transform.localPosition = Vector3.zero;

            catchRangeIndicator = catchIndicator.AddComponent<LineRenderer>();
            catchRangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
            catchRangeIndicator.startColor = goodZoneColor;
            catchRangeIndicator.endColor = goodZoneColor;
            catchRangeIndicator.startWidth = 0.1f;
            catchRangeIndicator.endWidth = 0.1f;
            catchRangeIndicator.useWorldSpace = false;

            CreateCircle(catchRangeIndicator, catchRange, 24); // Reduced vertices for performance
        }

        catchIndicator.SetActive(false); // Start hidden
    }

    void CreateCircle(LineRenderer lr, float radius, int segments)
    {
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, 0, z));
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

            // Reduced network sync frequency
            if (Time.time - lastNetworkSync >= 1f / NETWORK_SYNC_RATE)
            {
                SendNetworkUpdate();
                lastNetworkSync = Time.time;
            }
        }

        UpdateVisualIndicator();
    }

    void CheckForThrownBalls()
    {
        var previousBall = nearestThrownBall;
        nearestThrownBall = null;
        float closestDistance = catchRange;

        // Optimized ball search - only check balls in thrown state
        var allBalls = FindObjectsOfType<BallController>();
        foreach (var ball in allBalls)
        {
            if (ball.GetBallState() != BallController.BallState.Thrown) continue;

            float distance = Vector3.Distance(transform.position, ball.transform.position);
            if (distance <= catchRange && distance < closestDistance)
            {
                nearestThrownBall = ball;
                closestDistance = distance;
            }
        }

        // Handle ball detection
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
            // Simplified timing - just good/miss
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

        // ADDED: Trigger catch animation immediately when button is pressed
        var animController = playerCharacter.GetComponent<RetroDodgeRumble.Animation.PlayerAnimationController>();
        animController?.TriggerCatch();

        if (nearestThrownBall == null ||
            nearestThrownBall.GetBallState() != BallController.BallState.Thrown ||
            Vector3.Distance(transform.position, nearestThrownBall.transform.position) > catchRange)
        {
            OnCatchResult(CatchResult.Miss, null);
            return;
        }

        // Simplified timing - much more forgiving
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

            // Clear ball reference on catcher
            var catchSystem = catcher.GetComponent<CatchSystem>();
            if (catchSystem != null) catchSystem.nearestThrownBall = null;

            // Success effects
            var catcherCatchSystem = catcher.GetComponent<CatchSystem>();
            catcherCatchSystem?.PlayCatchSuccessEffects();

            // Add ability charges
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
        // Add visual effects here if needed
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Simplified public API
    public bool IsCatchingAvailable() => isCatchingAvailable;
    public bool IsBallInRange() => nearestThrownBall != null;
    public float GetCatchRange() => catchRange;
    public BallController GetNearestThrownBall() => nearestThrownBall;

    // Trigger callbacks (simplified)
    public void OnBallEnterRange(BallController ball) { /* Handled by CheckForThrownBalls */ }
    public void OnBallExitRange(BallController ball) { /* Handled by CheckForThrownBalls */ }
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