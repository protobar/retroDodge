# Retro Dodge Rumble - Player Progression System Implementation

## 🎯 Overview

Implementation guide for a complete player progression system with XP, levels, currency (DodgeCoins/RumbleTokens), and competitive SR system for **Retro Dodge Rumble**.

### Game Modes
- **🏆 Competitive** - Ranked matches with SR system (best rewards)
- **🎮 Casual** - Standard matches, no SR (base rewards)  
- **🤖 AI Practice** - Training against bots (reduced rewards)
- **🎉 Custom** - Fun matches (minimal rewards)

## 📊 Progression Data Structure

### Core Player Data
```csharp
[System.Serializable]
public class PlayerProgressionData
{
    // Experience & Level
    public int currentXP = 0;
    public int currentLevel = 1;
    public int xpToNextLevel = 1000;
    
    // Currency
    public int dodgeCoins = 800;      // Soft currency
    public int rumbleTokens = 0;      // Hard currency (premium)
    
    // Competitive Stats
    public int competitiveSR = 0;     // Skill Rating
    public string currentRank = "Unranked";
    public int placementMatchesLeft = 5;
    public bool isRanked = false;
    
    // Match Statistics
    public int totalMatches = 0;
    public int totalWins = 0;
    public int totalLosses = 0;
    public int winStreak = 0;
    public int bestWinStreak = 0;
    
    // Mode-specific stats
    public PlayerModeStats casualStats = new PlayerModeStats();
    public PlayerModeStats competitiveStats = new PlayerModeStats();
    public PlayerModeStats aiStats = new PlayerModeStats();
    
    // Rewards tracking
    public bool hasFirstWinOfDay = true;
    public System.DateTime lastLogin = System.DateTime.Now;
    public System.DateTime lastSRDecay = System.DateTime.Now;
}

[System.Serializable]
public class PlayerModeStats
{
    public int matches = 0;
    public int wins = 0;
    public int losses = 0;
    public int damageDealt = 0;
    public int damageTaken = 0;
    public float averageMatchDuration = 0f;
}
```

### Match Result Data
```csharp
[System.Serializable]
public struct MatchResult
{
    public GameMode gameMode;
    public bool isWin;
    public int finalScore; // e.g., 5-3 (your score - opponent score)
    public float matchDuration;
    public int damageDealt;
    public int damageTaken;
    public string characterUsed;
    public bool wasComeback; // Won after being behind
    public bool wasChoke;    // Lost after being ahead
    public int opponentSR;   // For competitive matches
    public System.DateTime matchTime;
}

public enum GameMode
{
    Casual,
    Competitive, 
    AI,
    Custom
}
```

## 💰 Modular Configuration System

### ScriptableObject Configuration
```csharp
[CreateAssetMenu(fileName = "ProgressionConfig", menuName = "RDR/Progression Configuration")]
public class ProgressionConfiguration : ScriptableObject
{
    [Header("XP Rewards")]
    public int casualWinXP = 140;
    public int casualLossXP = 70;
    public int competitiveWinXP = 200;
    public int competitiveLossXP = 100;
    [Range(0f, 1f)] public float aiXPMultiplier = 0.85f;
    
    [Header("Currency Rewards")]
    public int casualWinCoins = 95;
    public int casualLossCoins = 45;
    public int competitiveWinCoins = 120;
    public int competitiveLossCoins = 55;
    [Range(0f, 1f)] public float aiCurrencyMultiplier = 0.4f;
    
    [Header("Bonus Rewards")]
    public int firstWinBonusXP = 400;
    public int winStreakBonusXP = 35;
    public int maxStreakBonusXP = 175;
    
    [Header("SR System")]
    public int baseSRWin = 22;
    public int baseSRLoss = -14;
    public int placementBonusSR = 40;
    public int streakBonusSR = 6;
    public int srDecayAmount = 12;
    public int srDecayDays = 10;
    
    [Header("Level System")]
    public AnimationCurve levelCurve = AnimationCurve.Linear(0, 1000, 50, 50000);
    public int maxLevel = 100;
    
    [Header("Rank Thresholds")]
    public List<RankThreshold> rankThresholds = new List<RankThreshold>
    {
        new RankThreshold { rankName = "Bronze", minSR = 0, maxSR = 599 },
        new RankThreshold { rankName = "Silver", minSR = 600, maxSR = 1199 },
        new RankThreshold { rankName = "Gold", minSR = 1200, maxSR = 1799 },
        new RankThreshold { rankName = "Platinum", minSR = 1800, maxSR = 2399 },
        new RankThreshold { rankName = "Diamond", minSR = 2400, maxSR = 2999 },
        new RankThreshold { rankName = "Dodger", minSR = 3000, maxSR = 3599 },
        new RankThreshold { rankName = "Rumbler", minSR = 3600, maxSR = int.MaxValue }
    };
    
    [Header("Game Mode Settings")]
    public bool casualRewardsEnabled = true;
    public bool competitiveRewardsEnabled = true;
    public bool aiRewardsEnabled = true;
    public bool customRewardsEnabled = false; // Set to false - no rewards for custom
}

[System.Serializable]
public class RankThreshold
{
    public string rankName;
    public int minSR;
    public int maxSR;
}
```

