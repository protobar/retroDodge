using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;

// ==================== GAME MANAGER ====================
public class GameManager : MonoBehaviourPunCallbacks
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("Game Settings")]
    public GameSettings gameSettings;
    public GameObject ballPrefab;
    public Transform ballSpawnPoint;

    [Header("Game State")]
    public GameState currentState = GameState.WaitingForPlayers;
    public int currentRound = 0;
    public float roundTimer = 90f;
    public int player1Score = 0;
    public int player2Score = 0;

    // Game flow events
    public System.Action<GameState> OnGameStateChanged;
    public System.Action<int> OnRoundStart;
    public System.Action<int> OnRoundEnd;
    public System.Action<int> OnMatchEnd;

    // Player references
    private Dictionary<int, CharacterBase> players = new Dictionary<int, CharacterBase>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            InitializeMatch();
        }
    }

    void Update()
    {
        if (currentState == GameState.Playing && PhotonNetwork.IsMasterClient)
        {
            roundTimer -= Time.deltaTime;

            if (roundTimer <= 0)
            {
                EndRound();
            }

            CheckForKnockout();
        }

        // Update HUD
        if (HUDController.Instance != null)
        {
            HUDController.Instance.UpdateTimer(roundTimer);
        }
    }

    void InitializeMatch()
    {
        StartCoroutine(MatchCountdown());
    }

    IEnumerator MatchCountdown()
    {
        yield return new WaitForSeconds(3f);
        photonView.RPC("StartRound", RpcTarget.All, 1);
    }

    [PunRPC]
    void StartRound(int roundNumber)
    {
        currentRound = roundNumber;
        ChangeGameState(GameState.Playing);
        roundTimer = gameSettings.roundDuration;

        // Reset player health
        foreach (var player in players.Values)
        {
            player.ResetHealth();
        }

        // Spawn ball after delay
        SpawnNewBall(gameSettings.ballSpawnDelay);

        OnRoundStart?.Invoke(roundNumber);
        HUDController.Instance?.ShowRoundStart(roundNumber);
    }

    void CheckForKnockout()
    {
        foreach (var kvp in players)
        {
            if (kvp.Value.currentHealth <= 0)
            {
                EndRound();
                return;
            }
        }
    }

    void EndRound()
    {
        ChangeGameState(GameState.RoundEnd);

        // Determine round winner
        int winner = DetermineRoundWinner();
        if (winner != -1)
        {
            if (winner == 1) player1Score++;
            else player2Score++;
        }

        photonView.RPC("OnRoundEndRPC", RpcTarget.All, winner);
        OnRoundEnd?.Invoke(winner);

        // Check for match winner
        if (player1Score >= gameSettings.roundsToWin || player2Score >= gameSettings.roundsToWin)
        {
            EndMatch();
        }
        else
        {
            StartCoroutine(NextRoundDelay());
        }
    }

    int DetermineRoundWinner()
    {
        if (players.Count < 2) return -1;

        var playerList = new List<CharacterBase>(players.Values);

        // Check for knockout
        if (playerList[0].currentHealth <= 0) return 2;
        if (playerList[1].currentHealth <= 0) return 1;

        // Time ran out - higher health wins
        if (playerList[0].currentHealth > playerList[1].currentHealth) return 1;
        if (playerList[1].currentHealth > playerList[0].currentHealth) return 2;

        return -1; // Draw
    }

    [PunRPC]
    void OnRoundEndRPC(int winner)
    {
        // Show round results UI
    }

    IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(3f);

        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartRound", RpcTarget.All, currentRound + 1);
        }
    }

    void EndMatch()
    {
        ChangeGameState(GameState.MatchEnd);
        int matchWinner = player1Score > player2Score ? 1 : 2;

        OnMatchEnd?.Invoke(matchWinner);

        // Update PlayFab stats
        if (PlayFabManager.Instance != null)
        {
            bool won = (PhotonNetwork.LocalPlayer.ActorNumber == matchWinner);
            // Calculate damage stats from match
            PlayFabManager.Instance.RecordMatchResult(won, 0, 0, ""); // TODO: Add actual stats
        }
    }

    public void SpawnNewBall(float delay = 0f)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(SpawnBallDelayed(delay));
        }
    }

    IEnumerator SpawnBallDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (ballSpawnPoint != null && ballPrefab != null)
        {
            PhotonNetwork.Instantiate(ballPrefab.name, ballSpawnPoint.position, ballSpawnPoint.rotation);
        }
    }

    public void RegisterPlayer(CharacterBase player)
    {
        int actorNumber = player.photonView.Owner.ActorNumber;
        players[actorNumber] = player;
    }

    public void UnregisterPlayer(CharacterBase player)
    {
        int actorNumber = player.photonView.Owner.ActorNumber;
        if (players.ContainsKey(actorNumber))
        {
            players.Remove(actorNumber);
        }
    }

    void ChangeGameState(GameState newState)
    {
        currentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (currentState == GameState.Playing)
        {
            EndMatch(); // Forfeit
        }
    }
}

// ==================== ENUMS ====================
public enum GameState
{
    WaitingForPlayers,
    CountingDown,
    Playing,
    RoundEnd,
    MatchEnd
}

public enum DamageType
{
    BallHit,
    HoldPenalty,
    Ultimate
}
