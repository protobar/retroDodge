using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// NetworkGameManager - Step 1: Dynamic Character Spawning
/// Spawns the specific character prefab that each player selected during character selection
/// Master Client authority pattern with proper PUN2 implementation
/// </summary>
public class NetworkGameManager : MonoBehaviourPunCallbacks
{
    [Header("Character Setup")]
    [SerializeField] private CharacterData[] availableCharacters;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float gameStartDelay = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Game state
    private bool gameStarted = false;
    private bool playersSpawned = false;

    // Room properties keys
    private const string PLAYERS_SPAWNED_KEY = "PlayersSpawned";
    private const string GAME_STARTED_KEY = "GameStarted";
    private const string PLAYER_CHARACTER_KEY = "SelectedCharacter";

    #region Unity Callbacks

    void Start()
    {
        // CRITICAL: Set PhotonNetwork settings for better performance
        PhotonNetwork.SendRate = 30;        // Position updates per second
        PhotonNetwork.SerializationRate = 15; // Data serialization per second

        // Validate character data
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogError("No available characters assigned! Please assign CharacterData array in inspector.");
            return;
        }

        // Validate character prefabs
        ValidateCharacterPrefabs();

        // Only Master Client handles game initialization
        if (PhotonNetwork.IsMasterClient)
        {
            InitializeGameAsMasterClient();
        }
        else
        {
            // Non-master clients wait for game initialization
            WaitForGameInitialization();
        }

        if (debugMode)
        {
            Debug.Log($"NetworkGameManager started. IsMasterClient: {PhotonNetwork.IsMasterClient}");
            Debug.Log($"Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");
            LogPlayerSelections();
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate that all character prefabs exist and are properly set up
    /// </summary>
    void ValidateCharacterPrefabs()
    {
        for (int i = 0; i < availableCharacters.Length; i++)
        {
            CharacterData character = availableCharacters[i];
            if (character == null)
            {
                Debug.LogError($"Character at index {i} is null!");
                continue;
            }

            if (character.characterPrefab == null)
            {
                Debug.LogError($"Character {character.characterName} has no prefab assigned!");
                continue;
            }

            // Check if prefab has PhotonView
            PhotonView prefabPhotonView = character.characterPrefab.GetComponent<PhotonView>();
            if (prefabPhotonView == null)
            {
                Debug.LogError($"Character prefab {character.characterName} is missing PhotonView component!");
            }

            // Check if prefab has required components
            PlayerCharacter playerCharacter = character.characterPrefab.GetComponent<PlayerCharacter>();
            if (playerCharacter == null)
            {
                Debug.LogError($"Character prefab {character.characterName} is missing PlayerCharacter component!");
            }

            if (debugMode)
            {
                Debug.Log($"✓ Character {character.characterName} prefab validated");
            }
        }
    }

    #endregion

    #region Game Initialization

    /// <summary>
    /// Master Client initializes the game
    /// </summary>
    void InitializeGameAsMasterClient()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Only Master Client should call InitializeGameAsMasterClient!");
            return;
        }

