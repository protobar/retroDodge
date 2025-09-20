using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RetroDodge.Progression
{
    /// <summary>
    /// UI component for individual leaderboard entries
    /// Displays rank, player name, rank icon, rank name, and SR
    /// </summary>
    public class LeaderboardEntryUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private Image rankIconImage;
        [SerializeField] private TMP_Text rankNameText;
        [SerializeField] private TMP_Text srText;
        [SerializeField] private Image backgroundImage;
        
        [Header("Configuration")]
        [SerializeField] private RankIconConfiguration rankIconConfig;
        [SerializeField] private bool useSmallIcon = false;
        [SerializeField] private bool useLargeIcon = false;
        
        [Header("Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private LeaderboardEntry currentEntry;
        
        /// <summary>
        /// Setup the leaderboard entry with data
        /// </summary>
        public void SetupEntry(LeaderboardEntry entry)
        {
            currentEntry = entry;
            
            // Rank (left-aligned with # prefix)
            if (rankText != null)
            {
                rankText.text = entry.GetFormattedRank();
                rankText.alignment = TextAlignmentOptions.Left;
            }
            
            // Player name (left-aligned, truncated)
            if (playerNameText != null)
            {
                playerNameText.text = entry.GetTruncatedPlayerName(15);
                playerNameText.alignment = TextAlignmentOptions.Left;
            }
            
            // Rank icon (custom image)
            if (rankIconImage != null)
            {
                Sprite rankIconSprite = GetRankIconSprite(entry.rankInfo?.rankName);
                if (rankIconSprite != null)
                {
                    rankIconImage.sprite = rankIconSprite;
                    rankIconImage.gameObject.SetActive(true);
                    
                    // Apply icon color and size from configuration
                    var rankIconData = rankIconConfig?.GetRankIconData(entry.rankInfo?.rankName);
                    if (rankIconData != null)
                    {
                        rankIconImage.color = rankIconData.iconColor;
                        rankIconImage.transform.localScale = Vector3.one * rankIconData.sizeMultiplier;
                    }
                }
                else
                {
                    rankIconImage.gameObject.SetActive(false);
                }
            }
            
            // Rank name (color-coded)
            if (rankNameText != null)
            {
                rankNameText.text = entry.rankInfo?.rankName ?? "Unknown";
                rankNameText.alignment = TextAlignmentOptions.Left;
                
                // Apply rank color
                if (entry.rankInfo != null)
                {
                    rankNameText.color = entry.rankInfo.color;
                }
            }
            
            // SR (right-aligned with formatting)
            if (srText != null)
            {
                srText.text = entry.GetFormattedSR();
                srText.alignment = TextAlignmentOptions.Right;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[LeaderboardEntryUI] Setup entry: {entry.GetFormattedRank()} {entry.GetTruncatedPlayerName()} {entry.GetRankIcon()} {entry.rankInfo?.rankName} {entry.GetFormattedSR()}");
            }
        }
        
        /// <summary>
        /// Highlight the current player's entry
        /// </summary>
        public void HighlightPlayer(Color highlightColor)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = highlightColor;
            }
            
            // Make text more prominent for current player
            if (rankText != null) rankText.fontStyle = FontStyles.Bold;
            if (playerNameText != null) playerNameText.fontStyle = FontStyles.Bold;
            if (rankNameText != null) rankNameText.fontStyle = FontStyles.Bold;
            if (srText != null) srText.fontStyle = FontStyles.Bold;
        }
        
        /// <summary>
        /// Set row background color for alternating rows
        /// </summary>
        public void SetRowColor(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }
        
        /// <summary>
        /// Get the current entry data
        /// </summary>
        public LeaderboardEntry GetEntry()
        {
            return currentEntry;
        }
        
        /// <summary>
        /// Check if this entry represents the current player
        /// </summary>
        public bool IsCurrentPlayer(string playerId)
        {
            return currentEntry != null && currentEntry.playerId == playerId;
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
                return rankIconConfig.GetRankIcon(rankName, useSmallIcon, useLargeIcon);
            }
            
            // Fallback to Resources folder loading
            string iconPath = $"RankIcons/{rankName}";
            Sprite rankIcon = Resources.Load<Sprite>(iconPath);
            
            if (rankIcon == null && enableDebugLogs)
            {
                Debug.LogWarning($"[LeaderboardEntryUI] Rank icon not found: {iconPath}. Consider using RankIconConfiguration.");
            }
            
            return rankIcon;
        }
    }
}
