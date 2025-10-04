# Technical Report: Economy System Implementation
## Retro Dodge Rumble - Final Year Project

---

## Executive Summary

The economy system in Retro Dodge Rumble implements a sophisticated dual-currency progression model with modular reward calculations, configurable balancing parameters, and comprehensive match-based reward distribution. The system supports multiple game modes with distinct reward structures, ensuring balanced progression while maintaining engagement across different player preferences and skill levels.

---

## 1. System Architecture Overview

### 1.1 Core Components

The economy system consists of four primary components:

1. **ProgressionConfiguration** - ScriptableObject-based configuration system
2. **ModularRewardCalculator** - Dynamic reward calculation engine
3. **PlayerProgressionData** - Player state and statistics management
4. **MatchResult** - Match outcome data structure

### 1.2 Design Philosophy

The economy system is designed with the following principles:
- **Modular Configuration**: Easy balancing without code changes
- **Mode-Specific Rewards**: Different rewards for different game modes
- **Performance-Based Bonuses**: Rewards based on match performance
- **Anti-Farming Measures**: Prevents exploitation of AI matches
- **Scalable Progression**: Supports long-term player engagement

---

## 2. Currency System Design

### 2.1 Dual Currency Model

The system implements two distinct currencies:

#### DodgeCoins (Soft Currency)
- **Primary Use**: General progression and basic unlocks
- **Earning Method**: All game modes except Custom matches
- **Balance**: High volume, low individual value
- **Purpose**: Daily progression and engagement

#### RumbleTokens (Premium Currency)
- **Primary Use**: Premium unlocks and special items
- **Earning Method**: Level-up rewards only
- **Balance**: Low volume, high individual value
- **Purpose**: Long-term progression milestones

### 2.2 Currency Reward Structure

```csharp
[Header("Currency Rewards")]
public int casualWinCoins = 95;
public int casualLossCoins = 45;
public int competitiveWinCoins = 120;
public int competitiveLossCoins = 55;
public float aiCurrencyMultiplier = 0.4f;
public int rumbleTokensPerLevel = 1;
```

**Reward Philosophy:**
- **Competitive Mode**: Highest rewards (120/55 coins)
- **Casual Mode**: Moderate rewards (95/45 coins)
- **AI Practice**: Reduced rewards (40% of casual)
- **Custom Matches**: No rewards (prevents farming)

---

## 3. Experience Point (XP) System

### 3.1 XP Reward Structure

```csharp
[Header("XP Rewards")]
public int casualWinXP = 140;
public int casualLossXP = 70;
public int competitiveWinXP = 200;
public int competitiveLossXP = 100;
public float aiXPMultiplier = 0.85f;
```

### 3.2 XP Calculation Logic

The XP system implements sophisticated bonus calculations:

```csharp
private int CalculateBonusXP(MatchResult result, PlayerProgressionData playerData)
{
    int bonusXP = 0;
    
    // First win of the day bonus
    if (result.isFirstWinOfDay)
    {
        bonusXP += config.firstWinBonusXP; // +400 XP
    }
    
    // Win streak bonus
    if (result.isWin && playerData.winStreak >= 2)
    {
        int streakBonus = Mathf.Min(playerData.winStreak - 1, 10) * config.winStreakBonusXP;
        bonusXP += streakBonus; // Up to +350 XP
    }
    
    // Performance bonus
    if (result.isWin && result.finalScore >= 4)
    {
        bonusXP += config.dominantWinBonusXP; // +50 XP
    }
    
    return bonusXP;
}
```

### 3.3 Level Progression Curve

The system uses an exponential leveling curve:

```csharp
public int GetXPRequiredForLevel(int level)
{
    return Mathf.RoundToInt(config.levelCurve.Evaluate(level));
}
```

**Level Cap**: 100 levels with increasing XP requirements
**Progression Philosophy**: Early levels are quick, later levels require significant commitment

---

## 4. Modular Reward Calculator

### 4.1 Architecture Design

The ModularRewardCalculator implements a flexible reward calculation system:

