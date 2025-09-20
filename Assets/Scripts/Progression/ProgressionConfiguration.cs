using System.Collections.Generic;
using UnityEngine;

namespace RetroDodge.Progression
{
    /// <summary>
    /// ScriptableObject configuration for the progression system
    /// Allows easy balancing and configuration without code changes
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionConfig", menuName = "RDR/Progression Configuration")]
    public class ProgressionConfiguration : ScriptableObject
    {
        [Header("XP Rewards")]
        [Tooltip("XP gained for winning a casual match")]
        public int casualWinXP = 140;
        [Tooltip("XP gained for losing a casual match")]
        public int casualLossXP = 70;
        [Tooltip("XP gained for winning a competitive match")]
        public int competitiveWinXP = 200;
        [Tooltip("XP gained for losing a competitive match")]
        public int competitiveLossXP = 100;
        [Tooltip("XP multiplier for AI matches (0.85 = 85% of normal XP)")]
        [Range(0f, 1f)] public float aiXPMultiplier = 0.85f;
        
        [Header("Currency Rewards")]
        [Tooltip("DodgeCoins gained for winning a casual match")]
        public int casualWinCoins = 95;
        [Tooltip("DodgeCoins gained for losing a casual match")]
        public int casualLossCoins = 45;
        [Tooltip("DodgeCoins gained for winning a competitive match")]
        public int competitiveWinCoins = 120;
        [Tooltip("DodgeCoins gained for losing a competitive match")]
        public int competitiveLossCoins = 55;
        [Tooltip("Currency multiplier for AI matches (0.4 = 40% of normal currency)")]
        [Range(0f, 1f)] public float aiCurrencyMultiplier = 0.4f;
        [Tooltip("RumbleTokens gained per level up")]
        public int rumbleTokensPerLevel = 1;
        
        [Header("Bonus Rewards")]
        [Tooltip("Bonus XP for first win of the day")]
        public int firstWinBonusXP = 400;
        [Tooltip("Bonus XP per win streak (after first win)")]
        public int winStreakBonusXP = 35;
        [Tooltip("Maximum streak bonus XP")]
        public int maxStreakBonusXP = 175;
        [Tooltip("Bonus XP for comeback wins")]
        public int comebackBonusXP = 50;
        [Tooltip("Bonus XP for dominant wins (5-0, 5-1)")]
        public int dominantWinBonusXP = 75;
        
        [Header("SR System")]
        [Tooltip("Base SR gained for winning a competitive match")]
        public int baseSRWin = 22;
        [Tooltip("Base SR lost for losing a competitive match")]
        public int baseSRLoss = -14;
        [Tooltip("Bonus SR during placement matches")]
        public int placementBonusSR = 40;
        [Tooltip("Bonus SR for win streaks")]
        public int streakBonusSR = 6;
        [Tooltip("SR decay amount after inactivity")]
        public int srDecayAmount = 12;
        [Tooltip("Days of inactivity before SR decay")]
        public int srDecayDays = 10;
        [Tooltip("Minimum SR before decay stops")]
        public int minSRForDecay = 1000;
        
        [Header("Level System")]
        [Tooltip("Animation curve for level XP requirements")]
        public AnimationCurve levelCurve = AnimationCurve.Linear(0, 1000, 50, 50000);
        [Tooltip("Maximum player level")]
        public int maxLevel = 100;
        [Tooltip("Starting XP required for level 2")]
        public int startingXPRequirement = 1000;
        [Tooltip("XP growth multiplier per level")]
        public float xpGrowthMultiplier = 1.15f;
        