### Modular Reward Calculator
```csharp
public class ModularRewardCalculator : MonoBehaviour
{
    [Header("Configuration")]
    public ProgressionConfiguration config;
    
    public static ModularRewardCalculator Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Load default config if none assigned
            if (config == null)
                config = Resources.Load<ProgressionConfiguration>("DefaultProgressionConfig");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public int CalculateXPReward(MatchResult result, int currentStreak, bool isFirstWin)
    {
        if (!ShouldGiveRewards(result.gameMode)) return 0;
        
        int baseXP = result.gameMode switch
        {
            GameMode.Competitive => result.isWin ? config.competitiveWinXP : config.competitiveLossXP,
            GameMode.Casual => result.isWin ? config.casualWinXP : config.casualLossXP,
            GameMode.AI => (int)((result.isWin ? config.casualWinXP : config.casualLossXP) * config.aiXPMultiplier),
            GameMode.Custom => 0, // No XP for custom matches
            _ => 0
        };
        
        // Add bonuses
        if (isFirstWin) baseXP += config.firstWinBonusXP;
        if (result.isWin && currentStreak > 1)
        {
            int streakBonus = Mathf.Min(config.winStreakBonusXP * (currentStreak - 1), config.maxStreakBonusXP);
            baseXP += streakBonus;
        }
        
        return baseXP;
    }
    
    public int CalculateCurrencyReward(MatchResult result)
    {
        if (!ShouldGiveRewards(result.gameMode)) return 0;
        
        return result.gameMode switch
        {
            GameMode.Competitive => result.isWin ? config.competitiveWinCoins : config.competitiveLossCoins,
            GameMode.Casual => result.isWin ? config.casualWinCoins : config.casualLossCoins,
            GameMode.AI => (int)((result.isWin ? config.casualWinCoins : config.casualLossCoins) * config.aiCurrencyMultiplier),
            GameMode.Custom => 0, // No currency for custom matches
            _ => 0
        };
    }
    
    public int CalculateSRChange(MatchResult result, PlayerProgressionData playerData)
    {
        if (result.gameMode != GameMode.Competitive || !config.competitiveRewardsEnabled) return 0;
        
        int srChange = result.isWin ? config.baseSRWin : config.baseSRLoss;
        
        // Placement matches bonus
        if (playerData.placementMatchesLeft > 0)
        {
            srChange += config.placementBonusSR;
        }
        
        // Win streak bonus
        if (result.isWin && playerData.winStreak >= 2)
        {
            srChange += config.streakBonusSR;
        }
        
        // Performance modifiers
        srChange += CalculatePerformanceModifier(result);
        
        return Mathf.Clamp(srChange, -50, 50);
    }
    
    public string GetRankFromSR(int sr)
    {
        foreach (var threshold in config.rankThresholds)
        {
            if (sr >= threshold.minSR && sr <= threshold.maxSR)
                return threshold.rankName;
        }
        return "Unranked";
    }
    
    public int GetXPRequiredForLevel(int level)
    {
        return Mathf.RoundToInt(config.levelCurve.Evaluate(level));
    }
    
    private bool ShouldGiveRewards(GameMode mode)
    {
        return mode switch
        {
            GameMode.Casual => config.casualRewardsEnabled,
            GameMode.Competitive => config.competitiveRewardsEnabled,
            GameMode.AI => config.aiRewardsEnabled,
            GameMode.Custom => config.customRewardsEnabled, // Always false
            _ => false
        };
    }
    
    private int CalculatePerformanceModifier(MatchResult result)
    {
        // Performance based on damage ratio and score difference
        float damageRatio = result.damageTaken > 0 ? 
            (float)result.damageDealt / result.damageTaken : 2f;
        
        int performanceBonus = 0;
        if (damageRatio > 1.5f) performanceBonus += 3;
        else if (damageRatio < 0.7f) performanceBonus -= 3;
        
        // Score difference modifier  
        if (result.isWin && result.finalScore >= 4) performanceBonus += 2; // Dominant win
        else if (!result.isWin && result.finalScore >= 3) performanceBonus += 1; // Close loss
        
        return performanceBonus;
    }
}
```



