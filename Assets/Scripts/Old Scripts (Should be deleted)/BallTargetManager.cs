using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages ball targeting logic for multiplayer dodgeball game
/// Dynamically determines throw targets based on current ball holder
/// Supports local multiplayer and is ready for PUN2 networking
/// </summary>
public class BallTargetManager : MonoBehaviour
{
    #region Singleton
    private static BallTargetManager instance;
    public static BallTargetManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<BallTargetManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("BallTargetManager");
                    instance = go.AddComponent<BallTargetManager>();
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeTargetManager();
    }
    #endregion

    [Header("Player Management")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool autoFindPlayers = true;

    [Header("Local Testing (Manual Assignment)")]
    [SerializeField] private Transform player1Transform;
    [SerializeField] private Transform player2Transform;
    [SerializeField] private Transform dummyOpponentTransform;

    // Player registry - supports multiple players
    private Dictionary<int, PlayerTargetInfo> registeredPlayers = new Dictionary<int, PlayerTargetInfo>();

    // Current ball state
    private CharacterController currentBallHolder = null;
    private Transform currentTarget = null;

    // Fallback targeting for local testing
    private bool useLocalFallback = true;

    /// <summary>
    /// Information about a registered player for targeting
    /// </summary>
    [System.Serializable]
    public class PlayerTargetInfo
    {
        public int playerId;
        public Transform playerTransform;
        public CharacterController characterController;
        public bool isLocalPlayer;
        public bool isActive;

        public PlayerTargetInfo(int id, Transform transform, CharacterController controller, bool isLocal = true)
        {
            playerId = id;
            playerTransform = transform;
            characterController = controller;
            isLocalPlayer = isLocal;
            isActive = true;
        }
    }

    void Start()
    {
        if (autoFindPlayers)
        {
            AutoRegisterPlayers();
        }

        SetupLocalFallback();
    }

    void InitializeTargetManager()
    {
        registeredPlayers.Clear();
        currentBallHolder = null;
        currentTarget = null;

        if (debugMode)
        {
            Debug.Log("BallTargetManager: Initialized and ready for player registration");
        }
    }

    #region Player Registration System

    /// <summary>
    /// Register a player for targeting system
    /// Call this when players spawn (local or network)
    /// </summary>
    public void RegisterPlayer(int playerId, Transform playerTransform, CharacterController characterController, bool isLocal = true)
    {
        if (playerTransform == null || characterController == null)
        {
            Debug.LogError($"BallTargetManager: Cannot register player {playerId} - null references provided!");
            return;
        }

        PlayerTargetInfo playerInfo = new PlayerTargetInfo(playerId, playerTransform, characterController, isLocal);

        if (registeredPlayers.ContainsKey(playerId))
        {
            Debug.LogWarning($"BallTargetManager: Player {playerId} already registered, updating info");
            registeredPlayers[playerId] = playerInfo;
        }
        else
        {
            registeredPlayers.Add(playerId, playerInfo);
        }

        if (debugMode)
        {
            Debug.Log($"BallTargetManager: Registered Player {playerId} ({playerTransform.name}) - Local: {isLocal}");
            Debug.Log($"BallTargetManager: Total registered players: {registeredPlayers.Count}");
        }
    }

    /// <summary>
    /// Unregister a player (when they disconnect or leave)
    /// </summary>
    public void UnregisterPlayer(int playerId)
    {
        if (registeredPlayers.ContainsKey(playerId))
        {
            registeredPlayers.Remove(playerId);

            if (debugMode)
            {
                Debug.Log($"BallTargetManager: Unregistered Player {playerId}");
            }
        }
    }

    /// <summary>
    /// Automatically find and register players in the scene
    /// Useful for local testing
    /// </summary>
    void AutoRegisterPlayers()
    {
        CharacterController[] allCharacters = FindObjectsOfType<CharacterController>();

        for (int i = 0; i < allCharacters.Length; i++)
        {
            CharacterController character = allCharacters[i];

            // Skip dummy opponents or non-player characters
            if (character.name.ToLower().Contains("dummy"))
                continue;

            // Assign player IDs based on scene order (local testing)
            int playerId = i + 1;
            RegisterPlayer(playerId, character.transform, character, true);
        }

        if (debugMode)
        {
            Debug.Log($"BallTargetManager: Auto-registered {allCharacters.Length} players from scene");
        }
    }

    /// <summary>
    /// Set up fallback targeting for local testing with manual assignments
    /// </summary>
    void SetupLocalFallback()
    {
        // Manual registration for local testing if transforms are assigned
        if (player1Transform != null)
        {
            CharacterController char1 = player1Transform.GetComponent<CharacterController>();
            if (char1 != null)
            {
                RegisterPlayer(1, player1Transform, char1, true);
            }
        }

        if (player2Transform != null)
        {
            CharacterController char2 = player2Transform.GetComponent<CharacterController>();
            if (char2 != null)
            {
                RegisterPlayer(2, player2Transform, char2, true);
            }
        }

        // Set up dummy opponent as fallback
        if (dummyOpponentTransform == null)
        {
            GameObject dummy = GameObject.Find("DummyOpponent");
            if (dummy != null)
            {
                dummyOpponentTransform = dummy.transform;
            }
        }
    }

    #endregion

    #region Ball Holder Management

    /// <summary>
    /// Update who is currently holding the ball
    /// Call this whenever ball possession changes
    /// </summary>
    public void SetBallHolder(CharacterController holder)
    {
        currentBallHolder = holder;

        if (debugMode)
        {
            string holderName = holder != null ? holder.name : "None";
            Debug.Log($"BallTargetManager: Ball holder updated to: {holderName}");
        }
    }

    /// <summary>
    /// Clear the current ball holder
    /// Call this when ball becomes free or is thrown
    /// </summary>
    public void ClearBallHolder()
    {
        currentBallHolder = null;
        currentTarget = null;

        if (debugMode)
        {
            Debug.Log("BallTargetManager: Ball holder cleared");
        }
    }

    /// <summary>
    /// Get the current ball holder
    /// </summary>
    public CharacterController GetCurrentBallHolder()
    {
        return currentBallHolder;
    }

    #endregion

    #region Target Resolution System

    /// <summary>
    /// Get the opponent target for a given thrower
    /// This is the main method called when throwing the ball
    /// </summary>
    public Transform GetOpponent(CharacterController thrower)
    {
        if (thrower == null)
        {
            Debug.LogWarning("BallTargetManager: Cannot get opponent for null thrower!");
            return GetFallbackTarget();
        }

        // Find the thrower's player ID
        int throwerId = GetPlayerIdFromCharacter(thrower);

        if (throwerId == -1)
        {
            if (debugMode)
            {
                Debug.LogWarning($"BallTargetManager: Thrower {thrower.name} not registered, using fallback targeting");
            }
            return GetFallbackTarget(thrower);
        }

        // Get opponent based on registered players
        Transform opponent = GetOpponentById(throwerId);

        if (opponent != null)
        {
            currentTarget = opponent; // Store for continuous tracking

            if (debugMode)
            {
                Debug.Log($"BallTargetManager: Player {throwerId} ({thrower.name}) targeting opponent at {opponent.name}");
            }

            return opponent;
        }

        // Fallback if no valid opponent found
        return GetFallbackTarget(thrower);
    }

    /// <summary>
    /// Get opponent by player ID
    /// </summary>
    Transform GetOpponentById(int throwerId)
    {
        // For 2-player game: simple opponent logic
        foreach (var kvp in registeredPlayers)
        {
            PlayerTargetInfo playerInfo = kvp.Value;

            // Skip if this is the thrower
            if (playerInfo.playerId == throwerId)
                continue;

            // Skip if player is inactive
            if (!playerInfo.isActive)
                continue;

            // Return first valid opponent
            return playerInfo.playerTransform;
        }

        return null;
    }

    /// <summary>
    /// Get player ID from CharacterController reference
    /// </summary>
    int GetPlayerIdFromCharacter(CharacterController character)
    {
        foreach (var kvp in registeredPlayers)
        {
            if (kvp.Value.characterController == character)
            {
                return kvp.Value.playerId;
            }
        }
        return -1; // Not found
    }

    /// <summary>
    /// Fallback targeting for when registered system fails
    /// </summary>
    Transform GetFallbackTarget(CharacterController thrower = null)
    {
        if (useLocalFallback)
        {
            // Local testing fallback logic
            if (thrower != null)
            {
                // Simple position-based targeting for local testing
                Vector3 throwerPos = thrower.transform.position;

                // If thrower is on left side, target right side
                if (throwerPos.x < 0 && player2Transform != null)
                {
                    return player2Transform;
                }
                // If thrower is on right side, target left side
                else if (throwerPos.x > 0 && player1Transform != null)
                {
                    return player1Transform;
                }
            }

            // Use dummy opponent as last resort
            if (dummyOpponentTransform != null)
            {
                return dummyOpponentTransform;
            }
        }

        Debug.LogWarning("BallTargetManager: No valid target found! Ball may not track properly.");
        return null;
    }

    #endregion

    #region Current Target Tracking

    /// <summary>
    /// Get the current target for continuous ball tracking
    /// Call this from ball Update() for homing behavior
    /// </summary>
    public Vector3 GetCurrentTargetPosition()
    {
        if (currentTarget != null)
        {
            return currentTarget.position;
        }

        // If no current target, try to get fallback
        Transform fallback = GetFallbackTarget();
        if (fallback != null)
        {
            return fallback.position;
        }

        // Ultimate fallback - return position to the right
        return new Vector3(8f, 0f, 0f);
    }

    /// <summary>
    /// Check if we have a valid current target
    /// </summary>
    public bool HasValidTarget()
    {
        return currentTarget != null;
    }

    /// <summary>
    /// Update the current target manually (useful for special throws)
    /// </summary>
    public void SetCurrentTarget(Transform target)
    {
        currentTarget = target;

        if (debugMode)
        {
            string targetName = target != null ? target.name : "None";
            Debug.Log($"BallTargetManager: Current target set to: {targetName}");
        }
    }

    #endregion

    #region Networking Preparation

    /// <summary>
    /// Register player using PhotonView data (for future PUN2 integration)
    /// </summary>
    public void RegisterNetworkPlayer(int actorNumber, Transform playerTransform, CharacterController characterController, bool isLocal)
    {
        RegisterPlayer(actorNumber, playerTransform, characterController, isLocal);
    }

    /// <summary>
    /// Get opponent using PhotonView.Owner.ActorNumber (for future PUN2 integration)
    /// </summary>
    public Transform GetNetworkOpponent(int throwerActorNumber)
    {
        return GetOpponentById(throwerActorNumber);
    }

    #endregion

    #region Debug and Utility

    /// <summary>
    /// Get debug information about registered players
    /// </summary>
    public string GetDebugInfo()
    {
        if (registeredPlayers.Count == 0)
        {
            return "No players registered";
        }

        string info = $"Registered Players ({registeredPlayers.Count}):\n";
        foreach (var kvp in registeredPlayers)
        {
            PlayerTargetInfo player = kvp.Value;
            info += $"  Player {player.playerId}: {player.playerTransform.name} (Local: {player.isLocalPlayer}, Active: {player.isActive})\n";
        }

        string holder = currentBallHolder != null ? currentBallHolder.name : "None";
        string target = currentTarget != null ? currentTarget.name : "None";

        info += $"Current Ball Holder: {holder}\n";
        info += $"Current Target: {target}";

        return info;
    }

    /// <summary>
    /// Get all registered players (useful for game management)
    /// </summary>
    public List<PlayerTargetInfo> GetAllPlayers()
    {
        return registeredPlayers.Values.ToList();
    }

    /// <summary>
    /// Get active player count
    /// </summary>
    public int GetActivePlayerCount()
    {
        return registeredPlayers.Values.Count(p => p.isActive);
    }

    void Update()
    {
        // Debug key to show target info
        if (debugMode && Input.GetKeyDown(KeyCode.I))
        {
            Debug.Log("=== BallTargetManager Debug Info ===");
            Debug.Log(GetDebugInfo());
        }
    }

    #endregion

    #region Editor Support

    void OnDrawGizmosSelected()
    {
        if (!debugMode) return;

        // Draw connections between registered players
        if (registeredPlayers.Count >= 2)
        {
            Gizmos.color = Color.yellow;
            var players = registeredPlayers.Values.ToList();

            for (int i = 0; i < players.Count; i++)
            {
                for (int j = i + 1; j < players.Count; j++)
                {
                    if (players[i].playerTransform != null && players[j].playerTransform != null)
                    {
                        Gizmos.DrawLine(players[i].playerTransform.position, players[j].playerTransform.position);
                    }
                }
            }
        }

        // Draw current targeting line
        if (currentBallHolder != null && currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(currentBallHolder.transform.position, currentTarget.position);

            // Draw target indicator
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(currentTarget.position, 1f);
        }
    }

    #endregion
}