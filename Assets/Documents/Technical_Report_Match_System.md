# Technical Report: Match System Implementation
## Retro Dodge Rumble - Final Year Project

---

## Executive Summary

The match system in Retro Dodge Rumble implements a sophisticated round-based competitive structure with distinct formats for different game modes. The system features Casual matches (Best of 3 rounds) and Competitive matches (Best of 9 rounds), designed to provide appropriate challenge levels and time investment for different player preferences. The implementation includes comprehensive state management, network synchronization, and seamless integration with the progression system.

---

## 1. System Architecture Overview

### 1.1 Core Components

The match system consists of four primary components:

1. **MatchManager** - Central match orchestration and state management
2. **RoomStateManager** - Network synchronization and room properties
3. **MatchUI** - User interface and match information display
4. **RoomSettings** - Configurable match parameters

### 1.2 Design Philosophy

The match system is designed with the following principles:
- **Mode-Specific Formats**: Different round structures for different game modes
- **Network Synchronization**: Consistent state across all clients
- **Scalable Architecture**: Support for future game modes and formats
- **Performance Optimization**: Efficient state management and updates
- **User Experience**: Clear match progression and information

---

## 2. Match Format Design

### 2.1 Casual Mode: Best of 3 Rounds

**Rationale**: Casual matches are designed for quick, accessible gameplay sessions that don't require significant time investment.

```csharp
[Header("Match Settings")]
[SerializeField] private int roundsToWin = 2; // Best of 3 (first to 2 wins)
```

**Characteristics:**
- **Duration**: 5-15 minutes per match
- **Rounds**: First to win 2 rounds
- **Rewards**: Standard progression rewards
- **Accessibility**: No level requirements
- **Purpose**: Practice, fun, and casual progression

### 2.2 Competitive Mode: Best of 9 Rounds

**Rationale**: Competitive matches require significant skill demonstration and time investment to ensure meaningful rank progression.

```csharp
[Header("Competitive Series Settings")]
[SerializeField] private int maxSeriesMatches = 9; // Best of 9
[SerializeField] private int competitiveRoundsToWin = 3; // Each match is best of 3 rounds
```

**Characteristics:**
- **Duration**: 20-45 minutes per series
- **Format**: Best of 9 matches (first to win 5 matches)
- **Rewards**: Highest progression rewards
- **Requirements**: Level 20+ requirement
- **Purpose**: Serious competitive play and rank advancement

**Design Justification**: The Best of 9 format ensures that:
- Players must demonstrate consistent skill over multiple matches
- Rank progression requires significant time investment
- Prevents quick rank inflation from lucky wins
- Creates meaningful competitive milestones
- Rewards players who can maintain performance over extended periods

---

## 3. Technical Implementation

### 3.1 Match State Management

The system implements comprehensive state tracking:

```csharp
public enum MatchState
{
    WaitingForPlayers,
    PreFight,
    Fighting,
    PostRound,
    MatchEnd
}

private MatchState currentState = MatchState.WaitingForPlayers;
private int currentRound = 1;
private int player1RoundsWon = 0;
private int player2RoundsWon = 0;
private int matchWinner = -1;
```

### 3.2 Round Progression Logic

The system handles round progression with proper synchronization:

```csharp
IEnumerator CheckForMatchEndAfterDelay()
{
    yield return new WaitForSeconds(1f); // Wait for all clients to sync

    int requiredRounds = GetRoundsToWin();
    
    // All clients can check for match end
    if (player1RoundsWon >= requiredRounds || player2RoundsWon >= requiredRounds)
    {
        matchWinner = player1RoundsWon >= requiredRounds ? 1 : 2;
        StartCoroutine(EndMatchSequence());
    }
    else if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
    {
        // Start next round
        StartCoroutine(NextRoundDelay());
    }
}
```

### 3.3 Competitive Series Management

The competitive system tracks series progression:

```csharp
[Header("Competitive Series Settings")]
[SerializeField] private bool isCompetitiveMode = false;
[SerializeField] private int currentSeriesMatch = 1;
[SerializeField] private int player1SeriesWins = 0;
[SerializeField] private int player2SeriesWins = 0;
[SerializeField] private int maxSeriesMatches = 9;
[SerializeField] private bool seriesCompleted = false;
[SerializeField] private int seriesWinner = -1;
```

---

## 4. Network Synchronization

### 4.1 Room Properties Management

The system uses Photon's custom room properties for state synchronization:

```csharp
public class RoomStateManager : MonoBehaviourPunCallbacks
{
    // Room property constants
    private const string CURRENT_ROUND = "CR";
    private const string PLAYER1_ROUNDS_WON = "P1R";
    private const string PLAYER2_ROUNDS_WON = "P2R";
    private const string MATCH_WINNER = "MW";
    private const string CURRENT_MATCH = "CM";
    private const string SERIES_WINS_PLAYER1 = "S1W";
    private const string SERIES_WINS_PLAYER2 = "S2W";
    private const string SERIES_MAX_MATCHES = "SMM";
    private const string SERIES_COMPLETED = "SC";
    private const string SERIES_WINNER = "SW";
}
```

