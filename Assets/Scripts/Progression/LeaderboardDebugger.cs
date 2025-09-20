using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Debug tool for troubleshooting leaderboard issues
    /// Helps identify why players aren't appearing on leaderboard
    /// </summary>
    public class LeaderboardDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private string testLeaderboardName = "CompetitiveSR";
        
        [Header("Test Values")]
        [SerializeField] private int testSR = 5000; // High SR for testing
        
        void Start()
        {
            // Subscribe to player data events to run checks when data is available
            PlayerDataManager.OnDataLoaded += OnPlayerDataLoaded;
            PlayerDataManager.OnDataUpdated += OnPlayerDataUpdated;
            
            // If data is already loaded, run checks immediately
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsDataLoaded())
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[LeaderboardDebugger] Player data already loaded, running debug checks...");
                    RunAllDebugChecks();
                }
            }
            else
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[LeaderboardDebugger] Waiting for player data to load before running debug checks...");
                }
            }
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            PlayerDataManager.OnDataLoaded -= OnPlayerDataLoaded;
            PlayerDataManager.OnDataUpdated -= OnPlayerDataUpdated;
        }
        
        private void OnPlayerDataLoaded(PlayerProgressionData data)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[LeaderboardDebugger] Player data loaded, running debug checks...");
                RunAllDebugChecks();
            }
        }
        
        private void OnPlayerDataUpdated(PlayerProgressionData data)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[LeaderboardDebugger] Player data updated, running debug checks...");
                RunAllDebugChecks();
            }
        }
        
        /// <summary>
        /// Run all debug checks to identify leaderboard issues
        /// </summary>
        [ContextMenu("Run All Debug Checks")]
        public void RunAllDebugChecks()
        {
            Debug.Log("=== LEADERBOARD DEBUG CHECKS ===");
            
            CheckPlayFabAuthentication();
            CheckPlayerData();
            CheckLeaderboardManager();
            CheckPlayFabLeaderboard();
            TestSRSubmission();
        }
        
        /// <summary>
        /// Check PlayFab authentication status
        /// </summary>
        [ContextMenu("Check PlayFab Authentication")]
        public void CheckPlayFabAuthentication()
        {
            Debug.Log("--- PlayFab Authentication Check ---");
            
            if (PlayFabSettings.staticSettings == null)
            {
                Debug.LogError("[LeaderboardDebugger] PlayFabSettings is null!");
                return;
            }
            
            Debug.Log($"[LeaderboardDebugger] PlayFab Title ID: {PlayFabSettings.staticSettings.TitleId}");
            Debug.Log($"[LeaderboardDebugger] PlayFab Developer Secret Key: {(string.IsNullOrEmpty(PlayFabSettings.staticSettings.DeveloperSecretKey) ? "NOT SET" : "SET")}");
            
            // Check if player is logged in
            if (PlayFabClientAPI.IsClientLoggedIn())
            {
                Debug.Log("[LeaderboardDebugger] ✅ Player is logged in to PlayFab");
                Debug.Log($"[LeaderboardDebugger] Player ID: {PlayFabSettings.staticSettings.TitleId}");
            }
            else
            {
                Debug.LogError("[LeaderboardDebugger] ❌ Player is NOT logged in to PlayFab!");
            }
        }
        
        /// <summary>
        /// Check player data and SR
        /// </summary>
        [ContextMenu("Check Player Data")]
        public void CheckPlayerData()
        {
            Debug.Log("--- Player Data Check ---");
            
            if (PlayerDataManager.Instance == null)
            {
                Debug.LogError("[LeaderboardDebugger] ❌ PlayerDataManager.Instance is null!");
                return;
            }
            
            if (!PlayerDataManager.Instance.IsDataLoaded())
            {
                Debug.LogError("[LeaderboardDebugger] ❌ Player data is not loaded!");
                return;
            }
            
            var playerData = PlayerDataManager.Instance.GetPlayerData();
            Debug.Log($"[LeaderboardDebugger] ✅ Player Data Loaded:");
            Debug.Log($"  - Current Level: {playerData.currentLevel}");
            Debug.Log($"  - Current XP: {playerData.currentXP}");
            Debug.Log($"  - Competitive SR: {playerData.competitiveSR}");
            Debug.Log($"  - Current Rank: {playerData.currentRank}");
            Debug.Log($"  - Total Matches: {playerData.totalMatches}");
            Debug.Log($"  - Total Wins: {playerData.totalWins}");
            
            // Check if SR qualifies for leaderboard using the new threshold system
            if (LeaderboardManager.Instance != null)
            {
                bool qualifies = LeaderboardManager.Instance.QualifiesForLeaderboard(playerData.competitiveSR);
                int minSR = LeaderboardManager.Instance.GetMinimumSRForLeaderboard();
                
                if (qualifies)
                {
                    Debug.Log($"[LeaderboardDebugger] ✅ Player qualifies for leaderboard (SR {playerData.competitiveSR} >= {minSR})");
                }
                else
                {
                    Debug.Log($"[LeaderboardDebugger] ❌ Player does NOT qualify for leaderboard (SR {playerData.competitiveSR} < {minSR})");
                }
            }
            else
            {
                Debug.Log($"[LeaderboardDebugger] ✅ SR {playerData.competitiveSR} should be visible on leaderboard");
            }
        }
        
        /// <summary>
        /// Check LeaderboardManager status
        /// </summary>
        [ContextMenu("Check LeaderboardManager")]
        public void CheckLeaderboardManager()
        {
            Debug.Log("--- LeaderboardManager Check ---");
            
            if (LeaderboardManager.Instance == null)
            {
                Debug.LogError("[LeaderboardDebugger] ❌ LeaderboardManager.Instance is null!");
                return;
            }
            
            Debug.Log("[LeaderboardDebugger] ✅ LeaderboardManager found");
            
            var currentLeaderboard = LeaderboardManager.Instance.GetCurrentLeaderboard();
            Debug.Log($"[LeaderboardDebugger] Current leaderboard entries: {currentLeaderboard.Count}");
            
            if (currentLeaderboard.Count == 0)
            {
                Debug.LogWarning("[LeaderboardDebugger] ⚠️ No leaderboard entries loaded - try refreshing");
            }
            else
            {
                Debug.Log("[LeaderboardDebugger] ✅ Leaderboard has entries loaded");
                
                // Show top 5 entries
                for (int i = 0; i < Mathf.Min(5, currentLeaderboard.Count); i++)
                {
                    var entry = currentLeaderboard[i];
                    Debug.Log($"  #{entry.rank}: {entry.playerName} - {entry.sr} SR ({entry.rankInfo?.rankName})");
                }
            }
        }
        
        /// <summary>
        /// Check PlayFab leaderboard directly
        /// </summary>
        [ContextMenu("Check PlayFab Leaderboard")]
        public void CheckPlayFabLeaderboard()
        {
            Debug.Log("--- PlayFab Leaderboard Check ---");
            
            var request = new GetLeaderboardRequest
            {
                StatisticName = testLeaderboardName,
                StartPosition = 0,
                MaxResultsCount = 10
            };
            
            Debug.Log($"[LeaderboardDebugger] Requesting leaderboard: {testLeaderboardName}");
            
            PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardCheckSuccess, OnLeaderboardCheckError);
        }
        
        private void OnLeaderboardCheckSuccess(GetLeaderboardResult result)
        {
            Debug.Log($"[LeaderboardDebugger] ✅ PlayFab leaderboard loaded successfully!");
            Debug.Log($"[LeaderboardDebugger] Total entries: {result.Leaderboard.Count}");
            
            if (result.Leaderboard.Count == 0)
            {
                Debug.LogWarning("[LeaderboardDebugger] ⚠️ No entries in PlayFab leaderboard - this might be the issue!");
                Debug.Log("[LeaderboardDebugger] 💡 Try submitting a test score");
            }
            else
            {
                Debug.Log("[LeaderboardDebugger] Top 10 entries from PlayFab:");
                for (int i = 0; i < result.Leaderboard.Count; i++)
                {
                    var entry = result.Leaderboard[i];
                    Debug.Log($"  #{entry.Position + 1}: {entry.DisplayName} - {entry.StatValue} SR");
                }
            }
        }
        
        private void OnLeaderboardCheckError(PlayFabError error)
        {
            Debug.LogError($"[LeaderboardDebugger] ❌ PlayFab leaderboard error: {error.ErrorMessage}");
            Debug.LogError($"[LeaderboardDebugger] Error details: {error.GenerateErrorReport()}");
            
            if (error.ErrorMessage.Contains("StatisticName"))
            {
                Debug.LogError("[LeaderboardDebugger] 💡 Leaderboard 'CompetitiveSR' might not exist in PlayFab dashboard!");
                Debug.LogError("[LeaderboardDebugger] 💡 Create the leaderboard in PlayFab dashboard first");
            }
        }
        
        /// <summary>
        /// Test SR submission with high value
        /// </summary>
        [ContextMenu("Test SR Submission")]
        public void TestSRSubmission()
        {
            Debug.Log("--- SR Submission Test ---");
            
            if (LeaderboardManager.Instance == null)
            {
                Debug.LogError("[LeaderboardDebugger] ❌ LeaderboardManager not found!");
                return;
            }
            
            Debug.Log($"[LeaderboardDebugger] Submitting test SR: {testSR}");
            LeaderboardManager.Instance.SubmitScore(testSR);
            
            // Wait a moment then check if it appears
            Invoke(nameof(CheckAfterSubmission), 2f);
        }
        
        private void CheckAfterSubmission()
        {
            Debug.Log("[LeaderboardDebugger] Checking leaderboard after test submission...");
            CheckPlayFabLeaderboard();
        }
        
        /// <summary>
        /// Force update player SR to high value
        /// </summary>
        [ContextMenu("Force Update Player SR")]
        public void ForceUpdatePlayerSR()
        {
            Debug.Log("--- Force Update Player SR ---");
            
            if (PlayerDataManager.Instance == null || !PlayerDataManager.Instance.IsDataLoaded())
            {
                Debug.LogError("[LeaderboardDebugger] ❌ PlayerDataManager not available!");
                return;
            }
            
            var playerData = PlayerDataManager.Instance.GetPlayerData();
            int oldSR = playerData.competitiveSR;
            playerData.competitiveSR = testSR;
            
            Debug.Log($"[LeaderboardDebugger] Updated SR: {oldSR} → {testSR}");
            
            // Submit to leaderboard
            LeaderboardManager.Instance.SubmitScore(testSR);
            
            Debug.Log("[LeaderboardDebugger] ✅ SR updated and submitted to leaderboard");
        }
        
        /// <summary>
        /// Check if current player appears in leaderboard
        /// </summary>
        [ContextMenu("Check Player Rank")]
        public void CheckPlayerRank()
        {
            Debug.Log("--- Player Rank Check ---");
            
            if (LeaderboardManager.Instance == null)
            {
                Debug.LogError("[LeaderboardDebugger] ❌ LeaderboardManager not found!");
                return;
            }
            
            int playerRank = LeaderboardManager.Instance.GetPlayerRank();
            
            if (playerRank == -1)
            {
                Debug.LogWarning("[LeaderboardDebugger] ⚠️ Player not found in top 100 leaderboard");
                Debug.Log("[LeaderboardDebugger] 💡 This could mean:");
                Debug.Log("  - SR is too low for top 100");
                Debug.Log("  - Player ID mismatch");
                Debug.Log("  - Leaderboard not loaded");
            }
            else
            {
                Debug.Log($"[LeaderboardDebugger] ✅ Player found at rank: {playerRank + 1}");
            }
        }
        
        /// <summary>
        /// Get comprehensive debug report
        /// </summary>
        [ContextMenu("Generate Debug Report")]
        public void GenerateDebugReport()
        {
            Debug.Log("=== COMPREHENSIVE LEADERBOARD DEBUG REPORT ===");
            
            CheckPlayFabAuthentication();
            CheckPlayerData();
            CheckLeaderboardManager();
            CheckPlayFabLeaderboard();
            CheckPlayerRank();
            
            Debug.Log("=== END DEBUG REPORT ===");
        }
    }
}
