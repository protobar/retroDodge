using UnityEngine;
using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Centralized manager for player progression data
    /// Handles PlayFab integration, local caching, and data synchronization
    /// </summary>
    public class PlayerDataManager : MonoBehaviour
    {
        [Header("Configuration")]
        public ProgressionConfiguration config;
        
        [Header("Debug")]
        public bool enableDebugLogs = true;
        public bool autoSaveOnChanges = true;
        
        // Events
        public static event Action<PlayerProgressionData> OnDataLoaded;
        public static event Action<PlayerProgressionData> OnDataUpdated;
        public static event Action<int> OnLevelUp;
        public static event Action<string, string> OnRankUp; // oldRank, newRank
        public static event Action<int> OnCurrencyChanged; // amount
        public static event Action<string> OnAchievementUnlocked; // achievementId
        
        // Last match rewards for UI display
        private MatchRewards? lastMatchRewards;
        
        // Singleton
        public static PlayerDataManager Instance { get; private set; }
        
        // Data
        private PlayerProgressionData playerData;
        private bool isDataLoaded = false;
        private bool isSaving = false;
        private bool hasUnsavedChanges = false;
        
        // PlayFab Keys
        private const string PROGRESSION_DATA_KEY = "PlayerProgressionData";
        private const string LAST_SAVE_TIME_KEY = "LastSaveTime";
        
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
                        Debug.LogError("[PlayerDataManager] No ProgressionConfiguration found in Resources folder.");
                    }
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            LoadPlayerData();
        }
        
        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && hasUnsavedChanges)
            {
                SavePlayerData();
            }
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && hasUnsavedChanges)
            {
                SavePlayerData();
            }
        }
        
        /// <summary>
        /// Load player data from PlayFab
        /// </summary>
        public void LoadPlayerData()
        {
            if (isDataLoaded) return;
            
            if (enableDebugLogs) Debug.Log("[PlayerDataManager] Loading player data from PlayFab...");
            
            var request = new GetUserDataRequest
            {
                Keys = new List<string> { PROGRESSION_DATA_KEY }
            };
            
            PlayFabClientAPI.GetUserData(request, OnDataLoadedSuccess, OnDataLoadedError);
        }
        
        private void OnDataLoadedSuccess(GetUserDataResult result)
        {
            if (result.Data.ContainsKey(PROGRESSION_DATA_KEY))
            {
                try
                {
                    string jsonData = result.Data[PROGRESSION_DATA_KEY].Value;
                    playerData = JsonUtility.FromJson<PlayerProgressionData>(jsonData);
                    
                    // Sync rank with SR in case SR was manually updated in PlayFab dashboard
                    SyncRankWithSR();
                    
                    if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Data loaded successfully. Level: {playerData.currentLevel}, SR: {playerData.competitiveSR}, Rank: {playerData.currentRank}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PlayerDataManager] Failed to parse progression data: {e.Message}");
                    CreateNewPlayerData();
                }
            }
            else
            {
                if (enableDebugLogs) Debug.Log("[PlayerDataManager] No existing data found, creating new player data.");
                CreateNewPlayerData();
            }
            
            isDataLoaded = true;
            OnDataLoaded?.Invoke(playerData);
            
            // Automatically submit existing SR to leaderboard if it qualifies
            SubmitExistingSRToLeaderboard();
        }
        
        private void OnDataLoadedError(PlayFabError error)
        {
            Debug.LogError($"[PlayerDataManager] Failed to load data: {error.ErrorMessage}");
            CreateNewPlayerData();
            isDataLoaded = true;
            OnDataLoaded?.Invoke(playerData);
            
            // Automatically submit existing SR to leaderboard if it qualifies
            SubmitExistingSRToLeaderboard();
        }
        
        private void CreateNewPlayerData()
        {
            playerData = new PlayerProgressionData();
            if (enableDebugLogs) Debug.Log("[PlayerDataManager] Created new player data with default values.");
            
            // Force save new player data to PlayFab immediately
            StartCoroutine(SaveNewPlayerDataCoroutine());
        }
        
        /// <summary>
        /// Save player data to PlayFab
        /// </summary>
        public void SavePlayerData()
        {
            if (!isDataLoaded || isSaving) return;
            
            StartCoroutine(SaveDataCoroutine());
        }
        
        /// <summary>
        /// Save new player data immediately after creation
        /// </summary>
        private IEnumerator SaveNewPlayerDataCoroutine()
        {
            // Wait a frame to ensure data is properly initialized
            yield return null;
            
            if (enableDebugLogs) Debug.Log("[PlayerDataManager] Saving new player data to PlayFab...");
            
            string jsonData = JsonUtility.ToJson(playerData);
            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { PROGRESSION_DATA_KEY, jsonData },
                    { LAST_SAVE_TIME_KEY, DateTime.Now.ToBinary().ToString() }
                }
            };
            
            PlayFabClientAPI.UpdateUserData(request, OnNewDataSavedSuccess, OnNewDataSavedError);
        }
        
        private void OnNewDataSavedSuccess(UpdateUserDataResult result)
        {
            if (enableDebugLogs) Debug.Log("[PlayerDataManager] New player data saved successfully to PlayFab.");
        }
        
        private void OnNewDataSavedError(PlayFabError error)
        {
            Debug.LogError($"[PlayerDataManager] Failed to save new player data: {error.ErrorMessage}");
        }
        
        private IEnumerator SaveDataCoroutine()
        {
            isSaving = true;
            
            if (enableDebugLogs) Debug.Log("[PlayerDataManager] Saving player data to PlayFab...");
            
            string jsonData = JsonUtility.ToJson(playerData);
            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { PROGRESSION_DATA_KEY, jsonData },
                    { LAST_SAVE_TIME_KEY, DateTime.Now.ToBinary().ToString() }
                }
            };
            
            bool saveCompleted = false;
            PlayFabError saveError = null;
            
            PlayFabClientAPI.UpdateUserData(request, 
                result => { saveCompleted = true; },
                error => { saveError = error; saveCompleted = true; });
            
            // Wait for save to complete
            while (!saveCompleted)
            {
                yield return null;
            }
            
            if (saveError == null)
            {
                hasUnsavedChanges = false;
                if (enableDebugLogs) Debug.Log("[PlayerDataManager] Data saved successfully.");
            }
            else
            {
                Debug.LogError($"[PlayerDataManager] Failed to save data: {saveError.ErrorMessage}");
            }
            
            isSaving = false;
        }
        
        /// <summary>
        /// Sync the current rank with the SR value
        /// This ensures rank is always correct when SR is manually updated in PlayFab dashboard
        /// </summary>
        private void SyncRankWithSR()
        {
            if (playerData == null) return;
            
            var rankInfo = RankingSystem.GetRankFromSR(playerData.competitiveSR, config);
            string correctRank = rankInfo.rankName;
            
            if (playerData.currentRank != correctRank)
            {
                if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Syncing rank: {playerData.currentRank} -> {correctRank} (SR: {playerData.competitiveSR})");
                
                playerData.currentRank = correctRank;
                
                // Mark as changed so it gets saved
                hasUnsavedChanges = true;
                
                // Auto-save if enabled
                if (autoSaveOnChanges)
                {
                    SavePlayerData();
                }
            }
        }

        /// <summary>
        /// Apply match rewards to player data
        /// </summary>
        public void ApplyMatchRewards(MatchResult matchResult)
        {
            if (!isDataLoaded) return;
            
            // Check if this match should give rewards
            if (!matchResult.ShouldGiveRewards())
            {
                if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Skipping rewards for {matchResult.gameMode} match (no rewards allowed)");
                return;
            }
            
            if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Applying rewards for {matchResult.gameMode} match. Win: {matchResult.isWin}");
            
            // Calculate rewards
            var rewards = ModularRewardCalculator.Instance.CalculateAllRewards(matchResult, playerData);
            
            // Store last match rewards for UI display
            lastMatchRewards = rewards;
            
            // Apply XP and handle level ups
            int oldLevel = playerData.currentLevel;
            int totalXPGained = rewards.xpGained;
            playerData.currentXP += rewards.xpGained;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerDataManager] Applying {totalXPGained} XP. Current: {playerData.currentXP}/{playerData.xpToNextLevel}");
            }
            
            // Check for level up (handle multiple level ups)
            int levelsGained = 0;
            while (playerData.currentXP >= playerData.xpToNextLevel && playerData.currentLevel < config.maxLevel)
            {
                LevelUp();
                levelsGained++;
            }
            
            if (enableDebugLogs && levelsGained > 0)
            {
                Debug.Log($"[PlayerDataManager] Gained {levelsGained} levels! Final XP: {playerData.currentXP}/{playerData.xpToNextLevel}");
            }
            
            // Apply currency
            playerData.dodgeCoins += rewards.dodgeCoinsGained;
            playerData.rumbleTokens += rewards.rumbleTokensGained;
            
            // Apply SR changes (competitive only)
            if (matchResult.gameMode == GameMode.Competitive)
            {
                string oldRank = playerData.currentRank;
                playerData.competitiveSR += rewards.srChange;
                playerData.currentRank = rewards.newRank;
                
                // Ensure rank is synced with SR (safety check)
                SyncRankWithSR();
                
                // Submit SR to leaderboard
                if (LeaderboardManager.Instance != null)
                {
                    LeaderboardManager.Instance.SubmitScore(playerData.competitiveSR);
                    if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Submitted SR {playerData.competitiveSR} to leaderboard");
                }
                
                // Check for rank up/down
                if (rewards.rankedUp)
                {
                    OnRankUp?.Invoke(oldRank, playerData.currentRank);
                    if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Rank up! {oldRank} -> {playerData.currentRank}");
                }
                else if (rewards.rankedDown)
                {
                    if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Rank down! {oldRank} -> {playerData.currentRank}");
                }
            }
            
            // Update match statistics
            UpdateMatchStatistics(matchResult);
            
            // Update win streak
            if (matchResult.isWin)
            {
                playerData.winStreak++;
                if (playerData.winStreak > playerData.bestWinStreak)
                {
                    playerData.bestWinStreak = playerData.winStreak;
                }
            }
            else
            {
                playerData.winStreak = 0;
            }
            
            // Check for achievements
            CheckAchievements(matchResult);
            
            // Mark as changed and save
            hasUnsavedChanges = true;
            if (autoSaveOnChanges)
            {
                SavePlayerData();
            }
            
            OnDataUpdated?.Invoke(playerData);
        }
        
        private void LevelUp()
        {
            int oldLevel = playerData.currentLevel;
            int oldXP = playerData.currentXP;
            
            playerData.currentLevel++;
            playerData.currentXP -= playerData.xpToNextLevel;
            playerData.xpToNextLevel = ModularRewardCalculator.Instance.GetXPRequiredForLevel(playerData.currentLevel);
            
            // Give level up rewards
            playerData.rumbleTokens += config.rumbleTokensPerLevel;
            
            OnLevelUp?.Invoke(playerData.currentLevel);
            if (enableDebugLogs) 
            {
                Debug.Log($"[PlayerDataManager] Level up! {oldLevel} -> {playerData.currentLevel}, " +
                         $"XP: {oldXP} -> {playerData.currentXP}/{playerData.xpToNextLevel}");
            }
        }
        
        private void UpdateMatchStatistics(MatchResult matchResult)
        {
            playerData.totalMatches++;
            
            if (matchResult.isWin)
            {
                playerData.totalWins++;
            }
            else
            {
                playerData.totalLosses++;
            }
            
            // Update mode-specific stats
            var modeStats = GetModeStats(matchResult.gameMode);
            modeStats.matches++;
            if (matchResult.isWin) modeStats.wins++;
            else modeStats.losses++;
            modeStats.damageDealt += matchResult.damageDealt;
            modeStats.damageTaken += matchResult.damageTaken;
            
            // Update average match duration
            modeStats.averageMatchDuration = (modeStats.averageMatchDuration * (modeStats.matches - 1) + matchResult.matchDuration) / modeStats.matches;
        }
        
        private PlayerModeStats GetModeStats(GameMode mode)
        {
            return mode switch
            {
                GameMode.Casual => playerData.casualStats,
                GameMode.Competitive => playerData.competitiveStats,
                GameMode.AI => playerData.aiStats,
                _ => playerData.casualStats
            };
        }
        
        private void CheckAchievements(MatchResult matchResult)
        {
            // Check for first win achievement
            if (matchResult.isWin && playerData.totalWins == 1)
            {
                UnlockAchievement("ACH_FIRST_WIN");
            }
            
            // Check for competitive wins achievement
            if (matchResult.gameMode == GameMode.Competitive && matchResult.isWin)
            {
                if (playerData.competitiveStats.wins == 10)
                {
                    UnlockAchievement("ACH_WIN_10_COMP");
                }
            }
            
            // Add more achievement checks here
        }
        
        private void UnlockAchievement(string achievementId)
        {
            if (playerData.unlockedAchievements.Contains(achievementId)) return;
            
            playerData.unlockedAchievements.Add(achievementId);
            OnAchievementUnlocked?.Invoke(achievementId);
            
            if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Achievement unlocked: {achievementId}");
        }
        
        /// <summary>
        /// Get current player data (read-only)
        /// </summary>
        public PlayerProgressionData GetPlayerData()
        {
            return playerData;
        }
        
        /// <summary>
        /// Check if data is loaded
        /// </summary>
        public bool IsDataLoaded()
        {
            return isDataLoaded;
        }
        
        /// <summary>
        /// Get last match rewards for UI display
        /// </summary>
        public MatchRewards? GetLastMatchRewards()
        {
            return lastMatchRewards;
        }
        
        /// <summary>
        /// Automatically submit existing SR to leaderboard when data loads
        /// This ensures manually set SR values in PlayFab appear on leaderboard
        /// </summary>
        private void SubmitExistingSRToLeaderboard()
        {
            if (LeaderboardManager.Instance != null && playerData != null)
            {
                // Check if SR qualifies for leaderboard
                if (LeaderboardManager.Instance.QualifiesForLeaderboard(playerData.competitiveSR))
                {
                    LeaderboardManager.Instance.SubmitScore(playerData.competitiveSR);
                    if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Auto-submitted existing SR {playerData.competitiveSR} to leaderboard");
                }
                else
                {
                    int minSR = LeaderboardManager.Instance.GetMinimumSRForLeaderboard();
                    if (enableDebugLogs) Debug.Log($"[PlayerDataManager] SR {playerData.competitiveSR} below minimum threshold {minSR}, not submitting to leaderboard");
                }
            }
        }
        
        /// <summary>
        /// Force save data (useful for manual saves)
        /// </summary>
        public void ForceSave()
        {
            if (isDataLoaded)
            {
                SavePlayerData();
            }
        }

        /// <summary>
        /// Manually sync rank with SR and save to PlayFab
        /// Useful for debugging or when SR is manually updated in PlayFab dashboard
        /// </summary>
        [ContextMenu("Sync Rank with SR")]
        public void ForceSyncRankWithSR()
        {
            if (isDataLoaded && playerData != null)
            {
                SyncRankWithSR();
                if (enableDebugLogs) Debug.Log($"[PlayerDataManager] Manually synced rank with SR: {playerData.currentRank} (SR: {playerData.competitiveSR})");
            }
        }
        
        /// <summary>
        /// Reset player data (for testing)
        /// </summary>
        [ContextMenu("Reset Player Data")]
        public void ResetPlayerData()
        {
            playerData = new PlayerProgressionData();
            hasUnsavedChanges = true;
            SavePlayerData();
            if (enableDebugLogs) Debug.Log("[PlayerDataManager] Player data reset.");
        }
    }
}
