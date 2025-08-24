using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// PUN2 Multiplayer Match Manager - Handles Best of 3 Match System
/// Fully synchronized across network with Master Client authority
/// Fixed timer synchronization using PhotonNetwork.Time
/// </summary>
public class MatchManager : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Match Settings")]
    [SerializeField] private int roundsToWin = 2;
    [SerializeField] private float roundDuration = 90f;
    [SerializeField] private float preFightCountdown = 3f;
    [SerializeField] private float postRoundDelay = 3f;
    [SerializeField] private float matchEndDelay = 5f;

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

    // Network management - room property keys
    private const string ROOM_MATCH_STATE = "MatchState";
    private const string ROOM_CURRENT_ROUND = "CurrentRound";
    private const string ROOM_P1_ROUNDS = "P1Rounds";
    private const string ROOM_P2_ROUNDS = "P2Rounds";
    private const string ROOM_ROUND_START_TIME = "RoundStartTime";
    private const string ROOM_ROUND_END_TIME = "RoundEndTime";
    private const string ROOM_ROUND_ACTIVE = "RoundActive";

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
        // Initialize match when all players are ready
        StartCoroutine(WaitForPlayersAndInitialize());
    }

    void Update()
    {
        // Update timer display for all clients
        if (roundActive && matchUI != null)
        {
            float remainingTime = GetRemainingTime();
            matchUI.UpdateTimer(remainingTime);

            // Master Client checks for time up
            if (PhotonNetwork.IsMasterClient && remainingTime <= 0f)
            {
                EndRoundByTimeOut();
            }
        }
    }

    IEnumerator WaitForPlayersAndInitialize()
    {
        // Wait for both players to be in room
        while (PhotonNetwork.PlayerList.Length < 2)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Wait for player objects to spawn
        yield return new WaitForSeconds(1f);

        // Cache network players
        CacheNetworkPlayers();

        // Only Master Client initializes the match
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(InitializeMatch());
        }
        else
        {
            // Non-master clients sync with room properties
            SyncFromRoomProperties();
            InitializeUI();
        }
    }

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
                int actorNumber = pv.Owner.ActorNumber;
                networkPlayers[actorNumber] = player;

                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    networkPlayersHealth[actorNumber] = health;
                    health.OnPlayerDeath += (controller) => OnPlayerDeath(actorNumber);
                    health.OnHealthChanged += (current, max) => OnPlayerHealthChanged(actorNumber, current, max);
                }
            }
        }
    }

    IEnumerator InitializeMatch()
    {
        currentState = MatchState.Initializing;
        UpdateRoomProperties();

        yield return new WaitForSeconds(1f);

        // Initialize UI
        InitializeUI();

        // Start first round
        StartRound(1);
    }

    void InitializeUI()
    {
        if (matchUI == null) return;

        // Get character data from network players
        CharacterData player1Data = null;
        CharacterData player2Data = null;

        var playerList = PhotonNetwork.PlayerList;
        if (playerList.Length >= 2)
        {
            // Player 1 (ActorNumber 1)
            if (networkPlayers.ContainsKey(1))
            {
                player1Data = networkPlayers[1].GetCharacterData();
            }

            // Player 2 (ActorNumber 2)  
            if (networkPlayers.ContainsKey(2))
            {
                player2Data = networkPlayers[2].GetCharacterData();
            }
        }

        if (player1Data != null && player2Data != null)
        {
            matchUI.InitializeMatch(player1Data, player2Data, player1RoundsWon, player2RoundsWon, roundsToWin);
        }
    }

    void StartRound(int roundNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        currentRound = roundNumber;
        currentState = MatchState.PreFight;
        roundActive = false;

        // Reset timer using PhotonNetwork.Time for synchronization
        roundStartTime = 0.0;
        roundEndTime = 0.0;

        // Update room properties for synchronization
        UpdateRoomProperties();

        // Reset player positions and health
        photonView.RPC("ResetPlayersForNewRound", RpcTarget.All);

        // Start pre-fight sequence
        photonView.RPC("StartPreFightSequence", RpcTarget.All);
    }

    [PunRPC]
    void ResetPlayersForNewRound()
    {
        // Reset positions for network players
        foreach (var kvp in networkPlayers)
        {
            int actorNumber = kvp.Key;
            PlayerCharacter player = kvp.Value;

            if (player != null)
            {
                // Position based on actor number
                Transform spawnPoint = actorNumber == 1 ? player1SpawnPoint : player2SpawnPoint;
                if (spawnPoint != null)
                {
                    player.transform.position = spawnPoint.position;
                }
            }

            // Reset health to full
            if (networkPlayersHealth.ContainsKey(actorNumber))
            {
                PlayerHealth health = networkPlayersHealth[actorNumber];
                if (health != null)
                {
                    health.SetHealth(health.GetMaxHealth());
                }
            }
        }

        // Reset ball
        BallManager ballManager = FindObjectOfType<BallManager>();
        if (ballManager != null)
        {
            ballManager.ResetBall();
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

        // Set synchronized timer start time using PhotonNetwork.Time
        if (PhotonNetwork.IsMasterClient)
        {
            roundStartTime = PhotonNetwork.Time;
            roundEndTime = roundStartTime + roundDuration;
            UpdateRoomProperties();
        }

        // Enable player input
        EnablePlayerInput(true);
    }

    void EndRoundByTimeOut()
    {
        if (!PhotonNetwork.IsMasterClient || !roundActive) return;

        int winner = DetermineWinnerByHealth();
        photonView.RPC("EndRound", RpcTarget.All, winner, "timeout");
    }

    void OnPlayerDeath(int deadPlayerActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || !roundActive) return;

        int winner = deadPlayerActorNumber == 1 ? 2 : 1;
        photonView.RPC("EndRound", RpcTarget.All, winner, "knockout");
    }

    [PunRPC]
    void EndRound(int winner, string reason)
    {
        if (!roundActive) return;

        roundActive = false;
        currentState = MatchState.RoundEnd;

        // Disable player input
        EnablePlayerInput(false);

        // Update round scores (Master Client only)
        if (PhotonNetwork.IsMasterClient)
        {
            if (winner == 1)
            {
                player1RoundsWon++;
            }
            else if (winner == 2)
            {
                player2RoundsWon++;
            }

            UpdateRoomProperties();
        }

        // Update UI
        if (matchUI != null)
        {
            matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
            matchUI.ShowRoundResult(winner);
        }

        PlaySound(roundEndSound);

        // Check for match end
        if (player1RoundsWon >= roundsToWin || player2RoundsWon >= roundsToWin)
        {
            matchWinner = player1RoundsWon >= roundsToWin ? 1 : 2;
            StartCoroutine(EndMatchSequence());
        }
        else
        {
            // Continue to next round
            StartCoroutine(NextRoundDelay());
        }
    }

    IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(postRoundDelay);

        // Only Master Client starts next round
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
            {
                winnerCharacter = networkPlayers[1].GetCharacterData();
            }
            else if (matchWinner == 2 && networkPlayers.ContainsKey(2))
            {
                winnerCharacter = networkPlayers[2].GetCharacterData();
            }

            if (winnerCharacter != null)
            {
                matchUI.ShowMatchResult(matchWinner, winnerCharacter);
            }
        }

        PlaySound(matchWinSound);

        yield return new WaitForSeconds(matchEndDelay);

        // Return to lobby/menu
        ReturnToCharacterSelection();
    }

    int DetermineWinnerByHealth()
    {
        float player1Health = 0f;
        float player2Health = 0f;

        if (networkPlayersHealth.ContainsKey(1))
        {
            player1Health = networkPlayersHealth[1].GetHealthPercentage();
        }

        if (networkPlayersHealth.ContainsKey(2))
        {
            player2Health = networkPlayersHealth[2].GetHealthPercentage();
        }

        if (player1Health > player2Health)
            return 1;
        else if (player2Health > player1Health)
            return 2;
        else
            return 0; // Draw
    }

    void EnablePlayerInput(bool enable)
    {
        foreach (var player in networkPlayers.Values)
        {
            if (player != null)
            {
                player.SetInputEnabled(enable);
            }
        }
    }

    void OnPlayerHealthChanged(int actorNumber, int currentHealth, int maxHealth)
    {
        if (matchUI != null)
        {
            // Convert actor number to player number (1 or 2)
            int playerNumber = actorNumber;
            matchUI.UpdatePlayerHealth(playerNumber, currentHealth, maxHealth);
        }
    }

    void UpdateRoomProperties()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Hashtable props = new Hashtable();
        props[ROOM_MATCH_STATE] = (int)currentState;
        props[ROOM_CURRENT_ROUND] = currentRound;
        props[ROOM_P1_ROUNDS] = player1RoundsWon;
        props[ROOM_P2_ROUNDS] = player2RoundsWon;
        props[ROOM_ROUND_START_TIME] = roundStartTime;
        props[ROOM_ROUND_END_TIME] = roundEndTime;
        props[ROOM_ROUND_ACTIVE] = roundActive;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    void SyncFromRoomProperties()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOM_MATCH_STATE))
        {
            currentState = (MatchState)(int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_MATCH_STATE];
            currentRound = (int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_CURRENT_ROUND];
            player1RoundsWon = (int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_P1_ROUNDS];
            player2RoundsWon = (int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_P2_ROUNDS];
            roundStartTime = (double)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_ROUND_START_TIME];
            roundEndTime = (double)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_ROUND_END_TIME];
            roundActive = (bool)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_ROUND_ACTIVE];
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // Sync with room properties when they change
        if (propertiesThatChanged.ContainsKey(ROOM_ROUND_START_TIME))
        {
            roundStartTime = (double)propertiesThatChanged[ROOM_ROUND_START_TIME];
        }

        if (propertiesThatChanged.ContainsKey(ROOM_ROUND_END_TIME))
        {
            roundEndTime = (double)propertiesThatChanged[ROOM_ROUND_END_TIME];
        }

        if (propertiesThatChanged.ContainsKey(ROOM_ROUND_ACTIVE))
        {
            roundActive = (bool)propertiesThatChanged[ROOM_ROUND_ACTIVE];
        }

        if (propertiesThatChanged.ContainsKey(ROOM_P1_ROUNDS) || propertiesThatChanged.ContainsKey(ROOM_P2_ROUNDS))
        {
            player1RoundsWon = (int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_P1_ROUNDS];
            player2RoundsWon = (int)PhotonNetwork.CurrentRoom.CustomProperties[ROOM_P2_ROUNDS];

            if (matchUI != null)
            {
                matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
            }
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Handle player disconnection - auto-win for remaining player
        if (currentState == MatchState.Fighting || currentState == MatchState.PreFight)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                int winnerActorNumber = PhotonNetwork.PlayerList[0].ActorNumber;
                int winner = winnerActorNumber == 1 ? 1 : 2;

                matchWinner = winner;
                photonView.RPC("OnPlayerDisconnected", RpcTarget.All, winner);
            }
        }
    }

    [PunRPC]
    void OnPlayerDisconnected(int winner)
    {
        matchWinner = winner;
        StartCoroutine(EndMatchSequence());
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // New master client takes over match control
        if (PhotonNetwork.IsMasterClient)
        {
            // Sync from room properties to ensure consistency
            SyncFromRoomProperties();
        }
    }

    void ReturnToCharacterSelection()
    {
        // Leave room and return to character selection
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(characterSelectionScene);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // IPunObservable for additional data synchronization (now simplified)
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Master Client sends essential state
            stream.SendNext(roundActive);
        }
        else
        {
            // Non-master clients receive state
            roundActive = (bool)stream.ReceiveNext();
        }
    }

    // Public API
    public MatchState GetMatchState() => currentState;
    public int GetCurrentRound() => currentRound;

    /// <summary>
    /// Get remaining time using synchronized PhotonNetwork.Time
    /// </summary>
    public float GetRemainingTime()
    {
        if (!roundActive || roundEndTime <= 0.0)
            return 0f;

        double timeRemaining = roundEndTime - PhotonNetwork.Time;
        return Mathf.Max(0f, (float)timeRemaining);
    }

    public bool IsRoundActive() => roundActive;

    public void GetRoundScores(out int player1Wins, out int player2Wins)
    {
        player1Wins = player1RoundsWon;
        player2Wins = player2RoundsWon;
    }
}