using System;
using UnityEngine;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Complete match result data for progression system processing
    /// Contains all necessary information to calculate rewards and update statistics
    /// </summary>
    [System.Serializable]
    public struct MatchResult
    {
        [Header("Match Information")]
        public GameMode gameMode;
        public bool isWin;
        public int finalScore; // e.g., 5-3 (your score - opponent score)
        public float matchDuration;
        public DateTime matchTime;
        
        [Header("Performance Metrics")]
        public int damageDealt;
        public int damageTaken;
        public string characterUsed;
        public int opponentSR; // For competitive matches
        
        [Header("Match Context")]
        public bool wasComeback; // Won after being behind
        public bool wasChoke;    // Lost after being ahead
        public bool wasFirstWinOfDay;
        public int currentWinStreak;
        
        [Header("Series Information")]
        public int seriesMatch; // Which match in the series (for competitive)
        public int totalSeriesMatches;
        public bool isSeriesComplete;
        public int seriesWinner;
        
        /// <summary>
        /// Create a new match result with basic information
        /// </summary>
        public static MatchResult Create(GameMode mode, bool won, int score, float duration, string character)
        {
            return new MatchResult
            {
                gameMode = mode,
                isWin = won,
                finalScore = score,
                matchDuration = duration,
                characterUsed = character,
                matchTime = DateTime.Now,
                damageDealt = 0,
                damageTaken = 0,
                opponentSR = 0,
                wasComeback = false,
                wasChoke = false,
                wasFirstWinOfDay = false,
                currentWinStreak = 0,
                seriesMatch = 1,
                totalSeriesMatches = 1,
                isSeriesComplete = true,
                seriesWinner = won ? 1 : 2
            };
        }
        
        /// <summary>
        /// Create a competitive match result with series information
        /// </summary>
        public static MatchResult CreateCompetitive(bool won, int score, float duration, string character, 
            int seriesMatch, int totalSeries, bool seriesComplete, int seriesWinner, int opponentSR)
        {
            return new MatchResult
            {
                gameMode = GameMode.Competitive,
                isWin = won,
                finalScore = score,
                matchDuration = duration,
                characterUsed = character,
                matchTime = DateTime.Now,
                damageDealt = 0,
                damageTaken = 0,
                opponentSR = opponentSR,
                wasComeback = false,
                wasChoke = false,
                wasFirstWinOfDay = false,
                currentWinStreak = 0,
                seriesMatch = seriesMatch,
                totalSeriesMatches = totalSeries,
                isSeriesComplete = seriesComplete,
                seriesWinner = seriesWinner
            };
        }
        
        /// <summary>
        /// Create an AI practice match result
        /// </summary>
        public static MatchResult CreateAI(bool won, int score, float duration, string character, int damageDealt, int damageTaken)
        {
            return new MatchResult
            {
                gameMode = GameMode.AI,
                isWin = won,
                finalScore = score,
                matchDuration = duration,
                characterUsed = character,
                matchTime = DateTime.Now,
                damageDealt = damageDealt,
                damageTaken = damageTaken,
                opponentSR = 0,
                wasComeback = false,
                wasChoke = false,
                wasFirstWinOfDay = false,
                currentWinStreak = 0,
                seriesMatch = 1,
                totalSeriesMatches = 1,
                isSeriesComplete = true,
                seriesWinner = won ? 1 : 2
            };
        }
        
        /// <summary>
        /// Create a custom match result (no rewards)
        /// </summary>
        public static MatchResult CreateCustom(bool won, int score, float duration, string character)
        {
            return new MatchResult
            {
                gameMode = GameMode.Custom,
                isWin = won,
                finalScore = score,
                matchDuration = duration,
                characterUsed = character,
                matchTime = DateTime.Now,
                damageDealt = 0,
                damageTaken = 0,
                opponentSR = 0,
                wasComeback = false,
                wasChoke = false,
                wasFirstWinOfDay = false,
                currentWinStreak = 0,
                seriesMatch = 1,
                totalSeriesMatches = 1,
                isSeriesComplete = true,
                seriesWinner = won ? 1 : 2
            };
        }
        
        /// <summary>
        /// Set performance metrics for the match
        /// </summary>
        public void SetPerformanceMetrics(int dealt, int taken, bool comeback = false, bool choke = false)
        {
            damageDealt = dealt;
            damageTaken = taken;
            wasComeback = comeback;
            wasChoke = choke;
        }
        
        /// <summary>
        /// Set progression context
        /// </summary>
        public void SetProgressionContext(bool firstWinOfDay, int winStreak)
        {
            wasFirstWinOfDay = firstWinOfDay;
            currentWinStreak = winStreak;
        }
        
        /// <summary>
        /// Get damage ratio for performance calculations
        /// </summary>
        public float GetDamageRatio()
        {
            if (damageTaken == 0) return damageDealt > 0 ? 2f : 1f;
            return (float)damageDealt / damageTaken;
        }
        
        /// <summary>
        /// Get match quality score (0-1) based on performance
        /// </summary>
        public float GetMatchQuality()
        {
            float quality = 0.5f; // Base quality
            
            // Damage ratio bonus
            float damageRatio = GetDamageRatio();
            if (damageRatio > 1.5f) quality += 0.2f;
            else if (damageRatio < 0.7f) quality -= 0.1f;
            
            // Score difference bonus
            if (isWin && finalScore >= 4) quality += 0.2f; // Dominant win
            else if (!isWin && finalScore >= 3) quality += 0.1f; // Close loss
            
            // Comeback/choke modifiers
            if (wasComeback) quality += 0.1f;
            if (wasChoke) quality -= 0.1f;
            
            return Mathf.Clamp01(quality);
        }
        
        /// <summary>
        /// Check if this match should give rewards
        /// </summary>
        public bool ShouldGiveRewards()
        {
            return gameMode != GameMode.Custom; // Custom matches give no rewards
        }
        
        /// <summary>
        /// Get a summary string for logging/debugging
        /// </summary>
        public string GetSummary()
        {
            return $"{gameMode} Match: {(isWin ? "WIN" : "LOSS")} {finalScore}, " +
                   $"Duration: {matchDuration:F1}s, Character: {characterUsed}, " +
                   $"Damage: {damageDealt}/{damageTaken}, Quality: {GetMatchQuality():F2}";
        }
        
        /// <summary>
        /// Validate the match result data
        /// </summary>
        public bool IsValid()
        {
            return matchDuration > 0f && 
                   !string.IsNullOrEmpty(characterUsed) && 
                   finalScore >= 0 && 
                   damageDealt >= 0 && 
                   damageTaken >= 0;
        }
    }
}