```csharp
public class ModularRewardCalculator : MonoBehaviour
{
    [SerializeField] private ProgressionConfiguration config;
    [SerializeField] private bool debugMode = false;
    
    public MatchRewards CalculateRewards(MatchResult result, PlayerProgressionData playerData)
    {
        var rewards = new MatchRewards();
        
        // Calculate base rewards
        rewards.xpGained = GetBaseXP(result);
        rewards.dodgeCoinsGained = GetBaseCurrency(result);
        
        // Apply bonuses
        rewards.xpGained += CalculateBonusXP(result, playerData);
        
        // Calculate SR changes (competitive only)
        if (result.gameMode == GameMode.Competitive)
        {
            rewards.srChange = CalculateSRChange(result, playerData);
        }
        
        return rewards;
    }
}
```

### 4.2 Game Mode Differentiation

The system provides distinct reward structures for each game mode:

```csharp
private int GetBaseXP(MatchResult result)
{
    int baseXP = result.gameMode switch
    {
        GameMode.Competitive => result.isWin ? config.competitiveWinXP : config.competitiveLossXP,
        GameMode.Casual => result.isWin ? config.casualWinXP : config.casualLossXP,
        GameMode.AI => (int)((result.isWin ? config.casualWinXP : config.casualLossXP) * config.aiXPMultiplier),
        GameMode.Custom => 0, // No rewards for custom matches
        _ => 0
    };
    
    return baseXP;
}
```

---

## 5. Competitive Ranking System

### 5.1 Skill Rating (SR) Calculation

The competitive system implements sophisticated SR calculations:

```csharp
public int CalculateSRChange(MatchResult result, PlayerProgressionData playerData)
{
    if (result.gameMode != GameMode.Competitive) return 0;
    
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
```

### 5.2 Performance-Based Modifiers

The system rewards exceptional performance:

```csharp
private int CalculatePerformanceModifier(MatchResult result)
{
    // Performance based on damage ratio
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
```

---

## 6. Configuration System

### 6.1 ScriptableObject Architecture

The ProgressionConfiguration uses Unity's ScriptableObject system for easy balancing:

```csharp
[CreateAssetMenu(fileName = "ProgressionConfig", menuName = "RDR/Progression Configuration")]
public class ProgressionConfiguration : ScriptableObject
{
    [Header("XP Rewards")]
    public int casualWinXP = 140;
    public int casualLossXP = 70;
    public int competitiveWinXP = 200;
    public int competitiveLossXP = 100;
    public float aiXPMultiplier = 0.85f;
    
    [Header("Currency Rewards")]
    public int casualWinCoins = 95;
    public int casualLossCoins = 45;
    public int competitiveWinCoins = 120;
    public int competitiveLossCoins = 55;
    public float aiCurrencyMultiplier = 0.4f;
    
    [Header("Bonus Rewards")]
    public int firstWinBonusXP = 400;
    public int winStreakBonusXP = 35;
    public int dominantWinBonusXP = 50;
}
```

### 6.2 Balancing Benefits

**Advantages of ScriptableObject Configuration:**
- **Runtime Balancing**: Adjust values without recompiling
- **Version Control**: Track balance changes in source control
- **Team Collaboration**: Designers can modify without programmer access
- **A/B Testing**: Easy to create multiple configurations
- **Hot Reloading**: Changes apply immediately in editor

---

## 7. Anti-Farming Measures

### 7.1 AI Match Limitations

The system implements anti-farming measures for AI matches:

```csharp
// AI matches give reduced rewards to prevent farming
GameMode.AI => (int)((result.isWin ? config.casualWinXP : config.casualLossXP) * config.aiXPMultiplier),
```

**AI Reward Structure:**
- **XP**: 85% of casual match rewards
- **Currency**: 40% of casual match rewards
- **SR**: No SR changes (competitive only)

### 7.2 Custom Match Restrictions

Custom matches provide no progression rewards:

```csharp
GameMode.Custom => 0, // No rewards for custom matches
```

**Rationale:**
- Prevents exploitation through custom match farming
- Maintains competitive integrity
- Encourages participation in ranked modes

---

## 8. Match Result Data Structure

### 8.1 Comprehensive Match Tracking

The MatchResult structure captures all relevant match data:

