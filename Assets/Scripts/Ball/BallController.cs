using UnityEngine;
using System.Collections;
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

    [Header("Ball Position")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0.5f, 1.5f, 0f);
    [SerializeField] private float holdFollowSpeed = 10f;

    [Header("Arena Bounds")]
    [SerializeField] private float arenaLeftBound = -12f;
    [SerializeField] private float arenaRightBound = 12f;
    [SerializeField] private float arenaTopBound = 8f;
    [SerializeField] private float arenaBottomBound = -2f;
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

    // References - CLEANED UP: Only PlayerCharacter support
    private Transform ballTransform;
    private Renderer ballRenderer;
    private CollisionDamageSystem collisionSystem;
    private AudioSource audioSource;
    private PlayerCharacter holder;
    private PlayerCharacter thrower;
    private Transform targetOpponent;
    private Color originalBallColor;

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

        SetBallState(BallState.Free);
    }

    void Update()
    {
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
            // We own this ball - run full ball logic
            HandleBallLogic();
        }
        else
        {
            // We don't own this ball - interpolate to network state
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
        CheckGrounded();
    }

    void HandleHeldBall()
    {
        if (holder != null)
        {
            Vector3 holdPosition = holder.transform.position + holdOffset;
            ballTransform.position = Vector3.Lerp(ballTransform.position, holdPosition, holdFollowSpeed * Time.deltaTime);
            velocity = Vector3.zero;
        }
        else
        {
            SetBallState(BallState.Free);
        }
    }

    void HandleThrownBall()
    {
        if (homingEnabled && targetOpponent != null)
        {
            Vector3 directionToTarget = (targetOpponent.position - ballTransform.position).normalized;
            velocity = Vector3.Slerp(velocity, directionToTarget * velocity.magnitude, 2f * Time.deltaTime);
        }

        CheckWallCollisions();
        ballTransform.Translate(velocity * Time.deltaTime, Space.World);
        CheckGrounded();

        if (isGrounded && velocity.y <= 0)
        {
            velocity.y = -velocity.y * bounceMultiplier;
            if (velocity.magnitude < 5f)
            {
                SetBallState(BallState.Free);
            }
        }

        if (ballTransform.position.y < arenaBottomBound - 3f)
        {
            ResetBall();
        }
    }

    void CheckWallCollisions()
    {
        if (velocity.magnitude < 0.1f || currentWallBounces >= maxWallBounces) return;

        Vector3 currentPos = ballTransform.position;
        Vector3 nextPos = currentPos + velocity * Time.deltaTime;

        if ((currentPos.x > arenaLeftBound && nextPos.x <= arenaLeftBound) ||
            (currentPos.x < arenaRightBound && nextPos.x >= arenaRightBound))
        {
            velocity.x = -velocity.x * wallBounceMultiplier;
            currentWallBounces++;
        }

        if ((currentPos.y < arenaTopBound && nextPos.y >= arenaTopBound) ||
            (currentPos.y > arenaBottomBound && nextPos.y <= arenaBottomBound))
        {
            velocity.y = -velocity.y * wallBounceMultiplier;
            currentWallBounces++;
        }

        if (currentWallBounces >= maxWallBounces || velocity.magnitude < 2f)
        {
            SetBallState(BallState.Free);
            currentWallBounces = 0;
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

        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
            cameraController.ShakeCamera(0.2f, 0.3f);
    }

    void StartPenaltyPhase()
    {
        hasAppliedPenalty = true;
        PlayHoldTimerSound(corruptionSound);

        if (ballRenderer != null)
            ballRenderer.material.color = corruptionColor;

        ApplyOneTimePenalty();
        BallHoldTimerUI.Instance?.ShowPenalty(this);

        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
            cameraController.ShakeCamera(0.8f, 0.8f);

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

        if (distance > 5f)
        {
            ballTransform.position = networkPosition;
            velocity = networkVelocity;
        }
        else if (distance > 0.1f)
        {
            float positionLerp = Time.deltaTime * positionLerpRate;
            ballTransform.position = Vector3.Lerp(ballTransform.position, networkPosition, positionLerp);

            float velocityLerp = Time.deltaTime * velocityLerpRate;
            velocity = Vector3.Lerp(velocity, networkVelocity, velocityLerp);
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
        if (stream.IsWriting && hasNetworkAuthority)
        {
            // Ball owner: Send ball data
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
            // Other clients: Receive ball data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            networkBallState = (BallState)stream.ReceiveNext();
            networkHolderID = (int)stream.ReceiveNext();
            networkThrowerID = (int)stream.ReceiveNext();
            currentDamage = (int)stream.ReceiveNext();
            currentThrowType = (ThrowType)stream.ReceiveNext();
            float timestamp = (float)stream.ReceiveNext();

            // Apply lag compensation
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - timestamp));
            if (networkVelocity.magnitude > 0.1f && lag > 0.02f)
            {
                networkPosition += networkVelocity * lag;
            }
        }
    }

    [PunRPC]
    void SyncBallEvent(string eventType, int playerID, float posX, float posY, float posZ,
                      float velX, float velY, float velZ, int damage, int throwType)
    {
        Vector3 position = new Vector3(posX, posY, posZ);
        Vector3 ballVelocity = new Vector3(velX, velY, velZ);

        switch (eventType)
        {
            case "Pickup":
                HandleNetworkPickup(playerID, position);
                break;
            case "Throw":
                HandleNetworkThrow(playerID, position, ballVelocity, damage, (ThrowType)throwType);
                break;
            case "Catch":
                HandleNetworkCatch(playerID, position);
                break;
            case "Drop":
                HandleNetworkDrop(position, ballVelocity);
                break;
        }
    }

    void HandleNetworkPickup(int playerID, Vector3 position)
    {
        if (hasNetworkAuthority) return;

        PlayerCharacter player = FindPlayerByID(playerID);
        if (player != null)
        {
            holder = player;
            player.SetHasBall(true);
            SetBallState(BallState.Held);
            ballTransform.position = position;
        }
    }

    void HandleNetworkThrow(int playerID, Vector3 position, Vector3 ballVelocity, int damage, ThrowType throwType)
    {
        if (hasNetworkAuthority) return;

        PlayerCharacter player = FindPlayerByID(playerID);
        if (player != null)
        {
            thrower = player;
            player.SetHasBall(false);
            holder = null;

            ballTransform.position = position;
            velocity = ballVelocity;
            currentDamage = damage;
            currentThrowType = throwType;
            SetBallState(BallState.Thrown);
        }
    }

    void HandleNetworkCatch(int playerID, Vector3 position)
    {
        if (hasNetworkAuthority) return;

        PlayerCharacter player = FindPlayerByID(playerID);
        if (player != null)
        {
            holder = player;
            player.SetHasBall(true);
            SetBallState(BallState.Held);
            ballTransform.position = position;
            velocity = Vector3.zero;
        }
    }

    void HandleNetworkDrop(Vector3 position, Vector3 ballVelocity)
    {
        if (hasNetworkAuthority) return;

        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }

        ballTransform.position = position;
        velocity = ballVelocity;
        SetBallState(BallState.Free);
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC INTERFACE METHODS - CLEANED UP
    // ═══════════════════════════════════════════════════════════════

    public bool TryPickup(PlayerCharacter character)
    {
        Debug.Log($"⚽ === BALLCONTROLLER PICKUP START === {character.name}");

        Debug.Log($"🔍 Ball State Check:");
        Debug.Log($"  - Current State: {currentState}");
        Debug.Log($"  - Is Free: {currentState == BallState.Free}");

        if (currentState != BallState.Free)
        {
            Debug.LogWarning($"❌ Ball not free - current state: {currentState}");
            return false;
        }

        float distance = Vector3.Distance(ballTransform.position, character.transform.position);
        Debug.Log($"📏 BallController distance: {distance:F2} (range: {pickupRange})");

        if (distance <= pickupRange)
        {
            PhotonView characterView = character.GetComponent<PhotonView>();

            Debug.Log($"📡 BallController Network Analysis:");
            Debug.Log($"  - Ball hasNetworkAuthority: {hasNetworkAuthority}");
            Debug.Log($"  - Ball photonView.IsMine: {photonView.IsMine}");
            Debug.Log($"  - Ball Owner: {photonView.Owner?.NickName ?? "NULL"}");

            if (characterView != null)
            {
                Debug.Log($"  - Character ViewID: {characterView.ViewID}");
                Debug.Log($"  - Character IsMine: {characterView.IsMine}");
                Debug.Log($"  - Character Owner: {characterView.Owner?.NickName ?? "NULL"}");
            }

            if (characterView != null && characterView.IsMine)
            {
                Debug.Log($"✅ BallController: This is MY character");
                Debug.Log($"🔐 Authority Check: hasNetworkAuthority = {hasNetworkAuthority}");

                if (hasNetworkAuthority)
                {
                    Debug.Log($"✅ BallController: We have network authority - executing pickup");

                    // Execute pickup
                    holder = character;
                    SetBallState(BallState.Held);
                    character.SetHasBall(true);
                    ballHoldStartTime = Time.time;
                    ResetHoldTimer();

                    Debug.Log($"📤 Syncing pickup to other clients...");
                    // Sync pickup to other clients
                    photonView.RPC("SyncBallEvent", RpcTarget.Others, "Pickup",
                                 characterView.ViewID,
                                 ballTransform.position.x, ballTransform.position.y, ballTransform.position.z,
                                 0f, 0f, 0f, 0, 0);

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
                Debug.Log($"🎮 BallController: Authority holder processing remote pickup");

                holder = character;
                SetBallState(BallState.Held);
                character.SetHasBall(true);
                ballHoldStartTime = Time.time;
                ResetHoldTimer();

                Debug.Log($"📤 Syncing remote pickup to all clients...");
                // Sync to all clients
                photonView.RPC("SyncBallEvent", RpcTarget.All, "Pickup",
                             characterView?.ViewID ?? -1,
                             ballTransform.position.x, ballTransform.position.y, ballTransform.position.z,
                             0f, 0f, 0f, 0, 0);

                Debug.Log($"🎉 Remote pickup successful for {character.name}");
                return true;
            }
            else
            {
                Debug.LogWarning($"❌ BallController: No authority and not my character");
            }
        }
        else
        {
            Debug.LogWarning($"❌ BallController: Distance too far: {distance:F2} > {pickupRange}");
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

        Debug.Log($"🚀 === BALL THROW START ===");
        Debug.Log($"  - hasNetworkAuthority: {hasNetworkAuthority}");
        Debug.Log($"  - photonView.IsMine: {photonView.IsMine}");
        Debug.Log($"  - Ball Owner: {photonView.Owner?.NickName ?? "NULL"}");

        // FIXED: Allow throws if we own the ball OR if we're the holder
        bool canThrow = hasNetworkAuthority || (holder != null && holder.GetComponent<PhotonView>()?.IsMine == true);

        if (!canThrow)
        {
            Debug.LogWarning($"❌ Cannot throw ball - no authority. hasNetworkAuthority={hasNetworkAuthority}");
            return;
        }

        Debug.Log($"✅ Throw authorized - proceeding");

        StopHoldTimer();

        thrower = holder;

        bool isInAir = thrower != null && !thrower.IsGrounded();
        if (currentThrowType == ThrowType.Normal && isInAir)
        {
            currentThrowType = ThrowType.JumpThrow;
        }

        if (collisionSystem != null && thrower != null)
        {
            collisionSystem.OnBallThrown(thrower);
        }

        targetOpponent = FindOpponentForThrower();
        Vector3 throwDirection = direction;

        if (targetOpponent != null)
        {
            Vector3 targetPos = targetOpponent.position;
            throwDirection = (targetPos - ballTransform.position).normalized;
        }

        velocity = throwDirection * currentThrowSpeed * power;

        if (holder != null)
        {
            holder.SetHasBall(false);
            holder = null;
        }

        SetBallState(BallState.Thrown);

        // FIXED: Sync to other clients if we have authority
        if (hasNetworkAuthority)
        {
            PhotonView throwerView = thrower?.GetComponent<PhotonView>();
            if (throwerView != null)
            {
                photonView.RPC("SyncBallEvent", RpcTarget.Others, "Throw",
                             throwerView.ViewID,
                             ballTransform.position.x, ballTransform.position.y, ballTransform.position.z,
                             velocity.x, velocity.y, velocity.z,
                             currentDamage, (int)currentThrowType);
            }
        }

        Debug.Log($"🚀 === BALL THROW COMPLETE ===");
    }

    public void ResetBall()
    {
        Vector3 spawnPosition = new Vector3(0, 2f, 0);
        ballTransform.position = spawnPosition;
        velocity = Vector3.zero;
        currentWallBounces = 0;

        StopHoldTimer();

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
        if (!Application.isPlaying) return;

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
        }
    }
}