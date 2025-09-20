using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Core player progression data structure for Retro Dodge Rumble
    /// Handles XP, levels, currency, competitive SR, and match statistics
    /// </summary>
    [System.Serializable]
    public class PlayerProgressionData
    {
        [Header("Experience & Level")]
        public int currentXP = 0;
        public int currentLevel = 1;
        public int xpToNextLevel = 1000;
        
        [Header("Currency")]
        public int dodgeCoins = 800;      // Soft currency
        public int rumbleTokens = 0;      // Hard currency (premium)
        
        [Header("Competitive Stats")]
        public int competitiveSR = 0;     // Skill Rating
        public string currentRank = "Unranked";
        public int placementMatchesLeft = 5;
        public bool isRanked = false;
        
        [Header("Match Statistics")]
        public int totalMatches = 0;
        public int totalWins = 0;
        public int totalLosses = 0;
        public int winStreak = 0;
        public int bestWinStreak = 0;
        
        [Header("Mode-specific Stats")]
        public PlayerModeStats casualStats = new PlayerModeStats();
        public PlayerModeStats competitiveStats = new PlayerModeStats();
        public PlayerModeStats aiStats = new PlayerModeStats();
        
        [Header("Rewards Tracking")]
        public bool hasFirstWinOfDay = true;
        public DateTime lastLogin = DateTime.Now;
        public DateTime lastSRDecay = DateTime.Now;
        
        [Header("Character Unlocks")]
        public List<int> unlockedCharacters = new List<int> { 0 }; // Start with first character unlocked
        
        [Header("Achievements")]
        public List<string> completedAchievements = new List<string>();
        public List<string> unlockedAchievements = new List<string>();
        
        [Header("Daily/Weekly Challenges")]
        public List<ChallengeProgress> activeChallenges = new List<ChallengeProgress>();
        public DateTime lastChallengeRefresh = DateTime.Now;
        
        /// <summary>
        /// Default constructor with sensible starting values
        /// </summary>
        public PlayerProgressionData()
        {
            // Initialize with default values
            currentXP = 0;
            currentLevel = 1;
            xpToNextLevel = 1000;
            dodgeCoins = 800;
            rumbleTokens = 0;
            competitiveSR = 0;
            currentRank = "Unranked";
            placementMatchesLeft = 5;
            isRanked = false;
            totalMatches = 0;
            totalWins = 0;
            totalLosses = 0;
            winStreak = 0;
            bestWinStreak = 0;
            hasFirstWinOfDay = true;
            lastLogin = DateTime.Now;
            lastSRDecay = DateTime.Now;
            unlockedCharacters = new List<int> { 0 };
            completedAchievements = new List<string>();
            activeChallenges = new List<ChallengeProgress>();
            lastChallengeRefresh = DateTime.Now;
        }
        
        /// <summary>
        /// Add XP and handle level up logic
        /// </summary>
        public bool AddXP(int xpAmount, out int levelsGained)
        {
            levelsGained = 0;
            if (xpAmount <= 0) return false;
            
            currentXP += xpAmount;
            
            // Check for level ups
            while (currentXP >= xpToNextLevel)
            {
                currentXP -= xpToNextLevel;
                currentLevel++;
                levelsGained++;
                
                // Calculate next level requirement (exponential growth)
                xpToNextLevel = Mathf.RoundToInt(1000 * Mathf.Pow(1.15f, currentLevel - 1));
            }
            
            return levelsGained > 0;
        }
        
        /// <summary>
        /// Add currency with validation
        /// </summary>
        public void AddCurrency(int coins, int tokens = 0)
        {
            dodgeCoins = Mathf.Max(0, dodgeCoins + coins);
            rumbleTokens = Mathf.Max(0, rumbleTokens + tokens);
        }
        
        /// <summary>
        /// Spend currency with validation
        /// </summary>
        public bool SpendCurrency(int coins, int tokens = 0)
        {
            if (dodgeCoins >= coins && rumbleTokens >= tokens)
            {
                dodgeCoins -= coins;
                rumbleTokens -= tokens;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Update competitive SR and rank
        /// </summary>
        public void UpdateSR(int srChange, string newRank)
        {
            competitiveSR = Mathf.Max(0, competitiveSR + srChange);
            currentRank = newRank;
            
            if (placementMatchesLeft > 0)
            {
                placementMatchesLeft--;
                if (placementMatchesLeft <= 0)
                {
                    isRanked = true;
                }
            }
        }
        
        /// <summary>
        /// Record a match result
        /// </summary>
        public void RecordMatch(GameMode mode, bool isWin, int damageDealt, int damageTaken, float matchDuration)
        {
            totalMatches++;
            
            if (isWin)
            {
                totalWins++;
                winStreak++;
                bestWinStreak = Mathf.Max(bestWinStreak, winStreak);
            }
            else
            {
                totalLosses++;
                winStreak = 0;
            }
            
            // Update mode-specific stats
            var modeStats = GetModeStats(mode);
            modeStats.matches++;
            if (isWin) modeStats.wins++;
            else modeStats.losses++;
            
            modeStats.damageDealt += damageDealt;
            modeStats.damageTaken += damageTaken;
            modeStats.averageMatchDuration = (modeStats.averageMatchDuration * (modeStats.matches - 1) + matchDuration) / modeStats.matches;
        }
        
        /// <summary>
        /// Get mode-specific statistics
        /// </summary>
        public PlayerModeStats GetModeStats(GameMode mode)
        {
            return mode switch
            {
                GameMode.Casual => casualStats,
                GameMode.Competitive => competitiveStats,
                GameMode.AI => aiStats,
                _ => casualStats
            };
        }
        
        /// <summary>
        /// Check if character is unlocked
        /// </summary>
        public bool IsCharacterUnlocked(int characterIndex)
        {
            return unlockedCharacters.Contains(characterIndex);
        }
        
        /// <summary>
        /// Unlock a character
        /// </summary>
        public bool UnlockCharacter(int characterIndex)
        {
            if (!unlockedCharacters.Contains(characterIndex))
            {
                unlockedCharacters.Add(characterIndex);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Check if achievement is completed
        /// </summary>
        public bool IsAchievementCompleted(string achievementId)
        {
            return completedAchievements.Contains(achievementId);
        }
        
        /// <summary>
        /// Complete an achievement
        /// </summary>
        public bool CompleteAchievement(string achievementId)
        {
            if (!completedAchievements.Contains(achievementId))
            {
                completedAchievements.Add(achievementId);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get win rate percentage
        /// </summary>
        public float GetWinRate()
        {
            if (totalMatches == 0) return 0f;
            return (float)totalWins / totalMatches * 100f;
        }
        
        /// <summary>
        /// Get win rate for specific mode
        /// </summary>
        public float GetModeWinRate(GameMode mode)
        {
            var stats = GetModeStats(mode);
            if (stats.matches == 0) return 0f;
            return (float)stats.wins / stats.matches * 100f;
        }
        
        /// <summary>
        /// Check if it's a new day for first win bonus
        /// </summary>
        public bool IsNewDay()
        {
            return DateTime.Now.Date > lastLogin.Date;
        }
        
        /// <summary>
        /// Reset first win of day if it's a new day
        /// </summary>
        public void CheckFirstWinOfDay()
        {
            if (IsNewDay())
            {
                hasFirstWinOfDay = true;
                lastLogin = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Check if SR decay should occur
        /// </summary>
        public bool ShouldDecaySR()
        {
            return isRanked && (DateTime.Now - lastSRDecay).TotalDays >= 10;
        }
        
        /// <summary>
        /// Apply SR decay
        /// </summary>
        public void ApplySRDecay(int decayAmount)
        {
            if (ShouldDecaySR())
            {
                competitiveSR = Mathf.Max(0, competitiveSR - decayAmount);
                lastSRDecay = DateTime.Now;
            }
        }
    }
    
    /// <summary>
    /// Mode-specific player statistics
    /// </summary>
    [System.Serializable]
    public class PlayerModeStats
    {
        public int matches = 0;
        public int wins = 0;
        public int losses = 0;
        public int damageDealt = 0;
        public int damageTaken = 0;
        public float averageMatchDuration = 0f;
        
        /// <summary>
        /// Get win rate for this mode
        /// </summary>
        public float GetWinRate()
        {
            if (matches == 0) return 0f;
            return (float)wins / matches * 100f;
        }
        
        /// <summary>
        /// Get damage ratio (dealt/taken)
        /// </summary>
        public float GetDamageRatio()
        {
            if (damageTaken == 0) return damageDealt > 0 ? 2f : 1f;
            return (float)damageDealt / damageTaken;
        }
    }
    
    /// <summary>
    /// Challenge progress tracking
    /// </summary>
    [System.Serializable]
    public class ChallengeProgress
    {
        public string challengeId;
        public string description;
        public int currentProgress;
        public int targetProgress;
        public ChallengeType type;
        public DateTime expiryTime;
        public bool isCompleted;
        
        public ChallengeProgress(string id, string desc, int target, ChallengeType challengeType, DateTime expiry)
        {
            challengeId = id;
            description = desc;
            currentProgress = 0;
            targetProgress = target;
            type = challengeType;
            expiryTime = expiry;
            isCompleted = false;
        }
        
        public float GetProgressPercentage()
        {
            if (targetProgress == 0) return 0f;
            return Mathf.Clamp01((float)currentProgress / targetProgress);
        }
        
        public bool IsExpired()
        {
            return DateTime.Now > expiryTime;
        }
    }
    
    /// <summary>
    /// Challenge types for daily/weekly challenges
    /// </summary>
    public enum ChallengeType
    {
        WinMatches,
        DealDamage,
        PlayTime,
        WinStreak,
        CharacterSpecific,
        ModeSpecific
    }
    
    /// <summary>
    /// Game modes for progression tracking
    /// </summary>
    public enum GameMode
    {
        Casual,
        Competitive,
        AI,
        Custom
    }
}