        [Header("Rank Thresholds")]
        [Tooltip("SR thresholds for each rank tier")]
        public List<RankThreshold> rankThresholds = new List<RankThreshold>
        {
            // Bronze Tiers
            new RankThreshold { rankName = "Bronze 1", minSR = 0, maxSR = 199, color = new Color(0.8f, 0.4f, 0.4f) },
            new RankThreshold { rankName = "Bronze 2", minSR = 200, maxSR = 399, color = new Color(0.8f, 0.4f, 0.4f) },
            new RankThreshold { rankName = "Bronze 3", minSR = 400, maxSR = 599, color = new Color(0.8f, 0.4f, 0.4f) },
            
            // Silver Tiers
            new RankThreshold { rankName = "Silver 1", minSR = 600, maxSR = 799, color = new Color(0.7f, 0.7f, 0.7f) },
            new RankThreshold { rankName = "Silver 2", minSR = 800, maxSR = 999, color = new Color(0.7f, 0.7f, 0.7f) },
            new RankThreshold { rankName = "Silver 3", minSR = 1000, maxSR = 1199, color = new Color(0.7f, 0.7f, 0.7f) },
            
            // Gold Tiers
            new RankThreshold { rankName = "Gold 1", minSR = 1200, maxSR = 1399, color = new Color(1f, 0.8f, 0.2f) },
            new RankThreshold { rankName = "Gold 2", minSR = 1400, maxSR = 1599, color = new Color(1f, 0.8f, 0.2f) },
            new RankThreshold { rankName = "Gold 3", minSR = 1600, maxSR = 1799, color = new Color(1f, 0.8f, 0.2f) },
            
            // Platinum Tiers
            new RankThreshold { rankName = "Platinum 1", minSR = 1800, maxSR = 1999, color = new Color(0.2f, 0.8f, 1f) },
            new RankThreshold { rankName = "Platinum 2", minSR = 2000, maxSR = 2199, color = new Color(0.2f, 0.8f, 1f) },
            new RankThreshold { rankName = "Platinum 3", minSR = 2200, maxSR = 2399, color = new Color(0.2f, 0.8f, 1f) },
            
            // Diamond Tiers
            new RankThreshold { rankName = "Diamond 1", minSR = 2400, maxSR = 2599, color = new Color(0.2f, 0.4f, 1f) },
            new RankThreshold { rankName = "Diamond 2", minSR = 2600, maxSR = 2799, color = new Color(0.2f, 0.4f, 1f) },
            new RankThreshold { rankName = "Diamond 3", minSR = 2800, maxSR = 2999, color = new Color(0.2f, 0.4f, 1f) },
            
            // Dodger Tiers
            new RankThreshold { rankName = "Dodger 1", minSR = 3000, maxSR = 3199, color = new Color(0.2f, 1f, 0.4f) },
            new RankThreshold { rankName = "Dodger 2", minSR = 3200, maxSR = 3399, color = new Color(0.2f, 1f, 0.4f) },
            
            // Rumbler (Final Rank)
            new RankThreshold { rankName = "Rumbler", minSR = 3400, maxSR = int.MaxValue, color = new Color(1f, 0.2f, 1f) }
        };
        
        [Header("Achievement Definitions")]
        [Tooltip("List of all available achievements")]
        public List<AchievementDefinition> achievementDefinitions = new List<AchievementDefinition>
        {
            new AchievementDefinition { id = "ACH_FIRST_WIN", name = "First Blood", description = "Win your first match.", xpReward = 100, rumbleTokensReward = 5 },
            new AchievementDefinition { id = "ACH_WIN_10_COMP", name = "Competitive Rookie", description = "Win 10 competitive matches.", xpReward = 500, rumbleTokensReward = 20 },
            new AchievementDefinition { id = "ACH_LEVEL_10", name = "Rising Star", description = "Reach level 10.", xpReward = 200, rumbleTokensReward = 10 },
            new AchievementDefinition { id = "ACH_LEVEL_25", name = "Veteran", description = "Reach level 25.", xpReward = 500, rumbleTokensReward = 25 },
            new AchievementDefinition { id = "ACH_LEVEL_50", name = "Elite", description = "Reach level 50.", xpReward = 1000, rumbleTokensReward = 50 }
        };
        
        [Header("Game Mode Settings")]
        [Tooltip("Enable rewards for casual matches")]
        public bool casualRewardsEnabled = true;
        [Tooltip("Enable rewards for competitive matches")]
        public bool competitiveRewardsEnabled = true;
        [Tooltip("Enable rewards for AI matches")]
        public bool aiRewardsEnabled = true;
        [Tooltip("Enable rewards for custom matches (should be false)")]
        public bool customRewardsEnabled = false;
        
