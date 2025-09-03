using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// FIXED: MatchManager with NO DUPLICATE SPAWNING and proper UI sync
/// KEY FIXES: Removed EnsureCharacterDataApplied coroutine, Fixed Owner vs owner, Added UI sync
/// </summary>
public class MatchManager : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Match Settings")]
    [SerializeField] private int roundsToWin = 2;
    [SerializeField] private float roundDuration = 90f;
    [SerializeField] private float preFightCountdown = 3f;
    [SerializeField] private float postRoundDelay = 3f;
    [SerializeField] private float matchEndDelay = 5f;

    [Header("Character Setup")]
    [SerializeField] private CharacterData[] availableCharacters;
    [SerializeField] private float gameStartDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    [Header("Player Spawn Points")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Scene References")]
    [SerializeField] private string characterSelectionScene = "CharacterSelection";
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("UI References")]
    [SerializeField] private MatchUI matchUI;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip roundStartSound;
    [SerializeField] private AudioClip roundEndSound;
    [SerializeField] private AudioClip matchWinSound;
    [SerializeField] private AudioClip countdownSound;

    // Match state
    public enum MatchState { Initializing, PreFight, Fighting, RoundEnd, MatchEnd }
    private MatchState currentState = MatchState.Initializing;

    [Header("State Management")]
    private bool isReturningToMenu = false;

    // Match data (synchronized)
    private int currentRound = 0;
    private int player1RoundsWon = 0;
    private int player2RoundsWon = 0;
    private int matchWinner = 0;

    // Timer synchronization using PhotonNetwork.Time
    private double roundStartTime = 0.0;
    private double roundEndTime = 0.0;
    private bool roundActive = false;

    // Player references
    private Dictionary<int, PlayerCharacter> networkPlayers = new Dictionary<int, PlayerCharacter>();
    private Dictionary<int, PlayerHealth> networkPlayersHealth = new Dictionary<int, PlayerHealth>();

    // FIXED: Spawn control flags
    private bool playersSpawned = false;
    private bool isInitialSpawn = true;
    private List<GameObject> spawnedPlayers = new List<GameObject>();

    // FIXED: Use centralized room state manager
    // Room property constants are now in RoomStateManager

    void Awake()
    {
        // Setup audio
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.7f;
        }

        // Find UI
        if (matchUI == null)
        {
            matchUI = FindObjectOfType<MatchUI>();
        }
    }

    void Start()
    {
        StartCoroutine(CompleteMatchFlow());
    }

    void Update()
    {
        // FIXED: Update timer for ALL clients, not just master
        if (roundActive && matchUI != null)
        {
            // Calculate remaining time using network time for ALL clients
            float remainingTime = GetRemainingTime();

            // Update UI on ALL clients
            matchUI.UpdateTimer(remainingTime);
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);

            // Only Master Client checks for timeout and manages room properties
            if (PhotonNetwork.IsMasterClient && remainingTime <= 0f)
            {
                EndRoundByTimeOut();
            }
        }

        // ALL clients sync match state from room properties
        SyncFromRoomProperties();

        // Debug controls
        if (debugMode)
        {
            DebugSpawnIssues();
        }
    }

    /// <summary>
    /// FIXED: Allow ANY client to trigger round end, but master handles authority
    /// </summary>
    public void RequestRoundEnd(int winner, string reason)
    {
        // Any client can request round end, but only if round is active
        if (!roundActive) return;

        Debug.Log($"[ROUND END REQUEST] Player {PhotonNetwork.LocalPlayer.ActorNumber} requesting round end. Winner: {winner}, Reason: {reason}");

        if (PhotonNetwork.IsMasterClient)
        {
            // Master client handles immediately
            photonView.RPC("EndRound", RpcTarget.All, winner, reason);
        }
        else
        {
            // Non-master client sends request to master
            photonView.RPC("RequestRoundEndFromMaster", RpcTarget.MasterClient, winner, reason, PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }

    [PunRPC]
    void RequestRoundEndFromMaster(int winner, string reason, int requestingPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || !roundActive) return;

        Debug.Log($"[MASTER] Received round end request from player {requestingPlayer}. Winner: {winner}, Reason: {reason}");

        // Master client processes the request
        photonView.RPC("EndRound", RpcTarget.All, winner, reason);
    }

    // ══════════════════════════════════════════════════════════
    // FIXED: NO DUPLICATE SPAWN SYSTEM
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// FIXED: Streamlined match flow - no duplicate calls
    /// </summary>
    IEnumerator CompleteMatchFlow()
    {
        currentState = MatchState.Initializing;

        // Wait for players to join
        while (PhotonNetwork.PlayerList.Length < 2)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // FIXED: Single spawn call - no duplicates
        yield return StartCoroutine(HandlePlayerSpawning());

        // Wait for all players to be ready
        yield return new WaitForSeconds(gameStartDelay);

        // Cache players and setup
        CacheNetworkPlayers();
        AssignPlayerSides();
        InitializeUI();

        // Sync initial state to all clients
        SyncMatchStateToAll();

        // Only Master Client starts the first round
        if (PhotonNetwork.IsMasterClient)
        {
            StartRound(1);
        }
    }

    /// <summary>
    /// FIXED: Single spawn handling - no duplicates
    /// </summary>
    IEnumerator HandlePlayerSpawning()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Master spawns everyone
            yield return StartCoroutine(SpawnAllPlayers());
        }
        else
        {
            // Non-master clients wait for spawn signal
            while (!playersSpawned)
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.PLAYERS_SPAWNED_KEY))
                {
                    bool shouldSpawn = (bool)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.PLAYERS_SPAWNED_KEY];
                    if (shouldSpawn)
                    {
                        SpawnMyPlayer();
                        playersSpawned = true;
                    }
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    /// <summary>
    /// FIXED: Master client spawns all players in sequence
    /// </summary>
    IEnumerator SpawnAllPlayers()
    {
        Debug.Log("[MASTER] Starting controlled spawn sequence...");

        // Set spawn flag to false initially
        Hashtable roomProps = new Hashtable { [RoomStateManager.PLAYERS_SPAWNED_KEY] = false };
        SafeSetRoomProperties(roomProps);
        yield return new WaitForSeconds(0.2f);

        // Spawn master client first
        SpawnMyPlayer();
        yield return new WaitForSeconds(0.5f);

        // Signal others to spawn
        photonView.RPC("SpawnMyPlayer", RpcTarget.Others);
        yield return new WaitForSeconds(1f);

        // Mark spawning complete
        roomProps[RoomStateManager.PLAYERS_SPAWNED_KEY] = true;
        SafeSetRoomProperties(roomProps);

        playersSpawned = true;
        Debug.Log("[MASTER] All players spawned successfully");
    }

    /// <summary>
    /// FIXED: Single spawn method - NO position resets or duplicate coroutines
    /// </summary>
    [PunRPC]
    void SpawnMyPlayer()
    {
        if (playersSpawned && isInitialSpawn)
        {
            Debug.LogWarning("[SPAWN] Already spawned, ignoring duplicate call");
            return;
        }

        int selectedCharacterIndex = GetSelectedCharacterIndex();
        CharacterData selectedCharacter = GetCharacterData(selectedCharacterIndex);

        if (selectedCharacter?.characterPrefab == null)
        {
            Debug.LogError($"[SPAWN ERROR] Invalid character selection: {selectedCharacterIndex}");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        spawnPosition.y = Mathf.Max(spawnPosition.y, 0.5f);

        Debug.Log($"[SPAWN] {selectedCharacter.characterName} at {spawnPosition} for Actor {PhotonNetwork.LocalPlayer.ActorNumber}");

        // Store character data in player properties BEFORE spawning
        StorePlayerCharacterData(selectedCharacter, selectedCharacterIndex);

        // Spawn player
        GameObject playerObj = PhotonNetwork.Instantiate(
            selectedCharacter.characterPrefab.name,
            spawnPosition,
            Quaternion.identity
        );

        PlayerCharacter playerCharacter = playerObj.GetComponent<PlayerCharacter>();
        if (playerCharacter != null)
        {
            // Load character data immediately - NO COROUTINES, NO DELAYS
            playerCharacter.LoadCharacter(selectedCharacter);

            // FIXED: Network sync immediately
            photonView.RPC("ApplyCharacterDataToPlayer", RpcTarget.All,
                playerObj.GetComponent<PhotonView>().ViewID,
                selectedCharacterIndex);

            // Track this player
            TrackSpawnedPlayer(playerObj);

            Debug.Log($"[SPAWN SUCCESS] {selectedCharacter.characterName} ready with color {selectedCharacter.characterColor}");
        }

        if (isInitialSpawn)
        {
            playersSpawned = true;
        }
    }

    /// <summary>
    /// FIXED: Store character data in player properties
    /// </summary>
    void StorePlayerCharacterData(CharacterData character, int characterIndex)
    {
        Hashtable playerProps = new Hashtable
        {
            ["CharacterIndex"] = characterIndex,
            ["CharacterName"] = character.characterName,
            ["CharacterColor_R"] = character.characterColor.r,
            ["CharacterColor_G"] = character.characterColor.g,
            ["CharacterColor_B"] = character.characterColor.b
        };
        // FIXED: Use centralized room state manager with fallback
        RoomStateManager.GetOrCreateInstance()?.SetPlayerCharacterData(
            characterIndex,
            character.characterName,
            character.characterColor
        );

        Debug.Log($"[DATA STORED] Character data stored for {character.characterName}");
    }

    /// <summary>
    /// FIXED: Apply character data across network - no position changes
    /// </summary>
    [PunRPC]
    void ApplyCharacterDataToPlayer(int viewID, int characterIndex)
    {
        PhotonView targetView = PhotonView.Find(viewID);
        if (targetView == null)
        {
            Debug.LogWarning($"[SYNC] PhotonView {viewID} not found");
            return;
        }

        PlayerCharacter playerCharacter = targetView.GetComponent<PlayerCharacter>();
        if (playerCharacter == null)
        {
            Debug.LogWarning($"[SYNC] PlayerCharacter not found on view {viewID}");
            return;
        }

        CharacterData characterData = GetCharacterData(characterIndex);
        if (characterData == null)
        {
            Debug.LogWarning($"[SYNC] CharacterData {characterIndex} not found");
            return;
        }

        // Apply character data without position changes
        playerCharacter.LoadCharacter(characterData);

        // Apply color from network properties if available
        if (targetView.Owner.CustomProperties.ContainsKey("CharacterColor_R"))
        {
            float r = (float)targetView.Owner.CustomProperties["CharacterColor_R"];
            float g = (float)targetView.Owner.CustomProperties["CharacterColor_G"];
            float b = (float)targetView.Owner.CustomProperties["CharacterColor_B"];
            Color networkColor = new Color(r, g, b, 1f);
            playerCharacter.ForceApplyColor(networkColor);
        }

        Debug.Log($"[SYNC SUCCESS] Applied {characterData.characterName} data to player {targetView.Owner.ActorNumber}");
    }

    /// <summary>
    /// Assign player sides (left/right facing)
    /// </summary>
    void AssignPlayerSides()
    {
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();

        foreach (PlayerCharacter player in allPlayers)
        {
            if (player.photonView != null)
            {
                int actorNumber = player.photonView.Owner.ActorNumber; // FIXED: Capital O

                if (actorNumber == 1 || PhotonNetwork.PlayerList[0].ActorNumber == actorNumber)
                {
                    player.SetPlayerSide(1); // Left player faces right
                }
                else
                {
                    player.SetPlayerSide(2); // Right player faces left
                }
            }
        }

        Debug.Log("[SIDES] Player sides assigned successfully");
    }

    void TrackSpawnedPlayer(GameObject playerObj)
    {
        if (!spawnedPlayers.Contains(playerObj))
        {
            spawnedPlayers.Add(playerObj);
        }
        spawnedPlayers.RemoveAll(p => p == null);
    }

    // ══════════════════════════════════════════════════════════
    // FIXED: ROUND MANAGEMENT - No duplicate spawns
    // ══════════════════════════════════════════════════════════

    void StartRound(int roundNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        currentRound = roundNumber;
        currentState = MatchState.PreFight;
        roundActive = false;
        roundStartTime = 0.0;
        roundEndTime = 0.0;

        UpdateRoomProperties();

        // FIXED: For rounds > 1, only reset positions - DON'T respawn
        if (roundNumber > 1)
        {
            photonView.RPC("ResetPlayersForNewRound", RpcTarget.All);
        }
        else
        {
            // First round - just enable players
            photonView.RPC("PreparePlayersForFirstRound", RpcTarget.All);
        }

        // Start pre-fight sequence
        photonView.RPC("StartPreFightSequence", RpcTarget.All);
    }

    /// <summary>
    /// FIXED: Reset existing players for new round - NO spawning
    /// </summary>
    [PunRPC]
    void ResetPlayersForNewRound()
    {
        Debug.Log("[ROUND RESET] Resetting players for new round...");

        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();

        foreach (PlayerCharacter player in allPlayers)
        {
            if (player != null && player.photonView != null)
            {
                // Reset position
                int actorNumber = player.photonView.Owner.ActorNumber;
                Vector3 resetPosition = GetSpawnPositionForActor(actorNumber);
                player.transform.position = resetPosition;

                // CRITICAL FIX: Reset player state first, THEN health
                player.ResetPlayerState();

                // FIXED: Properly reset health on the player's owner
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null && player.photonView.IsMine)
                {
                    health.ResetHealthForNewRound();
                }

                // FIXED: Reapply character data and color to prevent color loss
                CharacterData characterData = player.GetCharacterData();
                if (characterData != null)
                {
                    player.LoadCharacter(characterData);

                    // Force reapply network color
                    if (player.photonView.Owner.CustomProperties.ContainsKey("CharacterColor_R"))
                    {
                        float r = (float)player.photonView.Owner.CustomProperties["CharacterColor_R"];
                        float g = (float)player.photonView.Owner.CustomProperties["CharacterColor_G"];
                        float b = (float)player.photonView.Owner.CustomProperties["CharacterColor_B"];
                        Color networkColor = new Color(r, g, b, 1f);
                        player.ForceApplyColor(networkColor);
                    }
                }

                Debug.Log($"[ROUND RESET] Player {actorNumber} reset to {resetPosition} with health reset");
            }
        }

        // FIXED: Reset ball properly with network authority
        ResetBallForNewRound();

        Debug.Log("[ROUND RESET] All players and ball reset completed");
    }

    void ResetBallForNewRound()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Master client handles ball reset
            BallManager ballManager = FindObjectOfType<BallManager>();
            if (ballManager != null)
            {
                ballManager.ResetBall();
            }
            else
            {
                // Fallback: Direct ball reset
                BallController[] balls = FindObjectsOfType<BallController>();
                foreach (BallController ball in balls)
                {
                    if (ball.photonView.IsMine)
                    {
                        ball.ResetBall();
                        break; // Only reset one ball
                    }
                }
            }

            // Signal ball reset to other clients
            photonView.RPC("OnBallResetComplete", RpcTarget.Others);
        }
    }

    [PunRPC]
    void OnBallResetComplete()
    {
        Debug.Log("[BALL RESET] Ball reset completed across network");
    }

    /// <summary>
    /// Prepare players for first round
    /// </summary>
    [PunRPC]
    void PreparePlayersForFirstRound()
    {
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();

        foreach (PlayerCharacter player in allPlayers)
        {
            if (player != null)
            {
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null && health.IsDead())
                {
                    player.ReviveForNewRound();
                }
                player.SetInputEnabled(false); // Disabled until fight starts
            }
        }
    }

    Vector3 GetSpawnPositionForActor(int actorNumber)
    {
        if (actorNumber == 1 || PhotonNetwork.PlayerList[0].ActorNumber == actorNumber)
        {
            return player1SpawnPoint != null ? player1SpawnPoint.position : new Vector3(-3f, 0.5f, 0f);
        }
        else
        {
            return player2SpawnPoint != null ? player2SpawnPoint.position : new Vector3(3f, 0.5f, 0f);
        }
    }

    [PunRPC]
    void StartPreFightSequence()
    {
        StartCoroutine(PreFightSequenceCoroutine());
    }

    IEnumerator PreFightSequenceCoroutine()
    {
        currentState = MatchState.PreFight;

        // FIXED: Update UI on all clients immediately
        SyncUIToAllClients();

        // Show round announcement
        if (matchUI != null)
        {
            matchUI.ShowRoundAnnouncement(currentRound);
        }

        PlaySound(roundStartSound);

        // Countdown
        for (int i = Mathf.RoundToInt(preFightCountdown); i > 0; i--)
        {
            if (matchUI != null)
            {
                matchUI.ShowCountdown(i);
            }

            PlaySound(countdownSound);
            yield return new WaitForSeconds(1f);
        }

        // Show "FIGHT!"
        if (matchUI != null)
        {
            matchUI.ShowFightStart();
        }

        // Only Master Client starts the fight phase
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartFightPhase", RpcTarget.All);
        }
    }

    [PunRPC]
    void StartFightPhase()
    {
        currentState = MatchState.Fighting;
        roundActive = true;

        // FIXED: Master client sets timer, others sync from room properties
        if (PhotonNetwork.IsMasterClient)
        {
            roundStartTime = PhotonNetwork.Time;
            roundEndTime = roundStartTime + roundDuration;
            UpdateRoomProperties();

            // Immediately sync to all clients
            photonView.RPC("SyncTimerValues", RpcTarget.Others, roundStartTime, roundEndTime);
        }

        // Enable player input
        EnablePlayerInput(true);

        // Update UI immediately for all clients
        if (matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
            if (roundActive)
            {
                float remainingTime = GetRemainingTime();
                matchUI.UpdateTimer(remainingTime);
            }
        }
    }

    [PunRPC]
    void SyncTimerValues(double startTime, double endTime)
    {
        roundStartTime = startTime;
        roundEndTime = endTime;

        // Immediately update UI
        if (matchUI != null && roundActive)
        {
            float remainingTime = GetRemainingTime();
            matchUI.UpdateTimer(remainingTime);
        }

        if (debugMode)
        {
            Debug.Log($"[TIMER SYNC] Received timer sync: Start={startTime}, End={endTime}, Remaining={GetRemainingTime()}");
        }
    }

    void EndRoundByTimeOut()
    {
        if (!PhotonNetwork.IsMasterClient || !roundActive) return;

        int winner = DetermineWinnerByHealth();
        photonView.RPC("EndRound", RpcTarget.All, winner, "timeout");
    }

    [PunRPC]
    void EndRound(int winner, string reason)
    {
        if (!roundActive) return;

        Debug.Log($"[END ROUND] Round ended. Winner: {winner}, Reason: {reason}");

        roundActive = false;
        currentState = MatchState.RoundEnd;
        EnablePlayerInput(false);

        // Update round scores (Master Client only manages the data)
        if (PhotonNetwork.IsMasterClient)
        {
            if (winner == 1) player1RoundsWon++;
            else if (winner == 2) player2RoundsWon++;

            UpdateRoomProperties();
        }
        else
        {
            // Non-master clients sync from room properties
            StartCoroutine(WaitAndSyncRoundScores());
        }

        // FIXED: Update UI on ALL clients immediately
        if (matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
            matchUI.ShowRoundResult(winner);
        }

        PlaySound(roundEndSound);

        // Check for match end (all clients can check)
        StartCoroutine(CheckForMatchEndAfterDelay());
    }
    IEnumerator WaitAndSyncRoundScores()
    {
        yield return new WaitForSeconds(0.5f); // Wait for room properties to sync
        SyncFromRoomProperties();

        // Update UI with synced data
        if (matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
        }
    }

    IEnumerator CheckForMatchEndAfterDelay()
    {
        yield return new WaitForSeconds(1f); // Wait for all clients to sync

        // All clients can check for match end
        if (player1RoundsWon >= roundsToWin || player2RoundsWon >= roundsToWin)
        {
            matchWinner = player1RoundsWon >= roundsToWin ? 1 : 2;

            // FIXED: Show match results on ALL clients
            StartCoroutine(EndMatchSequence());
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            // Only master starts next round
            StartCoroutine(NextRoundDelay());
        }
    }

    IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(postRoundDelay);

        if (PhotonNetwork.IsMasterClient)
        {
            StartRound(currentRound + 1);
        }
    }

    IEnumerator EndMatchSequence()
    {
        currentState = MatchState.MatchEnd;

        // FIXED: Only update room properties if we're still connected and able to
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            UpdateRoomProperties();
        }

        // Show match results
        if (matchUI != null)
        {
            CharacterData winnerCharacter = null;
            if (matchWinner == 1 && networkPlayers.ContainsKey(1))
                winnerCharacter = networkPlayers[1].GetCharacterData();
            else if (matchWinner == 2 && networkPlayers.ContainsKey(2))
                winnerCharacter = networkPlayers[2].GetCharacterData();

            if (winnerCharacter != null)
                matchUI.ShowMatchResult(matchWinner, winnerCharacter);
        }

        PlaySound(matchWinSound);
        yield return new WaitForSeconds(2f); // Show result briefly

        // Enable the "Return to Menu" button
        if (matchUI != null)
        {
            matchUI.ShowReturnToMenuButton(true);
        }
    }

    public void OnReturnToMenuButtonPressed()
    {
        // This will be called by the UI button
        ReturnToMainMenu();
    }

    /// <summary>
    /// REFACTORED: Called by MatchUI when return to menu is requested
    /// </summary>
    public void OnReturnToMenuRequested()
    {
        ReturnToMainMenu();
    }

    void ReturnToMainMenu()
    {
        if (debugMode)
            Debug.Log($"[MATCH MANAGER] ReturnToMainMenu called. InRoom: {PhotonNetwork.InRoom}, State: {PhotonNetwork.NetworkClientState}");

        isReturningToMenu = true;
        EnablePlayerInput(false);

        StartCoroutine(SafeReturnToMainMenu());
    }

    /// <summary>
    /// FIXED: Safe coroutine to handle main menu return without SetProperties errors
    /// </summary>
    IEnumerator SafeReturnToMainMenu()
    {
        yield return null;

        if (PhotonNetwork.InRoom &&
            PhotonNetwork.NetworkClientState != Photon.Realtime.ClientState.Leaving &&
            PhotonNetwork.NetworkClientState != Photon.Realtime.ClientState.Disconnecting)
        {
            if (debugMode)
                Debug.Log("[MATCH MANAGER] Leaving room safely...");

            PhotonNetwork.LeaveRoom();
        }
        else
        {
            if (debugMode)
                Debug.Log("[MATCH MANAGER] Already leaving or disconnected");
        }
    }

    // When the room is left, go straight to Main Menu
    public override void OnLeftRoom()
    {
        if (debugMode)
            Debug.Log("[MATCH MANAGER] Left room, loading main menu scene");

        if (isReturningToMenu &&
            SceneManager.GetActiveScene().name != mainMenuScene)
        {
            SceneManager.LoadScene(mainMenuScene);
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (debugMode)
        {
            Debug.Log($"[MATCH MANAGER] Player {otherPlayer.NickName} left the room. Match state: {currentState}");
        }

        // Handle player leaving during different match states
        switch (currentState)
        {
            case MatchState.Initializing:
            case MatchState.PreFight:
                HandlePlayerLeavePreMatch(otherPlayer);
                break;

            case MatchState.Fighting:
                HandlePlayerLeaveDuringMatch(otherPlayer);
                break;

            case MatchState.RoundEnd:
                HandlePlayerLeaveRoundEnd(otherPlayer);
                break;

            case MatchState.MatchEnd:
                // Match already ended, no special handling needed
                break;

            default:
                HandlePlayerLeaveDefault(otherPlayer);
                break;
        }
    }

    // NEW METHOD: Handle player leaving before match starts
    void HandlePlayerLeavePreMatch(Player otherPlayer)
    {
        if (debugMode)
            Debug.Log("[MATCH MANAGER] Player left before match started, returning to menu");

        // If match hasn't started yet, just return to menu
        if (matchUI != null)
        {
            matchUI.ShowMessage($"{otherPlayer.NickName} left the match", 3f);
        }

        StartCoroutine(DelayedReturnToMenu(3f));
    }

    // NEW METHOD: Handle player leaving during active match (MAIN FEATURE)
    void HandlePlayerLeaveDuringMatch(Player otherPlayer)
    {
        if (debugMode)
            Debug.Log($"[MATCH MANAGER] Player {otherPlayer.NickName} left during active match");

        // Determine winner (the remaining player)
        int winnerActorNumber = GetRemainingPlayerActorNumber();

        if (winnerActorNumber == -1)
        {
            Debug.LogError("[MATCH MANAGER] Could not determine remaining player!");
            return;
        }

        // End match immediately with forfeit win
        EndMatchByForfeit(winnerActorNumber, otherPlayer);
    }

    // NEW METHOD: Handle player leaving during round end
    void HandlePlayerLeaveRoundEnd(Player otherPlayer)
    {
        if (debugMode)
            Debug.Log($"[MATCH MANAGER] Player {otherPlayer.NickName} left during round end");

        // If we're between rounds, treat as forfeit
        int winnerActorNumber = GetRemainingPlayerActorNumber();

        if (winnerActorNumber != -1)
        {
            EndMatchByForfeit(winnerActorNumber, otherPlayer);
        }
    }

    // NEW METHOD: Default handling for other states
    void HandlePlayerLeaveDefault(Player otherPlayer)
    {
        if (debugMode)
            Debug.Log($"[MATCH MANAGER] Player {otherPlayer.NickName} left in state {currentState}");

        // Return to main menu with message
        if (matchUI != null)
        {
            matchUI.ShowMessage($"{otherPlayer.NickName} left the match", 3f);
        }

        StartCoroutine(DelayedReturnToMenu(3f));
    }

    // NEW METHOD: Get the actor number of the remaining player
    int GetRemainingPlayerActorNumber()
    {
        // Check who's still in the room
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            // Return the first (and should be only) remaining player
            return player.ActorNumber;
        }

        // If no players found, check our tracked network players
        foreach (var kvp in networkPlayers)
        {
            int actorNumber = kvp.Key;
            var playerComponent = kvp.Value;

            // Check if this player still exists in PhotonNetwork
            Player photonPlayer = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (photonPlayer != null)
            {
                return actorNumber;
            }
        }

        return -1; // No remaining player found
    }

    // NEW METHOD: End match by forfeit
    void EndMatchByForfeit(int winnerActorNumber, Player forfeitPlayer)
    {
        if (debugMode)
            Debug.Log($"[MATCH MANAGER] Ending match by forfeit. Winner: Player {winnerActorNumber}");

        // Stop any active round
        roundActive = false;
        currentState = MatchState.MatchEnd;

        // Set the winner
        matchWinner = winnerActorNumber;

        // Update round scores to reflect forfeit win
        // Give winner enough rounds to win the match
        if (winnerActorNumber == 1)
        {
            player1RoundsWon = roundsToWin; // Instant match win
        }
        else if (winnerActorNumber == 2)
        {
            player2RoundsWon = roundsToWin; // Instant match win
        }

        // Update room properties
        if (PhotonNetwork.IsMasterClient)
            UpdateRoomProperties();

        // Disable player input
        EnablePlayerInput(false);

        // Show forfeit message and results
        StartCoroutine(ShowForfeitResult(winnerActorNumber, forfeitPlayer));
    }

    // NEW COROUTINE: Show forfeit result
    IEnumerator ShowForfeitResult(int winnerActorNumber, Player forfeitPlayer)
    {
        // Show forfeit message first
        if (matchUI != null)
        {
            string forfeitMessage = $"{forfeitPlayer.NickName} forfeited the match!";
            matchUI.ShowMessage(forfeitMessage, 3f);
        }

        PlaySound(matchWinSound);
        yield return new WaitForSeconds(3f);

        // Now show match results
        if (matchUI != null)
        {
            CharacterData winnerCharacter = null;
            if (winnerActorNumber == 1 && networkPlayers.ContainsKey(1))
                winnerCharacter = networkPlayers[1].GetCharacterData();
            else if (winnerActorNumber == 2 && networkPlayers.ContainsKey(2))
                winnerCharacter = networkPlayers[2].GetCharacterData();

            if (winnerCharacter != null)
            {
                // Show special forfeit victory message
                matchUI.ShowForfeitVictory(winnerActorNumber, winnerCharacter, forfeitPlayer.NickName);
            }
            else
            {
                // Fallback to regular result display
                matchUI.ShowMatchResult(winnerActorNumber, winnerCharacter);
            }
        }

        yield return new WaitForSeconds(2f);

        // Enable return to menu button
        if (matchUI != null)
        {
            matchUI.ShowReturnToMenuButton(true);
        }
    }

    // NEW COROUTINE: Delayed return to menu
    IEnumerator DelayedReturnToMenu(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToMainMenu();
    }


    // ══════════════════════════════════════════════════════════
    // FIXED: UI SYNCHRONIZATION
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// FIXED: Sync match state to all clients for proper UI updates
    /// </summary>
    void SyncMatchStateToAll()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("ReceiveMatchStateSync", RpcTarget.Others,
                currentRound, player1RoundsWon, player2RoundsWon, roundActive, currentState);
        }
    }

    /// <summary>
    /// FIXED: Sync UI elements to all clients
    /// </summary>
    void SyncUIToAllClients()
    {
        if (matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);

            if (roundActive)
            {
                float timeRemaining = GetRemainingTime();
                matchUI.UpdateTimer(timeRemaining);
            }
        }
    }

    [PunRPC]
    void ReceiveMatchStateSync(int round, int p1Rounds, int p2Rounds, bool active, MatchState state)
    {
        currentRound = round;
        player1RoundsWon = p1Rounds;
        player2RoundsWon = p2Rounds;
        roundActive = active;
        currentState = state;

        // Update UI immediately
        SyncUIToAllClients();

        Debug.Log($"[UI SYNC] Received state: Round {round}, Score {p1Rounds}-{p2Rounds}, Active: {active}");
    }

    // ══════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ══════════════════════════════════════════════════════════

    void CacheNetworkPlayers()
    {
        networkPlayers.Clear();
        networkPlayersHealth.Clear();

        PlayerCharacter[] players = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in players)
        {
            PhotonView pv = player.GetComponent<PhotonView>();
            if (pv != null)
            {
                int actorNumber = pv.Owner.ActorNumber; // FIXED: Capital O
                networkPlayers[actorNumber] = player;

                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    networkPlayersHealth[actorNumber] = health;
                    health.OnPlayerDeath += OnPlayerDeathEvent;
                    health.OnHealthChanged += (current, max) => OnPlayerHealthChanged(actorNumber, current, max);
                }
            }
        }

        Debug.Log($"[CACHE] Cached {networkPlayers.Count} players");
    }

    void OnPlayerDeathEvent(PlayerCharacter deadPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || !roundActive) return;

        int deadPlayerActor = GetPlayerActorNumber(deadPlayer);
        if (deadPlayerActor != -1)
        {
            int winner = deadPlayerActor == 1 ? 2 : 1;
            photonView.RPC("EndRound", RpcTarget.All, winner, "knockout");
        }
    }

    int GetPlayerActorNumber(PlayerCharacter player)
    {
        foreach (var kvp in networkPlayers)
        {
            if (kvp.Value == player) return kvp.Key;
        }
        return -1;
    }

    void InitializeUI()
    {
        if (matchUI == null) return;

        CharacterData player1Data = networkPlayers.ContainsKey(1) ? networkPlayers[1].GetCharacterData() : null;
        CharacterData player2Data = networkPlayers.ContainsKey(2) ? networkPlayers[2].GetCharacterData() : null;

        if (player1Data != null && player2Data != null)
        {
            matchUI.InitializeMatch(player1Data, player2Data, player1RoundsWon, player2RoundsWon, roundsToWin);
        }

        Debug.Log("[UI] Match UI initialized");
    }

    int DetermineWinnerByHealth()
    {
        float player1Health = networkPlayersHealth.ContainsKey(1) ? networkPlayersHealth[1].GetHealthPercentage() : 0f;
        float player2Health = networkPlayersHealth.ContainsKey(2) ? networkPlayersHealth[2].GetHealthPercentage() : 0f;

        if (player1Health > player2Health) return 1;
        else if (player2Health > player1Health) return 2;
        else return 0;
    }

    void EnablePlayerInput(bool enable)
    {
        foreach (var player in networkPlayers.Values)
        {
            if (player != null)
            {
                player.SetInputEnabled(enable);
                player.SetMovementEnabled(enable);
            }
        }
    }

    void OnPlayerHealthChanged(int actorNumber, int currentHealth, int maxHealth)
    {
        if (matchUI != null)
        {
            matchUI.UpdatePlayerHealth(actorNumber, currentHealth, maxHealth);
        }
    }

    // Helper methods
    int GetSelectedCharacterIndex()
    {
        return RoomStateManager.GetOrCreateInstance()?.GetPlayerProperty<int>(
            PhotonNetwork.LocalPlayer,
            RoomStateManager.PLAYER_CHARACTER_KEY,
            0
        ) ?? 0;
    }

    public CharacterData GetCharacterData(int characterIndex)
    {
        if (characterIndex >= 0 && characterIndex < availableCharacters.Length)
            return availableCharacters[characterIndex];
        return null;
    }

    Vector3 GetSpawnPosition() => GetSpawnPositionForActor(PhotonNetwork.LocalPlayer.ActorNumber);

    /// <summary>
    /// FIXED: Safe method to set room properties with comprehensive checks
    /// </summary>
    bool SafeSetRoomProperties(Hashtable properties)
    {
        // Enhanced checks to prevent SetProperties errors during disconnection
        if (!PhotonNetwork.IsMasterClient) return false;
        if (!PhotonNetwork.IsConnected) return false;
        if (!PhotonNetwork.InRoom) return false;
        if (PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Leaving) return false;
        if (PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Disconnecting) return false;
        if (isReturningToMenu) return false; // Use your existing flag

        // Additional safety check for room state
        if (PhotonNetwork.CurrentRoom == null) return false;

        // Check if we're in the correct scene (prevent updates during scene transitions)
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameplayArena") return false;

        try
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
            return true;
        }
        catch (System.Exception ex)
        {
            if (debugMode)
                Debug.LogWarning($"[ROOM PROPS] Failed to set room properties: {ex.Message}");
            return false;
        }
    }

    void UpdateRoomProperties()
    {
        // FIXED: Don't update room properties if we're leaving
        if (isReturningToMenu) return;

        // FIXED: Use centralized room state manager with fallback
        RoomStateManager.GetOrCreateInstance()?.SetMatchState(
            (int)currentState,
            currentRound,
            player1RoundsWon,
            player2RoundsWon,
            roundStartTime,
            roundEndTime,
            roundActive
        );
    }

    void SyncFromRoomProperties()
    {
        // FIXED: Don't sync room properties if we're leaving
        if (isReturningToMenu) return;

        // Simple null check - if any of these are null, just return
        if (PhotonNetwork.CurrentRoom?.CustomProperties == null) return;

        var room = PhotonNetwork.CurrentRoom;

        // Rest of your existing code stays exactly the same...
        bool changed = false;
        bool timerChanged = false;

        // FIXED: Use centralized room state manager for reading properties with fallback
        MatchState newState = (MatchState)RoomStateManager.GetOrCreateInstance().GetRoomProperty<int>(RoomStateManager.ROOM_MATCH_STATE, (int)currentState);
        int newRound = RoomStateManager.GetOrCreateInstance().GetRoomProperty<int>(RoomStateManager.ROOM_CURRENT_ROUND, currentRound);
        int newP1Rounds = RoomStateManager.GetOrCreateInstance().GetRoomProperty<int>(RoomStateManager.ROOM_P1_ROUNDS, player1RoundsWon);
        int newP2Rounds = RoomStateManager.GetOrCreateInstance().GetRoomProperty<int>(RoomStateManager.ROOM_P2_ROUNDS, player2RoundsWon);
        bool newRoundActive = RoomStateManager.GetOrCreateInstance().GetRoomProperty<bool>(RoomStateManager.ROOM_ROUND_ACTIVE, roundActive);

        // FIXED: Also sync timer values
        double newRoundStartTime = RoomStateManager.GetOrCreateInstance().GetRoomProperty<double>(RoomStateManager.ROOM_ROUND_START_TIME, roundStartTime);
        double newRoundEndTime = RoomStateManager.GetOrCreateInstance().GetRoomProperty<double>(RoomStateManager.ROOM_ROUND_END_TIME, roundEndTime);

        // Check if anything changed
        if (currentState != newState || currentRound != newRound ||
            player1RoundsWon != newP1Rounds || player2RoundsWon != newP2Rounds ||
            roundActive != newRoundActive)
        {
            currentState = newState;
            currentRound = newRound;
            player1RoundsWon = newP1Rounds;
            player2RoundsWon = newP2Rounds;
            roundActive = newRoundActive;
            changed = true;
        }

        // Check if timer changed
        if (System.Math.Abs(roundStartTime - newRoundStartTime) > 0.1 ||
            System.Math.Abs(roundEndTime - newRoundEndTime) > 0.1)
        {
            roundStartTime = newRoundStartTime;
            roundEndTime = newRoundEndTime;
            timerChanged = true;
        }

        // Update UI if anything changed
        if (changed || timerChanged)
        {
            SyncUIToAllClients();
        }
    }
    // Debug method
    void DebugSpawnIssues()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log("=== SPAWN DEBUG ===");
            Debug.Log($"Players Spawned: {playersSpawned}");
            Debug.Log($"Tracked Players: {spawnedPlayers.Count}");
            Debug.Log($"Current Round: {currentRound}");
            Debug.Log($"Round Active: {roundActive}");
            Debug.Log($"Match State: {currentState}");
        }
    }

    // Photon callbacks
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            SyncFromRoomProperties();
        }
    }

    /*void ReturnToCharacterSelection() => PhotonNetwork.LeaveRoom();
    public override void OnLeftRoom() => SceneManager.LoadScene(characterSelectionScene);*/

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(roundActive);
            stream.SendNext(currentState);
            stream.SendNext(currentRound);
            stream.SendNext(player1RoundsWon);
            stream.SendNext(player2RoundsWon);
        }
        else
        {
            roundActive = (bool)stream.ReceiveNext();
            currentState = (MatchState)stream.ReceiveNext();
            currentRound = (int)stream.ReceiveNext();
            player1RoundsWon = (int)stream.ReceiveNext();
            player2RoundsWon = (int)stream.ReceiveNext();

            // Update UI when receiving network data
            SyncUIToAllClients();
        }
    }

    // Public API
    public MatchState GetMatchState() => currentState;
    public int GetCurrentRound() => currentRound;
    public float GetRemainingTime()
    {
        if (!roundActive || roundEndTime <= 0.0)
            return 0f;

        // Use PhotonNetwork.Time for synchronized timing across all clients
        double timeRemaining = roundEndTime - PhotonNetwork.Time;
        return Mathf.Max(0f, (float)timeRemaining);
    }
    public bool IsRoundActive() => roundActive;
    public void GetRoundScores(out int player1Wins, out int player2Wins)
    {
        player1Wins = player1RoundsWon;
        player2Wins = player2RoundsWon;
    }
    public CharacterData[] GetAvailableCharacters() => availableCharacters;
}