### 4.2 State Synchronization

The system ensures consistent state across all clients:

```csharp
public bool UpdateMatchState(int round, int p1Rounds, int p2Rounds, int winner)
{
    if (!PhotonNetwork.IsMasterClient) return false;
    
    var props = new Hashtable
    {
        [CURRENT_ROUND] = round,
        [PLAYER1_ROUNDS_WON] = p1Rounds,
        [PLAYER2_ROUNDS_WON] = p2Rounds,
        [MATCH_WINNER] = winner
    };
    
    PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    return true;
}
```

---

## 5. Match Flow Management

### 5.1 Complete Match Flow

The system orchestrates the entire match experience:

```csharp
IEnumerator CompleteMatchFlow()
{
    // 1. Wait for players to join
    yield return WaitForPlayers();
    
    // 2. Character selection phase
    yield return CharacterSelectionPhase();
    
    // 3. Pre-fight countdown
    yield return PreFightCountdown();
    
    // 4. Match execution
    yield return ExecuteMatch();
    
    // 5. Results and progression
    yield return ProcessMatchResults();
}
```

### 5.2 Round Management

Individual rounds are managed with proper timing:

```csharp
IEnumerator StartRound(int roundNumber)
{
    currentRound = roundNumber;
    currentState = MatchState.PreFight;
    
    // Update UI
    if (matchUI != null)
    {
        matchUI.UpdateRoundInfo(currentRound, player1RoundsWon, player2RoundsWon);
    }
    
    // Pre-fight countdown
    yield return new WaitForSeconds(preFightCountdown);
    
    // Start fighting
    currentState = MatchState.Fighting;
    StartCoroutine(RoundTimer());
}
```

---

## 6. Competitive Series Implementation

### 6.1 Series Progression Logic

The competitive system manages multi-match series:

```csharp
public bool UpdateSeriesResult(int winnerPlayerNumber)
{
    int currentMatch = GetRoomProperty(CURRENT_MATCH, 1);
    int p1Wins = GetRoomProperty(SERIES_WINS_PLAYER1, 0);
    int p2Wins = GetRoomProperty(SERIES_WINS_PLAYER2, 0);
    int maxMatches = GetRoomProperty(SERIES_MAX_MATCHES, 9);
    
    // Update wins
    if (winnerPlayerNumber == 1)
        p1Wins++;
    else if (winnerPlayerNumber == 2)
        p2Wins++;
    
    // Check if series is complete
    int winsNeeded = (maxMatches + 1) / 2; // Best of maxMatches
    bool seriesCompleted = (p1Wins >= winsNeeded) || (p2Wins >= winsNeeded);
    
    int seriesWinner = -1;
    if (seriesCompleted)
    {
        seriesWinner = (p1Wins >= winsNeeded) ? 1 : 2;
    }
    
    // Update current match for next round
    int nextMatch = seriesCompleted ? currentMatch : currentMatch + 1;
    
    return SetCompetitiveSeriesState(nextMatch, p1Wins, p2Wins, maxMatches, seriesCompleted, seriesWinner);
}
```

### 6.2 Series State Tracking

The system maintains comprehensive series information:

```csharp
private bool SetCompetitiveSeriesState(int currentMatch, int p1Wins, int p2Wins, 
                                     int maxMatches, bool completed, int winner)
{
    if (!PhotonNetwork.IsMasterClient) return false;
    
    var props = new Hashtable
    {
        [CURRENT_MATCH] = currentMatch,
        [SERIES_WINS_PLAYER1] = p1Wins,
        [SERIES_WINS_PLAYER2] = p2Wins,
        [SERIES_MAX_MATCHES] = maxMatches,
        [SERIES_COMPLETED] = completed,
        [SERIES_WINNER] = winner
    };
    
    PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    return true;
}
```

---

## 7. User Interface Integration

### 7.1 Match Information Display

The system provides comprehensive match information:

```csharp
public class MatchUI : MonoBehaviour
{
    [Header("Match Information")]
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text seriesText;
    
    public void UpdateRoundInfo(int round, int p1Wins, int p2Wins)
    {
        roundText.text = $"Round {round}";
        scoreText.text = $"{p1Wins} - {p2Wins}";
    }
    
    public void UpdateSeriesInfo(int currentMatch, int maxMatches, int p1Wins, int p2Wins)
    {
        seriesText.text = $"Match {currentMatch}/{maxMatches} | {p1Wins} - {p2Wins}";
    }
}
```

### 7.2 Progress Visualization

The UI provides clear visual feedback:
- **Round Counter**: Current round and total rounds
- **Score Display**: Wins for each player
- **Series Progress**: Current match in competitive series
- **Timer**: Round and match duration
- **Winner Announcement**: Clear match conclusion

