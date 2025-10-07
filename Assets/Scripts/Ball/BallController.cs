using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

/// <summary>
/// Network BallController with CLEANED UP PlayerCharacter-only support
/// FIXED: Multiplayer throwing now works for all players
/// </summary>
public class BallController : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Ball Settings")]
    [SerializeField] private float baseSpeed = 25f;
    [SerializeField] private float gravity = 15f;
    [SerializeField] private float pickupRange = 1.2f;
    [SerializeField] private float bounceMultiplier = 0.6f;
    private bool lastOwnershipState = false;

    [Header("2.5D Game Settings")]
    [SerializeField] private bool lock2DMovement = true;
    [SerializeField] private float fixedZPosition = 0f; // The Z position to lock the ball to
    [SerializeField] private bool compensateHandZOffset = true;
    [SerializeField] private bool lockZOnThrow = true;
    [SerializeField] private bool lockZWhenFree = true;

    [Header("Ball Hold Timer")]
    [SerializeField] private float maxHoldTime = 5f;
    [SerializeField] private float warningStartTime = 3f;
    [SerializeField] private float dangerStartTime = 4f;
    [SerializeField] private float holdPenaltyDamage = 10f;
    [SerializeField] private bool resetBallOnPenalty = true;

    [Header("Hold Timer Effects")]
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;
    [SerializeField] private Color corruptionColor = Color.magenta;
    [SerializeField] private AudioClip warningSound;
    [SerializeField] private AudioClip dangerSound;
    [SerializeField] private AudioClip corruptionSound;

    [Header("Ball Position - UPDATED")]
    [SerializeField] private bool useHandBoneAttachment = true;
    [SerializeField] private string rightHandBoneName = "mixamorig:RightHand";
    [SerializeField] private Vector3 handBoneOffset = new Vector3(0f, 0f, 0f); // Fine-tune if needed
    [SerializeField] private float handFollowSpeed = 20f; // Faster follow for hand attachment
    private Transform rightHandBone; // Cache the bone transform

    [Header("Arena Bounds")]
    [SerializeField] private float arenaLeftBound = -15f;
    [SerializeField] private float arenaRightBound = 15f;
    [SerializeField] private float arenaTopBound = 10f;
    [SerializeField] private float arenaBottomBound = -10f;
    [SerializeField] private float wallBounceMultiplier = 0.75f;
    [SerializeField] private int maxWallBounces = 3;

    [Header("Visual Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color heldColor = Color.yellow;

    [Header("Network Settings")]
    [SerializeField] private float positionLerpRate = 15f;
    [SerializeField] private float velocityLerpRate = 10f;

    private float lastOwnershipCheck = 0f;
    private bool wasOwned = false;
    private float lastRetryLog = 0f;

    // Ball state
    public enum BallState { Free, Held, Thrown }

    [SerializeField] private BallState currentState = BallState.Free;
    private ThrowType currentThrowType = ThrowType.Normal;
    private int currentDamage = 10;
    private float currentThrowSpeed = 18f;

    // Network sync variables
    private Vector3 networkPosition;
    private Vector3 networkVelocity;
    private BallState networkBallState;
    private int networkHolderID = -1;
    private int networkThrowerID = -1;
    private bool hasNetworkAuthority = false;
    
    // FIXED: Removed optimization variables - using fixed data structure for reliability

    // Hold timer state
    private float ballHoldStartTime = 0f;
    private bool isShowingWarning = false;
    private bool isInDangerPhase = false;
    private bool hasAppliedPenalty = false;
    private Coroutine ballDropCoroutine;

    // Physics and state
    public Vector3 velocity;
    private bool isGrounded = false;
    private bool homingEnabled = false;
    private int currentWallBounces = 0;
    [SerializeField] private int maxRoofBounces = 1;
    private int usedRoofBounces = 0;

    // References - CLEANED UP: Only PlayerCharacter support
    private Transform ballTransform;
    private Renderer ballRenderer;
    private CollisionDamageSystem collisionSystem;
    private AudioSource audioSource;
    private PlayerCharacter holder;
    private PlayerCharacter thrower;
    private Transform targetOpponent;
    private Color originalBallColor;

    public bool debugMode = false;

    // Ground detection
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float groundCheckDistance = 0.6f;

    void Awake()
    {
        ballTransform = transform;
        ballRenderer = GetComponent<Renderer>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        if (photonView != null)
        {
            photonView.OwnershipTransfer = OwnershipOption.Takeover;
            Debug.Log("Ball PhotonView set to Takeover ownership");

            // NOTE: Do not forcibly change ObservedComponents at runtime.
            // Ensure in prefab that ObservedComponents includes BOTH BallController and PhotonTransformViewClassic.
            // This component writes a fixed 8-item payload; PhotonTransformViewClassic handles separate Transform sync.
        }

        collisionSystem = GetComponent<CollisionDamageSystem>();
        if (collisionSystem == null)
        {
            collisionSystem = gameObject.AddComponent<CollisionDamageSystem>();
        }

        if (GetComponent<Collider>() == null)
        {
            SphereCollider ballCollider = gameObject.AddComponent<SphereCollider>();
            ballCollider.radius = 0.5f;
            ballCollider.isTrigger = false;
        }

        if (ballRenderer != null)
        {
            originalBallColor = ballRenderer.material.color;
        }

        // NEW: Initialize scaling system reference
        if (CharacterScaleManager.Instance == null)
        {
            GameObject scaleManager = new GameObject("CharacterScaleManager");
            scaleManager.AddComponent<CharacterScaleManager>();
        }

        SetBallState(BallState.Free);
    }

    void Update()
    {
        // Offline mode: ignore network ownership entirely and run local logic
        if (PhotonNetwork.OfflineMode)
        {
            HandleBallLogic();
            UpdateVisuals();

            if (lock2DMovement)
            {
                ForceZLock();
            }

            return;
        }

        // Store previous authority state for change detection
        bool previousAuthority = hasNetworkAuthority;

        // FIXED: Network authority is based on PhotonView ownership
        hasNetworkAuthority = photonView.IsMine;

        // Log authority changes
        if (hasNetworkAuthority != previousAuthority)
        {
            Debug.Log($"🔄 AUTHORITY CHANGED: {previousAuthority} → {hasNetworkAuthority}");
            Debug.Log($"  - photonView.IsMine: {photonView.IsMine}");
            Debug.Log($"  - photonView.Owner: {photonView.Owner?.NickName ?? "NULL"}");
            Debug.Log($"  - Local Player: {PhotonNetwork.LocalPlayer?.NickName ?? "NULL"}");
        }

        if (hasNetworkAuthority)
        {
            HandleBallLogic();

            // NEW: Safety check - force Z lock every frame in 2.5D mode
            if (lock2DMovement)
            {
                ForceZLock();
            }
        }
        else
        {
            InterpolateBallPosition();
            ApplyNetworkState();
        }


        // All clients handle visuals
        UpdateVisuals();

        // Check for ownership changes and retry pickup
        CheckForPickupRetry();
    }

    void CheckForPickupRetry()
    {
        bool nowOwned = photonView.IsMine;

        // Log ownership state every 2 seconds when ball is free
        if (Time.time - lastRetryLog > 2f && currentState == BallState.Free)
        {
            Debug.Log($"🔍 Ball Status Check:");
            Debug.Log($"  - Owner: {photonView.Owner?.NickName ?? "NULL"}");
            Debug.Log($"  - IsMine: {photonView.IsMine}");
            Debug.Log($"  - hasNetworkAuthority: {hasNetworkAuthority}");
            Debug.Log($"  - State: {currentState}");
            lastRetryLog = Time.time;
        }

        // If we just gained ownership, check if a local player wants to pick up
        if (nowOwned && !wasOwned && currentState == BallState.Free)
        {
            Debug.Log($"🔄 OWNERSHIP GAINED - checking for local players wanting pickup");

            // Check if any local player is close enough to pick up
            PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
            Debug.Log($"📊 Found {allPlayers.Length} players in scene");

            foreach (PlayerCharacter player in allPlayers)
            {
                PhotonView playerView = player.GetComponent<PhotonView>();
                if (playerView != null)
                {
                    float distance = Vector3.Distance(ballTransform.position, player.transform.position);
                    Debug.Log($"  - {player.name}: IsMine={playerView.IsMine}, HasBall={player.HasBall()}, Distance={distance:F2}");

                    if (playerView.IsMine && !player.HasBall() && distance <= pickupRange)
                    {
                        Debug.Log($"🔄 AUTO-RETRYING pickup for {player.name} after ownership transfer");
                        bool success = TryPickup(player);
                        Debug.Log($"🔄 Auto-retry result: {(success ? "SUCCESS" : "FAILED")}");
                        if (success) break;
                    }
                }
            }
        }

        wasOwned = nowOwned;
    }

    void HandleBallLogic()
    {
        switch (currentState)
        {
            case BallState.Free:
                HandleFreeBall();
                break;
            case BallState.Held:
                HandleHeldBall();
                UpdateBallHoldTimer();
                break;
            case BallState.Thrown:
                HandleThrownBall();
                break;
        }
    }

    void HandleFreeBall()
    {
        if (!isGrounded)
        {
            velocity.y -= gravity * Time.deltaTime;
        }
        else
        {
            if (velocity.y < -1f)
            {
                velocity.y = -velocity.y * bounceMultiplier;
                if (velocity.y < 2f) velocity.y = 0f;
            }
            else
            {
                velocity.y = 0f;
            }

            velocity.x *= 0.95f;
            velocity.z *= 0.95f;
        }

        ballTransform.Translate(velocity * Time.deltaTime, Space.World);

        // NEW: Lock Z position in 2.5D mode when free
        if (lock2DMovement && lockZWhenFree)
        {
            Vector3 pos = ballTransform.position;
            pos.z = fixedZPosition;
            ballTransform.position = pos;
            velocity.z = 0f; // Also zero out Z velocity
        }

        CheckGrounded();
    }

    void HandleHeldBall()
    {
        if (holder != null)
        {
            Vector3 holdPosition;

            // Use bone attachment if enabled and bone is found
            if (useHandBoneAttachment && rightHandBone != null)
            {
                holdPosition = rightHandBone.position + handBoneOffset;

                // NEW: Compensate for hand bone Z offset in 2.5D game
                if (lock2DMovement && compensateHandZOffset)
                {
                    holdPosition.z = fixedZPosition;
                }

                ballTransform.position = Vector3.Lerp(ballTransform.position, holdPosition, handFollowSpeed * Time.deltaTime);
            }
            else
            {
                // Fallback: Use scaled offset from CharacterScaleManager
                if (CharacterScaleManager.Instance != null)
                {
                    holdPosition = holder.transform.position + CharacterScaleManager.Instance.GetHoldOffset();
                }
                else
                {
                    holdPosition = holder.transform.position + new Vector3(0.5f, 1.5f, 0f);
                }

                // NEW: Lock Z position in 2.5D mode
                if (lock2DMovement)
                {
                    holdPosition.z = fixedZPosition;
                }

                ballTransform.position = Vector3.Lerp(ballTransform.position, holdPosition, 10f * Time.deltaTime);
            }

            velocity = Vector3.zero;
        }
        else
        {
            SetBallState(BallState.Free);
        }
    }

    // ADD this new method to find and cache the right hand bone:
    Transform FindRightHandBone(PlayerCharacter character)
    {
        if (character == null) return null;

        // Search for the right hand bone in character's hierarchy
        Transform[] allTransforms = character.GetComponentsInChildren<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t.name == rightHandBoneName)
            {
                if (debugMode)
                {
                    Debug.Log($"Found right hand bone: {t.name} on {character.name}");
                }
                return t;
            }
        }

        // Fallback: try common variations
        string[] commonHandNames = new string[]
        {
        "mixamorig:RightHand",
        "RightHand",
        "Right Hand",
        "R_Hand",
        "Bip01_R_Hand"
        };

        foreach (string handName in commonHandNames)
        {
            foreach (Transform t in allTransforms)
            {
                if (t.name.Contains(handName))
                {
                    if (debugMode)
                    {
                        Debug.Log($"Found right hand bone (variant): {t.name} on {character.name}");
                    }
                    return t;
                }
            }
        }

        Debug.LogWarning($"Could not find right hand bone on {character.name}. Searched for: {rightHandBoneName}");
        return null;
    }

    void HandleThrownBall()
    {
        if (homingEnabled && targetOpponent != null)
        {
            Vector3 directionToTarget = (targetOpponent.position - ballTransform.position).normalized;

            // NEW: Lock Z direction in 2.5D mode
            if (lock2DMovement)
            {
                directionToTarget.z = 0f;
                directionToTarget.Normalize();
            }

            velocity = Vector3.Slerp(velocity, directionToTarget * velocity.magnitude, 2f * Time.deltaTime);
        }

        CheckWallCollisions();
        ballTransform.Translate(velocity * Time.deltaTime, Space.World);

        // NEW: Lock Z position in 2.5D mode during throw
        if (lock2DMovement && lockZOnThrow)
        {
            Vector3 pos = ballTransform.position;
            pos.z = fixedZPosition;
            ballTransform.position = pos;
            velocity.z = 0f; // Also zero out Z velocity
        }

        CheckGrounded();

        if (isGrounded && velocity.y <= 0)
        {
            velocity.y = -velocity.y * bounceMultiplier;
            if (velocity.magnitude < 5f)
            {
                SetBallState(BallState.Free);
            }
        }

        CheckArenaBounds();
    }

    void CheckArenaBounds()
    {
        Vector3 ballPos = ballTransform.position;
        
        // Check if ball is completely outside arena bounds
        bool outOfBounds = ballPos.x < arenaLeftBound - 1f || 
                          ballPos.x > arenaRightBound + 1f ||
                          ballPos.y < arenaBottomBound - 1f ||
                          ballPos.y > arenaTopBound + 3f; // More generous top margin

        if (outOfBounds)
        {
            Debug.Log($"[BALL BOUNDS] Ball out of arena at {ballPos} - resetting to center");
            Debug.Log($"[BALL BOUNDS] X: {ballPos.x} (bounds: {arenaLeftBound} to {arenaRightBound})");
            Debug.Log($"[BALL BOUNDS] Y: {ballPos.y} (bounds: {arenaBottomBound} to {arenaTopBound})");
            
            // Reset ball to center of arena
            if (PhotonNetwork.OfflineMode || hasNetworkAuthority)
            {
                ResetBall();
            }
            else if (!PhotonNetwork.OfflineMode)
            {
                // Online mode: request reset from master client
                photonView.RPC("RequestBallReset", RpcTarget.MasterClient);
            }
        }
    }

    [PunRPC]
    void RequestBallReset()
    {
        // Master client handles ball reset requests
        if (PhotonNetwork.IsMasterClient && hasNetworkAuthority)
        {
            Debug.Log("[BALL RESET] Master client received reset request");
            ResetBall();
        }
    }

    void CheckWallCollisions()
    {
        if (velocity.magnitude < 0.1f) return;

        Vector3 currentPos = ballTransform.position;
        Vector3 nextPos = currentPos + velocity * Time.deltaTime;
        bool hitWall = false;

        // Check left/right walls
        if ((currentPos.x > arenaLeftBound && nextPos.x <= arenaLeftBound) ||
            (currentPos.x < arenaRightBound && nextPos.x >= arenaRightBound))
        {
            velocity.x = -velocity.x * wallBounceMultiplier;
            hitWall = true;
        }

        // Check top/bottom walls
        if ((currentPos.y < arenaTopBound && nextPos.y >= arenaTopBound) ||
            (currentPos.y > arenaBottomBound && nextPos.y <= arenaBottomBound))
        {
            if (currentPos.y < arenaTopBound && nextPos.y >= arenaTopBound)
            {
                // Roof hit - check if we can still bounce
                if (usedRoofBounces < maxRoofBounces)
                {
                    velocity.y = -velocity.y * wallBounceMultiplier;
                    usedRoofBounces++;
                    hitWall = true;
                }
                else
                {
                    // No more roof bounces - clamp to roof and dampen horizontal movement
                    velocity.y = 0f;
                    velocity.x *= 0.7f; // Dampen horizontal movement
                    Vector3 clamped = ballTransform.position;
                    clamped.y = arenaTopBound - 0.1f;
                    ballTransform.position = clamped;
                    hitWall = true;
                }
            }
            else
            {
                // Bottom wall hit
                velocity.y = -velocity.y * wallBounceMultiplier;
                hitWall = true;
            }
        }

        // Count wall bounces
        if (hitWall)
        {
            currentWallBounces++;
        }

        // Stop bouncing if we've hit too many walls or ball is too slow
        if (currentWallBounces >= maxWallBounces || velocity.magnitude < 3f)
        {
            SetBallState(BallState.Free);
            currentWallBounces = 0;
            usedRoofBounces = 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // BALL HOLD TIMER SYSTEM
    // ═══════════════════════════════════════════════════════════════

    void UpdateBallHoldTimer()
    {
        if (currentState != BallState.Held || ballHoldStartTime == 0f) return;

        float holdDuration = Time.time - ballHoldStartTime;

        if (holdDuration >= warningStartTime && !isShowingWarning && !isInDangerPhase && !hasAppliedPenalty)
        {
            StartWarningPhase();
        }

        if (holdDuration >= dangerStartTime && !isInDangerPhase && !hasAppliedPenalty)
        {
            StartDangerPhase();
        }

        if (holdDuration >= maxHoldTime && !hasAppliedPenalty)
        {
            StartPenaltyPhase();
        }

        UpdateHoldTimerVisuals(holdDuration);
    }

    void StartWarningPhase()
    {
        isShowingWarning = true;
        PlayHoldTimerSound(warningSound);

        if (ballRenderer != null)
            ballRenderer.material.color = warningColor;

        BallHoldTimerUI.Instance?.ShowWarning(this);
    }

    void StartDangerPhase()
    {
        isInDangerPhase = true;
        PlayHoldTimerSound(dangerSound);

        if (ballRenderer != null)
            ballRenderer.material.color = dangerColor;

        BallHoldTimerUI.Instance?.ShowDanger(this);

        CameraShakeManager.Instance.TriggerShake(0.5f, 0.3f, "BallDanger");
    }

    void StartPenaltyPhase()
    {
        hasAppliedPenalty = true;
        PlayHoldTimerSound(corruptionSound);

        if (ballRenderer != null)
            ballRenderer.material.color = corruptionColor;

        ApplyOneTimePenalty();
        BallHoldTimerUI.Instance?.ShowPenalty(this);

        CameraShakeManager.Instance.TriggerShake(1.5f, 0.8f, "BallPenalty");

        if (ballDropCoroutine != null)
            StopCoroutine(ballDropCoroutine);
        ballDropCoroutine = StartCoroutine(DropBallAfterDelay());
    }

    void ApplyOneTimePenalty()
    {
        if (holder != null)
        {
            PlayerHealth holderHealth = holder.GetComponent<PlayerHealth>();
            if (holderHealth != null)
            {
                Debug.Log($"Attempting damage on {holder.name}. PhotonView.IsMine: {holderHealth.photonView.IsMine}, IsDead: {holderHealth.IsDead()}, IsInvulnerable: {holderHealth.IsInvulnerable()}");

                int damage = Mathf.RoundToInt(holdPenaltyDamage);
                holderHealth.TakeDamage(damage, null);

                Debug.Log($"Health after damage attempt: {holderHealth.GetCurrentHealth()}");
            }
        }
    }

    IEnumerator DropBallAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (resetBallOnPenalty)
        {
            ResetBall();
        }
        else
        {
            DropBall();
        }
    }

    void DropBall()
    {
        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }

        velocity = new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 4f), Random.Range(-3f, 3f));
        SetBallState(BallState.Free);
    }

    void UpdateHoldTimerVisuals(float holdDuration)
    {
        if (ballRenderer == null) return;

        if (isShowingWarning && !isInDangerPhase)
        {
            float pulse = Mathf.Sin(Time.time * 4f) * 0.3f + 0.7f;
            ballRenderer.material.color = Color.Lerp(heldColor, warningColor, pulse);
        }
        else if (isInDangerPhase && !hasAppliedPenalty)
        {
            float pulse = Mathf.Sin(Time.time * 8f) * 0.4f + 0.6f;
            ballRenderer.material.color = Color.Lerp(warningColor, dangerColor, pulse);
        }
        else if (hasAppliedPenalty)
        {
            float pulse = Mathf.Sin(Time.time * 15f) * 0.6f + 0.4f;
            ballRenderer.material.color = Color.Lerp(dangerColor, corruptionColor, pulse);
        }
    }

    void ResetHoldTimer()
    {
        isShowingWarning = false;
        isInDangerPhase = false;
        hasAppliedPenalty = false;

        if (ballDropCoroutine != null)
        {
            StopCoroutine(ballDropCoroutine);
            ballDropCoroutine = null;
        }

        if (ballRenderer != null)
            ballRenderer.material.color = originalBallColor;

        BallHoldTimerUI.Instance?.HideTimer();
    }

    void StopHoldTimer()
    {
        ballHoldStartTime = 0f;
        ResetHoldTimer();
    }

    // ═══════════════════════════════════════════════════════════════
    // NETWORK SYNCHRONIZATION
    // ═══════════════════════════════════════════════════════════════

    void InterpolateBallPosition()
    {
        if (hasNetworkAuthority) return;

        float distance = Vector3.Distance(ballTransform.position, networkPosition);

        if (distance > 10f)
        {
            // Snap to network position if too far
            Vector3 snapPos = networkPosition;

            // NEW: Lock Z in 2.5D mode
            if (lock2DMovement)
            {
                snapPos.z = fixedZPosition;
            }

            ballTransform.position = snapPos;
            velocity = networkVelocity;

            // NEW: Lock Z velocity in 2.5D mode
            if (lock2DMovement)
            {
                velocity.z = 0f;
            }
        }
        else if (distance > 0.05f)
        {
            float adaptivePositionRate = positionLerpRate;
            float adaptiveVelocityRate = velocityLerpRate;

            if (networkVelocity.magnitude > 15f)
            {
                adaptivePositionRate *= 1.5f;
                adaptiveVelocityRate *= 1.2f;
            }

            float posT = Mathf.Clamp01(adaptivePositionRate * Time.deltaTime);
            Vector3 targetPos = networkPosition;

            // NEW: Lock Z in 2.5D mode
            if (lock2DMovement)
            {
                targetPos.z = fixedZPosition;
            }

            ballTransform.position = Vector3.Lerp(ballTransform.position, targetPos, posT);

            float velT = Mathf.Clamp01(adaptiveVelocityRate * Time.deltaTime);
            velocity = Vector3.Lerp(velocity, networkVelocity, velT);

            // NEW: Lock Z velocity in 2.5D mode
            if (lock2DMovement)
            {
                velocity.z = 0f;
            }
        }
    }

    void ApplyNetworkState()
    {
        if (hasNetworkAuthority) return;

        if (networkBallState != currentState)
        {
            SetBallState(networkBallState);
        }

        if (networkHolderID != -1)
        {
            UpdateHolderFromNetwork(networkHolderID);
        }

        if (networkThrowerID != -1)
        {
            UpdateThrowerFromNetwork(networkThrowerID);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (PhotonNetwork.OfflineMode) return;
        if (stream.IsWriting)
        {
            // FIXED: Always send the same number of data items
            stream.SendNext(ballTransform.position);
            stream.SendNext(velocity);
            stream.SendNext((int)currentState);
            stream.SendNext(GetHolderID());
            stream.SendNext(GetThrowerID());
            stream.SendNext(currentDamage);
            stream.SendNext((int)currentThrowType);
            stream.SendNext(Time.time);
        }
        else if (stream.IsReading)
        {
            // FIXED: Always read the same number of data items and use type-tolerant conversions
            object o1 = null, o2 = null, o3 = null, o4 = null, o5 = null, o6 = null, o7 = null, o8 = null;
            try
            {
                o1 = stream.ReceiveNext();
                o2 = stream.ReceiveNext();
                o3 = stream.ReceiveNext();
                o4 = stream.ReceiveNext();
                o5 = stream.ReceiveNext();
                o6 = stream.ReceiveNext();
                o7 = stream.ReceiveNext();
                o8 = stream.ReceiveNext();

                // Safe conversions
                if (o1 is Vector3 pos) networkPosition = pos; else networkPosition = Vector3.zero;
                if (o2 is Vector3 vel) networkVelocity = vel; else networkVelocity = Vector3.zero;

                int stateInt = o3 is int i3 ? i3 : System.Convert.ToInt32(o3);
                networkBallState = (BallState)stateInt;

                networkHolderID = o4 is int i4 ? i4 : System.Convert.ToInt32(o4);
                networkThrowerID = o5 is int i5 ? i5 : System.Convert.ToInt32(o5);
                currentDamage = o6 is int i6 ? i6 : System.Convert.ToInt32(o6);

                int throwTypeInt = o7 is int i7 ? i7 : System.Convert.ToInt32(o7);
                currentThrowType = (ThrowType)throwTypeInt;

                float timestamp = o8 is float f8 ? f8 : (o8 is double d8 ? (float)d8 : System.Convert.ToSingle(o8));

                // FIXED: Improved lag compensation
                float lag = Mathf.Abs((float)(PhotonNetwork.Time - timestamp));
                if (networkVelocity.magnitude > 0.1f && lag > 0.01f && lag < 0.5f) // Reasonable lag range
                {
                    networkPosition += networkVelocity * lag * 0.5f; // Reduced compensation factor
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BALL SYNC] Error deserializing ball data: {e.Message}");
            }
        }
    }

    // FIXED: Simplified RPC system - removed redundant SyncBallEvent
    // Ball state changes are now handled through OnPhotonSerializeView for better performance

    // FIXED: Removed redundant network event handlers
    // All ball state changes are now handled through OnPhotonSerializeView for better performance

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC INTERFACE METHODS - CLEANED UP
    // ═══════════════════════════════════════════════════════════════

    public bool TryPickup(PlayerCharacter character)
    {
        Debug.Log($"⚽ === BALLCONTROLLER PICKUP START === {character.name}");
        Debug.Log($"[PICKUP DEBUG] state:{currentState} hasAuthority:{hasNetworkAuthority} offline:{PhotonNetwork.OfflineMode}");

        if (currentState != BallState.Free)
        {
            Debug.LogWarning($"❌ Ball not free - current state: {currentState}");
            return false;
        }

        // NEW: Use CharacterScaleManager for distance check
        float pickupRange = this.pickupRange;
        if (CharacterScaleManager.Instance != null)
        {
            // Scale pickup range with character size
            pickupRange *= CharacterScaleManager.Instance.GetScaleFactor();
        }

        float distance = Vector3.Distance(ballTransform.position, character.transform.position);
        Debug.Log($"📏 BallController distance: {distance:F2} (range: {pickupRange})");

        if (distance <= pickupRange)
        {
            PhotonView characterView = character.GetComponent<PhotonView>();

            // ... existing network authority checks ...

            if (PhotonNetwork.OfflineMode)
            {
                holder = character;

                // NEW: Find and cache right hand bone
                rightHandBone = FindRightHandBone(character);

                SetBallState(BallState.Held);
                character.SetHasBall(true);
                ballHoldStartTime = Time.time;
                ResetHoldTimer();
                Debug.Log($"🎉 Offline pickup successful for {character.name}");
                return true;
            }
            else if (characterView != null && characterView.IsMine)
            {
                if (hasNetworkAuthority)
                {
                    holder = character;

                    // NEW: Find and cache right hand bone
                    rightHandBone = FindRightHandBone(character);

                    SetBallState(BallState.Held);
                    character.SetHasBall(true);
                    ballHoldStartTime = Time.time;
                    ResetHoldTimer();
                    Debug.Log($"🎉 {character.name} successfully picked up ball!");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"❌ BallController: No network authority yet");
                    return false;
                }
            }
            else if (hasNetworkAuthority && (characterView == null || !characterView.IsMine))
            {
                holder = character;

                // NEW: Find and cache right hand bone
                rightHandBone = FindRightHandBone(character);

                SetBallState(BallState.Held);
                character.SetHasBall(true);
                ballHoldStartTime = Time.time;
                ResetHoldTimer();
                Debug.Log($"🎉 Remote pickup successful for {character.name}");
                return true;
            }
        }

        Debug.Log($"⚽ === BALLCONTROLLER PICKUP END === FAILED");
        return false;
    }

    public void OnCaught(PlayerCharacter catcher)
    {
        if (catcher == null) return;

        holder = catcher;
        SetBallState(BallState.Held);
        catcher.SetHasBall(true);
        velocity = Vector3.zero;
        thrower = null;
        targetOpponent = null;
        homingEnabled = false;
        ballHoldStartTime = Time.time;
        ResetHoldTimer();
    }

    // FIXED: Improved ThrowBall method for multiplayer
    public void ThrowBall(Vector3 direction, float power)
    {
        if (currentState != BallState.Held) return;
        ThrowBallInternal(direction, power);
    }

    // FIXED: Internal throw method that bypasses state check for ultimate abilities
    public void ThrowBallInternal(Vector3 direction, float power)
    {
        Debug.Log($"🚀 === BALL THROW INTERNAL START ===");
        Debug.Log($"  - Current State: {currentState}");
        Debug.Log($"  - Direction: {direction}");
        Debug.Log($"  - Power: {power}");

        StopHoldTimer();

        // Clear hand bone reference when throwing
        rightHandBone = null;

        if (thrower == null && holder != null)
        {
            thrower = holder;
        }

        if (thrower == null)
        {
            var players = FindObjectsOfType<PlayerCharacter>();
            foreach (var player in players)
            {
                if (PhotonNetwork.OfflineMode)
                {
                    if (player.HasBall())
                    {
                        thrower = player;
                        break;
                    }
                }
                else if (player.GetComponent<PhotonView>()?.IsMine == true)
                {
                    thrower = player;
                    break;
                }
            }
        }

        if (collisionSystem != null && thrower != null)
        {
            collisionSystem.OnBallThrown(thrower);
        }

        targetOpponent = FindOpponentForThrower();
        Vector3 throwDirection = direction;

        if (targetOpponent != null)
        {
            Vector3 targetPos;
            if (CharacterScaleManager.Instance != null)
            {
                targetPos = CharacterScaleManager.Instance.GetCharacterTargetPosition(targetOpponent);
                throwDirection = CharacterScaleManager.Instance.CalculateArcDirection(ballTransform.position, targetPos);
            }
            else
            {
                targetPos = targetOpponent.position + Vector3.up * 2.5f;
                throwDirection = (targetPos - ballTransform.position).normalized;
                throwDirection.y += 0.2f;
                throwDirection.Normalize();
            }

            Debug.Log($"🎯 Targeting: {targetOpponent.name} at {targetPos}");
        }

        // NEW: Lock Z direction in 2.5D mode
        if (lock2DMovement)
        {
            throwDirection.z = 0f;
            throwDirection.Normalize();
            Debug.Log($"🔒 Z-axis locked for 2.5D gameplay");
        }

        velocity = throwDirection * currentThrowSpeed * power;

        // NEW: Ensure Z velocity is zero in 2.5D mode
        if (lock2DMovement)
        {
            velocity.z = 0f;
        }

        SetBallState(BallState.Thrown);

        Debug.Log($"🚀 === BALL THROW COMPLETE ===");
        Debug.Log($"  - New State: {currentState}");
        Debug.Log($"  - Velocity: {velocity}");
        Debug.Log($"  - Speed: {currentThrowSpeed}");
    }

    public void ResetBall()
    {
        Vector3 spawnPosition = new Vector3(0, 2f, fixedZPosition); // NEW: Use fixedZPosition
        ballTransform.position = spawnPosition;
        velocity = Vector3.zero;
        currentWallBounces = 0;
        usedRoofBounces = 0;

        StopHoldTimer();

        rightHandBone = null;

        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }

        thrower = null;
        targetOpponent = null;
        homingEnabled = false;

        currentThrowType = ThrowType.Normal;
        currentDamage = 10;
        currentThrowSpeed = 18f;

        if (collisionSystem != null)
        {
            collisionSystem.OnBallReset();
        }

        SetBallState(BallState.Free);
    }

    public void SetBallState(BallState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case BallState.Free:
                thrower = null;
                targetOpponent = null;
                homingEnabled = false;
                StopHoldTimer();
                if (collisionSystem != null)
                    collisionSystem.OnBallReset();
                break;
            case BallState.Held:
                velocity = Vector3.zero;
                break;
            case BallState.Thrown:
                StopHoldTimer();
                break;
        }
    }

    public void SetThrowData(ThrowType throwType, int damage, float throwSpeed)
    {
        currentThrowType = throwType;
        currentDamage = damage;
        currentThrowSpeed = throwSpeed;
    }

    public void SetThrower(PlayerCharacter newThrower)
    {
        thrower = newThrower;
    }

    public void EnableHoming(bool enable)
    {
        homingEnabled = enable;
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ═══════════════════════════════════════════════════════════════

    int GetHolderID()
    {
        if (holder != null)
        {
            PhotonView holderView = holder.GetComponent<PhotonView>();
            return holderView != null ? holderView.ViewID : -1;
        }
        return -1;
    }

    int GetThrowerID()
    {
        if (thrower != null)
        {
            PhotonView throwerView = thrower.GetComponent<PhotonView>();
            return throwerView != null ? throwerView.ViewID : -1;
        }
        return -1;
    }

    PlayerCharacter FindPlayerByID(int viewID)
    {
        if (viewID == -1) return null;
        PhotonView targetView = PhotonView.Find(viewID);
        return targetView?.GetComponent<PlayerCharacter>();
    }

    void UpdateHolderFromNetwork(int viewID)
    {
        PlayerCharacter player = FindPlayerByID(viewID);
        if (player != holder)
        {
            if (holder != null)
                holder.SetHasBall(false);

            holder = player;
            if (holder != null)
                holder.SetHasBall(true);
        }
    }

    void UpdateThrowerFromNetwork(int viewID)
    {
        PlayerCharacter player = FindPlayerByID(viewID);
        if (player != thrower)
        {
            thrower = player;
        }
    }

    Transform FindOpponentForThrower()
    {
        if (thrower == null) return null;

        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player != thrower)
                return player.transform;
        }
        return null;
    }

    Vector3 CalculateLeadTarget(Transform target)
    {
        if (target == null) return Vector3.zero;

        Vector3 targetPos;
        if (CharacterScaleManager.Instance != null)
        {
            targetPos = CharacterScaleManager.Instance.GetCharacterTargetPosition(target);
        }
        else
        {
            targetPos = target.position + Vector3.up * 2.5f;
        }

        // NEW: Lock Z in 2.5D mode
        if (lock2DMovement)
        {
            targetPos.z = fixedZPosition;
        }

        Vector3 targetVelocity = Vector3.zero;

        PlayerCharacter targetPlayer = target.GetComponent<PlayerCharacter>();
        if (targetPlayer != null)
        {
            var inputHandler = targetPlayer.GetInputHandler();
            if (inputHandler != null)
            {
                float horizontalInput = inputHandler.GetHorizontal();
                if (Mathf.Abs(horizontalInput) > 0.1f)
                {
                    CharacterData charData = targetPlayer.GetCharacterData();
                    float moveSpeed = charData != null ? charData.moveSpeed : 5f;
                    targetVelocity.x = horizontalInput * moveSpeed;
                }
            }
        }

        float ballSpeed = currentThrowSpeed;
        float timeToTarget = Vector3.Distance(ballTransform.position, targetPos) / ballSpeed;

        Vector3 predictedPos = targetPos + (targetVelocity * timeToTarget);

        // NEW: Lock Z in 2.5D mode
        if (lock2DMovement)
        {
            predictedPos.z = fixedZPosition;
        }

        if (thrower != null)
        {
            var aiBrain = thrower.GetComponent<RetroDodge.AIControllerBrain>();
            if (aiBrain != null)
            {
                float inaccuracy = UnityEngine.Random.Range(-1f, 1f) * 2f;
                predictedPos.x += inaccuracy;
            }
        }

        return predictedPos;
    }

    public void ForceZLock()
    {
        if (!lock2DMovement) return;

        Vector3 pos = ballTransform.position;
        pos.z = fixedZPosition;
        ballTransform.position = pos;
        velocity.z = 0f;
    }

    void CheckGrounded()
    {
        Vector3 rayStart = ballTransform.position;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayer);
    }

    void UpdateVisuals()
    {
        if (currentState == BallState.Thrown || velocity.magnitude > 0.1f)
        {
            ballTransform.Rotate(Vector3.right * rotationSpeed * Time.deltaTime, Space.Self);
        }

        if (ballRenderer != null && !isShowingWarning && !isInDangerPhase && !hasAppliedPenalty)
        {
            Color ballColor = currentState switch
            {
                BallState.Free => availableColor,
                BallState.Held => heldColor,
                BallState.Thrown => currentThrowType == ThrowType.Ultimate ? Color.red : Color.red,
                _ => availableColor
            };
            ballRenderer.material.color = ballColor;
        }
    }

    void PlayHoldTimerSound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // VFX INTEGRATION (SIMPLIFIED)
    // ═══════════════════════════════════════════════════════════════

    public void ApplyUltimateBallVFX()
    {
        // Placeholder for ultimate ball VFX
        Debug.Log("Ultimate ball VFX applied");
    }

    public void RemoveUltimateBallVFX()
    {
        // Placeholder for removing ultimate ball VFX
        Debug.Log("Ultimate ball VFX removed");
    }

    public void OnCatchFailed()
    {
        velocity *= 0.8f;
        velocity.x += Random.Range(-2f, 2f);
        velocity.y += Random.Range(1f, 3f);
    }

    public string GetCurrentHoldPhase()
    {
        if (currentState != BallState.Held) return "None";
        if (hasAppliedPenalty) return "Penalty";
        if (isInDangerPhase) return "Danger";
        if (isShowingWarning) return "Warning";
        return "Normal";
    }

    public float GetMaxHoldTime() => maxHoldTime;

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC GETTERS
    // ═══════════════════════════════════════════════════════════════

    public float GetHoldDuration()
    {
        if (currentState != BallState.Held || ballHoldStartTime == 0f) return 0f;
        return Time.time - ballHoldStartTime;
    }

    public float GetHoldProgress()
    {
        if (currentState != BallState.Held || ballHoldStartTime == 0f) return 0f;
        return Mathf.Clamp01(GetHoldDuration() / maxHoldTime);
    }

    public BallState GetBallState() => currentState;
    public bool IsHeld() => currentState == BallState.Held;
    public bool IsFree() => currentState == BallState.Free;
    public Vector3 GetVelocity() => velocity;
    public void SetVelocity(Vector3 newVelocity) => velocity = newVelocity;
    public int GetCurrentDamage() => currentDamage;
    public ThrowType GetThrowType() => currentThrowType;
    public PlayerCharacter GetHolder() => holder;
    public PlayerCharacter GetThrower() => thrower;
    public Transform GetCurrentTarget() => targetOpponent;
    public bool IsInWarningPhase() => isShowingWarning;
    public bool IsInDangerPhase() => isInDangerPhase;
    public bool IsInPenaltyPhase() => hasAppliedPenalty;

    // ═══════════════════════════════════════════════════════════════
    // REMOVED LEGACY SUPPORT METHODS
    // All CharacterController legacy methods have been removed
    // ═══════════════════════════════════════════════════════════════

    void OnGUI()
    {
        /*if (!Application.isPlaying) return;

        // Ball debug panel
        GUI.Box(new Rect(10, 10, 400, 150), "BALL DEBUG INFO");

        float y = 30;
        GUI.Label(new Rect(20, y, 380, 20), $"Ball Owner: {photonView.Owner?.NickName ?? "NULL"}");
        y += 20;
        GUI.Label(new Rect(20, y, 380, 20), $"Ball IsMine: {photonView.IsMine}");
        y += 20;
        GUI.Label(new Rect(20, y, 380, 20), $"hasNetworkAuthority: {hasNetworkAuthority}");
        y += 20;
        GUI.Label(new Rect(20, y, 380, 20), $"Ball State: {currentState}");
        y += 20;
        GUI.Label(new Rect(20, y, 380, 20), $"Ownership Transfer: {photonView.OwnershipTransfer}");
        y += 20;
        GUI.Label(new Rect(20, y, 380, 20), $"Local Player: {PhotonNetwork.LocalPlayer?.NickName ?? "NULL"}");

        // Player debug info
        y = 180;
        GUI.Box(new Rect(10, y, 400, 120), "PLAYERS DEBUG INFO");
        y += 20;

        PlayerCharacter[] players = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in players)
        {
            PhotonView pv = player.GetComponent<PhotonView>();
            float dist = Vector3.Distance(transform.position, player.transform.position);
            GUI.Label(new Rect(20, y, 380, 20),
                $"{player.name}: IsMine={pv?.IsMine}, Dist={dist:F1}, HasBall={player.HasBall()}");
            y += 20;
        }*/
    }
}