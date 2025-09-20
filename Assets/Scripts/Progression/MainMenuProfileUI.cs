using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace RetroDodge.Progression
{
    /// <summary>
    /// UI component for displaying player profile in main menu
    /// Shows player stats, currency, and progression info
    /// </summary>
    public class MainMenuProfileUI : MonoBehaviour
    {
        [Header("Profile Panel")]
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button closeProfileButton;

        [Header("Player Info - Arrays for Multiple Display Areas")]
        [SerializeField] private TMP_Text[] playerNameTexts;
        [SerializeField] private TMP_Text[] playerLevelTexts;
        [SerializeField] private TMP_Text[] playerRankTexts;
        [SerializeField] private Image[] rankIconImages;
        [SerializeField] private Image[] rankBackgroundImages;
        [SerializeField] private Slider[] xpProgressSliders;
        [SerializeField] private TMP_Text[] xpProgressTexts;

        [Header("Currency")]
        [SerializeField] private TMP_Text dodgeCoinsText;
        [SerializeField] private TMP_Text rumbleTokensText;
        [SerializeField] private Button shopButton;

        [Header("Quick Stats - Arrays for Multiple Display Areas")]
        [SerializeField] private TMP_Text[] totalMatchesTexts;
        [SerializeField] private TMP_Text[] totalWinsTexts;
        [SerializeField] private TMP_Text[] winRateTexts;
        [SerializeField] private TMP_Text[] currentStreakTexts;

        [Header("Competitive Stats - Arrays for Multiple Display Areas")]
        [SerializeField] private GameObject[] competitiveSections;
        [SerializeField] private TMP_Text[] skillRatingTexts;
        [SerializeField] private TMP_Text[] placementMatchesTexts;
        [SerializeField] private Button competitiveButton;

        [Header("Achievements")]
        [SerializeField] private Transform achievementsContainer;
        [SerializeField] private GameObject achievementPrefab;
        [SerializeField] private TMP_Text achievementsCountText;
        
        [Header("Detailed Stats Panel")]
        [SerializeField] private GameObject detailedStatsPanel;
        [SerializeField] private Button detailedStatsButton;
        [SerializeField] private Button closeDetailedStatsButton;

        [Header("Rank Icon Configuration")]
        [SerializeField] private RankIconConfiguration rankIconConfig;
        [SerializeField] private bool useSmallIcons = false;
        [SerializeField] private bool useLargeIcons = false;
        
        [Header("Settings")]
        [SerializeField] private bool autoUpdate = true;
        [SerializeField] private float updateInterval = 10f; // Increased to 10 seconds
        [SerializeField] private bool enableDebugLogs = true;
        
        private float lastUpdateTime;

        void Start()
        {
            // Setup button listeners
            if (profileButton != null)
                profileButton.onClick.AddListener(ToggleProfile);
            
            if (closeProfileButton != null)
                closeProfileButton.onClick.AddListener(CloseProfile);
            
            if (shopButton != null)
                shopButton.onClick.AddListener(OpenShop);
            
            if (competitiveButton != null)
                competitiveButton.onClick.AddListener(OpenCompetitive);
            
            if (detailedStatsButton != null)
                detailedStatsButton.onClick.AddListener(ToggleDetailedStats);
            
            if (closeDetailedStatsButton != null)
                closeDetailedStatsButton.onClick.AddListener(CloseDetailedStats);

            // Subscribe to PlayerDataManager events for real-time updates
            PlayerDataManager.OnDataUpdated += OnPlayerDataUpdated;
            PlayerDataManager.OnDataLoaded += OnPlayerDataLoaded;

            // Hide panels initially
            if (profilePanel != null)
                profilePanel.SetActive(false);
            
            if (detailedStatsPanel != null)
                detailedStatsPanel.SetActive(false);

            // Initial update
            UpdateProfileDisplay();
        }

        void Update()
        {
            // Auto-update profile if enabled (with longer interval)
            if (autoUpdate && Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateProfileDisplay();
                lastUpdateTime = Time.time;
                
                // Also trigger a save to PlayFab to keep data synchronized
                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.SavePlayerData();
                }
            }
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            PlayerDataManager.OnDataUpdated -= OnPlayerDataUpdated;
            PlayerDataManager.OnDataLoaded -= OnPlayerDataLoaded;
        }
        
        /// <summary>
        /// Event handler for when player data is updated (e.g., after a match)
        /// </summary>
        private void OnPlayerDataUpdated(PlayerProgressionData data)
        {
            if (enableDebugLogs) Debug.Log("[MainMenuProfileUI] Player data updated, refreshing display");
            UpdateProfileDisplay();
        }
        
        /// <summary>
        /// Event handler for when player data is initially loaded
        /// </summary>
        private void OnPlayerDataLoaded(PlayerProgressionData data)
        {
            if (enableDebugLogs) Debug.Log("[MainMenuProfileUI] Player data loaded, refreshing display");
            UpdateProfileDisplay();
        }
        
        /// <summary>
        /// Manually refresh the profile display
        /// Call this when returning from a match to ensure data is up-to-date
        /// </summary>
        [ContextMenu("Force Refresh Profile")]
        public void ForceRefreshProfile()
        {
            if (enableDebugLogs) Debug.Log("[MainMenuProfileUI] Force refreshing profile display");
            UpdateProfileDisplay();
            
            // Also trigger a save to PlayFab to keep data synchronized
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.SavePlayerData();
            }
        }

        /// <summary>
        /// Toggle profile panel visibility
        /// </summary>
        public void ToggleProfile()
        {
            if (profilePanel != null)
            {
                bool isActive = profilePanel.activeInHierarchy;
                profilePanel.SetActive(!isActive);
                
                if (!isActive)
                {
                    UpdateProfileDisplay();
                }
            }
        }

        /// <summary>
        /// Close profile panel
        /// </summary>
        public void CloseProfile()
        {
            if (profilePanel != null)
                profilePanel.SetActive(false);
        }

        /// <summary>
        /// Update the profile display with current data
        /// </summary>
        public void UpdateProfileDisplay()
        {
            if (PlayerDataManager.Instance == null) 
            {
                Debug.LogWarning("[MainMenuProfileUI] PlayerDataManager not found");
                return;
            }

            var playerData = PlayerDataManager.Instance.GetPlayerData();
            if (playerData == null) 
            {
                Debug.LogWarning("[MainMenuProfileUI] Player data is null");
                return;
            }

            UpdatePlayerInfo(playerData);
            UpdateCurrency(playerData);
            UpdateQuickStats(playerData);
            UpdateCompetitiveStats(playerData);
            UpdateAchievements(playerData);

            if (enableDebugLogs)
            {
                Debug.Log($"[MainMenuProfileUI] Profile display updated - Level: {playerData.currentLevel}, XP: {playerData.currentXP}/{playerData.xpToNextLevel}");
            }
        }
        
        
        
        /// <summary>
        /// Toggle detailed stats panel
        /// </summary>
        private void ToggleDetailedStats()
        {
            if (detailedStatsPanel != null)
            {
                bool isActive = detailedStatsPanel.activeInHierarchy;
                detailedStatsPanel.SetActive(!isActive);
                
                if (!isActive)
                {
                    UpdateProfileDisplay(); // Refresh data when opening
                }
            }
        }

        /// <summary>
        /// Close detailed stats panel
        /// </summary>
        private void CloseDetailedStats()
        {
            if (detailedStatsPanel != null)
                detailedStatsPanel.SetActive(false);
        }

        private void UpdatePlayerInfo(PlayerProgressionData data)
        {
            string playerName = Photon.Pun.PhotonNetwork.NickName ?? "Player";
            string playerLevel = $"{data.currentLevel}";
            var rankInfo = RankingSystem.GetRankFromSR(data.competitiveSR, ModularRewardCalculator.Instance?.config);
            string playerRank = rankInfo.rankName;

            // Update all player name texts
            UpdateTextArray(playerNameTexts, playerName);

            // Update all player level texts
            UpdateTextArray(playerLevelTexts, playerLevel);

            // Update all player rank texts
            UpdateTextArray(playerRankTexts, playerRank);

            // XP progress
            UpdateXPProgress(data);

            // Rank visual
            UpdateRankVisual(data);
        }

        private void UpdateXPProgress(PlayerProgressionData data)
        {
            if (ModularRewardCalculator.Instance?.config == null) return;

            int xpNeeded = data.xpToNextLevel;
            float xpProgress = xpNeeded > 0 ? (float)data.currentXP / xpNeeded : 1f;
            string xpText = data.currentLevel >= ModularRewardCalculator.Instance.config.maxLevel ? 
                "MAX LEVEL" : $"{data.currentXP:N0} / {xpNeeded:N0} XP";

            // Update all XP progress sliders
            UpdateSliderArray(xpProgressSliders, xpProgress);

            // Update all XP progress texts
            UpdateTextArray(xpProgressTexts, xpText);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MainMenuProfileUI] XP Progress: {data.currentXP}/{xpNeeded} (Level {data.currentLevel})");
            }
        }

        private void UpdateRankVisual(PlayerProgressionData data)
        {
            if (ModularRewardCalculator.Instance?.config == null) return;

            var rankInfo = RankingSystem.GetRankFromSR(data.competitiveSR, ModularRewardCalculator.Instance.config);
            
            // Update all rank icon images with custom sprites
            UpdateRankIconSprites(rankIconImages, rankInfo.rankName);

            // Update all rank background images
            var bgColor = rankInfo.color;
            bgColor.a = 0.3f;
            UpdateImageColorArray(rankBackgroundImages, bgColor);
        }

        private void UpdateCurrency(PlayerProgressionData data)
        {
            if (dodgeCoinsText != null)
            {
                dodgeCoinsText.text = data.dodgeCoins.ToString("N0");
            }

            if (rumbleTokensText != null)
            {
                rumbleTokensText.text = data.rumbleTokens.ToString("N0");
            }
        }

        private void UpdateQuickStats(PlayerProgressionData data)
        {
            string totalMatches = data.totalMatches.ToString();
            string totalWins = data.totalWins.ToString();
            float winRate = data.totalMatches > 0 ? 
                (float)data.totalWins / data.totalMatches * 100f : 0f;
            string winRateText = $"{winRate:F1}%";
            string currentStreak = data.winStreak.ToString();

            // Update all arrays
            UpdateTextArray(totalMatchesTexts, totalMatches);
            UpdateTextArray(totalWinsTexts, totalWins);
            UpdateTextArray(this.winRateTexts, winRateText);
            UpdateTextArray(currentStreakTexts, currentStreak);
        }

        private void UpdateCompetitiveStats(PlayerProgressionData data)
        {
            bool showCompetitive = data.isRanked || data.placementMatchesLeft > 0;
            string skillRating = data.competitiveSR.ToString();
            string placementText = data.placementMatchesLeft > 0 ? 
                $"{data.placementMatchesLeft} placement matches left" : "Ranked";

            // Update all competitive sections
            UpdateGameObjectArray(competitiveSections, showCompetitive);

            // Update all skill rating texts
            UpdateTextArray(skillRatingTexts, skillRating);

            // Update all placement matches texts
            UpdateTextArray(placementMatchesTexts, placementText);
        }

        private void UpdateAchievements(PlayerProgressionData data)
        {
            if (achievementsCountText != null)
            {
                achievementsCountText.text = $"{data.completedAchievements.Count} / {data.unlockedAchievements.Count}";
            }

            // Update achievement list (simplified for now)
            if (achievementsContainer != null && achievementPrefab != null)
            {
                // Clear existing achievements
                foreach (Transform child in achievementsContainer)
                {
                    Destroy(child.gameObject);
                }

                // Show first few achievements
                int maxAchievements = Mathf.Min(5, data.completedAchievements.Count);
                for (int i = 0; i < maxAchievements; i++)
                {
                    var achievementObj = Instantiate(achievementPrefab, achievementsContainer);
                    var achievementText = achievementObj.GetComponentInChildren<TMP_Text>();
                    if (achievementText != null)
                    {
                        achievementText.text = data.completedAchievements[i];
                    }
                }
            }
        }

        private void OpenShop()
        {
            if (enableDebugLogs)
                Debug.Log("[MainMenuProfileUI] Shop clicked - implement shop functionality");
            
            // TODO: Implement shop functionality
        }

        private void OpenCompetitive()
        {
            if (enableDebugLogs)
                Debug.Log("[MainMenuProfileUI] Competitive clicked - implement competitive matchmaking");
            
            // TODO: Implement competitive matchmaking
        }

        /// <summary>
        /// Force refresh the profile display
        /// </summary>
        [ContextMenu("Refresh Profile")]
        public void RefreshProfile()
        {
            UpdateProfileDisplay();
        }

        #region Array Helper Methods

        /// <summary>
        /// Update all text components in an array with the same value
        /// </summary>
        private void UpdateTextArray(TMP_Text[] textArray, string value)
        {
            if (textArray == null) return;
            
            foreach (var text in textArray)
            {
                if (text != null)
                    text.text = value;
            }
        }

        /// <summary>
        /// Update all slider components in an array with the same value
        /// </summary>
        private void UpdateSliderArray(Slider[] sliderArray, float value)
        {
            if (sliderArray == null) return;
            
            foreach (var slider in sliderArray)
            {
                if (slider != null)
                    slider.value = value;
            }
        }

        /// <summary>
        /// Update all image components in an array with the same color
        /// </summary>
        private void UpdateImageColorArray(Image[] imageArray, Color color)
        {
            if (imageArray == null) return;
            
            foreach (var image in imageArray)
            {
                if (image != null)
                    image.color = color;
            }
        }

        /// <summary>
        /// Update all gameobject components in an array with the same active state
        /// </summary>
        private void UpdateGameObjectArray(GameObject[] gameObjectArray, bool active)
        {
            if (gameObjectArray == null) return;
            
            foreach (var gameObject in gameObjectArray)
            {
                if (gameObject != null)
                    gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Update all rank icon images with custom sprites
        /// </summary>
        private void UpdateRankIconSprites(Image[] imageArray, string rankName)
        {
            if (imageArray == null || string.IsNullOrEmpty(rankName)) return;
            
            Sprite rankIconSprite = GetRankIconSprite(rankName);
            var rankIconData = rankIconConfig?.GetRankIconData(rankName);
            
            foreach (var image in imageArray)
            {
                if (image != null)
                {
                    if (rankIconSprite != null)
                    {
                        image.sprite = rankIconSprite;
                        image.gameObject.SetActive(true);
                        
                        // Apply icon color and size from configuration
                        if (rankIconData != null)
                        {
                            image.color = rankIconData.iconColor;
                            image.transform.localScale = Vector3.one * rankIconData.sizeMultiplier;
                        }
                    }
                    else
                    {
                        image.gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Get rank icon sprite based on rank name
        /// </summary>
        private Sprite GetRankIconSprite(string rankName)
        {
            if (string.IsNullOrEmpty(rankName)) return null;
            
            // Use RankIconConfiguration if available
            if (rankIconConfig != null)
            {
                return rankIconConfig.GetRankIcon(rankName, useSmallIcons, useLargeIcons);
            }
            
            // Fallback to Resources folder loading
            string iconPath = $"RankIcons/{rankName}";
            Sprite rankIcon = Resources.Load<Sprite>(iconPath);
            
            if (rankIcon == null && enableDebugLogs)
            {
                Debug.LogWarning($"[MainMenuProfileUI] Rank icon not found: {iconPath}. Consider using RankIconConfiguration.");
            }
            
            return rankIcon;
        }

        #endregion
    }
}