        [Header("Character Unlock System")]
        [Tooltip("Characters unlocked by level")]
        public List<CharacterUnlock> characterUnlocks = new List<CharacterUnlock>
        {
            new CharacterUnlock { characterIndex = 0, unlockLevel = 1, unlockType = CharacterUnlockType.Level },
            new CharacterUnlock { characterIndex = 1, unlockLevel = 5, unlockType = CharacterUnlockType.Level },
            new CharacterUnlock { characterIndex = 2, unlockLevel = 10, unlockType = CharacterUnlockType.Level },
            new CharacterUnlock { characterIndex = 3, unlockLevel = 15, unlockType = CharacterUnlockType.Level },
            new CharacterUnlock { characterIndex = 4, unlockLevel = 20, unlockType = CharacterUnlockType.Level }
        };
        
        [Header("Achievement System")]
        [Tooltip("Enable achievement system")]
        public bool achievementsEnabled = true;
        [Tooltip("XP reward for completing achievements")]
        public int achievementXPReward = 200;
        [Tooltip("Currency reward for completing achievements")]
        public int achievementCurrencyReward = 100;
        
        [Header("Challenge System")]
        [Tooltip("Enable daily/weekly challenges")]
        public bool challengesEnabled = true;
        [Tooltip("Number of daily challenges")]
        public int dailyChallengeCount = 3;
        [Tooltip("Number of weekly challenges")]
        public int weeklyChallengeCount = 2;
        [Tooltip("XP reward for completing daily challenges")]
        public int dailyChallengeXPReward = 150;
        [Tooltip("XP reward for completing weekly challenges")]
        public int weeklyChallengeXPReward = 500;
        [Tooltip("Currency reward for completing daily challenges")]
        public int dailyChallengeCurrencyReward = 75;
        [Tooltip("Currency reward for completing weekly challenges")]
        public int weeklyChallengeCurrencyReward = 250;
        
        [Header("Performance Modifiers")]
        [Tooltip("SR bonus for high damage ratio")]
        public int highDamageRatioSRBonus = 3;
        [Tooltip("SR penalty for low damage ratio")]
        public int lowDamageRatioSRPenalty = -3;
        [Tooltip("SR bonus for dominant wins")]
        public int dominantWinSRBonus = 2;
        [Tooltip("SR bonus for close losses")]
        public int closeLossSRBonus = 1;
        [Tooltip("Damage ratio threshold for high performance")]
        public float highDamageRatioThreshold = 1.5f;
        [Tooltip("Damage ratio threshold for low performance")]
        public float lowDamageRatioThreshold = 0.7f;
        
        /// <summary>
        /// Get XP required for a specific level
        /// </summary>
        public int GetXPRequiredForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level == 2) return startingXPRequirement;
            