        // Wait for all players to be in room (should be 2 for your game)
        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
        {
            SpawnAllPlayers();
        }
        else
        {
            if (debugMode)
                Debug.Log("Waiting for more players to join...");
        }
    }

    /// <summary>
    /// Non-master clients wait for game to be initialized
    /// </summary>
    void WaitForGameInitialization()
    {
        if (debugMode)
            Debug.Log("Waiting for Master Client to initialize game...");

        // Check if players are already spawned (late join scenario)
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PLAYERS_SPAWNED_KEY))
        {
            bool alreadySpawned = (bool)PhotonNetwork.CurrentRoom.CustomProperties[PLAYERS_SPAWNED_KEY];
            if (alreadySpawned && !playersSpawned)
            {
                SpawnLocalPlayerCharacter();
            }
        }
    }

    #endregion

    #region Player Spawning

    /// <summary>
    /// Master Client spawns all players
    /// </summary>
    void SpawnAllPlayers()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (debugMode)
            Debug.Log("Master Client spawning all players...");

        // Set room property to indicate players are being spawned
        Hashtable roomProps = new Hashtable();
        roomProps[PLAYERS_SPAWNED_KEY] = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

        // Spawn local player (Master Client)
        SpawnLocalPlayerCharacter();

        // Notify other clients to spawn via RPC
        photonView.RPC("SpawnLocalPlayerCharacter", RpcTarget.Others);

        playersSpawned = true;

        // Start game after delay
        Invoke(nameof(StartGameplay), gameStartDelay);
    }

    /// <summary>
    /// Each client spawns their own character based on their selection
    /// </summary>
    [PunRPC]
    void SpawnLocalPlayerCharacter()
    {
        // Check if player already spawned
        if (playersSpawned && HasLocalPlayerSpawned())
        {
            if (debugMode)
                Debug.Log("Player already spawned, skipping...");
            return;
        }

        // Get selected character index from player properties
        int selectedCharacterIndex = GetSelectedCharacterIndex();

        // Get character data
        CharacterData selectedCharacter = GetCharacterData(selectedCharacterIndex);
        if (selectedCharacter == null)
        {
            Debug.LogError($"Invalid character selection: {selectedCharacterIndex}");
            return;
        }

        // Get spawn position
        Vector3 spawnPosition = GetSpawnPosition();

        if (debugMode)
        {
            Debug.Log($"Spawning {selectedCharacter.characterName} at position: {spawnPosition}");
        }

        // CRITICAL: Use PhotonNetwork.Instantiate with the specific character prefab
        GameObject playerObj = PhotonNetwork.Instantiate(
            selectedCharacter.characterPrefab.name,
            spawnPosition,
            Quaternion.identity
        );

        // The character prefab should already have the correct CharacterData applied
        // But we can double-check and apply it if needed
        PlayerCharacter playerCharacter = playerObj.GetComponent<PlayerCharacter>();
        if (playerCharacter != null)
        {
            // Ensure character data is properly set
            if (playerCharacter.GetCharacterData() == null)
            {
                playerCharacter.LoadCharacter(selectedCharacter);
            }

            if (debugMode)
                Debug.Log($"✓ {selectedCharacter.characterName} spawned successfully for {PhotonNetwork.LocalPlayer.NickName}");
        }
        else
        {
            Debug.LogError($"Spawned character {selectedCharacter.characterName} is missing PlayerCharacter component!");
        }

        playersSpawned = true;
    }

    /// <summary>
    /// Check if local player has already spawned
    /// </summary>
    bool HasLocalPlayerSpawned()
    {
        // Look for any PlayerCharacter with our PhotonView ownership
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            PhotonView photonView = player.GetComponent<PhotonView>();
            if (photonView != null && photonView.IsMine)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get selected character index from player properties
    /// </summary>
    int GetSelectedCharacterIndex()
    {
        // Try to get from player custom properties
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PLAYER_CHARACTER_KEY))
        {
            object characterObj = PhotonNetwork.LocalPlayer.CustomProperties[PLAYER_CHARACTER_KEY];
            if (characterObj != null && characterObj is int)
            {
                return (int)characterObj;
            }
        }

        if (debugMode)
        {
            Debug.LogWarning($"No character selection found for {PhotonNetwork.LocalPlayer.NickName}, using default character (index 0)");
        }

        // Default to first character if no selection found
        return 0;
    }

    /// <summary>
    /// Get character data by index
    /// </summary>
    CharacterData GetCharacterData(int characterIndex)
    {
        if (characterIndex >= 0 && characterIndex < availableCharacters.Length)
        {
            return availableCharacters[characterIndex];
        }

        Debug.LogError($"Character index {characterIndex} is out of bounds! Available characters: {availableCharacters.Length}");
        return null;
    }

    /// <summary>
    /// Get spawn position based on player's actor number
    /// </summary>
    Vector3 GetSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned, using default positions");
            // Default positions for 2 players
            return PhotonNetwork.LocalPlayer.ActorNumber == 1 ?
                new Vector3(-5f, 0f, 0f) : new Vector3(5f, 0f, 0f);
        }

        // Use ActorNumber to determine spawn point (1-based, so subtract 1)
        int spawnIndex = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % spawnPoints.Length;
        return spawnPoints[spawnIndex].position;
    }

    #endregion

    #region Game Flow

    /// <summary>
    /// Start gameplay after all players spawned
    /// </summary>
    void StartGameplay()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (debugMode)
            Debug.Log("Starting gameplay...");

        // Set room property to indicate game started
        Hashtable roomProps = new Hashtable();
        roomProps[GAME_STARTED_KEY] = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

        // Notify all clients that game started
        photonView.RPC("OnGameStarted", RpcTarget.All);

        gameStarted = true;
    }

    /// <summary>
    /// Called on all clients when game starts
    /// </summary>
    [PunRPC]
    void OnGameStarted()
    {
        gameStarted = true;

        if (debugMode)
            Debug.Log("Game started! Players can now move and interact.");

        // TODO: Enable player controls, spawn ball, start UI, etc.
        EnablePlayerControls();
    }

    /// <summary>
    /// Enable player controls for local player
    /// </summary>
    void EnablePlayerControls()
    {
        // Find local player and enable input
        PlayerCharacter[] allPlayers = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in allPlayers)
        {
            PhotonView photonView = player.GetComponent<PhotonView>();
            if (photonView != null && photonView.IsMine)
            {
                // Enable input for local player
                PlayerInputHandler inputHandler = player.GetComponent<PlayerInputHandler>();
                if (inputHandler != null)
                {
                    inputHandler.enabled = true;
                }

                if (debugMode)
                    Debug.Log($"Enabled controls for local player: {player.GetCharacterData()?.characterName}");
                break;
            }
        }
    }

    #endregion

    #region PUN Callbacks

    /// <summary>
    /// Called when a new player joins the room
    /// </summary>
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (debugMode)
            Debug.Log($"Player {newPlayer.NickName} entered room");

        // If Master Client and room is full, spawn players
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount >= 2 && !playersSpawned)
        {
            SpawnAllPlayers();
        }
    }

    /// <summary>
    /// Called when a player leaves the room
    /// </summary>
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (debugMode)
            Debug.Log($"Player {otherPlayer.NickName} left room");

        // Handle player disconnect
        HandlePlayerDisconnect(otherPlayer);
    }

    /// <summary>
    /// Called when Master Client switches
    /// </summary>
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (debugMode)
            Debug.Log($"Master Client switched to: {newMasterClient.NickName}");

        // If this client is the new Master Client, take control
        if (PhotonNetwork.IsMasterClient)
        {
            TakeOverGameControl();
        }
    }

    /// <summary>
    /// Called when room properties are updated
    /// </summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // Handle room property changes for late-joining players
        if (propertiesThatChanged.ContainsKey(PLAYERS_SPAWNED_KEY) && !playersSpawned)
        {
            bool shouldSpawn = (bool)propertiesThatChanged[PLAYERS_SPAWNED_KEY];
            if (shouldSpawn)
            {
                SpawnLocalPlayerCharacter();
            }
        }

        if (propertiesThatChanged.ContainsKey(GAME_STARTED_KEY) && !gameStarted)
        {
            bool hasStarted = (bool)propertiesThatChanged[GAME_STARTED_KEY];
            if (hasStarted)
            {
                OnGameStarted();
            }
        }
    }

    #endregion

    #region Disconnect Handling

    /// <summary>
    /// Handle player disconnection
    /// </summary>
    void HandlePlayerDisconnect(Player disconnectedPlayer)
    {
        if (!gameStarted) return;

        // In a 2-player game, if one player leaves, other wins by forfeit
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            if (debugMode)
                Debug.Log("Player disconnected, remaining player wins by forfeit");

            // TODO: End game with remaining player as winner
            EndGameWithWinner(PhotonNetwork.LocalPlayer);
        }
    }

    /// <summary>
    /// Take over game control when becoming Master Client
    /// </summary>
    void TakeOverGameControl()
    {
        if (debugMode)
            Debug.Log("Taking over game control as new Master Client");

        // TODO: Take over game state management
        // - Ball physics authority
        // - Game timer
        // - Damage validation
    }

    /// <summary>
    /// End game with winner (placeholder for now)
    /// </summary>
    void EndGameWithWinner(Player winner)
    {
        if (debugMode)
            Debug.Log($"Game ended. Winner: {winner.NickName}");

        // TODO: Implement proper game end logic
    }

    #endregion

    #region Debug Helpers

    /// <summary>
    /// Log all player selections for debugging
    /// </summary>
    void LogPlayerSelections()
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.ContainsKey(PLAYER_CHARACTER_KEY))
            {
                int characterIndex = (int)player.CustomProperties[PLAYER_CHARACTER_KEY];
                CharacterData character = GetCharacterData(characterIndex);
                Debug.Log($"Player {player.NickName} (Actor {player.ActorNumber}) selected: {character?.characterName ?? "Invalid"}");
            }
            else
            {
                Debug.Log($"Player {player.NickName} (Actor {player.ActorNumber}) has no character selection");
            }
        }
    }

    void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 300, 350, 250));
        GUILayout.BeginVertical("box");

        GUILayout.Label("=== NETWORK GAME MANAGER DEBUG ===");
        GUILayout.Label($"Master Client: {PhotonNetwork.IsMasterClient}");
        GUILayout.Label($"Players in Room: {PhotonNetwork.CurrentRoom.PlayerCount}");
        GUILayout.Label($"Players Spawned: {playersSpawned}");
        GUILayout.Label($"Game Started: {gameStarted}");
        GUILayout.Label($"Local Player Actor: {PhotonNetwork.LocalPlayer.ActorNumber}");

        // Show local player's selection
        int localSelection = GetSelectedCharacterIndex();
        CharacterData localCharacter = GetCharacterData(localSelection);
        GUILayout.Label($"Local Selection: {localCharacter?.characterName ?? "None"}");

        // Show all players and their characters
        GUILayout.Label("All Players:");
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            int characterIndex = -1;
            if (player.CustomProperties.ContainsKey(PLAYER_CHARACTER_KEY))
            {
                characterIndex = (int)player.CustomProperties[PLAYER_CHARACTER_KEY];
            }
            CharacterData character = GetCharacterData(characterIndex);
            GUILayout.Label($"  {player.NickName}: {character?.characterName ?? "None"}");
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PLAYERS_SPAWNED_KEY))
        {
            bool roomSpawnedFlag = (bool)PhotonNetwork.CurrentRoom.CustomProperties[PLAYERS_SPAWNED_KEY];
            GUILayout.Label($"Room Spawn Flag: {roomSpawnedFlag}");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion
}