### Currency Rewards  
```csharp
public static class CurrencyRewards
{
    // Base currency (from config)
    public const int CASUAL_WIN = 95;
    public const int CASUAL_LOSS = 45; 
    public const int COMP_WIN = 120;
    public const int COMP_LOSS = 55;
    public const float AI_MULTIPLIER = 0.4f;
    
    public static int CalculateDodgeCoins(MatchResult result)
    {
        return result.gameMode switch
        {
            GameMode.Competitive => result.isWin ? COMP_WIN : COMP_LOSS,
            GameMode.Casual => result.isWin ? CASUAL_WIN : CASUAL_LOSS,
            GameMode.AI => (int)((result.isWin ? CASUAL_WIN : CASUAL_LOSS) * AI_MULTIPLIER),
            GameMode.Custom => 0, // No rewards for custom matches
            _ => 0
        };
    }
}
```



## 🏆 Ranking System

```csharp
public static class RankingSystem
{
    public static readonly Dictionary<string, (int minSR, int maxSR)> RANKS = 
        new Dictionary<string, (int, int)>
        {
            { "Bronze", (0, 599) },
            { "Silver", (600, 1199) },
            { "Gold", (1200, 1799) },
            { "Platinum", (1800, 2399) },
            { "Diamond", (2400, 2999) },
            { "Dodger", (3000, 3599) },
            { "Rumbler", (3600, int.MaxValue) }
        };
    
    public static string GetRankFromSR(int sr)
    {
        foreach (var rank in RANKS)
        {
            if (sr >= rank.Value.minSR && sr <= rank.Value.maxSR)
                return rank.Key;
        }
        return "Unranked";
    }
    
    public static bool DidRankUp(string oldRank, string newRank)
    {
        var ranks = RANKS.Keys.ToList();
        int oldIndex = ranks.IndexOf(oldRank);
        int newIndex = ranks.IndexOf(newRank);
        return newIndex > oldIndex;
    }
}
```

## 🎮 Implementation Architecture

### 1. PlayerDataManager.cs (Core System)
```csharp
public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }
    
    [Header("Configuration")]
    public bool enableLocalCaching = true;
    public float syncInterval = 30f; // Batch sync every 30 seconds
    
    // Data
    private PlayerProgressionData playerData;
    private Dictionary<string, object> pendingPlayFabUpdates = new Dictionary<string, object>();
    private Queue<MatchResult> pendingMatches = new Queue<MatchResult>();
    private bool hasPendingChanges = false;
    
    // Events
    public System.Action<int> OnXPGained;
    public System.Action<int, int> OnLevelUp; // newLevel, xpGained
    public System.Action<int> OnCurrencyGained;
    public System.Action<int, string> OnSRChanged; // newSR, newRank
    public System.Action<PlayerProgressionData> OnDataLoaded;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePlayerData();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Start periodic sync
        InvokeRepeating(nameof(SyncPendingChanges), syncInterval, syncInterval);
        
        // Load data from PlayFab
        LoadPlayerDataFromPlayFab();
    }
    
    // Core API methods would go here...
}
```

