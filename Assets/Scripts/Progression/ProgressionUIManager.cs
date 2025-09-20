using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace RetroDodge.Progression
{
    /// <summary>
    /// Manages UI display for player progression data
    /// Shows level, rank, currency, and other progression information
    /// </summary>
    public class ProgressionUIManager : MonoBehaviour
    {
        [Header("Level Display")]
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI xpText;
        public Slider xpBar;
        
        [Header("Rank Display")]
        public TextMeshProUGUI rankText;
        public TextMeshProUGUI srText;
        public Image rankIcon;
        
        [Header("Currency Display")]
        public TextMeshProUGUI dodgeCoinsText;
        public TextMeshProUGUI rumbleTokensText;
        
        [Header("Statistics Display")]
        public TextMeshProUGUI totalMatchesText;
        public TextMeshProUGUI winRateText;
        public TextMeshProUGUI winStreakText;
        
        [Header("Notifications")]
        public GameObject levelUpNotification;
        public GameObject rankUpNotification;
        public GameObject achievementNotification;
        public TextMeshProUGUI notificationText;
        
        [Header("Settings")]
        public bool showDetailedStats = true;
        public float notificationDuration = 3f;
        
        private PlayerProgressionData currentData;
        private Coroutine notificationCoroutine;
        
        void Start()
        {
            // Subscribe to events
            PlayerDataManager.OnDataLoaded += OnDataLoaded;
            PlayerDataManager.OnDataUpdated += OnDataUpdated;
            PlayerDataManager.OnLevelUp += OnLevelUp;
            PlayerDataManager.OnRankUp += OnRankUp;
            PlayerDataManager.OnAchievementUnlocked += OnAchievementUnlocked;
            
            // Initialize UI
            UpdateUI();
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            PlayerDataManager.OnDataLoaded -= OnDataLoaded;
            PlayerDataManager.OnDataUpdated -= OnDataUpdated;
            PlayerDataManager.OnLevelUp -= OnLevelUp;
            PlayerDataManager.OnRankUp -= OnRankUp;
            PlayerDataManager.OnAchievementUnlocked -= OnAchievementUnlocked;
        }
        
        private void OnDataLoaded(PlayerProgressionData data)
        {
            currentData = data;
            UpdateUI();
        }
        
        private void OnDataUpdated(PlayerProgressionData data)
        {
            currentData = data;
            UpdateUI();
        }
        
        private void OnLevelUp(int newLevel)
        {
            ShowNotification($"Level Up! You are now level {newLevel}!", levelUpNotification);
        }
        
        private void OnRankUp(string oldRank, string newRank)
        {
            ShowNotification($"Rank Up! {oldRank} → {newRank}!", rankUpNotification);
        }
        
        private void OnAchievementUnlocked(string achievementId)
        {
            // Get achievement name from config
            string achievementName = GetAchievementName(achievementId);
            ShowNotification($"Achievement Unlocked: {achievementName}!", achievementNotification);
        }
        
        private string GetAchievementName(string achievementId)
        {
            if (PlayerDataManager.Instance?.config?.achievementDefinitions != null)
            {
                var achievement = System.Array.Find(PlayerDataManager.Instance.config.achievementDefinitions.ToArray(), 
                    a => a.id == achievementId);
                return achievement?.name ?? achievementId;
            }
            return achievementId;
        }
        
        private void ShowNotification(string message, GameObject notificationPrefab)
        {
            if (notificationPrefab != null)
            {
                // Stop previous notification
                if (notificationCoroutine != null)
                {
                    StopCoroutine(notificationCoroutine);
                }
                
                notificationCoroutine = StartCoroutine(ShowNotificationCoroutine(message, notificationPrefab));
            }
        }
        
        private System.Collections.IEnumerator ShowNotificationCoroutine(string message, GameObject notificationPrefab)
        {
            notificationPrefab.SetActive(true);
            if (notificationText != null)
            {
                notificationText.text = message;
            }
            
            yield return new WaitForSeconds(notificationDuration);
            
            notificationPrefab.SetActive(false);
            notificationCoroutine = null;
        }
        
        private void UpdateUI()
        {
            if (currentData == null) return;
            
            UpdateLevelDisplay();
            UpdateRankDisplay();
            UpdateCurrencyDisplay();
            
            if (showDetailedStats)
            {
                UpdateStatisticsDisplay();
            }
        }
        
        private void UpdateLevelDisplay()
        {
            if (levelText != null)
            {
                levelText.text = $"Level {currentData.currentLevel}";
            }
            
            if (xpText != null)
            {
                xpText.text = $"{currentData.currentXP} / {currentData.xpToNextLevel} XP";
            }
            
            if (xpBar != null)
            {
                float progress = (float)currentData.currentXP / currentData.xpToNextLevel;
                xpBar.value = Mathf.Clamp01(progress);
            }
        }
        
        private void UpdateRankDisplay()
        {
            if (rankText != null)
            {
                rankText.text = currentData.currentRank;
            }
            
            if (srText != null)
            {
                srText.text = $"SR: {currentData.competitiveSR}";
            }
            
            if (rankIcon != null)
            {
                // Set rank icon color based on rank
                var rankInfo = RankingSystem.GetRankFromSR(currentData.competitiveSR);
                rankIcon.color = rankInfo.color;
            }
        }
        
        private void UpdateCurrencyDisplay()
        {
            if (dodgeCoinsText != null)
            {
                dodgeCoinsText.text = currentData.dodgeCoins.ToString();
            }
            
            if (rumbleTokensText != null)
            {
                rumbleTokensText.text = currentData.rumbleTokens.ToString();
            }
        }
        
        private void UpdateStatisticsDisplay()
        {
            if (totalMatchesText != null)
            {
                totalMatchesText.text = $"Matches: {currentData.totalMatches}";
            }
            
            if (winRateText != null)
            {
                float winRate = currentData.totalMatches > 0 ? 
                    (float)currentData.totalWins / currentData.totalMatches * 100f : 0f;
                winRateText.text = $"Win Rate: {winRate:F1}%";
            }
            
            if (winStreakText != null)
            {
                winStreakText.text = $"Streak: {currentData.winStreak}";
            }
        }
        
        /// <summary>
        /// Toggle detailed statistics display
        /// </summary>
        public void ToggleDetailedStats()
        {
            showDetailedStats = !showDetailedStats;
            UpdateUI();
        }
        
        /// <summary>
        /// Force refresh UI (useful for manual updates)
        /// </summary>
        public void RefreshUI()
        {
            if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsDataLoaded())
            {
                currentData = PlayerDataManager.Instance.GetPlayerData();
                UpdateUI();
            }
        }
    }
}

