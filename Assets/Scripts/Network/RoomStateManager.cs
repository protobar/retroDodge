using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// OPTIMIZED: Centralized Room State Manager
/// Handles ALL room and player property operations to prevent race conditions
/// </summary>
public class RoomStateManager : MonoBehaviourPunCallbacks
{
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // FIXED: Flag to prevent property updates when leaving
    private bool isLeavingRoom = false;
    
    // ═══════════════════════════════════════════════════════════════
    // ROOM PROPERTY CONSTANTS
    // ═══════════════════════════════════════════════════════════════
    
    // Match State Properties
    public const string ROOM_MATCH_STATE = "MatchState";
    public const string ROOM_CURRENT_ROUND = "CurrentRound";
    public const string ROOM_P1_ROUNDS = "P1Rounds";
    public const string ROOM_P2_ROUNDS = "P2Rounds";
    public const string ROOM_ROUND_START_TIME = "RoundStartTime";
    public const string ROOM_ROUND_END_TIME = "RoundEndTime";
    public const string ROOM_ROUND_ACTIVE = "RoundActive";
    
    // Character Selection Properties
    public const string TIMER_STARTED_KEY = "TimerStarted";
    public const string SELECTION_START_TIME_KEY = "SelectionStartTime";
    
    // Player Spawning Properties
    public const string PLAYERS_SPAWNED_KEY = "PlayersSpawned";
    
    // Player Properties
    public const string PLAYER_CHARACTER_KEY = "SelectedCharacter";
    public const string PLAYER_LOCKED_KEY = "CharacterLocked";
    public const string PLAYER_CHARACTER_INDEX = "CharacterIndex";
    public const string PLAYER_CHARACTER_NAME = "CharacterName";
    public const string PLAYER_CHARACTER_COLOR_R = "CharacterColor_R";
    public const string PLAYER_CHARACTER_COLOR_G = "CharacterColor_G";
    public const string PLAYER_CHARACTER_COLOR_B = "CharacterColor_B";
    
    // Room Settings Properties
    public const string ROOM_MATCH_LENGTH = "MatchLength";
    public const string ROOM_SELECTED_MAP = "SelectedMap";
    public const string ROOM_IS_PRIVATE = "IsPrivate";
    public const string ROOM_PASSWORD = "RoomPassword";
    public const string ROOM_MAX_PLAYERS = "MaxPlayers";
    public const string ROOM_ALLOW_SPECTATORS = "AllowSpectators";
    public const string ROOM_IS_VISIBLE = "IsVisible";
    
    // ═══════════════════════════════════════════════════════════════
    // SINGLETON PATTERN
    // ═══════════════════════════════════════════════════════════════
    
    public static RoomStateManager Instance { get; private set; }
    
    /// <summary>
    /// FIXED: Ensure RoomStateManager exists - create if needed
    /// </summary>
    public static RoomStateManager GetOrCreateInstance()
    {
        if (Instance == null)
        {
            // Try to find existing instance first
            Instance = FindObjectOfType<RoomStateManager>();
            
            if (Instance == null)
            {
                // Create new instance if none exists
                GameObject managerObj = new GameObject("RoomStateManager");
                Instance = managerObj.AddComponent<RoomStateManager>();
                DontDestroyOnLoad(managerObj);
                Debug.Log("[ROOM STATE MANAGER] Auto-created instance");
            }
        }
        
        return Instance;
    }
    
    void Awake()
    {
        // FIXED: Ensure singleton persists across all scenes
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[ROOM STATE MANAGER] Singleton created and will persist across scenes");
        }
        else if (Instance != this)
        {
            Debug.Log("[ROOM STATE MANAGER] Duplicate instance destroyed");
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // FIXED: Ensure we're always available
        if (Instance == null)
        {
            Instance = this;
        }
        
        Debug.Log($"[ROOM STATE MANAGER] Started in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
    }
    
    // ═══════════════════════════════════════════════════════════════
    // SAFE PROPERTY UPDATE METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// FIXED: Safe room property update with comprehensive checks
    /// </summary>
    public bool SetRoomProperty(string key, object value)
    {
        return SetRoomProperties(new Hashtable { [key] = value });
    }
    
    /// <summary>
    /// FIXED: Safe room properties update with comprehensive checks
    /// </summary>
    public bool SetRoomProperties(Hashtable properties)
    {
        // Comprehensive safety checks
        if (!PhotonNetwork.IsMasterClient) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Not master client, cannot set room properties");
            return false;
        }
        
        if (!PhotonNetwork.IsConnectedAndReady) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Not connected and ready");
            return false;
        }
        
