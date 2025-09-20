using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Manages leaderboard data retrieval and submission using PlayFab
    /// Handles competitive SR leaderboard for top players
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        [Header("Leaderboard Settings")]
        [SerializeField] private string leaderboardName = "CompetitiveSR";
        [SerializeField] private int maxLeaderboardEntries = 100;
        [SerializeField] private int minimumSRForLeaderboard = 50;
        [SerializeField] private bool enableDebugLogs = true;

        // ----------------- Events -----------------
        public static event Action<List<LeaderboardEntry>> OnLeaderboardLoaded;
        public static event Action<string> OnLeaderboardError;
        
        private List<LeaderboardEntry> currentLeaderboard = new List<LeaderboardEntry>();
        private bool isLoading = false;
        
        public static LeaderboardManager Instance { get; private set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Load the competitive SR leaderboard
        /// </summary>
        public void LoadLeaderboard()
        {
            if (isLoading)
            {
                if (enableDebugLogs) Debug.LogWarning("[LeaderboardManager] Already loading leaderboard");
                return;
            }
            
            isLoading = true;
            
            var request = new GetLeaderboardRequest
            {
                StatisticName = leaderboardName,
                StartPosition = 0,
                MaxResultsCount = maxLeaderboardEntries
            };
            
            if (enableDebugLogs) Debug.Log($"[LeaderboardManager] Loading leaderboard: {leaderboardName}");
            
            PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardSuccess, OnLeaderboardCallbackError);
        }
        
        private void OnLeaderboardSuccess(GetLeaderboardResult result)
        {
            try
            {
                currentLeaderboard.Clear();
                
                foreach (var entry in result.Leaderboard)
                {
                    var leaderboardEntry = new LeaderboardEntry
                    {
                        rank = entry.Position + 1, // PlayFab uses 0-based indexing
                        playerName = entry.DisplayName ?? "Unknown Player",
                        playerId = entry.PlayFabId,
                        sr = entry.StatValue,
                        rankInfo = RankingSystem.GetRankFromSR(entry.StatValue, ModularRewardCalculator.Instance?.config)
                    };
                    
                    currentLeaderboard.Add(leaderboardEntry);
                }
                
                if (enableDebugLogs) Debug.Log($"[LeaderboardManager] Loaded {currentLeaderboard.Count} leaderboard entries");
                OnLeaderboardLoaded?.Invoke(new List<LeaderboardEntry>(currentLeaderboard));
            }
            catch (Exception ex)
            {
                if (enableDebugLogs) Debug.LogError($"[LeaderboardManager] Exception processing leaderboard: {ex.Message}");
                OnLeaderboardError?.Invoke(ex.Message);
            }
            finally
            {
                isLoading = false;
            }
        }
        
        private void OnLeaderboardCallbackError(PlayFabError error)
        {
            if (enableDebugLogs) Debug.LogError($"[LeaderboardManager] Error loading leaderboard: {error.ErrorMessage}");
            OnLeaderboardError?.Invoke(error.ErrorMessage);
            isLoading = false;
        }
        
        /// <summary>
        /// Submit player's competitive SR to the leaderboard
        /// Only submits if SR meets minimum requirement
        /// </summary>
        public void SubmitScore(int sr)
        {
            // Check if SR meets minimum requirement for leaderboard
            if (sr < minimumSRForLeaderboard)
            {
                if (enableDebugLogs) Debug.Log($"[LeaderboardManager] SR {sr} below minimum threshold {minimumSRForLeaderboard}, not submitting to leaderboard");
                return;
            }
            
            var request = new UpdatePlayerStatisticsRequest
            {
                Statistics = new List<StatisticUpdate>
                {
                    new StatisticUpdate
                    {
                        StatisticName = leaderboardName,
                        Value = sr
                    }
                }
            };
            
            if (enableDebugLogs) Debug.Log($"[LeaderboardManager] Submitting SR: {sr} (meets minimum threshold {minimumSRForLeaderboard})");
            
            PlayFabClientAPI.UpdatePlayerStatistics(request, OnScoreSubmitSuccess, OnScoreSubmitError);
        }
        
        private void OnScoreSubmitSuccess(UpdatePlayerStatisticsResult result)
        {
            if (enableDebugLogs) Debug.Log("[LeaderboardManager] Successfully submitted SR to leaderboard");
        }
        
        private void OnScoreSubmitError(PlayFabError error)
        {
            if (enableDebugLogs) Debug.LogError($"[LeaderboardManager] Error submitting score: {error.ErrorMessage}");
        }
        
        /// <summary>
        /// Get the current leaderboard data
        /// </summary>
        public List<LeaderboardEntry> GetCurrentLeaderboard()
        {
            return new List<LeaderboardEntry>(currentLeaderboard);
        }
        
        /// <summary>
        /// Find player's rank in the current leaderboard
        /// </summary>
        public int GetPlayerRank(string playerId = null)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                // Get current player's PlayFab ID from PlayFabAuthManager
                if (PlayFabAuthManager.Instance != null && PlayFabAuthManager.Instance.IsAuthenticated)
                {
                    playerId = PlayFabAuthManager.Instance.PlayFabId;
                }
                else
                {
                    playerId = PlayFabSettings.staticSettings.TitleId; // Fallback
                }
            }
            
            for (int i = 0; i < currentLeaderboard.Count; i++)
            {
                if (currentLeaderboard[i].playerId == playerId)
                {
                    return i; // Return index for UI scrolling
                }
            }
            
            return -1; // Player not found in top 100
        }
        
        /// <summary>
        /// Load leaderboard around current player for Find Me functionality
        /// This ensures the player's position is always visible even if they're not in top 100
        /// </summary>
        public void LoadLeaderboardAroundPlayer()
        {
            if (isLoading)
            {
                if (enableDebugLogs) Debug.LogWarning("[LeaderboardManager] Already loading leaderboard");
                return;
            }
            
            isLoading = true;
            
            var request = new GetLeaderboardAroundPlayerRequest
            {
                StatisticName = leaderboardName,
                MaxResultsCount = 20 // Show 10 players above and below current player
            };
            
            if (enableDebugLogs) Debug.Log($"[LeaderboardManager] Loading leaderboard around player: {leaderboardName}");
            
            PlayFabClientAPI.GetLeaderboardAroundPlayer(request, OnLeaderboardAroundPlayerSuccess, OnLeaderboardCallbackError);
        }
        
        private void OnLeaderboardAroundPlayerSuccess(GetLeaderboardAroundPlayerResult result)
        {
            try
            {
                currentLeaderboard.Clear();
                
                foreach (var entry in result.Leaderboard)
                {
                    var leaderboardEntry = new LeaderboardEntry
                    {
                        rank = entry.Position + 1, // PlayFab uses 0-based indexing
                        playerName = entry.DisplayName ?? "Unknown Player",
                        playerId = entry.PlayFabId,
                        sr = entry.StatValue,
                        rankInfo = RankingSystem.GetRankFromSR(entry.StatValue, ModularRewardCalculator.Instance?.config)
                    };
                    
                    currentLeaderboard.Add(leaderboardEntry);
                }
                
                if (enableDebugLogs) Debug.Log($"[LeaderboardManager] Loaded {currentLeaderboard.Count} leaderboard entries around player");
                OnLeaderboardLoaded?.Invoke(new List<LeaderboardEntry>(currentLeaderboard));
            }
            catch (Exception ex)
            {
                if (enableDebugLogs) Debug.LogError($"[LeaderboardManager] Exception processing leaderboard around player: {ex.Message}");
                OnLeaderboardError?.Invoke(ex.Message);
            }
            finally
            {
                isLoading = false;
            }
        }

        /// <summary>
        /// Force refresh the leaderboard
        /// </summary>
        [ContextMenu("Force Refresh Leaderboard")]
        public void ForceRefreshLeaderboard()
        {
            LoadLeaderboard();
        }
        
        /// <summary>
        /// Check if leaderboard is currently loading
        /// </summary>
        public bool IsLoading()
        {
            return isLoading;
        }
        
        /// <summary>
        /// Check if a player's SR qualifies for leaderboard visibility
        /// </summary>
        public bool QualifiesForLeaderboard(int sr)
        {
            return sr >= minimumSRForLeaderboard;
        }
        
        /// <summary>
        /// Get the minimum SR required for leaderboard
        /// </summary>
        public int GetMinimumSRForLeaderboard()
        {
            return minimumSRForLeaderboard;
        }
    }
    
    /// <summary>
    /// Data structure for leaderboard entries
    /// </summary>
    [System.Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string playerName;
        public string playerId;
        public int sr;
        public RankInfo rankInfo;
        
        /// <summary>
        /// Get formatted rank string with # prefix
        /// </summary>
        public string GetFormattedRank()
        {
            return $"#{rank}";
        }
        
        /// <summary>
        /// Get formatted SR string with commas
        /// </summary>
        public string GetFormattedSR()
        {
            return $"{sr:N0} SR";
        }
        
        /// <summary>
        /// Get rank icon emoji based on rank tier
        /// </summary>
        public string GetRankIcon()
        {
            if (rankInfo == null) return "🥉";
            
            // Map rank names to emojis
            switch (rankInfo.rankName)
            {
                case "Rumbler":
                    return "🏆";
                case "Dodger":
                    return "💎";
                case "Platinum":
                    return "🥇";
                case "Gold":
                    return "🥇";
                case "Silver":
                    return "🥈";
                case "Bronze":
                default:
                    return "🥉";
            }
        }
        
        /// <summary>
        /// Get truncated player name for display
        /// </summary>
        public string GetTruncatedPlayerName(int maxLength = 15)
        {
            if (string.IsNullOrEmpty(playerName)) return "Unknown";
            
            if (playerName.Length <= maxLength) return playerName;
            
            return playerName.Substring(0, maxLength - 3) + "...";
        }
    }
}