```csharp
[System.Serializable]
public class MatchResult
{
    public GameMode gameMode;
    public bool isWin;
    public int finalScore;
    public int damageDealt;
    public int damageTaken;
    public float matchDuration;
    public bool isFirstWinOfDay;
    public string opponentName;
    public int opponentLevel;
}
```

### 8.2 Performance Metrics

The system tracks multiple performance indicators:
- **Damage Ratio**: Offensive vs defensive performance
- **Match Duration**: Engagement time
- **Score Difference**: Dominance vs close matches
- **Opponent Level**: Match difficulty context

---

## 9. Integration with Backend Systems

### 9.1 PlayFab Integration

The economy system integrates with PlayFab for persistent data:

```csharp
public void SavePlayerData()
{
    if (playerData == null) return;
    
    var data = new Dictionary<string, string>
    {
        ["Level"] = playerData.currentLevel.ToString(),
        ["XP"] = playerData.currentXP.ToString(),
        ["DodgeCoins"] = playerData.dodgeCoins.ToString(),
        ["RumbleTokens"] = playerData.rumbleTokens.ToString(),
        ["CompetitiveSR"] = playerData.competitiveSR.ToString(),
        ["CurrentRank"] = playerData.currentRank
    };
    
    PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
    {
        Data = data
    }, OnDataSaved, OnDataSaveError);
}
```

### 9.2 Data Synchronization

The system ensures data consistency across sessions:
- **Local Caching**: Offline data availability
- **Conflict Resolution**: Server-side data validation
- **Auto-Save**: Automatic data persistence
- **Error Recovery**: Robust fallback mechanisms

---

## 10. Performance Optimization

### 10.1 Efficient Calculations

The reward calculation system is optimized for performance:

```csharp
// Cache frequently used values
private int cachedBaseXP;
private int cachedBaseCurrency;

// Batch calculations
public MatchRewards CalculateRewards(MatchResult result, PlayerProgressionData playerData)
{
    // Single calculation pass
    var rewards = new MatchRewards();
    
    // Use switch expressions for performance
    rewards.xpGained = result.gameMode switch
    {
        GameMode.Competitive => result.isWin ? config.competitiveWinXP : config.competitiveLossXP,
        GameMode.Casual => result.isWin ? config.casualWinXP : config.casualLossXP,
        GameMode.AI => (int)((result.isWin ? config.casualWinXP : config.casualLossXP) * config.aiXPMultiplier),
        _ => 0
    };
    
    return rewards;
}
```

### 10.2 Memory Management

The system minimizes memory allocations:
- **Object Pooling**: Reuse MatchResult objects
- **Value Types**: Use structs where appropriate
- **Lazy Loading**: Calculate values only when needed

---

## 11. Future Enhancements

### 11.1 Planned Features

1. **Dynamic Balancing**: AI-driven reward adjustment based on player behavior
2. **Seasonal Rewards**: Time-limited bonus structures
3. **Achievement System**: Milestone-based reward unlocks
4. **Guild Rewards**: Team-based progression bonuses
5. **Daily Challenges**: Rotating objective-based rewards

### 11.2 Technical Improvements

1. **Analytics Integration**: Detailed reward distribution tracking
2. **A/B Testing Framework**: Automated balance testing
3. **Machine Learning**: Predictive reward optimization
4. **Cross-Platform Sync**: Unified progression across devices

---

## 12. Conclusion

The economy system in Retro Dodge Rumble successfully implements a sophisticated, modular progression model that balances player engagement with competitive integrity. The dual-currency system, configurable reward structures, and comprehensive anti-farming measures create a robust foundation for long-term player retention.

**Key Achievements:**
- **Modular Architecture**: Easy balancing and configuration
- **Mode-Specific Rewards**: Appropriate incentives for different playstyles
- **Performance-Based Bonuses**: Rewards exceptional gameplay
- **Anti-Farming Measures**: Maintains competitive integrity
- **Scalable Design**: Supports future feature expansion

The system demonstrates advanced understanding of game economy design principles and provides a solid foundation for continued development and player engagement.

---

*This technical report documents the economy system implementation as of the current project state and will be updated as the system continues to evolve.*