### 2. CompetitiveRewardManager.cs (Match Processing)
```csharp
public class CompetitiveRewardManager : MonoBehaviour
{
    public static CompetitiveRewardManager Instance { get; private set; }
    
    [Header("UI References")]
    public GameObject matchEndPanel;
    public MatchResultsUI resultsUI;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void ProcessMatchResult(MatchResult matchResult)
    {
        var playerData = PlayerDataManager.Instance.GetPlayerData();
        
        // Calculate all rewards
        var rewards = CalculateRewards(matchResult, playerData);
        
        // Update player data locally (instant UI update)
        ApplyRewards(rewards, playerData);
        
        // Queue for PlayFab sync
        PlayerDataManager.Instance.QueueMatchForSync(matchResult);
        
        // Show results UI
        ShowMatchResults(matchResult, rewards);
    }
    
    private MatchRewards CalculateRewards(MatchResult result, PlayerProgressionData data)
    {
        var calculator = ModularRewardCalculator.Instance;
        
        return new MatchRewards
        {
            xpGained = calculator.CalculateXPReward(result, data.winStreak, data.hasFirstWinOfDay),
            currencyGained = calculator.CalculateCurrencyReward(result),
            srChange = calculator.CalculateSRChange(result, data),
            newRank = calculator.GetRankFromSR(data.competitiveSR + calculator.CalculateSRChange(result, data)),
            // ... other calculations
        };
    }
}

[System.Serializable]
public struct MatchRewards
{
    public int xpGained;
    public int currencyGained; 
    public int srChange;
    public string newRank;
    public bool leveledUp;
    public int newLevel;
    public bool rankedUp;
    public bool isFirstWin;
    public int streakBonus;
}
```

### 3. MatchResultsUI.cs (Display System)
```csharp
public class MatchResultsUI : MonoBehaviour
{
    [Header("Winner Display")]
    public Text winnerText;
    public Image winnerCharacterImage;
    
    [Header("Rewards Display")]
    public Text xpGainedText;
    public Text currencyGainedText;  
    public Text srChangeText;
    public Slider xpProgressSlider;
    
    [Header("Animations")]
    public float animationDuration = 2f;
    public AnimationCurve rewardCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    public void ShowResults(MatchResult match, MatchRewards rewards)
    {
        gameObject.SetActive(true);
        
        // Show winner immediately
        winnerText.text = match.isWin ? "VICTORY!" : "DEFEAT";
        winnerText.color = match.isWin ? Color.green : Color.red;
        
        // Animate rewards
        StartCoroutine(AnimateRewards(rewards));
        
        // Show special effects for level up/rank up
        if (rewards.leveledUp)
            StartCoroutine(ShowLevelUpEffect());
        if (rewards.rankedUp)
            StartCoroutine(ShowRankUpEffect());
    }
    
    private IEnumerator AnimateRewards(MatchRewards rewards)
    {
        // Animate XP gain, currency gain, SR change over time
        // Implementation details...
        yield return null;
    }
}
```

## 📱 PlayFab Integration

### Optimized Sync Strategy
```csharp
public class PlayFabStatsSync : MonoBehaviour  
{
    private const string PLAYER_DATA_KEY = "PlayerProgression";
    
    public void BatchSyncPlayerData(PlayerProgressionData data, System.Action onComplete = null)
    {
        // Convert to PlayFab format
        var updateRequest = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate { StatisticName = "Level", Value = data.currentLevel },
                new StatisticUpdate { StatisticName = "XP", Value = data.currentXP },
                new StatisticUpdate { StatisticName = "CompetitiveSR", Value = data.competitiveSR },
                new StatisticUpdate { StatisticName = "TotalWins", Value = data.totalWins },
                new StatisticUpdate { StatisticName = "TotalMatches", Value = data.totalMatches },
                // ... other stats
            }
        };
        
        // Save currency separately
        var currencyRequest = new SubtractUserVirtualCurrencyRequest
        {
            VirtualCurrency = "DC",
            Amount = -data.dodgeCoins // Negative to set absolute value
        };
        
        // Batch send both requests
        PlayFabClientAPI.UpdatePlayerStatistics(updateRequest, 
            result => UpdateCurrency(currencyRequest, onComplete),
            error => Debug.LogError($"Failed to sync stats: {error.GenerateErrorReport()}")
        );
    }
    
    private void UpdateCurrency(SubtractUserVirtualCurrencyRequest request, System.Action onComplete)
    {
        PlayFabClientAPI.SubtractUserVirtualCurrency(request,
            result => onComplete?.Invoke(),
            error => Debug.LogError($"Failed to sync currency: {error.GenerateErrorReport()}")
        );
    }
}
```

