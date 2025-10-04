using UnityEngine;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Network BallManager with CLEANED UP PlayerCharacter-only support
/// FIXED: Improved multiplayer throwing system for all players
/// </summary>
public class BallManager : MonoBehaviourPunCallbacks
{
    public static BallManager Instance { get; private set; }

    [Header("Ball Settings")]
    [SerializeField] public GameObject ballPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2f, 0);
    [SerializeField] private float respawnDelay = 2f;

    [Header("Ball Hold Timer Integration")]
    [SerializeField] private bool enableHoldTimerUI = true;
    [SerializeField] private bool autoShowTimerOnPickup = true;
    [SerializeField] private bool autoHideTimerOnThrow = true;

    // Current ball reference
    private BallController currentBall;
    private bool gameActive = true;
    private bool isTimerUIShown = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (PhotonNetwork.OfflineMode)
        {
            // Offline single-player: spawn locally
            SpawnBall();
        }
        else
        {
            // Only Master Client spawns the ball initially
            if (PhotonNetwork.IsMasterClient)
            {
                SpawnBall();
            }
            else
            {
                // FIXED: Non-master clients should look for existing ball
                StartCoroutine(FindExistingBall());
            }
        }
    }

    /// <summary>
    /// FIXED: Coroutine for non-master clients to find the ball spawned by master
    /// </summary>
    IEnumerator FindExistingBall()
    {
        // Wait a frame to ensure the ball has been spawned
        yield return null;

        // Try to find the ball for up to 2 seconds
        float timeout = 2f;
        float elapsed = 0f;

        while (currentBall == null && elapsed < timeout)
        {
            BallController[] balls = FindObjectsOfType<BallController>();
            if (balls.Length > 0)
            {
                // Register the first ball found
                currentBall = balls[0];
                Debug.Log($"[BallManager] Non-master client found and registered ball: {currentBall.name}");
                break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (currentBall == null)
        {
            Debug.LogWarning("[BallManager] Non-master client could not find ball after timeout");
        }
    }

    void Update()
    {
        if (enableHoldTimerUI && currentBall != null)
        {
            MonitorBallHoldState();
        }

        // FIXED: Continuously check for ball if we don't have one
        if (currentBall == null && gameActive)
        {
            BallController foundBall = FindObjectOfType<BallController>();
            if (foundBall != null)
            {
                currentBall = foundBall;
                Debug.Log($"[BallManager] Found and registered ball during update: {currentBall.name}");
            }
        }
    }

    void MonitorBallHoldState()
    {
        bool ballIsHeld = currentBall.IsHeld();

        if (ballIsHeld && !isTimerUIShown && autoShowTimerOnPickup)
        {
            ShowHoldTimerUI();
        }
        else if (!ballIsHeld && isTimerUIShown && autoHideTimerOnThrow)
        {
            HideHoldTimerUI();
        }
    }

    void SpawnBall()
    {
        if (ballPrefab == null) return;

        if (currentBall != null)
        {
            if (isTimerUIShown)
            {
                HideHoldTimerUI();
            }

            if (currentBall.photonView != null)
            {
                PhotonNetwork.Destroy(currentBall.gameObject);
            }
            else
            {
                Destroy(currentBall.gameObject);
            }
        }

        if (PhotonNetwork.OfflineMode)
        {
            // Local spawn in OfflineMode
            GameObject ballObj = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
            currentBall = ballObj.GetComponent<BallController>();
            Debug.Log("[BallManager] OfflineMode spawned ball locally");
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            // Network spawn ball
            GameObject ballObj = PhotonNetwork.Instantiate(ballPrefab.name, spawnPosition, Quaternion.identity);
            currentBall = ballObj.GetComponent<BallController>();

            // FIXED: Notify all clients about the new ball
            photonView.RPC("RegisterBallOnClients", RpcTarget.Others, ballObj.GetPhotonView().ViewID);

            Debug.Log($"[BallManager] Master spawned ball with ViewID: {ballObj.GetPhotonView().ViewID}");
        }
    }

    /// <summary>
    /// FIXED: RPC to register the ball on all clients
    /// </summary>
    [PunRPC]
    void RegisterBallOnClients(int ballViewID)
    {
        // Find the ball by its PhotonView ID
        PhotonView ballPhotonView = PhotonView.Find(ballViewID);
        if (ballPhotonView != null)
        {
            currentBall = ballPhotonView.GetComponent<BallController>();
            Debug.Log($"[BallManager] Client registered ball with ViewID: {ballViewID}");
        }
        else
        {
            Debug.LogError($"[BallManager] Could not find ball with ViewID: {ballViewID}");
        }
    }

    /// <summary>
    /// FIXED: Enhanced pickup request with better null checking - PlayerCharacter only
    /// </summary>
    public bool RequestBallPickup(PlayerCharacter character)
    {
        Debug.Log($"🏀 === BALLMANAGER PICKUP START === {character.name}");

        // FIXED: Better null checking with helpful debug info
        if (currentBall == null)
        {
            Debug.LogError($"❌ currentBall is NULL! Attempting to find ball...");

            // Try to find the ball one more time
            BallController foundBall = FindObjectOfType<BallController>();
            if (foundBall != null)
            {
                currentBall = foundBall;
                Debug.Log($"✅ Found ball during pickup attempt: {currentBall.name}");
            }
            else
            {
                Debug.LogError($"❌ No ball found in scene at all!");
                return false;
            }
        }

        if (!currentBall.IsFree())
        {
            Debug.LogWarning($"❌ Ball not free - state: {currentBall.GetBallState()}");
            return false;
        }

        // Check distance
        float distance = Vector3.Distance(character.transform.position, currentBall.transform.position);
        Debug.Log($"📏 BallManager distance check: {distance:F2}");

        if (distance > 1.2f)
        {
            Debug.LogWarning($"❌ Distance too far in BallManager: {distance:F2} > 1.2");
            return false;
        }

        PhotonView characterView = character.GetComponent<PhotonView>();
        PhotonView ballView = currentBall.GetComponent<PhotonView>();

        Debug.Log($"📡 BallManager PhotonView Analysis:");
        Debug.Log($"  - Character PhotonView: {(characterView != null ? "EXISTS" : "NULL")}");
        Debug.Log($"  - Ball PhotonView: {(ballView != null ? "EXISTS" : "NULL")}");

        if (characterView != null)
        {
            Debug.Log($"  - Character ViewID: {characterView.ViewID}");
            Debug.Log($"  - Character IsMine: {characterView.IsMine}");
            Debug.Log($"  - Character Owner: {characterView.Owner?.NickName ?? "NULL"}");
        }

        if (ballView != null)
        {
            Debug.Log($"  - Ball ViewID: {ballView.ViewID}");
            Debug.Log($"  - Ball IsMine: {ballView.IsMine}");
            Debug.Log($"  - Ball Owner: {ballView.Owner?.NickName ?? "NULL"}");
        }

        if (characterView != null && characterView.IsMine && ballView != null)
        {
            Debug.Log($"✅ BallManager: This is our local player");

            // If we don't own the ball, request ownership
            if (!ballView.IsMine)
            {
                Debug.Log($"🔄 BallManager: Ball not owned locally, requesting ownership");
                Debug.Log($"  - Ball owner before: {ballView.Owner?.NickName ?? "NULL"}");

                ballView.RequestOwnership();

                Debug.Log($"  - Ball owner after: {ballView.Owner?.NickName ?? "NULL"}");
                Debug.Log($"  - Ball IsMine after: {ballView.IsMine}");
                Debug.Log($"❌ BallManager: Returning false, will retry next frame");
                return false;
            }

            Debug.Log($"✅ BallManager: We own the ball, proceeding to TryPickup");
            bool result = currentBall.TryPickup(character);
            Debug.Log($"🎯 BallManager: TryPickup result = {result}");
            return result;
        }
        else
        {
            Debug.Log($"🎮 BallManager: Single player or master client mode");
            bool result = currentBall.TryPickup(character);
            Debug.Log($"🎯 BallManager: Single player TryPickup result = {result}");
            return result;
        }
    }

    /// <summary>
    /// IMPROVED: Request ball throw with character data and FIXED network authority
    /// This method now works for both Master Client and regular clients
    /// </summary>
    public void RequestBallThrowWithCharacterData(PlayerCharacter thrower, CharacterData characterData, ThrowType throwType, int damage)
    {
        if (currentBall == null || characterData == null) return;

        if (currentBall.GetHolder() != thrower) return;

        Debug.Log($"🚀 === BALLMANAGER THROW REQUEST === {thrower.name}");
        Debug.Log($"  - PhotonNetwork.IsMasterClient: {PhotonNetwork.IsMasterClient}");
        Debug.Log($"  - Thrower IsMine: {thrower.GetComponent<PhotonView>()?.IsMine}");

        // FIXED: Allow both Master Client and ball owner to execute throws
        PhotonView throwerView = thrower.GetComponent<PhotonView>();
        PhotonView ballView = currentBall.GetComponent<PhotonView>();

        bool isMyPlayer = throwerView != null && throwerView.IsMine;
        bool ownsBall = ballView != null && ballView.IsMine;

        if (isMyPlayer && ownsBall)
        {
            // Local player owns ball - execute throw directly
            Debug.Log($"✅ Local player owns ball - executing throw directly");
            ExecuteThrow(thrower, characterData, throwType, damage);
        }
        else if (PhotonNetwork.IsMasterClient && !isMyPlayer)
        {
            // Master client handling remote player throw
            Debug.Log($"🎮 Master client handling remote player throw");
            ExecuteThrow(thrower, characterData, throwType, damage);
        }
        else if (isMyPlayer && !ownsBall)
        {
            // My player but don't own ball - send RPC to ball owner
            Debug.Log($"📤 Sending throw request to ball owner");
            if (throwerView != null)
            {
                photonView.RPC("RequestThrowFromBallOwner", RpcTarget.All,
                             throwerView.ViewID, (int)throwType, damage);
            }
        }
        else
        {
            Debug.LogWarning($"❌ Throw rejected - invalid authority");
        }
    }

    /// <summary>
    /// FIXED: New RPC for handling throw requests from players who don't own the ball
    /// </summary>
    [PunRPC]
    void RequestThrowFromBallOwner(int throwerViewID, int throwType, int damage)
    {
        // Only the ball owner should process this
        if (currentBall == null || !currentBall.GetComponent<PhotonView>().IsMine)
        {
            Debug.Log($"Ignoring throw request - not ball owner");
            return;
        }

        PhotonView throwerView = PhotonView.Find(throwerViewID);
        PlayerCharacter thrower = throwerView?.GetComponent<PlayerCharacter>();

        if (thrower != null && currentBall.GetHolder() == thrower)
        {
            CharacterData characterData = thrower.GetCharacterData();
            if (characterData != null)
            {
                Debug.Log($"🔄 Ball owner executing throw for remote player: {thrower.name}");
                ExecuteThrow(thrower, characterData, (ThrowType)throwType, damage);
            }
        }
    }

    /// <summary>
    /// FIXED: Execute throw method that works for all network scenarios
    /// </summary>
    void ExecuteThrow(PlayerCharacter thrower, CharacterData characterData, ThrowType throwType, int damage)
    {
        if (isTimerUIShown && autoHideTimerOnThrow)
        {
            HideHoldTimerUI();
        }

        float throwSpeed = characterData.GetThrowSpeed(GetBaseThrowSpeed(throwType));
        Vector3 direction = CalculateThrowDirection(thrower);

        currentBall.SetThrowData(throwType, damage, throwSpeed);
        currentBall.SetThrower(thrower);
        currentBall.ThrowBall(direction, 1f);

        // Apply VFX
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnThrowVFX(currentBall.transform.position, thrower, throwType);
        }

        Debug.Log($"✅ Ball throw executed: {thrower.name} threw {throwType} for {damage} damage");

        StartCoroutine(CheckForRespawn());
    }

    /// <summary>
    /// IMPROVED: Better throw direction calculation with player side consideration
    /// </summary>
    Vector3 CalculateThrowDirection(PlayerCharacter thrower)
    {
        // Find opponent for direction calculation
        PlayerCharacter opponent = FindOpponent(thrower);
        if (opponent != null)
        {
            Vector3 targetDir = (opponent.transform.position - currentBall.transform.position).normalized;
            return thrower.GetCharacterData().ApplyThrowAccuracy(targetDir);
        }

        // Fallback: determine direction based on player position (left side throws right, right side throws left)
        Vector3 throwerPos = thrower.transform.position;
        Vector3 direction = throwerPos.x < 0 ? Vector3.right : Vector3.left;

        return direction;
    }

    PlayerCharacter FindOpponent(PlayerCharacter thrower)
    {
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            if (player != thrower)
            {
                return player;
            }
        }
        return null;
    }

    /// <summary>
    /// SIMPLIFIED: Legacy throw method now redirects to main method
    /// </summary>
    public void RequestBallThrowSimple(PlayerCharacter thrower, float power = 1f)
    {
        if (currentBall != null && currentBall.GetHolder() == thrower)
        {
            CharacterData characterData = thrower.GetCharacterData();
            if (characterData != null)
            {
                ThrowType throwType = thrower.IsGrounded() ? ThrowType.Normal : ThrowType.JumpThrow;
                int damage = characterData.GetThrowDamage(throwType);
                RequestBallThrowWithCharacterData(thrower, characterData, throwType, damage);
            }
        }
    }

    float GetBaseThrowSpeed(ThrowType throwType)
    {
        return throwType switch
        {
            ThrowType.Normal => 18f,
            ThrowType.JumpThrow => 22f,
            ThrowType.Ultimate => 25f,
            _ => 18f
        };
    }

    /// <summary>
    /// Handle ultimate throws - PlayerCharacter only
    /// </summary>
    public void RequestUltimateThrow(PlayerCharacter thrower, UltimateType ultimateType)
    {
        if (currentBall == null || currentBall.GetHolder() != thrower) return;

        CharacterData characterData = thrower.GetCharacterData();
        if (characterData == null) return;

        int ultimateDamage = characterData.GetUltimateDamage();

        switch (ultimateType)
        {
            case UltimateType.PowerThrow:
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;
            case UltimateType.MultiThrow:
                StartCoroutine(MultiThrowCoroutine(thrower, characterData));
                break;
            case UltimateType.Curveball:
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;
            default:
                RequestBallThrowWithCharacterData(thrower, characterData, ThrowType.Ultimate, ultimateDamage);
                break;
        }
    }

    public IEnumerator MultiThrowCoroutine(PlayerCharacter thrower, CharacterData characterData)
    {
        if (isTimerUIShown)
        {
            HideHoldTimerUI();
        }

        int throwCount = characterData.GetMultiThrowCount();
        int damagePerBall = characterData.GetUltimateDamage();
        float throwSpeed = characterData.GetUltimateSpeed();

        for (int i = 0; i < throwCount; i++)
        {
            GameObject tempBallObj;
            if (PhotonNetwork.OfflineMode)
            {
                tempBallObj = Instantiate(ballPrefab, thrower.transform.position + Vector3.up * 1.5f, Quaternion.identity);
            }
            else
            {
                tempBallObj = PhotonNetwork.Instantiate(ballPrefab.name,
                                                             thrower.transform.position + Vector3.up * 1.5f,
                                                             Quaternion.identity);
            }
            BallController tempBall = tempBallObj.GetComponent<BallController>();

            if (tempBall != null)
            {
                tempBall.SetThrowData(ThrowType.Ultimate, damagePerBall, throwSpeed);
                tempBall.SetThrower(thrower);

                // FIXED: Calculate direction properly for multi-throw
                PlayerCharacter opponent = FindOpponent(thrower);
                Vector3 throwDir;
                
                if (opponent != null)
                {
                    Vector3 targetDir = (opponent.transform.position - tempBall.transform.position).normalized;
                    throwDir = characterData.ApplyThrowAccuracy(targetDir);
                }
                else
                {
                    // Fallback: determine direction based on player position
                    throwDir = thrower.transform.position.x < 0 ? Vector3.right : Vector3.left;
                }
                
                // Apply spread angle
                float spreadAngle = characterData.GetMultiThrowSpread();
                float angleOffset = (i - (throwCount - 1) * 0.5f) * (spreadAngle / throwCount);
                throwDir = Quaternion.Euler(0, angleOffset, 0) * throwDir;
                throwDir.y = 0.1f;

                tempBall.ThrowBallInternal(throwDir.normalized, 1f);

                // Auto-destroy temp balls after time
                StartCoroutine(DestroyTempBall(tempBallObj, 4f));
            }

            yield return new WaitForSeconds(characterData.GetMultiThrowDelay());
        }
    }

    IEnumerator DestroyTempBall(GameObject ballObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ballObj != null)
        {
            if (PhotonNetwork.OfflineMode)
            {
                Destroy(ballObj);
            }
            else
            {
                PhotonNetwork.Destroy(ballObj);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HOLD TIMER UI INTEGRATION
    // ═══════════════════════════════════════════════════════════════

    void ShowHoldTimerUI()
    {
        if (BallHoldTimerUI.Instance != null && currentBall != null)
        {
            BallHoldTimerUI.Instance.ShowTimer(currentBall);
            isTimerUIShown = true;
        }
    }

    void HideHoldTimerUI()
    {
        if (BallHoldTimerUI.Instance != null)
        {
            BallHoldTimerUI.Instance.HideTimer();
            isTimerUIShown = false;
        }
    }

    public void ForceShowHoldTimerUI()
    {
        if (currentBall != null && currentBall.IsHeld())
        {
            ShowHoldTimerUI();
        }
    }

    public void ForceHideHoldTimerUI()
    {
        HideHoldTimerUI();
    }

    public void ResetBall()
    {
        // FIXED: All clients can reset ball, not just master client
        if (currentBall != null)
        {
            if (isTimerUIShown)
            {
                HideHoldTimerUI();
            }
            currentBall.ResetBall();
            Debug.Log($"[BALL RESET] Ball reset by client {PhotonNetwork.LocalPlayer.ActorNumber}");
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            // Only master client spawns new ball if none exists
            SpawnBall();
        }
    }

    IEnumerator CheckForRespawn()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (currentBall != null && PhotonNetwork.IsMasterClient)
        {
            Vector3 ballPos = currentBall.transform.position;

            if (ballPos.y < -3f || Mathf.Abs(ballPos.x) > 20f)
            {
                SpawnBall();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // NETWORK RPC HANDLERS - LEGACY REMOVED
    // ═══════════════════════════════════════════════════════════════

    [PunRPC]
    void RequestPickupFromMaster(int characterViewID)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PhotonView characterView = PhotonView.Find(characterViewID);
        PlayerCharacter character = characterView?.GetComponent<PlayerCharacter>();

        if (character != null && currentBall != null)
        {
            bool success = currentBall.TryPickup(character);
            if (success && enableHoldTimerUI && autoShowTimerOnPickup)
            {
                ShowHoldTimerUI();
            }
        }
    }

    [PunRPC]
    void RequestThrowFromMaster(int throwerViewID, int throwType, int damage)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PhotonView throwerView = PhotonView.Find(throwerViewID);
        PlayerCharacter thrower = throwerView?.GetComponent<PlayerCharacter>();

        if (thrower != null && currentBall != null)
        {
            CharacterData characterData = thrower.GetCharacterData();
            if (characterData != null)
            {
                ExecuteThrow(thrower, characterData, (ThrowType)throwType, damage);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC INTERFACE - CLEANED UP
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// FIXED: Enhanced GetCurrentBall with auto-find fallback
    /// </summary>
    public BallController GetCurrentBall()
    {
        // If we don't have a ball reference, try to find it
        if (currentBall == null)
        {
            currentBall = FindObjectOfType<BallController>();
            if (currentBall != null)
            {
                Debug.Log($"[BallManager] GetCurrentBall() found ball: {currentBall.name}");
            }
        }
        return currentBall;
    }

    public bool HasActiveBall() => currentBall != null;
    public Vector3 GetBallSpawnPosition() => spawnPosition;
    public bool IsHoldTimerUIEnabled() => enableHoldTimerUI;
    public bool IsTimerUIShown() => isTimerUIShown;

    public void SetGameActive(bool active)
    {
        gameActive = active;
        if (!active && isTimerUIShown)
        {
            HideHoldTimerUI();
        }
    }

    public void SetHoldTimerUIEnabled(bool enabled)
    {
        enableHoldTimerUI = enabled;
        if (!enabled && isTimerUIShown)
        {
            HideHoldTimerUI();
        }
    }

    public void SetAutoShowTimerOnPickup(bool enabled)
    {
        autoShowTimerOnPickup = enabled;
    }

    public void SetAutoHideTimerOnThrow(bool enabled)
    {
        autoHideTimerOnThrow = enabled;
    }

    // ═══════════════════════════════════════════════════════════════
    // PUN CALLBACKS
    // ═══════════════════════════════════════════════════════════════

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // If this client became Master Client, take control of ball spawning
        if (PhotonNetwork.IsMasterClient && currentBall == null)
        {
            SpawnBall();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Handle player leaving - could trigger game end logic
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            // Less than 2 players, might want to pause or end game
            SetGameActive(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // NETWORK BALL MANAGEMENT
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// FIXED: Called when a ball is spawned (by any means)
    /// </summary>
    public void OnBallSpawned(BallController ball)
    {
        currentBall = ball;
        Debug.Log($"[BallManager] Ball registered via OnBallSpawned: {ball.name}");
    }

    public void OnBallDestroyed()
    {
        if (isTimerUIShown)
        {
            HideHoldTimerUI();
        }
        currentBall = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // DEBUG GUI
    // ═══════════════════════════════════════════════════════════════

    void OnGUI()
    {
        /*if (!Application.isPlaying) return;

        // BallManager debug panel (top right)
        float x = Screen.width - 420;
        GUI.Box(new Rect(x, 10, 400, 150), "BALLMANAGER DEBUG INFO");

        float y = 30;
        GUI.Label(new Rect(x + 10, y, 380, 20), $"IsMasterClient: {PhotonNetwork.IsMasterClient}");
        y += 20;
        GUI.Label(new Rect(x + 10, y, 380, 20), $"Current Ball: {(currentBall != null ? currentBall.name : "NULL")}");
        y += 20;

        if (currentBall != null)
        {
            PhotonView ballPV = currentBall.GetComponent<PhotonView>();
            if (ballPV != null)
            {
                GUI.Label(new Rect(x + 10, y, 380, 20), $"Ball ViewID: {ballPV.ViewID}");
                y += 20;
                GUI.Label(new Rect(x + 10, y, 380, 20), $"Ball Owner: {ballPV.Owner?.NickName ?? "NULL"}");
                y += 20;
                GUI.Label(new Rect(x + 10, y, 380, 20), $"Ball IsMine: {ballPV.IsMine}");
                y += 20;
            }
        }

        GUI.Label(new Rect(x + 10, y, 380, 20), $"Game Active: {gameActive}");
        y += 20;
        GUI.Label(new Rect(x + 10, y, 380, 20), $"Timer UI Shown: {isTimerUIShown}");*/
    }

    // ═══════════════════════════════════════════════════════════════
    // LEGACY SUPPORT REMOVED
    // All CharacterController methods have been removed to clean up the code
    // ═══════════════════════════════════════════════════════════════
}