        if (!PhotonNetwork.InRoom) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Not in room");
            return false;
        }
        
        if (PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Leaving) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Client is leaving");
            return false;
        }
        
        if (PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Disconnecting) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Client is disconnecting");
            return false;
        }
        
        if (isLeavingRoom) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Room state manager is leaving");
            return false;
        }
        
        if (PhotonNetwork.CurrentRoom == null) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Current room is null");
            return false;
        }
        
        try
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
            if (debugMode) Debug.Log($"[ROOM STATE] Successfully set room properties: {string.Join(", ", properties.Keys)}");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ROOM STATE] Failed to set room properties: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// FIXED: Safe player property update with comprehensive checks
    /// </summary>
    public bool SetPlayerProperty(string key, object value)
    {
        return SetPlayerProperties(new Hashtable { [key] = value });
    }
    
    /// <summary>
    /// FIXED: Safe player properties update with comprehensive checks
    /// </summary>
    public bool SetPlayerProperties(Hashtable properties)
    {
        // Comprehensive safety checks
        if (!PhotonNetwork.IsConnectedAndReady) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Not connected and ready for player properties");
            return false;
        }
        
        if (!PhotonNetwork.InRoom) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Not in room for player properties");
            return false;
        }
        
        if (PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Leaving) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Client is leaving for player properties");
            return false;
        }
        
        if (PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Disconnecting) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Client is disconnecting for player properties");
            return false;
        }
        
        if (isLeavingRoom) 
        {
            if (debugMode) Debug.LogWarning($"[ROOM STATE] Room state manager is leaving for player properties");
            return false;
        }
        
        try
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
            if (debugMode) Debug.Log($"[ROOM STATE] Successfully set player properties: {string.Join(", ", properties.Keys)}");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ROOM STATE] Failed to set player properties: {ex.Message}");
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // CONVENIENCE METHODS FOR COMMON OPERATIONS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Set match state properties in one call
    /// </summary>
    public bool SetMatchState(int matchState, int currentRound, int p1Rounds, int p2Rounds, 
                             double roundStartTime, double roundEndTime, bool roundActive)
    {
        Hashtable props = new Hashtable
        {
            [ROOM_MATCH_STATE] = matchState,
            [ROOM_CURRENT_ROUND] = currentRound,
            [ROOM_P1_ROUNDS] = p1Rounds,
            [ROOM_P2_ROUNDS] = p2Rounds,
            [ROOM_ROUND_START_TIME] = roundStartTime,
            [ROOM_ROUND_END_TIME] = roundEndTime,
            [ROOM_ROUND_ACTIVE] = roundActive
        };
        
        return SetRoomProperties(props);
    }
    
    /// <summary>
    /// Set character selection properties in one call
    /// </summary>
    public bool SetCharacterSelectionState(bool timerStarted, double selectionStartTime)
    {
        Hashtable props = new Hashtable
        {
            [TIMER_STARTED_KEY] = timerStarted,
            [SELECTION_START_TIME_KEY] = selectionStartTime
        };
        
        return SetRoomProperties(props);
    }
    
    /// <summary>
    /// Set player character data in one call
    /// </summary>
    public bool SetPlayerCharacterData(int characterIndex, string characterName, Color characterColor)
    {
        Hashtable props = new Hashtable
        {
            [PLAYER_CHARACTER_INDEX] = characterIndex,
            [PLAYER_CHARACTER_NAME] = characterName,
            [PLAYER_CHARACTER_COLOR_R] = characterColor.r,
            [PLAYER_CHARACTER_COLOR_G] = characterColor.g,
            [PLAYER_CHARACTER_COLOR_B] = characterColor.b
        };
        
        return SetPlayerProperties(props);
    }
    
    /// <summary>
    /// Set player selection state in one call
    /// </summary>
    public bool SetPlayerSelectionState(int selectedCharacter, bool isLocked)
    {
        Hashtable props = new Hashtable
        {
            [PLAYER_CHARACTER_KEY] = selectedCharacter,
            [PLAYER_LOCKED_KEY] = isLocked
        };
        
        return SetPlayerProperties(props);
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PROPERTY READING METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get room property with type safety
    /// </summary>
    public T GetRoomProperty<T>(string key, T defaultValue = default(T))
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties == null) return defaultValue;
        
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key))
        {
            try
            {
                return (T)PhotonNetwork.CurrentRoom.CustomProperties[key];
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ROOM STATE] Failed to cast room property {key}: {ex.Message}");
                return defaultValue;
            }
        }
        
        return defaultValue;
    }
    
    /// <summary>
    /// Get player property with type safety
    /// </summary>
    public T GetPlayerProperty<T>(Player player, string key, T defaultValue = default(T))
    {
        if (player?.CustomProperties == null) return defaultValue;
        
        if (player.CustomProperties.ContainsKey(key))
        {
            try
            {
                return (T)player.CustomProperties[key];
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ROOM STATE] Failed to cast player property {key}: {ex.Message}");
                return defaultValue;
            }
        }
        
        return defaultValue;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // ROOM SETTINGS METHODS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Set room settings from RoomSettings object
    /// </summary>
    public bool SetRoomSettings(RoomSettings settings)
    {
        if (settings == null) return false;
        
        Hashtable props = new Hashtable
        {
            [ROOM_MATCH_LENGTH] = settings.matchLengthSeconds,
            [ROOM_SELECTED_MAP] = settings.selectedMap,
            [ROOM_IS_PRIVATE] = settings.isPrivate,
            [ROOM_PASSWORD] = settings.roomPassword,
            [ROOM_MAX_PLAYERS] = settings.maxPlayers,
            [ROOM_ALLOW_SPECTATORS] = settings.allowSpectators,
            [ROOM_IS_VISIBLE] = settings.isVisible
        };
        
        return SetRoomProperties(props);
    }
    
    /// <summary>
    /// Get room settings as RoomSettings object
    /// </summary>
    public RoomSettings GetRoomSettings()
    {
        return new RoomSettings
        {
            matchLengthSeconds = GetRoomProperty(ROOM_MATCH_LENGTH, 60),
            selectedMap = GetRoomProperty(ROOM_SELECTED_MAP, "Arena1"),
            isPrivate = GetRoomProperty(ROOM_IS_PRIVATE, false),
            roomPassword = GetRoomProperty(ROOM_PASSWORD, ""),
            maxPlayers = GetRoomProperty(ROOM_MAX_PLAYERS, 2),
            allowSpectators = GetRoomProperty(ROOM_ALLOW_SPECTATORS, false),
            isVisible = GetRoomProperty(ROOM_IS_VISIBLE, true)
        };
    }
    
    /// <summary>
    /// Set individual room setting
    /// </summary>
    public bool SetRoomSetting(string key, object value)
    {
        Hashtable props = new Hashtable { [key] = value };
        return SetRoomProperties(props);
    }
    
    /// <summary>
    /// Get individual room setting
    /// </summary>
    public T GetRoomSetting<T>(string key, T defaultValue = default(T))
    {
        return GetRoomProperty<T>(key, defaultValue);
    }
    
    // ═══════════════════════════════════════════════════════════════
    // PHOTON CALLBACKS
    // ═══════════════════════════════════════════════════════════════
    
    public override void OnJoinedRoom()
    {
        Debug.Log("[ROOM STATE MANAGER] Joined room - resetting leaving flag");
        isLeavingRoom = false; // FIXED: Reset flag when joining new room
    }
    
    public override void OnLeftRoom()
    {
        Debug.Log("[ROOM STATE MANAGER] Left room");
        isLeavingRoom = true; // FIXED: Set flag to prevent any remaining property updates
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"[ROOM STATE MANAGER] Disconnected: {cause}");
        isLeavingRoom = true;
    }
    
    /// <summary>
    /// FIXED: Manually reset the leaving flag (for debugging/fix purposes)
    /// </summary>
    public void ResetLeavingFlag()
    {
        isLeavingRoom = false;
        if (debugMode) Debug.Log("[ROOM STATE] Manually reset leaving flag");
    }
}