            return Mathf.RoundToInt(startingXPRequirement * Mathf.Pow(xpGrowthMultiplier, level - 2));
        }
        
        /// <summary>
        /// Get rank from SR value
        /// </summary>
        public RankThreshold GetRankFromSR(int sr)
        {
            foreach (var threshold in rankThresholds)
            {
                if (sr >= threshold.minSR && sr <= threshold.maxSR)
                    return threshold;
            }
            return rankThresholds[0]; // Default to Bronze
        }
        
        /// <summary>
        /// Check if a character should be unlocked at a given level
        /// </summary>
        public List<int> GetCharactersUnlockedAtLevel(int level)
        {
            var unlocked = new List<int>();
            foreach (var unlock in characterUnlocks)
            {
                if (unlock.unlockType == CharacterUnlockType.Level && unlock.unlockLevel <= level)
                {
                    unlocked.Add(unlock.characterIndex);
                }
            }
            return unlocked;
        }
        
        /// <summary>
        /// Validate configuration values
        /// </summary>
        public bool ValidateConfiguration()
        {
            bool isValid = true;
            
            // Validate XP values
            if (casualWinXP <= 0 || casualLossXP <= 0 || competitiveWinXP <= 0 || competitiveLossXP <= 0)
            {
                Debug.LogError("ProgressionConfig: XP values must be positive");
                isValid = false;
            }
            
            // Validate currency values
            if (casualWinCoins <= 0 || casualLossCoins <= 0 || competitiveWinCoins <= 0 || competitiveLossCoins <= 0)
            {
                Debug.LogError("ProgressionConfig: Currency values must be positive");
                isValid = false;
            }
            
            // Validate multipliers
            if (aiXPMultiplier < 0f || aiXPMultiplier > 1f || aiCurrencyMultiplier < 0f || aiCurrencyMultiplier > 1f)
            {
                Debug.LogError("ProgressionConfig: Multipliers must be between 0 and 1");
                isValid = false;
            }
            
            // Validate level system
            if (maxLevel <= 1 || startingXPRequirement <= 0 || xpGrowthMultiplier <= 1f)
            {
                Debug.LogError("ProgressionConfig: Invalid level system parameters");
                isValid = false;
            }
            
            // Validate rank thresholds
            if (rankThresholds.Count == 0)
            {
                Debug.LogError("ProgressionConfig: No rank thresholds defined");
                isValid = false;
            }
            
            return isValid;
        }
        
        /// <summary>
        /// Reset to default values
        /// </summary>
        [ContextMenu("Reset to Defaults")]
        public void ResetToDefaults()
        {
            casualWinXP = 140;
            casualLossXP = 70;
            competitiveWinXP = 200;
            competitiveLossXP = 100;
            aiXPMultiplier = 0.85f;
            
            casualWinCoins = 95;
            casualLossCoins = 45;
            competitiveWinCoins = 120;
            competitiveLossCoins = 55;
            aiCurrencyMultiplier = 0.4f;
            
            firstWinBonusXP = 400;
            winStreakBonusXP = 35;
            maxStreakBonusXP = 175;
            comebackBonusXP = 50;
            dominantWinBonusXP = 75;
            
            baseSRWin = 22;
            baseSRLoss = -14;
            placementBonusSR = 40;
            streakBonusSR = 6;
            srDecayAmount = 12;
            srDecayDays = 10;
            minSRForDecay = 1000;
            
            maxLevel = 100;
            startingXPRequirement = 1000;
            xpGrowthMultiplier = 1.15f;
            
            casualRewardsEnabled = true;
            competitiveRewardsEnabled = true;
            aiRewardsEnabled = true;
            customRewardsEnabled = false;
            
            achievementsEnabled = true;
            achievementXPReward = 200;
            achievementCurrencyReward = 100;
            
            challengesEnabled = true;
            dailyChallengeCount = 3;
            weeklyChallengeCount = 2;
            dailyChallengeXPReward = 150;
            weeklyChallengeXPReward = 500;
            dailyChallengeCurrencyReward = 75;
            weeklyChallengeCurrencyReward = 250;
        }
    }
    
    /// <summary>
    /// Rank threshold definition
    /// </summary>
    [System.Serializable]
    public class RankThreshold
    {
        [Tooltip("Name of the rank")]
        public string rankName = "Bronze";
        [Tooltip("Minimum SR for this rank")]
        public int minSR = 0;
        [Tooltip("Maximum SR for this rank")]
        public int maxSR = 599;
        [Tooltip("Color for UI display")]
        public Color color = Color.white;
        
        /// <summary>
        /// Check if SR is within this rank range
        /// </summary>
        public bool IsInRange(int sr)
        {
            return sr >= minSR && sr <= maxSR;
        }
    }
    
    /// <summary>
    /// Character unlock definition
    /// </summary>
    [System.Serializable]
    public class CharacterUnlock
    {
        [Tooltip("Character index to unlock")]
        public int characterIndex = 0;
        [Tooltip("Level required to unlock")]
        public int unlockLevel = 1;
        [Tooltip("Type of unlock requirement")]
        public CharacterUnlockType unlockType = CharacterUnlockType.Level;
        [Tooltip("Additional requirements (e.g., achievement ID)")]
        public string additionalRequirement = "";
    }
    
    /// <summary>
    /// Achievement definition
    /// </summary>
    [System.Serializable]
    public class AchievementDefinition
    {
        [Tooltip("Unique ID for the achievement")]
        public string id = "";
        [Tooltip("Display name of the achievement")]
        public string name = "";
        [Tooltip("Description of how to unlock this achievement")]
        public string description = "";
        [Tooltip("XP reward for unlocking this achievement")]
        public int xpReward = 0;
        [Tooltip("RumbleTokens reward for unlocking this achievement")]
        public int rumbleTokensReward = 0;
        [Tooltip("DodgeCoins reward for unlocking this achievement")]
        public int dodgeCoinsReward = 0;
    }
    
    /// <summary>
    /// Character unlock types
    /// </summary>
    public enum CharacterUnlockType
    {
        Level,
        Achievement,
        Currency,
        Special
    }
}