## 🔄 Integration with Existing Systems

### Integration Helper for Current Architecture
```csharp
[System.Serializable]
public class ProgressionSystemIntegration
{
    [Header("Current System Compatibility")]
    public bool preserveExistingMatchFlow = true;
    public bool useCurrentRoomProperties = true;
    public bool maintainNetworkSync = true;
    
    // Integration points with your existing systems
    public void IntegrateWithMatchManager(MatchManager existingManager)
    {
        // Hook into existing match end sequence without breaking flow
        // This preserves your current competitive series system
    }
    
    public GameMode DetectGameModeFromRoom()
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties == null) 
            return GameMode.Casual;
            
        // Use your existing RoomStateManager keys
        int roomType = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomStateManager.ROOM_TYPE_KEY) ? 
                      (int)PhotonNetwork.CurrentRoom.CustomProperties[RoomStateManager.ROOM_TYPE_KEY] : 0;
        
        return roomType switch
        {
            2 => GameMode.Competitive, // Your existing competitive mode
            1 => GameMode.Casual,      // Your existing casual mode  
            3 => GameMode.AI,          // AI mode
            4 => GameMode.Custom,      // Custom rooms
            _ => GameMode.Casual
        };
    }
    
    public void PreserveExistingSeriesFlow(MatchResult result)
    {
        // Your existing competitive series (best-of-9) continues to work
        // This system adds progression on top without interfering
        if (result.gameMode == GameMode.Competitive)
        {
            // Let existing series system handle match progression
            // Only add XP/Currency/SR rewards after series match completes
        }
    }
}

### Simple MatchManager Integration (Non-Breaking)
```csharp
// Add this minimal hook to your existing MatchManager.cs:

public class MatchManager : MonoBehaviourPunPV, IPunObservable
{
    // ... your existing code stays the same ...
    
    // ADD: Simple progression hook (one line addition)
    IEnumerator EndMatchSequence() 
    {
        // ... your existing match end code ...
        
        // ADD: Single line to trigger progression (non-breaking)
        ProgressionSystemBridge.ProcessMatchEnd(this);
        
        // ... rest of your existing code continues ...
    }
}

// Separate bridge class to keep integration clean
public static class ProgressionSystemBridge
{
    public static void ProcessMatchEnd(MatchManager manager)
    {
        if (CompetitiveRewardManager.Instance == null) return;
        
        // Extract data from your existing MatchManager
        var result = new MatchResult
        {
            gameMode = DetectGameModeFromCurrentRoom(),
            isWin = manager.GetMatchWinner() == GetLocalPlayerNumber(),
            finalScore = manager.GetWinnerScore(),
            matchDuration = manager.GetMatchDuration(),
            // ... other data extraction using your existing methods
        };
        
        CompetitiveRewardManager.Instance.ProcessMatchResult(result);
    }
    
    private static GameMode DetectGameModeFromCurrentRoom()
    {
        // Uses your existing RoomStateManager
        return ProgressionSystemIntegration.DetectGameModeFromRoom();
    }
}

## 📝 Implementation Checklist

### Phase 1: Modular Foundation (Week 1)
- [ ] Create `ProgressionConfiguration.cs` ScriptableObject
- [ ] Create default progression config asset in Resources folder
- [ ] Create `PlayerProgressionData.cs` with your tier system
- [ ] Create `ModularRewardCalculator.cs` with configurable values
- [ ] Test configuration system in editor