---

## 8. Performance Optimization

### 8.1 Efficient State Updates

The system minimizes unnecessary updates:

```csharp
void Update()
{
    // Only update UI if state has changed
    if (lastState != currentState)
    {
        UpdateUI();
        lastState = currentState;
    }
    
    // Only sync network state when necessary
    if (needsNetworkSync)
    {
        SyncNetworkState();
        needsNetworkSync = false;
    }
}
```

### 8.2 Network Optimization

The system optimizes network usage:
- **Batched Updates**: Group multiple state changes
- **Conditional Sync**: Only sync when values change
- **Compressed Data**: Use efficient data types
- **Rate Limiting**: Prevent excessive network calls

---

## 9. Error Handling and Recovery

### 9.1 Network Disconnection Handling

The system handles network issues gracefully:

```csharp
public override void OnPlayerLeftRoom(Player otherPlayer)
{
    if (currentState == MatchState.Fighting)
    {
        // Handle disconnection during match
        StartCoroutine(HandlePlayerDisconnection(otherPlayer));
    }
}

IEnumerator HandlePlayerDisconnection(Player disconnectedPlayer)
{
    // Wait for potential reconnection
    yield return new WaitForSeconds(10f);
    
    if (!IsPlayerConnected(disconnectedPlayer))
    {
        // End match due to disconnection
        EndMatchDueToDisconnection(disconnectedPlayer);
    }
}
```

### 9.2 State Recovery

The system can recover from inconsistent states:

```csharp
public void RecoverMatchState()
{
    if (PhotonNetwork.CurrentRoom?.CustomProperties != null)
    {
        currentRound = GetRoomProperty(CURRENT_ROUND, 1);
        player1RoundsWon = GetRoomProperty(PLAYER1_ROUNDS_WON, 0);
        player2RoundsWon = GetRoomProperty(PLAYER2_ROUNDS_WON, 0);
        matchWinner = GetRoomProperty(MATCH_WINNER, -1);
        
        // Validate and correct state
        ValidateMatchState();
    }
}
```

---

## 10. Integration with Progression System

### 10.1 Match Result Processing

The system integrates with the progression system:

```csharp
IEnumerator ProcessMatchResults()
{
    // Create match result
    var matchResult = new MatchResult
    {
        gameMode = isCompetitiveMode ? GameMode.Competitive : GameMode.Casual,
        isWin = (matchWinner == localPlayerNumber),
        finalScore = GetFinalScore(),
        damageDealt = GetDamageDealt(),
        damageTaken = GetDamageTaken(),
        matchDuration = GetMatchDuration()
    };
    
    // Apply progression rewards
    if (ProgressionManager.Instance != null)
    {
        ProgressionManager.Instance.ProcessMatchResult(matchResult);
    }
    
    yield return new WaitForSeconds(matchEndDelay);
    
    // Return to main menu
    SceneManager.LoadScene(mainMenuScene);
}
```

### 10.2 Competitive Series Rewards

Competitive series provide enhanced rewards:

```csharp
private void ApplySeriesRewards(int seriesWinner)
{
    if (seriesWinner == localPlayerNumber)
    {
        // Apply series win bonuses
        var bonusRewards = new MatchRewards
        {
            xpGained = config.seriesWinBonusXP,
            dodgeCoinsGained = config.seriesWinBonusCoins,
            srChange = config.seriesWinBonusSR
        };
        
        ProgressionManager.Instance.ApplyRewards(bonusRewards);
    }
}
```

---

## 11. Future Enhancements

### 11.1 Planned Features

1. **Tournament Mode**: Bracket-based competitive tournaments
2. **Spectator Mode**: Watch ongoing matches
3. **Replay System**: Record and playback match history
4. **Custom Match Formats**: Player-defined match rules
5. **Team Matches**: Multi-player team competitions

### 11.2 Technical Improvements

1. **Advanced Analytics**: Detailed match performance tracking
2. **Machine Learning**: AI-driven match balancing
3. **Cross-Platform**: Unified matchmaking across platforms
4. **Anti-Cheat**: Enhanced security measures

---

## 12. Conclusion

The match system in Retro Dodge Rumble successfully implements a sophisticated, mode-specific competitive structure that balances accessibility with competitive integrity. The Best of 3 format for casual matches provides quick, engaging gameplay, while the Best of 9 format for competitive matches ensures meaningful rank progression through sustained skill demonstration.

**Key Achievements:**
- **Mode-Specific Formats**: Appropriate time investment for different playstyles
- **Network Synchronization**: Consistent state across all clients
- **Scalable Architecture**: Support for future game modes and formats
- **Performance Optimization**: Efficient state management and updates
- **User Experience**: Clear match progression and information

The system demonstrates advanced understanding of competitive game design principles and provides a solid foundation for continued development and player engagement.

---

*This technical report documents the match system implementation as of the current project state and will be updated as the system continues to evolve.*

