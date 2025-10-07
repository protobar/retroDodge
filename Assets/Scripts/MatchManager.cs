using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using RetroDodge.Progression;

/// <summary>
/// FIXED: MatchManager with NO DUPLICATE SPAWNING and proper UI sync
/// KEY FIXES: Removed EnsureCharacterDataApplied coroutine, Fixed Owner vs owner, Added UI sync
/// </summary>
public class MatchManager : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Match Settings")]
    [SerializeField] private int roundsToWin = 2;
    [SerializeField] private float defaultRoundDuration = 90f; // Fallback if no room settings
    [SerializeField] private float preFightCountdown = 3f;
    [SerializeField] private float postRoundDelay = 3f;
    [SerializeField] private float matchEndDelay = 5f;
    
    [Header("Competitive Series Settings")]
    [SerializeField] private bool isCompetitiveMode = false;
    [SerializeField] private int currentSeriesMatch = 1;
    [SerializeField] private int player1SeriesWins = 0;
    [SerializeField] private int player2SeriesWins = 0;
    [SerializeField] private int maxSeriesMatches = 9;
    [SerializeField] private bool seriesCompleted = false;
    [SerializeField] private int seriesWinner = -1;
    [SerializeField] private int competitiveRoundsToWin = 3; // Competitive matches are best of 3 rounds

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
    
    [Header("Progression System")]
    [SerializeField] private MatchResultHandler matchResultHandler;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip roundStartSound;
    [SerializeField] private AudioClip roundEndSound;
    [SerializeField] private AudioClip matchWinSound;
    [SerializeField] private AudioClip countdownSound;

    [Header("Camera")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private CameraShakeManager cameraShakeManager;

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
    
    /// <summary>
    /// Get round duration from room settings or use default
    /// </summary>
    private float GetRoundDuration()
    {
        // FIXED: Read match length directly from room properties
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("ML"))
        {
            int matchLength = (int)PhotonNetwork.CurrentRoom.CustomProperties["ML"];
            if (debugMode) Debug.Log($"[MATCH MANAGER] Using match length from room properties: {matchLength} seconds");
            return matchLength;
        }
        
        // Fallback to RoomStateManager if available
        if (RoomStateManager.Instance != null)
        {
            RoomSettings roomSettings = RoomStateManager.Instance.GetRoomSettings();
            if (roomSettings != null)
            {
                if (debugMode) Debug.Log($"[MATCH MANAGER] Using match length from RoomStateManager: {roomSettings.matchLengthSeconds} seconds");
                return roomSettings.matchLengthSeconds;
            }
        }
        
        if (debugMode) Debug.Log($"[MATCH MANAGER] Using default match length: {defaultRoundDuration} seconds");
        return defaultRoundDuration;
    }

    void Start()
    {
        // Check if this is a competitive series
        CheckCompetitiveMode();
        
        if (PhotonNetwork.OfflineMode && RetroDodge.AISessionConfig.Instance.withAI)
        {
            StartCoroutine(StartOfflineAIFlow());
        }
        else
        {
            StartCoroutine(CompleteMatchFlow());
        }
    }
    
    void CheckCompetitiveMode()
    {
        if (PhotonNetwork.OfflineMode) return;
        
        if (PhotonNetwork.CurrentRoom?.CustomProperties != null)
        {
            // Check if this is a competitive room
            int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.ROOM_TYPE_KEY) ? 
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.ROOM_TYPE_KEY] : 0;
            
            if (roomType == 2) // Competitive mode
            {
                isCompetitiveMode = true;
                
                // Load series data from room properties
                currentSeriesMatch = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.CURRENT_MATCH) ? 
                                   (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.CURRENT_MATCH] : 1;
                player1SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER1) ? 
                                   (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER1] : 0;
                player2SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER2) ? 
                                   (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER2] : 0;
                maxSeriesMatches = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_MAX_MATCHES) ? 
                                  (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_MAX_MATCHES] : 9;
                seriesCompleted = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_COMPLETED) ? 
                                (bool)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_COMPLETED] : false;
                seriesWinner = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINNER) ? 
                              (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINNER] : -1;
                
                if (debugMode) Debug.Log($"[COMPETITIVE] Series Match {currentSeriesMatch}/{maxSeriesMatches}, P1: {player1SeriesWins}, P2: {player2SeriesWins}");
            }
        }
    }
    
    int GetRoundsToWin()
    {
        int rounds = isCompetitiveMode ? competitiveRoundsToWin : roundsToWin;
        if (debugMode) Debug.Log($"[ROUNDS] GetRoundsToWin - IsCompetitive: {isCompetitiveMode}, CompetitiveRounds: {competitiveRoundsToWin}, QuickRounds: {roundsToWin}, Returning: {rounds}");
        return rounds;
    }

    void OnDestroy()
    {
        // Stop all coroutines to prevent SerializedObject errors during scene transition
        StopAllCoroutines();
        
        // Clean up any remaining references
        isReturningToMenu = true;
        
        if (debugMode)
            Debug.Log("[MATCH MANAGER] OnDestroy - cleaned up coroutines and references");
    }

    /// <summary>
    /// Apply progression rewards when a match ends
    /// </summary>
    private void ApplyProgressionRewards()
    {
        // Try to find MatchResultHandler if not assigned
        if (matchResultHandler == null)
        {
            matchResultHandler = FindObjectOfType<RetroDodge.Progression.MatchResultHandler>();
            if (matchResultHandler == null)
            {
                Debug.LogWarning("[MatchManager] MatchResultHandler not found. Progression rewards will not be applied.");
                return;
            }
            else
            {
                Debug.Log("[MatchManager] Found MatchResultHandler and assigned it.");
            }
        }

        // Determine game mode
        GameMode gameMode = GameMode.Casual;
        if (isCompetitiveMode)
        {
            gameMode = GameMode.Competitive;
        }
        else if (PhotonNetwork.IsMasterClient && networkPlayers.ContainsKey(2) && 
                 networkPlayers[2].GetComponent<RetroDodge.AIControllerBrain>() != null)
        {
            gameMode = GameMode.AI;
        }
        else if (PhotonNetwork.CurrentRoom?.CustomProperties != null)
        {
            // Check for custom room type
            int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.ROOM_TYPE_KEY) ? 
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.ROOM_TYPE_KEY] : 0;
            
            if (roomType == 1) // Custom room type
            {
                gameMode = GameMode.Custom;
            }
        }
        
        if (debugMode) Debug.Log($"[MatchManager] Detected game mode: {gameMode}");

        // Get match data
        bool localPlayerWon = (matchWinner == 1 && PhotonNetwork.IsMasterClient) || 
                             (matchWinner == 2 && !PhotonNetwork.IsMasterClient);
        
        int localPlayerRoundsWon = PhotonNetwork.IsMasterClient ? player1RoundsWon : player2RoundsWon;
        int opponentRoundsWon = PhotonNetwork.IsMasterClient ? player2RoundsWon : player1RoundsWon;
        
        // Calculate match duration
        float matchDuration = (float)(PhotonNetwork.Time - roundStartTime);
        
        // Get character used by local player
        string characterUsed = "Unknown";
        if (PhotonNetwork.IsMasterClient && networkPlayers.ContainsKey(1))
        {
            var characterData = networkPlayers[1].GetCharacterData();
            characterUsed = characterData != null ? characterData.characterName : "Unknown";
        }
        else if (!PhotonNetwork.IsMasterClient && networkPlayers.ContainsKey(2))
        {
            var characterData = networkPlayers[2].GetCharacterData();
            characterUsed = characterData != null ? characterData.characterName : "Unknown";
        }
        
        // Get damage dealt/taken by local player
        int damageDealt = 0;
        int damageTaken = 0;
        if (PhotonNetwork.IsMasterClient && networkPlayers.ContainsKey(1))
        {
            var playerHealth = networkPlayers[1].GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                damageDealt = playerHealth.damageDealt;
                damageTaken = playerHealth.damageTaken;
            }
        }
        else if (!PhotonNetwork.IsMasterClient && networkPlayers.ContainsKey(2))
        {
            var playerHealth = networkPlayers[2].GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                damageDealt = playerHealth.damageDealt;
                damageTaken = playerHealth.damageTaken;
            }
        }
        
        // Get opponent SR for competitive matches
        int opponentSR = 0;
        if (gameMode == GameMode.Competitive && PlayerDataManager.Instance != null)
        {
            var playerData = PlayerDataManager.Instance.GetPlayerData();
            opponentSR = playerData.competitiveSR; // Use current SR as opponent SR for now
        }
        
        // Create and process match result
        var matchResult = matchResultHandler.CreateMatchResult(
            gameMode: gameMode,
            isWin: localPlayerWon,
            finalScore: localPlayerRoundsWon,
            matchDuration: matchDuration,
            characterUsed: characterUsed,
            damageDealt: damageDealt,
            damageTaken: damageTaken,
            opponentSR: opponentSR,
            currentSeriesMatch: isCompetitiveMode ? currentSeriesMatch : 1,
            totalSeriesMatches: isCompetitiveMode ? maxSeriesMatches : 1,
            seriesCompleted: isCompetitiveMode ? seriesCompleted : true,
            seriesWinner: isCompetitiveMode ? seriesWinner : (localPlayerWon ? 1 : 2)
        );
        
        // Process the match result
        matchResultHandler.ProcessMatchResult(matchResult);
        
        if (debugMode) Debug.Log($"[MatchManager] Applied progression rewards for {gameMode} match. Win: {localPlayerWon}");
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

        Debug.Log($"[ROUND END REQUEST] Requesting round end. Winner: {winner}, Reason: {reason}, OfflineMode: {PhotonNetwork.OfflineMode}");

        if (PhotonNetwork.OfflineMode)
        {
            EndRound(winner, reason);
        }
        else if (PhotonNetwork.IsMasterClient)
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

        // Refresh camera to find new players
        if (cameraManager != null)
        {
            cameraManager.RefreshCamera();
        }
        
        // Initialize camera shake manager
        if (cameraShakeManager == null)
        {
            cameraShakeManager = CameraShakeManager.Instance;
        }
        if (cameraShakeManager != null)
        {
            cameraShakeManager.RefreshCameraController();
        }

        // Sync initial state to all clients
        SyncMatchStateToAll();

        // Only Master Client starts the first round
        if (PhotonNetwork.IsMasterClient)
        {
            StartRound(1);
        }
    }

    IEnumerator StartOfflineAIFlow()
    {
        currentState = MatchState.Initializing;

        // Ensure OfflineMode networking is configured
        PhotonNetwork.AutomaticallySyncScene = false;

        // Spawn two local players without PUN Instantiate to reuse prefabs quickly
        yield return StartCoroutine(SpawnOfflinePlayers());

        CacheNetworkPlayers();
        AssignPlayerSides();
        InitializeUI();

        // Refresh camera to find new players
        if (cameraManager != null)
        {
            cameraManager.RefreshCamera();
        }
        
        // Initialize camera shake manager
        if (cameraShakeManager == null)
        {
            cameraShakeManager = CameraShakeManager.Instance;
        }
        if (cameraShakeManager != null)
        {
            cameraShakeManager.RefreshCameraController();
        }

        // Start match with proper round initialization
        StartRound(1);
    }

    // REPLACE the entire SpawnOfflinePlayers() method in MatchManager.cs with this:

    IEnumerator SpawnOfflinePlayers()
    {
        playersSpawned = true;

        // Read from AI session config filled by CharacterSelection
        var cfg = RetroDodge.AISessionConfig.Instance;
        int playerIndex = (cfg != null && cfg.playerCharacterIndex >= 0) ? cfg.playerCharacterIndex : GetSelectedCharacterIndex();
        if (playerIndex < 0 || playerIndex >= availableCharacters.Length) playerIndex = 0;
        CharacterData playerChar = GetCharacterData(playerIndex);

        // AI character selection
        int aiIndex = (cfg != null && cfg.aiCharacterIndex >= 0) ? cfg.aiCharacterIndex : -1;
        if (aiIndex < 0 || aiIndex >= availableCharacters.Length)
        {
            aiIndex = Random.Range(0, availableCharacters.Length);
        }
        CharacterData aiChar = GetCharacterData(aiIndex);

        // Spawn positions
        Vector3 p1Pos = player1SpawnPoint != null ? player1SpawnPoint.position : new Vector3(-3f, 0.5f, 0f);
        Vector3 p2Pos = player2SpawnPoint != null ? player2SpawnPoint.position : new Vector3(3f, 0.5f, 0f);

        // Human player (local) on left
        GameObject p1 = Instantiate(playerChar.characterPrefab, p1Pos, Quaternion.identity);
        var p1PC = p1.GetComponent<PlayerCharacter>() ?? p1.GetComponentInChildren<PlayerCharacter>();
        if (p1PC != null)
        {
            p1PC.LoadCharacter(playerChar);
        }
        TrackSpawnedPlayer(p1);

        // AI player on right - FIXED: Spawn with 180° rotation
        GameObject p2 = Instantiate(aiChar.characterPrefab, p2Pos, Quaternion.Euler(0, 180f, 0));
        var p2PC = p2.GetComponent<PlayerCharacter>() ?? p2.GetComponentInChildren<PlayerCharacter>();
        if (p2PC != null)
        {
            p2PC.LoadCharacter(aiChar);
        }
        TrackSpawnedPlayer(p2);

        // Add AI brain to AI object with selected difficulty
        var brain = p2.AddComponent<RetroDodge.AIControllerBrain>();
        RetroDodge.AI.AIDifficulty difficulty = (cfg != null) ? cfg.difficulty : RetroDodge.AI.AIDifficulty.Normal;
        brain.SetDifficulty(difficulty);

        // Disable network flags
        foreach (var pc in new[] { p1PC, p2PC })
        {
            if (pc != null)
            {
                var ih = pc.GetInputHandler();
                if (ih == null) ih = pc.gameObject.AddComponent<PlayerInputHandler>();
                ih.isPUN2Enabled = false;
            }
        }

        // Explicitly set player sides for offline (and configure restrictor)
        if (p1PC != null)
        {
            p1PC.SetPlayerSide(1);
            var r = p1PC.GetComponent<ArenaMovementRestrictor>();
            if (r != null) r.SetPlayerSide(ArenaMovementRestrictor.PlayerSide.Left);

            Debug.Log($"✅ Player 1 spawned at {p1Pos}, rotation: {p1.transform.rotation.eulerAngles.y}°");
        }
        if (p2PC != null)
        {
            p2PC.SetPlayerSide(2);
            var r = p2PC.GetComponent<ArenaMovementRestrictor>();
            if (r != null) r.SetPlayerSide(ArenaMovementRestrictor.PlayerSide.Right);

            // CRITICAL FIX: Force 180° rotation on AI player
            p2.transform.rotation = Quaternion.Euler(0, 180f, 0);

            Debug.Log($"✅ AI Player spawned at {p2Pos}, rotation: {p2.transform.rotation.eulerAngles.y}°");
        }

        // Ensure a BallManager exists and spawns a ball in OfflineMode
        var ballManager = FindObjectOfType<BallManager>();

        yield return null;
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

        // FIXED: Determine rotation based on spawn side
        // Player 1 (left side) = 0° rotation (faces right)
        // Player 2 (right side) = 180° rotation (faces left)
        Quaternion spawnRotation = Quaternion.identity;
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;

        // Determine if this is player 2 (right side)
        bool isPlayer2 = myActorNumber != 1 && PhotonNetwork.PlayerList[0].ActorNumber != myActorNumber;

        if (isPlayer2)
        {
            spawnRotation = Quaternion.Euler(0, 180f, 0);
            Debug.Log($"[SPAWN] Player 2 detected - spawning with 180° rotation");
        }
        else
        {
            Debug.Log($"[SPAWN] Player 1 detected - spawning with 0° rotation");
        }

        Debug.Log($"[SPAWN] {selectedCharacter.characterName} at {spawnPosition} with rotation {spawnRotation.eulerAngles.y}° for Actor {myActorNumber}");

        // Store character data in player properties BEFORE spawning
        StorePlayerCharacterData(selectedCharacter, selectedCharacterIndex);

        // Spawn player with correct rotation
        GameObject playerObj = PhotonNetwork.Instantiate(
            selectedCharacter.characterPrefab.name,
            spawnPosition,
            spawnRotation  // FIXED: Use calculated rotation instead of Quaternion.identity
        );

        PlayerCharacter playerCharacter = playerObj.GetComponent<PlayerCharacter>();
        if (playerCharacter != null)
        {
            // Load character data immediately
            playerCharacter.LoadCharacter(selectedCharacter);

            // FIXED: Network sync with rotation info
            photonView.RPC("ApplyCharacterDataToPlayer", RpcTarget.All,
                playerObj.GetComponent<PhotonView>().ViewID,
                selectedCharacterIndex,
                isPlayer2); // Pass player side info

            // Track this player
            TrackSpawnedPlayer(playerObj);

            Debug.Log($"[SPAWN SUCCESS] {selectedCharacter.characterName} ready with rotation {spawnRotation.eulerAngles.y}°");
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
    void ApplyCharacterDataToPlayer(int viewID, int characterIndex, bool isPlayer2)
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

        // FIXED: Force 180° rotation for player 2
        if (isPlayer2)
        {
            playerCharacter.transform.rotation = Quaternion.Euler(0, 180f, 0);
            Debug.Log($"[SYNC] Applied 180° rotation to Player 2 (ViewID: {viewID})");
        }

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

        if (PhotonNetwork.OfflineMode)
        {
            if (allPlayers.Length >= 2)
            {
                // Determine left/right by x-position
                System.Array.Sort(allPlayers, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                allPlayers[0].SetPlayerSide(1); // Left
                allPlayers[1].SetPlayerSide(2); // Right
            }
            else if (allPlayers.Length == 1)
            {
                // Single player fallback
                allPlayers[0].SetPlayerSide(1);
            }
        }
        else
        {
            foreach (PlayerCharacter player in allPlayers)
            {
                if (player.photonView != null && player.photonView.Owner != null)
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
        if (!PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient) return;

        currentRound = roundNumber;
        currentState = MatchState.PreFight;
        roundActive = false;
        roundStartTime = 0.0;
        roundEndTime = 0.0;

        if (!PhotonNetwork.OfflineMode)
        {
            UpdateRoomProperties();
        }

        // Reset players for new round
        if (roundNumber > 1)
        {
            // Subsequent rounds - reset positions and health
            if (PhotonNetwork.OfflineMode)
            {
                ResetPlayersForNewRound();
            }
            else
            {
                photonView.RPC("ResetPlayersForNewRound", RpcTarget.All);
            }
        }
        else
        {
            // First round - prepare players
            if (PhotonNetwork.OfflineMode)
            {
                PreparePlayersForFirstRound();
            }
            else
            {
                photonView.RPC("PreparePlayersForFirstRound", RpcTarget.All);
            }
        }

        // Start countdown sequence for all rounds
        if (PhotonNetwork.OfflineMode)
        {
            StartCoroutine(PreFightSequenceCoroutine());
        }
        else
        {
            photonView.RPC("StartPreFightSequence", RpcTarget.All);
        }
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
            if (player != null)
            {
                // Reset position
                Vector3 resetPosition;
                int side = player.GetPlayerSide();

                if (PhotonNetwork.OfflineMode)
                {
                    resetPosition = side == 2 ? (player2SpawnPoint != null ? player2SpawnPoint.position : new Vector3(3f, 0.5f, 0f))
                                              : (player1SpawnPoint != null ? player1SpawnPoint.position : new Vector3(-3f, 0.5f, 0f));
                }
                else
                {
                    int actorNumber = player.photonView.Owner.ActorNumber;
                    resetPosition = GetSpawnPositionForActor(actorNumber);
                }

                player.transform.position = resetPosition;

                // CRITICAL FIX: Reapply rotation based on side
                if (side == 2)
                {
                    player.transform.rotation = Quaternion.Euler(0, 180f, 0);
                    Debug.Log($"[ROUND RESET] Reapplied 180° rotation to Player 2");
                }
                else
                {
                    player.transform.rotation = Quaternion.Euler(0, 0f, 0);
                    Debug.Log($"[ROUND RESET] Reapplied 0° rotation to Player 1");
                }

                // CRITICAL FIX: Reset player state first, THEN health
                player.ResetPlayerState();

                // FIXED: Properly reset health on the player's owner
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null && (PhotonNetwork.OfflineMode || (player.photonView != null && player.photonView.IsMine)))
                {
                    health.ResetHealthForNewRound();
                }

                // FIXED: Reapply character data and color to prevent color loss
                CharacterData characterData = player.GetCharacterData();
                if (characterData != null)
                {
                    player.LoadCharacter(characterData);

                    // Force reapply network color
                    if (!PhotonNetwork.OfflineMode && player.photonView.Owner.CustomProperties.ContainsKey("CharacterColor_R"))
                    {
                        float r = (float)player.photonView.Owner.CustomProperties["CharacterColor_R"];
                        float g = (float)player.photonView.Owner.CustomProperties["CharacterColor_G"];
                        float b = (float)player.photonView.Owner.CustomProperties["CharacterColor_B"];
                        Color networkColor = new Color(r, g, b, 1f);
                        player.ForceApplyColor(networkColor);
                    }
                }

                Debug.Log($"[ROUND RESET] Player reset to {resetPosition} with rotation {player.transform.rotation.eulerAngles.y}°");
            }
        }

        // FIXED: Reset ball properly with network authority
        ResetBallForNewRound();

        Debug.Log("[ROUND RESET] All players and ball reset completed");
    }

    void ResetBallForNewRound()
    {
        // FIXED: All clients can reset ball, not just master client
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
                if (PhotonNetwork.OfflineMode || ball.photonView.IsMine)
                {
                    ball.ResetBall();
                    break; // Only reset one ball
                }
            }
        }

        if (PhotonNetwork.OfflineMode)
        {
            Debug.Log($"[BALL RESET] Ball reset in offline mode");
        }
        else
        {
            Debug.Log($"[BALL RESET] Ball reset by client {PhotonNetwork.LocalPlayer.ActorNumber}");
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
                // Position at spawn points for offline or derive by actor for online
                Vector3 spawnPos;
                if (PhotonNetwork.OfflineMode)
                {
                    int side = player.GetPlayerSide();
                    spawnPos = side == 2 ? (player2SpawnPoint != null ? player2SpawnPoint.position : new Vector3(3f, 0.5f, 0f))
                                          : (player1SpawnPoint != null ? player1SpawnPoint.position : new Vector3(-3f, 0.5f, 0f));
                }
                else
                {
                    int actorNumber = player.photonView.Owner.ActorNumber;
                    spawnPos = GetSpawnPositionForActor(actorNumber);
                }
                spawnPos.y = Mathf.Max(spawnPos.y, 0.5f);
                player.transform.position = spawnPos;

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

        // Start the fight phase
        if (PhotonNetwork.OfflineMode)
        {
            StartFightPhase();
        }
        else if (PhotonNetwork.IsMasterClient)
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
        if (PhotonNetwork.IsMasterClient || PhotonNetwork.OfflineMode)
        {
            roundStartTime = PhotonNetwork.Time;
            roundEndTime = roundStartTime + GetRoundDuration();
            
            if (!PhotonNetwork.OfflineMode)
            {
                UpdateRoomProperties();
                // Immediately sync to all clients
                photonView.RPC("SyncTimerValues", RpcTarget.Others, roundStartTime, roundEndTime);
            }
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
        if (!roundActive) return;

        int winner = DetermineWinnerByHealth();
        if (PhotonNetwork.OfflineMode)
        {
            EndRound(winner, "timeout");
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("EndRound", RpcTarget.All, winner, "timeout");
        }
    }

    [PunRPC]
    void EndRound(int winner, string reason)
    {
        if (!roundActive) return;

        Debug.Log($"[END ROUND] Round {currentRound} ended. Winner: {winner}, Reason: {reason}. Score: P1={player1RoundsWon}, P2={player2RoundsWon}");

        roundActive = false;
        currentState = MatchState.RoundEnd;
        EnablePlayerInput(false);

        // Update round scores
        if (PhotonNetwork.OfflineMode)
        {
            // Offline mode: update directly
            if (winner == 1) player1RoundsWon++;
            else if (winner == 2) player2RoundsWon++;
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            // Online mode: Master Client manages the data
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

        // FIXED: Reset ball on ALL clients when round ends
        ResetBallForNewRound();

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

        int requiredRounds = GetRoundsToWin();
        
        // All clients can check for match end
        if (player1RoundsWon >= requiredRounds || player2RoundsWon >= requiredRounds)
        {
            matchWinner = player1RoundsWon >= requiredRounds ? 1 : 2;

            if (debugMode) Debug.Log($"[MATCH END] {(isCompetitiveMode ? "Competitive" : "Quick")} match ended - P1: {player1RoundsWon}, P2: {player2RoundsWon}, Required: {requiredRounds}, Winner: {matchWinner}");

            // FIXED: Show match results on ALL clients
            StartCoroutine(EndMatchSequence());
        }
        else if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
        {
            // Start next round (offline mode or master client)
            StartCoroutine(NextRoundDelay());
        }
    }

    IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(postRoundDelay);

        if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
        {
            StartRound(currentRound + 1);
        }
    }

    IEnumerator EndMatchSequence()
    {
        currentState = MatchState.MatchEnd;

        // Disable AFK detection for all players
        DisableAFKDetectionForAllPlayers();

        // FIXED: Only update room properties if we're still connected and able to
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            UpdateRoomProperties();
        }

        // Handle competitive series progression
        if (isCompetitiveMode && PhotonNetwork.IsMasterClient)
        {
            if (debugMode) Debug.Log($"[COMPETITIVE] Calling HandleCompetitiveSeriesProgression - Match Winner: {matchWinner}");
            HandleCompetitiveSeriesProgression();
        }
        else if (isCompetitiveMode && !PhotonNetwork.IsMasterClient)
        {
            // Non-master clients: sync series data from room properties
            if (PhotonNetwork.CurrentRoom?.CustomProperties != null)
            {
                player1SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER1) ? 
                                   (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER1] : player1SeriesWins;
                player2SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER2) ? 
                                   (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER2] : player2SeriesWins;
                currentSeriesMatch = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.CURRENT_MATCH) ? 
                                   (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.CURRENT_MATCH] : currentSeriesMatch;
                seriesCompleted = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_COMPLETED) ? 
                                 (bool)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_COMPLETED] : false;
                seriesWinner = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINNER) ? 
                              (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINNER] : -1;
                
                if (debugMode) Debug.Log($"[COMPETITIVE] Non-master synced series data - P1: {player1SeriesWins}, P2: {player2SeriesWins}");
            }
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
            {
                if (isCompetitiveMode)
                {
                    // FIXED: Reload series wins from room properties to get updated values
                    if (PhotonNetwork.CurrentRoom?.CustomProperties != null)
                    {
                        player1SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER1) ? 
                                           (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER1] : player1SeriesWins;
                        player2SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER2) ? 
                                           (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER2] : player2SeriesWins;
                    }
                    
                    // Show series progress in competitive mode
                    if (debugMode) Debug.Log($"[COMPETITIVE] Showing match result - P1 Wins: {player1SeriesWins}, P2 Wins: {player2SeriesWins}");
                    matchUI.ShowCompetitiveMatchResult(matchWinner, winnerCharacter, currentSeriesMatch, maxSeriesMatches, player1SeriesWins, player2SeriesWins);
                }
                else
                {
                    matchUI.ShowMatchResult(matchWinner, winnerCharacter);
                }
            }
        }

        PlaySound(matchWinSound);
        yield return new WaitForSeconds(2f); // Show result briefly

        // Apply progression rewards
        ApplyProgressionRewards();

        // Check if series is complete
        if (isCompetitiveMode && seriesCompleted)
        {
            // Series is complete, show final results
            if (matchUI != null)
            {
                matchUI.ShowSeriesCompleteResult(seriesWinner, player1SeriesWins, player2SeriesWins);
            }
            yield return new WaitForSeconds(3f);
        }

        // Enable the "Return to Menu" button
        if (matchUI != null)
        {
            matchUI.ShowReturnToMenuButton(true);
        }
    }
    
    void HandleCompetitiveSeriesProgression()
    {
        if (!isCompetitiveMode || !PhotonNetwork.IsMasterClient) return;
        
        // Update series result
        bool success = RoomStateManager.GetOrCreateInstance()?.UpdateSeriesResult(matchWinner) ?? false;
        
        if (success)
        {
            // Reload series data from room properties
            currentSeriesMatch = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.CURRENT_MATCH) ? 
                               (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.CURRENT_MATCH] : currentSeriesMatch;
            player1SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER1) ? 
                               (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER1] : player1SeriesWins;
            player2SeriesWins = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINS_PLAYER2) ? 
                               (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER2] : player2SeriesWins;
            seriesCompleted = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_COMPLETED) ? 
                             (bool)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_COMPLETED] : false;
            seriesWinner = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.SERIES_WINNER) ? 
                          (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINNER] : -1;
            
            if (debugMode) Debug.Log($"[COMPETITIVE] Series updated - Match {currentSeriesMatch}/{maxSeriesMatches}, P1: {player1SeriesWins}, P2: {player2SeriesWins}, Completed: {seriesCompleted}");
            if (debugMode) Debug.Log($"[COMPETITIVE] Room properties - P1 Wins: {PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER1]}, P2 Wins: {PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.SERIES_WINS_PLAYER2]}");
        }
        else
        {
            if (debugMode) Debug.LogError("[COMPETITIVE] Failed to update series result");
        }
    }

    /// <summary>
    /// Called by MatchUI when return to menu is requested
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
    /// Sync camera shake across network
    /// </summary>
    public void SyncCameraShake(float intensity, float duration, string source)
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            photonView.RPC("SyncCameraShakeRPC", RpcTarget.Others, intensity, duration, source);
        }
    }
    
    /// <summary>
    /// RPC to sync camera shake to other clients
    /// </summary>
    [PunRPC]
    void SyncCameraShakeRPC(float intensity, float duration, string source)
    {
        if (cameraShakeManager != null)
        {
            cameraShakeManager.SyncShakeFromNetwork(intensity, duration, source);
        }
    }

    /// <summary>
    /// FIXED: Safe coroutine to handle main menu return without SetProperties errors
    /// </summary>
    IEnumerator SafeReturnToMainMenu()
    {
        yield return null;

        // Safety check - ensure this object is still valid
        if (this == null || gameObject == null)
        {
            if (debugMode)
                Debug.Log("[MATCH MANAGER] Object destroyed, skipping return to menu");
            yield break;
        }

        if (PhotonNetwork.OfflineMode)
        {
            if (debugMode)
                Debug.Log("[MATCH MANAGER] Offline mode - returning to main menu directly");

            // In offline mode, go directly to main menu
            PhotonNetwork.OfflineMode = false; // Reset offline mode
            SceneManager.LoadScene(mainMenuScene);
        }
        else if (PhotonNetwork.InRoom &&
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
        // Safety check - ensure this object is still valid
        if (this == null || gameObject == null)
        {
            if (debugMode)
                Debug.Log("[MATCH MANAGER] Object destroyed, skipping OnLeftRoom");
            return;
        }

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
        int requiredRounds = GetRoundsToWin();
        if (winnerActorNumber == 1)
        {
            player1RoundsWon = requiredRounds; // Instant match win
        }
        else if (winnerActorNumber == 2)
        {
            player2RoundsWon = requiredRounds; // Instant match win
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
            if (PhotonNetwork.OfflineMode)
            {
                int side = player.GetPlayerSide();
                if (side == 0)
                {
                    side = player.transform.position.x <= 0 ? 1 : 2;
                    player.SetPlayerSide(side);
                }

                networkPlayers[side] = player;

                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    networkPlayersHealth[side] = health;
                    health.OnPlayerDeath += OnPlayerDeathEvent;
                    health.OnHealthChanged += (current, max) => OnPlayerHealthChanged(side, current, max);
                }
            }
            else
            {
                PhotonView pv = player.GetComponent<PhotonView>();
                if (pv != null && pv.Owner != null)
                {
                    int actorNumber = pv.Owner.ActorNumber;
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
            int roundsToWinForUI = GetRoundsToWin();
            matchUI.InitializeMatch(player1Data, player2Data, player1RoundsWon, player2RoundsWon, roundsToWinForUI);
            
            // Show competitive series info if in competitive mode
            if (isCompetitiveMode)
            {
                matchUI.ShowCompetitiveSeries(currentSeriesMatch, maxSeriesMatches, player1SeriesWins, player2SeriesWins);
                if (debugMode) Debug.Log($"[COMPETITIVE] Showing series UI - Match {currentSeriesMatch}/{maxSeriesMatches}, P1: {player1SeriesWins}, P2: {player2SeriesWins}, Rounds to Win: {roundsToWinForUI}");
            }
        }

        Debug.Log("[UI] Match UI initialized");
    }
    
    void DisableAFKDetectionForAllPlayers()
    {
        // Find all AFK detectors and disable them
        SimpleAFKDetector[] afkDetectors = FindObjectsOfType<SimpleAFKDetector>();
        foreach (var detector in afkDetectors)
        {
            if (detector != null)
            {
                detector.enabled = false;
                if (debugMode) Debug.Log($"[AFK] Disabled AFK detection for {PhotonNetwork.NickName ?? detector.name}");
            }
        }
        
        if (debugMode) Debug.Log($"[AFK] Disabled AFK detection for {afkDetectors.Length} players");
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
        // Use networkPlayers if available, otherwise find all PlayerCharacters
        if (networkPlayers.Count > 0)
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
        else
        {
            // Fallback: find all PlayerCharacters directly (useful for first round)
            PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
            foreach (PlayerCharacter player in allPlayers)
            {
                if (player != null)
                {
                    player.SetInputEnabled(enable);
                    player.SetMovementEnabled(enable);
                }
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