### Phase 2: Core Integration (Week 2)  
- [ ] Create `PlayerDataManager.cs` with local caching
- [ ] Create `CompetitiveRewardManager.cs` for match processing
- [ ] Create `ProgressionSystemBridge.cs` for clean integration
- [ ] Add single-line hook to existing `MatchManager.cs`
- [ ] Test that existing match flow is unbroken

### Phase 3: UI & PlayFab (Week 3)
- [ ] Create `MatchResultsUI.cs` for reward display
- [ ] Create `PlayFabStatsSync.cs` for batch operations  
- [ ] Add optional UI elements to `MainMenuManager.cs`
- [ ] Test PlayFab sync with your existing auth system
- [ ] Verify custom matches give zero rewards

### Phase 4: Polish & Balance
- [ ] Tune reward values using ScriptableObject
- [ ] Test all tiers: Bronze → Silver → Gold → Platinum → Diamond → Dodger → Rumbler
- [ ] Verify AI matches give reduced rewards (85% XP, 40% currency)
- [ ] Test competitive SR system with placement matches
- [ ] Final integration testing with existing competitive series

## 🎯 Architecture Benefits

### ✅ **Non-Breaking Integration**
- **Single-line addition** to existing MatchManager
- **Preserves current competitive series** system
- **No changes to networking** or room properties
- **Optional UI components** - add as needed

### ✅ **Fully Modular Design** 
- **ScriptableObject configuration** - edit values without code
- **Runtime value changes** - test balance easily
- **Per-mode reward toggles** - disable any mode's rewards
- **Custom tiers** - your exact rank system implemented

### ✅ **Performance & Reliability**
- **Local caching** for instant UI updates
- **Batch PlayFab syncing** to avoid API limits
- **Error recovery** and retry mechanisms
- **Custom mode = zero rewards** as requested Data (Week 1)
- [ ] Create `PlayerProgressionData.cs` with all data structures
- [ ] Create `MatchResult.cs` with match data container
- [ ] Create `PlayerDataManager.cs` with local caching
- [ ] Implement XP and currency calculation classes
- [ ] Create `SRCalculator.cs` for competitive ranking

### Phase 2: Match Processing (Week 2)  
- [ ] Create `CompetitiveRewardManager.cs` for post-match processing
- [ ] Create `MatchResultsUI.cs` for displaying results
- [ ] Integrate with existing `MatchManager.cs`
- [ ] Add reward animations and level-up effects
- [ ] Test all game modes (Casual, Competitive, AI, Custom)

### Phase 3: PlayFab & UI (Week 3)
- [ ] Create `PlayFabStatsSync.cs` for batch operations
- [ ] Integrate with existing `MainMenuManager.cs`
- [ ] Add player stats display to main menu
- [ ] Implement background syncing with error handling
- [ ] Add SR decay system for inactive competitive players
- [ ] Test data persistence and recovery

### Phase 4: Polish & Testing
- [ ] Add visual feedback for rank changes
- [ ] Implement anti-cheat validation
- [ ] Performance testing with batch operations
- [ ] Mobile UI scaling for reward displays
- [ ] Final balancing of reward values

## 🎯 Expected Results

### Performance Metrics
- **UI Response**: Instant (local cache)
- **PlayFab Calls**: 1 batch per 30 seconds max
- **Memory Usage**: Minimal overhead
- **Network Traffic**: Optimized batch operations

### Player Experience
- **Immediate Feedback**: All rewards show instantly
- **Progression Feel**: Meaningful advancement
- **Competitive Integrity**: Fair SR system
- **Mode Variety**: Appropriate rewards per mode

This system will provide a **professional-grade progression experience** that feels responsive and rewarding while maintaining optimal performance! 🚀

## 📋 Quick Start Commands for Cursor AI

1. "Create PlayerProgressionData.cs with the data structures from the guide"
2. "Implement PlayerDataManager.cs with local caching and events"  
3. "Build CompetitiveRewardManager.cs with all reward calculations"
4. "Create MatchResultsUI.cs with animated reward display"
5. "Integrate progression system into existing MatchManager.cs"