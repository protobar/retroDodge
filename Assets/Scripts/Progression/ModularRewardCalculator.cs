using UnityEngine;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Centralized reward calculation system for the progression system
    /// Handles XP, currency, and SR calculations with configurable values
    /// </summary>
    public class ModularRewardCalculator : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Progression configuration asset")]
        public ProgressionConfiguration config;
        
        [Header("Debug")]
        [Tooltip("Enable debug logging for reward calculations")]
        public bool debugMode = false;
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static ModularRewardCalculator Instance { get; private set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // Load default config if none assigned
                if (config == null)
                {
                    config = Resources.Load<ProgressionConfiguration>("DefaultProgressionConfig");
                    if (config == null)
                    {
                        Debug.LogWarning("[REWARD CALCULATOR] No progression config found, using default values");
                        CreateDefaultConfig();
                    }
                }
                
                // Validate configuration
                if (config != null && !config.ValidateConfiguration())
                {
                    Debug.LogError("[REWARD CALCULATOR] Invalid configuration detected!");
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Calculate XP reward for a match result
        /// </summary>
        public int CalculateXPReward(MatchResult result, PlayerProgressionData playerData)
        {
            if (!ShouldGiveRewards(result.gameMode))
            {
                if (debugMode) Debug.Log($"[REWARD CALC] No rewards for {result.gameMode} mode");
                return 0;
            }
            
            int baseXP = GetBaseXP(result);
            int bonusXP = CalculateBonusXP(result, playerData);
            int totalXP = baseXP + bonusXP;
            
            if (debugMode)
            {
                Debug.Log($"[REWARD CALC] XP: Base={baseXP}, Bonus={bonusXP}, Total={totalXP}");
            }
            
            return totalXP;
        }
        
        /// <summary>
        /// Calculate DodgeCoins reward for a match result
        /// </summary>
        public int CalculateDodgeCoinsReward(MatchResult result)
        {
            if (!ShouldGiveRewards(result.gameMode))
            {
                if (debugMode) Debug.Log($"[REWARD CALC] No currency for {result.gameMode} mode");
                return 0;
            }
            
            int baseCurrency = GetBaseCurrency(result);
            
            if (debugMode)
            {
                Debug.Log($"[REWARD CALC] Currency: {baseCurrency} for {result.gameMode} {(result.isWin ? "win" : "loss")}");
            }
            
            return baseCurrency;
        }
        
        /// <summary>
        /// Calculate RumbleTokens reward for a match result
        /// </summary>
        public int CalculateRumbleTokensReward(MatchResult result)
        {
            // RumbleTokens are primarily from level ups and achievements, not per-match
            // This method can be extended if per-match premium currency rewards are introduced
            return 0;
        }
        
        /// <summary>
        /// Calculate SR change for a competitive match
        /// </summary>
        public int CalculateSRChange(MatchResult result, PlayerProgressionData playerData)
        {
            if (result.gameMode != GameMode.Competitive || !config.competitiveRewardsEnabled)
            {
                if (debugMode) Debug.Log($"[REWARD CALC] No SR change for {result.gameMode} mode");
                return 0;
            }
            
            int srChange = RankingSystem.CalculateSRChange(result, playerData, config);
            
            if (debugMode)
            {
                Debug.Log($"[REWARD CALC] SR Change: {srChange} for competitive {(result.isWin ? "win" : "loss")}");
            }
            
            return srChange;
        }
        
        /// <summary>
        /// Calculate all rewards for a match result
        /// </summary>
        public MatchRewards CalculateAllRewards(MatchResult result, PlayerProgressionData playerData)
        {
            var rewards = new MatchRewards
            {
                xpGained = CalculateXPReward(result, playerData),
                dodgeCoinsGained = CalculateDodgeCoinsReward(result),
                rumbleTokensGained = CalculateRumbleTokensReward(result),
                srChange = CalculateSRChange(result, playerData),
                isFirstWin = result.wasFirstWinOfDay,
                streakBonusXP = CalculateStreakBonus(result, playerData),
                performanceBonus = CalculatePerformanceBonus(result)
            };
            
            // Calculate new rank after SR change
            rewards.oldRank = playerData.currentRank;
            if (rewards.srChange != 0)
            {
                int newSR = playerData.competitiveSR + rewards.srChange;
                rewards.newRank = RankingSystem.GetRankFromSR(newSR, config).rankName;
                rewards.rankedUp = RankingSystem.DidRankUp(playerData.currentRank, rewards.newRank, config);
                rewards.rankedDown = RankingSystem.DidRankDown(playerData.currentRank, rewards.newRank, config);
            }
            else
            {
                rewards.newRank = playerData.currentRank;
                rewards.rankedUp = false;
                rewards.rankedDown = false;
            }
            
            // Calculate level up
            int tempXP = playerData.currentXP + rewards.xpGained;
            int tempLevel = playerData.currentLevel;
            int levelsGained = 0;
            
            while (tempXP >= config.GetXPRequiredForLevel(tempLevel + 1))
            {
                tempXP -= config.GetXPRequiredForLevel(tempLevel + 1);
                tempLevel++;
                levelsGained++;
            }
            
            rewards.leveledUp = levelsGained > 0;
            rewards.newLevel = tempLevel;
            rewards.levelsGained = levelsGained;
            
            if (debugMode)
            {
                Debug.Log($"[REWARD CALC] All Rewards: XP={rewards.xpGained}, Coins={rewards.dodgeCoinsGained}, " +
                         $"SR={rewards.srChange}, LevelUp={rewards.leveledUp}, RankUp={rewards.rankedUp}");
            }
            
            return rewards;
        }
        
        /// <summary>
        /// Get base XP for a match result
        /// </summary>
        private int GetBaseXP(MatchResult result)
        {
            int baseXP = result.gameMode switch
            {
                GameMode.Competitive => result.isWin ? config.competitiveWinXP : config.competitiveLossXP,
                GameMode.Casual => result.isWin ? config.casualWinXP : config.casualLossXP,
                GameMode.AI => (int)((result.isWin ? config.casualWinXP : config.casualLossXP) * config.aiXPMultiplier),
                GameMode.Custom => 0,
                _ => 0
            };
            
            return baseXP;
        }
        
        /// <summary>
        /// Get base currency for a match result
        /// </summary>
        private int GetBaseCurrency(MatchResult result)
        {
            int baseCurrency = result.gameMode switch
            {
                GameMode.Competitive => result.isWin ? config.competitiveWinCoins : config.competitiveLossCoins,
                GameMode.Casual => result.isWin ? config.casualWinCoins : config.casualLossCoins,
                GameMode.AI => (int)((result.isWin ? config.casualWinCoins : config.casualLossCoins) * config.aiCurrencyMultiplier),
                GameMode.Custom => 0,
                _ => 0
            };
            
            return baseCurrency;
        }
        
        /// <summary>
        /// Calculate bonus XP for special circumstances
        /// </summary>
        private int CalculateBonusXP(MatchResult result, PlayerProgressionData playerData)
        {
            int bonusXP = 0;
            
            // First win of day bonus
            if (result.wasFirstWinOfDay && result.isWin)
            {
                bonusXP += config.firstWinBonusXP;
            }
            
            // Win streak bonus
            if (result.isWin && result.currentWinStreak > 1)
            {
                int streakBonus = Mathf.Min(
                    config.winStreakBonusXP * (result.currentWinStreak - 1),
                    config.maxStreakBonusXP
                );
                bonusXP += streakBonus;
            }
            
            // Comeback bonus
            if (result.wasComeback && result.isWin)
            {
                bonusXP += config.comebackBonusXP;
            }
            
            // Dominant win bonus
            if (result.isWin && result.finalScore >= 4)
            {
                bonusXP += config.dominantWinBonusXP;
            }
            
            return bonusXP;
        }
        
        /// <summary>
        /// Calculate streak bonus amount
        /// </summary>
        private int CalculateStreakBonus(MatchResult result, PlayerProgressionData playerData)
        {
            if (!result.isWin || result.currentWinStreak <= 1)
                return 0;
            
            return Mathf.Min(
                config.winStreakBonusXP * (result.currentWinStreak - 1),
                config.maxStreakBonusXP
            );
        }
        
        /// <summary>
        /// Calculate performance bonus
        /// </summary>
        private int CalculatePerformanceBonus(MatchResult result)
        {
            int bonus = 0;
            
            if (result.wasComeback && result.isWin)
                bonus += config.comebackBonusXP;
            
            if (result.isWin && result.finalScore >= 4)
                bonus += config.dominantWinBonusXP;
            
            return bonus;
        }
        
        /// <summary>
        /// Check if rewards should be given for a game mode
        /// </summary>
        private bool ShouldGiveRewards(GameMode mode)
        {
            return mode switch
            {
                GameMode.Casual => config.casualRewardsEnabled,
                GameMode.Competitive => config.competitiveRewardsEnabled,
                GameMode.AI => config.aiRewardsEnabled,
                GameMode.Custom => config.customRewardsEnabled,
                _ => false
            };
        }
        
        /// <summary>
        /// Create default configuration if none exists
        /// </summary>
        private void CreateDefaultConfig()
        {
            config = ScriptableObject.CreateInstance<ProgressionConfiguration>();
            config.ResetToDefaults();
            
            if (debugMode)
            {
                Debug.Log("[REWARD CALCULATOR] Created default configuration");
            }
        }
        
        /// <summary>
        /// Get XP required for a specific level
        /// </summary>
        public int GetXPRequiredForLevel(int level)
        {
            return config != null ? config.GetXPRequiredForLevel(level) : 1000;
        }
        
        /// <summary>
        /// Get rank from SR value
        /// </summary>
        public string GetRankFromSR(int sr)
        {
            return config != null ? 
                RankingSystem.GetRankFromSR(sr, config).rankName : 
                RankingSystem.GetRankFromSR(sr).rankName;
        }
        
        /// <summary>
        /// Validate current configuration
        /// </summary>
        public bool ValidateConfiguration()
        {
            return config != null && config.ValidateConfiguration();
        }
    }
    
    /// <summary>
    /// Complete match rewards structure
    /// </summary>
    [System.Serializable]
    public struct MatchRewards
    {
        [Header("Basic Rewards")]
        public int xpGained;
        public int dodgeCoinsGained;
        public int rumbleTokensGained;
        public int srChange;
        
        [Header("Progression")]
        public bool leveledUp;
        public int newLevel;
        public int levelsGained;
        public bool rankedUp;
        public bool rankedDown;
        public string oldRank;
        public string newRank;
        
        [Header("Bonuses")]
        public bool isFirstWin;
        public int streakBonusXP;
        public int comebackBonusXP;
        public int qualityMatchBonusXP;
        public int performanceBonus;
        
        /// <summary>
        /// Get total bonus XP
        /// </summary>
        public int GetTotalBonusXP()
        {
            return streakBonusXP + comebackBonusXP + qualityMatchBonusXP + performanceBonus + (isFirstWin ? 400 : 0);
        }
        
        /// <summary>
        /// Get summary string for display
        /// </summary>
        public string GetSummary()
        {
            return $"XP: +{xpGained}, Coins: +{dodgeCoinsGained}, SR: {(srChange >= 0 ? "+" : "")}{srChange}";
        }
    }
}
