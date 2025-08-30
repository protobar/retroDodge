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

    // Network management - room property keys
    private const string ROOM_MATCH_STATE = "MatchState";
    private const string ROOM_CURRENT_ROUND = "CurrentRound";
    private const string ROOM_P1_ROUNDS = "P1Rounds";
    private const string ROOM_P2_ROUNDS = "P2Rounds";
    private const string ROOM_ROUND_START_TIME = "RoundStartTime";
    private const string ROOM_ROUND_END_TIME = "RoundEndTime";
    private const string ROOM_ROUND_ACTIVE = "RoundActive";
    private const string PLAYERS_SPAWNED_KEY = "PlayersSpawned";
    private const string PLAYER_CHARACTER_KEY = "SelectedCharacter";

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
        // FIXED: Sync timer AND round info to ALL clients, not just master
        if (roundActive && matchUI != null)
        {
            // Calculate remaining time using network time
            float remainingTime = 0f;
            if (roundEndTime > 0.0)
            {
                remainingTime = Mathf.Max(0f, (float)(roundEndTime - PhotonNetwork.Time));
            }

            // Update UI on ALL clients (both master and remote)
            matchUI.UpdateTimer(remainingTime);
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);

            // Only Master Client checks for timeout and manages room properties
            if (PhotonNetwork.IsMasterClient && remainingTime <= 0f)
            {
                EndRoundByTimeOut();
            }
        }

        // Sync match state from room properties for non-master clients
        if (!PhotonNetwork.IsMasterClient)
        {
            SyncFromRoomProperties();
        }

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
                if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PLAYERS_SPAWNED_KEY))
                {
                    bool shouldSpawn = (bool)PhotonNetwork.CurrentRoom.CustomProperties[PLAYERS_SPAWNED_KEY];
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
        Hashtable roomProps = new Hashtable { [PLAYERS_SPAWNED_KEY] = false };
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        yield return new WaitForSeconds(0.2f);

        // Spawn master client first
        SpawnMyPlayer();
        yield return new WaitForSeconds(0.5f);

        // Signal others to spawn
        photonView.RPC("SpawnMyPlayer", RpcTarget.Others);
        yield return new WaitForSeconds(1f);

        // Mark spawning complete
        roomProps[PLAYERS_SPAWNED_KEY] = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

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
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);

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

        // FIXED: Sync timer start for all clients
        if (PhotonNetwork.IsMasterClient)
        {
            roundStartTime = PhotonNetwork.Time;
            roundEndTime = roundStartTime + roundDuration;
            UpdateRoomProperties();
        }

        // Enable player input
        EnablePlayerInput(true);

        // Update UI immediately
        SyncUIToAllClients();
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

        if (PhotonNetwork.IsMasterClient)
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
        yield return new WaitForSeconds(matchEndDelay);

        ReturnToCharacterSelection();
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
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PLAYER_CHARACTER_KEY))
            return (int)PhotonNetwork.LocalPlayer.CustomProperties[PLAYER_CHARACTER_KEY];
        return 0;
    }

    public CharacterData GetCharacterData(int characterIndex)
    {
        if (characterIndex >= 0 && characterIndex < availableCharacters.Length)
            return availableCharacters[characterIndex];
        return null;
    }

    Vector3 GetSpawnPosition() => GetSpawnPositionForActor(PhotonNetwork.LocalPlayer.ActorNumber);

    void UpdateRoomProperties()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Hashtable props = new Hashtable
        {
            [ROOM_MATCH_STATE] = (int)currentState,
            [ROOM_CURRENT_ROUND] = currentRound,
            [ROOM_P1_ROUNDS] = player1RoundsWon,
            [ROOM_P2_ROUNDS] = player2RoundsWon,
            [ROOM_ROUND_START_TIME] = roundStartTime,
            [ROOM_ROUND_END_TIME] = roundEndTime,
            [ROOM_ROUND_ACTIVE] = roundActive
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    void SyncFromRoomProperties()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room?.CustomProperties == null) return;

        bool changed = false;

        if (room.CustomProperties.ContainsKey(ROOM_MATCH_STATE))
        {
            MatchState newState = (MatchState)(int)room.CustomProperties[ROOM_MATCH_STATE];
            int newRound = (int)room.CustomProperties[ROOM_CURRENT_ROUND];
            int newP1Rounds = (int)room.CustomProperties[ROOM_P1_ROUNDS];
            int newP2Rounds = (int)room.CustomProperties[ROOM_P2_ROUNDS];
            double newRoundStartTime = (double)room.CustomProperties[ROOM_ROUND_START_TIME];
            double newRoundEndTime = (double)room.CustomProperties[ROOM_ROUND_END_TIME];
            bool newRoundActive = (bool)room.CustomProperties[ROOM_ROUND_ACTIVE];

            // Check if anything changed
            if (currentState != newState || currentRound != newRound ||
                player1RoundsWon != newP1Rounds || player2RoundsWon != newP2Rounds ||
                roundActive != newRoundActive)
            {
                currentState = newState;
                currentRound = newRound;
                player1RoundsWon = newP1Rounds;
                player2RoundsWon = newP2Rounds;
                roundStartTime = newRoundStartTime;
                roundEndTime = newRoundEndTime;
                roundActive = newRoundActive;
                changed = true;
            }
        }

        // Update UI if anything changed
        if (changed && matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);

            if (roundActive && roundEndTime > 0.0)
            {
                float remainingTime = Mathf.Max(0f, (float)(roundEndTime - PhotonNetwork.Time));
                matchUI.UpdateTimer(remainingTime);
            }
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

    void ReturnToCharacterSelection() => PhotonNetwork.LeaveRoom();
    public override void OnLeftRoom() => SceneManager.LoadScene(characterSelectionScene);

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
        if (!roundActive || roundEndTime <= 0.0) return 0f;
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