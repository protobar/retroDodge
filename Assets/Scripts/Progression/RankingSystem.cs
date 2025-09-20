using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Centralized ranking system for competitive play
    /// Handles SR calculations, rank determination, and competitive progression
    /// </summary>
    public static class RankingSystem
    {
        /// <summary>
        /// Default rank thresholds with tier-based system
        /// Bronze 1-3, Silver 1-3, Gold 1-3, Platinum 1-3, Diamond 1-3, Dodger 1-2, Rumbler
        /// </summary>
        public static readonly Dictionary<string, RankInfo> DEFAULT_RANKS = new Dictionary<string, RankInfo>
{
    // Bronze Tiers
    { "Bronze 1", new RankInfo { rankName = "Bronze 1", minSR = 0, maxSR = 199, tier = 1, color = new Color(0.8f, 0.4f, 0.4f) } },
    { "Bronze 2", new RankInfo { rankName = "Bronze 2", minSR = 200, maxSR = 399, tier = 2, color = new Color(0.8f, 0.4f, 0.4f) } },
    { "Bronze 3", new RankInfo { rankName = "Bronze 3", minSR = 400, maxSR = 599, tier = 3, color = new Color(0.8f, 0.4f, 0.4f) } },
    
    // Silver Tiers
    { "Silver 1", new RankInfo { rankName = "Silver 1", minSR = 600, maxSR = 799, tier = 4, color = new Color(0.7f, 0.7f, 0.7f) } },
    { "Silver 2", new RankInfo { rankName = "Silver 2", minSR = 800, maxSR = 999, tier = 5, color = new Color(0.7f, 0.7f, 0.7f) } },
    { "Silver 3", new RankInfo { rankName = "Silver 3", minSR = 1000, maxSR = 1199, tier = 6, color = new Color(0.7f, 0.7f, 0.7f) } },
    
    // Gold Tiers
    { "Gold 1", new RankInfo { rankName = "Gold 1", minSR = 1200, maxSR = 1399, tier = 7, color = new Color(1f, 0.8f, 0.2f) } },
    { "Gold 2", new RankInfo { rankName = "Gold 2", minSR = 1400, maxSR = 1599, tier = 8, color = new Color(1f, 0.8f, 0.2f) } },
    { "Gold 3", new RankInfo { rankName = "Gold 3", minSR = 1600, maxSR = 1799, tier = 9, color = new Color(1f, 0.8f, 0.2f) } },
    
    // Platinum Tiers
    { "Platinum 1", new RankInfo { rankName = "Platinum 1", minSR = 1800, maxSR = 1999, tier = 10, color = new Color(0.2f, 0.8f, 1f) } },
    { "Platinum 2", new RankInfo { rankName = "Platinum 2", minSR = 2000, maxSR = 2199, tier = 11, color = new Color(0.2f, 0.8f, 1f) } },
    { "Platinum 3", new RankInfo { rankName = "Platinum 3", minSR = 2200, maxSR = 2399, tier = 12, color = new Color(0.2f, 0.8f, 1f) } },
    
    // Diamond Tiers
    { "Diamond 1", new RankInfo { rankName = "Diamond 1", minSR = 2400, maxSR = 2599, tier = 13, color = new Color(0.2f, 0.4f, 1f) } },
    { "Diamond 2", new RankInfo { rankName = "Diamond 2", minSR = 2600, maxSR = 2799, tier = 14, color = new Color(0.2f, 0.4f, 1f) } },
    { "Diamond 3", new RankInfo { rankName = "Diamond 3", minSR = 2800, maxSR = 2999, tier = 15, color = new Color(0.2f, 0.4f, 1f) } },
    
    // Dodger Tiers
    { "Dodger 1", new RankInfo { rankName = "Dodger 1", minSR = 3000, maxSR = 3199, tier = 16, color = new Color(0.2f, 1f, 0.4f) } },
    { "Dodger 2", new RankInfo { rankName = "Dodger 2", minSR = 3200, maxSR = 3399, tier = 17, color = new Color(0.2f, 1f, 0.4f) } },
    
    // Rumbler (Final Rank)
    { "Rumbler", new RankInfo { rankName = "Rumbler", minSR = 3400, maxSR = int.MaxValue, tier = 18, color = new Color(1f, 0.2f, 1f) } }
};

        /// <summary>
        /// Get rank information from SR value using default ranks
        /// </summary>
        public static RankInfo GetRankFromSR(int sr)
        {
            // Check ranks in tier order (Bronze 1 to Rumbler)
            var ranksInOrder = new[] { 
                "Bronze 1", "Bronze 2", "Bronze 3",
                "Silver 1", "Silver 2", "Silver 3", 
                "Gold 1", "Gold 2", "Gold 3",
                "Platinum 1", "Platinum 2", "Platinum 3",
                "Diamond 1", "Diamond 2", "Diamond 3",
                "Dodger 1", "Dodger 2",
                "Rumbler"
            };
            
            foreach (var rankName in ranksInOrder)
            {
                if (DEFAULT_RANKS.ContainsKey(rankName))
                {
                    var rank = DEFAULT_RANKS[rankName];
                    if (sr >= rank.minSR && sr <= rank.maxSR)
                        return rank;
                }
            }
            return DEFAULT_RANKS["Bronze 1"]; // Default fallback
        }
        
        /// <summary>
        /// Get rank information from SR value using custom configuration
        /// </summary>
        public static RankInfo GetRankFromSR(int sr, ProgressionConfiguration config)
        {
            if (config == null || config.rankThresholds == null || config.rankThresholds.Count == 0)
                return GetRankFromSR(sr);
            
            // Sort by tier order to ensure correct rank selection
            var sortedThresholds = config.rankThresholds.OrderBy(r => GetTierFromRankName(r.rankName)).ToList();
            
            foreach (var threshold in sortedThresholds)
            {
                if (sr >= threshold.minSR && sr <= threshold.maxSR)
                {
                    return new RankInfo
                    {
                        rankName = threshold.rankName,
                        minSR = threshold.minSR,
                        maxSR = threshold.maxSR,
                        tier = GetTierFromRankName(threshold.rankName),
                        color = threshold.color
                    };
                }
            }
            
            return DEFAULT_RANKS["Bronze 1"]; // Default fallback
        }
        
        /// <summary>
        /// Check if player ranked up
        /// </summary>
        public static bool DidRankUp(string oldRank, string newRank)
        {
            if (string.IsNullOrEmpty(oldRank) || string.IsNullOrEmpty(newRank))
                return false;
            
            var oldRankInfo = DEFAULT_RANKS.ContainsKey(oldRank) ? DEFAULT_RANKS[oldRank] : DEFAULT_RANKS["Bronze 1"];
            var newRankInfo = DEFAULT_RANKS.ContainsKey(newRank) ? DEFAULT_RANKS[newRank] : DEFAULT_RANKS["Bronze 1"];
            
            return newRankInfo.tier > oldRankInfo.tier;
        }
        
        /// <summary>
        /// Check if player ranked up using custom configuration
        /// </summary>
        public static bool DidRankUp(string oldRank, string newRank, ProgressionConfiguration config)
        {
            if (string.IsNullOrEmpty(oldRank) || string.IsNullOrEmpty(newRank))
                return false;
            
            var oldRankInfo = GetRankFromSR(0, config); // Get default rank
            var newRankInfo = GetRankFromSR(0, config); // Get default rank
            
            // Find actual rank info
            if (config != null && config.rankThresholds != null)
            {
                foreach (var threshold in config.rankThresholds)
                {
                    if (threshold.rankName == oldRank)
                        oldRankInfo = new RankInfo { rankName = threshold.rankName, tier = GetTierFromRankName(threshold.rankName) };
                    if (threshold.rankName == newRank)
                        newRankInfo = new RankInfo { rankName = threshold.rankName, tier = GetTierFromRankName(threshold.rankName) };
                }
            }
            
            return newRankInfo.tier > oldRankInfo.tier;
        }
        
        /// <summary>
        /// Check if player ranked down
        /// </summary>
        public static bool DidRankDown(string oldRank, string newRank)
        {
            if (string.IsNullOrEmpty(oldRank) || string.IsNullOrEmpty(newRank))
                return false;
            
            var oldRankInfo = DEFAULT_RANKS.ContainsKey(oldRank) ? DEFAULT_RANKS[oldRank] : DEFAULT_RANKS["Bronze 1"];
            var newRankInfo = DEFAULT_RANKS.ContainsKey(newRank) ? DEFAULT_RANKS[newRank] : DEFAULT_RANKS["Bronze 1"];
            
            return newRankInfo.tier < oldRankInfo.tier;
        }
        
        /// <summary>
        /// Check if player ranked down using custom configuration
        /// </summary>
        public static bool DidRankDown(string oldRank, string newRank, ProgressionConfiguration config)
        {
            if (string.IsNullOrEmpty(oldRank) || string.IsNullOrEmpty(newRank))
                return false;
            
            var oldRankInfo = GetRankFromSR(0, config); // Get default rank
            var newRankInfo = GetRankFromSR(0, config); // Get default rank
            
            // Find actual rank info
            if (config != null && config.rankThresholds != null)
            {
                foreach (var threshold in config.rankThresholds)
                {
                    if (threshold.rankName == oldRank)
                        oldRankInfo = new RankInfo { rankName = threshold.rankName, tier = GetTierFromRankName(threshold.rankName) };
                    if (threshold.rankName == newRank)
                        newRankInfo = new RankInfo { rankName = threshold.rankName, tier = GetTierFromRankName(threshold.rankName) };
                }
            }
            
            return newRankInfo.tier < oldRankInfo.tier;
        }
        
        /// <summary>
        /// Calculate SR change for a match result
        /// </summary>
        public static int CalculateSRChange(MatchResult result, PlayerProgressionData playerData, ProgressionConfiguration config)
        {
            if (result.gameMode != GameMode.Competitive || !config.competitiveRewardsEnabled)
                return 0;
            
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
            srChange += CalculatePerformanceModifier(result, config);
            
            // Opponent SR difference modifier
            if (result.opponentSR > 0)
            {
                srChange += CalculateOpponentSRModifier(playerData.competitiveSR, result.opponentSR, result.isWin);
            }
            
            return Mathf.Clamp(srChange, -50, 50); // Cap SR change
        }
        
        /// <summary>
        /// Calculate performance-based SR modifier
        /// </summary>
        private static int CalculatePerformanceModifier(MatchResult result, ProgressionConfiguration config)
        {
            int modifier = 0;
            
            // Damage ratio modifier
            float damageRatio = result.GetDamageRatio();
            if (damageRatio > config.highDamageRatioThreshold)
                modifier += config.highDamageRatioSRBonus;
            else if (damageRatio < config.lowDamageRatioThreshold)
                modifier += config.lowDamageRatioSRPenalty;
            
            // Score difference modifier
            if (result.isWin && result.finalScore >= 4)
                modifier += config.dominantWinSRBonus; // Dominant win
            else if (!result.isWin && result.finalScore >= 3)
                modifier += config.closeLossSRBonus; // Close loss
            
            // Comeback/choke modifiers
            if (result.wasComeback) modifier += 2;
            if (result.wasChoke) modifier -= 2;
            
            return modifier;
        }
        
        /// <summary>
        /// Calculate SR modifier based on opponent SR difference
        /// </summary>
        private static int CalculateOpponentSRModifier(int playerSR, int opponentSR, bool isWin)
        {
            int srDifference = opponentSR - playerSR;
            
            if (isWin)
            {
                // Bonus for beating higher SR opponents
                if (srDifference > 100) return 3;
                if (srDifference > 50) return 2;
                if (srDifference > 0) return 1;
            }
            else
            {
                // Reduced penalty for losing to higher SR opponents
                if (srDifference > 100) return 2;
                if (srDifference > 50) return 1;
                if (srDifference > 0) return 0;
            }
            
            return 0;
        }
        
        /// <summary>
        /// Calculate SR decay amount
        /// </summary>
        public static int CalculateSRDecay(PlayerProgressionData playerData, ProgressionConfiguration config)
        {
            if (!playerData.isRanked || playerData.competitiveSR < config.minSRForDecay)
                return 0;
            
            int daysSinceLastDecay = (System.DateTime.Now - playerData.lastSRDecay).Days;
            if (daysSinceLastDecay < config.srDecayDays)
                return 0;
            
            return config.srDecayAmount;
        }
        
        /// <summary>
        /// Get tier number from rank name
        /// </summary>
        private static int GetTierFromRankName(string rankName)
        {
            return rankName switch
            {
                "Bronze" => 1,
                "Silver" => 2,
                "Gold" => 3,
                "Platinum" => 4,
                "Diamond" => 5,
                "Dodger" => 6,
                "Rumbler" => 7,
                _ => 1
            };
        }
        
        /// <summary>
        /// Get rank progress percentage within current rank
        /// </summary>
        public static float GetRankProgress(int currentSR, ProgressionConfiguration config)
        {
            var currentRank = GetRankFromSR(currentSR, config);
            var nextRank = GetNextRank(currentRank, config);
            
            if (nextRank == null)
                return 1f; // Max rank reached
            
            int currentRange = currentRank.maxSR - currentRank.minSR;
            int progressInRange = currentSR - currentRank.minSR;
            
            return (float)progressInRange / currentRange;
        }
        
        /// <summary>
        /// Get next rank in progression
        /// </summary>
        public static RankInfo GetNextRank(RankInfo currentRank, ProgressionConfiguration config)
        {
            if (config == null || config.rankThresholds == null)
                return null;
            
            var sortedRanks = config.rankThresholds.OrderBy(r => r.minSR).ToList();
            int currentIndex = sortedRanks.FindIndex(r => r.rankName == currentRank.rankName);
            
            if (currentIndex >= 0 && currentIndex < sortedRanks.Count - 1)
            {
                var nextThreshold = sortedRanks[currentIndex + 1];
                return new RankInfo
                {
                    rankName = nextThreshold.rankName,
                    minSR = nextThreshold.minSR,
                    maxSR = nextThreshold.maxSR,
                    tier = GetTierFromRankName(nextThreshold.rankName),
                    color = nextThreshold.color
                };
            }
            
            return null; // Max rank reached
        }
        
        /// <summary>
        /// Get SR required to reach next rank
        /// </summary>
        public static int GetSRToNextRank(int currentSR, ProgressionConfiguration config)
        {
            var currentRank = GetRankFromSR(currentSR, config);
            var nextRank = GetNextRank(currentRank, config);
            
            if (nextRank == null)
                return 0; // Max rank reached
            
            return nextRank.minSR - currentSR;
        }
        
        /// <summary>
        /// Validate SR value
        /// </summary>
        public static bool IsValidSR(int sr)
        {
            return sr >= 0 && sr <= 5000; // Reasonable SR range
        }
        
        /// <summary>
        /// Get rank display name with tier
        /// </summary>
        public static string GetRankDisplayName(RankInfo rankInfo)
        {
            return $"{rankInfo.rankName} {rankInfo.tier}";
        }
    }
    
    /// <summary>
    /// Rank information container
    /// </summary>
    [System.Serializable]
    public class RankInfo
    {
        public string rankName = "Bronze";
        public int minSR = 0;
        public int maxSR = 599;
        public int tier = 1;
        public Color color = Color.white;
        
        /// <summary>
        /// Check if SR is within this rank range
        /// </summary>
        public bool IsInRange(int sr)
        {
            return sr >= minSR && sr <= maxSR;
        }
        
        /// <summary>
        /// Get progress within this rank (0-1)
        /// </summary>
        public float GetProgress(int sr)
        {
            if (maxSR == minSR) return 1f;
            return Mathf.Clamp01((float)(sr - minSR) / (maxSR - minSR));
        }
    }
}
